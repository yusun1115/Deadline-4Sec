using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadline4Sec
{
    [DefaultExecutionOrder(200)]
    public sealed class PatternSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController player;
        [SerializeField] private RunManager runManager;
        [SerializeField] private GameTimer gameTimer;
        [SerializeField] private CoursePattern[] patternPrefabs;

        [Header("Streaming")]
        [SerializeField, Min(1)] private int patternsAhead = 4;
        [SerializeField, Min(0f)] private float cleanupDistance = 25f;
        [SerializeField, Range(0.5f, 4f)] private float maxOpportunityGapSeconds = 3.5f;

        [Header("Difficulty by expected arrival time")]
        [SerializeField, Min(0f)] private float mediumStartsAtSeconds = 20f;
        [SerializeField, Min(0f)] private float hardStartsAtSeconds = 45f;

        [Header("Repeatable selection")]
        [SerializeField] private bool useFixedSeed;
        [SerializeField] private int fixedSeed = 12345;
        [SerializeField] private bool showDebugLogs;

        [Header("Graybox floor per pattern")]
        [SerializeField] private bool createFloorUnderPatterns = true;
        [SerializeField, Min(1f)] private float floorWidth = 8.6f;
        [SerializeField, Min(0.05f)] private float floorThickness = 0.2f;
        [SerializeField] private float floorTopY;

        private readonly Queue<SpawnedPattern> activePatterns = new Queue<SpawnedPattern>();
        private readonly List<CoursePattern> candidates = new List<CoursePattern>();
        private System.Random random;
        private CoursePattern previousPrefab;
        private CoursePattern.PatternCategory previousCategory;
        private int categoryStreak;
        private float nextStartZ;

        private struct SpawnedPattern
        {
            public CoursePattern Instance;
            public float EndZ;
        }

        private void Awake()
        {
            if (player == null)
                player = FindFirstObjectByType<PlayerController>();
            if (runManager == null)
                runManager = FindFirstObjectByType<RunManager>();
            if (gameTimer == null)
                gameTimer = FindFirstObjectByType<GameTimer>();
            random = useFixedSeed ? new System.Random(fixedSeed) : new System.Random();
            nextStartZ = transform.position.z;
        }

        private void Start()
        {
            EnsurePatternsAhead();
        }

        private void Update()
        {
            if (player == null || (gameTimer != null && gameTimer.IsGameOver))
                return;

            CleanupPassedPatterns();
            EnsurePatternsAhead();
        }

        private void CleanupPassedPatterns()
        {
            while (activePatterns.Count > 0 &&
                   player.transform.position.z > activePatterns.Peek().EndZ + cleanupDistance)
            {
                SpawnedPattern passed = activePatterns.Dequeue();
                if (passed.Instance != null)
                    Destroy(passed.Instance.gameObject);
            }
        }

        private void EnsurePatternsAhead()
        {
            if (player == null)
                return;

            int ahead = 0;
            foreach (SpawnedPattern pattern in activePatterns)
                if (pattern.EndZ > player.transform.position.z)
                    ahead++;

            while (ahead < patternsAhead)
            {
                CoursePattern prefab = ChooseNextPattern();
                if (prefab == null)
                {
                    Debug.LogError("PatternSpawner: no compatible Pattern Prefab is available.");
                    break;
                }

                SpawnPattern(prefab);
                ahead++;
            }
        }

        private CoursePattern ChooseNextPattern()
        {
            float speed = runManager != null ? Mathf.Max(0.1f, runManager.CurrentForwardSpeed) : 10.2f;
            float currentTime = runManager != null ? runManager.CurrentRunTime : 0f;
            float arrivalTime = currentTime +
                Mathf.Max(0f, nextStartZ - player.transform.position.z) / speed;

            int minimumDifficulty = arrivalTime >= hardStartsAtSeconds ? 1 : 0;
            int maximumDifficulty = arrivalTime >= hardStartsAtSeconds ? 2 :
                arrivalTime >= mediumStartsAtSeconds ? 1 : 0;

            CollectCandidates(minimumDifficulty, maximumDifficulty, true);
            if (candidates.Count == 0)
                CollectCandidates(minimumDifficulty, maximumDifficulty, false);
            // A misconfigured set should still keep the course going when a
            // compatible lower-tier Pattern exists.
            if (candidates.Count == 0)
                CollectCandidates(0, maximumDifficulty, false);

            return candidates.Count == 0 ? null : candidates[random.Next(candidates.Count)];
        }

        private void CollectCandidates(int minimumDifficulty, int maximumDifficulty,
            bool avoidCategoryStreak)
        {
            candidates.Clear();
            if (patternPrefabs == null)
                return;

            foreach (CoursePattern prefab in patternPrefabs)
            {
                if (prefab == null || prefab == previousPrefab || prefab.PatternLength <= 0f)
                    continue;
                int difficulty = (int)prefab.Difficulty;
                if (difficulty < minimumDifficulty || difficulty > maximumDifficulty)
                    continue;
                if (previousPrefab != null && !StatesConnect(previousPrefab.ExitState, prefab.EntryState))
                    continue;
                if (avoidCategoryStreak && categoryStreak >= 2 &&
                    prefab.Category == previousCategory)
                    continue;
                if (!OpportunityGapIsSafe(prefab))
                    continue;
                candidates.Add(prefab);
            }
        }

        private bool OpportunityGapIsSafe(CoursePattern next)
        {
            if (previousPrefab == null ||
                !previousPrefab.TryGetOpportunityRange(out _, out float previousLast) ||
                !next.TryGetOpportunityRange(out float nextFirst, out _))
                return true;

            float speed = runManager != null ? Mathf.Max(0.1f, runManager.CurrentForwardSpeed) : 10.2f;
            float gap = previousPrefab.PatternLength - previousLast + nextFirst;
            return gap / speed <= maxOpportunityGapSeconds;
        }

        private static bool StatesConnect(CoursePattern.TravelState exitState,
            CoursePattern.TravelState entryState)
        {
            return exitState == CoursePattern.TravelState.Any ||
                entryState == CoursePattern.TravelState.Any || exitState == entryState;
        }

        private void SpawnPattern(CoursePattern prefab)
        {
            float startZ = nextStartZ;
            CoursePattern instance = Instantiate(prefab,
                new Vector3(transform.position.x, transform.position.y, startZ),
                transform.rotation, transform);
            instance.name = prefab.name;
            if (createFloorUnderPatterns)
                AddGrayboxFloor(instance, startZ);

            nextStartZ += prefab.PatternLength;
            activePatterns.Enqueue(new SpawnedPattern { Instance = instance, EndZ = nextStartZ });
            if (prefab.Category == previousCategory && previousPrefab != null)
                categoryStreak++;
            else
                categoryStreak = 1;
            previousCategory = prefab.Category;
            previousPrefab = prefab;

            if (showDebugLogs)
                Debug.Log("Spawn Pattern: " + prefab.name + " | " + prefab.Difficulty);
        }

        private void AddGrayboxFloor(CoursePattern instance, float startZ)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Graybox Floor";
            floor.transform.position = new Vector3(instance.transform.position.x,
                floorTopY - floorThickness * 0.5f,
                startZ + instance.PatternLength * 0.5f);
            floor.transform.localScale = new Vector3(floorWidth, floorThickness,
                instance.PatternLength);
            floor.transform.SetParent(instance.transform, true);
        }

        private void OnValidate()
        {
            hardStartsAtSeconds = Mathf.Max(mediumStartsAtSeconds, hardStartsAtSeconds);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position - transform.right * floorWidth * 0.5f,
                transform.position + transform.right * floorWidth * 0.5f);
        }
    }
}
