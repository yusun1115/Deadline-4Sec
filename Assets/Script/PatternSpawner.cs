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

        [Header("Difficulty unlock by run time")]
        [SerializeField, Min(0f)] private float mediumStartsAtSeconds = 20f;
        [SerializeField, Min(0f)] private float hardStartsAtSeconds = 45f;

        [Header("Repeatable selection")]
        [SerializeField] private bool useFixedSeed;
        [SerializeField] private int fixedSeed = 12345;
        [SerializeField] private bool showDebugLogs;

        [Header("Pickup rarity (coins always appear)")]
        [SerializeField, Range(0f, 1f)] private float powerUpChance = 0.3f;
        [SerializeField, Range(0f, 1f)] private float giftBoxChance = 0.18f;
        [SerializeField, Min(0)] private int maxPowerUpsPerPattern = 1;

        [Header("Graybox floor per pattern")]
        [SerializeField] private bool createFloorUnderPatterns = true;
        [SerializeField, Min(1f)] private float floorWidth = 8.6f;
        [SerializeField, Min(0.05f)] private float floorThickness = 0.2f;
        [SerializeField] private float floorTopY;
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Renderer[] existingFloorRenderers;
        [SerializeField] private bool showFloorDebugLogs;

        private readonly Queue<SpawnedPattern> activePatterns = new Queue<SpawnedPattern>();
        private readonly List<CoursePattern> candidates = new List<CoursePattern>();
        private readonly List<float> opportunityPositions = new List<float>();
        private System.Random random;
        // Separate stream so pickup rolls never change which patterns a seed produces.
        private System.Random pickupRandom;
        private CoursePattern previousPrefab;
        private CoursePattern.PatternCategory previousCategory;
        private int categoryStreak;
        private float nextStartZ;
        private bool floorMaterialWarningShown;

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
            pickupRandom = useFixedSeed ? new System.Random(fixedSeed + 7919) : new System.Random();
            nextStartZ = transform.position.z;
            ApplyExistingFloorMaterials();
        }

        private void Start()
        {
            if (gameTimer != null && gameTimer.IsRunning)
                EnsurePatternsAhead();
        }

        private void Update()
        {
            if (player == null || gameTimer == null || !gameTimer.IsRunning)
                return;

            CleanupPassedPatterns();
            EnsurePatternsAhead();
        }

        public void PrepareOpeningPatterns(float expectedGoZ)
        {
            if (activePatterns.Count > 0 || player == null)
                return;

            // Show the opening course throughout the intro, with at least 1.5s
            // to read its first threat after Go (before its local object offset).
            float speed = runManager != null ? runManager.BaseForwardSpeed : 10.2f;
            nextStartZ = Mathf.Max(nextStartZ, expectedGoZ + speed * 1.5f);
            if (createFloorUnderPatterns)
            {
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Opening Runway";
                float start = player.transform.position.z - 5f;
                floor.transform.position = new Vector3(transform.position.x,
                    floorTopY - floorThickness * 0.5f, (start + nextStartZ) * 0.5f);
                floor.transform.localScale = new Vector3(floorWidth, floorThickness, nextStartZ - start);
                floor.transform.SetParent(transform, true);
                ApplyFloorMaterial(floor.GetComponent<Renderer>());
            }
            // Initial gap validation is measured from Go, excluding cinematic travel.
            openingGoZ = expectedGoZ;
            EnsurePatternsAhead();
        }

        private float? openingGoZ;

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
            float speed = runManager != null ? Mathf.Max(runManager.BaseForwardSpeed, runManager.CurrentForwardSpeed) : 10.2f;
            float currentTime = runManager != null ? runManager.CurrentRunTime : 0f;
            float arrivalTime = currentTime +
                Mathf.Max(0f, nextStartZ - player.transform.position.z) / speed;

            int minimumDifficulty = arrivalTime >= hardStartsAtSeconds ? 1 : 0;
            // Homing can reach queued patterns much sooner than an auto-run
            // estimate. Unlock new tiers only after the actual run threshold;
            // retain the estimate to phase Easy out of the queued course.
            int maximumDifficulty = currentTime >= hardStartsAtSeconds ? 2 :
                currentTime >= mediumStartsAtSeconds ? 1 : 0;

            CollectCandidates(minimumDifficulty, maximumDifficulty, true);
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
            next.CollectOpportunityPositions(opportunityPositions);
            if (opportunityPositions.Count == 0)
                return false;
            float speed = runManager != null ? Mathf.Max(runManager.BaseForwardSpeed, runManager.CurrentForwardSpeed) : 10.2f;
            float maximumGap = maxOpportunityGapSeconds * speed;
            if (previousPrefab != null)
            {
                if (!previousPrefab.TryGetOpportunityRange(out _, out float previousLast) ||
                    previousPrefab.PatternLength - previousLast + opportunityPositions[0] > maximumGap)
                    return false;
            }
            else if (Mathf.Max(0f, nextStartZ - (openingGoZ ?? player.transform.position.z)) +
                     opportunityPositions[0] > maximumGap)
            {
                return false;
            }

            for (int i = 1; i < opportunityPositions.Count; i++)
                if (opportunityPositions[i] - opportunityPositions[i - 1] > maximumGap)
                    return false;
            return true;
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
            ThinOutPickups(instance);
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

        // Authored pickups are candidates, not guarantees: each power-up and gift
        // box survives with its chance (own seeded stream, so fixed seeds replay
        // the same course), and at most one power-up stays per pattern.
        private void ThinOutPickups(CoursePattern instance)
        {
            int keptPowerUps = 0;
            foreach (PowerUpPickup pickup in instance.GetComponentsInChildren<PowerUpPickup>(true))
            {
                bool keep = keptPowerUps < maxPowerUpsPerPattern && pickupRandom.NextDouble() < powerUpChance;
                if (keep)
                    keptPowerUps++;
                else
                    pickup.gameObject.SetActive(false);
            }
            foreach (GiftBoxPickup gift in instance.GetComponentsInChildren<GiftBoxPickup>(true))
                if (pickupRandom.NextDouble() >= giftBoxChance)
                    gift.gameObject.SetActive(false);
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
            ApplyFloorMaterial(floor.GetComponent<Renderer>());
        }

        private void ApplyExistingFloorMaterials()
        {
            if (existingFloorRenderers == null)
                return;
            foreach (Renderer floorRenderer in existingFloorRenderers)
                ApplyFloorMaterial(floorRenderer);
        }

        private void ApplyFloorMaterial(Renderer floorRenderer)
        {
            if (floorRenderer == null)
                return;
            if (floorMaterial == null)
            {
                if (!floorMaterialWarningShown)
                {
                    floorMaterialWarningShown = true;
                    Debug.LogWarning("Floor Material Missing");
                }
                return;
            }

            floorRenderer.sharedMaterial = floorMaterial;
            if (showFloorDebugLogs)
                Debug.Log("Floor Material Applied: " + floorMaterial.name +
                    " -> [" + floorRenderer.gameObject.name + "]");
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
