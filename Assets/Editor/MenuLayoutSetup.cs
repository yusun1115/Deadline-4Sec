using System;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Deadline4Sec.Editor
{
    // Title / Upgrade / Skin screen layout (2026-10-01 request):
    //  - Title: logo and frames untouched; settings gear top-left; SKIN and
    //    UPGRADE round buttons at the bottom; no coin counter.
    //  - Upgrade: items and consumables only; back arrow top-left, coins top-right.
    //  - Skin (new): character close-up on the left, scythe skin cards in a
    //    right-hand column; back arrow top-left, coins top-right.
    // Existing objects keep their names and On Click wiring; skin rows are moved,
    // not recreated, so their SkinShopRow references stay valid.
    public static class MenuLayoutSetup
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/EndlessRun.unity",
            "Assets/Scenes/GrayboxCourse.unity",
            "Assets/Scenes/TestScene.unity",
        };

        [MenuItem("Deadline 4 Sec/UI/Apply Title, Upgrade And Skin Layout")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the open scene before changing the menu layout.");
            UiIconFactory.DrawAll();
            foreach (string icon in new[] { "icon_coin_gold", "icon_dash", "icon_shield" })
                ImportSprite(GeneratedFolder + icon + ".png");
            string original = EditorSceneManager.GetActiveScene().path;
            foreach (string path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path);
                Transform canvas = GameObject.Find("Canvas")?.transform;
                GameFlowManager flow = UnityEngine.Object.FindFirstObjectByType<GameFlowManager>();
                if (canvas == null || flow == null)
                    continue;
                LayoutTitle(canvas, flow);
                Transform upgrade = canvas.Find("UpgradePanel");
                LayoutUpgrade(upgrade);
                LayoutUpgradeScreen(upgrade);
                BuildSkinPanel(canvas, upgrade, flow);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (!string.IsNullOrEmpty(original))
                EditorSceneManager.OpenScene(original);
        }

        // Painted (AI) icons win over the drawn fallbacks when they exist.
        private static Sprite Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(UiIconFactory.Folder + "AI/ai_" + name + ".png") ??
            AssetDatabase.LoadAssetAtPath<Sprite>(UiIconFactory.Folder + name + ".png");

        private static Sprite LoadDrawn(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(UiIconFactory.Folder + name + ".png");

        // ---------------- title ----------------

        private static void LayoutTitle(Transform canvas, GameFlowManager flow)
        {
            Transform title = canvas.Find("TitlePanel");
            if (title == null)
                return;
            // Coins live on the Upgrade/Skin screens only. Kept (inactive) for its references.
            Transform coin = title.Find("CoinBalanceText");
            if (coin != null)
                coin.gameObject.SetActive(false);

            Transform settings = title.Find("SettingsButton");
            if (settings != null)
                RoundButton(settings, "icon_settings", new Vector2(0f, 1f), new Vector2(26f, -26f), 118f, null);

            Transform upgrade = title.Find("UpgradeButton");
            if (upgrade != null)
            {
                RoundButton(upgrade, "icon_upgrade", new Vector2(0.5f, 0f), new Vector2(165f, 210f), 170f, "UPGRADE");
                Transform skin = title.Find("SkinButton");
                if (skin == null)
                {
                    skin = UnityEngine.Object.Instantiate(upgrade.gameObject, title).transform;
                    skin.name = "SkinButton";
                }
                Button button = skin.GetComponent<Button>();
                while (button.onClick.GetPersistentEventCount() > 0)
                    UnityEventTools.RemovePersistentListener(button.onClick, 0);
                UnityEventTools.AddPersistentListener(button.onClick, new UnityAction(flow.OpenSkins));
                RoundButton(skin, "icon_skin", new Vector2(0.5f, 0f), new Vector2(-165f, 210f), 170f, "SKIN");
            }

            RectTransform tap = title.Find("TapToStartText") as RectTransform;
            if (tap != null)
            {
                tap.anchorMin = tap.anchorMax = new Vector2(0.5f, 0f);
                tap.anchoredPosition = new Vector2(0f, 420f);
            }
        }

        // Turns a framed rectangular button into a round icon button with an
        // optional caption underneath. The Button, its name and listeners stay.
        private static void RoundButton(Transform button, string icon, Vector2 anchor, Vector2 position, float size, string caption)
        {
            RectTransform rt = (RectTransform)button;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(anchor.x == 0f ? 0f : 0.5f, anchor.y == 1f ? 1f : 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(size, size);
            Image frame = button.GetComponent<Image>();
            frame.sprite = Load("ui_round_button");
            frame.type = Image.Type.Simple;
            frame.preserveAspect = true;
            frame.color = Color.white;

            Transform iconTransform = button.Find("Icon");
            Image iconImage = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            if (iconImage == null)
            {
                GameObject go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(button, false);
                iconImage = go.GetComponent<Image>();
            }
            iconImage.sprite = Load(icon);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            RectTransform irt = iconImage.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = Vector2.one * size * 0.62f;

            Transform label = button.Find("Label");
            if (label == null)
                return;
            TMP_Text text = label.GetComponent<TMP_Text>();
            label.gameObject.SetActive(caption != null);
            if (caption == null || text == null)
                return;
            text.text = caption;
            text.fontSize = 36f;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            RectTransform lrt = (RectTransform)label;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = new Vector2(0f, -4f);
            lrt.sizeDelta = new Vector2(280f, 50f);
        }

        // ---------------- upgrade ----------------

        private static void LayoutUpgrade(Transform upgrade)
        {
            if (upgrade == null)
                return;
            Transform back = upgrade.Find("BackButton");
            if (back != null)
                RoundButton(back, "icon_back", new Vector2(0f, 1f), new Vector2(26f, -26f), 118f, null);
            Transform heading = upgrade.Find("ItemScrollView/Viewport/Content/WeaponSkinHeading");
            if (heading != null)
                UnityEngine.Object.DestroyImmediate(heading.gameObject);
            // The back button moved to the corner, so the list can use the full height.
            if (upgrade.Find("Frame") is RectTransform frame)
            {
                frame.anchoredPosition = new Vector2(0f, -40f);
                frame.sizeDelta = new Vector2(820f, 1080f);
            }
            if (upgrade.Find("HeadingText") is RectTransform title)
                title.anchoredPosition = new Vector2(0f, 400f);
            if (upgrade.Find("ItemScrollView") is RectTransform list)
            {
                list.anchoredPosition = new Vector2(0f, -60f);
                list.sizeDelta = new Vector2(740f, 800f);
            }
        }

        private const string GeneratedFolder = "Assets/Art/2D/Generated/";

        private static Sprite Generated(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(GeneratedFolder + name + ".png");

        private static void ImportSprite(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer) ||
                importer.textureType == TextureImporterType.Sprite)
                return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
        }

        // ---------------- upgrade screen (2026-10-01 sketch) ----------------
        // Full-screen list: icon | level pips + name + "now -> next" | arrow button
        // + coin cost. Consumables sit in their own box at the bottom. Text is
        // kept to item names and numbers; coins and upgrades are icons.
        private static void LayoutUpgradeScreen(Transform panel)
        {
            if (panel == null)
                return;
            Image backdrop = panel.GetComponent<Image>();
            if (backdrop != null)
                backdrop.color = new Color(0.03f, 0.015f, 0.05f, 0.94f);

            // Coin counter: icon + number, top-right.
            RectTransform coin = panel.Find("CoinBalanceText") as RectTransform;
            if (coin != null)
            {
                Corner(coin, new Vector2(-24f, -85f), new Vector2(160f, 60f));
                TMP_Text text = coin.GetComponent<TMP_Text>();
                text.alignment = TextAlignmentOptions.Right;
                text.fontSize = 38f;
                text.color = new Color(1f, 0.86f, 0.42f);
                Image icon = Child<Image>(panel, "CoinIcon");
                icon.sprite = Generated("icon_coin_gold");
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                Corner(icon.rectTransform, new Vector2(-190f, -85f), new Vector2(58f, 58f));
            }

            RectTransform heading = panel.Find("HeadingText") as RectTransform;
            if (heading != null)
            {
                heading.anchorMin = heading.anchorMax = new Vector2(0.5f, 1f);
                heading.pivot = new Vector2(0.5f, 0.5f);
                heading.anchoredPosition = new Vector2(0f, -190f);
                heading.sizeDelta = new Vector2(640f, 70f);
                TMP_Text text = heading.GetComponent<TMP_Text>();
                if (text != null) { text.fontSize = 50f; text.text = "ITEM UPGRADES"; }
            }

            if (panel.Find("Frame") is RectTransform frame)
                Inset(frame, 22f, 300f, 22f, 232f);
            RectTransform list = panel.Find("ItemScrollView") as RectTransform;
            if (list != null)
                Inset(list, 58f, 356f, 58f, 300f);

            foreach (PowerUpUpgradeRow row in panel.GetComponentsInChildren<PowerUpUpgradeRow>(true))
                StyleUpgradeRow(row);
            Transform content = panel.Find("ItemScrollView/Viewport/Content");
            if (content != null && content.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup vlg)
            {
                vlg.spacing = 4f;
                vlg.childForceExpandWidth = true;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandHeight = false;
            }

            BuildConsumableArea(panel);
            BuildUpgradePopup(panel);
        }

        private static void StyleUpgradeRow(PowerUpUpgradeRow row)
        {
            Transform t = row.transform;
            Image bg = t.GetComponent<Image>();
            if (bg != null)
                bg.color = new Color(0f, 0f, 0f, 0f);
            LayoutElement le = t.TryGetComponent(out LayoutElement found) ? found : t.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 150f;
            RectTransform rowRect = (RectTransform)t;
            rowRect.sizeDelta = new Vector2(rowRect.sizeDelta.x, 150f);

            Image divider = Child<Image>(t, "Divider");
            divider.color = new Color(1f, 0.3f, 0.6f, 0.35f);
            divider.raycastTarget = false;
            RectTransform drt = divider.rectTransform;
            drt.anchorMin = new Vector2(0f, 0f);
            drt.anchorMax = new Vector2(1f, 0f);
            drt.pivot = new Vector2(0.5f, 0f);
            drt.anchoredPosition = Vector2.zero;
            drt.sizeDelta = new Vector2(-20f, 3f);

            if (t.Find("Icon") is RectTransform icon)
                Place(icon, new Vector2(-250f, 0f), new Vector2(112f, 112f));

            // Seven level pips above the name.
            RectTransform pips = Child<RectTransform>(t, "LevelPips");
            Place(pips, new Vector2(-55f, 44f), new Vector2(250f, 16f));
            Image[] pipImages = new Image[7];
            for (int i = 0; i < 7; i++)
            {
                Image pip = Child<Image>(pips, "Pip" + i);
                pip.raycastTarget = false;
                RectTransform prt = pip.rectTransform;
                prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f);
                prt.pivot = new Vector2(0f, 0.5f);
                prt.anchoredPosition = new Vector2(i * 34f, 0f);
                prt.sizeDelta = new Vector2(28f, 14f);
                pipImages[i] = pip;
            }

            LeftText(t, "NameLevelText", new Vector2(-55f, 6f), new Vector2(260f, 40f), 30f, new Color(1f, 0.62f, 0.82f));
            LeftText(t, "EffectText", new Vector2(-55f, -38f), new Vector2(260f, 40f), 28f, Color.white);

            RectTransform button = t.Find("UpgradeButton") as RectTransform;
            if (button != null)
            {
                RoundButton(button, "icon_upgrade", new Vector2(0.5f, 0.5f), new Vector2(232f, 18f), 96f, null);
                Button b = button.GetComponent<Button>();
                ColorBlock colors = b.colors;
                colors.disabledColor = new Color(1f, 1f, 1f, 0.3f);
                b.colors = colors;
                b.targetGraphic = button.GetComponent<Image>();
            }
            Image costIcon = Child<Image>(t, "CostIcon");
            costIcon.sprite = Generated("icon_coin_gold");
            costIcon.preserveAspect = true;
            costIcon.raycastTarget = false;
            Place(costIcon.rectTransform, new Vector2(196f, -54f), new Vector2(32f, 32f));
            LeftText(t, "CostText", new Vector2(270f, -54f), new Vector2(110f, 34f), 26f, new Color(1f, 0.85f, 0.4f));

            SerializedObject data = new SerializedObject(row);
            SerializedProperty pipProp = data.FindProperty("levelPips");
            pipProp.arraySize = 7;
            for (int i = 0; i < 7; i++)
                pipProp.GetArrayElementAtIndex(i).objectReferenceValue = pipImages[i];
            data.FindProperty("costIcon").objectReferenceValue = costIcon.gameObject;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildConsumableArea(Transform panel)
        {
            RectTransform area = Child<RectTransform>(panel, "ConsumableArea");
            area.anchorMin = new Vector2(0f, 0f);
            area.anchorMax = new Vector2(1f, 0f);
            area.pivot = new Vector2(0.5f, 0f);
            area.anchoredPosition = new Vector2(0f, 36f);
            area.sizeDelta = new Vector2(-48f, 250f);

            Transform heading = panel.Find("ItemScrollView/Viewport/Content/ConsumableHeading") ?? area.Find("ConsumableHeading");
            if (heading != null)
            {
                heading.SetParent(area, false);
                if (heading.TryGetComponent(out LayoutElement headingLayout))
                    headingLayout.ignoreLayout = true;
                RectTransform hrt = (RectTransform)heading;
                hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1f);
                hrt.pivot = new Vector2(0.5f, 1f);
                hrt.anchoredPosition = Vector2.zero;
                hrt.sizeDelta = new Vector2(600f, 60f);
                TMP_Text text = heading.GetComponent<TMP_Text>();
                if (text != null)
                {
                    text.text = "CONSUMABLES";
                    text.fontSize = 42f;
                    text.alignment = TextAlignmentOptions.Center;
                }
            }

            Image box = Child<Image>(area, "Box");
            box.sprite = Load("ui_skin_card");
            box.type = Image.Type.Sliced;
            box.pixelsPerUnitMultiplier = 1.4f;
            box.color = new Color(1f, 0.55f, 0.8f);
            box.raycastTarget = false;
            Inset(box.rectTransform, 0f, 0f, 0f, 66f);
            box.transform.SetAsFirstSibling();

            ConsumableShopRow[] rows = panel.GetComponentsInChildren<ConsumableShopRow>(true);
            for (int i = 0; i < rows.Length; i++)
            {
                Transform t = rows[i].transform;
                t.SetParent(area, false);
                if (t.TryGetComponent(out LayoutElement rowLayout))
                    rowLayout.ignoreLayout = true;
                RectTransform rt = (RectTransform)t;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(i == 0 ? -168f : 168f, -32f);
                rt.sizeDelta = new Vector2(310f, 130f);
                Image card = t.GetComponent<Image>();
                card.sprite = Load("ui_skin_card");
                card.type = Image.Type.Sliced;
                card.pixelsPerUnitMultiplier = 1.6f;
                card.color = Color.white;

                Image mark = Child<Image>(t, "EquippedMark");
                mark.transform.SetAsFirstSibling();
                mark.sprite = Load("ui_skin_card");
                mark.type = Image.Type.Sliced;
                mark.pixelsPerUnitMultiplier = 1.6f;
                mark.color = new Color(1f, 0.3f, 0.62f);
                mark.raycastTarget = false;
                RectTransform mrt = mark.rectTransform;
                mrt.anchorMin = Vector2.zero;
                mrt.anchorMax = Vector2.one;
                mrt.offsetMin = new Vector2(-7f, -7f);
                mrt.offsetMax = new Vector2(7f, 7f);

                bool shield = t.name.Contains("Shield");
                Image icon = Child<Image>(t, "Icon");
                icon.sprite = Generated(shield ? "icon_shield" : "icon_dash");
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                Place(icon.rectTransform, new Vector2(-92f, 0f), new Vector2(92f, 92f));
                LeftText(t, "CountText", new Vector2(52f, 0f), new Vector2(170f, 50f), 30f, Color.white);

                // The whole card is the equip toggle; its label is no longer shown.
                RectTransform action = t.Find("ActionButton") as RectTransform;
                if (action != null)
                {
                    action.anchorMin = Vector2.zero;
                    action.anchorMax = Vector2.one;
                    action.offsetMin = action.offsetMax = Vector2.zero;
                    Image hit = action.GetComponent<Image>();
                    hit.sprite = null;
                    hit.color = new Color(0f, 0f, 0f, 0f);
                    Transform label = action.Find("Label");
                    if (label != null)
                        label.gameObject.SetActive(false);
                    action.SetAsLastSibling();
                }

                SerializedObject data = new SerializedObject(rows[i]);
                data.FindProperty("equippedMark").objectReferenceValue = mark.gameObject;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void BuildUpgradePopup(Transform panel)
        {
            Image dim = Child<Image>(panel, "UpgradePopup");
            dim.color = new Color(0f, 0f, 0f, 0.6f);
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            UpgradePopup popup = dim.TryGetComponent(out UpgradePopup existingPopup) ? existingPopup : dim.gameObject.AddComponent<UpgradePopup>();
            Button dimButton = dim.TryGetComponent(out Button existingDim) ? existingDim : dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            while (dimButton.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(dimButton.onClick, 0);
            UnityEventTools.AddPersistentListener(dimButton.onClick, new UnityAction(popup.Hide));

            Image dialog = Child<Image>(dim.transform, "Dialog");
            dialog.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GeneratedFolder + "ui_panel_gothic.png");
            dialog.type = Image.Type.Sliced;
            dialog.pixelsPerUnitMultiplier = 2.2f;
            dialog.color = Color.white;
            Place(dialog.rectTransform, Vector2.zero, new Vector2(660f, 380f));

            TextMeshProUGUI message = Child<TextMeshProUGUI>(dialog.transform, "MessageText");
            GameFont.Apply(message);
            message.fontSize = 38f;
            message.alignment = TextAlignmentOptions.Center;
            message.enableWordWrapping = true;
            message.raycastTarget = false;
            Place(message.rectTransform, new Vector2(0f, 40f), new Vector2(540f, 140f));

            Image ok = Child<Image>(dialog.transform, "OkButton");
            ok.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GeneratedFolder + "ui_button_gothic.png");
            ok.type = Image.Type.Simple;
            ok.preserveAspect = true;
            Place(ok.rectTransform, new Vector2(0f, -100f), new Vector2(300f, 100f));
            Button okButton = ok.TryGetComponent(out Button existingOk) ? existingOk : ok.gameObject.AddComponent<Button>();
            okButton.targetGraphic = ok;
            while (okButton.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(okButton.onClick, 0);
            UnityEventTools.AddPersistentListener(okButton.onClick, new UnityAction(popup.Hide));
            TextMeshProUGUI okLabel = Child<TextMeshProUGUI>(ok.transform, "Label");
            GameFont.Apply(okLabel);
            okLabel.text = "OK";
            okLabel.fontSize = 36f;
            okLabel.alignment = TextAlignmentOptions.Center;
            okLabel.raycastTarget = false;
            Stretch(okLabel.rectTransform);

            SerializedObject data = new SerializedObject(popup);
            data.FindProperty("messageText").objectReferenceValue = message;
            data.FindProperty("dialog").objectReferenceValue = dialog.rectTransform;
            data.ApplyModifiedPropertiesWithoutUndo();
            dim.transform.SetAsLastSibling();
            dim.gameObject.SetActive(false);
        }

        private static void LeftText(Transform row, string name, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            TMP_Text text = row.Find(name) is Transform found ? found.GetComponent<TMP_Text>() : null;
            if (text == null)
            {
                text = Child<TextMeshProUGUI>(row, name);
                GameFont.Apply(text);
            }
            Place(text.rectTransform, position, size);
            text.fontSize = fontSize;
            text.enableAutoSizing = true;
            text.fontSizeMin = 16f;
            text.fontSizeMax = fontSize;
            text.alignment = TextAlignmentOptions.Left;
            text.color = color;
            text.richText = true;
            text.raycastTarget = false;
        }

        private static void Corner(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        private static void Inset(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        // ---------------- skins ----------------

        private static void BuildSkinPanel(Transform canvas, Transform upgrade, GameFlowManager flow)
        {
            Transform panel = canvas.Find("SkinPanel");
            if (panel == null)
            {
                panel = new GameObject("SkinPanel", typeof(RectTransform)).transform;
                panel.SetParent(canvas, false);
                if (upgrade != null)
                    panel.SetSiblingIndex(upgrade.GetSiblingIndex() + 1);
            }
            Stretch((RectTransform)panel);

            // Darkened column behind the card list so cards read over the scene.
            Image backdrop = Child<Image>(panel, "ListBackdrop");
            backdrop.color = new Color(0.04f, 0.015f, 0.06f, 0.8f);
            backdrop.raycastTarget = true;
            RectTransform brt = backdrop.rectTransform;
            brt.anchorMin = new Vector2(1f, 0f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(1f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(280f, 0f);

            // Back arrow and coin counter mirror the Upgrade screen.
            Transform back = panel.Find("BackButton");
            if (back == null && upgrade != null)
            {
                back = UnityEngine.Object.Instantiate(upgrade.Find("BackButton").gameObject, panel).transform;
                back.name = "BackButton";
            }
            if (back != null)
            {
                Button button = back.GetComponent<Button>();
                while (button.onClick.GetPersistentEventCount() > 0)
                    UnityEventTools.RemovePersistentListener(button.onClick, 0);
                UnityEventTools.AddPersistentListener(button.onClick, new UnityAction(flow.CloseSkins));
                RoundButton(back, "icon_back", new Vector2(0f, 1f), new Vector2(26f, -26f), 118f, null);
            }
            Transform coin = panel.Find("CoinBalanceText");
            if (coin == null && upgrade != null)
            {
                coin = UnityEngine.Object.Instantiate(upgrade.Find("CoinBalanceText").gameObject, panel).transform;
                coin.name = "CoinBalanceText";
            }

            TMP_Text heading = Child<TextMeshProUGUI>(panel, "HeadingText");
            GameFont.Apply(heading);
            heading.text = "SCYTHE";
            heading.fontSize = 34f;
            heading.alignment = TextAlignmentOptions.Center;
            heading.color = new Color(1f, 0.45f, 0.7f);
            heading.raycastTarget = false;
            RectTransform hrt = heading.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(1f, 1f);
            hrt.anchoredPosition = new Vector2(-10f, -96f);
            hrt.sizeDelta = new Vector2(260f, 50f);

            RectTransform content = BuildScroll(panel);
            foreach (SkinShopRow row in canvas.GetComponentsInChildren<SkinShopRow>(true))
                StyleCard(row, content);
            foreach (SkinShopRow row in canvas.GetComponentsInChildren<SkinShopRow>(true))
                if (row.transform.parent != content)
                    row.transform.SetParent(content, false);

            SerializedObject flowData = new SerializedObject(flow);
            flowData.FindProperty("skinPanel").objectReferenceValue = panel.gameObject;
            flowData.ApplyModifiedPropertiesWithoutUndo();
            UpgradeMenu menu = flow.GetComponent<UpgradeMenu>();
            if (menu != null && coin != null)
            {
                SerializedObject menuData = new SerializedObject(menu);
                menuData.FindProperty("skinCoinText").objectReferenceValue = coin.GetComponent<TMP_Text>();
                menuData.ApplyModifiedPropertiesWithoutUndo();
            }
            panel.gameObject.SetActive(false);
        }

        private static RectTransform BuildScroll(Transform panel)
        {
            Transform existing = panel.Find("SkinScrollView");
            RectTransform scroll = existing as RectTransform;
            if (scroll == null)
            {
                scroll = new GameObject("SkinScrollView", typeof(RectTransform), typeof(ScrollRect)).GetComponent<RectTransform>();
                scroll.SetParent(panel, false);
            }
            scroll.anchorMin = new Vector2(1f, 0f);
            scroll.anchorMax = new Vector2(1f, 1f);
            scroll.pivot = new Vector2(1f, 0.5f);
            scroll.offsetMin = new Vector2(-270f, 60f);
            scroll.offsetMax = new Vector2(-10f, -150f);

            RectTransform viewport = scroll.Find("Viewport") as RectTransform;
            if (viewport == null)
            {
                viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask)).GetComponent<RectTransform>();
                viewport.SetParent(scroll, false);
            }
            Stretch(viewport);
            viewport.GetComponent<Mask>().showMaskGraphic = false;
            viewport.GetComponent<Image>().color = Color.white;

            RectTransform content = viewport.Find("Content") as RectTransform;
            if (content == null)
            {
                content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                    typeof(ContentSizeFitter)).GetComponent<RectTransform>();
                content.SetParent(viewport, false);
            }
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.padding = new RectOffset(10, 10, 6, 20);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect rect = scroll.GetComponent<ScrollRect>();
            rect.content = content;
            rect.viewport = viewport;
            rect.horizontal = false;
            rect.vertical = true;
            rect.movementType = ScrollRect.MovementType.Elastic;
            rect.scrollSensitivity = 30f;
            return content;
        }

        private static void StyleCard(SkinShopRow row, RectTransform content)
        {
            Transform t = row.transform;
            Image card = t.GetComponent<Image>();
            card.sprite = Load("ui_skin_card");
            card.type = Image.Type.Sliced;
            card.color = Color.white;
            card.pixelsPerUnitMultiplier = 1.4f;
            LayoutElement le = (t.TryGetComponent(out LayoutElement existingLayoutElement) ? existingLayoutElement : t.gameObject.AddComponent<LayoutElement>());
            le.minHeight = le.preferredHeight = 250f;

            Image mark = Child<Image>(t, "EquippedMark");
            mark.transform.SetAsFirstSibling();
            mark.sprite = Load("ui_skin_card");
            mark.type = Image.Type.Sliced;
            mark.pixelsPerUnitMultiplier = 1.4f;
            mark.color = new Color(1f, 0.3f, 0.62f, 1f);
            mark.raycastTarget = false;
            RectTransform mrt = mark.rectTransform;
            mrt.anchorMin = Vector2.zero;
            mrt.anchorMax = Vector2.one;
            mrt.offsetMin = new Vector2(-7f, -7f);
            mrt.offsetMax = new Vector2(7f, 7f);
            mark.gameObject.SetActive(false);

            Image swatch = Child<Image>(t, "Swatch");
            swatch.sprite = LoadDrawn("icon_skin"); // white silhouette tints cleanly
            swatch.preserveAspect = true;
            swatch.raycastTarget = false;
            Place(swatch.rectTransform, new Vector2(0f, 52f), new Vector2(120f, 120f));

            PlaceText(t, "NameText", new Vector2(0f, -28f), new Vector2(220f, 34f), 24f);
            PlaceText(t, "PriceText", new Vector2(0f, -60f), new Vector2(220f, 30f), 19f);
            RectTransform action = t.Find("ActionButton") as RectTransform;
            if (action != null)
                Place(action, new Vector2(0f, -100f), new Vector2(196f, 50f));

            SerializedObject data = new SerializedObject(row);
            data.FindProperty("swatch").objectReferenceValue = swatch;
            data.FindProperty("equippedMark").objectReferenceValue = mark.gameObject;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PlaceText(Transform row, string name, Vector2 position, Vector2 size, float fontSize)
        {
            RectTransform rt = row.Find(name) as RectTransform;
            if (rt == null)
                return;
            Place(rt, position, size);
            TMP_Text text = rt.GetComponent<TMP_Text>();
            if (text == null)
                return;
            text.fontSize = fontSize;
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            text.fontSizeMax = fontSize;
            text.alignment = TextAlignmentOptions.Center;
        }

        private static void Place(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static T Child<T>(Transform parent, string name) where T : Component
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name, typeof(RectTransform), typeof(T)).transform;
                t.SetParent(parent, false);
            }
            return (t.TryGetComponent(out T existingT) ? existingT : t.gameObject.AddComponent<T>());
        }
    }
}
