using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec.Editor
{
    // Gift box opening screen (2026-10-02 request): a 3D chest over a rotating
    // sunburst that bursts open on tap and pops the reward icon out
    // (Subway Surfers mystery-box style). Existing texts and buttons keep their
    // names and GameExtrasUI / On Click wiring; only their layout changes.
    public static class GiftChestSetup
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/EndlessRun.unity",
            "Assets/Scenes/GrayboxCourse.unity",
            "Assets/Scenes/TestScene.unity",
        };

        private const string Generated = "Assets/Art/2D/Generated/";

        [MenuItem("Deadline 4 Sec/UI/Apply Gift Chest Opening")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the open scene before changing the gift screen.");
            string original = EditorSceneManager.GetActiveScene().path;
            foreach (string path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path);
                Transform canvas = GameObject.Find("Canvas")?.transform;
                Transform panel = canvas != null ? canvas.Find("GiftOpeningPanel") : null;
                if (panel == null)
                    continue;
                Layout(panel);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (!string.IsNullOrEmpty(original))
                EditorSceneManager.OpenScene(original);
        }

        private static void Layout(Transform panel)
        {
            foreach (string hidden in new[] { "Frame", "GiftBoxArt" })
            {
                Transform t = panel.Find(hidden);
                if (t != null)
                    t.gameObject.SetActive(false);
            }

            Image sunburst = Child<Image>(panel, "Sunburst");
            Stretch(sunburst.rectTransform);
            sunburst.color = Color.white;
            sunburst.raycastTarget = false;
            sunburst.transform.SetAsFirstSibling();

            // The canvas scales from an 800-wide reference, so the stage is laid
            // out in screen fractions: a square chest view between 22% and 84%
            // of the height, title on top, reward under the chest, prompt at the bottom.
            RawImage view = Child<RawImage>(panel, "ChestView");
            RectTransform viewRect = view.rectTransform;
            viewRect.anchorMin = new Vector2(0.5f, 0.22f);
            viewRect.anchorMax = new Vector2(0.5f, 0.84f);
            viewRect.pivot = new Vector2(0.5f, 0.5f);
            viewRect.anchoredPosition = Vector2.zero;
            viewRect.sizeDelta = Vector2.zero;
            AspectRatioFitter fitter = view.TryGetComponent(out AspectRatioFitter f) ? f : view.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            fitter.aspectRatio = 1f;
            view.raycastTarget = false;
            view.transform.SetSiblingIndex(1);

            Image icon = Child<Image>(view.transform, "RewardIcon");
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0.34f, 0.34f);
            iconRect.anchorMax = new Vector2(0.66f, 0.66f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = Vector2.zero;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = false;

            TMP_Text progress = Text(panel, "ProgressText", new Vector2(0.5f, 0.92f), new Vector2(720f, 80f), 46f);
            TMP_Text reward = Text(panel, "RewardText", new Vector2(0.5f, 0.17f), new Vector2(740f, 90f), 54f);
            if (reward != null)
                reward.color = new Color(1f, 0.86f, 0.35f);
            if (progress != null)
                progress.color = Color.white;

            Transform open = panel.Find("OpenButton");
            if (open != null)
            {
                Stretch((RectTransform)open);
                open.SetAsLastSibling();
                TMP_Text label = open.Find("Label")?.GetComponent<TMP_Text>();
                if (label != null)
                {
                    Place(label.rectTransform, new Vector2(0.5f, 0.07f), new Vector2(600f, 70f));
                    label.fontSize = 40f;
                }
            }
            Transform confirm = panel.Find("ConfirmButton");
            if (confirm != null)
            {
                Place((RectTransform)confirm, new Vector2(0.5f, 0.07f), new Vector2(340f, 84f));
                confirm.SetAsLastSibling();
            }

            GiftChestPresenter presenter = panel.TryGetComponent(out GiftChestPresenter existing)
                ? existing : panel.gameObject.AddComponent<GiftChestPresenter>();
            var so = new SerializedObject(presenter);
            so.FindProperty("chestView").objectReferenceValue = view;
            so.FindProperty("rewardIcon").objectReferenceValue = icon;
            so.FindProperty("sunburst").objectReferenceValue = sunburst;
            so.FindProperty("rewardLabel").objectReferenceValue = reward != null ? reward.rectTransform : null;
            so.FindProperty("coinIcon").objectReferenceValue = Sprite("icon_coin_gold");
            so.FindProperty("shieldIcon").objectReferenceValue = Sprite("icon_shield");
            so.FindProperty("dashIcon").objectReferenceValue = Sprite("icon_dash");
            so.FindProperty("couponIcon").objectReferenceValue = Sprite("powerup_time_heart");
            so.FindProperty("skinCurrencyIcon").objectReferenceValue = Sprite("UI/icon_skin");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Generated + name + ".png");

        private static T Child<T>(Transform parent, string name) where T : Graphic
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name, typeof(RectTransform), typeof(T)).transform;
                t.SetParent(parent, false);
            }
            t.gameObject.layer = parent.gameObject.layer;
            return t.TryGetComponent(out T graphic) ? graphic : t.gameObject.AddComponent<T>();
        }

        private static TMP_Text Text(Transform panel, string name, Vector2 anchor, Vector2 size, float fontSize)
        {
            TMP_Text text = panel.Find(name)?.GetComponent<TMP_Text>();
            if (text == null)
                return null;
            Place(text.rectTransform, anchor, size);
            text.fontSize = fontSize;
            text.enableAutoSizing = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // Anchored at a screen fraction, sized in canvas units.
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }
    }
}
