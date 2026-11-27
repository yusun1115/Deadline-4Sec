using System.Collections.Generic;
using UnityEngine;

namespace Deadline4Sec
{
    // Main menu backdrop (2026-11-27, Main Menu 3D Background Visual Polish.md).
    // The title camera stands in front of the Reaper and looks back down the
    // line (-Z), where the run's backdrop (built ahead, +Z) is not: the 30 m
    // Start Floor ended in the middle of the screen against the bare sky. This
    // builds the Soul Railway behind the start once, from the same pieces and
    // materials as the run's backdrop, and shows it only while the menu does:
    //   Rail_Extension  copies of the Start Floor (no colliders) running on into the fog,
    //   Midground       the track-side pillars with their chains, and some of the
    //                   railway's mid pieces (gothic blocks, rail arches, a viaduct), not mirrored,
    //   ClockTower      the railway's clock tower on the vanishing point, its hands turning slowly,
    //   FarBackground   spires and the far city skyline, sunk in the fog, over the cloud sea.
    // The low fog behind and the rising souls are AreaAtmosphere's (SetBackView).
    // Its own root object (the scroller's child count is its backdrops), laid out
    // in a frame turned 180 degrees: local +Z runs away from the title camera.
    public sealed partial class EnvironmentScroller
    {
        [Header("Main menu backdrop (behind the start, title camera)")]
        [SerializeField] private bool mainMenuBackdrop = true;
        [Tooltip("How far the rail runs on behind the start (fog swallows the end).")]
        [SerializeField, Min(30f)] private float menuRailLength = 330f;
        [Tooltip("Track-side pillars behind the start: distance from the track, first one, count " +
                 "(spaced like the run's, their chains reach the next).")]
        [SerializeField] private float menuPillarOffsetX = 6.4f;
        [SerializeField] private float menuPillarStart = 22f;
        [SerializeField, Range(0, 16)] private int menuPillarCount = 10;
        [Tooltip("Clock tower: x, y (base sunk in the fog), distance behind the start. " +
                 "The dial sits between the logo and the Reaper's hood.")]
        [SerializeField] private Vector3 menuClockTower = new Vector3(0f, -44.5f, 220f);
        [SerializeField, Min(0.1f)] private float menuClockScale = 0.85f;
        [Tooltip("The tower's own material (a copy of the run's landmark): darker, less glow, " +
                 "more fog, so it stays behind the logo and the Reaper.")]
        [SerializeField] private Color menuClockTint = new Color(0.5f, 0.44f, 0.58f);
        [SerializeField, Min(0f)] private float menuClockGlow = 0.85f;
        [SerializeField, Range(0f, 1f)] private float menuClockFog = 0.9f;
        [Tooltip("Clock hands, degrees per second (the run's are -24 / -2).")]
        [SerializeField] private float menuMinuteHandSpeed = -6f;
        [SerializeField] private float menuHourHandSpeed = -0.5f;
        [SerializeField, Range(0, 12)] private int menuSpireCount = 8;
        [SerializeField] private float menuFarDistance = 290f;

        private static readonly GameFlowManager.GameState[] MenuStates =
        {
            GameFlowManager.GameState.Title, GameFlowManager.GameState.Settings,
            GameFlowManager.GameState.Upgrades, GameFlowManager.GameState.Skins,
        };
        // Leaving the menu the camera swings round the Reaper for about this long
        // (TitleShowcaseCamera.handoffDuration): the scenery behind stays for it.
        private const float MenuLingerSeconds = 1.6f;

        private Transform menuRoot;
        private Material menuClockMaterial;
        private readonly List<Mesh> menuMeshes = new List<Mesh>();
        private readonly List<Spinner> menuSpinners = new List<Spinner>();
        private GameFlowManager menuFlow;
        private float menuLeftAt = float.NegativeInfinity;
        private bool menuShown;

        public Transform MainMenuBackdrop => menuRoot;
        public bool MainMenuBackdropShown => menuRoot != null && menuRoot.gameObject.activeSelf;

        private void Start()
        {
            if (mainMenuBackdrop && current != null)
                BuildMainMenuBackdrop();
        }

        private void BuildMainMenuBackdrop()
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            float startZ = player != null ? player.transform.position.z : transform.position.z;
            GameObject root = new GameObject("MainMenu_Background_3D");
            if (gameObject.scene.IsValid())
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            menuRoot = root.transform;
            menuRoot.SetPositionAndRotation(new Vector3(trackX, 0f, startZ), Quaternion.Euler(0f, 180f, 0f));

            Random.State saved = Random.state;
            Random.InitState(seed + 7919);
            BuildMenuRail(startZ);
            BuildMenuMidground();
            BuildMenuClockTower();
            BuildMenuFar();
            Random.state = saved;
            ShowMainMenuBackdrop(true);
        }

        // ---------- Rail_Extension ----------

        // The Start Floor again and again (its mesh and material, no colliders),
        // each one where the last ended, until the fog has it.
        private void BuildMenuRail(float startZ)
        {
            Transform group = MenuGroup("Rail_Extension");
            GameObject floor = GameObject.Find("Start Floor");
            if (floor == null || !floor.TryGetComponent(out MeshFilter sourceMesh) ||
                !floor.TryGetComponent(out MeshRenderer sourceRenderer))
                return;
            Bounds b = sourceRenderer.bounds;
            float length = b.size.z;
            if (length < 1f)
                return;
            int count = Mathf.CeilToInt(menuRailLength / length);
            string[] names = { "MainMenu_Rail_Near", "MainMenu_Rail_Mid", "MainMenu_Rail_Far" };
            for (int i = 0; i < count; i++)
            {
                GameObject copy = new GameObject(names[Mathf.Min(2, i * 3 / Mathf.Max(1, count))] + " " + i);
                copy.transform.SetParent(group, false);
                copy.transform.SetPositionAndRotation(floor.transform.position - Vector3.forward * length * (i + 1),
                    floor.transform.rotation);
                copy.transform.localScale = floor.transform.lossyScale;
                copy.AddComponent<MeshFilter>().sharedMesh = sourceMesh.sharedMesh;
                MeshRenderer r = copy.AddComponent<MeshRenderer>();
                r.sharedMaterials = sourceRenderer.sharedMaterials;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = sourceRenderer.receiveShadows;
            }
        }

        // ---------- Midground ----------

        private void BuildMenuMidground()
        {
            Transform group = MenuGroup("Midground");
            // The run's pillars with their chains and lanterns, receding on both sides.
            Mesh pillar = MenuMesh(BuildPillarMesh());
            for (int i = 0; i < menuPillarCount; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                // The mid layer's (dimmer) material: behind the Reaper they stay darker than the run's.
                Transform p = MenuSpawn(group, "Pillar", pillar, current.MidMaterial);
                p.localPosition = new Vector3(side * menuPillarOffsetX, 0f,
                    menuPillarStart + (i / 2) * pillarSpacing + (side > 0 ? pillarSpacing * 0.5f : 0f));
                p.localScale = new Vector3(side, 1f, 1f);
            }
            // Mid pieces, left and right not alike: a gothic block and a viaduct on
            // the left, rail arches and another block on the right.
            Mesh block = MenuMesh(BuildGothicBlockMesh());
            Mesh viaduct = MenuMesh(BuildViaductMesh());
            Mesh arch = MenuMesh(BuildRailArchMesh());
            var pieces = new (Mesh mesh, float x, float z)[]
            {
                (block, -13f, 58f), (viaduct, -16f, 112f), (block, -19f, 170f),
                (arch, 10.5f, 46f), (arch, 11.5f, 92f), (block, 15f, 135f), (viaduct, 18f, 190f),
            };
            foreach (var piece in pieces)
            {
                Transform m = MenuSpawn(group, "Mid", piece.mesh, current.MidMaterial);
                float side = Mathf.Sign(piece.x);
                m.localPosition = new Vector3(piece.x, 0f, piece.z + Random.Range(-4f, 4f));
                m.localScale = new Vector3(-side, 1f, 1f); // the meshes face +X (the track) on the left
            }
        }

        // ---------- ClockTower ----------

        private void BuildMenuClockTower()
        {
            Transform group = MenuGroup("ClockTower");
            menuClockMaterial = new Material(current.LandmarkMaterial) { name = "Silhouette MainMenu Clock" };
            menuClockMaterial.SetColor("_Tint", menuClockTint);
            menuClockMaterial.SetFloat("_Glow", menuClockGlow);
            menuClockMaterial.SetFloat("_FogAmount", menuClockFog);
            Transform tower = MenuSpawn(group, "ClockTower", MenuMesh(BuildClockTowerMesh()), menuClockMaterial);
            tower.localPosition = menuClockTower;
            tower.localScale = Vector3.one * menuClockScale;
            Transform hour = MenuSpawn(tower, "ClockHourHand",
                MenuMesh(BuildBox(new Vector3(-0.6f, 0f, -0.4f), new Vector3(0.6f, 7f, 0f), Iron)), menuClockMaterial);
            Transform minute = MenuSpawn(tower, "ClockMinuteHand",
                MenuMesh(BuildBox(new Vector3(-0.4f, 0f, -0.5f), new Vector3(0.4f, 10f, 0f), Iron)), menuClockMaterial);
            hour.localPosition = minute.localPosition = new Vector3(0f, 66f, -9.2f);
            menuSpinners.Add(new Spinner { Part = minute, Speed = menuMinuteHandSpeed, Phase = 20f });
            menuSpinners.Add(new Spinner { Part = hour, Speed = menuHourHandSpeed, Phase = -120f });
        }

        // ---------- FarBackground ----------

        private void BuildMenuFar()
        {
            Transform group = MenuGroup("FarBackground");
            Mesh[] spires = new Mesh[3];
            for (int i = 0; i < spires.Length; i++)
                spires[i] = MenuMesh(BuildSpireMesh(Random.Range(3f, 6f), Random.Range(16f, 34f), Random.Range(0, 3)));
            for (int i = 0; i < menuSpireCount; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Transform s = MenuSpawn(group, "Spire", spires[i % spires.Length], current.MidMaterial);
                s.localPosition = new Vector3(side * Random.Range(22f, 50f), Random.Range(-10f, -5f),
                    Mathf.Lerp(105f, 235f, i / Mathf.Max(1f, menuSpireCount - 1f)) + Random.Range(-8f, 8f));
                s.localRotation = Quaternion.Euler(0f, Random.Range(-20f, 20f), 0f);
                s.localScale = Vector3.one * Random.Range(0.8f, 1.2f);
            }
            Transform city = MenuSpawn(group, "FarCity", MenuMesh(BuildFarCityMesh()), current.FarMaterial);
            city.localPosition = new Vector3(0f, 0f, menuFarDistance);
            Transform clouds = MenuSpawn(group, "CloudSea", MenuMesh(BuildCloudSeaMesh()), current.Material);
            clouds.localPosition = new Vector3(0f, cloudSeaY, 150f);
        }

        // ---------- shown with the menu ----------

        private void UpdateMainMenuBackdrop()
        {
            if (menuRoot == null)
                return;
            if (menuFlow == null)
                menuFlow = FindFirstObjectByType<GameFlowManager>();
            bool inMenu = menuFlow != null && System.Array.IndexOf(MenuStates, menuFlow.State) >= 0;
            if (inMenu)
                menuLeftAt = float.PositiveInfinity;
            else if (float.IsPositiveInfinity(menuLeftAt))
                menuLeftAt = Time.unscaledTime;
            bool show = inMenu || Time.unscaledTime - menuLeftAt < MenuLingerSeconds;
            // Its materials are the first backdrop's: gone with it at an area change.
            show &= current != null && current.Material != null;
            if (show != menuShown)
                ShowMainMenuBackdrop(show);
            if (!show)
                return;
            float t = Time.unscaledTime;
            foreach (Spinner spinner in menuSpinners)
                spinner.Part.localRotation = Quaternion.Euler(0f, 0f, t * spinner.Speed + spinner.Phase);
        }

        private void ShowMainMenuBackdrop(bool show)
        {
            menuShown = show;
            menuRoot.gameObject.SetActive(show);
            if (Atmosphere != null)
                Atmosphere.SetBackView(show);
        }

        private void DestroyMainMenuBackdrop()
        {
            if (menuRoot != null)
                Destroy(menuRoot.gameObject);
            foreach (Mesh mesh in menuMeshes)
                Destroy(mesh);
            menuMeshes.Clear();
            if (menuClockMaterial != null)
                Destroy(menuClockMaterial);
        }

        private Transform MenuGroup(string name)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(menuRoot, false);
            return group;
        }

        private Mesh MenuMesh(Mesh mesh)
        {
            menuMeshes.Add(mesh);
            return mesh;
        }

        private static Transform MenuSpawn(Transform parent, string name, Mesh mesh, Material material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }
    }
}
