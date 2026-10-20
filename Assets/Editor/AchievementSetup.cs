using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Deadline4Sec.Editor
{
    // Achievement system scene/asset setup (Achievement.md, 2026-10-14):
    //  - Resources/Achievements/AchievementCatalog.asset (created once from the
    //    built-in defaults; later Inspector edits are kept),
    //  - Canvas/AchievementPopup (top banner, never takes taps),
    //  - TitlePanel/AchievementPanel (list, inactive) with achievementButton
    //    On Click → AchievementListUI.Open and BackButton → Close.
    // Applies to the open scene in place (left for you to save if it was
    // already dirty); the other gameplay scenes are opened, saved and closed.
    public static class AchievementSetup
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/EndlessRun.unity",
            "Assets/Scenes/GrayboxCourse.unity",
            "Assets/Scenes/TestScene.unity",
        };

        private const string CatalogPath = "Assets/Resources/Achievements/AchievementCatalog.asset";
        private const string Generated = "Assets/Art/2D/Generated/";
        private static readonly Color Pink = new Color(1f, 0.32f, 0.62f);

        [MenuItem("Deadline 4 Sec/UI/Apply Achievements")]
        public static void Apply()
        {
            EnsureCatalog();
            var open = EditorSceneManager.GetActiveScene();
            bool openWasDirty = open.isDirty;
            foreach (string path in ScenePaths)
            {
                bool isOpen = open.path == path;
                var scene = isOpen ? open : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                Transform canvas = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.name == "Canvas")
                        canvas = root.transform;
                Transform title = canvas != null ? canvas.Find("TitlePanel") : null;
                if (title != null)
                {
                    BuildPopup(canvas);
                    BuildPanel(title);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!(isOpen && openWasDirty))
                        EditorSceneManager.SaveScene(scene);
                }
                if (!isOpen)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        public static AchievementCatalog EnsureCatalog()
        {
            AchievementCatalog catalog = AssetDatabase.LoadAssetAtPath<AchievementCatalog>(CatalogPath);
            if (catalog != null)
                return catalog;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CatalogPath));
            catalog = ScriptableObject.CreateInstance<AchievementCatalog>();
            catalog.achievements = AchievementCatalog.Defaults();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        // ---- Popup ------------------------------------------------------------

        private static void BuildPopup(Transform canvas)
        {
            RectTransform root = Child(canvas, "AchievementPopup");
            Stretch(root);
            root.SetAsLastSibling();
            if (!root.TryGetComponent(out CanvasGroup group))
                group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            AchievementPopup popup = root.TryGetComponent(out AchievementPopup p) ? p : root.gameObject.AddComponent<AchievementPopup>();

            // Centred below the timer and the score / Soul pills, above the lanes.
            Image body = Img(root, "Body", Load(UiIconFactory.Folder + "ui_hud_pill.png"), new Color(0.07f, 0.03f, 0.1f, 0.93f));
            body.type = Image.Type.Sliced;
            RectTransform bodyRect = body.rectTransform;
            bodyRect.anchorMin = bodyRect.anchorMax = new Vector2(0.5f, 1f);
            bodyRect.pivot = new Vector2(0.5f, 1f);
            bodyRect.anchoredPosition = new Vector2(0f, -318f);
            bodyRect.sizeDelta = new Vector2(580f, 132f);

            Image edge = Img(bodyRect, "Edge", Load(UiIconFactory.Folder + "ui_hud_pill.png"), new Color(1f, 0.32f, 0.62f, 0.9f));
            edge.type = Image.Type.Sliced;
            edge.fillCenter = false;
            Stretch(edge.rectTransform);

            Image icon = Img(bodyRect, "Icon", Load(UiIconFactory.Folder + "icon_achievement.png"), Color.white);
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(-232f, 0f), new Vector2(92f, 92f));

            Text(bodyRect, "Header", "ACHIEVEMENT UNLOCKED", new Vector2(50f, 38f), new Vector2(450f, 30f), 22f, Pink,
                TextAlignmentOptions.Left).characterSpacing = 4f;
            TMP_Text name = Text(bodyRect, "NameText", "공중 콤보!", new Vector2(50f, 2f), new Vector2(450f, 46f), 38f, Color.white,
                TextAlignmentOptions.Left);
            TMP_Text description = Text(bodyRect, "DescriptionText", "공중 적 3마리를 연속으로 처치했습니다.", new Vector2(50f, -38f),
                new Vector2(450f, 32f), 22f, new Color(0.9f, 0.85f, 0.95f), TextAlignmentOptions.Left);

            var so = new SerializedObject(popup);
            so.FindProperty("body").objectReferenceValue = bodyRect;
            so.FindProperty("nameText").objectReferenceValue = name;
            so.FindProperty("descriptionText").objectReferenceValue = description;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- List panel -------------------------------------------------------

        private static void BuildPanel(Transform title)
        {
            RectTransform root = Child(title, "AchievementPanel");
            Stretch(root);
            root.SetAsLastSibling();
            AchievementListUI ui = root.TryGetComponent(out AchievementListUI existing) ? existing : root.gameObject.AddComponent<AchievementListUI>();

            // Opaque backdrop: the title underneath gets no taps while open.
            Image backdrop = Img(root, "Backdrop", null, new Color(0.05f, 0.02f, 0.07f, 1f));
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;

            // Temporary frame (2026-10-18): the gothic panel art's thick side
            // ornaments squeezed the list, so a plain dark panel with a pink
            // outline and accent bars stands in until final art arrives.
            Image frame = Img(root, "Frame", null, new Color(0.11f, 0.05f, 0.15f, 0.94f));
            frame.type = Image.Type.Simple;
            StretchInset(frame.rectTransform, new Vector2(0f, -110f), new Vector2(-40f, -300f));
            Outline outline = frame.TryGetComponent(out Outline o) ? o : frame.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.32f, 0.62f, 0.75f);
            outline.effectDistance = new Vector2(3f, -3f);
            foreach (bool top in new[] { true, false })
            {
                Image bar = Img(frame.rectTransform, top ? "TopAccent" : "BottomAccent", null, Pink);
                RectTransform br = bar.rectTransform;
                br.anchorMin = new Vector2(0.5f, top ? 1f : 0f);
                br.anchorMax = new Vector2(0.5f, top ? 1f : 0f);
                br.pivot = new Vector2(0.5f, 0.5f);
                br.anchoredPosition = Vector2.zero;
                br.sizeDelta = new Vector2(220f, 6f);
                br.localScale = Vector3.one;
            }

            Text(root, "HeadingText", "ACHIEVEMENTS", Vector2.zero, new Vector2(640f, 70f), 50f, Color.white, TextAlignmentOptions.Center);
            TopAnchor((RectTransform)root.Find("HeadingText"), new Vector2(0.5f, 1f), new Vector2(0f, -190f));
            TMP_Text count = Text(root, "CountText", "0 / 0", Vector2.zero, new Vector2(170f, 60f), 38f, new Color(1f, 0.86f, 0.42f),
                TextAlignmentOptions.Right);
            TopAnchor(count.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -85f));
            count.rectTransform.pivot = new Vector2(1f, 0.5f);
            Image countIcon = Img(root, "CountIcon", Load(UiIconFactory.Folder + "icon_achievement.png"), Color.white);
            countIcon.preserveAspect = true;
            TopAnchor(countIcon.rectTransform, new Vector2(1f, 1f), new Vector2(-226f, -85f));
            countIcon.rectTransform.sizeDelta = new Vector2(58f, 58f);

            // Back: round button with the back icon, same as the upgrade screen.
            Image back = Img(root, "BackButton", Load(UiIconFactory.Folder + "ui_round_button.png"), Color.white);
            back.preserveAspect = true;
            back.raycastTarget = true;
            RectTransform backRect = back.rectTransform;
            backRect.anchorMin = backRect.anchorMax = new Vector2(0f, 1f);
            backRect.pivot = new Vector2(0f, 1f);
            backRect.anchoredPosition = new Vector2(26f, -26f);
            backRect.sizeDelta = new Vector2(118f, 118f);
            Image backIcon = Img(backRect, "Icon", Load(UiIconFactory.Folder + "icon_back.png"), Color.white);
            backIcon.preserveAspect = true;
            Place(backIcon.rectTransform, Vector2.zero, new Vector2(73f, 73f));
            OnClick(back.gameObject, back, ui.Close);

            // Scroll list filling the frame (18 px inside its sides, under the heading).
            RectTransform scroll = Child(root, "ListScrollView");
            StretchInset(scroll, new Vector2(0f, -110f), new Vector2(-76f, -336f));
            ScrollRect scrollRect = scroll.TryGetComponent(out ScrollRect sr) ? sr : scroll.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = Child(scroll, "Viewport");
            Stretch(viewport);
            Image mask = viewport.TryGetComponent(out Image vm) ? vm : viewport.gameObject.AddComponent<Image>();
            mask.color = Color.white;
            mask.raycastTarget = true;
            if (!viewport.TryGetComponent(out Mask m))
                m = viewport.gameObject.AddComponent<Mask>();
            m.showMaskGraphic = false;
            RectTransform content = Child(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.TryGetComponent(out VerticalLayoutGroup l) ? l : content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.TryGetComponent(out ContentSizeFitter cf) ? cf : content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            AchievementRow template = BuildRow(content);

            string[] order = { "Backdrop", "Frame", "ListScrollView", "HeadingText", "CountIcon", "CountText", "BackButton" };
            for (int i = 0; i < order.Length; i++)
                root.Find(order[i])?.SetSiblingIndex(i);

            var so = new SerializedObject(ui);
            so.FindProperty("rowTemplate").objectReferenceValue = template;
            so.FindProperty("content").objectReferenceValue = content;
            so.FindProperty("countText").objectReferenceValue = count;
            so.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);

            Transform opener = title.Find("achievementButton");
            if (opener != null)
                OnClick(opener.gameObject, opener.GetComponent<Graphic>(), ui.Open);
        }

        private static AchievementRow BuildRow(RectTransform content)
        {
            RectTransform row = Child(content, "RowTemplate");
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(0f, 1f);
            row.sizeDelta = new Vector2(700f, 128f);
            LayoutElement element = row.TryGetComponent(out LayoutElement le) ? le : row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 128f;
            element.minHeight = 128f;
            AchievementRow component = row.TryGetComponent(out AchievementRow r) ? r : row.gameObject.AddComponent<AchievementRow>();

            Image divider = Img(row, "Divider", null, new Color(1f, 0.3f, 0.6f, 0.35f));
            RectTransform dr = divider.rectTransform;
            dr.anchorMin = new Vector2(0f, 0f);
            dr.anchorMax = new Vector2(1f, 0f);
            dr.pivot = new Vector2(0.5f, 0f);
            dr.anchoredPosition = Vector2.zero;
            dr.sizeDelta = new Vector2(-20f, 3f);

            // Anchored to the row's edges so the row uses the full list width.
            Image icon = Img(row, "StateIcon", Load(UiIconFactory.Folder + "icon_achievement.png"), Color.white);
            icon.preserveAspect = true;
            EdgePlace(icon.rectTransform, 0f, 0f, new Vector2(0f, 0.5f), new Vector2(18f, 4f), new Vector2(82f, 82f));

            TMP_Text name = Text(row, "NameText", "첫 수확", Vector2.zero, Vector2.zero, 30f, Color.white,
                TextAlignmentOptions.Left);
            Span(name.rectTransform, 120f, 190f, 34f, 40f);
            TMP_Text tag = Text(row, "TagText", "DEADLINE", Vector2.zero, Vector2.zero, 18f, Pink,
                TextAlignmentOptions.Right);
            EdgePlace(tag.rectTransform, 1f, 1f, new Vector2(1f, 0.5f), new Vector2(-20f, 36f), new Vector2(160f, 26f));
            tag.characterSpacing = 3f;
            TMP_Text description = Text(row, "DescriptionText", "적을 처음으로 처치했습니다.", Vector2.zero,
                Vector2.zero, 22f, new Color(0.9f, 0.85f, 0.95f), TextAlignmentOptions.TopLeft);
            Span(description.rectTransform, 120f, 20f, -4f, 46f);
            description.textWrappingMode = TextWrappingModes.Normal;
            description.enableAutoSizing = true;
            description.fontSizeMin = 17f;

            Image barBack = Img(row, "ProgressBar", null, new Color(1f, 1f, 1f, 0.14f));
            Span(barBack.rectTransform, 120f, 180f, -44f, 12f);
            Image fill = Img(barBack.rectTransform, "Fill", null, Pink);
            Stretch(fill.rectTransform);
            fill.sprite = Load(UiIconFactory.Folder + "ui_hud_pill.png");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0.35f;
            TMP_Text progress = Text(row, "ProgressText", "35 / 100", Vector2.zero, Vector2.zero, 22f,
                Color.white, TextAlignmentOptions.Right);
            EdgePlace(progress.rectTransform, 1f, 1f, new Vector2(1f, 0.5f), new Vector2(-20f, -44f), new Vector2(150f, 28f));

            var so = new SerializedObject(component);
            so.FindProperty("stateIcon").objectReferenceValue = icon;
            so.FindProperty("nameText").objectReferenceValue = name;
            so.FindProperty("descriptionText").objectReferenceValue = description;
            so.FindProperty("tagText").objectReferenceValue = tag;
            so.FindProperty("progressText").objectReferenceValue = progress;
            so.FindProperty("progressBar").objectReferenceValue = barBack.gameObject;
            so.FindProperty("progressFill").objectReferenceValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo();
            row.SetSiblingIndex(0);
            row.gameObject.SetActive(false);
            return component;
        }

        // ---- Debug (development only; not in any game UI) --------------------

        [MenuItem("Deadline 4 Sec/Debug/Log Achievement Progress")]
        public static void LogProgress() => Debug.Log(AchievementManager.ProgressReport());

        [MenuItem("Deadline 4 Sec/Debug/Reset Achievement Save")]
        public static void ResetAchievements()
        {
            AchievementManager.DeleteSave();
            Debug.Log("Achievement unlocks cleared.");
        }

        [MenuItem("Deadline 4 Sec/Debug/Reset Player Stats")]
        public static void ResetStats()
        {
            PlayerStats.DeleteSave();
            Debug.Log("Player stats cleared.");
        }

        // ---- Helpers ------------------------------------------------------------

        private static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static void OnClick(GameObject target, Graphic graphic, UnityAction action)
        {
            Button button = target.TryGetComponent(out Button b) ? b : target.AddComponent<Button>();
            if (button.targetGraphic == null)
                button.targetGraphic = graphic;
            if (graphic != null)
                graphic.raycastTarget = true;
            while (button.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(button.onClick, 0);
            UnityEventTools.AddPersistentListener(button.onClick, action);
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
            text.overflowMode = TextOverflowModes.Ellipsis;
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

        // Fixed-size element anchored at a row edge (x anchor 0 = left, 1 = right).
        private static void EdgePlace(RectTransform rect, float anchorMinX, float anchorMaxX, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(anchorMinX, 0.5f);
            rect.anchorMax = new Vector2(anchorMaxX, 0.5f);
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        // Stretches across the row between a left and a right margin.
        private static void Span(RectTransform rect, float left, float right, float y, float height)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2((left - right) * 0.5f, y);
            rect.sizeDelta = new Vector2(-(left + right), height);
            rect.localScale = Vector3.one;
        }

        private static void TopAnchor(RectTransform rect, Vector2 anchor, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect) => StretchInset(rect, Vector2.zero, Vector2.zero);

        private static void StretchInset(RectTransform rect, Vector2 position, Vector2 sizeDelta)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;
        }
    }
}
