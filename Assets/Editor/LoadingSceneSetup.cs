using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec.Editor
{
    // Builds Assets/Scenes/Loading.unity (2026-10-09 request) to match the
    // reference: full-screen key art, "LOADING..." with ruled lines, the
    // bg/fill/frame bar and a random tip below. The scene goes first in Build
    // Settings and loads EndlessRun. Re-running rebuilds the layout but keeps
    // the tips already typed into the LoadingScreen component.
    public static class LoadingSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Loading.unity";
        private const string Art = "Assets/Art/2D/loading/";
        private static readonly Vector2 Reference = new Vector2(1080f, 1920f);

        private static readonly string[] DefaultTips =
        {
            "장애물을 아슬아슬하게 피해보세요!",
            "적을 처치하거나 아슬아슬하게 피하면 타이머가 4초로 돌아갑니다.",
            "콤보가 끊기기 전에 다음 적을 노리세요!",
            "공중의 적은 호밍 킥으로 연달아 처치할 수 있어요.",
            "선물상자는 런이 끝난 뒤 한꺼번에 열 수 있어요.",
            "업그레이드로 아이템 지속 시간을 늘려보세요.",
        };

        [MenuItem("Deadline 4 Sec/UI/Build Loading Scene")]
        public static void Apply()
        {
            foreach (string name in new[] { "loadingscreen", "loading_bg", "loading_fill", "loading_Frame" })
                ImportSprite(Art + name + ".png", name == "loadingscreen");

            string[] keptTips = ReadExistingTips();
            var previous = EditorSceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene); // new objects go into the active scene

            // Untagged on purpose: the game scene activates underneath this one and
            // its scripts must find their own camera through Camera.main.
            var cameraObject = new GameObject("Loading Camera", typeof(Camera));
            Camera view = cameraObject.GetComponent<Camera>();
            view.depth = -10f;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = Color.black;
            view.cullingMask = 0;

            var canvasObject = new GameObject("LoadingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(CanvasGroup));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // above the game's UI while it loads underneath
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasObject.transform;

            // Key art always covers the screen.
            Image art = Img(root, "Background", Sprite("loadingscreen"));
            Stretch(art.rectTransform);
            AspectRatioFitter fitter = art.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 941f / 1672f;

            // Bottom band (fractions measured from the reference image).
            Color ink = new Color(0.92f, 0.88f, 0.96f);
            TMP_Text loading = Label(root, "LoadingText", "LOADING...", 0.118f, new Vector2(400f, 60f), 32f, ink);
            loading.characterSpacing = 24f;
            Rule(root, "LoadingRuleLeft", 0.118f, -420f, -205f, ink);
            Rule(root, "LoadingRuleRight", 0.118f, 205f, 420f, ink);

            float barWidth = Reference.x * 0.79f;
            Vector2 barSize = new Vector2(barWidth, barWidth * 41f / 712f);
            var bar = (RectTransform)new GameObject("LoadingBar", typeof(RectTransform)).transform;
            bar.SetParent(root, false);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.088f);
            bar.sizeDelta = barSize;
            Image back = Img(bar, "Back", Sprite("loading_bg"));
            Stretch(back.rectTransform);
            Image fill = Img(bar, "Fill", Sprite("loading_fill"));
            Stretch(fill.rectTransform);
            float scale = barWidth / 712f;
            fill.rectTransform.offsetMin = new Vector2(4f * scale, 5f * scale);
            fill.rectTransform.offsetMax = new Vector2(-4f * scale, -5f * scale);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            Image frame = Img(bar, "Frame", Sprite("loading_Frame"));
            Stretch(frame.rectTransform);

            TMP_Text tip = Label(root, "TipText", DefaultTips[0], 0.049f, new Vector2(820f, 70f), 34f, ink);
            tip.enableAutoSizing = true;
            tip.fontSizeMin = 22f;
            tip.fontSizeMax = 34f;
            Rule(root, "TipRuleLeft", 0.049f, -470f, -420f, ink);
            Rule(root, "TipRuleRight", 0.049f, 420f, 470f, ink);

            LoadingScreen screen = canvasObject.AddComponent<LoadingScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("loadingText").objectReferenceValue = loading;
            so.FindProperty("tipText").objectReferenceValue = tip;
            so.FindProperty("sceneToLoad").stringValue = "EndlessRun";
            string[] tips = keptTips != null && keptTips.Length > 0 ? keptTips : DefaultTips;
            SerializedProperty list = so.FindProperty("tips");
            list.arraySize = tips.Length;
            for (int i = 0; i < tips.Length; i++)
                list.GetArrayElementAtIndex(i).stringValue = tips[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (previous.IsValid())
                EditorSceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
            PutFirstInBuild();
        }

        // Tips typed into an existing Loading scene survive a rebuild.
        private static string[] ReadExistingTips()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                return null;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            string[] tips = null;
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                LoadingScreen screen = go.GetComponentInChildren<LoadingScreen>(true);
                if (screen == null)
                    continue;
                SerializedProperty list = new SerializedObject(screen).FindProperty("tips");
                tips = new string[list.arraySize];
                for (int i = 0; i < tips.Length; i++)
                    tips[i] = list.GetArrayElementAtIndex(i).stringValue;
            }
            EditorSceneManager.CloseScene(scene, true);
            return tips;
        }

        private static void PutFirstInBuild()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            scenes.AddRange(EditorBuildSettings.scenes.Where(s => s.path != ScenePath));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");

        private static Image Img(Transform parent, string name, Sprite sprite)
        {
            Image image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Label(Transform parent, string name, string text, float anchorY, Vector2 size, float fontSize, Color color)
        {
            TextMeshProUGUI label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(parent, false);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, anchorY);
            label.rectTransform.sizeDelta = size;
            GameFont.Apply(label);
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        // Thin rule from x0 to x1 (canvas units from centre) with a small diamond at its inner end.
        private static void Rule(Transform parent, string name, float anchorY, float x0, float x1, Color color)
        {
            Image line = Img(parent, name, null);
            line.color = new Color(color.r, color.g, color.b, 0.45f);
            RectTransform rect = line.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, anchorY);
            rect.sizeDelta = new Vector2(Mathf.Abs(x1 - x0), 2f);
            rect.anchoredPosition = new Vector2((x0 + x1) * 0.5f, 0f);
            Image diamond = Img(rect, "Diamond", null);
            diamond.color = new Color(color.r, color.g, color.b, 0.8f);
            RectTransform d = diamond.rectTransform;
            bool left = x0 < 0f;
            d.anchorMin = d.anchorMax = new Vector2(left ? 1f : 0f, 0.5f);
            d.anchoredPosition = new Vector2(left ? 8f : -8f, 0f);
            d.sizeDelta = new Vector2(8f, 8f);
            d.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void ImportSprite(string path, bool keyArt)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                return;
            bool changed = importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled ||
                           (keyArt && importer.maxTextureSize < 2048);
            if (!changed)
                return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            if (keyArt)
                importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
    }
}
