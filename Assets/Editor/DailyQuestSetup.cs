using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Deadline4Sec.Editor
{
    // Daily quest setup (DailyQuest.md, 2026-10-15):
    //  - Resources/DailyQuests/DailyQuestCatalog.asset (pool; created once),
    //  - TitlePanel/DailyQuestPopup (inactive): Backdrop, Popup_Dailyquest
    //    (ResetTimerText, box_Dailyquest_1..3, Xbutton_Dailyquest → Close),
    //    CurrencyBox (Soul icon + amount, off-screen right until a reward),
    //    RewardAnimationLayer (on top, for the flying reward icons),
    //  - the title's QuestButton: Button On Click → DailyQuestPopup.Open and a
    //    DailyQuestButton driving its RedDot child.
    // Art: Assets/Art/2D/Dailyquest. The open scene is changed in place (left
    // for you to save if it was dirty); the other scenes are saved and closed.
    public static class DailyQuestSetup
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/EndlessRun.unity",
            "Assets/Scenes/GrayboxCourse.unity",
            "Assets/Scenes/TestScene.unity",
        };

        private const string CatalogPath = "Assets/Resources/DailyQuests/DailyQuestCatalog.asset";
        private const string Art = "Assets/Art/2D/Dailyquest/";
        private const string SoulIcon = "Assets/Art/2D/Generated/icon_coin_gold.png";
        private static readonly Color Pink = new Color(0.93f, 0.31f, 0.55f);
        private static readonly float[] BoxY = { 184f, -10f, -204f };

        [MenuItem("Deadline 4 Sec/UI/Apply Daily Quest")]
        public static void Apply()
        {
            EnsureCatalog();
            var open = EditorSceneManager.GetActiveScene();
            bool openWasDirty = open.isDirty;
            foreach (string path in ScenePaths)
            {
                bool isOpen = open.path == path;
                var scene = isOpen ? open : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                Transform title = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.name == "Canvas")
                        title = root.transform.Find("TitlePanel");
                if (title != null)
                {
                    Build(title);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!(isOpen && openWasDirty))
                        EditorSceneManager.SaveScene(scene);
                }
                if (!isOpen)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        public static DailyQuestCatalog EnsureCatalog()
        {
            DailyQuestCatalog catalog = AssetDatabase.LoadAssetAtPath<DailyQuestCatalog>(CatalogPath);
            if (catalog != null)
                return catalog;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CatalogPath));
            catalog = ScriptableObject.CreateInstance<DailyQuestCatalog>();
            catalog.pool = DailyQuestCatalog.Defaults();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static void Build(Transform title)
        {
            RectTransform root = Child(title, "DailyQuestPopup");
            Stretch(root);
            root.SetAsLastSibling();
            DailyQuestPopup popup = root.TryGetComponent(out DailyQuestPopup p) ? p : root.gameObject.AddComponent<DailyQuestPopup>();

            Image backdrop = Img(root, "Backdrop", null, new Color(0.03f, 0.01f, 0.05f, 0.62f));
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            CanvasGroup backdropGroup = backdrop.TryGetComponent(out CanvasGroup g) ? g : backdrop.gameObject.AddComponent<CanvasGroup>();

            Image panel = Img(root, "Popup_Dailyquest", Load(Art + "Popup_Dailyquest.png"), Color.white);
            panel.raycastTarget = true;
            Place(panel.rectTransform, new Vector2(0f, -10f), new Vector2(700f, 1174f));
            RectTransform panelRect = panel.rectTransform;

            TMP_Text timer = Text(panelRect, "ResetTimerText", "11시간 11분 후 리셋됩니다", new Vector2(0f, 372f), new Vector2(520f, 40f),
                24f, Pink, TextAlignmentOptions.Center);

            var boxes = new DailyQuestBoxView[DailyQuestManager.QuestCount];
            for (int i = 0; i < boxes.Length; i++)
                boxes[i] = Box(panelRect, "box_Dailyquest_" + (i + 1), BoxY[i]);

            Image close = Img(panelRect, "Xbutton_Dailyquest", Load(Art + "Xbutton_Dailyquest.png"), Color.white);
            close.preserveAspect = true;
            Place(close.rectTransform, new Vector2(0f, -433f), new Vector2(110f, 104f));
            Button closeButton = OnClick(close.gameObject, close, popup.Close);

            // Soul box: top-right, a black copy of the box shape; parked off-screen.
            Image currency = Img(root, "CurrencyBox", Load(Art + "box_Dailyquest.png"), new Color(0.06f, 0.03f, 0.07f, 1f));
            RectTransform currencyRect = currency.rectTransform;
            currencyRect.anchorMin = currencyRect.anchorMax = new Vector2(1f, 1f);
            currencyRect.pivot = new Vector2(1f, 1f);
            currencyRect.anchoredPosition = new Vector2(-14f, -40f);
            currencyRect.sizeDelta = new Vector2(300f, 76f);
            Image currencyIcon = Img(currencyRect, "Icon", Load(SoulIcon), Color.white);
            currencyIcon.preserveAspect = true;
            Place(currencyIcon.rectTransform, new Vector2(-92f, 4f), new Vector2(74f, 74f));
            TMP_Text currencyText = Text(currencyRect, "AmountText", "0", new Vector2(36f, 0f), new Vector2(180f, 56f), 42f,
                Color.white, TextAlignmentOptions.Left);
            if (!currencyText.TryGetComponent(out NumericLabel _))
                currencyText.gameObject.AddComponent<NumericLabel>();
            GameFont.Apply(currencyText);

            RectTransform layer = Child(root, "RewardAnimationLayer");
            Stretch(layer);

            string[] order = { "Backdrop", "Popup_Dailyquest", "CurrencyBox", "RewardAnimationLayer" };
            for (int i = 0; i < order.Length; i++)
                root.Find(order[i])?.SetSiblingIndex(i);

            var so = new SerializedObject(popup);
            so.FindProperty("panel").objectReferenceValue = panelRect;
            so.FindProperty("backdrop").objectReferenceValue = backdropGroup;
            so.FindProperty("resetTimerText").objectReferenceValue = timer;
            SerializedProperty boxList = so.FindProperty("boxes");
            boxList.arraySize = boxes.Length;
            for (int i = 0; i < boxes.Length; i++)
                boxList.GetArrayElementAtIndex(i).objectReferenceValue = boxes[i];
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.FindProperty("currencyBox").objectReferenceValue = currencyRect;
            so.FindProperty("currencyIcon").objectReferenceValue = currencyIcon.rectTransform;
            so.FindProperty("currencyText").objectReferenceValue = currencyText;
            so.FindProperty("rewardLayer").objectReferenceValue = layer;
            so.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);

            // Title button (EndlessRun): opens the popup; its RedDot child shows unclaimed rewards.
            Transform questButton = title.Find("QuestButton");
            if (questButton != null)
            {
                OnClick(questButton.gameObject, questButton.GetComponent<Graphic>(), popup.Open);
                DailyQuestButton driver = questButton.TryGetComponent(out DailyQuestButton d) ? d : questButton.gameObject.AddComponent<DailyQuestButton>();
                Transform redDot = questButton.Find("RedDot");
                if (redDot != null)
                {
                    redDot.gameObject.SetActive(false);
                    if (redDot.TryGetComponent(out Image dotImage))
                        dotImage.raycastTarget = false;
                }
                var driverData = new SerializedObject(driver);
                driverData.FindProperty("redDot").objectReferenceValue = redDot != null ? redDot.gameObject : null;
                driverData.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static DailyQuestBoxView Box(RectTransform panel, string name, float y)
        {
            Image box = Img(panel, name, Load(Art + "box_Dailyquest.png"), Color.white);
            Place(box.rectTransform, new Vector2(0f, y), new Vector2(565f, 150f));
            RectTransform rect = box.rectTransform;
            DailyQuestBoxView view = box.TryGetComponent(out DailyQuestBoxView v) ? v : box.gameObject.AddComponent<DailyQuestBoxView>();

            TMP_Text description = Text(rect, "DescriptionText", "적 25마리 처치하기", new Vector2(-75f, 24f), new Vector2(330f, 46f), 30f,
                Pink, TextAlignmentOptions.Left);

            Image bar = Img(rect, "ProgressBar", null, new Color(0.52f, 0.05f, 0.2f, 1f));
            Place(bar.rectTransform, new Vector2(-78f, -30f), new Vector2(306f, 26f));
            // Plain white sprite so the pink stays pink (a Filled image needs a sprite).
            Image fill = Img(bar.rectTransform, "ProgressFill", AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"), Pink);
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0.28f;
            TMP_Text progress = Text(bar.rectTransform, "ProgressText", "7 / 25", Vector2.zero, new Vector2(300f, 26f), 19f,
                Color.white, TextAlignmentOptions.Center);

            Image icon = Img(rect, "RewardIcon", Load(SoulIcon), Color.white);
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(176f, 14f), new Vector2(72f, 72f));
            TMP_Text amount = Text(rect, "RewardAmountText", "350", new Vector2(176f, -34f), new Vector2(130f, 34f), 26f,
                new Color(0.1f, 0.06f, 0.1f), TextAlignmentOptions.Center);

            // Dim: a dark copy of the box shape over everything (text stays readable).
            Image dim = Img(rect, "DimOverlay", Load(Art + "box_Dailyquest.png"), new Color(0.24f, 0.1f, 0.17f, 0f));
            Stretch(dim.rectTransform);
            dim.enabled = false;
            dim.transform.SetAsLastSibling();

            var so = new SerializedObject(view);
            so.FindProperty("descriptionText").objectReferenceValue = description;
            so.FindProperty("progressFill").objectReferenceValue = fill;
            so.FindProperty("progressText").objectReferenceValue = progress;
            so.FindProperty("rewardIcon").objectReferenceValue = icon;
            so.FindProperty("rewardAmountText").objectReferenceValue = amount;
            so.FindProperty("dimOverlay").objectReferenceValue = dim;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // ---- Debug (development only) ------------------------------------------

        [MenuItem("Deadline 4 Sec/Debug/Daily Quest - Log")]
        public static void LogQuests() => Debug.Log(DailyQuestManager.Report());

        [MenuItem("Deadline 4 Sec/Debug/Daily Quest - Complete All")]
        public static void CompleteAll()
        {
            DailyQuestManager.CompleteAll();
            Debug.Log(DailyQuestManager.Report());
        }

        [MenuItem("Deadline 4 Sec/Debug/Daily Quest - New Quests")]
        public static void NewQuests()
        {
            DailyQuestManager.DeleteSave();
            Debug.Log(DailyQuestManager.Report());
        }

        // ---- Helpers ------------------------------------------------------------

        private static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static Button OnClick(GameObject target, Graphic graphic, UnityAction action)
        {
            Button button = target.TryGetComponent(out Button b) ? b : target.AddComponent<Button>();
            if (button.targetGraphic == null)
                button.targetGraphic = graphic;
            if (graphic != null)
                graphic.raycastTarget = true;
            while (button.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(button.onClick, 0);
            UnityEventTools.AddPersistentListener(button.onClick, action);
            return button;
        }

        private static RectTransform Child(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name, typeof(RectTransform)).transform;
                t.SetParent(parent, false);
                t.gameObject.layer = parent.gameObject.layer;
            }
            return (RectTransform)t;
        }

        private static Image Img(RectTransform parent, string name, Sprite sprite, Color color)
        {
            RectTransform rect = Child(parent, name);
            Image image = rect.TryGetComponent(out Image i) ? i : rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Text(RectTransform parent, string name, string value, Vector2 position, Vector2 size,
            float fontSize, Color color, TextAlignmentOptions alignment)
        {
            RectTransform rect = Child(parent, name);
            TMP_Text text = rect.TryGetComponent(out TMP_Text t) ? t : rect.gameObject.AddComponent<TextMeshProUGUI>();
            GameFont.Apply(text);
            Place(rect, position, size);
            text.text = value;
            text.fontSize = fontSize;
            text.enableAutoSizing = true;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = fontSize * 0.6f;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
