using UnityEditor;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Makes pickups read at a glance: glowing soul coins, power-up orbs that
    // show their icon, and glowing gift boxes. Trigger colliders are untouched.
    public static class PickupVisualSetup
    {
        private const string PickupFolder = "Assets/Prefab/Pickup/";
        private const string IconFolder = "Assets/Art/2D/Generated/";

        private static readonly (string prefab, string icon, Color glow)[] PowerUps =
        {
            ("FreezeClock_Pickup", "powerup_freeze_clock", new Color(0.3f, 0.8f, 1f)),
            ("SoulAmplifier_Pickup", "powerup_soul_amplifier", new Color(1f, 0.3f, 0.6f)),
            ("ReaperRush_Pickup", "powerup_reaper_rush", new Color(1f, 0.25f, 0.25f)),
            ("SoulMagnet_Pickup", "powerup_soul_magnet", new Color(0.7f, 0.35f, 1f)),
            ("ComboSeal_Pickup", "powerup_combo_seal", new Color(1f, 0.75f, 0.2f)),
            ("TimeHeart_Pickup", "powerup_time_heart", new Color(1f, 0.2f, 0.45f)),
        };

        // In-game sizes (meters). Pickups read small on a phone, so they are
        // larger than their first graybox versions; triggers grow a little too.
        private const float PowerUpIconSize = 1.55f;
        private const float PowerUpOrbSize = 0.95f;
        private const float CoinDiameter = 1f;
        private const float CoinGlowSize = 2.4f;
        private const float GiftChestScale = 1.6f;

        private static void SetTriggerRadius(GameObject root, float radius)
        {
            if (root.TryGetComponent(out SphereCollider trigger))
                trigger.radius = radius;
        }

        [MenuItem("Deadline 4 Sec/Art/Polish Pickup Visuals")]
        public static void Apply()
        {
            Glow("Assets/Art/Materials/PowerUps/Coin.mat", new Color(1f, 0.78f, 0.25f), new Color(1f, 0.5f, 0.08f) * 1.6f);
            Glow("Assets/Art/Materials/WeaponSkins/GiftBoxGold.mat", new Color(1f, 0.8f, 0.3f), new Color(1f, 0.55f, 0.1f) * 0.9f);
            Glow("Assets/Art/Materials/WeaponSkins/GiftBoxPurple.mat", new Color(0.6f, 0.25f, 0.9f), new Color(0.6f, 0.15f, 1f) * 1.1f);

            AddFloat(PickupFolder + "Coin.prefab", null, 0.1f);
            AddCoinGlow();
            UseGiftChest();
            AddFloat(PickupFolder + "GiftBox.prefab", null, 0.15f);
            foreach (var p in PowerUps)
            {
                Material orb = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/PowerUps/" + p.prefab + ".mat");
                if (orb != null)
                    Glow(orb, p.glow * 0.5f, p.glow * 1.3f);
                Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + p.icon + ".png");
                AddFloat(PickupFolder + p.prefab + ".prefab", icon, 0.15f);
            }
            AssetDatabase.SaveAssets();
        }

        // A soft additive halo that always faces the camera makes coins pop
        // against the dark track. FxAdditive draws its falloff from UVs, so any
        // full-rect sprite works as the quad.
        private static void AddCoinGlow()
        {
            const string glowPath = "Assets/Art/Materials/PowerUps/CoinGlow.mat";
            Material glow = AssetDatabase.LoadAssetAtPath<Material>(glowPath);
            if (glow == null)
            {
                glow = new Material(Shader.Find("Deadline4Sec/FxAdditive"));
                AssetDatabase.CreateAsset(glow, glowPath);
            }
            glow.SetFloat("_Softness", 2.6f);
            glow.SetFloat("_Core", 0.3f);
            Sprite quad = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "icon_coin.png");
            string prefabPath = PickupFolder + "Coin.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform pivot = root.transform.Find("FloatPivot") ?? root.transform;
                Transform existing = pivot.Find("Glow");
                SpriteRenderer sr = existing != null ? existing.GetComponent<SpriteRenderer>() : null;
                if (sr == null)
                {
                    GameObject go = new GameObject("Glow", typeof(SpriteRenderer));
                    go.transform.SetParent(pivot, false);
                    sr = go.GetComponent<SpriteRenderer>();
                }
                sr.sprite = quad;
                sr.sharedMaterial = glow;
                sr.color = new Color(1f, 0.62f, 0.15f, 0.55f);
                float size = quad != null ? Mathf.Max(quad.bounds.size.x, quad.bounds.size.y) : 1f;
                sr.transform.localScale = Vector3.one * (CoinGlowSize / Mathf.Max(0.01f, size));
                Transform coinVisual = pivot.Find("Visual");
                if (coinVisual != null)
                    coinVisual.localScale = new Vector3(CoinDiameter, 0.11f, CoinDiameter);
                SetTriggerRadius(root, 0.7f);
                PickupFloat floater = root.GetComponent<PickupFloat>();
                if (floater != null)
                {
                    SerializedObject so = new SerializedObject(floater);
                    so.FindProperty("billboard").objectReferenceValue = sr.transform;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Gothic treasure chest built from boxes with vertex colors: purple wood,
        // silver bands, gold corners, a glowing pink lock and lid seam. It replaces
        // the gift box cubes; the trigger collider and GiftBoxPickup are untouched.
        private static void UseGiftChest()
        {
            const string folder = "Assets/Art/3DModel/Props/";
            System.IO.Directory.CreateDirectory(folder);
            var v = new System.Collections.Generic.List<Vector3>();
            var c = new System.Collections.Generic.List<Color>();
            var t = new System.Collections.Generic.List<int>();
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
                    int i0 = v.Count;
                    foreach (int i in f) { v.Add(p[i]); c.Add(col); }
                    t.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
                }
            }
            Color wood = new Color(0.2f, 0.08f, 0.26f), lid = new Color(0.3f, 0.12f, 0.38f);
            Color silver = new Color(0.82f, 0.8f, 0.9f), gold = new Color(1f, 0.72f, 0.25f);
            Color glow = new Color(1f, 0.2f, 0.6f) * 2.2f;
            Box(new Vector3(-0.45f, -0.3f, -0.3f), new Vector3(0.45f, 0.13f, 0.3f), wood);
            Box(new Vector3(-0.44f, 0.13f, -0.29f), new Vector3(0.44f, 0.16f, 0.29f), glow);
            Box(new Vector3(-0.47f, 0.16f, -0.32f), new Vector3(0.47f, 0.32f, 0.32f), lid);
            Box(new Vector3(-0.38f, 0.32f, -0.22f), new Vector3(0.38f, 0.39f, 0.22f), lid);
            foreach (float x in new[] { -0.28f, 0.28f })
                Box(new Vector3(x - 0.045f, -0.31f, -0.33f), new Vector3(x + 0.045f, 0.4f, 0.33f), silver);
            foreach (float x in new[] { -0.47f, 0.47f })
                foreach (float z in new[] { -0.31f, 0.31f })
                {
                    Box(new Vector3(x - 0.05f, -0.32f, z - 0.05f), new Vector3(x + 0.05f, -0.2f, z + 0.05f), gold);
                    Box(new Vector3(x - 0.05f, 0.24f, z - 0.05f), new Vector3(x + 0.05f, 0.34f, z + 0.05f), gold);
                }
            Box(new Vector3(-0.09f, -0.02f, -0.37f), new Vector3(0.09f, 0.22f, -0.3f), gold);
            Box(new Vector3(-0.055f, 0.02f, -0.39f), new Vector3(0.055f, 0.18f, -0.36f), glow);

            Mesh mesh = new Mesh { name = "Prop_GiftChest" };
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string meshPath = folder + "Prop_GiftChest_Mesh.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            string matPath = folder + "Prop_GiftChest.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Deadline4Sec/VertexColorLit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetColor("_RimColor", new Color(1f, 0.6f, 0.2f));
            mat.SetFloat("_RimPower", 2.5f);
            EditorUtility.SetDirty(mat);

            string prefabPath = PickupFolder + "GiftBox.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform pivot = root.transform.Find("FloatPivot") ?? root.transform;
                foreach (Renderer r in pivot.GetComponentsInChildren<Renderer>(true))
                    if (r.name == "BoxVisual" || r.name == "RibbonVisual")
                        r.enabled = false;
                Transform existing = pivot.Find("ChestModel");
                GameObject model = existing != null ? existing.gameObject : new GameObject("ChestModel");
                model.transform.SetParent(pivot, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * GiftChestScale;
                SetTriggerRadius(root, 0.95f);
                ((model.TryGetComponent(out MeshFilter existingMeshFilter) ? existingMeshFilter : model.AddComponent<MeshFilter>())).sharedMesh = mesh;
                ((model.TryGetComponent(out MeshRenderer existingMeshRenderer) ? existingMeshRenderer : model.AddComponent<MeshRenderer>())).sharedMaterial = mat;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Glow(string path, Color baseColor, Color emission) =>
            Glow(AssetDatabase.LoadAssetAtPath<Material>(path), baseColor, emission);

        private static void Glow(Material m, Color baseColor, Color emission)
        {
            if (m == null)
                return;
            m.SetColor("_BaseColor", baseColor);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            if (m.HasProperty("_Smoothness"))
                m.SetFloat("_Smoothness", 0.75f);
            EditorUtility.SetDirty(m);
        }

        private static void AddFloat(string prefabPath, Sprite icon, float bob)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                // Group visuals under one bob pivot so multi-part pickups move together.
                Transform pivot = root.transform.Find("FloatPivot");
                if (pivot == null)
                {
                    pivot = new GameObject("FloatPivot").transform;
                    pivot.SetParent(root.transform, false);
                    for (int i = root.transform.childCount - 1; i >= 0; i--)
                    {
                        Transform child = root.transform.GetChild(i);
                        if (child != pivot && child.GetComponent<Collider>() == null)
                            child.SetParent(pivot, true);
                    }
                }
                // Re-runs must not compound scales: the pivot stays at 1 and each
                // visual gets an absolute size.
                pivot.localScale = Vector3.one;
                Transform visual = pivot.Find("Visual") ?? pivot.Find("BoxVisual");
                Transform billboard = null;
                if (icon != null)
                {
                    Transform existing = pivot.Find("Icon");
                    SpriteRenderer sr = existing != null ? existing.GetComponent<SpriteRenderer>() : null;
                    if (sr == null)
                    {
                        GameObject go = new GameObject("Icon", typeof(SpriteRenderer));
                        go.transform.SetParent(pivot, false);
                        sr = go.GetComponent<SpriteRenderer>();
                    }
                    sr.sprite = icon;
                    sr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                    float size = Mathf.Max(icon.bounds.size.x, icon.bounds.size.y);
                    sr.transform.localScale = Vector3.one * (PowerUpIconSize / Mathf.Max(0.01f, size));
                    sr.transform.localPosition = Vector3.zero;
                    sr.sortingOrder = 5;
                    billboard = sr.transform;
                    // The orb becomes a glowing shell around the icon.
                    if (visual != null)
                        visual.localScale = Vector3.one * PowerUpOrbSize;
                    SetTriggerRadius(root, 0.8f);
                }
                PickupFloat floater = (root.TryGetComponent(out PickupFloat existingPickupFloat) ? existingPickupFloat : root.AddComponent<PickupFloat>());
                SerializedObject so = new SerializedObject(floater);
                so.FindProperty("bobTarget").objectReferenceValue = pivot;
                so.FindProperty("billboard").objectReferenceValue = billboard;
                so.FindProperty("bobHeight").floatValue = bob;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
