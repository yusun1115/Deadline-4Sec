using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Builds the extended pattern library (2026-10-01): train-roof runs, a giant
    // clock-tower gate, gothic arch corridors and remixed combat/obstacle beats.
    // Patterns reuse the existing Enemy/Obstacle/Pickup prefabs as nested
    // instances, so gameplay rules stay identical. Spacing follows the proven
    // patterns A–E (ground enemies in adjacent lanes ≥5m apart, obstacles ≥6m).
    // Set pieces are decoration only: no colliders except the train wagons,
    // which are ordinary Jump obstacles (front = hit, roof = safe to run on).
    public static class PatternLibraryBuilder
    {
        private const string PatternFolder = "Assets/Prefab/Pattern/";
        private const string DecorFolder = "Assets/Prefab/Decor/";
        private const string ModelFolder = "Assets/Art/3DModel/SetPieces/";
        private const float Lane = 2.5f;
        private const float GroundEnemyY = 0.74f;
        private const float RoofY = 1.0f;

        private static GameObject enemy, enemyAir, jump, slide, blocker, coin, gift, wagon, wagonDouble;
        private static readonly Dictionary<string, GameObject> PowerUps = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, GameObject> Decor = new Dictionary<string, GameObject>();

        [MenuItem("Deadline 4 Sec/Course/Build Extended Pattern Library")]
        public static void Build()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the open scene before rebuilding patterns.");
            System.IO.Directory.CreateDirectory(DecorFolder);
            System.IO.Directory.CreateDirectory(ModelFolder);
            LoadPrefabs();
            BuildSetPieces();
            List<GameObject> built = BuildPatterns();
            RegisterInScenes(built);
            AssetDatabase.SaveAssets();
            Debug.Log("Extended pattern library: " + built.Count + " new patterns.");
        }

        private static void LoadPrefabs()
        {
            GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path) ??
                throw new InvalidOperationException("Missing " + path);
            enemy = Load("Assets/Prefab/Enemy/Enemy.prefab");
            enemyAir = Load("Assets/Prefab/Enemy/EnemyAir.prefab");
            jump = Load("Assets/Prefab/Obstacle/Jump Obstacle.prefab");
            slide = Load("Assets/Prefab/Obstacle/Slide Obstacle.prefab");
            blocker = Load("Assets/Prefab/Obstacle/Lane Blocker.prefab");
            coin = Load("Assets/Prefab/Pickup/Coin.prefab");
            gift = Load("Assets/Prefab/Pickup/GiftBox.prefab");
            foreach (string p in new[] { "FreezeClock", "SoulAmplifier", "ReaperRush", "SoulMagnet", "ComboSeal", "TimeHeart" })
                PowerUps[p] = Load("Assets/Prefab/Pickup/" + p + "_Pickup.prefab");
        }

        // ------------------------------------------------------------------
        // Set pieces
        // ------------------------------------------------------------------

        private static void BuildSetPieces()
        {
            Material stone = Material("DecorStone", new Color(1f, 1f, 1f), new Color(1f, 0.25f, 0.6f), 3.5f);
            Material iron = Material("TrainIron", new Color(1f, 1f, 1f), new Color(1f, 0.2f, 0.55f), 5f);

            Decor["ClockGate"] = SavePiece("Decor_ClockGate", ClockGateMesh(), stone);
            Decor["Arch"] = SavePiece("Decor_GothicArch", ArchMesh(), stone);
            GameObject wagonModel = SavePiece("Obstacle_TrainWagon", WagonMesh(1), iron, ModelFolder);
            GameObject wagonDoubleModel = SavePiece("Obstacle_TrainWagonDouble", WagonMesh(2), iron, ModelFolder);
            wagon = SaveWagonObstacle("Train Wagon", 10f, wagonModel);
            wagonDouble = SaveWagonObstacle("Train Wagon Double", 22f, wagonDoubleModel);
        }

        private static Material Material(string name, Color tint, Color rim, float rimPower)
        {
            string path = ModelFolder + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Deadline4Sec/VertexColorLit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_Tint", tint);
            m.SetColor("_RimColor", rim);
            m.SetFloat("_RimPower", rimPower);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static GameObject SavePiece(string name, Mesh mesh, Material material, string folder = DecorFolder)
        {
            string meshPath = ModelFolder + name + "_Mesh.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            GameObject go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Replace the prefab outright; overwriting kept a stale mesh reference.
            AssetDatabase.DeleteAsset(folder + name + ".prefab");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, folder + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        // A full-width freight wagon. The root sits on the floor at the wagon's
        // front face (so distance checks measure to the face you must clear);
        // the box extends forward. Jump onto the roof, run on it, drop off the end.
        private static GameObject SaveWagonObstacle(string name, float length, GameObject model)
        {
            string path = "Assets/Prefab/Obstacle/" + name + ".prefab";
            GameObject root = new GameObject(name);
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.5f, length * 0.5f);
            box.size = new Vector3(8.6f, 1f, length);
            Obstacle obstacle = root.AddComponent<Obstacle>();
            SerializedObject data = new SerializedObject(obstacle);
            data.FindProperty("obstacleType").enumValueIndex = (int)Obstacle.ObstacleType.JumpObstacle;
            data.ApplyModifiedPropertiesWithoutUndo();
            ObstacleVisual visual = root.AddComponent<ObstacleVisual>();
            SerializedObject vdata = new SerializedObject(visual);
            vdata.FindProperty("modelPrefab").objectReferenceValue = model;
            vdata.FindProperty("modelEuler").vector3Value = Vector3.zero;
            vdata.ApplyModifiedPropertiesWithoutUndo();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private sealed class MeshBuilder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int> T = new List<int>();

            public void Box(Vector3 min, Vector3 max, Color col)
            {
                Vector3[] p =
                {
                    new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z),
                    new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z),
                };
                int[][] faces = { new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 4, 7, 3 }, new[] { 1, 2, 6, 5 }, new[] { 3, 7, 6, 2 }, new[] { 0, 1, 5, 4 } };
                foreach (int[] f in faces)
                {
                    int b = V.Count;
                    foreach (int i in f) { V.Add(p[i]); C.Add(col); }
                    T.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                }
            }

            // Pyramid roof: clockwise from outside so back-face culling keeps it.
            public void Spire(Vector3 baseCenter, float half, float height, Color col)
            {
                Vector3 top = baseCenter + Vector3.up * height;
                Vector3 a = baseCenter + new Vector3(-half, 0, -half), b = baseCenter + new Vector3(half, 0, -half);
                Vector3 c = baseCenter + new Vector3(half, 0, half), d = baseCenter + new Vector3(-half, 0, half);
                foreach (var (p, q) in new[] { (a, b), (b, c), (c, d), (d, a) })
                {
                    int i = V.Count;
                    V.Add(p); V.Add(top); V.Add(q);
                    C.Add(col); C.Add(col); C.Add(col);
                    T.AddRange(new[] { i, i + 1, i + 2 });
                }
            }

            // Flat ring facing -Z (toward the oncoming player).
            public void Ring(Vector3 center, float inner, float outer, int segments, Color col)
            {
                for (int s = 0; s < segments; s++)
                {
                    float a0 = s * Mathf.PI * 2f / segments, a1 = (s + 1) * Mathf.PI * 2f / segments;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f), d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                    int i = V.Count;
                    V.Add(center + d0 * inner); V.Add(center + d0 * outer); V.Add(center + d1 * outer); V.Add(center + d1 * inner);
                    C.Add(col); C.Add(col); C.Add(col); C.Add(col);
                    T.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
                }
            }

            public Mesh Build(string name)
            {
                Mesh m = new Mesh { name = name, indexFormat = V.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                m.SetVertices(V);
                m.SetColors(C);
                m.SetTriangles(T, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        private static readonly Color Stone = new Color(0.16f, 0.11f, 0.2f);
        private static readonly Color StoneLight = new Color(0.3f, 0.22f, 0.36f);
        private static readonly Color Ink = new Color(0.06f, 0.04f, 0.08f);
        private static readonly Color Glow = new Color(1f, 0.18f, 0.55f) * 2.2f;
        private static readonly Color Bone = new Color(0.9f, 0.85f, 0.92f);

        // Giant clock-tower gate straddling the track: legs outside the 8.6m
        // floor, an arch beam high above, a huge clock face toward the player.
        private static Mesh ClockGateMesh()
        {
            MeshBuilder b = new MeshBuilder();
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box(new Vector3(s * 6.4f - 1.3f, -8f, -1.3f), new Vector3(s * 6.4f + 1.3f, 21f, 1.3f), Stone);
                b.Box(new Vector3(s * 6.4f - 1.6f, -8f, -1.6f), new Vector3(s * 6.4f + 1.6f, 1.2f, 1.6f), StoneLight);
                b.Box(new Vector3(s * 6.4f - 0.25f, 4f, -1.35f), new Vector3(s * 6.4f + 0.25f, 12f, -1.3f), Glow);
                b.Spire(new Vector3(s * 6.4f, 21f, 0f), 1.4f, 7f, Stone);
            }
            b.Box(new Vector3(-8f, 15f, -1.5f), new Vector3(8f, 19f, 1.5f), Stone);
            b.Box(new Vector3(-8.3f, 14.4f, -1.7f), new Vector3(8.3f, 15f, 1.7f), StoneLight);
            b.Box(new Vector3(-3.4f, 19f, -1.2f), new Vector3(3.4f, 27f, 1.2f), Stone);
            b.Spire(new Vector3(0f, 27f, 0f), 3.2f, 9f, Stone);
            Vector3 face = new Vector3(0f, 22.8f, -1.3f);
            b.Ring(face, 0f, 3.1f, 36, Bone);
            b.Ring(face + new Vector3(0, 0, -0.02f), 3.1f, 3.6f, 36, StoneLight);
            b.Ring(face + new Vector3(0, 0, -0.03f), 2.75f, 2.85f, 36, Glow);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 p = face + d * 2.4f + new Vector3(0, 0, -0.04f);
                b.Box(p - new Vector3(0.12f, 0.12f, 0f), p + new Vector3(0.12f, 0.12f, 0.01f), Ink);
            }
            b.Box(face + new Vector3(-0.1f, 0f, -0.06f), face + new Vector3(0.1f, 2.2f, -0.05f), Ink);
            b.Box(face + new Vector3(0f, -0.1f, -0.07f), face + new Vector3(1.6f, 0.1f, -0.06f), Glow);
            // Hanging chains from the beam frame the passage.
            foreach (float x in new[] { -4.6f, 4.6f })
                for (int k = 0; k < 14; k++)
                {
                    float y = 14.4f - k * 0.55f;
                    float w = k % 2 == 0 ? 0.14f : 0.05f;
                    b.Box(new Vector3(x - w, y - 0.5f, -w), new Vector3(x + w, y, w), Ink);
                }
            return b.Build("Decor_ClockGate");
        }

        // Pointed gothic arch over the track; several make a corridor.
        private static Mesh ArchMesh()
        {
            MeshBuilder b = new MeshBuilder();
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box(new Vector3(s * 5.7f - 0.55f, -6f, -0.55f), new Vector3(s * 5.7f + 0.55f, 8f, 0.55f), Stone);
                b.Box(new Vector3(s * 5.7f - 0.75f, 8f, -0.75f), new Vector3(s * 5.7f + 0.75f, 8.4f, 0.75f), StoneLight);
                b.Box(new Vector3(s * 5.7f + -s * 0.6f - 0.12f, 5f, -0.2f), new Vector3(s * 5.7f + -s * 0.6f + 0.12f, 6f, 0.2f), Glow);
            }
            // Stepped pointed arch from both pillars to the apex.
            for (int i = 0; i < 8; i++)
            {
                float t = i / 7f;
                float x = Mathf.Lerp(5.7f, 0.4f, t);
                float y = 8.2f + Mathf.Sin(t * Mathf.PI * 0.5f) * 4.2f;
                foreach (float s in new[] { -1f, 1f })
                    b.Box(new Vector3(s * x - 0.55f, y - 0.4f, -0.45f), new Vector3(s * x + 0.55f, y + 0.4f, 0.45f), Stone);
            }
            b.Spire(new Vector3(0f, 12.6f, 0f), 0.6f, 2.6f, StoneLight);
            b.Box(new Vector3(-0.15f, 11.4f, -0.5f), new Vector3(0.15f, 12.2f, -0.46f), Glow);
            return b.Build("Decor_GothicArch");
        }

        // Freight wagon(s) in a unit box (-0.5..0.5) that ObstacleVisual stretches
        // to the collider: dark iron body, light roof walkway, pink window strips,
        // wheels and a glowing front lamp.
        private static Mesh WagonMesh(int cars)
        {
            MeshBuilder b = new MeshBuilder();
            float gap = cars > 1 ? 0.03f : 0f;
            float carLength = (1f - gap * (cars - 1)) / cars;
            for (int c = 0; c < cars; c++)
            {
                float z0 = -0.5f + c * (carLength + gap), z1 = z0 + carLength;
                b.Box(new Vector3(-0.48f, -0.32f, z0), new Vector3(0.48f, 0.44f, z1), Ink);
                // Dark roof deck with plank seams and two walkway rails.
                b.Box(new Vector3(-0.5f, 0.44f, z0 - 0.002f), new Vector3(0.5f, 0.5f, z1 + 0.002f), new Color(0.12f, 0.08f, 0.15f));
                int planks = Mathf.Max(6, Mathf.RoundToInt(carLength * 24f));
                for (int k = 1; k < planks; k++)
                {
                    float pz = Mathf.Lerp(z0, z1, k / (float)planks);
                    b.Box(new Vector3(-0.5f, 0.5f, pz - 0.0015f), new Vector3(0.5f, 0.503f, pz + 0.0015f), Ink);
                }
                foreach (float rx in new[] { -0.36f, 0.36f })
                    b.Box(new Vector3(rx - 0.01f, 0.5f, z0), new Vector3(rx + 0.01f, 0.506f, z1), new Color(0.55f, 0.5f, 0.62f));
                b.Box(new Vector3(-0.5f, 0.5f, z0), new Vector3(-0.47f, 0.508f, z1), Glow);
                b.Box(new Vector3(0.47f, 0.5f, z0), new Vector3(0.5f, 0.508f, z1), Glow);
                b.Box(new Vector3(-0.5f, -0.36f, z0), new Vector3(0.5f, -0.3f, z1), Stone);
                int windows = Mathf.Max(3, Mathf.RoundToInt(carLength * 10f));
                for (int w = 0; w < windows; w++)
                {
                    float wz = Mathf.Lerp(z0, z1, (w + 0.5f) / windows);
                    float half = carLength / windows * 0.3f;
                    foreach (float s in new[] { -1f, 1f })
                        b.Box(new Vector3(s * 0.485f - 0.01f, 0.02f, wz - half), new Vector3(s * 0.485f + 0.01f, 0.24f, wz + half), Glow);
                }
                foreach (float wz in new[] { z0 + carLength * 0.18f, z1 - carLength * 0.18f })
                    foreach (float s in new[] { -0.32f, 0.32f })
                        b.Box(new Vector3(s - 0.07f, -0.5f, wz - 0.03f), new Vector3(s + 0.07f, -0.3f, wz + 0.03f), Ink);
                if (c < cars - 1)
                    b.Box(new Vector3(-0.08f, -0.2f, z1), new Vector3(0.08f, 0.05f, z1 + gap), Stone);
            }
            // Front face: lamp and a bone-white warning stripe at roof height.
            b.Box(new Vector3(-0.12f, 0.1f, -0.51f), new Vector3(0.12f, 0.3f, -0.5f), Glow);
            b.Box(new Vector3(-0.5f, 0.38f, -0.505f), new Vector3(0.5f, 0.44f, -0.5f), Bone);
            return b.Build(cars > 1 ? "Obstacle_TrainWagonDouble" : "Obstacle_TrainWagon");
        }

        // ------------------------------------------------------------------
        // Patterns
        // ------------------------------------------------------------------

        private sealed class Spec
        {
            public string Name;
            public float Length;
            public CoursePattern.DifficultyLevel Difficulty;
            public CoursePattern.PatternCategory Category;
            public CoursePattern.TravelState Entry, Exit;
            public readonly List<Action<Transform>> Items = new List<Action<Transform>>();
        }

        private static Spec P(string name, float length, CoursePattern.DifficultyLevel d, CoursePattern.PatternCategory c,
            CoursePattern.TravelState entry = CoursePattern.TravelState.Ground, CoursePattern.TravelState exit = CoursePattern.TravelState.Ground) =>
            new Spec { Name = name, Length = length, Difficulty = d, Category = c, Entry = entry, Exit = exit };

        private static Spec Enemy(this Spec s, int lane, float z, float floor = 0f) =>
            s.Add(enemy, $"Enemy {LaneName(lane)}", new Vector3(lane * Lane, GroundEnemyY + floor, z));
        private static Spec Air(this Spec s, int lane, float y, float z) =>
            s.Add(enemyAir, $"Air {LaneName(lane)}", new Vector3(lane * Lane, y, z));
        private static Spec Jump(this Spec s, int lane, float z) =>
            s.Add(jump, $"Jump / Near Miss {LaneName(lane)}", new Vector3(lane * Lane, 0.38f, z));
        private static Spec Slide(this Spec s, int lane, float z) =>
            s.Add(slide, $"Slide / Near Miss {LaneName(lane)}", new Vector3(lane * Lane, 2.56f, z));
        private static Spec Block(this Spec s, int lane, float z) =>
            s.Add(blocker, $"Lane Blocker / Near Miss {LaneName(lane)}", new Vector3(lane * Lane, 1.02f, z));
        private static Spec Wagon(this Spec s, float z, bool twoCars = false) =>
            s.Add(twoCars ? wagonDouble : wagon, twoCars ? "Train Wagon Double" : "Train Wagon", new Vector3(0f, 0f, z));
        private static Spec Coins(this Spec s, int lane, float y, float z0, int count, float step = 1.2f)
        {
            for (int i = 0; i < count; i++)
                s.Add(coin, "Coin", new Vector3(lane * Lane, y, z0 + i * step));
            return s;
        }
        private static Spec Power(this Spec s, string type, int lane, float z, float y = 1f) =>
            s.Add(PowerUps[type], type + "_Pickup", new Vector3(lane * Lane, y, z));
        private static Spec Gift(this Spec s, int lane, float z, float y = 1f) =>
            s.Add(gift, "GiftBox", new Vector3(lane * Lane, y, z));
        private static Spec Set(this Spec s, string decor, float z) =>
            s.Add(Decor[decor], "SetPiece " + decor, new Vector3(0f, 0f, z));

        private static Spec Add(this Spec s, GameObject prefab, string name, Vector3 position)
        {
            s.Items.Add(parent =>
            {
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.localPosition = position;
                go.name = name;
            });
            return s;
        }

        private static string LaneName(int lane) => lane < 0 ? "Left" : lane > 0 ? "Right" : "Center";

        private static List<GameObject> BuildPatterns()
        {
            var E = CoursePattern.DifficultyLevel.Easy;
            var M = CoursePattern.DifficultyLevel.Medium;
            var H = CoursePattern.DifficultyLevel.Hard;
            var ground = CoursePattern.PatternCategory.GroundCombat;
            var air = CoursePattern.PatternCategory.AirCombat;
            var obstacle = CoursePattern.PatternCategory.Obstacle;
            var stomp = CoursePattern.PatternCategory.Stomp;
            var mixed = CoursePattern.PatternCategory.Mixed;
            var G = CoursePattern.TravelState.Ground;
            var A = CoursePattern.TravelState.Air;
            var Any = CoursePattern.TravelState.Any;

            List<Spec> specs = new List<Spec>
            {
                // ---- Easy ----
                P("Pattern_F_Crossfire", 24, E, ground)
                    .Enemy(-1, 5).Enemy(0, 10).Enemy(1, 15).Enemy(0, 20)
                    .Coins(1, 1f, 6, 3).Coins(-1, 1f, 16, 3).Power("FreezeClock", 1, 12).Gift(-1, 21),
                // A jump airtime covers ~8m, so never chain two jumps; alternate with slides.
                P("Pattern_G_Hurdles", 30, E, obstacle)
                    .Jump(0, 4).Slide(0, 12).Jump(0, 21).Slide(0, 28)
                    .Coins(0, 1.9f, 3.4f, 2).Coins(0, 0.7f, 11, 3, 1f).Coins(0, 1.9f, 20.4f, 2).Power("SoulMagnet", -1, 8),
                P("Pattern_H_TrainHop", 26, E, mixed)
                    .Wagon(5).Enemy(1, 11, RoofY).Enemy(0, 21)
                    .Coins(0, RoofY + 1f, 6.5f, 6).Power("ComboSeal", 0, 18).Gift(-1, 23),
                P("Pattern_I_ArchWalk", 26, E, ground)
                    .Set("Arch", 2).Set("Arch", 12).Set("Arch", 22)
                    .Enemy(1, 6).Enemy(0, 12).Enemy(-1, 18).Enemy(0, 24)
                    .Coins(0, 1f, 7, 3).Coins(1, 1f, 19, 3).Power("SoulAmplifier", -1, 9),

                // ---- Medium ----
                // Ground entry only: dropping in from a high air combo lands in the
                // first enemy's front zone. Ends on the stomp so the bounce lands clear.
                // Air exits must end as high as Pattern C (4.2m) so the next stomp
                // pattern's first head is reachable from the bounce.
                P("Pattern_J_ClockGate", 24, M, mixed, G, A)
                    .Set("ClockGate", 14)
                    .Enemy(0, 4).Jump(0, 9).Air(0, 2.5f, 14).Air(1, 3.4f, 18).Air(0, 4.2f, 22)
                    .Coins(-1, 1f, 5, 3).Coins(0, 3.6f, 14.5f, 3).Power("TimeHeart", -1, 11).Gift(1, 20, 4f),
                P("Pattern_K_TrainRun", 34, M, mixed)
                    .Wagon(4, true).Enemy(-1, 10, RoofY).Enemy(0, 16, RoofY).Enemy(1, 22, RoofY).Enemy(0, 31)
                    .Coins(0, RoofY + 1f, 7, 4).Coins(1, RoofY + 1f, 17, 3).Power("ReaperRush", -1, 19, RoofY + 1f).Gift(1, 12, RoofY + 1f),
                P("Pattern_L_AirLadder", 18, M, air, Any, A)
                    .Air(1, 2.4f, 3).Air(0, 3f, 7).Air(-1, 3.6f, 11).Air(0, 4.2f, 15)
                    .Coins(1, 3.4f, 4.5f, 1).Coins(0, 4f, 8.5f, 1).Coins(-1, 4.6f, 12.5f, 1).Power("SoulAmplifier", 1, 6, 1.2f),
                P("Pattern_M_StompStairs", 20, M, stomp, A, A)
                    .Enemy(1, 1).Enemy(0, 9).Enemy(-1, 16)
                    .Coins(0, 1.1f, 4, 2).Coins(-1, 1.1f, 12, 2).Power("FreezeClock", 0, 6),

                // ---- Hard ----
                P("Pattern_N_Gauntlet", 32, H, mixed, G, G)
                    .Set("Arch", 3).Set("Arch", 15).Set("Arch", 27)
                    .Enemy(0, 4).Slide(0, 10).Enemy(1, 15).Jump(0, 20).Block(0, 27)
                    .Coins(0, 0.7f, 9, 3, 1f).Coins(0, 1.9f, 19.4f, 2).Power("ComboSeal", -1, 23).Gift(1, 29),
                P("Pattern_O_TrainCombo", 30, H, mixed, G, A)
                    .Wagon(4).Air(0, RoofY + 2.4f, 10).Air(1, RoofY + 3f, 14).Air(0, RoofY + 3.6f, 18).Air(-1, RoofY + 4.2f, 22)
                    .Coins(0, RoofY + 1f, 5, 4).Coins(1, RoofY + 4f, 14.8f, 1).Power("TimeHeart", 0, 26, 1.4f),
                P("Pattern_P_ClockAirStorm", 22, H, air, Any, A)
                    .Set("ClockGate", 10)
                    .Air(-1, 2.4f, 3).Air(0, 3.2f, 7).Air(1, 4f, 11).Air(0, 4.6f, 15).Air(-1, 5f, 19)
                    .Coins(0, 4f, 7.8f, 1).Coins(1, 4.8f, 11.8f, 1).Gift(0, 16, 5.4f),
                // Hard runs are fast; leave ~8m between obstacles so landings settle.
                P("Pattern_Q_ObstacleRush", 40, H, obstacle)
                    .Jump(0, 4).Slide(0, 12).Jump(0, 20).Block(0, 28).Slide(1, 36)
                    .Coins(0, 1.9f, 3.4f, 2).Coins(0, 0.7f, 11, 3, 1f).Coins(1, 1f, 29, 3).Power("ReaperRush", -1, 16),
            };

            List<GameObject> built = new List<GameObject>();
            foreach (Spec spec in specs)
                built.Add(SavePattern(spec));
            RestrictLegacyEntry();
            return built;
        }

        // Pattern E opens with a ground enemy and ends with a lane blocker after a
        // stomp; entering it from the new high air-combo exits (L, M, J, P) drops
        // the runner into those threats. Its layout stays; only its entry narrows.
        private static void RestrictLegacyEntry()
        {
            const string path = PatternFolder + "Pattern_E_MixedRisk.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SerializedObject data = new SerializedObject(root.GetComponent<CoursePattern>());
                data.FindProperty("entryState").enumValueIndex = (int)CoursePattern.TravelState.Ground;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject SavePattern(Spec spec)
        {
            GameObject root = new GameObject(spec.Name);
            CoursePattern pattern = root.AddComponent<CoursePattern>();
            SerializedObject data = new SerializedObject(pattern);
            data.FindProperty("patternLength").floatValue = spec.Length;
            data.FindProperty("difficulty").enumValueIndex = (int)spec.Difficulty;
            data.FindProperty("category").enumValueIndex = (int)spec.Category;
            data.FindProperty("entryState").enumValueIndex = (int)spec.Entry;
            data.FindProperty("exitState").enumValueIndex = (int)spec.Exit;
            data.ApplyModifiedPropertiesWithoutUndo();
            int index = 1;
            foreach (Action<Transform> add in spec.Items)
                add(root.transform);
            // Number gameplay beats in travel order like the hand-made patterns.
            foreach (Transform child in root.transform.Cast<Transform>().OrderBy(t => t.localPosition.z).ToList())
                if (!child.name.StartsWith("Coin") && !child.name.Contains("_Pickup") && !child.name.StartsWith("Gift") &&
                    !child.name.StartsWith("SetPiece"))
                    child.name = (index++).ToString("00") + " " + child.name;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PatternFolder + spec.Name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void RegisterInScenes(List<GameObject> built)
        {
            string original = EditorSceneManager.GetActiveScene().path;
            foreach (string path in new[] { "Assets/Scenes/EndlessRun.unity", "Assets/Scenes/GrayboxCourse.unity", "Assets/Scenes/TestScene.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                PatternSpawner spawner = UnityEngine.Object.FindFirstObjectByType<PatternSpawner>();
                if (spawner == null)
                    continue;
                SerializedObject data = new SerializedObject(spawner);
                SerializedProperty list = data.FindProperty("patternPrefabs");
                HashSet<UnityEngine.Object> present = new HashSet<UnityEngine.Object>();
                for (int i = 0; i < list.arraySize; i++)
                    present.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
                foreach (GameObject prefab in built)
                {
                    CoursePattern pattern = prefab.GetComponent<CoursePattern>();
                    if (present.Contains(pattern))
                        continue;
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = pattern;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (!string.IsNullOrEmpty(original))
                EditorSceneManager.OpenScene(original);
        }
    }
}
