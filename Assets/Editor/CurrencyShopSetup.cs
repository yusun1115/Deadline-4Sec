using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Deadline4Sec.Editor
{
    // Soul shop setup (Currencyshop.md, 2026-10-15):
    //  - Resources/Shop/CurrencyShopCatalog.asset (products; created once),
    //  - Canvas/CurrencyShopPanel (inactive): Background, BackButton → Close,
    //    SoulBox (top-right), Card_1..4 (CurrencyShop_card art; BuyButton →
    //    CurrencyShopCard.Buy), StatusText, EffectLayer,
    //  - TitlePanel/CurrencyshopButton → OpenFromTitle, and a CurrencyPlusBtn
    //    next to the Soul amount of SkinPanel / UpgradePanel →
    //    OpenFromSkinShop / OpenFromUpgradeShop.
    // Art: Assets/Art/2D/CurrencyShop. The open scene is changed in place (left
    // for you to save if it was dirty); the other scenes are saved and closed.
    public static class CurrencyShopSetup
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/EndlessRun.unity",
            "Assets/Scenes/GrayboxCourse.unity",
            "Assets/Scenes/TestScene.unity",
        };

        private const string CatalogPath = "Assets/Resources/Shop/CurrencyShopCatalog.asset";
        private const string Art = "Assets/Art/2D/CurrencyShop/";
        private const string SoulIcon = "Assets/Art/2D/Generated/icon_coin_gold.png";
        private static readonly Vector2[] CardPositions =
        {
            new Vector2(-190f, 173f), new Vector2(190f, 173f), new Vector2(-190f, -306f), new Vector2(190f, -306f),
        };
        private static readonly string[] CardArt = { "CurrencyShop_soul1", "CurrencyShop_soul1", "CurrencyShop_soul2", "CurrencyShop_soul3" };

        [MenuItem("Deadline 4 Sec/UI/Apply Currency Shop")]
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
                if (canvas != null && canvas.Find("TitlePanel") != null)
                {
                    Build(canvas);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!(isOpen && openWasDirty))
                        EditorSceneManager.SaveScene(scene);
                }
                if (!isOpen)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        public static CurrencyShopCatalog EnsureCatalog()
        {
            CurrencyShopCatalog catalog = AssetDatabase.LoadAssetAtPath<CurrencyShopCatalog>(CatalogPath);
            if (catalog == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CatalogPath));
                catalog = ScriptableObject.CreateInstance<CurrencyShopCatalog>();
                catalog.products = CurrencyShopCatalog.Defaults();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            for (int i = 0; i < catalog.products.Length && i < CardArt.Length; i++)
                if (catalog.products[i].icon == null)
                    catalog.products[i].icon = Load(Art + CardArt[i] + ".png");
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static void Build(Transform canvas)
        {
            RectTransform root = Child(canvas, "CurrencyShopPanel");
            Stretch(root);
            root.SetAsLastSibling();
            CurrencyShopPanel shop = root.TryGetComponent(out CurrencyShopPanel s) ? s : root.gameObject.AddComponent<CurrencyShopPanel>();

            Image background = Img(root, "Background", null, new Color(0.21f, 0.035f, 0.11f, 1f));
            Stretch(background.rectTransform);
            background.raycastTarget = true;

            // Back: same round button as the other menus.
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
            OnClick(back.gameObject, back, shop.Close);

            // Soul box: same black slanted box as the daily quest reward box.
            Image soulBox = Img(root, "SoulBox", Load("Assets/Art/2D/Dailyquest/box_Dailyquest.png"), new Color(0.06f, 0.03f, 0.07f, 1f));
            RectTransform soulRect = soulBox.rectTransform;
            soulRect.anchorMin = soulRect.anchorMax = new Vector2(1f, 1f);
            soulRect.pivot = new Vector2(1f, 1f);
            soulRect.anchoredPosition = new Vector2(-14f, -40f);
            soulRect.sizeDelta = new Vector2(300f, 76f);
            Image soulIcon = Img(soulRect, "Icon", Load(SoulIcon), Color.white);
            soulIcon.preserveAspect = true;
            Place(soulIcon.rectTransform, new Vector2(-92f, 4f), new Vector2(74f, 74f));
            TMP_Text soulText = Text(soulRect, "AmountText", "0", new Vector2(36f, 0f), new Vector2(180f, 56f), 42f, Color.white,
                TextAlignmentOptions.Left, true);

            var cards = new CurrencyShopCard[CardPositions.Length];
            for (int i = 0; i < cards.Length; i++)
                cards[i] = Card(root, shop, i);

            TMP_Text status = Text(root, "StatusText", "", Vector2.zero, new Vector2(700f, 60f), 30f, Color.white,
                TextAlignmentOptions.Center, false);
            RectTransform statusRect = status.rectTransform;
            statusRect.anchorMin = statusRect.anchorMax = new Vector2(0.5f, 0f);
            statusRect.anchoredPosition = new Vector2(0f, 120f);

            RectTransform effects = Child(root, "EffectLayer");
            Stretch(effects);
            effects.SetAsLastSibling();

            var so = new SerializedObject(shop);
            SerializedProperty list = so.FindProperty("cards");
            list.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            so.FindProperty("soulText").objectReferenceValue = soulText;
            so.FindProperty("soulIcon").objectReferenceValue = soulIcon.rectTransform;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("effectLayer").objectReferenceValue = effects;
            so.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);

            // Entry points.
            Transform title = canvas.Find("TitlePanel");
            Transform titleButton = title.Find("CurrencyshopButton");
            if (titleButton != null)
            {
                OnClick(titleButton.gameObject, titleButton.GetComponent<Graphic>(), shop.OpenFromTitle);
                // RedDot while today's ad Soul is still available (same art as the quest button's).
                RectTransform buttonRect = (RectTransform)titleButton;
                Image dot = RedDot(buttonRect, new Vector2(buttonRect.sizeDelta.x * 0.5f - 28f, buttonRect.sizeDelta.y * 0.5f - 22f),
                    new Vector2(31f, 30f));
                CurrencyShopButton driver = titleButton.TryGetComponent(out CurrencyShopButton d) ? d : titleButton.gameObject.AddComponent<CurrencyShopButton>();
                var driverData = new SerializedObject(driver);
                driverData.FindProperty("redDot").objectReferenceValue = dot.gameObject;
                driverData.ApplyModifiedPropertiesWithoutUndo();
            }
            Transform skin = canvas.Find("SkinPanel");
            if (skin != null)
                PlusButton(skin, shop.OpenFromSkinShop);
            Transform upgrade = canvas.Find("UpgradePanel");
            if (upgrade != null)
                PlusButton(upgrade, shop.OpenFromUpgradeShop);
        }

        private static CurrencyShopCard Card(RectTransform root, CurrencyShopPanel shop, int index)
        {
            Image card = Img(root, "Card_" + (index + 1), Load(Art + "CurrencyShop_card.png"), Color.white);
            Place(card.rectTransform, CardPositions[index], new Vector2(380f, 402f));
            RectTransform rect = card.rectTransform;
            if (!rect.TryGetComponent(out CanvasGroup _))
                rect.gameObject.AddComponent<CanvasGroup>();
            CurrencyShopCard view = rect.TryGetComponent(out CurrencyShopCard c) ? c : rect.gameObject.AddComponent<CurrencyShopCard>();

            Image icon = Img(rect, "Icon", Load(Art + CardArt[index] + ".png"), Color.white);
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(4f, 64f), new Vector2(230f, 236f));

            // Dark copy behind the amount for contrast on the pink card (kept in sync at runtime).
            TMP_Text shadow = Text(rect, "AmountShadow", "X100", new Vector2(4f, -60f), new Vector2(300f, 66f), 54f,
                new Color(0.22f, 0.03f, 0.12f, 0.85f), TextAlignmentOptions.Center, true);
            TMP_Text amount = Text(rect, "AmountText", "X100", new Vector2(0f, -56f), new Vector2(300f, 66f), 54f, Color.white,
                TextAlignmentOptions.Center, true);

            Image bonus = Img(rect, "BonusBadge", Load(Art + "CurrencyShop_bonus.png"), Color.white);
            bonus.preserveAspect = true;
            Place(bonus.rectTransform, new Vector2(138f, -66f), new Vector2(124f, 94f));
            TMP_Text bonusText = Text(bonus.rectTransform, "BonusText", "+500", new Vector2(0f, 2f), new Vector2(110f, 44f), 28f,
                new Color(0.86f, 0.22f, 0.5f), TextAlignmentOptions.Center, true);

            Image buy = Img(rect, "BuyButton", Load(Art + "CurrencyShop_buybtn.png"), Color.white);
            Place(buy.rectTransform, new Vector2(-14f, -142f), new Vector2(256f, 76f));
            Button buyButton = OnClick(buy.gameObject, buy, view.Buy);
            ColorBlock colors = buyButton.colors;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            buyButton.colors = colors;
            TMP_Text price = Text(buy.rectTransform, "PriceText", "₩1,100", new Vector2(10f, 0f), new Vector2(190f, 56f), 32f,
                Color.white, TextAlignmentOptions.Center, false);

            // "AD" tag on the rewarded-ad card's button.
            Image adBadge = Img(buy.rectTransform, "AdBadge", null, Color.white);
            Place(adBadge.rectTransform, new Vector2(-92f, 0f), new Vector2(44f, 30f));
            Text(adBadge.rectTransform, "Label", "AD", Vector2.zero, new Vector2(44f, 30f), 20f, new Color(0.1f, 0.05f, 0.1f),
                TextAlignmentOptions.Center, false);

            Image dim = Img(rect, "DimOverlay", Load(Art + "CurrencyShop_card.png"), new Color(0f, 0f, 0f, 0.6f));
            Stretch(dim.rectTransform);
            dim.enabled = false;
            dim.transform.SetAsLastSibling();
            // Top-right corner of the slanted card; only the ad card turns it on.
            Image dot = RedDot(rect, new Vector2(160f, 180f), new Vector2(46f, 44f));

            var so = new SerializedObject(view);
            so.FindProperty("shop").objectReferenceValue = shop;
            so.FindProperty("productIndex").intValue = index;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("amountText").objectReferenceValue = amount;
            so.FindProperty("amountShadow").objectReferenceValue = shadow;
            so.FindProperty("bonusBadge").objectReferenceValue = bonus.gameObject;
            so.FindProperty("bonusText").objectReferenceValue = bonusText;
            so.FindProperty("buyButton").objectReferenceValue = buyButton;
            so.FindProperty("priceText").objectReferenceValue = price;
            so.FindProperty("adBadge").objectReferenceValue = adBadge.gameObject;
            so.FindProperty("dimOverlay").objectReferenceValue = dim;
            so.FindProperty("redDot").objectReferenceValue = dot.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // "+" right of the Soul amount; the amount (and its icon) move left to make room.
        private static void PlusButton(Transform panel, UnityAction open)
        {
            Transform balance = panel.Find("CoinBalanceText");
            if (balance == null)
                return;
            RectTransform balanceRect = (RectTransform)balance;
            Transform existing = panel.Find("CurrencyPlusBtn");
            if (existing == null)
            {
                const float shift = 46f;
                balanceRect.anchoredPosition += new Vector2(-shift, 0f);
                Transform icon = panel.Find("CoinIcon");
                if (icon != null)
                    ((RectTransform)icon).anchoredPosition += new Vector2(-shift, 0f);
            }
            Image plus = Img((RectTransform)panel, "CurrencyPlusBtn", Load(Art + "CurrencyPlusBtn.png"), Color.white);
            plus.preserveAspect = true;
            RectTransform rect = plus.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            // Level with the amount: its rect centre, or its first line when top-aligned.
            float centreY = balanceRect.anchoredPosition.y + (0.5f - balanceRect.pivot.y) * balanceRect.sizeDelta.y;
            if (balance.TryGetComponent(out TMP_Text balanceText) &&
                (balanceText.alignment & (TextAlignmentOptions)0xF00) == (TextAlignmentOptions)0x100) // Top*
                centreY = balanceRect.anchoredPosition.y + (1f - balanceRect.pivot.y) * balanceRect.sizeDelta.y -
                          balanceText.fontSize * 0.6f;
            rect.anchoredPosition = new Vector2(-16f, centreY);
            rect.sizeDelta = new Vector2(42f, 42f);
            OnClick(plus.gameObject, plus, open);
        }

        private static Image RedDot(RectTransform parent, Vector2 position, Vector2 size)
        {
            Image dot = Img(parent, "RedDot", Load("Assets/Art/2D/Dailyquest/reddot.png"), Color.white);
            dot.preserveAspect = true;
            Place(dot.rectTransform, position, size);
            dot.transform.SetAsLastSibling();
            dot.gameObject.SetActive(false);
            return dot;
        }

        // ---- Debug (development only) ------------------------------------------

        [MenuItem("Deadline 4 Sec/Debug/Soul Shop - Reset Today's Ad Views")]
        public static void ResetAds()
        {
            CurrencyShopManager.ResetAdCount();
            Debug.Log("Soul shop ad views reset.");
        }

        // ---- Helpers ------------------------------------------------------------

        private static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static Button OnClick(GameObject target, Graphic graphic, UnityAction action)
        {
            Button button = target.TryGetComponent(out Button b) ? b : target.AddComponent<Button>();
            if (button.targetGraphic == null)
                button.targetGraphic = graphic;
            if (graphic != null)
                graphic.raycastTarget = true;
            while (button.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(button.onClick, 0);
            UnityEventTools.AddPersistentListener(button.onClick, action);
            return button;
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
            float fontSize, Color color, TextAlignmentOptions alignment, bool number)
        {
            RectTransform rect = Child(parent, name);
            TMP_Text text = rect.TryGetComponent(out TMP_Text t) ? t : rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (number && !rect.TryGetComponent(out NumericLabel _))
                rect.gameObject.AddComponent<NumericLabel>();
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
