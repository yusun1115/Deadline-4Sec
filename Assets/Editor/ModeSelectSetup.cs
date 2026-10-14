using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Deadline4Sec.Editor
{
    // Mode-select popup (2026-10-11 request; replaces the "< DEADLINE >" title
    // selector of 2026-10-10, which this removes): a full-screen
    // TitlePanel/ModeSelectPopup with the Classic (left) and Deadline (right)
    // art split by the deco line, a bottom panel with the mode name, tagline and
    // description, and a SELECT button. Every action is a persistent On Click:
    // ModeSelect_Btn → Open, the arts → ChooseClassic / ChooseDeadline,
    // SELECT → Confirm. The popup starts inactive so the full-screen
    // TitleTapArea keeps the screen centre.
    public static class ModeSelectSetup
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/EndlessRun.unity",
            "Assets/Scenes/GrayboxCourse.unity",
            "Assets/Scenes/TestScene.unity",
        };

        private const string ArtFolder = "Assets/Art/2D/";
        private const float ArtAspect = 941f / 1672f;
        private static readonly Color Pink = new Color(1f, 0.32f, 0.6f);

        [MenuItem("Deadline 4 Sec/UI/Apply Mode Select")]
        public static void Apply()
        {
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

        private static void Build(Transform title)
        {
            Transform old = title.Find("ModeSelect");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            RectTransform root = Child(title, "ModeSelectPopup");
            Stretch(root);
            root.SetAsLastSibling();
            ModeSelectUI ui = root.TryGetComponent(out ModeSelectUI existing) ? existing : root.gameObject.AddComponent<ModeSelectUI>();

            // Opaque backdrop: blocks every tap meant for the title underneath.
            Image backdrop = Img(root, "Backdrop", null, new Color(0.04f, 0.02f, 0.05f, 1f));
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;

            // Stage keeps the 941x1672 art covering the screen at any aspect.
            RectTransform stage = Child(root, "Stage");
            Stretch(stage);
            AspectRatioFitter fitter = stage.TryGetComponent(out AspectRatioFitter f) ? f : stage.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = ArtAspect;

            Image classic = Art(stage, "ClassicArt", "ModeSelect_classic.png", true);
            Image deadline = Art(stage, "DeadlineArt", "ModeSelect_deadline.png", true);
            if (!deadline.TryGetComponent(out ModeSplitHitArea _))
                deadline.gameObject.AddComponent<ModeSplitHitArea>();
            Image line = Art(stage, "DecoLine", "ModeSelect_decoline.png", false);
            OnClick(classic.gameObject, classic, ui.ChooseClassic, Selectable.Transition.None);
            OnClick(deadline.gameObject, deadline, ui.ChooseDeadline, Selectable.Transition.None);
            classic.transform.SetSiblingIndex(0);
            deadline.transform.SetSiblingIndex(1);
            line.transform.SetSiblingIndex(2);

            // Bottom info panel: name (white), tagline (pink), description.
            Image panel = Img(root, "InfoPanel", AssetDatabase.LoadAssetAtPath<Sprite>(UiIconFactory.Folder + "ui_hud_pill.png"),
                new Color(0.07f, 0.04f, 0.09f, 0.94f));
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = true;
            Bottom(panel.rectTransform, new Vector2(0f, 330f), new Vector2(700f, 250f));
            TMP_Text name = Text(panel.rectTransform, "ModeName", "DEADLINE", new Vector2(0f, 72f), new Vector2(620f, 76f), 70f, Color.white);
            TMP_Text tagline = Text(panel.rectTransform, "ModeTagline", "4 Second Survival", new Vector2(0f, 14f), new Vector2(620f, 40f), 34f, Pink);
            TMP_Text description = Text(panel.rectTransform, "ModeDescription", "", new Vector2(0f, -66f), new Vector2(640f, 92f), 26f,
                new Color(0.92f, 0.88f, 0.96f));
            description.textWrappingMode = TextWrappingModes.Normal;
            description.lineSpacing = -6f;

            // SELECT: the gothic button used by the other dialogs.
            Image select = Img(root, "SelectButton", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/2D/Generated/ui_button_gothic.png"),
                Color.white);
            select.preserveAspect = true;
            select.raycastTarget = true;
            Bottom(select.rectTransform, new Vector2(0f, 120f), new Vector2(380f, 120f));
            OnClick(select.gameObject, select, ui.Confirm, Selectable.Transition.ColorTint);
            TMP_Text selectLabel = Text(select.rectTransform, "Label", "SELECT", new Vector2(0f, 2f), new Vector2(300f, 80f), 46f, Color.white);
            selectLabel.characterSpacing = 6f;

            string[] order = { "Backdrop", "Stage", "InfoPanel", "SelectButton" };
            for (int i = 0; i < order.Length; i++)
                root.Find(order[i])?.SetSiblingIndex(i);

            var so = new SerializedObject(ui);
            so.FindProperty("deadlineArt").objectReferenceValue = deadline;
            so.FindProperty("classicArt").objectReferenceValue = classic;
            so.FindProperty("infoPanel").objectReferenceValue = panel.rectTransform;
            so.FindProperty("selectButton").objectReferenceValue = select.rectTransform;
            so.FindProperty("titleText").objectReferenceValue = name;
            so.FindProperty("taglineText").objectReferenceValue = tagline;
            so.FindProperty("descriptionText").objectReferenceValue = description;
            so.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);

            // The title button the user placed (EndlessRun) opens the popup.
            Transform opener = title.Find("ModeSelect_Btn");
            if (opener != null)
                OnClick(opener.gameObject, opener.GetComponent<Graphic>(), ui.Open, Selectable.Transition.ColorTint);
        }

        private static Image Art(RectTransform stage, string name, string file, bool raycast)
        {
            Image image = Img(stage, name, AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + file), Color.white);
            Stretch(image.rectTransform);
            image.preserveAspect = false;
            image.raycastTarget = raycast;
            return image;
        }

        private static void OnClick(GameObject target, Graphic graphic, UnityAction action, Selectable.Transition transition)
        {
            Button button = target.TryGetComponent(out Button b) ? b : target.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.transition = transition;
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
            float fontSize, Color color)
        {
            RectTransform rect = Child(parent, name);
            TMP_Text text = rect.TryGetComponent(out TMP_Text t) ? t : rect.gameObject.AddComponent<TextMeshProUGUI>();
            GameFont.Apply(text);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            text.text = value;
            text.fontSize = fontSize;
            text.enableAutoSizing = true;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = fontSize * 0.6f;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
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

        private static void Bottom(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }
    }
}
