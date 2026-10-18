using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
#if DEADLINE_DOTWEEN
using DG.Tweening;
#endif

namespace Deadline4Sec
{
    // Canvas/CurrencyShopPanel: the Soul shop, laid over whatever screen opened
    // it. Title CurrencyshopButton → OpenFromTitle, the Skin / Upgrade
    // CurrencyPlusBtn → OpenFromSkinShop / OpenFromUpgradeShop. Back → Close,
    // which simply uncovers that screen again (it never closed), so the
    // entry point (PreviousShopContext) is all the navigation needed.
    // On a granted reward: a Soul icon flies from the card to the top Soul
    // box, the box icon punches and the number counts up, "+100 SOUL" fades.
    public sealed class CurrencyShopPanel : MonoBehaviour
    {
        [SerializeField] private CurrencyShopCard[] cards = new CurrencyShopCard[0];
        [SerializeField] private TMP_Text soulText;
        [SerializeField] private RectTransform soulIcon;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private RectTransform effectLayer;
        [SerializeField, Min(0.1f)] private float flyDuration = 0.55f;
        [SerializeField] private float flyArcHeight = 220f;
        [SerializeField, Min(0.05f)] private float countUpDuration = 0.4f;

        private int shownSoul;
        private readonly List<GameObject> flying = new List<GameObject>();

        public ShopEntrySource PreviousShopContext { get; private set; }
        public bool IsOpen => gameObject.activeSelf;
        public int ShownSoul => shownSoul;

        public void OpenFromTitle() => Open(ShopEntrySource.Title);
        public void OpenFromSkinShop() => Open(ShopEntrySource.SkinShop);
        public void OpenFromUpgradeShop() => Open(ShopEntrySource.UpgradeShop);

        public void Open(ShopEntrySource source)
        {
            PreviousShopContext = source;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            CurrencyShopManager.CheckAdDate();
            shownSoul = SoulBank.TotalSoul;
            SetSoulText(shownSoul);
            SetStatus("");
            Refresh();
            PlayCardsIn();
            Click();
        }

        // Back: the screen underneath (title, skins or upgrades) is still open.
        public void Close()
        {
            if (CurrencyShopManager.IsBusy)
                return;
            Click();
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            CurrencyShopManager.Changed += Refresh;
        }

        private void OnDisable()
        {
            CurrencyShopManager.Changed -= Refresh;
#if DEADLINE_DOTWEEN
            DOTween.Kill(this);
#endif
            foreach (GameObject icon in flying)
                if (icon != null)
                    Destroy(icon);
            flying.Clear();
        }

        public void Refresh()
        {
            CurrencyShopProduct[] products = CurrencyShopManager.Catalog.products;
            int remaining = CurrencyShopManager.AdsRemaining;
            bool busy = CurrencyShopManager.IsBusy;
            foreach (CurrencyShopCard card in cards)
                if (card != null)
                    card.Show(card.ProductIndex < products.Length ? products[card.ProductIndex] : null, remaining,
                        CurrencyShopManager.AdDailyLimit, busy);
        }

        public void Buy(int index)
        {
            CurrencyShopProduct[] products = CurrencyShopManager.Catalog.products;
            if (index < 0 || index >= products.Length || CurrencyShopManager.IsBusy)
                return;
            CurrencyShopProduct product = products[index];
            CurrencyShopCard card = System.Array.Find(cards, c => c != null && c.ProductIndex == index);
            Click();
            int before = SoulBank.TotalSoul;
            if (product.productType == CurrencyShopProductType.RewardedAd)
            {
                CurrencyShopManager.TryWatchSoulRewardAd(granted =>
                {
                    if (this == null || !isActiveAndEnabled)
                        return;
                    if (granted)
                        PlayReward(card, before, product.TotalSoul);
                    else
                        SetStatus(CurrencyShopManager.AdsRemaining <= 0 ? "오늘 광고 보상을 모두 받았습니다." : "광고를 끝까지 보지 않아 보상이 지급되지 않았습니다.");
                });
            }
            else
            {
                CurrencyShopManager.Purchase(product, result =>
                {
                    if (this == null || !isActiveAndEnabled)
                        return;
                    if (result == PurchaseResult.Success)
                        PlayReward(card, before, product.TotalSoul);
                    else
                        SetStatus(result == PurchaseResult.Cancelled ? "구매가 취소되었습니다." : "구매에 실패했습니다.");
                });
            }
        }

        // ---- Reward presentation (Soul is already granted and saved) -------------

        private void PlayReward(CurrencyShopCard card, int before, int amount)
        {
            shownSoul = before;
            SetSoulText(before);
            SetStatus("+" + amount.ToString("N0", CultureInfo.InvariantCulture) + " SOUL", new Color(0.75f, 0.92f, 1f));
#if DEADLINE_DOTWEEN
            if (card == null || soulIcon == null || effectLayer == null)
            {
                CountTo(before + amount);
                return;
            }
            var go = new GameObject("FlyingSoul", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(effectLayer, false);
            flying.Add(go);
            RectTransform rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(110f, 110f);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = card.IconSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            Vector3 start = card.Icon.position;
            rect.position = start;
            DOTween.To(() => 0f, t =>
                {
                    Vector3 end = soulIcon.position;
                    Vector3 control = (start + end) * 0.5f + effectLayer.TransformVector(new Vector3(-flyArcHeight * 0.4f, flyArcHeight, 0f));
                    float u = 1f - t;
                    rect.position = u * u * start + 2f * u * t * control + t * t * end;
                    rect.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.45f, t * t);
                }, 1f, flyDuration)
                .SetEase(Ease.InQuad).SetUpdate(true).SetTarget(this)
                .OnComplete(() =>
                {
                    flying.Remove(go);
                    Destroy(go);
                    soulIcon.DOKill(true);
                    soulIcon.localScale = Vector3.one;
                    soulIcon.DOPunchScale(Vector3.one * 0.18f, 0.25f, 6, 0.6f).SetUpdate(true).SetTarget(this);
                    CountTo(before + amount);
                    FindFirstObjectByType<CameraFeedbackController>()?.PlayMenuResult(true);
                });
#else
            CountTo(before + amount);
#endif
        }

        private void CountTo(int target)
        {
#if DEADLINE_DOTWEEN
            int from = shownSoul;
            shownSoul = target;
            DOTween.To(() => from, v => SetSoulText(v), target, countUpDuration).SetUpdate(true).SetTarget(this);
#else
            shownSoul = target;
            SetSoulText(target);
#endif
        }

        // Cards pop in one after another (scale 0.9 → 1, fade in).
        private void PlayCardsIn()
        {
#if DEADLINE_DOTWEEN
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null)
                    continue;
                Transform t = cards[i].transform;
                CanvasGroup group = cards[i].GetComponent<CanvasGroup>();
                t.DOKill();
                t.localScale = Vector3.one * 0.9f;
                t.DOScale(1f, 0.28f).SetEase(Ease.OutBack).SetDelay(0.06f * i).SetUpdate(true);
                if (group != null)
                {
                    group.DOKill();
                    group.alpha = 0f;
                    group.DOFade(1f, 0.2f).SetDelay(0.06f * i).SetUpdate(true);
                }
            }
#endif
        }

        private void SetSoulText(int value)
        {
            if (soulText != null)
                soulText.text = value.ToString(CultureInfo.InvariantCulture);
        }

        private void SetStatus(string message) => SetStatus(message, new Color(1f, 0.6f, 0.68f));

        private void SetStatus(string message, Color color)
        {
            if (statusText == null)
                return;
            statusText.text = message;
            statusText.color = color;
#if DEADLINE_DOTWEEN
            DOTween.Kill(statusText);
            if (message.Length > 0)
            {
                // DOTween's TMP module is off, so the fade is a plain To().
                statusText.alpha = 1f;
                TMP_Text label = statusText;
                DOTween.To(() => label.alpha, a => label.alpha = a, 0f, 0.6f).SetDelay(1.6f).SetUpdate(true).SetTarget(label);
            }
#endif
        }

        private void Click()
        {
            CameraFeedbackController feedback = FindFirstObjectByType<CameraFeedbackController>();
            if (feedback != null)
                feedback.PlayUIClick();
        }
    }
}
