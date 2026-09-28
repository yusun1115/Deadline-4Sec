using System.IO;
using UnityEditor;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    public static class PowerUpContentSetup
    {
        private const string BalancePath = "Assets/Resources/PowerUps/PowerUpBalance.asset";
        private const string PickupFolder = "Assets/Prefab/Pickup";
        private const string MaterialFolder = "Assets/Art/Materials/PowerUps";

        [MenuItem("Deadline 4 Sec/Prepare Coin And Power-Up Assets")]
        public static void PrepareAssets()
        {
            EnsureFolder("Assets/Resources/PowerUps");
            EnsureFolder(PickupFolder);
            EnsureFolder(MaterialFolder);
            if (AssetDatabase.LoadAssetAtPath<PowerUpBalance>(BalancePath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<PowerUpBalance>(), BalancePath);

            GameObject coin = CreatePickupPrefab("Coin", new Color(1f, 0.75f, 0.1f),
                true, PowerUpType.FreezeClock);
            GameObject[] items = new GameObject[6];
            Color[] colors = {
                new Color(0.3f, 0.8f, 1f), new Color(0.85f, 0.3f, 1f),
                new Color(1f, 0.2f, 0.25f), new Color(0.4f, 1f, 0.55f),
                new Color(1f, 0.55f, 0.2f), new Color(1f, 0.35f, 0.65f)
            };
            for (int i = 0; i < items.Length; i++)
                items[i] = CreatePickupPrefab(((PowerUpType)i).ToString() + "_Pickup",
                    colors[i], false, (PowerUpType)i);

            PlaceInPattern("Pattern_A_LaneAttack", coin,
                ("Coin_01", new Vector3(0, 1, 2)),
                ("Coin_02", new Vector3(-2.5f, 1, 7)),
                ("Coin_03", new Vector3(2.5f, 1, 12)),
                ("Coin_04", new Vector3(0, 1, 17)));
            PlaceInPattern("Pattern_A_LaneAttack", items[0],
                ("FreezeClock_Pickup", new Vector3(0, 1, 3)));
            PlaceInPattern("Pattern_A_LaneAttack", items[1],
                ("SoulAmplifier_Pickup", new Vector3(-2.5f, 1, 13)));

            PlaceInPattern("Pattern_B_JumpSlide", coin,
                ("Coin_01", new Vector3(0, 1.9f, 4)),
                ("Coin_02", new Vector3(0, 1.9f, 5)),
                ("Coin_03", new Vector3(0, 0.7f, 9)),
                ("Coin_04", new Vector3(0, 0.7f, 11)));
            PlaceInPattern("Pattern_B_JumpSlide", items[2],
                ("ReaperRush_Pickup", new Vector3(-2.5f, 1, 7)));

            PlaceInPattern("Pattern_C_AirCombo", coin,
                ("Coin_01", new Vector3(0, 3.4f, 4.5f)),
                ("Coin_02", new Vector3(2.5f, 3.7f, 8.5f)),
                ("Coin_03", new Vector3(0, 4.4f, 12.5f)));
            PlaceInPattern("Pattern_C_AirCombo", items[3],
                ("SoulMagnet_Pickup", new Vector3(-2.5f, 1.2f, 6)));

            PlaceInPattern("Pattern_D_Stomp", coin,
                ("Coin_01", new Vector3(0, 1.1f, 4)),
                ("Coin_02", new Vector3(2.5f, 1.1f, 12)));
            PlaceInPattern("Pattern_D_Stomp", items[4],
                ("ComboSeal_Pickup", new Vector3(0, 1, 5)));

            PlaceInPattern("Pattern_E_MixedRisk", coin,
                ("Coin_01", new Vector3(-2.5f, 1, 6)),
                ("Coin_02", new Vector3(2.5f, 1, 11)),
                ("Coin_03", new Vector3(-2.5f, 1, 24)));
            PlaceInPattern("Pattern_E_MixedRisk", items[5],
                ("TimeHeart_Pickup", new Vector3(-2.5f, 1, 7)));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Coin, six Power-Up prefabs, balance asset, and pattern placements are ready.");
        }

        private static GameObject CreatePickupPrefab(string name, Color color,
            bool coin, PowerUpType type)
        {
            string path = PickupFolder + "/" + name + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                if (existing.GetComponent<Rigidbody>() == null)
                {
                    GameObject loaded = PrefabUtility.LoadPrefabContents(path);
                    Rigidbody existingBody = loaded.AddComponent<Rigidbody>();
                    existingBody.isKinematic = true;
                    existingBody.useGravity = false;
                    PrefabUtility.SaveAsPrefabAsset(loaded, path);
                    PrefabUtility.UnloadPrefabContents(loaded);
                }
                return existing;
            }
            GameObject root = new GameObject(name);
            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = coin ? 0.55f : 0.65f;
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            if (coin)
                root.AddComponent<CoinPickup>();
            else
            {
                PowerUpPickup pickup = root.AddComponent<PowerUpPickup>();
                SerializedObject data = new SerializedObject(pickup);
                data.FindProperty("itemType").enumValueIndex = (int)type;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            GameObject visual = GameObject.CreatePrimitive(coin ? PrimitiveType.Cylinder : PrimitiveType.Sphere);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = coin ? new Vector3(0.65f, 0.08f, 0.65f) :
                new Vector3(0.85f, 0.85f, 0.85f);
            if (coin)
                visual.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = GetMaterial(name, color);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material GetMaterial(string name, Color color)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void PlaceInPattern(string patternName, GameObject prefab,
            params (string name, Vector3 position)[] placements)
        {
            string path = "Assets/Prefab/Pattern/" + patternName + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            foreach (var placement in placements)
            {
                if (root.transform.Find(placement.name) != null)
                    continue;
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                instance.name = placement.name;
                instance.transform.localPosition = placement.position;
                changed = true;
            }
            if (changed)
                PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
