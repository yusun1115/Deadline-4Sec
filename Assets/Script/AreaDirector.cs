using TMPro;
using UnityEngine;

namespace Deadline4Sec
{
    // Keeps track of the area of a run, in one place. Two positions, because the
    // course is built ahead of the runner:
    //  - SpawnArea: what new patterns are made of (changes when a transition spawns),
    //  - CurrentArea: where the runner is (changes inside the transition tunnel,
    //    where sky, fog, light and background are swapped unseen).
    // Added by PatternSpawner when an AreaCatalog exists.
    public sealed class AreaDirector : MonoBehaviour
    {
        private static readonly int SkyTop = Shader.PropertyToID("_TopColor");
        private static readonly int SkyMid = Shader.PropertyToID("_MidColor");
        private static readonly int SkyHorizon = Shader.PropertyToID("_HorizonColor");
        private static readonly int SkyCloud = Shader.PropertyToID("_CloudColor");
        private static readonly int Moon = Shader.PropertyToID("_MoonColor");
        private static readonly int MoonGlow = Shader.PropertyToID("_MoonGlow");

        private const float LookBlendSeconds = 0.6f;

        private AreaCatalog catalog;
        private System.Random random;
        private EnvironmentScroller environment;
        private Light sun;
        private Material skyInstance;
        private Transform viewer;

        private AreaTransitionSection pendingSection;
        private AreaDefinition pendingArea;
        private bool awaitingExit;
        private AreaLook fromLook, toLook;
        private float lookBlend = 1f;
        private int targetPatterns;
        private int patternsSinceSpecial;
        private int nextSpecialGap;
        private TMP_Text nameLabel;
        private float nameShownAt = float.NegativeInfinity;

        public AreaDefinition SpawnArea { get; private set; }
        public AreaDefinition CurrentArea { get; private set; }
        public AreaType CurrentAreaType => CurrentArea != null ? CurrentArea.areaId : AreaType.SoulRailway;
        // Patterns spawned in SpawnArea so far.
        public int PatternsInArea { get; private set; }
        public bool SpecialDoneInArea { get; private set; }
        // Areas entered this run (for tests and debugging).
        public int AreaChanges { get; private set; }
        public CoursePattern[] ActivePool { get; private set; }
        public bool HasSpecial => SpawnArea != null && SpawnArea.specialPatternPool != null && SpawnArea.specialPatternPool.Length > 0;

        public bool SpecialIsWallRun => HasSpecial && SpawnArea.specialPatternPool[0] != null && SpawnArea.specialPatternPool[0].IsWallRun;

        public bool SpecialDue => HasSpecial && PatternsInArea >= SpawnArea.firstSpecialAfterPatterns &&
            patternsSinceSpecial >= nextSpecialGap;

        public bool TransitionDue => PatternsInArea >= targetPatterns &&
            (!HasSpecial || SpecialDoneInArea || PatternsInArea >= targetPatterns + 3);

        public CoursePattern TransitionPattern => catalog.transitionPattern;

        public void Begin(AreaCatalog areas, System.Random areaRandom)
        {
            catalog = areas;
            random = areaRandom;
            environment = FindFirstObjectByType<EnvironmentScroller>();
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                    sun = light;
            CurrentArea = areas.areas[0];
            EnterSpawnArea(CurrentArea);
        }

        private void EnterSpawnArea(AreaDefinition area)
        {
            SpawnArea = area;
            PatternsInArea = 0;
            SpecialDoneInArea = false;
            patternsSinceSpecial = int.MaxValue / 2;
            nextSpecialGap = 0;
            targetPatterns = random.Next(area.minPatterns, Mathf.Max(area.minPatterns, area.maxPatterns) + 1);
            int normal = area.patternPool != null ? area.patternPool.Length : 0;
            int special = area.specialPatternPool != null ? area.specialPatternPool.Length : 0;
            ActivePool = new CoursePattern[normal + special];
            if (normal > 0)
                area.patternPool.CopyTo(ActivePool, 0);
            if (special > 0)
                area.specialPatternPool.CopyTo(ActivePool, normal);
        }

        // Called for every spawned pattern except the transition and upper-deck ones.
        public void NotifyPatternSpawned(CoursePattern prefab)
        {
            PatternsInArea++;
            if (prefab.IsWallRun || prefab.IsAirRouteEntry)
            {
                SpecialDoneInArea = true;
                patternsSinceSpecial = 0;
                nextSpecialGap = random.Next(SpawnArea.patternsBetweenSpecialMin,
                    Mathf.Max(SpawnArea.patternsBetweenSpecialMin, SpawnArea.patternsBetweenSpecialMax) + 1);
            }
            else
                patternsSinceSpecial++;
        }

        // The transition section was placed: the course after it is the next area.
        public void NotifyTransitionSpawned(CoursePattern instance)
        {
            AreaDefinition next = catalog.After(SpawnArea);
            AreaTransitionSection section = instance.GetComponentInChildren<AreaTransitionSection>(true);
            if (section != null)
                section.SetPortalColor(next.look.accent);
            pendingSection = section;
            pendingArea = next;
            if (environment != null)
                environment.PrepareArea(next.areaId, next.look.haze);
            EnterSpawnArea(next);
            if (section == null)
                SwitchWorld();
        }

        private void Update()
        {
            if (viewer == null)
            {
                Camera main = Camera.main;
                if (main != null)
                    viewer = main.transform;
            }
            if (viewer != null && pendingSection != null)
            {
                if (pendingArea != null)
                {
                    // Until the swap the portal stays shut: it is what hides it.
                    if (viewer.position.z >= pendingSection.SwitchZ)
                        SwitchWorld();
                }
                else if (awaitingExit)
                {
                    pendingSection.RevealFor(viewer.position.z);
                    if (viewer.position.z >= pendingSection.ExitZ)
                    {
                        awaitingExit = false;
                        pendingSection = null;
                        ShowAreaName();
                    }
                }
            }
            UpdateLook();
            UpdateAreaName();
        }

        private void SwitchWorld()
        {
            if (catalog.showPatternDebugLogs)
                Debug.Log($"Current Area: {CurrentArea.areaId} -> {pendingArea.areaId}");
            fromLook = CurrentArea.look;
            toLook = pendingArea.look;
            CurrentArea = pendingArea;
            pendingArea = null;
            awaitingExit = pendingSection != null;
            lookBlend = 0f;
            AreaChanges++;
            if (environment != null)
                environment.ShowPreparedArea();
        }

        private void UpdateLook()
        {
            if (lookBlend >= 1f)
                return;
            lookBlend = Mathf.Min(1f, lookBlend + Time.unscaledDeltaTime / LookBlendSeconds);
            float t = Mathf.SmoothStep(0f, 1f, lookBlend);
            RenderSettings.fogColor = Color.Lerp(fromLook.fog, toLook.fog, t);
            RenderSettings.ambientSkyColor = Color.Lerp(fromLook.ambientSky, toLook.ambientSky, t);
            RenderSettings.ambientEquatorColor = Color.Lerp(fromLook.ambientEquator, toLook.ambientEquator, t);
            RenderSettings.ambientGroundColor = Color.Lerp(fromLook.ambientGround, toLook.ambientGround, t);
            if (sun != null)
                sun.color = Color.Lerp(fromLook.sun, toLook.sun, t);
            // A runtime copy: changing the sky asset itself would stick in the editor.
            if (skyInstance == null && RenderSettings.skybox != null)
            {
                skyInstance = new Material(RenderSettings.skybox);
                RenderSettings.skybox = skyInstance;
            }
            if (skyInstance != null)
            {
                skyInstance.SetColor(SkyTop, Color.Lerp(fromLook.skyTop, toLook.skyTop, t));
                skyInstance.SetColor(SkyMid, Color.Lerp(fromLook.skyMid, toLook.skyMid, t));
                skyInstance.SetColor(SkyHorizon, Color.Lerp(fromLook.skyHorizon, toLook.skyHorizon, t));
                skyInstance.SetColor(SkyCloud, Color.Lerp(fromLook.skyCloud, toLook.skyCloud, t));
                skyInstance.SetColor(Moon, Color.Lerp(fromLook.moon, toLook.moon, t));
                skyInstance.SetColor(MoonGlow, Color.Lerp(fromLook.moonGlow, toLook.moonGlow, t));
            }
        }

        // A small name on leaving the tunnel: reaching a new place is the reward.
        private void ShowAreaName()
        {
            if (nameLabel == null)
            {
                Canvas canvas = FindFirstObjectByType<Canvas>();
                if (canvas == null)
                    return;
                nameLabel = GameFlowManager.CreateMenuText(canvas.transform, "", Vector2.zero, new Vector2(720f, 80f), 44f);
                nameLabel.name = "AreaName";
                RectTransform rect = nameLabel.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.72f);
                nameLabel.characterSpacing = 4f;
            }
            nameLabel.text = CurrentArea.displayName;
            nameLabel.gameObject.SetActive(true);
            nameShownAt = Time.unscaledTime;
            UpdateAreaName();
        }

        private void UpdateAreaName()
        {
            if (nameLabel == null || !nameLabel.gameObject.activeSelf)
                return;
            float age = Time.unscaledTime - nameShownAt;
            if (age >= 2f)
            {
                nameLabel.gameObject.SetActive(false);
                return;
            }
            nameLabel.alpha = Mathf.Clamp01(age / 0.25f) * Mathf.Clamp01((2f - age) / 0.5f);
        }

        private void OnDestroy()
        {
            if (skyInstance != null)
                Destroy(skyInstance);
        }
    }
}
