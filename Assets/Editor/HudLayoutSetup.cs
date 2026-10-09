using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec.Editor
{
    // In-game HUD layout (2026-10-05 request): score, coins and gift boxes in
    // rounded pills stacked top-right (in that order) with small icons; distance,
    // consumable status and the active power-up list are hidden. The existing
    // labels keep their names and references; the pills and icons are siblings
    // placed under them, and HudStats animates the gift pill at runtime.
    public static class HudLayoutSetup
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/EndlessRun.unity",
            "Assets/Scenes/GrayboxCourse.unity",
            "Assets/Scenes/TestScene.unity",
        };

        private const string PillPath = UiIconFactory.Folder + "ui_hud_pill.png";
        private const string GiftIconPath = UiIconFactory.Folder + "icon_gift_small.png";
        private const string CoinIconPath = "Assets/Art/2D/Generated/icon_coin_gold.png";

        private static readonly Vector2 PillSize = new Vector2(196f, 54f);
        private const float Right = -16f, Top = -196f, Gap = 8f;

        [MenuItem("Deadline 4 Sec/UI/Apply In-Game HUD Layout")]
        public static void Apply()
        {
            Sprite pill = EnsurePillSprite();
            Sprite gift = EnsureGiftIcon();
            Sprite coin = AssetDatabase.LoadAssetAtPath<Sprite>(CoinIconPath);
            // The open scene may hold someone's unsaved work: it is changed in
            // place and left for them to save. Other scenes open additively and
            // are saved and closed.
            var open = EditorSceneManager.GetActiveScene();
            bool openWasDirty = open.isDirty;
            foreach (string path in ScenePaths)
            {
                bool isOpen = open.path == path;
                var scene = isOpen ? open : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                Transform hud = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.name == "Canvas")
                        hud = root.transform.Find("GameplayUI");
                if (hud == null)
                {
                    if (!isOpen)
                        EditorSceneManager.CloseScene(scene, true);
                    continue;
                }
                foreach (string hidden in new[] { "DistanceText", "ConsumableStatusText", "ActivePowerUpsText" })
                {
                    Transform t = hud.Find(hidden);
                    if (t != null)
                        t.gameObject.SetActive(false);
                }
                Row(hud, 0, "ScorePill", "ScoreText", pill, null);
                Row(hud, 1, "CoinPill", "RunCoinText", pill, coin);
                Row(hud, 2, "GiftPill", "GiftBoxCountText", pill, gift);
                EditorSceneManager.MarkSceneDirty(scene);
                if (isOpen && openWasDirty)
                    continue;
                EditorSceneManager.SaveScene(scene);
                if (!isOpen)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        // One pill: rounded background, optional icon on the left, the existing
        // label laid over the rest. Background and icon sit just before the label.
        private static void Row(Transform hud, int index, string pillName, string labelName, Sprite pill, Sprite icon)
        {
            Transform label = hud.Find(labelName);
            if (label == null)
                return;
            Vector2 position = new Vector2(Right, Top - index * (PillSize.y + Gap));

            Image background = Child(hud, pillName);
            background.sprite = pill;
            background.type = Image.Type.Sliced;
            background.color = Color.white;
            background.raycastTarget = false;
            TopRight(background.rectTransform, position, PillSize);
            if (background.transform.GetSiblingIndex() > label.GetSiblingIndex())
                background.transform.SetSiblingIndex(label.GetSiblingIndex());

            float textLeft = 16f;
            Transform iconTransform = hud.Find(pillName + "Icon");
            if (icon != null)
            {
                Image image = Child(hud, pillName + "Icon");
                image.sprite = icon;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = Color.white;
                TopRight(image.rectTransform, position + new Vector2(-(PillSize.x - 14f - 32f), -(PillSize.y - 32f) * 0.5f),
                    new Vector2(32f, 32f));
                PlaceAfter(image.transform, background.transform);
                textLeft = 54f;
            }
            else if (iconTransform != null)
            {
                UnityEngine.Object.DestroyImmediate(iconTransform.gameObject);
            }

            // Draw order must end background → icon → label, also on re-runs.
            Transform iconNow = hud.Find(pillName + "Icon");
            PlaceAfter(label, iconNow != null ? iconNow : background.transform);

            RectTransform rect = (RectTransform)label;
            TopRight(rect, position + new Vector2(-12f, 0f), new Vector2(PillSize.x - textLeft - 12f, PillSize.y));
            label.gameObject.SetActive(true);
            if (label.TryGetComponent(out TMP_Text tmp))
            {
                tmp.enableAutoSizing = true;
                tmp.fontSizeMax = 40f;
                tmp.fontSizeMin = 18f;
                tmp.alignment = icon != null ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.color = Color.white;
                tmp.margin = Vector4.zero;
            }
            if (label.TryGetComponent(out Text legacy))
            {
                legacy.fontSize = 40;
                legacy.resizeTextForBestFit = true;
                legacy.resizeTextMinSize = 18;
                legacy.resizeTextMaxSize = 40;
                legacy.alignment = icon != null ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
                legacy.horizontalOverflow = HorizontalWrapMode.Overflow;
                legacy.verticalOverflow = VerticalWrapMode.Overflow;
                legacy.color = Color.white;
            }
        }

        // Moves `item` to directly after `anchor` among their siblings.
        private static void PlaceAfter(Transform item, Transform anchor)
        {
            int target = anchor.GetSiblingIndex();
            item.SetSiblingIndex(item.GetSiblingIndex() < target ? target : target + 1);
        }

        private static Image Child(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name, typeof(RectTransform), typeof(Image)).transform;
                t.SetParent(parent, false);
                t.gameObject.layer = parent.gameObject.layer;
            }
            return t.GetComponent<Image>();
        }

        private static void TopRight(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        // ---------------- generated art ----------------

        // Dark plum rounded rectangle with a light rim, 9-sliced.
        private static Sprite EnsurePillSprite()
        {
            const int w = 96, h = 64, radius = 22;
            Color fill = new Color(0.2f, 0.07f, 0.24f, 0.88f);
            Color rim = new Color(0.86f, 0.62f, 0.92f, 0.95f);
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = RoundedDistance(x + 0.5f, y + 0.5f, w, h, radius);
                float inside = Mathf.Clamp01(-d + 0.5f);
                float border = Mathf.Clamp01(1f - Mathf.Abs(d + 1.6f) / 1.4f);
                Color c = Color.Lerp(fill, rim, border);
                c.a *= inside;
                pixels[y * w + x] = c;
            }
            return Save(PillPath, w, h, pixels, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
        }

        // Purple gift box with a gold ribbon cross and bow.
        private static Sprite EnsureGiftIcon()
        {
            const int s = 64;
            Color box = new Color(0.55f, 0.22f, 0.78f), lid = new Color(0.68f, 0.3f, 0.92f);
            Color gold = new Color(1f, 0.78f, 0.22f), ink = new Color(0.12f, 0.03f, 0.16f);
            var pixels = new Color[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                Color c = Color.clear;
                bool inBox = x >= 10 && x < 54 && y >= 6 && y < 38;
                bool inLid = x >= 7 && x < 57 && y >= 38 && y < 48;
                if (inBox)
                    c = box;
                if (inLid)
                    c = lid;
                if ((inBox || inLid) && Mathf.Abs(x - 31.5f) < 5f)
                    c = gold;
                // bow: two loops above the lid
                float lx = (x - 23f) / 8f, ly = (y - 53f) / 5.5f, rx = (x - 40f) / 8f;
                if (y >= 48 && (lx * lx + ly * ly < 1f || rx * rx + ly * ly < 1f))
                    c = gold;
                // dark outline around anything drawn
                if (c.a == 0f && Near(x, y, s, (px, py) =>
                        (px >= 10 && px < 54 && py >= 6 && py < 38) || (px >= 7 && px < 57 && py >= 38 && py < 48)))
                    c = ink;
                pixels[y * s + x] = c;
            }
            return Save(GiftIconPath, s, s, pixels, Vector4.zero);
        }

        private static bool Near(int x, int y, int s, Func<int, int, bool> solid)
        {
            for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int px = x + dx, py = y + dy;
                if (px >= 0 && py >= 0 && px < s && py < s && solid(px, py))
                    return true;
            }
            return false;
        }

        private static float RoundedDistance(float x, float y, int w, int h, float r)
        {
            float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r);
            float qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static Sprite Save(string path, int w, int h, Color[] pixels, Vector4 border)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
