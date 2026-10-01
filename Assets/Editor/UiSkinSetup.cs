using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec.Editor
{
    // Skins the saved scene UI with the gothic frame sprites and fixes the HUD
    // layout. Only Image sprites/colors and RectTransforms change; object names,
    // hierarchy paths and On Click wiring stay as the menu tests expect.
    public static class UiSkinSetup
    {
        private const string Folder = "Assets/Art/2D/Generated/";
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/EndlessRun.unity",
            "Assets/Scenes/GrayboxCourse.unity",
            "Assets/Scenes/TestScene.unity",
        };

        private static readonly Color Backdrop = new Color(0.03f, 0.015f, 0.05f, 0.82f);

        [MenuItem("Deadline 4 Sec/Art/Apply Gothic UI Skin To Scenes")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the open scene before skinning the UI.");
            Sprite button = ImportSprite("ui_button_gothic.png", new Vector4(330, 160, 330, 160), 1024);
            Sprite panel = ImportSprite("ui_panel_gothic.png", new Vector4(200, 250, 200, 250), 1024);
            Sprite timer = ImportSprite("hud_timer_frame.png", new Vector4(430, 160, 430, 160), 1024);
            ImportSprite("icon_coin.png", Vector4.zero, 256);
            foreach (string name in new[] { "powerup_combo_seal.png", "powerup_freeze_clock.png",
                         "powerup_reaper_rush.png", "powerup_soul_amplifier.png",
                         "powerup_soul_magnet.png", "powerup_time_heart.png" })
                ImportSprite(name, Vector4.zero, 256);

            string original = EditorSceneManager.GetActiveScene().path;
            foreach (string path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path);
                GameObject canvas = GameObject.Find("Canvas");
                if (canvas == null)
                    continue;
                SkinButtons(canvas.transform, button);
                SpaceButtons(canvas.transform);
                SkinPanels(canvas.transform, panel);
                LayoutHud(canvas.transform, timer);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (!string.IsNullOrEmpty(original))
                EditorSceneManager.OpenScene(original);
        }

        private static Sprite ImportSprite(string file, Vector4 border, int maxSize)
        {
            string path = Folder + file;
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
                return null;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            // Borders are authored in source pixels; scale them to the import size.
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            importer.GetSourceTextureWidthAndHeight(out int w, out int h);
            float scale = Mathf.Min(1f, maxSize / (float)Mathf.Max(w, h));
            importer.spriteBorder = border * scale;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void SkinButtons(Transform canvas, Sprite sprite)
        {
            foreach (Button b in canvas.GetComponentsInChildren<Button>(true))
            {
                Image image = b.GetComponent<Image>();
                if (image == null)
                    continue;
                // Full-screen invisible tap areas stay invisible.
                RectTransform rt = (RectTransform)b.transform;
                bool fullScreen = rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one;
                if (fullScreen || b.name == "TitleTapArea")
                    continue;
                // Shop rows keep their own purchase styling.
                if (b.GetComponentInParent<ScrollRect>(true) != null)
                    continue;
                // Round icon buttons and the Upgrade/Skin screens are owned by MenuLayoutSetup.
                if (b.transform.Find("Icon") != null)
                    continue;
                Transform upgradePanel = canvas.Find("UpgradePanel"), skinPanel = canvas.Find("SkinPanel");
                if ((upgradePanel != null && b.transform.IsChildOf(upgradePanel)) ||
                    (skinPanel != null && b.transform.IsChildOf(skinPanel)))
                    continue;
                // Show the whole spiked frame at its native 3:1 aspect; 9-slicing it
                // down to button height left only a thin band.
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.color = Color.white;
                ColorBlock colors = b.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 0.85f, 0.95f);
                colors.pressedColor = new Color(1f, 0.45f, 0.7f);
                colors.selectedColor = Color.white;
                colors.fadeDuration = 0.06f;
                b.colors = colors;
                // Slightly taller so the spiked frame reads at phone size.
                if (rt.sizeDelta.y < 110f && rt.sizeDelta.y > 0f)
                    rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 320f), 110f);
                EditorUtility.SetDirty(image);
            }
        }

        // Taller framed buttons need more vertical spacing than the flat originals.
        private static void SpaceButtons(Transform canvas)
        {
            (string path, Vector2 pos)[] layout =
            {
                ("ResultPanel/RetryButton", new Vector2(0f, -215f)),
                ("ResultPanel/MainMenuButton", new Vector2(-150f, -345f)),
                ("ResultPanel/ConfirmButton", new Vector2(150f, -345f)),
                ("SettingsPanel/SoundButton", new Vector2(0f, 90f)),
                ("SettingsPanel/VibrationButton", new Vector2(0f, -30f)),
                ("SettingsPanel/TutorialButton", new Vector2(0f, -150f)),
                ("SettingsPanel/BackButton", new Vector2(0f, -280f)),
                ("RevivePanel/CouponButton", new Vector2(0f, -45f)),
                ("RevivePanel/AdButton", new Vector2(0f, -165f)),
                ("RevivePanel/GiveUpButton", new Vector2(0f, -285f)),
            };
            foreach (var (path, pos) in layout)
            {
                Transform t = canvas.Find(path);
                if (t != null)
                    ((RectTransform)t).anchoredPosition = pos;
            }
            // Retry is the primary action; the two secondary buttons sit inside the frame.
            Transform result = canvas.Find("ResultPanel/RetryButton");
            if (result != null)
                ((RectTransform)result).sizeDelta = new Vector2(420f, 140f);
            foreach (string secondary in new[] { "ResultPanel/MainMenuButton", "ResultPanel/ConfirmButton" })
            {
                result = canvas.Find(secondary);
                if (result != null)
                    ((RectTransform)result).sizeDelta = new Vector2(285f, 95f);
            }
        }

        private static void SkinPanels(Transform canvas, Sprite frame)
        {
            Transform result = canvas.Find("ResultPanel");
            if (result != null)
                SetFrame(result.GetComponent<Image>(), frame, new Vector2(760f, 1000f));
            Transform dialog = canvas.Find("TutorialSkipConfirmation/Dialog");
            if (dialog != null)
                SetFrame(dialog.GetComponent<Image>(), frame, new Vector2(740f, 460f));

            // GiftOpeningPanel has its own sunburst stage (GiftChestSetup), no frame.
            foreach (string name in new[] { "SettingsPanel", "RevivePanel" })
            {
                Transform panel = canvas.Find(name);
                if (panel == null)
                    continue;
                Image backdrop = panel.GetComponent<Image>();
                if (backdrop != null)
                    backdrop.color = Backdrop;
                Transform existing = panel.Find("Frame");
                Image image = existing != null ? existing.GetComponent<Image>() : null;
                if (image == null)
                {
                    GameObject go = new GameObject("Frame", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(panel, false);
                    go.transform.SetAsFirstSibling();
                    image = go.GetComponent<Image>();
                }
                image.raycastTarget = false;
                RectTransform rt = image.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = name == "UpgradePanel" ? new Vector2(0f, 0f) : new Vector2(0f, -20f);
                SetFrame(image, frame, name == "UpgradePanel" ? new Vector2(800f, 760f) : new Vector2(720f, 720f));
            }
        }

        private static void SetFrame(Image image, Sprite frame, Vector2 size)
        {
            if (image == null)
                return;
            image.sprite = frame;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            image.pixelsPerUnitMultiplier = 2.2f;
            RectTransform rt = image.rectTransform;
            if (rt.anchorMin == rt.anchorMax)
                rt.sizeDelta = size;
            EditorUtility.SetDirty(image);
        }

        private static void LayoutHud(Transform canvas, Sprite timerFrame)
        {
            Transform hud = canvas.Find("GameplayUI");
            if (hud == null)
                return;
            Transform timer = hud.Find("SurvivalTimer");
            if (timer != null)
            {
                Transform existing = hud.Find("TimerFrame");
                Image frame = existing != null ? existing.GetComponent<Image>() : null;
                if (frame == null)
                {
                    GameObject go = new GameObject("TimerFrame", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(hud, false);
                    frame = go.GetComponent<Image>();
                }
                // Frame first, timer text right after it, so re-runs keep the text on top.
                frame.transform.SetAsFirstSibling();
                timer.SetSiblingIndex(1);
                frame.sprite = timerFrame;
                frame.type = Image.Type.Simple;
                frame.preserveAspect = true;
                frame.raycastTarget = false;
                RectTransform rt = frame.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -10f);
                rt.sizeDelta = new Vector2(500f, 168f);
            }

            Place(hud, "ScoreText", new Vector2(0f, 1f), new Vector2(28f, -190f), new Vector2(360f, 50f), 34, TextAnchor.MiddleLeft);
            Place(hud, "DistanceText", new Vector2(0f, 1f), new Vector2(28f, -236f), new Vector2(360f, 40f), 26, TextAnchor.MiddleLeft);
            Place(hud, "ComboText", new Vector2(1f, 1f), new Vector2(-28f, -190f), new Vector2(300f, 60f), 40, TextAnchor.MiddleRight);
            Place(hud, "GiftBoxCountText", new Vector2(0f, 1f), new Vector2(28f, -276f), new Vector2(300f, 36f), 22, TextAnchor.MiddleLeft);
            Place(hud, "ConsumableStatusText", new Vector2(0f, 1f), new Vector2(28f, -310f), new Vector2(300f, 36f), 22, TextAnchor.MiddleLeft);
            Place(hud, "RunCoinText", new Vector2(1f, 1f), new Vector2(-28f, -244f), new Vector2(260f, 44f), 28, TextAnchor.MiddleRight);
            Place(hud, "ActivePowerUpsText", new Vector2(1f, 1f), new Vector2(-28f, -290f), new Vector2(320f, 180f), 22, TextAnchor.UpperRight);
        }

        // Each power-up upgrade row gets its icon at the left; texts shift right.
        private static void AddUpgradeIcons(Transform canvas)
        {
            Transform content = canvas.Find("UpgradePanel/ItemScrollView/Viewport/Content");
            if (content == null)
                return;
            (string row, string icon)[] rows =
            {
                ("FreezeClockRow", "powerup_freeze_clock"), ("SoulAmplifierRow", "powerup_soul_amplifier"),
                ("ReaperRushRow", "powerup_reaper_rush"), ("SoulMagnetRow", "powerup_soul_magnet"),
                ("ComboSealRow", "powerup_combo_seal"), ("TimeHeartRow", "powerup_time_heart"),
            };
            foreach (var (rowName, iconName) in rows)
            {
                Transform row = content.Find(rowName);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + iconName + ".png");
                if (row == null || sprite == null)
                    continue;
                Transform existing = row.Find("Icon");
                Image icon = existing != null ? existing.GetComponent<Image>() : null;
                if (icon == null)
                {
                    GameObject go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(row, false);
                    icon = go.GetComponent<Image>();
                }
                icon.sprite = sprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                RectTransform rt = icon.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(-315f, 0f);
                rt.sizeDelta = new Vector2(82f, 82f);
                foreach (string textName in new[] { "NameLevelText", "EffectText" })
                {
                    RectTransform text = row.Find(textName) as RectTransform;
                    if (text == null)
                        continue;
                    text.anchoredPosition = new Vector2(-118f, text.anchoredPosition.y);
                    text.sizeDelta = new Vector2(300f, text.sizeDelta.y);
                }
                EditorUtility.SetDirty(row.gameObject);
            }
        }

        private static void Place(Transform hud, string name, Vector2 anchor, Vector2 position, Vector2 size,
            int fontSize, TextAnchor alignment)
        {
            Transform t = hud.Find(name);
            if (t == null)
                return;
            RectTransform rt = (RectTransform)t;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(anchor.x, 1f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            Text legacy = t.GetComponent<Text>();
            if (legacy != null)
            {
                legacy.fontSize = fontSize;
                legacy.alignment = alignment;
                EditorUtility.SetDirty(legacy);
            }
            TMPro.TMP_Text tmp = t.GetComponent<TMPro.TMP_Text>();
            if (tmp != null)
            {
                tmp.fontSize = fontSize;
                tmp.alignment = alignment switch
                {
                    TextAnchor.MiddleRight => TMPro.TextAlignmentOptions.Right,
                    TextAnchor.UpperRight => TMPro.TextAlignmentOptions.TopRight,
                    _ => TMPro.TextAlignmentOptions.Left,
                };
                EditorUtility.SetDirty(tmp);
            }
        }
    }
}
