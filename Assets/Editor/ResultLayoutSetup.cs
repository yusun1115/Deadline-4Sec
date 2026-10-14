using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec.Editor
{
    // Result screen hierarchy (gameover.md, 2026-10-09). Inside the existing
    // gothic ResultPanel (760x1000): big score, a compact three-column run stats
    // row, NEW BEST, quiet personal bests, a run reward line and the buttons
    // (MAIN MENU | RETRY side by side). Existing objects keep their
    // names, references and On Click bindings; new captions and the reward line
    // are added next to them. A black overlay sibling dims the game behind.
    public static class ResultLayoutSetup
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/EndlessRun.unity",
            "Assets/Scenes/GrayboxCourse.unity",
            "Assets/Scenes/TestScene.unity",
        };

        private static readonly Color White = Color.white;
        private static readonly Color Caption = new Color(0.78f, 0.7f, 0.86f);
        private static readonly Color Quiet = new Color(0.86f, 0.82f, 0.9f);
        private static readonly Color Pink = new Color(1f, 0.45f, 0.75f);
        private const float StatsX = 215f;

        [MenuItem("Deadline 4 Sec/UI/Apply Result Screen Layout")]
        public static void Apply()
        {
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
                Transform panel = canvas != null ? canvas.Find("ResultPanel") : null;
                if (panel != null)
                {
                    Layout(canvas, panel);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!(isOpen && openWasDirty))
                        EditorSceneManager.SaveScene(scene);
                }
                if (!isOpen)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void Layout(Transform canvas, Transform panel)
        {
            // Overlay: sibling right behind the panel, full screen.
            Image overlay = Child<Image>(canvas, "ResultOverlay");
            overlay.color = new Color(0f, 0f, 0f, 0.6f);
            overlay.raycastTarget = true; // nothing behind the result takes taps
            Stretch(overlay.rectTransform);
            if (overlay.transform.GetSiblingIndex() > panel.GetSiblingIndex())
                overlay.transform.SetSiblingIndex(panel.GetSiblingIndex()); // stays behind, also on re-runs
            overlay.gameObject.SetActive(false);

            var parts = new Dictionary<string, CanvasGroup>();
            CanvasGroup Text(string name, Vector2 pos, Vector2 size, float fontSize, Color color,
                TextAlignmentOptions align = TextAlignmentOptions.Center, string value = null)
            {
                TMP_Text text = Child<TextMeshProUGUI>(panel, name);
                GameFont.Apply(text);
                Place(text.rectTransform, pos, size);
                text.fontSize = fontSize;
                text.enableAutoSizing = true;
                text.fontSizeMax = fontSize;
                text.fontSizeMin = fontSize * 0.55f;
                text.color = color;
                text.alignment = align;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.raycastTarget = false;
                if (value != null)
                    text.text = value;
                CanvasGroup group = Group(text.gameObject);
                parts[name] = group;
                return group;
            }

            // Header and score - the score number is the biggest thing on the screen.
            Text("GameOverText", new Vector2(0f, 370f), new Vector2(620f, 76f), 56f, new Color(1f, 0.22f, 0.36f), value: "GAME OVER");
            Text("ResultScoreText", new Vector2(0f, 288f), new Vector2(640f, 108f), 96f, White);
            Text("ScoreLabel", new Vector2(0f, 220f), new Vector2(300f, 36f), 24f, Pink, value: "SCORE");

            // Run stats in one compact row: value over caption.
            Text("ResultDistanceText", new Vector2(-StatsX, 160f), new Vector2(220f, 52f), 40f, White);
            Text("BestComboText", new Vector2(0f, 160f), new Vector2(220f, 52f), 40f, White);
            Text("SurvivalTimeText", new Vector2(StatsX, 160f), new Vector2(220f, 52f), 40f, White);
            Text("DistanceLabel", new Vector2(-StatsX, 124f), new Vector2(220f, 30f), 20f, Caption, value: "DISTANCE");
            Text("ComboLabel", new Vector2(0f, 124f), new Vector2(220f, 30f), 20f, Caption, value: "BEST COMBO");
            Text("TimeLabel", new Vector2(StatsX, 124f), new Vector2(220f, 30f), 20f, Caption, value: "TIME");

            // Records.
            CanvasGroup newBest = Text("NewBestText", new Vector2(0f, 76f), new Vector2(600f, 54f), 42f, White, value: "★ NEW BEST! ★");
            TMP_Text newBestText = newBest.GetComponent<TMP_Text>();
            newBestText.enableVertexGradient = true;
            newBestText.colorGradient = new VertexGradient(new Color(1f, 0.95f, 0.55f), new Color(1f, 0.95f, 0.55f), Pink, Pink);
            Text("PersonalBestLabel", new Vector2(0f, 36f), new Vector2(400f, 26f), 18f, Caption, value: "PERSONAL BEST");
            Text("BestScoreLabel", new Vector2(-130f, 10f), new Vector2(240f, 26f), 17f, Caption, value: "SCORE");
            Text("BestDistanceLabel", new Vector2(130f, 10f), new Vector2(240f, 26f), 17f, Caption, value: "DISTANCE");
            Text("BestScoreText", new Vector2(-130f, -16f), new Vector2(240f, 34f), 26f, Quiet);
            Text("BestDistanceText", new Vector2(130f, -16f), new Vector2(240f, 34f), 26f, Quiet);

            // Run reward.
            CanvasGroup divider1 = Divider(panel, "Divider1", -46f);
            Text("RunRewardLabel", new Vector2(0f, -68f), new Vector2(300f, 26f), 18f, Caption, value: "RUN REWARD");
            CanvasGroup coinIcon = Icon(panel, "RewardCoinIcon", "Assets/Art/2D/Generated/icon_coin_gold.png", new Vector2(-165f, -102f));
            Text("RewardCoinText", new Vector2(-50f, -102f), new Vector2(170f, 44f), 32f, White, TextAlignmentOptions.Left, "+0");
            CanvasGroup giftIcon = Icon(panel, "RewardGiftIcon", "Assets/Art/2D/Generated/UI/icon_gift_small.png", new Vector2(65f, -102f));
            Text("RewardGiftText", new Vector2(180f, -102f), new Vector2(170f, 44f), 32f, White, TextAlignmentOptions.Left, "x0");
            CanvasGroup divider2 = Divider(panel, "Divider2", -134f);

            // Two buttons in a row, MAIN MENU | RETRY (RETRY a little larger as the
            // primary action), the same with or without gift boxes. Both already
            // open earned boxes first, so the old CONFIRM/OPEN REWARDS button is
            // switched off (kept for its scene wiring).
            CanvasGroup mainMenu = Button(panel, "MainMenuButton", new Vector2(-150f, -222f), new Vector2(270f, 86f), "MAIN MENU", 30f, true);
            CanvasGroup retry = Button(panel, "RetryButton", new Vector2(150f, -222f), new Vector2(290f, 96f), "RETRY", 40f, true);
            Transform confirm = panel.Find("ConfirmButton");
            if (confirm != null)
                confirm.gameObject.SetActive(false);

            // Draw order follows reading order; overlay stays behind the panel.
            string[] order =
            {
                "GameOverText", "ResultScoreText", "ScoreLabel", "ResultDistanceText", "BestComboText", "SurvivalTimeText",
                "DistanceLabel", "ComboLabel", "TimeLabel", "NewBestText", "PersonalBestLabel", "BestScoreLabel",
                "BestDistanceLabel", "BestScoreText", "BestDistanceText", "Divider1", "RunRewardLabel", "RewardCoinIcon",
                "RewardCoinText", "RewardGiftIcon", "RewardGiftText", "Divider2", "MainMenuButton", "RetryButton", "ConfirmButton",
            };
            for (int i = 0; i < order.Length; i++)
                panel.Find(order[i])?.SetSiblingIndex(i);

            Group(panel.gameObject);
            ResultPresenter presenter = panel.TryGetComponent(out ResultPresenter existing)
                ? existing : panel.gameObject.AddComponent<ResultPresenter>();
            var so = new SerializedObject(presenter);
            so.FindProperty("overlay").objectReferenceValue = overlay;
            so.FindProperty("gameOver").objectReferenceValue = parts["GameOverText"];
            Assign(so, "scoreBlock", parts["ResultScoreText"], parts["ScoreLabel"]);
            so.FindProperty("scoreText").objectReferenceValue = parts["ResultScoreText"].GetComponent<TMP_Text>();
            Assign(so, "stats", parts["ResultDistanceText"], parts["DistanceLabel"], parts["BestComboText"], parts["ComboLabel"],
                parts["SurvivalTimeText"], parts["TimeLabel"]);
            so.FindProperty("newBest").objectReferenceValue = newBest;
            Assign(so, "personalBest", parts["PersonalBestLabel"], parts["BestScoreLabel"], parts["BestDistanceLabel"],
                parts["BestScoreText"], parts["BestDistanceText"]);
            Assign(so, "reward", divider1, parts["RunRewardLabel"], coinIcon, parts["RewardCoinText"], giftIcon,
                parts["RewardGiftText"], divider2);
            so.FindProperty("rewardCoinText").objectReferenceValue = parts["RewardCoinText"].GetComponent<TMP_Text>();
            so.FindProperty("rewardGiftText").objectReferenceValue = parts["RewardGiftText"].GetComponent<TMP_Text>();
            so.FindProperty("mainMenuButton").objectReferenceValue = mainMenu;
            so.FindProperty("retryButton").objectReferenceValue = retry;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static CanvasGroup Button(Transform panel, string name, Vector2 pos, Vector2 size, string label, float fontSize, bool boxed)
        {
            Transform t = panel.Find(name);
            if (t == null)
                return null;
            Place((RectTransform)t, pos, size);
            Image image = t.GetComponent<Image>();
            if (image != null)
            {
                // MAIN MENU becomes a text button: the image stays for the tap area only.
                image.color = boxed ? Color.white : new Color(1f, 1f, 1f, 0f);
                image.raycastTarget = true;
            }
            TMP_Text text = t.Find("Label")?.GetComponent<TMP_Text>();
            if (text != null)
            {
                Stretch(text.rectTransform);
                text.rectTransform.offsetMin = new Vector2(12f, 4f);
                text.rectTransform.offsetMax = new Vector2(-12f, -4f);
                text.text = label;
                text.enableAutoSizing = true;
                text.fontSizeMax = fontSize;
                text.fontSizeMin = fontSize * 0.55f;
                text.color = boxed ? White : Caption;
                text.fontStyle = boxed ? FontStyles.Normal : FontStyles.Underline;
                text.alignment = TextAlignmentOptions.Center;
            }
            return Group(t.gameObject);
        }

        private static CanvasGroup Divider(Transform panel, string name, float y)
        {
            Image line = Child<Image>(panel, name);
            line.color = new Color(Pink.r, Pink.g, Pink.b, 0.45f);
            line.raycastTarget = false;
            Place(line.rectTransform, new Vector2(0f, y), new Vector2(520f, 2f));
            return Group(line.gameObject);
        }

        private static CanvasGroup Icon(Transform panel, string name, string path, Vector2 pos)
        {
            Image icon = Child<Image>(panel, name);
            icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.color = Color.white;
            Place(icon.rectTransform, pos, new Vector2(38f, 38f));
            return Group(icon.gameObject);
        }

        private static void Assign(SerializedObject so, string field, params CanvasGroup[] groups)
        {
            SerializedProperty list = so.FindProperty(field);
            list.arraySize = groups.Length;
            for (int i = 0; i < groups.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = groups[i];
        }

        private static CanvasGroup Group(GameObject go)
        {
            CanvasGroup group = go.TryGetComponent(out CanvasGroup existing) ? existing : go.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            return group;
        }

        private static T Child<T>(Transform parent, string name) where T : Graphic
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name, typeof(RectTransform), typeof(T)).transform;
                t.SetParent(parent, false);
                t.gameObject.layer = parent.gameObject.layer;
            }
            return t.TryGetComponent(out T graphic) ? graphic : t.gameObject.AddComponent<T>();
        }

        private static void Place(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
