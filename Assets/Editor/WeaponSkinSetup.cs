using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Weapon skins (2026-10-30): the six models in Art/3DModel/Weapon become the
    // six catalog skins.
    //  - Materials: Art/Materials/WeaponSkins/Weapon_<NAME>.mat (URP Unlit with
    //    the model's base-colour texture; the models' own materials use a
    //    Built-in particle shader that URP cannot draw).
    //  - Prefabs: Prefab/WeaponSkins/WeaponSkin_<NAME>.prefab. The root is the
    //    grip (on the shaft, a third of the way up a scythe / on a sword's
    //    handle) in the same axes as the built-in scythe, so it takes that
    //    scythe's place in the hand unchanged. WeaponSkinFx sits on the root.
    //  - Icons: Art/2D/WeaponSkins/icon_weapon_<NAME>.png (rendered from the model).
    //  - Catalog: names (renamed 2026-10-30), prefab, accent colour, icon. Skin
    //    ids stay, so owned / equipped saves are unchanged. The default skin
    //    (NEON REAPER) keeps the built-in scythe (same model) without effects.
    public static class WeaponSkinSetup
    {
        private const string ModelFolder = "Assets/Art/3DModel/Weapon/";
        private const string TextureFolder = ModelFolder + "weapon_mat/";
        private const string MaterialFolder = "Assets/Art/Materials/WeaponSkins/";
        private const string PrefabFolder = "Assets/Prefab/WeaponSkins/";
        private const string IconFolder = "Assets/Art/2D/WeaponSkins/";
        private const string CatalogPath = "Assets/Resources/GameExtras/GameExtrasCatalog.asset";
        private const string DefaultTextureMaterial = "Assets/Art/3DModel/Material/tripo_mat_29219f7e.mat";

        private struct SkinArt
        {
            public string Model, Texture, DisplayName;
            public float GripHeight; // fraction of the height above the bottom
            public WeaponSkinFx.Effect Effect;
            public Color Accent, Secondary;
        }

        // Catalog order (ids DefaultScythe, NeonReaper, FrostMoon, BloodMoon, CyberDeath, Eclipse).
        private static readonly SkinArt[] Art =
        {
            new SkinArt { Model = "NEONREAPER", Texture = null, DisplayName = "NEON REAPER", GripHeight = 0.335f,
                Effect = WeaponSkinFx.Effect.None, Accent = new Color(1f, 0.22f, 0.55f), Secondary = Color.white },
            new SkinArt { Model = "STARLIGHT", Texture = "STAR_LIGHT", DisplayName = "STARLIGHT", GripHeight = 0.335f,
                Effect = WeaponSkinFx.Effect.Starlight, Accent = new Color(1f, 0.82f, 0.3f), Secondary = new Color(1f, 0.97f, 0.8f) },
            new SkinArt { Model = "FROSTBITE", Texture = "FROST_BITE", DisplayName = "FROST BITE", GripHeight = 0.335f,
                Effect = WeaponSkinFx.Effect.FrostBite, Accent = new Color(0.45f, 0.85f, 1f), Secondary = new Color(0.85f, 0.97f, 1f) },
            new SkinArt { Model = "BLOODMOON", Texture = "BLOOD_MOON", DisplayName = "BLOOD MOON", GripHeight = 0.13f,
                Effect = WeaponSkinFx.Effect.BloodMoon, Accent = new Color(1f, 0.1f, 0.18f), Secondary = new Color(1f, 0.42f, 0.2f) },
            new SkinArt { Model = "CYBERDEATH", Texture = "CYBER_DEATH", DisplayName = "CYBER DEATH", GripHeight = 0.335f,
                Effect = WeaponSkinFx.Effect.CyberDeath, Accent = new Color(0.3f, 1f, 0.35f), Secondary = new Color(0.55f, 1f, 0.9f) },
            new SkinArt { Model = "ECLIPSE", Texture = "ECLIPSE", DisplayName = "ECLIPSE", GripHeight = 0.335f,
                Effect = WeaponSkinFx.Effect.Eclipse, Accent = new Color(0.66f, 0.3f, 1f), Secondary = new Color(1f, 0.8f, 0.35f) },
        };

        [MenuItem("Deadline 4 Sec/Skins/Apply Weapon Skins")]
        public static void Apply()
        {
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(IconFolder);
            GameExtrasCatalog catalog = AssetDatabase.LoadAssetAtPath<GameExtrasCatalog>(CatalogPath);
            if (catalog == null || catalog.Skins == null || catalog.Skins.Length < Art.Length)
                throw new System.InvalidOperationException("GameExtrasCatalog with six skins is required.");

            var icons = new Sprite[Art.Length];
            var prefabs = new GameObject[Art.Length];
            for (int i = 0; i < Art.Length; i++)
            {
                SkinArt art = Art[i];
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + "weapon_" + art.Model + ".fbx");
                if (model == null)
                    throw new System.InvalidOperationException("Missing weapon model " + art.Model);
                Material material = WeaponMaterial(art);
                icons[i] = RenderIcon(art, model, material);
                // The default skin keeps the character's built-in scythe (the same model).
                prefabs[i] = i == 0 ? null : BuildPrefab(art, model, material);
            }

            SerializedObject data = new SerializedObject(catalog);
            SerializedProperty skins = data.FindProperty("skins");
            for (int i = 0; i < Art.Length; i++)
            {
                SerializedProperty skin = skins.GetArrayElementAtIndex(i);
                skin.FindPropertyRelative("displayName").stringValue = Art[i].DisplayName;
                skin.FindPropertyRelative("weaponPrefab").objectReferenceValue = prefabs[i];
                skin.FindPropertyRelative("accentColor").colorValue = Art[i].Accent;
                skin.FindPropertyRelative("icon").objectReferenceValue = icons[i];
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Weapon skins applied: " + Art.Length);
        }

        private static Material WeaponMaterial(SkinArt art)
        {
            string path = MaterialFolder + "Weapon_" + art.Model + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            Texture texture = art.Texture != null
                ? AssetDatabase.LoadAssetAtPath<Texture>(TextureFolder + art.Texture + "_basecolor.JPEG")
                : AssetDatabase.LoadAssetAtPath<Material>(DefaultTextureMaterial).GetTexture("_BaseMap");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Model space = the model's own node rotation / scale applied to the
        // mesh (all six stand blade-up, ~1 m tall, like the built-in scythe).
        private static Matrix4x4 NodeMatrix(GameObject model) =>
            Matrix4x4.TRS(Vector3.zero, model.transform.localRotation, model.transform.localScale);

        // Grip: the shaft's x (median of the lower shaft's vertices) at
        // GripHeight above the bottom. The built-in scythe is held there.
        private static Vector3 Grip(Mesh mesh, Matrix4x4 node, float gripHeight, out Bounds bounds)
        {
            Vector3[] vertices = mesh.vertices;
            bounds = new Bounds(node.MultiplyPoint3x4(vertices[0]), Vector3.zero);
            var points = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                points[i] = node.MultiplyPoint3x4(vertices[i]);
                bounds.Encapsulate(points[i]);
            }
            float height = bounds.size.y;
            var shaft = new List<float>();
            foreach (Vector3 p in points)
                if (p.y > bounds.min.y + 0.12f * height && p.y < bounds.min.y + 0.45f * height)
                    shaft.Add(p.x);
            shaft.Sort();
            float x = shaft.Count > 0 ? shaft[shaft.Count / 2] : bounds.center.x;
            return new Vector3(x, bounds.min.y + gripHeight * height, bounds.center.z);
        }

        private static GameObject BuildPrefab(SkinArt art, GameObject model, Material material)
        {
            MeshFilter source = model.GetComponentInChildren<MeshFilter>();
            Matrix4x4 node = NodeMatrix(model);
            Vector3 grip = Grip(source.sharedMesh, node, art.GripHeight, out Bounds bounds);

            GameObject root = new GameObject("WeaponSkin_" + art.Model);
            GameObject visual = new GameObject("Model");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = -grip;
            visual.transform.localRotation = model.transform.localRotation;
            visual.transform.localScale = model.transform.localScale;
            visual.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Effects around the blade: the part above the grip, in root space.
            Bounds blade = bounds;
            blade.center -= grip;
            float bladeBottom = blade.min.y + blade.size.y * (art.GripHeight < 0.2f ? 0.28f : 0.45f);
            blade.SetMinMax(new Vector3(blade.min.x, bladeBottom, blade.min.z - 0.05f), new Vector3(blade.max.x, blade.max.y, blade.max.z + 0.05f));
            WeaponSkinFx fx = root.AddComponent<WeaponSkinFx>();
            fx.Configure(art.Effect, art.Accent, art.Secondary, blade);

            string path = PrefabFolder + root.name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Shop icon: the model alone on a transparent background, tilted like a
        // held weapon.
        private static Sprite RenderIcon(SkinArt art, GameObject model, Material material)
        {
            const int Size = 256;
            const int Layer = 31;
            GameObject stage = new GameObject("Weapon Icon Stage") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                Vector3 origin = new Vector3(5000f, 5000f, 5000f);
                GameObject weapon = new GameObject("Weapon") { hideFlags = HideFlags.HideAndDontSave, layer = Layer };
                weapon.transform.SetParent(stage.transform, false);
                weapon.AddComponent<MeshFilter>().sharedMesh = model.GetComponentInChildren<MeshFilter>().sharedMesh;
                Renderer renderer = weapon.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                weapon.transform.localRotation = Quaternion.Euler(0f, 0f, -18f) * model.transform.localRotation;
                weapon.transform.localScale = model.transform.localScale;
                weapon.transform.position = origin;
                weapon.transform.position += origin - renderer.bounds.center;

                Camera camera = new GameObject("Icon Camera").AddComponent<Camera>();
                camera.gameObject.hideFlags = HideFlags.HideAndDontSave;
                camera.transform.SetParent(stage.transform, false);
                camera.orthographic = true;
                Bounds b = renderer.bounds;
                camera.orthographicSize = Mathf.Max(b.extents.x, b.extents.y) * 1.08f;
                camera.transform.position = origin + Vector3.back * 5f;
                camera.transform.rotation = Quaternion.identity;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.cullingMask = 1 << Layer;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 20f;
                camera.allowHDR = false;
                camera.allowMSAA = false;

                RenderTexture target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                Texture2D image = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                image.Apply();
                RenderTexture.active = null;
                camera.targetTexture = null;
                target.Release();

                string path = IconFolder + "icon_weapon_" + art.Model + ".png";
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            finally
            {
                Object.DestroyImmediate(stage);
            }
        }
    }
}
