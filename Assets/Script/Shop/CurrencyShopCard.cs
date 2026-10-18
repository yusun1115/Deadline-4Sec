using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // One Soul shop card (CurrencyShop_card): product art, "X1,000", the bonus
    // badge when the product has bonusSoul, and the black buy button with the
    // price — or, for the rewarded-ad card, "AD  FREE 3/3" and a dim overlay
    // once today's views are used up, with a RedDot while views remain.
    // BuyButton On Click → Buy.
    public sealed class CurrencyShopCard : MonoBehaviour
    {
        [SerializeField] private CurrencyShopPanel shop;
        [SerializeField] private int productIndex;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private TMP_Text amountShadow;
        [SerializeField] private GameObject bonusBadge;
        [SerializeField] private TMP_Text bonusText;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private GameObject adBadge;
        [SerializeField] private Image dimOverlay;
        [SerializeField] private GameObject redDot;
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.6f;
        [Tooltip("Amount moves left this much when the bonus badge shows.")]
        [SerializeField] private float bonusAmountShift = -34f;
        [Tooltip("Price moves right this much to clear the AD tag.")]
        [SerializeField] private float adPriceShift = 24f;

        private Vector2 amountHome, shadowHome, priceHome;
        private bool homesCaptured;

        private void CaptureHomes()
        {
            if (homesCaptured)
                return;
            homesCaptured = true;
            if (amountText != null)
                amountHome = amountText.rectTransform.anchoredPosition;
            if (amountShadow != null)
                shadowHome = amountShadow.rectTransform.anchoredPosition;
            if (priceText != null)
                priceHome = priceText.rectTransform.anchoredPosition;
        }

        public int ProductIndex => productIndex;
        public RectTransform Icon => icon != null ? icon.rectTransform : (RectTransform)transform;
        public Sprite IconSprite => icon != null ? icon.sprite : null;
        public Button BuyButton => buyButton;

        public void Buy()
        {
            if (shop != null)
                shop.Buy(productIndex);
        }

        public void Show(CurrencyShopProduct product, int adsRemaining, int adLimit, bool busy)
        {
            gameObject.SetActive(product != null);
            if (product == null)
                return;
            bool ad = product.productType == CurrencyShopProductType.RewardedAd;
            CaptureHomes();
            Vector2 amountShift = new Vector2(product.bonusSoul > 0 ? bonusAmountShift : 0f, 0f);
            if (amountText != null)
                amountText.rectTransform.anchoredPosition = amountHome + amountShift;
            if (amountShadow != null)
                amountShadow.rectTransform.anchoredPosition = shadowHome + amountShift;
            if (priceText != null)
                priceText.rectTransform.anchoredPosition = priceHome + new Vector2(ad ? adPriceShift : 0f, 0f);
            if (icon != null && product.icon != null)
                icon.sprite = product.icon;
            string amount = "X" + product.TotalSoul.ToString("N0", CultureInfo.InvariantCulture);
            if (amountText != null)
                amountText.text = amount;
            if (amountShadow != null)
                amountShadow.text = amount;
            if (bonusBadge != null)
                bonusBadge.SetActive(product.bonusSoul > 0);
            if (bonusText != null)
                bonusText.text = "+" + product.bonusSoul.ToString("N0", CultureInfo.InvariantCulture);
            if (adBadge != null)
                adBadge.SetActive(ad);
            bool soldOut = ad && adsRemaining <= 0;
            if (redDot != null)
                redDot.SetActive(ad && adsRemaining > 0);
            if (priceText != null)
                priceText.text = ad ? (product.displayPrice + "  " + Mathf.Max(0, adsRemaining) + "/" + adLimit) : product.displayPrice;
            if (buyButton != null)
                buyButton.interactable = !busy && !soldOut;
            if (dimOverlay != null)
            {
                dimOverlay.enabled = soldOut;
                Color c = dimOverlay.color;
                c.a = dimAlpha;
                dimOverlay.color = c;
            }
        }
    }
}
