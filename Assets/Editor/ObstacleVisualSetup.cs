using UnityEditor;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Adds ObstacleVisual to the three obstacle prefabs. Converted Tripo models
    // are stretched to the hazard box at runtime; until a model exists the cube
    // gets a dark gothic stone look with a pink danger rim.
    public static class ObstacleVisualSetup
    {
        private const string ModelFolder = "Assets/Art/3DModel/Obstacles/";
        private const string StonePath = "Assets/Art/Materials/Environment/ObstacleStone.mat";

        private static readonly (string prefab, string model, Vector3 euler)[] Map =
        {
            ("Assets/Prefab/Obstacle/Jump Obstacle.prefab", "Obstacle_Barricade", new Vector3(0f, 180f, 0f)),
            ("Assets/Prefab/Obstacle/Lane Blocker.prefab", "Obstacle_Monolith", new Vector3(0f, 180f, 0f)),
            ("Assets/Prefab/Obstacle/Slide Obstacle.prefab", "Obstacle_SpikedBeam", Vector3.zero),
        };

        [MenuItem("Deadline 4 Sec/Art/Apply Obstacle Visuals")]
        public static void Apply()
        {
            Material stone = AssetDatabase.LoadAssetAtPath<Material>(StonePath);
            if (stone == null)
            {
                stone = new Material(Shader.Find("Deadline4Sec/VertexColorLit")) { name = "ObstacleStone" };
                AssetDatabase.CreateAsset(stone, StonePath);
            }
            stone.SetColor("_Tint", new Color(0.2f, 0.14f, 0.27f));
            stone.SetColor("_RimColor", new Color(1f, 0.2f, 0.55f));
            stone.SetFloat("_RimPower", 2.2f);
            EditorUtility.SetDirty(stone);

            BuildSpikedBeam();
            foreach (string name in new[] { "Obstacle_Barricade", "Obstacle_Monolith" })
                HazardGlow(AssetDatabase.LoadAssetAtPath<Material>(ModelFolder + name + ".mat"));

            foreach (var entry in Map)
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + entry.model + ".prefab");
                GameObject root = PrefabUtility.LoadPrefabContents(entry.prefab);
                try
                {
                    ObstacleVisual visual = (root.TryGetComponent(out ObstacleVisual existingObstacleVisual) ? existingObstacleVisual : root.AddComponent<ObstacleVisual>());
                    SerializedObject so = new SerializedObject(visual);
                    so.FindProperty("modelPrefab").objectReferenceValue = model;
                    so.FindProperty("modelEuler").vector3Value = entry.euler;
                    so.FindProperty("fallbackMaterial").objectReferenceValue = stone;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, entry.prefab);
                    Debug.Log($"ObstacleVisual on {entry.prefab} model={(model != null ? model.name : "stone fallback")}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
        }

        // Obstacles must read as hazards at a glance: strong pink rim, faint glow.
        private static void HazardGlow(Material m)
        {
            if (m == null)
                return;
            m.SetColor("_Tint", new Color(1.15f, 1.05f, 1.2f));
            m.SetColor("_RimColor", new Color(1f, 0.18f, 0.55f) * 1.6f);
            m.SetFloat("_RimPower", 1.8f);
            m.SetColor("_EmissionColor", new Color(0.12f, 0.02f, 0.08f));
            EditorUtility.SetDirty(m);
        }

        // Slide hazard: an iron beam hung on chains with spikes pointing down, so
        // "go under it" reads instantly. Authored in a unit box (x,y,z in -0.5..0.5)
        // because ObstacleVisual stretches the model to the hazard box.
        private static void BuildSpikedBeam()
        {
            System.IO.Directory.CreateDirectory(ModelFolder);
            var v = new System.Collections.Generic.List<Vector3>();
            var c = new System.Collections.Generic.List<Color>();
            var t = new System.Collections.Generic.List<int>();
            Color iron = new Color(0.09f, 0.06f, 0.12f), edge = new Color(0.35f, 0.3f, 0.42f);
            Color glow = new Color(1f, 0.15f, 0.5f) * 1.4f;
            void Box(Vector3 min, Vector3 max, Color col)
            {
                Vector3[] p =
                {
                    new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z),
                    new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z),
                };
                int[][] faces = { new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 4, 7, 3 }, new[] { 1, 2, 6, 5 }, new[] { 3, 7, 6, 2 }, new[] { 0, 1, 5, 4 } };
                foreach (int[] f in faces)
                {
                    int b = v.Count;
                    foreach (int i in f) { v.Add(p[i]); c.Add(col); }
                    t.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                }
            }
            void Spike(float x, float top, float bottom, float half, Color col)
            {
                Vector3 tip = new Vector3(x, bottom, 0f);
                Vector3 a = new Vector3(x - half, top, -0.45f), b = new Vector3(x + half, top, -0.45f);
                Vector3 d = new Vector3(x + half, top, 0.45f), e = new Vector3(x - half, top, 0.45f);
                foreach (var tri in new[] { (a, b), (b, d), (d, e), (e, a) })
                {
                    int i0 = v.Count;
                    // Clockwise when seen from outside, so the faces survive back-face culling.
                    v.Add(tri.Item2); v.Add(tip); v.Add(tri.Item1);
                    c.Add(col); c.Add(glow); c.Add(col);
                    t.AddRange(new[] { i0, i0 + 1, i0 + 2 });
                }
            }
            // Beam across the bottom third, with a glowing underside strip.
            Box(new Vector3(-0.5f, -0.18f, -0.5f), new Vector3(0.5f, 0.02f, 0.5f), iron);
            Box(new Vector3(-0.52f, 0.02f, -0.52f), new Vector3(0.52f, 0.06f, 0.52f), edge);
            Box(new Vector3(-0.48f, -0.2f, -0.46f), new Vector3(0.48f, -0.18f, 0.46f), glow);
            for (int i = 0; i < 6; i++)
                Spike(-0.42f + i * 0.168f, -0.2f, -0.5f, 0.06f, iron);
            // Two chains rising to the top of the box.
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 9; k++)
                {
                    float y = 0.06f + k * 0.05f;
                    float w = k % 2 == 0 ? 0.035f : 0.012f;
                    Box(new Vector3(s * 0.32f - w, y, -w * 3f), new Vector3(s * 0.32f + w, y + 0.045f, w * 3f), edge);
                }
            Mesh mesh = new Mesh { name = "Obstacle_SpikedBeam" };
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string meshPath = ModelFolder + "Obstacle_SpikedBeam_Mesh.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            string matPath = ModelFolder + "Obstacle_SpikedBeam.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Deadline4Sec/VertexColorLit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            HazardGlow(mat);
            GameObject go = new GameObject("Obstacle_SpikedBeam");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            PrefabUtility.SaveAsPrefabAsset(go, ModelFolder + "Obstacle_SpikedBeam.prefab");
            Object.DestroyImmediate(go);
        }
    }
}
