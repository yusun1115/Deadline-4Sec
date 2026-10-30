using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // Skin shop card (2026-10-30 rework): the state reads from colour and frame,
    // not only the button word —
    //   Equipped  pink frame + pulsing glow, button "EQUIPPED" (off)
    //   Owned     white / violet frame, violet "EQUIP"
    //   Buyable   bright pink "BUY" (gently pulsing)
    //   SoulShort dim "BUY" that still opens the "not enough Soul" dialog
    //   Locked    grey "BUY" (its currency is short; nothing to do yet)
    // Prices are icons + numbers (Soul, and the skin currency as "have / need",
    // red while short). Tapping the card previews the weapon in the hand.
    public sealed class SkinShopRow : MonoBehaviour
    {
        public enum CardState { Equipped, Owned, Buyable, SoulShort, Locked }

        private static readonly Color Pink = new Color(1f, 0.3f, 0.62f);
        private static readonly Color Short = new Color(1f, 0.36f, 0.42f);

        [SerializeField] private int skinIndex;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private TMP_Text actionText;
        [SerializeField] private Button actionButton;
        [SerializeField] private Image swatch;
        // State frame behind the card (was the equipped-only mark).
        [SerializeField] private GameObject equippedMark;
        [Header("Price (icon + number)")]
        [SerializeField] private RectTransform soulPrice;
        [SerializeField] private TMP_Text soulPriceText;
        [SerializeField] private RectTransform currencyPrice;
        [SerializeField] private TMP_Text currencyPriceText;
        [SerializeField] private Image previewMark;

        private Image frame;
        private Image glow;
        private RectTransform actionRect;
        private bool previewed;
        private float popUntil;

        public int SkinIndex => skinIndex;
        public CardState State { get; private set; }

        private void Awake()
        {
            if (equippedMark != null)
                frame = equippedMark.GetComponent<Image>();
            if (actionButton != null)
            {
                actionRect = actionButton.GetComponent<RectTransform>();
                // Filled pill, so the state colour reads at full strength.
                if (actionButton.targetGraphic is Image pill)
                {
                    pill.sprite = FxSprites.RoundedBox;
                    pill.type = Image.Type.Sliced;
                    pill.pixelsPerUnitMultiplier = 1f;
                }
            }
            // Soft glow behind the frame, lit on the equipped card.
            glow = new GameObject("StateGlow", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            glow.rectTransform.SetParent(transform, false);
            glow.rectTransform.SetAsFirstSibling();
            glow.rectTransform.anchorMin = Vector2.zero;
            glow.rectTransform.anchorMax = Vector2.one;
            glow.rectTransform.offsetMin = new Vector2(-34f, -34f);
            glow.rectTransform.offsetMax = new Vector2(34f, 34f);
            glow.sprite = FxSprites.EdgeGlowBox;
            glow.type = Image.Type.Sliced;
            glow.raycastTarget = false;
            glow.color = new Color(Pink.r, Pink.g, Pink.b, 0f);
            gameObject.AddComponent<SkinCardTheme>().Build(skinIndex, swatch != null ? swatch.rectTransform : null);
        }

        // Card body: preview the weapon in the hand.
        public void Preview()
        {
            SkinShopUI shop = GetComponentInParent<SkinShopUI>();
            if (shop != null)
                shop.Preview(skinIndex);
        }

        // Button: equip / buy (with confirmation) through the shop screen.
        public void BuyOrEquip()
        {
            SkinShopUI shop = GetComponentInParent<SkinShopUI>();
            if (shop != null)
            {
                shop.OnCardAction(this);
                return;
            }
            RunInventory inventory = FindFirstObjectByType<RunInventory>();
            if (inventory != null)
                inventory.TryBuyOrEquipSkin(skinIndex);
        }

        public void SetPreviewed(bool value) => previewed = value;
        public void Pop() => popUntil = Time.unscaledTime + 0.35f;

        public void Refresh(RunInventory inventory)
        {
            if (inventory.Catalog == null || skinIndex >= inventory.Catalog.Skins.Length)
                return;
            GameExtrasCatalog.Skin skin = inventory.Catalog.Skins[skinIndex];
            bool owned = inventory.OwnsSkin(skinIndex);
            bool equipped = inventory.EquippedSkinIndex == skinIndex;
            inventory.SkinShortfall(skinIndex, out bool soulShort, out bool currencyShort);
            State = equipped ? CardState.Equipped : owned ? CardState.Owned :
                currencyShort ? CardState.Locked : soulShort ? CardState.SoulShort : CardState.Buyable;

            if (nameText != null)
                nameText.text = skin.displayName;
            if (priceText != null)
                priceText.text = owned ? (skinIndex == 0 ? "DEFAULT" : "OWNED") : "";

            bool showSoul = !owned && skin.coinCost > 0;
            bool showCurrency = !owned && skin.currencyCost > 0 && skin.currency != SkinCurrency.None;
            if (soulPriceText != null)
            {
                soulPriceText.text = skin.coinCost.ToString("N0");
                soulPriceText.color = soulShort ? Short : Color.white;
            }
            if (currencyPriceText != null)
            {
                int have = Mathf.Min(inventory.CurrencyCount(skin.currency), 9999);
                string haveText = currencyShort ? "<color=#FF5C6B>" + have + "</color>" : "<color=#7DFF9A>" + have + "</color>";
                currencyPriceText.text = haveText + " / " + skin.currencyCost;
            }
            PlacePrice(soulPrice, soulPriceText, showSoul, showCurrency ? -38f : -48f);
            PlacePrice(currencyPrice, currencyPriceText, showCurrency, showSoul ? -64f : -48f);

            if (actionText != null)
                actionText.text = equipped ? "EQUIPPED" : owned ? "EQUIP" : "BUY";
            if (actionButton != null)
            {
                actionButton.interactable = State == CardState.Owned || State == CardState.Buyable || State == CardState.SoulShort;
                ColorBlock colors = actionButton.colors;
                colors.disabledColor = Color.white; // the state colour below already says it
                actionButton.colors = colors;
                if (actionButton.targetGraphic != null)
                    actionButton.targetGraphic.color = State switch
                    {
                        CardState.Equipped => new Color(1f, 0.3f, 0.62f, 0.22f),
                        CardState.Owned => new Color(0.5f, 0.32f, 0.86f, 1f),
                        CardState.Buyable => new Color(1f, 0.2f, 0.52f, 1f),
                        CardState.SoulShort => new Color(0.42f, 0.18f, 0.3f, 1f),
                        _ => new Color(0.2f, 0.16f, 0.25f, 1f),
                    };
            }
            if (actionText != null)
                actionText.color = State switch
                {
                    CardState.Equipped => Pink,
                    CardState.SoulShort => new Color(1f, 0.78f, 0.84f, 0.75f),
                    CardState.Locked => new Color(0.55f, 0.5f, 0.6f, 1f),
                    _ => Color.white,
                };
            if (frame != null)
            {
                equippedMark.SetActive(true);
                frame.color = State switch
                {
                    CardState.Equipped => Pink,
                    CardState.Owned => new Color(0.86f, 0.8f, 1f, 1f),
                    CardState.Buyable => new Color(0.55f, 0.32f, 0.72f, 1f),
                    _ => new Color(0.26f, 0.2f, 0.32f, 1f),
                };
            }

            // Cards show the skin's weapon icon, dimmed until owned; without
            // one, the scythe silhouette tinted with the blade colour.
            if (swatch != null && skin.icon != null)
            {
                swatch.sprite = skin.icon;
                swatch.color = owned ? Color.white : State == CardState.Buyable
                    ? new Color(0.92f, 0.92f, 0.92f, 0.95f) : new Color(0.7f, 0.7f, 0.7f, 0.6f);
            }
            else if (swatch != null && skin.weaponMaterial != null && skin.weaponMaterial.HasProperty("_BaseColor"))
            {
                Color c = skin.weaponMaterial.GetColor("_BaseColor");
                c.a = owned ? 1f : 0.45f;
                swatch.color = c;
            }
        }

        // Icon + number centred as a pair on the card.
        private static void PlacePrice(RectTransform row, TMP_Text number, bool shown, float y)
        {
            if (row == null)
                return;
            row.gameObject.SetActive(shown);
            if (!shown || number == null)
                return;
            row.anchoredPosition = new Vector2(0f, y);
            RectTransform icon = row.Find("Icon") as RectTransform;
            float iconWidth = icon != null ? icon.sizeDelta.x : 0f;
            const float Gap = 6f;
            float textWidth = number.GetPreferredValues(number.text).x;
            float total = iconWidth + Gap + textWidth;
            if (icon != null)
            {
                icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
                icon.pivot = new Vector2(0.5f, 0.5f);
                icon.anchoredPosition = new Vector2(-total * 0.5f + iconWidth * 0.5f, 0f);
            }
            RectTransform text = number.rectTransform;
            text.anchorMin = text.anchorMax = new Vector2(0.5f, 0.5f);
            text.pivot = new Vector2(0f, 0.5f);
            text.sizeDelta = new Vector2(textWidth + 4f, text.sizeDelta.y);
            text.anchoredPosition = new Vector2(-total * 0.5f + iconWidth + Gap, 0f);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (glow != null)
            {
                float a = State == CardState.Equipped ? 0.55f + 0.25f * Mathf.Sin(now * 3f) : 0f;
                glow.color = new Color(Pink.r, Pink.g, Pink.b, Mathf.MoveTowards(glow.color.a, a, Time.unscaledDeltaTime * 3f));
                glow.enabled = glow.color.a > 0.01f;
            }
            if (actionRect != null)
            {
                float pulse = State == CardState.Buyable ? 1f + 0.045f * Mathf.Sin(now * 5f) : 1f;
                actionRect.localScale = Vector3.one * pulse;
            }
            if (previewMark != null)
            {
                float a = previewed ? 0.75f + 0.25f * Mathf.Sin(now * 4f) : 0f;
                previewMark.color = new Color(1f, 1f, 1f, a);
                previewMark.enabled = a > 0.01f;
            }
            float pop = popUntil > now ? 1f + 0.08f * Mathf.Sin((popUntil - now) / 0.35f * Mathf.PI) : 1f;
            transform.localScale = Vector3.one * pop;
        }
    }
}
