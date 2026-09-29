using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Deadline4Sec.Editor
{
    public static class GameExtrasSetup
    {
        private const string CatalogPath = "Assets/Resources/GameExtras/GameExtrasCatalog.asset";
        private const string GiftPrefabPath = "Assets/Prefab/Pickup/GiftBox.prefab";

        [MenuItem("Deadline 4 Sec/Prepare Skins Gift Boxes And Revive UI")]
        public static void PrepareAssetsAndScenes()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the open scene before preparing game extras.");
            GameExtrasCatalog catalog = PrepareCatalog();
            GameObject giftPrefab = PrepareGiftPrefab();
            PlaceGiftBox("Pattern_A_LaneAttack", giftPrefab, new Vector3(-2.5f, 1f, 20f));
            PlaceGiftBox("Pattern_E_MixedRisk", giftPrefab, new Vector3(0f, 1f, 14f));
            string originalScene = EditorSceneManager.GetActiveScene().path;
            foreach (string path in new[] { "Assets/Scenes/EndlessRun.unity",
                "Assets/Scenes/GrayboxCourse.unity", "Assets/Scenes/TestScene.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                GameFlowManager flow = UnityEngine.Object.FindFirstObjectByType<GameFlowManager>();
                Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
                if (flow == null || canvas == null)
                    throw new InvalidOperationException("Missing game flow or canvas in " + path);
                PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                WeaponSkinVisual weapon = EnsureWeapon(player, catalog);
                RunInventory inventory = flow.GetComponent<RunInventory>();
                if (inventory == null)
                    inventory = flow.gameObject.AddComponent<RunInventory>();
                SerializedObject invData = new SerializedObject(inventory);
                invData.FindProperty("catalog").objectReferenceValue = catalog;
                invData.FindProperty("wallet").objectReferenceValue = flow.GetComponent<CoinWallet>();
                invData.FindProperty("weaponVisual").objectReferenceValue = weapon;
                invData.ApplyModifiedPropertiesWithoutUndo();
                GameExtrasUI ui = flow.GetComponent<GameExtrasUI>();
                if (ui == null)
                    ui = flow.gameObject.AddComponent<GameExtrasUI>();
                Transform upgrades = canvas.transform.Find("UpgradePanel");
                if (upgrades == null)
                    throw new InvalidOperationException("Run Update Title And Tutorial Menus first: " + path);
                Transform content = upgrades.Find("ItemScrollView/Viewport/Content");
                EnsureShopRows(content, catalog);
                Transform result = canvas.transform.Find("ResultPanel");
                if (result == null)
                {
                    SerializedObject flowData = new SerializedObject(flow);
                    result = ((GameObject)flowData.FindProperty("resultPanel").objectReferenceValue).transform;
                }
                EnsureResultConfirm(result, flow);
                Transform revive = canvas.transform.Find("RevivePanel");
                if (revive == null)
                    revive = CreateRevivePanel(canvas.transform, flow);
                Transform gift = canvas.transform.Find("GiftOpeningPanel");
                if (gift == null)
                    gift = CreateGiftPanel(canvas.transform, flow);
                EnsureGiftTapArea(gift);
                SerializedObject flowRefs = new SerializedObject(flow);
                Transform gameplay = ((GameObject)flowRefs.FindProperty("gameplayUI").objectReferenceValue).transform;
                Transform giftHud = gameplay.Find("GiftBoxCountText");
                if (giftHud == null)
                    giftHud = CreateHudLabel(gameplay, "GiftBoxCountText", "GIFT BOX x0", -105);
                Transform itemHud = gameplay.Find("ConsumableStatusText");
                if (itemHud == null)
                    itemHud = CreateHudLabel(gameplay, "ConsumableStatusText", "ITEM NONE", -150);
                SerializedObject uiData = new SerializedObject(ui);
                Set(uiData, "revivePanel", revive.gameObject);
                Set(uiData, "reviveCountdownText", revive.Find("CountdownText").GetComponent<TMP_Text>());
                Set(uiData, "couponText", revive.Find("CouponText").GetComponent<TMP_Text>());
                Set(uiData, "couponButton", revive.Find("CouponButton").GetComponent<Button>());
                Set(uiData, "giftPanel", gift.gameObject);
                Set(uiData, "giftProgressText", gift.Find("ProgressText").GetComponent<TMP_Text>());
                Set(uiData, "giftRewardText", gift.Find("RewardText").GetComponent<TMP_Text>());
                Set(uiData, "giftActionText", gift.Find("OpenButton/Label").GetComponent<TMP_Text>());
                Set(uiData, "giftOpenButton", gift.Find("OpenButton").gameObject);
                Set(uiData, "giftConfirmButton", gift.Find("ConfirmButton").gameObject);
                Set(uiData, "giftHudText", giftHud.GetComponent<TMP_Text>());
                Set(uiData, "consumableHudText", itemHud.GetComponent<TMP_Text>());
                SkinShopRow[] skinRows = content.GetComponentsInChildren<SkinShopRow>(true);
                SetArray(uiData.FindProperty("skinRows"), skinRows);
                ConsumableShopRow[] consumableRows = content.GetComponentsInChildren<ConsumableShopRow>(true);
                SetArray(uiData.FindProperty("consumableRows"), consumableRows);
                uiData.ApplyModifiedPropertiesWithoutUndo();
                revive.gameObject.SetActive(false);
                gift.gameObject.SetActive(false);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("Saved game extras: " + path);
            }
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(originalScene))
                EditorSceneManager.OpenScene(originalScene);
        }

        private static GameExtrasCatalog PrepareCatalog()
        {
            EnsureFolder("Assets/Resources/GameExtras");
            EnsureFolder("Assets/Art/Materials/WeaponSkins");
            GameExtrasCatalog catalog = AssetDatabase.LoadAssetAtPath<GameExtrasCatalog>(CatalogPath);
            if (catalog != null)
                return catalog;
            catalog = ScriptableObject.CreateInstance<GameExtrasCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            SerializedObject data = new SerializedObject(catalog);
            string[] ids = { "DefaultScythe", "NeonReaper", "FrostMoon", "BloodMoon", "CyberDeath", "Eclipse" };
            string[] names = { "DEFAULT SCYTHE", "NEON REAPER", "FROST MOON", "BLOOD MOON", "CYBER DEATH", "ECLIPSE" };
            Color[] colors = { new Color(0.78f, 0.8f, 0.88f), new Color(0.2f, 1f, 0.85f),
                new Color(0.45f, 0.8f, 1f), new Color(0.9f, 0.12f, 0.24f),
                new Color(0.75f, 0.28f, 1f), new Color(0.12f, 0.13f, 0.26f) };
            int[] coins = { 0, 8000, 0, 0, 0, 15000 };
            SkinCurrency[] currencies = { SkinCurrency.None, SkinCurrency.None, SkinCurrency.FrostShard,
                SkinCurrency.BloodShard, SkinCurrency.CyberCore, SkinCurrency.EclipseFragment };
            int[] currencyCosts = { 0, 0, 30, 30, 40, 25 };
            SerializedProperty skins = data.FindProperty("skins");
            skins.arraySize = ids.Length;
            for (int i = 0; i < ids.Length; i++)
            {
                SerializedProperty skin = skins.GetArrayElementAtIndex(i);
                skin.FindPropertyRelative("id").stringValue = ids[i];
                skin.FindPropertyRelative("displayName").stringValue = names[i];
                skin.FindPropertyRelative("coinCost").intValue = coins[i];
                skin.FindPropertyRelative("currency").enumValueIndex = (int)currencies[i];
                skin.FindPropertyRelative("currencyCost").intValue = currencyCosts[i];
                skin.FindPropertyRelative("weaponMaterial").objectReferenceValue = SkinMaterial(ids[i], colors[i]);
            }
            GiftRewardKind[] kinds = { GiftRewardKind.Coin, GiftRewardKind.Coin,
                GiftRewardKind.SkinCurrency, GiftRewardKind.ReviveCoupon, GiftRewardKind.Shield,
                GiftRewardKind.Dash, GiftRewardKind.Dash };
            int[] amounts = { 300, 800, 3, 1, 1, 1, 2 };
            int[] weights = { 25, 10, 20, 8, 15, 15, 7 };
            SerializedProperty rewards = data.FindProperty("rewards");
            rewards.arraySize = kinds.Length;
            for (int i = 0; i < kinds.Length; i++)
            {
                SerializedProperty reward = rewards.GetArrayElementAtIndex(i);
                reward.FindPropertyRelative("kind").enumValueIndex = (int)kinds[i];
                reward.FindPropertyRelative("amount").intValue = amounts[i];
                reward.FindPropertyRelative("weight").intValue = weights[i];
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        private static Material SkinMaterial(string name, Color color)
        {
            string path = "Assets/Art/Materials/WeaponSkins/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static WeaponSkinVisual EnsureWeapon(PlayerController player, GameExtrasCatalog catalog)
        {
            Transform root = player.transform.Find("WeaponVisual");
            if (root == null)
            {
                root = new GameObject("WeaponVisual").transform;
                root.SetParent(player.transform, false);
                root.localPosition = new Vector3(0.5f, 0.1f, 0.35f);
                GameObject staff = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                staff.name = "Staff";
                staff.transform.SetParent(root, false);
                staff.transform.localPosition = new Vector3(0.3f, 0.15f, 0f);
                staff.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
                staff.transform.localScale = new Vector3(0.07f, 0.7f, 0.07f);
                UnityEngine.Object.DestroyImmediate(staff.GetComponent<Collider>());
                GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blade.name = "Blade";
                blade.transform.SetParent(root, false);
                blade.transform.localPosition = new Vector3(0.52f, 0.83f, 0f);
                blade.transform.localRotation = Quaternion.Euler(0f, 0f, 32f);
                blade.transform.localScale = new Vector3(0.58f, 0.10f, 0.16f);
                UnityEngine.Object.DestroyImmediate(blade.GetComponent<Collider>());
            }
            WeaponSkinVisual visual = root.GetComponent<WeaponSkinVisual>();
            if (visual == null)
                visual = root.gameObject.AddComponent<WeaponSkinVisual>();
            SerializedObject data = new SerializedObject(visual);
            SerializedProperty renderers = data.FindProperty("weaponRenderers");
            renderers.arraySize = 2;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = root.Find("Staff").GetComponent<Renderer>();
            renderers.GetArrayElementAtIndex(1).objectReferenceValue = root.Find("Blade").GetComponent<Renderer>();
            data.ApplyModifiedPropertiesWithoutUndo();
            visual.Apply(catalog.Skins[0].weaponMaterial);
            return visual;
        }

        private static GameObject PrepareGiftPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GiftPrefabPath);
            if (prefab != null)
                return prefab;
            GameObject root = new GameObject("GiftBox");
            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.82f;
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            root.AddComponent<GiftBoxPickup>();
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "BoxVisual";
            box.transform.SetParent(root.transform, false);
            box.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            box.GetComponent<Renderer>().sharedMaterial = SkinMaterial("GiftBoxPurple", new Color(0.55f, 0.2f, 0.85f));
            GameObject ribbon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ribbon.name = "RibbonVisual";
            ribbon.transform.SetParent(root.transform, false);
            ribbon.transform.localScale = new Vector3(0.18f, 0.94f, 0.95f);
            UnityEngine.Object.DestroyImmediate(ribbon.GetComponent<Collider>());
            ribbon.GetComponent<Renderer>().sharedMaterial = SkinMaterial("GiftBoxGold", new Color(1f, 0.75f, 0.14f));
            prefab = PrefabUtility.SaveAsPrefabAsset(root, GiftPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void PlaceGiftBox(string pattern, GameObject prefab, Vector3 position)
        {
            string path = "Assets/Prefab/Pattern/" + pattern + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root.transform.Find("GiftBox_01") == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                instance.name = "GiftBox_01";
                instance.transform.localPosition = position;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void EnsureShopRows(Transform content, GameExtrasCatalog catalog)
        {
            if (content.Find("WeaponSkinHeading") == null)
                Heading(content, "WeaponSkinHeading", "WEAPON SKINS");
            for (int i = 0; i < catalog.Skins.Length; i++)
            {
                if (content.Find(catalog.Skins[i].id + "Row") != null)
                    continue;
                GameObject row = Card(content, catalog.Skins[i].id + "Row", 108);
                SkinShopRow behaviour = row.AddComponent<SkinShopRow>();
                TMP_Text name = Label(row.transform, "NameText", "", new Vector2(-170, 28), new Vector2(350, 36), 25);
                TMP_Text price = Label(row.transform, "PriceText", "", new Vector2(-170, -18), new Vector2(350, 48), 19);
                Button button = MenuButton(row.transform, "ActionButton", "BUY", new Vector2(235, 0), new Vector2(170, 58), behaviour.BuyOrEquip);
                SerializedObject data = new SerializedObject(behaviour);
                data.FindProperty("skinIndex").intValue = i;
                Set(data, "nameText", name);
                Set(data, "priceText", price);
                Set(data, "actionText", button.transform.Find("Label").GetComponent<TMP_Text>());
                Set(data, "actionButton", button);
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            if (content.Find("ConsumableHeading") == null)
                Heading(content, "ConsumableHeading", "CONSUMABLES · DOUBLE TAP TO USE");
            foreach (ConsumableKind kind in new[] { ConsumableKind.Shield, ConsumableKind.Dash })
            {
                if (content.Find(kind + "ConsumableRow") != null)
                    continue;
                GameObject row = Card(content, kind + "ConsumableRow", 86);
                ConsumableShopRow behaviour = row.AddComponent<ConsumableShopRow>();
                TMP_Text count = Label(row.transform, "CountText", kind.ToString(), new Vector2(-165, 0), new Vector2(345, 55), 27);
                Button button = MenuButton(row.transform, "ActionButton", "EQUIP", new Vector2(235, 0), new Vector2(170, 56), behaviour.ToggleEquip);
                SerializedObject data = new SerializedObject(behaviour);
                data.FindProperty("kind").enumValueIndex = (int)kind;
                Set(data, "countText", count);
                Set(data, "actionText", button.transform.Find("Label").GetComponent<TMP_Text>());
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void EnsureResultConfirm(Transform result, GameFlowManager flow)
        {
            if (result.Find("ConfirmButton") == null)
                MenuButton(result, "ConfirmButton", "CONFIRM", new Vector2(200, -350),
                    new Vector2(280, 72), flow.ConfirmResult);
            Transform main = result.Find("MainMenuButton");
            if (main != null)
            {
                RectTransform rect = main.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(-200, -350);
                rect.sizeDelta = new Vector2(280, 72);
            }
        }

        private static Transform CreateRevivePanel(Transform canvas, GameFlowManager flow)
        {
            Transform panel = Overlay(canvas, "RevivePanel");
            Label(panel, "HeadingText", "부활하시겠습니까?", new Vector2(0, 200), new Vector2(620, 75), 43);
            Label(panel, "CountdownText", "4", new Vector2(0, 110), new Vector2(260, 90), 68);
            Label(panel, "CouponText", "REVIVE COUPON x0", new Vector2(0, 30), new Vector2(540, 55), 30);
            MenuButton(panel, "CouponButton", "쿠폰으로 부활", new Vector2(0, -55), new Vector2(360, 64), flow.UseCouponRevive);
            MenuButton(panel, "AdButton", "광고 보고 부활", new Vector2(0, -135), new Vector2(360, 64), flow.ClickRewardedAd);
            MenuButton(panel, "GiveUpButton", "포기", new Vector2(0, -215), new Vector2(260, 60), flow.GiveUpRevive);
            return panel;
        }

        private static Transform CreateGiftPanel(Transform canvas, GameFlowManager flow)
        {
            Transform panel = Overlay(canvas, "GiftOpeningPanel");
            Label(panel, "ProgressText", "GIFT BOX 1 / 1", new Vector2(0, 225), new Vector2(650, 70), 43);
            Image box = ImageObject(panel, "GiftBoxArt", new Vector2(0, 55), new Vector2(220, 170),
                new Color(0.5f, 0.2f, 0.85f));
            box.raycastTarget = false;
            ImageObject(box.transform, "RibbonVertical", Vector2.zero, new Vector2(38, 170),
                new Color(1f, 0.75f, 0.14f)).raycastTarget = false;
            ImageObject(box.transform, "RibbonHorizontal", Vector2.zero, new Vector2(220, 34),
                new Color(1f, 0.75f, 0.14f)).raycastTarget = false;
            Label(panel, "RewardText", "", new Vector2(0, -80), new Vector2(650, 65), 40);
            MenuButton(panel, "OpenButton", "TAP TO OPEN", new Vector2(0, -175),
                new Vector2(420, 76), flow.OpenOrContinueGiftBox);
            MenuButton(panel, "ConfirmButton", "CONFIRM", new Vector2(0, -175),
                new Vector2(320, 76), flow.ConfirmAllGiftBoxes);
            return panel;
        }

        private static void EnsureGiftTapArea(Transform gift)
        {
            Transform open = gift.Find("OpenButton");
            RectTransform rect = open.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            open.GetComponent<Image>().color = Color.clear;
            TMP_Text label = open.Find("Label").GetComponent<TMP_Text>();
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchoredPosition = new Vector2(0, -175);
            label.rectTransform.sizeDelta = new Vector2(420, 76);
            label.raycastTarget = false;
        }

        private static Transform CreateHudLabel(Transform parent, string name, string value, float y)
        {
            TMP_Text text = Label(parent, name, value, Vector2.zero, new Vector2(290, 46), 25);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(20, y);
            text.alignment = TextAlignmentOptions.Left;
            return rect;
        }

        private static void Heading(Transform parent, string name, string value)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<LayoutElement>().preferredHeight = 62;
            TMP_Text text = obj.GetComponent<TMP_Text>();
            GameFont.Apply(text);
            text.text = value;
            text.color = Color.white;
            text.fontSize = 31;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }

        private static GameObject Card(Transform parent, string name, float height)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<LayoutElement>().preferredHeight = height;
            Image image = obj.GetComponent<Image>();
            image.color = new Color(0.12f, 0.14f, 0.20f);
            image.raycastTarget = false;
            return obj;
        }

        private static Transform Overlay(Transform parent, string name)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            obj.GetComponent<Image>().color = new Color(0.025f, 0.03f, 0.055f, 0.97f);
            return rect;
        }

        private static Image ImageObject(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = obj.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text Label(Transform parent, string name, string value,
            Vector2 position, Vector2 size, float fontSize)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TMP_Text text = obj.GetComponent<TMP_Text>();
            GameFont.Apply(text);
            text.text = value;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 18;
            text.fontSizeMax = fontSize;
            text.raycastTarget = false;
            return text;
        }

        private static Button MenuButton(Transform parent, string name, string value,
            Vector2 position, Vector2 size, UnityAction action)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = obj.GetComponent<Image>();
            image.color = new Color(0.72f, 0.09f, 0.24f);
            Button button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            UnityEventTools.AddPersistentListener(button.onClick, action);
            Label(obj.transform, "Label", value, Vector2.zero, size - new Vector2(12, 8), 31);
            return button;
        }

        private static void Set(SerializedObject obj, string property, UnityEngine.Object value) =>
            obj.FindProperty(property).objectReferenceValue = value;

        private static void SetArray<T>(SerializedProperty property, T[] values) where T : UnityEngine.Object
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
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
