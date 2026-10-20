using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Wall Run patterns (Wall Run.md, 2026-10-18).
    //
    // Ground → floor gap with run-able walls → ground. The runner has to swipe
    // toward the entry wall inside the WallRunEntry window (~1s at the speed
    // wall runs appear, 14-16 m/s); no input or the wrong side runs off the
    // edge into the FallDeathTrigger. On the walls each obstacle blocks one wall,
    // so the runner switches to the other; the exit drops it onto the floor
    // after the gap in the lane next to its wall.
    // Deadline: the countdown holds while on a wall (GameTimer); an enemy just
    // before the entry window and one after the landing are the resets around
    // it (a late switch past a wall obstacle can add a Near Miss reset).
    public static partial class PatternLibraryBuilder
    {
        private const float WallX = 3.6f;          // runner's line on a wall (|x|)
        private const float WallFeet = 0.8f;       // feet height while on a wall
        private const float WallFaceX = 4.35f;     // inner face of the wall panels
        private const float WallTop = 4.6f;
        private const float WallBottom = -9f;

        private sealed class WallRunLayout
        {
            public WallRunSection.AvailableWall Entry;
            public float GapStart, GapEnd, ExitZ;
            public float EntryStart, EntryEnd;
            public float EntryWallFrom, OtherWallFrom;
            public float CheckpointZ, LandingZ;
        }

        [MenuItem("Deadline 4 Sec/Course/Build Wall Run Patterns")]
        public static void BuildWallRuns()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new System.InvalidOperationException("Save the open scene before building patterns.");
            LoadPrefabs();
            var M = CoursePattern.DifficultyLevel.Medium;
            var wallRun = CoursePattern.PatternCategory.WallRun;

            var specs = new List<Spec>();
            foreach (int side in new[] { -1, 1 })
            {
                // side: the entry wall (-1 left, +1 right); the first obstacle is
                // on it, the second on the other wall, so the run is
                // attach → switch → switch back → exit (Wall Run.md §32).
                var layout = new WallRunLayout
                {
                    Entry = side < 0 ? WallRunSection.AvailableWall.Left : WallRunSection.AvailableWall.Right,
                    GapStart = 26f, GapEnd = 53f, ExitZ = 51f,
                    EntryStart = 14f, EntryEnd = 28.5f,
                    EntryWallFrom = 8f, OtherWallFrom = 31f,
                    CheckpointZ = 13f, LandingZ = 56f,
                };
                string name = side < 0 ? "Pattern_W1_WallRun_Left" : "Pattern_W2_WallRun_Right";
                specs.Add(P(name, 66, M, wallRun)
                    // Approach: one enemy before the entry window (a timer reset
                    // that never shares a swipe with the attach), Souls toward the wall.
                    .Enemy(0, 12).Coins(side, 1f, 15, 5, 1.6f)
                    .WallSection(layout)
                    // Reward line on the entry wall up to its obstacle (stay late = risk).
                    .WallCoins(side, 31f, 4, 1.4f).WallGift(side, 36.4f)
                    // Even a last-moment attach (done ~31.5m) leaves ~0.35s to switch.
                    .WallObstacle(side, 39f)
                    // Safe line on the other wall, then back.
                    .WallCoins(-side, 40.5f, 5, 1.4f)
                    .WallObstacle(-side, 48f)
                    .WallCoins(side, 49.4f, 2, 1f)
                    // Landing (~55m) in the wall-side lane; the next enemy waits in
                    // the centre (kill-to-kill ~48m ≈ 3.2s at 15 m/s).
                    .Coins(side, 1f, 55f, 3).Enemy(0, 59.5f));
            }

            var built = new List<GameObject>();
            foreach (Spec spec in specs)
                built.Add(SavePattern(spec));
            RegisterInScenes(built);
            AssetDatabase.SaveAssets();
            Debug.Log("Wall run patterns: " + built.Count);
        }

        private static Spec WallGift(this Spec s, int side, float z) =>
            s.Add(gift, "GiftBox", new Vector3(side * WallX, WallFeet + 1f, z));

        private static Spec WallCoins(this Spec s, int side, float z0, int count, float step)
        {
            for (int i = 0; i < count; i++)
                s.Add(coin, "Coin", new Vector3(side * WallX, WallFeet + 1f, z0 + i * step));
            return s;
        }

        // A hazard box on one wall's running line (Lane Blocker = fatal contact,
        // Shield / Near Miss rules unchanged). It covers the runner on that wall
        // and ends 0.6m short of the centre so the other wall is clear.
        private static Spec WallObstacle(this Spec s, int side, float z)
        {
            s.Items.Add(parent =>
            {
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(blocker, parent);
                go.name = $"Wall Obstacle / Near Miss {LaneName(side)}";
                float inner = WallX - 0.75f;
                go.transform.localPosition = new Vector3(side * (inner + WallFaceX) * 0.5f, 1.9f, z);
                go.transform.localScale = new Vector3(WallFaceX - inner, 3.2f, 1.2f);
            });
            return s;
        }

        private static Spec WallSection(this Spec s, WallRunLayout l)
        {
            s.Items.Add(parent =>
            {
                int side = l.Entry == WallRunSection.AvailableWall.Right ? 1 : -1;
                GameObject root = new GameObject("SetPiece Wall Run");
                root.transform.SetParent(parent, false);
                WallRunSection section = root.AddComponent<WallRunSection>();

                Transform Point(string name, Vector3 local)
                {
                    Transform t = new GameObject(name).transform;
                    t.SetParent(root.transform, false);
                    t.localPosition = local;
                    return t;
                }

                BoxCollider entry = new GameObject("WallRunEntry").AddComponent<BoxCollider>();
                entry.transform.SetParent(root.transform, false);
                entry.isTrigger = true;
                entry.transform.localPosition = new Vector3(0f, 1f, (l.EntryStart + l.EntryEnd) * 0.5f);
                entry.size = new Vector3(10f, 8f, l.EntryEnd - l.EntryStart);
                Transform exit = Point("WallRunExit", new Vector3(0f, WallFeet, l.ExitZ));
                Transform left = Point("LeftWallAnchor", new Vector3(-WallX, WallFeet, l.GapStart));
                Transform right = Point("RightWallAnchor", new Vector3(WallX, WallFeet, l.GapStart));
                Transform landing = Point("GroundExitAnchor", new Vector3(side * Lane, 0f, l.LandingZ));
                Transform checkpoint = Point("ReviveCheckpoint", new Vector3(0f, 0f, l.CheckpointZ));

                GameObject fall = new GameObject("FallDeathTrigger");
                fall.transform.SetParent(root.transform, false);
                BoxCollider fallBox = fall.AddComponent<BoxCollider>();
                fallBox.isTrigger = true;
                fall.transform.localPosition = new Vector3(0f, -7f, (l.GapStart + l.GapEnd) * 0.5f);
                fallBox.size = new Vector3(14f, 2f, l.GapEnd - l.GapStart + 2f);
                fall.AddComponent<FallDeathTrigger>();

                SerializedObject data = new SerializedObject(section);
                data.FindProperty("availableWall").enumValueIndex = (int)l.Entry;
                data.FindProperty("entry").objectReferenceValue = entry;
                data.FindProperty("exit").objectReferenceValue = exit;
                data.FindProperty("leftWallAnchor").objectReferenceValue = left;
                data.FindProperty("rightWallAnchor").objectReferenceValue = right;
                data.FindProperty("groundExitAnchor").objectReferenceValue = landing;
                data.FindProperty("reviveCheckpoint").objectReferenceValue = checkpoint;
                data.FindProperty("floorGapStart").floatValue = l.GapStart;
                data.FindProperty("floorGapEnd").floatValue = l.GapEnd;
                data.ApplyModifiedPropertiesWithoutUndo();

                // Look: tall wall panels (visual only), a glowing running line on
                // each, a bright lip where the floor ends, a dark pit far below.
                Material floor = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/FloorColor.mat");
                Material glow = DeckEdgeMaterial();
                Material pit = PitMaterial();
                foreach (int wall in new[] { -1, 1 })
                {
                    float from = wall == side ? l.EntryWallFrom : l.OtherWallFrom;
                    float to = l.GapEnd + 2f;
                    float mid = (from + to) * 0.5f;
                    WallVisual(root.transform, "Wall Panel", floor,
                        new Vector3(wall * (WallFaceX + 0.25f), (WallTop + WallBottom) * 0.5f, mid),
                        new Vector3(0.5f, WallTop - WallBottom, to - from));
                    WallVisual(root.transform, "Wall Run Line", glow,
                        new Vector3(wall * (WallFaceX - 0.02f), WallFeet - 0.05f, mid), new Vector3(0.06f, 0.12f, to - from));
                    WallVisual(root.transform, "Wall Top Edge", glow,
                        new Vector3(wall * (WallFaceX + 0.25f), WallTop, mid), new Vector3(0.6f, 0.12f, to - from));
                    WallVisual(root.transform, "Wall Front Edge", glow,
                        new Vector3(wall * (WallFaceX + 0.25f), (WallTop + WallBottom) * 0.5f, from), new Vector3(0.62f, WallTop - WallBottom, 0.12f));
                }
                WallVisual(root.transform, "Floor End Edge", glow,
                    new Vector3(0f, -0.06f, l.GapStart - 0.06f), new Vector3(8.6f, 0.16f, 0.12f));
                WallVisual(root.transform, "Floor Start Edge", glow,
                    new Vector3(0f, -0.06f, l.GapEnd + 0.06f), new Vector3(8.6f, 0.16f, 0.12f));
                // Pit ends: without them the sky shows under the floor edges.
                WallVisual(root.transform, "Pit Front Wall", pit,
                    new Vector3(0f, (WallBottom - 0.2f) * 0.5f, l.GapStart + 0.1f), new Vector3(9.2f, -WallBottom - 0.2f, 0.2f));
                WallVisual(root.transform, "Pit Back Wall", pit,
                    new Vector3(0f, (WallBottom - 0.2f) * 0.5f, l.GapEnd - 0.1f), new Vector3(9.2f, -WallBottom - 0.2f, 0.2f));
                WallVisual(root.transform, "Pit Floor", pit,
                    new Vector3(0f, WallBottom, (l.GapStart + l.GapEnd) * 0.5f), new Vector3(9.2f, 0.2f, l.GapEnd - l.GapStart + 4f));
            });
            return s;
        }

        private static void WallVisual(Transform parent, string name, Material material, Vector3 position, Vector3 scale) =>
            Visual(parent, name, material, position, scale);

        private static Material PitMaterial()
        {
            const string path = ModelFolder + "WallRunPit.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null)
                return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", new Color(0.03f, 0.02f, 0.06f));
            System.IO.Directory.CreateDirectory(ModelFolder);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
