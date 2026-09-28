using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Deadline4Sec.Editor
{
    // Keeps saved menus up to date without rebuilding authored artwork at runtime.
    public static class EditableMenuSetup
    {
        [MenuItem("Deadline 4 Sec/Update Title And Tutorial Menus")]
        public static void PrepareScenes()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the open scene before updating saved menus.");
            if (AssetDatabase.LoadAssetAtPath<PowerUpBalance>(
                    "Assets/Resources/PowerUps/PowerUpBalance.asset") == null)
                throw new InvalidOperationException(
                    "Run Deadline 4 Sec/Prepare Coin And Power-Up Assets first.");
            string originalScene = EditorSceneManager.GetActiveScene().path;
            foreach (string path in new[] { "Assets/Scenes/EndlessRun.unity",
                "Assets/Scenes/GrayboxCourse.unity", "Assets/Scenes/TestScene.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                GameFlowManager flow = UnityEngine.Object.FindFirstObjectByType<GameFlowManager>();
                if (flow == null)
                    throw new InvalidOperationException("Missing GameFlowManager: " + path);
                if (flow.GetComponent<CoinWallet>() == null)
                    flow.gameObject.AddComponent<CoinWallet>();
                PowerUpManager powerUps = flow.GetComponent<PowerUpManager>();
                if (powerUps == null)
                    powerUps = flow.gameObject.AddComponent<PowerUpManager>();
                SerializedObject powerUpData = new SerializedObject(powerUps);
                powerUpData.FindProperty("balance").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<PowerUpBalance>("Assets/Resources/PowerUps/PowerUpBalance.asset");
                powerUpData.FindProperty("gameTimer").objectReferenceValue = flow.GetComponent<GameTimer>();
                powerUpData.FindProperty("wallet").objectReferenceValue = flow.GetComponent<CoinWallet>();
                powerUpData.FindProperty("player").objectReferenceValue =
                    UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                powerUpData.ApplyModifiedPropertiesWithoutUndo();
                UpgradeMenu upgradeMenu = flow.GetComponent<UpgradeMenu>();
                if (upgradeMenu == null)
                    upgradeMenu = flow.gameObject.AddComponent<UpgradeMenu>();
                SerializedObject data = new SerializedObject(flow);
                GameObject result = (GameObject)data.FindProperty("resultPanel").objectReferenceValue;
                Canvas canvas = result.GetComponentInParent<Canvas>();
                List<Button> buttons = new List<Button>();

                Transform title = canvas.transform.Find("TitlePanel");
                if (title == null)
                {
                    title = Overlay(canvas.transform, "TitlePanel").transform;
                    Label(title, "TitleText", "DEADLINE: 4 SEC", new Vector2(0, 185), new Vector2(680, 110), 58);
                    Label(title, "SubtitleText", "RISK TO LIVE", new Vector2(0, 105), new Vector2(600, 65), 28);
                    MenuButton(title, "SettingsButton", "SETTINGS", new Vector2(0, -245), new Vector2(360, 84), flow.OpenSettings);
                    CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(800, 600);
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 0;
                }
                // Keep authored artwork, headings and Settings placement intact.
                RemoveIfPresent(title, "StartButton");
                RemoveIfPresent(title, "TutorialButton");
                if (title.Find("TitleTapArea") == null)
                    TransparentTapArea(title, flow.StartFromTitle);
                if (title.Find("TapToStartText") == null)
                    Label(title, "TapToStartText", "Tap to start", new Vector2(0, -45),
                        new Vector2(560, 80), 38);
                Transform titleCoin = title.Find("CoinBalanceText");
                if (titleCoin == null)
                    titleCoin = CreateCoinLabel(title, "CoinBalanceText", new Vector2(-22, -22));
                if (title.Find("UpgradeButton") == null)
                {
                    RectTransform settingsButton = title.Find("SettingsButton").GetComponent<RectTransform>();
                    settingsButton.anchoredPosition = new Vector2(-180, -252);
                    settingsButton.sizeDelta = new Vector2(320, 84);
                    MenuButton(title, "UpgradeButton", "UPGRADES", new Vector2(180, -252),
                        new Vector2(320, 84), flow.OpenUpgrades);
                }
                foreach (Graphic graphic in title.GetComponentsInChildren<Graphic>(true))
                    if (graphic.GetComponent<Button>() == null)
                        graphic.raycastTarget = false;
                Transform settings = canvas.transform.Find("SettingsPanel");
                if (settings == null)
                {
                    settings = Overlay(canvas.transform, "SettingsPanel").transform;
                    Label(settings, "HeadingText", "SETTINGS", new Vector2(0, 175), new Vector2(600, 90), 52);
                    MenuButton(settings, "SoundButton", "SOUND   ON", new Vector2(0, 50), new Vector2(420, 80), flow.ToggleSound);
                    MenuButton(settings, "VibrationButton", "VIBRATION   ON", new Vector2(0, -50), new Vector2(420, 80), flow.ToggleVibration);
                    MenuButton(settings, "BackButton", "BACK", new Vector2(0, -175), new Vector2(360, 80), flow.CloseSettings);
                }
                if (settings.Find("TutorialButton") == null)
                {
                    RectTransform vibration = settings.Find("VibrationButton").GetComponent<RectTransform>();
                    float tutorialY = vibration.anchoredPosition.y - 100f;
                    MenuButton(settings, "TutorialButton", "TUTORIAL", new Vector2(0, tutorialY),
                        new Vector2(420, 80), flow.StartTutorial);
                    RectTransform back = settings.Find("BackButton").GetComponent<RectTransform>();
                    if (Mathf.Abs(back.anchoredPosition.y - tutorialY) < 92f)
                        back.anchoredPosition = new Vector2(back.anchoredPosition.x, tutorialY - 105f);
                }
                Transform skipUI = canvas.transform.Find("TutorialSkipUI");
                if (skipUI == null)
                    skipUI = CreateSkipUI(canvas.transform, flow);
                Transform skipConfirmation = canvas.transform.Find("TutorialSkipConfirmation");
                if (skipConfirmation == null)
                    skipConfirmation = CreateSkipConfirmation(canvas.transform, flow);
                if (result.transform.Find("MainMenuButton") == null)
                {
                    MenuButton(result.transform, "MainMenuButton", "MAIN MENU", new Vector2(0, -350), new Vector2(360, 80), flow.MainMenu);
                    Button retry = (Button)data.FindProperty("retryButton").objectReferenceValue;
                    retry.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -255);
                }
                Transform upgrades = canvas.transform.Find("UpgradePanel");
                if (upgrades == null)
                    upgrades = CreateUpgradePanel(canvas.transform, flow);
                RepairUpgradeRows(upgrades);
                Transform runCoin = ((GameObject)data.FindProperty("gameplayUI").objectReferenceValue)
                    .transform.Find("RunCoinText");
                if (runCoin == null)
                    runCoin = CreateCoinLabel(((GameObject)data.FindProperty("gameplayUI")
                        .objectReferenceValue).transform, "RunCoinText", new Vector2(-22, -105));
                Transform effects = ((GameObject)data.FindProperty("gameplayUI").objectReferenceValue)
                    .transform.Find("ActivePowerUpsText");
                if (effects == null)
                {
                    TMP_Text label = Label(((GameObject)data.FindProperty("gameplayUI")
                        .objectReferenceValue).transform, "ActivePowerUpsText", "", Vector2.zero,
                        new Vector2(320, 180), 24);
                    RectTransform rect = label.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
                    rect.pivot = Vector2.one;
                    rect.anchoredPosition = new Vector2(-22, -150);
                    label.alignment = TextAlignmentOptions.TopRight;
                    effects = rect;
                }
                SerializedObject upgradesData = new SerializedObject(upgradeMenu);
                upgradesData.FindProperty("titleCoinText").objectReferenceValue = titleCoin.GetComponent<TMP_Text>();
                upgradesData.FindProperty("upgradeCoinText").objectReferenceValue = upgrades.Find("CoinBalanceText").GetComponent<TMP_Text>();
                upgradesData.FindProperty("gameplayCoinText").objectReferenceValue = runCoin.GetComponent<TMP_Text>();
                upgradesData.FindProperty("activeEffectsText").objectReferenceValue = effects.GetComponent<TMP_Text>();
                PowerUpUpgradeRow[] rows = upgrades.GetComponentsInChildren<PowerUpUpgradeRow>(true);
                SerializedProperty rowList = upgradesData.FindProperty("rows");
                rowList.arraySize = rows.Length;
                for (int i = 0; i < rows.Length; i++)
                    rowList.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
                upgradesData.ApplyModifiedPropertiesWithoutUndo();
                data.FindProperty("upgradePanel").objectReferenceValue = upgrades.gameObject;
                data.FindProperty("titlePanel").objectReferenceValue = title.gameObject;
                data.FindProperty("settingsPanel").objectReferenceValue = settings.gameObject;
                data.FindProperty("tutorialSkipUI").objectReferenceValue = skipUI.gameObject;
                data.FindProperty("tutorialSkipConfirmation").objectReferenceValue = skipConfirmation.gameObject;
                data.FindProperty("soundSettingText").objectReferenceValue = settings.Find("SoundButton/Label").GetComponent<TMP_Text>();
                data.FindProperty("vibrationSettingText").objectReferenceValue = settings.Find("VibrationButton/Label").GetComponent<TMP_Text>();
                buttons.AddRange(title.GetComponentsInChildren<Button>(true));
                buttons.AddRange(settings.GetComponentsInChildren<Button>(true));
                buttons.AddRange(skipUI.GetComponentsInChildren<Button>(true));
                buttons.AddRange(skipConfirmation.GetComponentsInChildren<Button>(true));
                buttons.AddRange(upgrades.GetComponentsInChildren<Button>(true));
                buttons.Add(result.transform.Find("MainMenuButton").GetComponent<Button>());
                SerializedProperty buttonList = data.FindProperty("menuButtons");
                buttonList.arraySize = buttons.Count;
                for (int i = 0; i < buttons.Count; i++)
                    buttonList.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
                data.ApplyModifiedPropertiesWithoutUndo();
                title.gameObject.SetActive(true);
                settings.gameObject.SetActive(false);
                skipUI.gameObject.SetActive(false);
                skipConfirmation.gameObject.SetActive(false);
                upgrades.gameObject.SetActive(false);
                foreach (string field in new[] { "gameplayUI", "countdownUI", "resultPanel" })
                    ((GameObject)data.FindProperty(field).objectReferenceValue).SetActive(false);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("Saved editable menu: " + path);
            }
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(originalScene))
                EditorSceneManager.OpenScene(originalScene);
        }

        private static void RemoveIfPresent(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        private static void TransparentTapArea(Transform title, UnityAction action)
        {
            GameObject area = new GameObject("TitleTapArea", typeof(RectTransform),
                typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            area.transform.SetParent(title, false);
            area.transform.SetSiblingIndex(0);
            RectTransform rect = area.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            UnityEngine.UI.Image image = area.GetComponent<UnityEngine.UI.Image>();
            image.color = Color.clear;
            UnityEngine.UI.Button button = area.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            UnityEventTools.AddPersistentListener(button.onClick, action);
        }

        private static Transform CreateSkipUI(Transform canvas, GameFlowManager flow)
        {
            GameObject root = new GameObject("TutorialSkipUI", typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            Stretch(root.GetComponent<RectTransform>());
            MenuButton(root.transform, "SkipButton", "SKIP", Vector2.zero,
                new Vector2(140, 64), flow.ShowSkipConfirmation);
            RectTransform skip = root.transform.Find("SkipButton").GetComponent<RectTransform>();
            skip.anchorMin = skip.anchorMax = Vector2.one;
            skip.pivot = Vector2.one;
            skip.anchoredPosition = new Vector2(-24, -24);
            return root.transform;
        }

        private static Transform CreateSkipConfirmation(Transform canvas, GameFlowManager flow)
        {
            GameObject overlay = Overlay(canvas, "TutorialSkipConfirmation");
            overlay.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.84f);
            GameObject dialog = new GameObject("Dialog", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            dialog.transform.SetParent(overlay.transform, false);
            RectTransform dialogRect = dialog.GetComponent<RectTransform>();
            dialogRect.anchorMin = dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.anchoredPosition = Vector2.zero;
            dialogRect.sizeDelta = new Vector2(700, 360);
            dialog.GetComponent<UnityEngine.UI.Image>().color = new Color(0.025f, 0.03f, 0.055f, 1f);
            Label(dialog.transform, "MessageText",
                "튜토리얼을 스킵하시겠습니까?\n(설정창에서 언제든지\n튜토리얼을 다시 할 수 있습니다.)",
                new Vector2(0, 65), new Vector2(650, 205), 34);
            MenuButton(dialog.transform, "YesButton", "예", new Vector2(-135, -100),
                new Vector2(220, 72), flow.ConfirmSkipTutorial);
            MenuButton(dialog.transform, "NoButton", "아니오", new Vector2(135, -100),
                new Vector2(220, 72), flow.CancelSkipTutorial);
            return overlay.transform;
        }

        private static Transform CreateCoinLabel(Transform parent, string name, Vector2 topRightOffset)
        {
            TMP_Text label = Label(parent, name, "COIN 0", Vector2.zero,
                new Vector2(260, 60), 30);
            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = topRightOffset;
            label.alignment = TextAlignmentOptions.TopRight;
            return rect;
        }

        private static Transform CreateUpgradePanel(Transform canvas, GameFlowManager flow)
        {
            GameObject panel = Overlay(canvas, "UpgradePanel");
            Label(panel.transform, "HeadingText", "ITEM UPGRADES", new Vector2(0, 250),
                new Vector2(500, 75), 44);
            CreateCoinLabel(panel.transform, "CoinBalanceText", new Vector2(-22, -20));
            MenuButton(panel.transform, "BackButton", "BACK", new Vector2(0, -260),
                new Vector2(290, 64), flow.CloseUpgrades);

            GameObject scrollObject = new GameObject("ItemScrollView", typeof(RectTransform),
                typeof(ScrollRect));
            scrollObject.transform.SetParent(panel.transform, false);
            RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
            scrollRect.anchorMin = scrollRect.anchorMax = new Vector2(0.5f, 0.5f);
            scrollRect.anchoredPosition = new Vector2(0, 0);
            scrollRect.sizeDelta = new Vector2(740, 410);

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform),
                typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollObject.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject content = new GameObject("Content", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scrolling = scrollObject.GetComponent<ScrollRect>();
            scrolling.viewport = viewport.GetComponent<RectTransform>();
            scrolling.content = contentRect;
            scrolling.horizontal = false;
            scrolling.vertical = true;
            scrolling.movementType = ScrollRect.MovementType.Clamped;
            scrolling.scrollSensitivity = 35;

            for (int i = 0; i < 6; i++)
                CreateUpgradeRow(content.transform, (PowerUpType)i);
            return panel.transform;
        }

        private static void CreateUpgradeRow(Transform parent, PowerUpType type)
        {
            GameObject card = new GameObject(type + "Row", typeof(RectTransform),
                typeof(Image), typeof(LayoutElement), typeof(PowerUpUpgradeRow));
            card.transform.SetParent(parent, false);
            card.GetComponent<LayoutElement>().preferredHeight = 112;
            card.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.20f, 1);
            card.GetComponent<Image>().raycastTarget = false;
            TMP_Text name = Label(card.transform, "NameLevelText", type.ToString().ToUpperInvariant(),
                new Vector2(-175, 34), new Vector2(350, 36), 27);
            name.alignment = TextAlignmentOptions.Left;
            TMP_Text effect = Label(card.transform, "EffectText", "", new Vector2(-175, -14),
                new Vector2(350, 54), 19);
            effect.alignment = TextAlignmentOptions.Left;
            TMP_Text cost = Label(card.transform, "CostText", "", new Vector2(235, 30),
                new Vector2(210, 34), 21);
            MenuButton(card.transform, "UpgradeButton", "UPGRADE", new Vector2(235, -18),
                new Vector2(175, 54), card.GetComponent<PowerUpUpgradeRow>().Upgrade);
            Button button = card.transform.Find("UpgradeButton").GetComponent<Button>();
            SerializedObject data = new SerializedObject(card.GetComponent<PowerUpUpgradeRow>());
            data.FindProperty("type").enumValueIndex = (int)type;
            data.FindProperty("nameLevelText").objectReferenceValue = name;
            data.FindProperty("effectText").objectReferenceValue = effect;
            data.FindProperty("costText").objectReferenceValue = cost;
            data.FindProperty("upgradeButton").objectReferenceValue = button;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RepairUpgradeRows(Transform panel)
        {
            for (int i = 0; i < 6; i++)
            {
                PowerUpType type = (PowerUpType)i;
                Transform rowTransform = panel.Find("ItemScrollView/Viewport/Content/" + type + "Row");
                if (rowTransform == null)
                    throw new InvalidOperationException("Missing upgrade row: " + type);
                GameObject rowObject = rowTransform.gameObject;
                if (rowObject.GetComponent<PowerUpUpgradeRow>() != null)
                    continue;
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(rowObject);
                PowerUpUpgradeRow row = rowObject.AddComponent<PowerUpUpgradeRow>();
                SerializedObject data = new SerializedObject(row);
                data.FindProperty("type").enumValueIndex = i;
                data.FindProperty("nameLevelText").objectReferenceValue = rowTransform.Find("NameLevelText").GetComponent<TMP_Text>();
                data.FindProperty("effectText").objectReferenceValue = rowTransform.Find("EffectText").GetComponent<TMP_Text>();
                data.FindProperty("costText").objectReferenceValue = rowTransform.Find("CostText").GetComponent<TMP_Text>();
                Button button = rowTransform.Find("UpgradeButton").GetComponent<Button>();
                data.FindProperty("upgradeButton").objectReferenceValue = button;
                data.ApplyModifiedPropertiesWithoutUndo();
                for (int listener = button.onClick.GetPersistentEventCount() - 1; listener >= 0; listener--)
                    UnityEventTools.RemovePersistentListener(button.onClick, listener);
                UnityEventTools.AddPersistentListener(button.onClick, row.Upgrade);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static GameObject Overlay(Transform parent, string name)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.025f, 0.03f, 0.055f, 0.97f);
            return panel;
        }

        private static TMP_Text Label(Transform parent, string name, string text,
            Vector2 position, Vector2 size, float fontSize)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TMP_Text label = obj.GetComponent<TMP_Text>();
            GameFont.Apply(label);
            label.text = text;
            label.color = Color.white;
            label.fontSize = label.fontSizeMax = fontSize;
            label.fontSizeMin = 18;
            label.enableAutoSizing = true;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static void MenuButton(Transform parent, string name, string text,
            Vector2 position, Vector2 size, UnityAction action)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = obj.GetComponent<Image>();
            image.color = new Color(0.72f, 0.09f, 0.24f, 1);
            Button button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            UnityEventTools.AddPersistentListener(button.onClick, action);
            TMP_Text label = Label(obj.transform, "Label", text, Vector2.zero, size - new Vector2(20, 10), 36);
            // Stretch labels with the button so resizing only needs its RectTransform.
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10, 5);
            labelRect.offsetMax = new Vector2(-10, -5);
        }
    }
}
