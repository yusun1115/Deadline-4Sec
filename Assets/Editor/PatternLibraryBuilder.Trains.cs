using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Subway-Surfers-style trains (2026-10-10 request):
    //  - "Train": a tall single-lane car parked on the track. Its front and sides
    //    are a normal Obstacle (any contact except landing on top ends the run);
    //    it is too tall to jump onto (2.8m vs a 1.6m jump), so you go around it.
    //  - "Train Ramp": the same car with a ramp in front. The ramp is a plain
    //    collider in a sibling object (not under the Obstacle, so touching it is
    //    safe); it rises a little above the roof, so you run up and step down onto
    //    the roof, run along it and drop off the end.
    // Both are stationary. Patterns R/S/T follow the replay bot's rules (it
    // sidesteps Lane Blockers toward the right from lanes ≤ 0, lines up with
    // anything else) so the 60-second replay routes stay playable.
    public static partial class PatternLibraryBuilder
    {
        private const float TrainWidth = 2.2f;
        private const float TrainHeight = 2.8f;
        private const float TrainLength = 14f;
        private const float RampLength = 7.5f;
        private const float RampTop = TrainHeight + 0.15f;
        private const float RampLip = 0.4f;
        private const float TrainRoofY = TrainHeight;

        private static GameObject train, rampTrain;

        [MenuItem("Deadline 4 Sec/Course/Build Train Patterns")]
        public static void BuildTrains()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the open scene before rebuilding patterns.");
            System.IO.Directory.CreateDirectory(ModelFolder);
            LoadPrefabs();
            BuildTrainPieces();
            List<GameObject> built = BuildTrainPatterns();
            RegisterInScenes(built);
            AssetDatabase.SaveAssets();
            Debug.Log("Train patterns: " + built.Count + " built.");
        }

        private static void BuildTrainPieces()
        {
            Material iron = Material("TrainIron", new Color(1f, 1f, 1f), new Color(1f, 0.2f, 0.55f), 5f);
            GameObject car = SavePiece("Obstacle_SubwayCar", TrainMesh(), iron, ModelFolder);
            train = SaveTrain("Train", car, Obstacle.ObstacleType.LaneBlocker);

            // Ramp train: container with the train (Obstacle) and the ramp as siblings.
            Mesh rampVisual = RampMesh();
            Mesh rampCollision = RampCollisionMesh();
            ReplaceMeshAsset(rampVisual, ModelFolder + "Obstacle_TrainRamp_Mesh.asset");
            ReplaceMeshAsset(rampCollision, ModelFolder + "Obstacle_TrainRamp_Collision.asset");

            GameObject root = new GameObject("Train Ramp");
            GameObject body = BuildTrainObject("Train", car, Obstacle.ObstacleType.RampTrain);
            body.transform.SetParent(root.transform, false);
            GameObject ramp = new GameObject("Ramp");
            ramp.transform.SetParent(root.transform, false);
            ramp.AddComponent<MeshFilter>().sharedMesh = rampVisual;
            MeshRenderer r = ramp.AddComponent<MeshRenderer>();
            r.sharedMaterial = iron;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MeshCollider collider = ramp.AddComponent<MeshCollider>();
            collider.sharedMesh = rampCollision;
            collider.convex = true;
            rampTrain = SavePrefab(root, "Assets/Prefab/Obstacle/Train Ramp.prefab");
        }

        private static GameObject SaveTrain(string name, GameObject model, Obstacle.ObstacleType type) =>
            SavePrefab(BuildTrainObject(name, model, type), "Assets/Prefab/Obstacle/" + name + ".prefab");

        // Root at the front face, bottom centre; the car extends forward (+Z), so
        // distance checks measure to the face you must avoid.
        private static GameObject BuildTrainObject(string name, GameObject model, Obstacle.ObstacleType type)
        {
            GameObject go = new GameObject(name);
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, TrainHeight * 0.5f, TrainLength * 0.5f);
            box.size = new Vector3(TrainWidth, TrainHeight, TrainLength);
            Obstacle obstacle = go.AddComponent<Obstacle>();
            SerializedObject data = new SerializedObject(obstacle);
            data.FindProperty("obstacleType").enumValueIndex = (int)type;
            // Narrow sideways: running in the next lane past a train is not a near
            // miss, only a last-moment swerve across its front is.
            data.FindProperty("nearMissMargin").vector3Value = new Vector3(0.35f, 0.65f, 1.25f);
            data.ApplyModifiedPropertiesWithoutUndo();
            ObstacleVisual visual = go.AddComponent<ObstacleVisual>();
            SerializedObject vdata = new SerializedObject(visual);
            vdata.FindProperty("modelPrefab").objectReferenceValue = model;
            vdata.FindProperty("modelEuler").vector3Value = Vector3.zero;
            vdata.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            AssetDatabase.DeleteAsset(path);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void ReplaceMeshAsset(Mesh mesh, string path)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
        }

        // ------------------------------------------------------------------
        // Meshes
        // ------------------------------------------------------------------

        private static readonly Color CarBody = new Color(0.15f, 0.09f, 0.2f);
        private static readonly Color CarRoof = new Color(0.22f, 0.14f, 0.28f);
        private static readonly Color CarFace = new Color(0.26f, 0.16f, 0.32f);
        private static readonly Color Glass = new Color(0.05f, 0.03f, 0.08f);
        private static readonly Color Pale = new Color(0.86f, 0.8f, 0.92f);
        private static readonly Color Lamp = new Color(1f, 0.92f, 0.75f) * 2.4f;

        // Subway car in real metres (2.2 × 2.8 × 14), centred; its bounds equal the
        // collider so ObstacleVisual's stretch is 1:1. Front faces -Z (the player).
        private static Mesh TrainMesh()
        {
            MeshBuilder b = new MeshBuilder();
            const float w = TrainWidth * 0.5f, h = TrainHeight, l = TrainLength * 0.5f;
            const float y0 = -h * 0.5f; // centred vertically
            float Y(float y) => y0 + y;

            // Running gear: two bogies with wheels, a dark skirt.
            foreach (float bz in new[] { -l + 2.4f, l - 2.4f })
            {
                b.Box(new Vector3(-0.95f, Y(0f), bz - 1.1f), new Vector3(0.95f, Y(0.32f), bz + 1.1f), Ink);
                foreach (float wz in new[] { bz - 0.7f, bz + 0.7f })
                    foreach (float s in new[] { -1f, 1f })
                        b.Box(new Vector3(s * 0.98f - 0.07f, Y(0.02f), wz - 0.36f), new Vector3(s * 0.98f + 0.07f, Y(0.5f), wz + 0.36f), Stone);
            }
            b.Box(new Vector3(-w, Y(0.3f), -l), new Vector3(w, Y(0.42f), l), Ink);

            // Body shell and roof (roof top kept 2cm low so the trim defines the top).
            b.Box(new Vector3(-w + 0.03f, Y(0.42f), -l + 0.08f), new Vector3(w - 0.03f, Y(2.5f), l - 0.08f), CarBody);
            b.Box(new Vector3(-w + 0.06f, Y(2.5f), -l + 0.06f), new Vector3(w - 0.06f, Y(h - 0.02f), l - 0.06f), CarRoof);
            // Roof: glowing edge rails along both sides, a pale centre walkway,
            // low vent blocks.
            foreach (float s in new[] { -1f, 1f })
                b.Box(new Vector3(s * (w - 0.06f) - 0.04f, Y(h - 0.06f), -l + 0.06f), new Vector3(s * (w - 0.06f) + 0.04f, Y(h), l - 0.06f), Glow);
            b.Box(new Vector3(-0.32f, Y(h - 0.02f), -l + 0.4f), new Vector3(0.32f, Y(h - 0.005f), l - 0.4f), new Color(0.32f, 0.22f, 0.38f));
            for (int i = 0; i < 4; i++)
            {
                float vz = Mathf.Lerp(-l + 2.2f, l - 2.2f, i / 3f);
                b.Box(new Vector3(-0.75f, Y(h - 0.02f), vz - 0.7f), new Vector3(-0.42f, Y(h - 0.005f), vz + 0.7f), Ink);
                b.Box(new Vector3(0.42f, Y(h - 0.02f), vz - 0.7f), new Vector3(0.75f, Y(h - 0.005f), vz + 0.7f), Ink);
            }

            // Decals sit in thin layers proud of the shell (layer 0 innermost), so
            // overlapping details never share a plane (no z-fighting).
            // Sides: door panels and windows, then glass, belt line and window
            // glow, then the door seams.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 Min(int layer, float y, float z) => new Vector3(s > 0 ? w - 0.03f + layer * 0.01f : -w + 0.02f - layer * 0.01f, Y(y), z);
                Vector3 Max(int layer, float y, float z) => new Vector3(s > 0 ? w - 0.02f + layer * 0.01f : -w + 0.03f - layer * 0.01f, Y(y), z);
                b.Box(Min(1, 0.95f, -l + 0.1f), Max(1, 1.05f, l - 0.1f), Glow);
                b.Box(Min(1, 2.38f, -l + 0.1f), Max(1, 2.44f, l - 0.1f), Pale);
                foreach (float dz in new[] { -l * 0.45f, l * 0.45f })
                {
                    b.Box(Min(0, 0.46f, dz - 0.85f), Max(0, 2.3f, dz + 0.85f), CarFace);
                    b.Box(Min(1, 1.25f, dz - 0.7f), Max(1, 2.15f, dz - 0.08f), Glass);
                    b.Box(Min(1, 1.25f, dz + 0.08f), Max(1, 2.15f, dz + 0.7f), Glass);
                    b.Box(Min(2, 0.46f, dz - 0.03f), Max(2, 2.3f, dz + 0.03f), Glow);
                }
                for (int k = 0; k < 9; k++)
                {
                    float wz = Mathf.Lerp(-l + 0.9f, l - 0.9f, k / 8f);
                    if (Mathf.Abs(Mathf.Abs(wz) - l * 0.45f) < 1.1f)
                        continue; // doors
                    b.Box(Min(0, 1.35f, wz - 0.45f), Max(0, 2.1f, wz + 0.45f), Glass);
                    b.Box(Min(2, 1.4f, wz - 0.38f), Max(2, 1.48f, wz + 0.38f), Glow * 0.6f);
                }
            }

            // Front (toward the player) and back: face panel, then windscreen and
            // bumper, then lamps, sign, glow line and the centre pillar.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 Min(int layer, float x, float y) => new Vector3(x, Y(y), s > 0 ? l - 0.08f + layer * 0.02f : -l + 0.06f - layer * 0.02f);
                Vector3 Max(int layer, float x, float y) => new Vector3(x, Y(y), s > 0 ? l - 0.06f + layer * 0.02f : -l + 0.08f - layer * 0.02f);
                b.Box(Min(0, -w + 0.02f, 0.42f), Max(0, w - 0.02f, 2.5f), CarFace);
                b.Box(Min(1, -0.85f, 1.45f), Max(1, 0.85f, 2.25f), Glass);
                b.Box(Min(1, -w + 0.02f, 0.42f), Max(1, w - 0.02f, 0.52f), Pale);
                b.Box(Min(1, -0.5f, 2.32f), Max(1, 0.5f, 2.46f), Ink);
                b.Box(Min(2, -0.85f, 2.25f), Max(2, 0.85f, 2.3f), Glow);
                b.Box(Min(2, -0.44f, 2.35f), Max(2, 0.44f, 2.43f), Glow * 1.2f);
                b.Box(Min(2, -0.06f, 1.45f), Max(2, 0.06f, 2.25f), CarFace);
                foreach (float lx in new[] { -0.72f, 0.72f })
                    b.Box(Min(2, lx - 0.16f, 0.62f), Max(2, lx + 0.16f, 0.86f), s < 0f ? Lamp : Glow);
            }
            return b.Build("Obstacle_SubwayCar");
        }

        // Ramp in the train container's space: rises from the floor at
        // z = -RampLength to RampTop at the train front (z = 0) and runs a short lip
        // over the roof. Dark planks between glowing side rails, with chevrons.
        private static Mesh RampMesh()
        {
            MeshBuilder b = new MeshBuilder();
            const float w = 1.0f;
            Vector3 A = new Vector3(-w, 0f, -RampLength), B = new Vector3(w, 0f, -RampLength);
            Vector3 C = new Vector3(w, RampTop, 0f), D = new Vector3(-w, RampTop, 0f);
            Vector3 E = new Vector3(w, RampTop, RampLip), F = new Vector3(-w, RampTop, RampLip);
            Vector3 G = new Vector3(w, 0f, RampLip), H = new Vector3(-w, 0f, RampLip);
            Color deck = new Color(0.2f, 0.13f, 0.24f), truss = new Color(0.1f, 0.06f, 0.13f);

            Quad(b, A, D, C, B, deck, Vector3.up);
            Quad(b, D, F, E, C, deck, Vector3.up);
            Quad(b, B, C, E, G, truss, Vector3.right);
            Quad(b, H, F, D, A, truss, Vector3.left);
            Quad(b, G, E, F, H, truss, Vector3.forward);

            Vector3 slopeDir = (D - A).normalized;
            Vector3 slopeUp = Vector3.Cross(slopeDir, Vector3.right).normalized;
            if (slopeUp.y < 0f)
                slopeUp = -slopeUp;
            Vector3 OnSlope(float x, float t, float lift) =>
                Vector3.Lerp(new Vector3(x, 0f, -RampLength), new Vector3(x, RampTop, 0f), t) + slopeUp * lift;
            float rampLen = Vector3.Distance(A, D);

            // Plank seams.
            int planks = 18;
            for (int k = 1; k < planks; k++)
            {
                float t = k / (float)planks, dt = 0.012f / rampLen * 2f;
                Quad(b, OnSlope(-w, t - dt, 0.004f), OnSlope(-w, t + dt, 0.004f), OnSlope(w, t + dt, 0.004f), OnSlope(w, t - dt, 0.004f), Ink, slopeUp);
            }
            // Glowing side rails.
            foreach (float x in new[] { -w + 0.04f, w - 0.04f })
                Quad(b, OnSlope(x - 0.06f, 0f, 0.008f), OnSlope(x - 0.06f, 1f, 0.008f), OnSlope(x + 0.06f, 1f, 0.008f), OnSlope(x + 0.06f, 0f, 0.008f), Glow, slopeUp);
            // Chevrons pointing up the ramp.
            for (int c = 0; c < 4; c++)
            {
                float t = 0.15f + c * 0.2f;
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 tip = OnSlope(0f, t + 0.06f, 0.01f), tip2 = OnSlope(0f, t + 0.1f, 0.01f);
                    Vector3 end = OnSlope(s * 0.55f, t, 0.01f), end2 = OnSlope(s * 0.55f, t + 0.04f, 0.01f);
                    Quad(b, end, end2, tip2, tip, Glow * 0.8f, slopeUp);
                }
            }
            // Side truss diagonals.
            foreach (float s in new[] { -1f, 1f })
                for (int k = 0; k < 5; k++)
                {
                    float z0 = Mathf.Lerp(-RampLength, 0f, k / 5f), z1 = Mathf.Lerp(-RampLength, 0f, (k + 1) / 5f);
                    float yTop0 = RampTop * (z0 + RampLength) / RampLength, yTop1 = RampTop * (z1 + RampLength) / RampLength;
                    float x = s * (w + 0.003f);
                    Vector3 p0 = new Vector3(x, 0.05f, z0), p1 = new Vector3(x, Mathf.Max(0.06f, yTop1 - 0.08f), z1);
                    Vector3 off = new Vector3(0f, 0.05f, 0f);
                    if (yTop1 > 0.25f)
                        Quad(b, p0, p1, p1 + off, p0 + off, Glow * 0.5f, s > 0 ? Vector3.right : Vector3.left);
                    Vector3 q0 = new Vector3(x, Mathf.Max(0.05f, yTop0 - 0.06f), z0), q1 = new Vector3(x, Mathf.Max(0.05f, yTop1 - 0.06f), z1);
                    Quad(b, q0, q1, q1 + off * 1.2f, q0 + off * 1.2f, Pale * 0.7f, s > 0 ? Vector3.right : Vector3.left);
                }
            return b.Build("Obstacle_TrainRamp");
        }

        // Convex wedge used only for collision (walkable, harmless).
        private static Mesh RampCollisionMesh()
        {
            const float w = 1.0f;
            Vector3[] v =
            {
                new Vector3(-w, 0f, -RampLength), new Vector3(w, 0f, -RampLength),
                new Vector3(w, RampTop, 0f), new Vector3(-w, RampTop, 0f),
                new Vector3(w, RampTop, RampLip), new Vector3(-w, RampTop, RampLip),
                new Vector3(w, 0f, RampLip), new Vector3(-w, 0f, RampLip),
            };
            int[] t =
            {
                0, 3, 2, 0, 2, 1, 3, 5, 4, 3, 4, 2, // top
                1, 2, 4, 1, 4, 6, 7, 5, 3, 7, 3, 0, // sides
                6, 4, 5, 6, 5, 7, 0, 1, 6, 0, 6, 7, // back, bottom
            };
            Mesh m = new Mesh { name = "Obstacle_TrainRamp_Collision", vertices = v, triangles = t };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // Adds a quad and fixes its winding so it faces `outward`.
        private static void Quad(MeshBuilder b, Vector3 a, Vector3 c1, Vector3 c2, Vector3 d, Color col, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(c1 - a, c2 - a), outward) < 0f)
            {
                (c1, d) = (d, c1);
            }
            int i = b.V.Count;
            b.V.Add(a); b.V.Add(c1); b.V.Add(c2); b.V.Add(d);
            b.C.Add(col); b.C.Add(col); b.C.Add(col); b.C.Add(col);
            b.T.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        // ------------------------------------------------------------------
        // Patterns
        // ------------------------------------------------------------------

        private static Spec Train(this Spec s, int lane, float z) =>
            s.Add(train, $"Train / Lane Blocker {LaneName(lane)}", new Vector3(lane * Lane, 0f, z));
        private static Spec RampTrain(this Spec s, int lane, float z) =>
            s.Add(rampTrain, $"Train Ramp {LaneName(lane)}", new Vector3(lane * Lane, 0f, z));

        // Coins rising along a ramp whose train front is at z (lane centre).
        private static Spec RampCoins(this Spec s, int lane, float frontZ)
        {
            for (int i = 0; i < 5; i++)
            {
                float t = (i + 0.5f) / 5f;
                s.Add(coin, "Coin", new Vector3(lane * Lane, RampTop * t + 1f, frontZ - RampLength * (1f - t)));
            }
            return s;
        }

        private static List<GameObject> BuildTrainPatterns()
        {
            var E = CoursePattern.DifficultyLevel.Easy;
            var M = CoursePattern.DifficultyLevel.Medium;
            var H = CoursePattern.DifficultyLevel.Hard;
            var obstacle = CoursePattern.PatternCategory.Obstacle;
            var mixed = CoursePattern.PatternCategory.Mixed;
            float roofCoin = TrainRoofY + 1f;

            List<Spec> specs = new List<Spec>
            {
                // Two parked trains leave a corridor down the middle. Air souls hang
                // in the corridor; a ground soul waits past the end.
                P("Pattern_R_TrainCorridor", 36, E, obstacle)
                    .Train(-1, 6).Train(1, 6)
                    .Air(0, 2.4f, 11).Air(0, 3f, 15)
                    .Enemy(0, 30)
                    .Coins(0, 1f, 7, 3).Coins(0, 1f, 20.5f, 4).Gift(0, 24),

                // A ramp train in the middle, parked trains either side: run up, take
                // the souls above the roof, drop into the corridor. The trailing
                // ground soul sits past the landing after the roof (~18m at top speed).
                P("Pattern_S_RampRun", 56, M, mixed)
                    .RampTrain(0, 13.5f).Train(-1, 16.5f).Train(1, 16.5f)
                    .Air(0, TrainRoofY + 2.4f, 20).Air(0, TrainRoofY + 3f, 24)
                    .Enemy(0, 50f)
                    .RampCoins(0, 13.5f).Coins(0, roofCoin, 15, 3).Coins(0, roofCoin, 26, 1)
                    .Power("SoulAmplifier", 0, 17.5f, roofCoin).Gift(0, 44),

                // Swerve past a parked train, then climb a ramp train in the far lane.
                P("Pattern_T_TrainWeave", 60, H, obstacle)
                    .Train(-1, 4)
                    .Air(0, 2.4f, 9)
                    .RampTrain(1, 19f)
                    .Air(1, TrainRoofY + 2.4f, 25).Air(1, TrainRoofY + 3f, 29)
                    .Enemy(0, 55)
                    .Coins(0, 1f, 5, 3).RampCoins(1, 19f).Coins(1, roofCoin, 20, 4)
                    // Rush passes through obstacles, roofs included: keep it on the ground.
                    .Power("ReaperRush", 0, 44f),
            };

            List<GameObject> built = new List<GameObject>();
            foreach (Spec spec in specs)
                built.Add(SavePattern(spec));
            return built;
        }

        // ------------------------------------------------------------------
        // Batch helpers (validation copy): build, then render preview images.
        // ------------------------------------------------------------------

        public static void BuildTrainsBatch()
        {
            BuildTrains();
            string folder = Environment.GetEnvironmentVariable("TRAIN_PREVIEW_DIR");
            if (!string.IsNullOrEmpty(folder))
                RenderTrainPreviews(folder);
        }

        public static void RenderTrainPreviews(string folder)
        {
            System.IO.Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.45f, 0.62f);
            Light sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.5f, 60f);
            floor.transform.localScale = new Vector3(8.6f, 1f, 200f);
            floor.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.2f, 0.16f, 0.24f) };

            var placed = new List<(string, float)> { ("Pattern_S_RampRun", 0f), ("Pattern_R_TrainCorridor", 60f), ("Pattern_T_TrainWeave", 110f) };
            foreach (var (name, z) in placed)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PatternFolder + name + ".prefab");
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = new Vector3(0f, 0f, z);
                foreach (ObstacleVisual v in go.GetComponentsInChildren<ObstacleVisual>())
                    typeof(ObstacleVisual).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(v, null);
            }

            Shoot(folder + "/train_ramp_side.png", new Vector3(9f, 6f, 2f), new Vector3(0f, 1.6f, 18f), 50f);
            Shoot(folder + "/train_ramp_runner.png", new Vector3(0f, 2.6f, -2f), new Vector3(0f, 2f, 16f), 60f);
            Shoot(folder + "/train_corridor.png", new Vector3(0f, 2.6f, 58f), new Vector3(0f, 1.6f, 75f), 60f);
            Shoot(folder + "/train_weave.png", new Vector3(-7f, 7f, 105f), new Vector3(1f, 1.5f, 128f), 55f);
            Shoot(folder + "/train_front.png", new Vector3(-1.5f, 1.8f, 12f), new Vector3(-2.5f, 1.5f, 17f), 55f);
        }

        private static void Shoot(string path, Vector3 position, Vector3 lookAt, float fov)
        {
            Camera cam = new GameObject("PreviewCam").AddComponent<Camera>();
            cam.transform.position = position;
            cam.transform.LookAt(lookAt);
            cam.fieldOfView = fov;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.42f, 0.32f, 0.5f);
            var rt = new RenderTexture(960, 640, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(cam.gameObject);
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
