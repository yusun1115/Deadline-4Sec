using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Deadline4Sec.Editor
{
    // Leaderboard setup (leaderboard.md, 2026-10-14):
    //  - Resources/GooglePlay/GooglePlaySettings.asset (created once; paste the
    //    two leaderboard IDs there),
    //  - TitlePanel/LeaderboardPanel (inactive): dim Backdrop + BackButton →
    //    Close, Dialog with DEADLINE MODE / CLASSIC MODE buttons → OpenDeadline /
    //    OpenClassic, each with the local best, and a status line,
    //  - the title's lankingButton On Click → LeaderboardPanel.Open.
    // The open scene is changed in place (left for you to save if it was
    // already dirty); the other gameplay scenes are opened, saved and closed.
    public static class LeaderboardSetup
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/EndlessRun.unity",
            "Assets/Scenes/GrayboxCourse.unity",
            "Assets/Scenes/TestScene.unity",
        };

        private const string SettingsPath = "Assets/Resources/GooglePlay/GooglePlaySettings.asset";
        private const string Generated = "Assets/Art/2D/Generated/";
        private static readonly Color Pink = new Color(1f, 0.32f, 0.62f);
        private static readonly Color Gold = new Color(1f, 0.86f, 0.42f);

        [MenuItem("Deadline 4 Sec/UI/Apply Leaderboard")]
        public static void Apply()
        {
            EnsureSettings();
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

        public static GooglePlaySettings EnsureSettings()
        {
            GooglePlaySettings settings = AssetDatabase.LoadAssetAtPath<GooglePlaySettings>(SettingsPath);
            if (settings != null)
                return settings;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingsPath));
            settings = ScriptableObject.CreateInstance<GooglePlaySettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static void Build(Transform title)
        {
            RectTransform root = Child(title, "LeaderboardPanel");
            Stretch(root);
            root.SetAsLastSibling();
            LeaderboardPanel panel = root.TryGetComponent(out LeaderboardPanel p) ? p : root.gameObject.AddComponent<LeaderboardPanel>();

            // Dim backdrop: blocks the title and closes the panel when tapped.
            Image backdrop = Img(root, "Backdrop", null, new Color(0.03f, 0.01f, 0.05f, 0.88f));
            Stretch(backdrop.rectTransform);
            OnClick(backdrop.gameObject, backdrop, panel.Close, Selectable.Transition.None);

            Image dialog = Img(root, "Dialog", Load(Generated + "ui_panel_gothic.png"), Color.white);
            dialog.type = Image.Type.Sliced;
            dialog.raycastTarget = true; // taps on the dialog do not close it
            // The frame's ornaments take ~150 units at the top and bottom, so the
            // content sits inside ±280 and the heading goes above the frame.
            Place(dialog.rectTransform, new Vector2(0f, -20f), new Vector2(700f, 860f));
            RectTransform box = dialog.rectTransform;

            Text(box, "HeadingText", "LEADERBOARD", new Vector2(0f, 476f), new Vector2(560f, 70f), 54f, Color.white).characterSpacing = 4f;

            TMP_Text deadlineBest = ModeButton(box, "DeadlineButton", "DEADLINE MODE", new Vector2(0f, 150f), Pink, panel.OpenDeadline);
            TMP_Text classicBest = ModeButton(box, "ClassicButton", "CLASSIC MODE", new Vector2(0f, -36f), new Color(1f, 0.7f, 0.85f),
                panel.OpenClassic);

            TMP_Text status = Text(box, "StatusText", "", new Vector2(0f, -200f), new Vector2(480f, 80f), 26f, Color.white);
            status.textWrappingMode = TextWrappingModes.Normal;

            Image back = Img(root, "BackButton", Load(UiIconFactory.Folder + "ui_round_button.png"), Color.white);
            back.preserveAspect = true;
            RectTransform backRect = back.rectTransform;
            backRect.anchorMin = backRect.anchorMax = new Vector2(0f, 1f);
            backRect.pivot = new Vector2(0f, 1f);
            backRect.anchoredPosition = new Vector2(26f, -26f);
            backRect.sizeDelta = new Vector2(118f, 118f);
            Image backIcon = Img(backRect, "Icon", Load(UiIconFactory.Folder + "icon_back.png"), Color.white);
            backIcon.preserveAspect = true;
            Place(backIcon.rectTransform, Vector2.zero, new Vector2(73f, 73f));
            OnClick(back.gameObject, back, panel.Close, Selectable.Transition.ColorTint);

            string[] order = { "Backdrop", "Dialog", "BackButton" };
            for (int i = 0; i < order.Length; i++)
                root.Find(order[i])?.SetSiblingIndex(i);

            var so = new SerializedObject(panel);
            so.FindProperty("dialog").objectReferenceValue = box;
            so.FindProperty("deadlineBestText").objectReferenceValue = deadlineBest;
            so.FindProperty("classicBestText").objectReferenceValue = classicBest;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);

            Transform opener = title.Find("lankingButton");
            if (opener != null)
                OnClick(opener.gameObject, opener.GetComponent<Graphic>(), panel.Open, Selectable.Transition.ColorTint);
        }

        // Gothic button with the mode name and, under it, the local best.
        private static TMP_Text ModeButton(RectTransform parent, string name, string label, Vector2 position, Color accent,
            UnityAction action)
        {
            Image button = Img(parent, name, Load(Generated + "ui_button_gothic.png"), Color.white);
            button.preserveAspect = true;
            Place(button.rectTransform, position, new Vector2(520f, 150f));
            OnClick(button.gameObject, button, action, Selectable.Transition.ColorTint);
            Text(button.rectTransform, "Label", label, new Vector2(0f, 12f), new Vector2(380f, 56f), 42f, accent);
            return Text(button.rectTransform, "BestText", "MY BEST  0", new Vector2(0f, -30f), new Vector2(360f, 32f), 24f, Gold);
        }

        private static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static void OnClick(GameObject target, Graphic graphic, UnityAction action, Selectable.Transition transition)
        {
            Button button = target.TryGetComponent(out Button b) ? b : target.AddComponent<Button>();
            if (button.targetGraphic == null)
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
            Place(rect, position, size);
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
