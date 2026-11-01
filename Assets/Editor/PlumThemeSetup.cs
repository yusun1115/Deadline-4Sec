using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec.Editor
{
    // One window style (2026-11-01): every panel / button still drawn with the
    // generated ui_panel_gothic / ui_button_gothic art takes the ranking and
    // achievement look instead —
    //  - panel: dark plum box, pink outline, short pink accent bars top and bottom,
    //  - button: plum box with an accent outline, a left accent stripe and the
    //    label in the accent colour (pink; light pink for back / no / give up).
    // The in-game timer frame has its own art and is not touched.
    // The Upgrade screen also takes the achievement layout: a wide frame and one
    // list scrolling the whole screen, the consumables as its last entry.
    // The open scene is changed in place (left for you to save if it was
    // already dirty); the other gameplay scenes are opened, saved and closed.
    public static class PlumThemeSetup
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/EndlessRun.unity",
            "Assets/Scenes/GrayboxCourse.unity",
            "Assets/Scenes/TestScene.unity",
        };

        private static readonly Color Pink = new Color(1f, 0.32f, 0.62f);
        private static readonly Color LightPink = new Color(1f, 0.7f, 0.85f);
        private static readonly Color PanelColor = new Color(0.11f, 0.05f, 0.15f, 0.96f);
        private static readonly Color ButtonColor = new Color(0.2f, 0.08f, 0.24f, 1f);
        private static readonly string[] SecondaryButtons = { "No", "GiveUp", "Back", "MainMenu" };

        [MenuItem("Deadline 4 Sec/UI/Apply Ranking Style To Windows")]
        public static void Apply()
        {
            var open = EditorSceneManager.GetActiveScene();
            bool openWasDirty = open.isDirty;
            foreach (string path in ScenePaths)
            {
                bool isOpen = open.path == path;
                var scene = isOpen ? open : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                int changed = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.name == "Canvas")
                    {
                        changed += Restyle(root.transform);
                        if (root.transform.Find("UpgradePanel") is Transform upgrade)
                            LayoutUpgrade(upgrade);
                    }
                Debug.Log($"Ranking style: {changed} panels/buttons restyled in {path}");
                EditorSceneManager.MarkSceneDirty(scene);
                if (!(isOpen && openWasDirty))
                    EditorSceneManager.SaveScene(scene);
                if (!isOpen)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static int Restyle(Transform canvas)
        {
            int count = 0;
            foreach (Image image in canvas.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null)
                    continue;
                if (image.sprite.name == "ui_panel_gothic")
                {
                    StylePanel(image);
                    count++;
                }
                else if (image.sprite.name == "ui_button_gothic")
                {
                    StyleButton(image);
                    count++;
                }
            }
            return count;
        }

        private static void StylePanel(Image panel)
        {
            panel.sprite = null;
            panel.type = Image.Type.Simple;
            panel.color = PanelColor;
            Outline(panel, new Color(Pink.r, Pink.g, Pink.b, 0.75f));
            foreach (bool top in new[] { true, false })
            {
                Image bar = Child(panel.transform, top ? "TopAccent" : "BottomAccent");
                bar.color = Pink;
                RectTransform rect = bar.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, top ? 1f : 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(220f, 6f);
            }
        }

        private static void StyleButton(Image button)
        {
            bool secondary = false;
            foreach (string name in SecondaryButtons)
                secondary |= button.name.StartsWith(name);
            Color accent = secondary ? LightPink : Pink;

            button.sprite = null;
            button.type = Image.Type.Simple;
            button.color = ButtonColor;
            Shrink(button.rectTransform);
            Outline(button, new Color(accent.r, accent.g, accent.b, 0.85f));
            Image stripe = Child(button.transform, "Stripe");
            stripe.color = accent;
            RectTransform rect = stripe.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(10f, 0f);
            rect.SetAsFirstSibling();
            foreach (Transform child in button.transform)
                if (child.TryGetComponent(out TMPro.TMP_Text label))
                    label.color = accent;
        }

        // Achievement layout for the Upgrade screen: frame and list fill the
        // screen below the heading, and the consumables are the list's last
        // entry (reached by scrolling, the six upgrades all fit above it).
        private static void LayoutUpgrade(Transform panel)
        {
            if (panel.Find("Frame") is RectTransform frame)
            {
                Stretch(frame, new Vector2(0f, -110f), new Vector2(-40f, -300f));
                if (frame.TryGetComponent(out Image frameImage) && frameImage.sprite == null)
                    frameImage.color = new Color(0.11f, 0.05f, 0.15f, 0.94f);
            }
            if (panel.Find("ItemScrollView") is RectTransform list)
            {
                Stretch(list, new Vector2(0f, -110f), new Vector2(-76f, -336f));
                if (list.TryGetComponent(out ScrollRect scroll))
                {
                    scroll.horizontal = false;
                    scroll.vertical = true;
                    scroll.movementType = ScrollRect.MovementType.Elastic;
                }
            }
            Transform content = panel.Find("ItemScrollView/Viewport/Content");
            if (content == null || !(panel.Find("ConsumableArea") is RectTransform area))
                return;
            area.SetParent(content, false);
            area.SetAsLastSibling();
            LayoutElement layout = area.TryGetComponent(out LayoutElement found) ? found : area.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = false;
            layout.minHeight = layout.preferredHeight = 270f;
            area.anchorMin = new Vector2(0f, 1f);
            area.anchorMax = new Vector2(1f, 1f);
            area.pivot = new Vector2(0.5f, 1f); // its heading, box and rows keep their places inside
        }

        // The gothic art had transparent margins; a flat box of the same rect
        // would touch its neighbours, so it shrinks around its centre.
        public static void Shrink(RectTransform rect)
        {
            Vector2 size = rect.rect.size;
            Vector2 smaller = new Vector2(size.x * 0.92f, size.y * 0.8f);
            Vector2 middle = new Vector2(0.5f, 0.5f) - rect.pivot;
            Vector2 centre = rect.anchoredPosition + Vector2.Scale(middle, size);
            rect.sizeDelta += smaller - size;
            rect.anchoredPosition = centre - Vector2.Scale(middle, smaller);
        }

        private static void Outline(Graphic graphic, Color color)
        {
            Outline outline = graphic.TryGetComponent(out Outline found) ? found : graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(3f, -3f);
        }

        private static Image Child(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            Image image = go.GetComponent<Image>();
            image.sprite = null;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
