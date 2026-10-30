using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Deadline4Sec.Editor
{
    // Skin shop polish (2026-10-30), applied to the existing SkinPanel:
    //  - Soul icon left of the balance (TextLeadingIcon),
    //  - cards 290 tall: weapon icon, name, prices as icon + number (Soul and
    //    the skin currency "have / need"), pill action button, preview frame,
    //    and the card body as a preview button (SkinShopRow.Preview),
    //  - "스킨 미리보기 입니다." tag and the 예 / 아니오 purchase dialog
    //    (SkinShopUI). State colours, card themes and the 3D shop stage are
    //    runtime (SkinShopRow, SkinCardTheme, SkinShopStage).
    // The open scene is changed in place (left for you to save if it was
    // already dirty); the other gameplay scenes are opened, saved and closed.
    public static class SkinShopSetup
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/EndlessRun.unity",
            "Assets/Scenes/GrayboxCourse.unity",
            "Assets/Scenes/TestScene.unity",
        };

        private const string Generated = "Assets/Art/2D/Generated/";
        private static readonly Color Pink = new Color(1f, 0.32f, 0.62f);

        [MenuItem("Deadline 4 Sec/UI/Apply Skin Shop")]
        public static void Apply()
        {
            var open = EditorSceneManager.GetActiveScene();
            bool openWasDirty = open.isDirty;
            foreach (string path in ScenePaths)
            {
                bool isOpen = open.path == path;
                var scene = isOpen ? open : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                Transform panel = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.name == "Canvas")
                        panel = root.transform.Find("SkinPanel");
                if (panel != null)
                {
                    CurrencyShopPanel currencyShop = null;
                    foreach (GameObject root in scene.GetRootGameObjects())
                        if (currencyShop == null)
                            currencyShop = root.GetComponentInChildren<CurrencyShopPanel>(true);
                    Build(panel, currencyShop);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!(isOpen && openWasDirty))
                        EditorSceneManager.SaveScene(scene);
                }
                if (!isOpen)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void Build(Transform panel, CurrencyShopPanel currencyShop)
        {
            bool wasActive = panel.gameObject.activeSelf;
            SkinShopUI ui = panel.TryGetComponent(out SkinShopUI existingUi) ? existingUi : panel.gameObject.AddComponent<SkinShopUI>();
            GameExtrasCatalog catalog = AssetDatabase.LoadAssetAtPath<GameExtrasCatalog>("Assets/Resources/GameExtras/GameExtrasCatalog.asset");

            // Soul icon left of the balance.
            TMP_Text coin = panel.Find("CoinBalanceText")?.GetComponent<TMP_Text>();
            if (coin != null)
            {
                Image soul = Img(panel, "SoulIcon", Load(Generated + "icon_coin_gold.png"), Color.white);
                soul.preserveAspect = true;
                RectTransform sr = soul.rectTransform;
                sr.anchorMin = sr.anchorMax = new Vector2(1f, 1f);
                sr.pivot = new Vector2(1f, 0.5f);
                sr.anchoredPosition = new Vector2(-190f, -50f);
                sr.sizeDelta = new Vector2(46f, 46f);
                sr.SetSiblingIndex(coin.transform.GetSiblingIndex() + 1);
                TextLeadingIcon lead = soul.TryGetComponent(out TextLeadingIcon existingLead) ? existingLead : soul.gameObject.AddComponent<TextLeadingIcon>();
                var leadData = new SerializedObject(lead);
                leadData.FindProperty("text").objectReferenceValue = coin;
                leadData.FindProperty("gap").floatValue = 6f;
                leadData.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (SkinShopRow row in panel.GetComponentsInChildren<SkinShopRow>(true))
                BuildCard(row, catalog);

            // No header over the card column (2026-10-30).
            if (panel.Find("HeadingText") is Transform heading)
            {
                if (heading.TryGetComponent(out TMP_Text headingText))
                    headingText.text = "";
                heading.gameObject.SetActive(false);
            }

            // Preview tag, centred over the character (left of the card column).
            Image tag = Img(panel, "PreviewTag", Load(UiIconFactory.Folder + "ui_hud_pill.png"), new Color(0.07f, 0.03f, 0.1f, 0.85f));
            tag.type = Image.Type.Sliced;
            RectTransform tr = tag.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = new Vector2(-140f, -160f);
            tr.sizeDelta = new Vector2(330f, 50f);
            TMP_Text tagText = Text(tr, "Label", "스킨 미리보기 입니다.", Vector2.zero, new Vector2(310f, 44f), 24f, new Color(1f, 0.86f, 0.95f));
            tagText.characterSpacing = 1f;
            tag.gameObject.SetActive(false);

            // Purchase dialog.
            RectTransform dialog = Child(panel, "PurchaseDialog");
            Stretch(dialog);
            Image dim = Img(dialog, "Backdrop", null, new Color(0f, 0f, 0f, 0.62f));
            Stretch(dim.rectTransform);
            OnClick(dim.gameObject, dim, ui.ConfirmNo, Selectable.Transition.None);
            Image box = Img(dialog, "Box", null, new Color(0.11f, 0.05f, 0.15f, 0.97f));
            box.raycastTarget = true;
            Place(box.rectTransform, Vector2.zero, new Vector2(560f, 360f));
            Outline outline = box.TryGetComponent(out Outline existingOutline) ? existingOutline : box.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Pink.r, Pink.g, Pink.b, 0.8f);
            outline.effectDistance = new Vector2(3f, -3f);
            foreach (bool top in new[] { true, false })
                Place(Img(box.rectTransform, top ? "TopAccent" : "BottomAccent", null, Pink).rectTransform,
                    new Vector2(0f, top ? 180f : -180f), new Vector2(200f, 6f));
            TMP_Text title = Text(box.rectTransform, "TitleText", "STARLIGHT", new Vector2(0f, 112f), new Vector2(480f, 56f), 40f, Color.white);
            TMP_Text message = Text(box.rectTransform, "MessageText", "구매하시겠습니까?", new Vector2(0f, 22f), new Vector2(490f, 110f), 30f,
                new Color(0.93f, 0.87f, 0.98f));
            message.textWrappingMode = TextWrappingModes.Normal;
            TMP_Text yes = DialogButton(box.rectTransform, "YesButton", "예", new Vector2(115f, -112f), new Color(1f, 0.22f, 0.52f), ui.ConfirmYes);
            DialogButton(box.rectTransform, "NoButton", "아니오", new Vector2(-115f, -112f), new Color(0.32f, 0.26f, 0.4f), ui.ConfirmNo);
            dialog.SetAsLastSibling();
            dialog.gameObject.SetActive(false);

            var data = new SerializedObject(ui);
            data.FindProperty("previewTag").objectReferenceValue = tag.gameObject;
            data.FindProperty("dialog").objectReferenceValue = dialog.gameObject;
            data.FindProperty("dialogTitle").objectReferenceValue = title;
            data.FindProperty("dialogMessage").objectReferenceValue = message;
            data.FindProperty("yesLabel").objectReferenceValue = yes;
            data.FindProperty("currencyShop").objectReferenceValue = currencyShop;
            data.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(wasActive);
        }

        private static void BuildCard(SkinShopRow row, GameExtrasCatalog catalog)
        {
            Transform t = row.transform;
            var rowData = new SerializedObject(row);
            int index = rowData.FindProperty("skinIndex").intValue;
            GameExtrasCatalog.Skin skin = catalog != null && index < catalog.Skins.Length ? catalog.Skins[index] : null;

            LayoutElement layout = t.TryGetComponent(out LayoutElement existingLayout) ? existingLayout : t.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 290f;

            Image card = t.GetComponent<Image>();
            Button cardButton = t.TryGetComponent(out Button existingButton) ? existingButton : t.gameObject.AddComponent<Button>();
            cardButton.targetGraphic = card;
            OnClick(t.gameObject, card, row.Preview, Selectable.Transition.None);

            if (t.Find("Swatch") is RectTransform swatch)
                Place(swatch, new Vector2(0f, 68f), new Vector2(120f, 120f));
            Style(t, "NameText", new Vector2(0f, -6f), new Vector2(220f, 34f), 25f, Color.white);
            Style(t, "PriceText", new Vector2(0f, -46f), new Vector2(220f, 30f), 22f, new Color(0.86f, 0.8f, 0.98f));

            RectTransform soulRow = PriceRow(t, "SoulPrice", Load(Generated + "icon_coin_gold.png"), 32f, out TMP_Text soulText);
            Sprite currencyIcon = skin != null ? CurrencyIcon(skin.currency) : null;
            RectTransform currencyRow = PriceRow(t, "CurrencyPrice", currencyIcon, 36f, out TMP_Text currencyText);
            currencyText.richText = true;

            if (t.Find("ActionButton") is RectTransform action)
            {
                Place(action, new Vector2(0f, -109f), new Vector2(200f, 46f));
                Image pill = action.GetComponent<Image>();
                pill.sprite = Load(UiIconFactory.Folder + "ui_hud_pill.png");
                pill.type = Image.Type.Sliced;
                if (action.Find("Label") is RectTransform label && label.GetComponent<TMP_Text>() is TMP_Text labelText)
                {
                    Place(label, Vector2.zero, new Vector2(180f, 50f));
                    labelText.fontSize = 30f;
                    labelText.enableAutoSizing = true;
                    labelText.fontSizeMax = 30f;
                    labelText.fontSizeMin = 18f;
                }
            }

            Image previewMark = Img(t, "PreviewMark", Load(UiIconFactory.Folder + "ui_skin_card.png"), new Color(1f, 1f, 1f, 0f));
            previewMark.type = Image.Type.Sliced;
            previewMark.fillCenter = false;
            previewMark.pixelsPerUnitMultiplier = 1.4f;
            RectTransform pr = previewMark.rectTransform;
            pr.anchorMin = Vector2.zero;
            pr.anchorMax = Vector2.one;
            pr.offsetMin = new Vector2(-4f, -4f);
            pr.offsetMax = new Vector2(4f, 4f);
            pr.SetAsLastSibling();

            rowData.FindProperty("soulPrice").objectReferenceValue = soulRow;
            rowData.FindProperty("soulPriceText").objectReferenceValue = soulText;
            rowData.FindProperty("currencyPrice").objectReferenceValue = currencyRow;
            rowData.FindProperty("currencyPriceText").objectReferenceValue = currencyText;
            rowData.FindProperty("previewMark").objectReferenceValue = previewMark;
            rowData.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite CurrencyIcon(SkinCurrency currency) => currency switch
        {
            SkinCurrency.FrostShard => Load(Generated + "icon_currency_frost_shard.png"),
            SkinCurrency.BloodShard => Load(Generated + "icon_currency_blood_shard.png"),
            SkinCurrency.CyberCore => Load(Generated + "icon_currency_cyber_core.png"),
            SkinCurrency.EclipseFragment => Load(Generated + "icon_currency_eclipse_fragment.png"),
            _ => null,
        };

        // Icon + number, centred as a pair.
        private static RectTransform PriceRow(Transform card, string name, Sprite icon, float iconSize, out TMP_Text number)
        {
            // Laid out at runtime (SkinShopRow.PlacePrice) to centre icon + number.
            RectTransform row = Child(card, name);
            Place(row, new Vector2(0f, -50f), new Vector2(224f, 38f));
            if (row.TryGetComponent(out HorizontalLayoutGroup oldGroup))
                Object.DestroyImmediate(oldGroup);
            Image image = Img(row, "Icon", icon, Color.white);
            image.preserveAspect = true;
            if (image.TryGetComponent(out LayoutElement oldLayout))
                Object.DestroyImmediate(oldLayout);
            Place(image.rectTransform, new Vector2(-60f, 0f), new Vector2(iconSize, iconSize));
            number = Text(row, "Number", "0", Vector2.zero, new Vector2(150f, 38f), 27f, Color.white);
            number.enableAutoSizing = false;
            number.fontSize = 27f;
            number.alignment = TextAlignmentOptions.MidlineLeft;
            return row;
        }

        private static TMP_Text DialogButton(RectTransform box, string name, string label, Vector2 position, Color color, UnityAction action)
        {
            Image button = Img(box, name, Load(UiIconFactory.Folder + "ui_hud_pill.png"), color);
            button.type = Image.Type.Sliced;
            Place(button.rectTransform, position, new Vector2(200f, 70f));
            OnClick(button.gameObject, button, action, Selectable.Transition.ColorTint);
            return Text(button.rectTransform, "Label", label, Vector2.zero, new Vector2(180f, 60f), 34f, Color.white);
        }

        private static void Style(Transform card, string name, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            if (!(card.Find(name) is RectTransform rect))
                return;
            Place(rect, position, size);
            if (rect.GetComponent<TMP_Text>() is TMP_Text text)
            {
                text.fontSize = fontSize;
                text.enableAutoSizing = true;
                text.fontSizeMax = fontSize;
                text.fontSizeMin = 14f;
                text.alignment = TextAlignmentOptions.Center;
                text.color = color;
            }
        }

        // ---- helpers -------------------------------------------------------------

        private static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static void OnClick(GameObject target, Graphic graphic, UnityAction action, Selectable.Transition transition)
        {
            Button button = target.TryGetComponent(out Button existing) ? existing : target.AddComponent<Button>();
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

        private static Image Img(Transform parent, string name, Sprite sprite, Color color)
        {
            RectTransform rect = Child(parent, name);
            Image image = rect.TryGetComponent(out Image existing) ? existing : rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            RectTransform rect = Child(parent, name);
            TMP_Text text = rect.TryGetComponent(out TMP_Text existing) ? existing : rect.gameObject.AddComponent<TextMeshProUGUI>();
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
