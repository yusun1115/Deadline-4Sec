using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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

        // Areas (2026-11-06): with a catalog, the pattern pool, the signature
        // section and the floor come from the area being built; transitions
        // connect the areas. Without one this spawner works from patternPrefabs.
        [Header("Areas (empty: Resources/Areas/AreaCatalog)")]
        [SerializeField] private AreaCatalog areaCatalog;

        // Air Route (2026-10-16): a Ground → Upper entry pattern (air-enemy chain
        // up to the deck, lower route kept below), 1–2 Upper → Upper runs, then an
        // Upper → Ground return. It appears only after airRouteStartsAtSeconds and
        // after a few ordinary patterns, never back to back.
        [Header("Air Route")]
        [SerializeField, Min(0f)] private float airRouteStartsAtSeconds = 25f;
        [SerializeField, Min(1)] private int groundPatternsBetweenAirRoutesMin = 5;
        [SerializeField, Min(1)] private int groundPatternsBetweenAirRoutesMax = 8;
        [SerializeField, Min(0)] private int upperPatternsMin = 1;
        [SerializeField, Min(0)] private int upperPatternsMax = 2;

        // Wall Run (2026-10-18): a special section like the air route. It appears
        // only after wallRunStartsAtSeconds, every few ordinary patterns, and
        // never right after another special section (air route or wall run):
        // at least normalPatternsAfterSpecial ordinary patterns come between.
        [Header("Wall Run")]
        [SerializeField, Min(0f)] private float wallRunStartsAtSeconds = 45f;
        [SerializeField, Min(1)] private int patternsBetweenWallRunsMin = 6;
        [SerializeField, Min(1)] private int patternsBetweenWallRunsMax = 10;
        [FormerlySerializedAs("normalPatternsAfterSpecial")]
        [SerializeField, Min(0)] private int minNormalPatternsBetweenSpecial = 3;

        // Pacing (2026-11-08): in an area the next pattern is the one closest to
        // the target of the current pacing block. The difficulty tiers only decide
        // what is unlocked yet; Classic and Deadline build the same course.
        [Header("Pacing (areas)")]
        [SerializeField, Range(1, 8)] private int recentPatternMemory = 3;
        [SerializeField, Range(1, 5)] private int highIntensityFrom = 4;
        [SerializeField, Min(1)] private int maxHighIntensityInARow = 2;
        [Tooltip("A signature section waits for a beat of the curve at least this strong.")]
        [SerializeField, Range(1, 5)] private int specialFromTargetIntensity = 3;

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
        // Air route spacing has its own stream too: adding air routes must not
        // change which ordinary patterns a seed produces before the first one.
        private System.Random routeRandom;
        private int patternsSinceAirRoute;
        private int nextAirRouteGap;
        private int upperPatternsInRoute;
        private System.Random wallRunRandom;
        private int patternsSinceWallRun;
        private int nextWallRunGap;
        // Ordinary patterns since the last special section (starts long ago).
        private int patternsSinceSpecial = int.MaxValue / 2;
        private CoursePattern previousPrefab;
        private CoursePattern.PatternCategory previousCategory;
        private int categoryStreak;
        private float nextStartZ;
        private bool floorMaterialWarningShown;
        private AreaDirector areas;
        private Material floorOverride;
        private PacingDirector pacing;
        private readonly List<CoursePattern> recentPatterns = new List<CoursePattern>();

        private bool AreasActive => areas != null && areaCatalog != null && areaCatalog.IsUsable;
        private CoursePattern[] Pool => AreasActive ? areas.ActivePool : patternPrefabs;

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
            routeRandom = useFixedSeed ? new System.Random(fixedSeed + 104729) : new System.Random();
            nextAirRouteGap = RollAirRouteGap();
            wallRunRandom = useFixedSeed ? new System.Random(fixedSeed + 15485863) : new System.Random();
            nextWallRunGap = RollWallRunGap();
            nextStartZ = transform.position.z;
            ApplyExistingFloorMaterials();
            if (areaCatalog == null)
                areaCatalog = Resources.Load<AreaCatalog>(AreaCatalog.ResourcePath);
            if (areaCatalog != null && areaCatalog.IsUsable)
            {
                areas = gameObject.AddComponent<AreaDirector>();
                areas.Begin(areaCatalog, useFixedSeed ? new System.Random(fixedSeed + 32452843) : new System.Random());
                pacing = new PacingDirector(useFixedSeed ? new System.Random(fixedSeed + 49979687) : new System.Random(),
                    highIntensityFrom, maxHighIntensityInARow);
                pacing.UseBlocks(areas.SpawnArea.pacingBlocks);
            }
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

            // In an area the pacing brings the easy patterns back on purpose.
            int minimumDifficulty = !AreasActive && arrivalTime >= hardStartsAtSeconds ? 1 : 0;
            // Homing can reach queued patterns much sooner than an auto-run
            // estimate. Unlock new tiers only after the actual run threshold;
            // retain the estimate to phase Easy out of the queued course.
            int maximumDifficulty = currentTime >= hardStartsAtSeconds ? 2 :
                currentTime >= mediumStartsAtSeconds ? 1 : 0;

            // On the deck: only upper runs or the return, whatever the tier.
            if (previousPrefab != null && previousPrefab.ExitState == CoursePattern.TravelState.Upper)
                return ChooseUpperPattern();

            if (AreasActive && areas.TransitionDue)
            {
                CoursePattern transition = areas.TransitionPattern;
                bool connects = previousPrefab == null || StatesConnect(previousPrefab.ExitState, transition.EntryState);
                if (transition != previousPrefab && connects && OpportunityGapIsSafe(transition))
                    return transition;
            }

            CollectCandidates(minimumDifficulty, maximumDifficulty, true);
            bool specialAllowed = patternsSinceSpecial >= minNormalPatternsBetweenSpecial &&
                (!AreasActive || pacing.TargetIntensity >= specialFromTargetIntensity);
            // In an area its signature section follows the area's own spacing
            // (the area is what unlocks it, not the run time).
            bool airRouteDue = specialAllowed && (AreasActive ? areas.SpecialDue :
                arrivalTime >= airRouteStartsAtSeconds && patternsSinceAirRoute >= nextAirRouteGap);
            bool wallRunDue = specialAllowed && (AreasActive ? areas.SpecialDue :
                arrivalTime >= wallRunStartsAtSeconds && patternsSinceWallRun >= nextWallRunGap);
            // Both due: the more overdue one first, so neither starves the other.
            bool wallRunFirst = wallRunDue && (!airRouteDue ||
                patternsSinceWallRun - nextWallRunGap >= patternsSinceAirRoute - nextAirRouteGap);
            for (int pass = 0; pass < 2; pass++)
            {
                if ((pass == 0) == wallRunFirst)
                {
                    if (!wallRunDue)
                        continue;
                    List<CoursePattern> wallRuns = candidates.FindAll(p => p.IsWallRun && PacingAllows(p));
                    if (wallRuns.Count > 0)
                        return wallRuns[wallRunRandom.Next(wallRuns.Count)];
                }
                else if (airRouteDue)
                {
                    // Due: take an air-route entry when one fits here (else try again next pattern).
                    CoursePattern entry = PickWhere(p => p.IsAirRouteEntry && PacingAllows(p));
                    if (entry != null)
                        return entry;
                }
            }
            candidates.RemoveAll(p => p.IsAirRouteEntry || p.IsWallRun);
            // A wall run is waiting (spacing after a special section, or a
            // Ground predecessor): keep the course on the ground meanwhile, since
            // an air-combo stretch only leads back down through an air route.
            bool wallRunWaiting = AreasActive
                ? areas.SpecialIsWallRun && (!areas.SpecialDoneInArea || areas.SpecialDue)
                : arrivalTime >= wallRunStartsAtSeconds && patternsSinceWallRun >= nextWallRunGap;
            if (wallRunWaiting && candidates.Exists(p => p.ExitState == CoursePattern.TravelState.Ground))
                candidates.RemoveAll(p => p.ExitState != CoursePattern.TravelState.Ground);
            // An area's pool is smaller than the whole library: rather than stop
            // the course, let the tier and category limits go for this one pick.
            if (candidates.Count == 0 && AreasActive)
            {
                CollectCandidates(0, 2, false);
                candidates.RemoveAll(p => p.IsAirRouteEntry || p.IsWallRun);
            }
            if (candidates.Count == 0)
                return null;
            return AreasActive ? PickClosestToTarget() : candidates[random.Next(candidates.Count)];
        }

        private bool PacingAllows(CoursePattern prefab) => !AreasActive || pacing.Allows(prefab.Intensity);

        // Exact intensity first, then the nearest ones. Hard patterns stay out after
        // a hard stretch, unless nothing else fits here.
        private CoursePattern PickClosestToTarget()
        {
            if (candidates.Exists(PacingAllows))
                candidates.RemoveAll(p => !PacingAllows(p));
            int target = pacing.TargetIntensity;
            int closest = int.MaxValue;
            foreach (CoursePattern candidate in candidates)
                closest = Mathf.Min(closest, Mathf.Abs(candidate.Intensity - target));
            candidates.RemoveAll(p => Mathf.Abs(p.Intensity - target) != closest);
            return candidates[random.Next(candidates.Count)];
        }

        private CoursePattern PickWhere(Predicate<CoursePattern> match)
        {
            List<CoursePattern> matching = candidates.FindAll(match);
            return matching.Count == 0 ? null : matching[routeRandom.Next(matching.Count)];
        }

        // Upper runs until upperPatternsMin..Max is reached, then the return.
        private CoursePattern ChooseUpperPattern()
        {
            bool mustReturn = upperPatternsInRoute >= upperPatternsMax;
            bool mustStay = upperPatternsInRoute < upperPatternsMin;
            for (int pass = 0; pass < 2; pass++)
            {
                candidates.Clear();
                foreach (CoursePattern prefab in Pool)
                {
                    if (prefab == null || prefab == previousPrefab || prefab.PatternLength <= 0f ||
                        prefab.EntryState != CoursePattern.TravelState.Upper)
                        continue;
                    if ((mustReturn && prefab.IsUpperRun) || (mustStay && prefab.IsUpperReturn))
                        continue;
                    if (pass == 0 && !OpportunityGapIsSafe(prefab))
                        continue;
                    candidates.Add(prefab);
                }
                if (candidates.Count > 0)
                    return candidates[routeRandom.Next(candidates.Count)];
            }
            return null;
        }

        private int RollWallRunGap() =>
            wallRunRandom.Next(patternsBetweenWallRunsMin, Mathf.Max(patternsBetweenWallRunsMin, patternsBetweenWallRunsMax) + 1);

        private int RollAirRouteGap() =>
            routeRandom.Next(groundPatternsBetweenAirRoutesMin, Mathf.Max(groundPatternsBetweenAirRoutesMin, groundPatternsBetweenAirRoutesMax) + 1);

        private void CollectCandidates(int minimumDifficulty, int maximumDifficulty,
            bool avoidCategoryStreak)
        {
            candidates.Clear();
            if (Pool == null)
                return;

            foreach (CoursePattern prefab in Pool)
            {
                if (prefab == null || prefab == previousPrefab || prefab.PatternLength <= 0f)
                    continue;
                // Upper patterns only follow the deck (the first pattern included).
                if (prefab.EntryState == CoursePattern.TravelState.Upper)
                    continue;
                int difficulty = (int)prefab.Difficulty;
                if (difficulty < minimumDifficulty || difficulty > maximumDifficulty)
                    continue;
                if (previousPrefab != null && !StatesConnect(previousPrefab.ExitState, prefab.EntryState))
                    continue;
                if (avoidCategoryStreak && categoryStreak >= 2 &&
                    prefab.Category == previousCategory)
                    continue;
                if (avoidCategoryStreak && recentPatterns.Contains(prefab))
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
                {
                    LogUnsafeGap(next, "after " + previousPrefab.name);
                    return false;
                }
            }
            else if (Mathf.Max(0f, nextStartZ - (openingGoZ ?? player.transform.position.z)) +
                     opportunityPositions[0] > maximumGap)
            {
                return false;
            }

            for (int i = 1; i < opportunityPositions.Count; i++)
                if (opportunityPositions[i] - opportunityPositions[i - 1] > maximumGap)
                {
                    LogUnsafeGap(next, "inside the pattern");
                    return false;
                }
            return true;
        }

        private void LogUnsafeGap(CoursePattern next, string where)
        {
            if (AreasActive && areaCatalog.showPatternDebugLogs)
                Debug.Log($"Deadline gap over {maxOpportunityGapSeconds}s: {next.name} {where}");
        }

        private static bool StatesConnect(CoursePattern.TravelState exitState,
            CoursePattern.TravelState entryState)
        {
            // The deck has to continue: Upper never matches Any.
            if (exitState == CoursePattern.TravelState.Upper || entryState == CoursePattern.TravelState.Upper)
                return exitState == entryState;
            return exitState == CoursePattern.TravelState.Any ||
                entryState == CoursePattern.TravelState.Any || exitState == entryState;
        }

        private void SpawnPattern(CoursePattern prefab)
        {
            float startZ = nextStartZ;
            AreaDefinition area = AreasActive ? areas.SpawnArea : null;
            // Obstacles pick their model while they wake up, during Instantiate.
            ObstacleVisual.ActiveSkins = area != null ? area.obstacleSkins : null;
            CoursePattern instance = Instantiate(prefab,
                new Vector3(transform.position.x, transform.position.y, startZ),
                transform.rotation, transform);
            ObstacleVisual.ActiveSkins = null;
            instance.name = prefab.name;
            ThinOutPickups(instance);
            floorOverride = area != null ? area.floorMaterial : null;
            if (area != null)
                ApplyAreaMaterials(instance, area);
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
            if (AreasActive)
            {
                recentPatterns.Add(prefab);
                if (recentPatterns.Count > recentPatternMemory)
                    recentPatterns.RemoveAt(0);
                if (areaCatalog.showPatternDebugLogs)
                    Debug.Log($"Area: {areas.SpawnArea.areaId} | Pacing Block: {pacing.BlockName} | Target Intensity: {pacing.TargetIntensity} | " +
                        $"Selected Pattern: {prefab.name} | Actual Intensity: {prefab.Intensity}");
                pacing.Record(prefab.Intensity, !prefab.IsAreaTransition && prefab.EntryState != CoursePattern.TravelState.Upper);
            }
            if (prefab.IsAirRouteEntry)
            {
                upperPatternsInRoute = 0;
                patternsSinceAirRoute = 0;
                nextAirRouteGap = RollAirRouteGap();
            }
            else if (prefab.IsUpperRun)
                upperPatternsInRoute++;
            else if (!prefab.IsUpperReturn)
                patternsSinceAirRoute++;

            if (prefab.IsWallRun)
            {
                patternsSinceWallRun = 0;
                nextWallRunGap = RollWallRunGap();
            }
            else if (prefab.EntryState != CoursePattern.TravelState.Upper)
                patternsSinceWallRun++;
            // Special sections: the whole air route (entry, upper runs, return) and a wall run.
            if (prefab.IsWallRun || prefab.IsAirRouteEntry || prefab.EntryState == CoursePattern.TravelState.Upper)
                patternsSinceSpecial = 0;
            else
                patternsSinceSpecial++;

            if (AreasActive)
            {
                if (prefab.IsAreaTransition)
                {
                    areas.NotifyTransitionSpawned(instance);
                    pacing.UseBlocks(areas.SpawnArea.pacingBlocks);
                }
                else if (prefab.EntryState != CoursePattern.TravelState.Upper)
                    areas.NotifyPatternSpawned(prefab);
            }

            if (showDebugLogs)
                Debug.Log("Spawn Pattern: " + prefab.name + " | " + prefab.Difficulty);
        }

        // What the pattern prefabs carry themselves, in the area's own colours.
        private void ApplyAreaMaterials(CoursePattern instance, AreaDefinition area)
        {
            if (area.floorMaterial == null && area.deckEdgeMaterial == null && area.decorMaterial == null)
                return;
            foreach (Renderer part in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material material = part.sharedMaterial;
                if (material == null)
                    continue;
                if (area.floorMaterial != null && material == floorMaterial)
                    part.sharedMaterial = area.floorMaterial; // upper decks, wall panels
                else if (area.deckEdgeMaterial != null && material.name == "DeckEdgeGlow")
                    part.sharedMaterial = area.deckEdgeMaterial;
                else if (area.decorMaterial != null && material.name == "DecorStone")
                    part.sharedMaterial = area.decorMaterial;
            }
        }

        // Authored pickups are candidates, not guarantees: each power-up and gift
        // box survives with its chance (own seeded stream, so fixed seeds replay
        // the same course), and at most one power-up stays per pattern.
        private void ThinOutPickups(CoursePattern instance)
        {
            int keptPowerUps = 0;
            // Classic has no Survival Timer, so Freeze Clock / Time Heart never
            // spawn (decided once here; the roll still happens so the rest of the
            // course matches Deadline's).
            bool classic = GameModeState.IsClassicRun;
            foreach (PowerUpPickup pickup in instance.GetComponentsInChildren<PowerUpPickup>(true))
            {
                bool keep = keptPowerUps < maxPowerUpsPerPattern && pickupRandom.NextDouble() < powerUpChance &&
                            !(classic && GameModeState.IsTimerPowerUp(pickup.ItemType));
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
            // A wall-run section leaves its floor gap open (one piece before, one after).
            float endZ = startZ + instance.PatternLength;
            WallRunSection wallRun = instance.GetComponentInChildren<WallRunSection>(true);
            if (wallRun == null)
            {
                AddFloorPiece(instance, startZ, endZ);
                return;
            }
            AddFloorPiece(instance, startZ, Mathf.Clamp(wallRun.GapStartZ, startZ, endZ));
            AddFloorPiece(instance, Mathf.Clamp(wallRun.GapEndZ, startZ, endZ), endZ);
        }

        private void AddFloorPiece(CoursePattern instance, float fromZ, float toZ)
        {
            if (toZ - fromZ < 0.01f)
                return;
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Graybox Floor";
            floor.transform.position = new Vector3(instance.transform.position.x,
                floorTopY - floorThickness * 0.5f, (fromZ + toZ) * 0.5f);
            floor.transform.localScale = new Vector3(floorWidth, floorThickness, toZ - fromZ);
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
            if (floorOverride != null)
            {
                floorRenderer.sharedMaterial = floorOverride;
                return;
            }
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
            groundPatternsBetweenAirRoutesMax = Mathf.Max(groundPatternsBetweenAirRoutesMin, groundPatternsBetweenAirRoutesMax);
            upperPatternsMax = Mathf.Max(upperPatternsMin, upperPatternsMax);
            patternsBetweenWallRunsMax = Mathf.Max(patternsBetweenWallRunsMin, patternsBetweenWallRunsMax);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position - transform.right * floorWidth * 0.5f,
                transform.position + transform.right * floorWidth * 0.5f);
        }
    }
}
