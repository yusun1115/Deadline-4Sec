using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if DEADLINE_DOTWEEN
using DG.Tweening;
#endif

namespace Deadline4Sec
{
    // TitlePanel/DailyQuestPopup (built by Deadline 4 Sec/UI/Apply Daily Quest):
    // QuestButton On Click → Open, Xbutton_Dailyquest → Close.
    // Open: Popup_Dailyquest drops in from above; then every completed but
    // unclaimed quest pays out — the rewards are paid and saved first
    // (DailyQuestManager.ClaimReward), then, one quest at a time, its box dims,
    // the Soul box slides in from the right, a copy of the reward icon arcs
    // into it, the icon punches and the number counts up. The X button is off
    // while that plays, so nothing can be cut short or paid twice.
    public sealed class DailyQuestPopup : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private CanvasGroup backdrop;
        [SerializeField] private TMP_Text resetTimerText;
        [SerializeField] private DailyQuestBoxView[] boxes = new DailyQuestBoxView[DailyQuestManager.QuestCount];
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform currencyBox;
        [SerializeField] private RectTransform currencyIcon;
        [SerializeField] private TMP_Text currencyText;
        [SerializeField] private RectTransform rewardLayer;

        [Header("Popup")]
        [SerializeField] private float popupEnterOffsetY = 1100f;
        [SerializeField, Min(0.05f)] private float popupEnterDuration = 0.42f;
        [SerializeField, Min(0.05f)] private float popupExitDuration = 0.28f;
#if DEADLINE_DOTWEEN
        [SerializeField] private Ease popupEnterEase = Ease.OutBack;
#endif

        [Header("Reward")]
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.55f;
        [SerializeField, Min(0.01f)] private float dimDuration = 0.22f;
        [SerializeField] private float currencySlideOffsetX = 460f;
        [SerializeField, Min(0.05f)] private float currencySlideDuration = 0.3f;
        [SerializeField, Min(0.1f)] private float flyDuration = 0.55f;
        [SerializeField] private float flyArcHeight = 240f;
        [SerializeField, Min(0f)] private float claimInterval = 0.15f;
        [SerializeField, Min(0.05f)] private float countUpDuration = 0.35f;
        [SerializeField, Min(0f)] private float currencyHoldSeconds = 0.5f;

        private Vector2 panelHome;
        private Vector2 currencyHome;
        private bool homesCaptured;
        private bool isOpen;
        private bool closing;
        private bool claiming;
        private int shownSouls;
        private float nextTick;
        private readonly List<GameObject> flyingIcons = new List<GameObject>();
#if DEADLINE_DOTWEEN
        private Sequence sequence;
#endif

        public bool IsOpen => isOpen;
        public bool IsClaiming => claiming;

        private void CaptureHomes()
        {
            if (homesCaptured)
                return;
            homesCaptured = true;
            if (panel != null)
                panelHome = panel.anchoredPosition;
            if (currencyBox != null)
                currencyHome = currencyBox.anchoredPosition;
        }

        public void Open()
        {
            if (isOpen && !closing)
                return; // already open (repeated taps)
            CaptureHomes();
            KillTweens();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            isOpen = true;
            closing = false;
            DailyQuestManager.CheckDate();
            FitToScreen();
            RefreshBoxes();
            UpdateTimer();
            SetCurrencyHidden();
            Click();

            List<int> rewards = PendingRewards();
#if DEADLINE_DOTWEEN
            panel.anchoredPosition = panelHome + new Vector2(0f, popupEnterOffsetY);
            if (backdrop != null)
                backdrop.alpha = 0f;
            sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Append(panel.DOAnchorPos(panelHome, popupEnterDuration).SetEase(popupEnterEase));
            if (backdrop != null)
                sequence.Join(backdrop.DOFade(1f, popupEnterDuration * 0.6f));
            if (rewards.Count > 0)
                sequence.OnComplete(() => PlayRewards(rewards));
#else
            panel.anchoredPosition = panelHome;
            if (backdrop != null)
                backdrop.alpha = 1f;
            if (rewards.Count > 0)
                PlayRewards(rewards);
#endif
        }

        public void Close()
        {
            if (!isOpen || closing || claiming)
                return;
            closing = true;
            Click();
            KillTweens();
#if DEADLINE_DOTWEEN
            sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Append(panel.DOAnchorPos(panelHome + new Vector2(0f, popupEnterOffsetY), popupExitDuration).SetEase(Ease.InCubic));
            if (backdrop != null)
                sequence.Join(backdrop.DOFade(0f, popupExitDuration));
            sequence.OnComplete(FinishClose);
#else
            FinishClose();
#endif
        }

        private void FinishClose()
        {
            isOpen = false;
            closing = false;
            if (panel != null)
                panel.anchoredPosition = panelHome;
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            // Rewards are already paid; just drop any animation left.
            KillTweens();
            ClearFlyingIcons();
            claiming = false;
            isOpen = false;
            closing = false;
            if (closeButton != null)
                closeButton.interactable = true;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextTick)
                return;
            nextTick = Time.unscaledTime + 1f;
            UpdateTimer();
            if (DailyQuestManager.CheckDate() && !claiming)
                RefreshBoxes(); // midnight passed while open
        }

        // ---- Rewards ------------------------------------------------------------

        private List<int> PendingRewards()
        {
            var pending = new List<int>();
            IReadOnlyList<DailyQuestManager.Slot> slots = DailyQuestManager.Slots;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].HasUnclaimedReward)
                    pending.Add(i);
            return pending;
        }

        private void PlayRewards(List<int> pending)
        {
            CoinWallet wallet = FindFirstObjectByType<CoinWallet>();
            shownSouls = CurrentSouls(wallet);
            var paid = new List<int>();
            var amounts = new List<int>();
            foreach (int index in pending)
            {
                int amount = DailyQuestManager.Slots[index].Definition.rewardAmount;
                if (DailyQuestManager.ClaimReward(index, wallet))
                {
                    paid.Add(index);
                    amounts.Add(amount);
                }
            }
            if (paid.Count == 0)
                return;
            SetCurrencyText(shownSouls);
#if DEADLINE_DOTWEEN
            claiming = true;
            if (closeButton != null)
                closeButton.interactable = false;
            KillTweens();
            sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Append(currencyBox.DOAnchorPos(currencyHome, currencySlideDuration).SetEase(Ease.OutCubic));
            for (int n = 0; n < paid.Count; n++)
            {
                DailyQuestBoxView box = boxes[paid[n]];
                int amount = amounts[n];
                if (box == null)
                    continue;
                sequence.Append(DOTween.To(() => 0f, box.SetDim, dimAlpha, dimDuration));
                GameObject icon = CreateFlyingIcon(box);
                RectTransform iconRect = (RectTransform)icon.transform;
                sequence.AppendCallback(() =>
                {
                    icon.SetActive(true);
                    iconRect.position = box.RewardIcon.position;
                });
                sequence.Append(DOTween.To(() => 0f, t => Fly(iconRect, box.RewardIcon, t), 1f, flyDuration).SetEase(Ease.InQuad));
                sequence.AppendCallback(() =>
                {
                    flyingIcons.Remove(icon);
                    Destroy(icon);
                    Arrive(amount);
                });
                sequence.AppendInterval(Mathf.Max(claimInterval, 0.01f));
            }
            sequence.AppendInterval(countUpDuration + currencyHoldSeconds);
            sequence.Append(currencyBox.DOAnchorPos(currencyHome + new Vector2(currencySlideOffsetX, 0f), currencySlideDuration)
                .SetEase(Ease.InCubic));
            sequence.OnComplete(FinishRewards);
#else
            foreach (int index in paid)
                boxes[index]?.SetDim(dimAlpha);
            SetCurrencyText(CurrentSouls(wallet));
            FinishRewards();
#endif
        }

        private void FinishRewards()
        {
            claiming = false;
            if (closeButton != null)
                closeButton.interactable = true;
            SetCurrencyHidden();
        }

#if DEADLINE_DOTWEEN
        // Arc from the quest's reward icon to the Soul box icon, shrinking at the end.
        private void Fly(RectTransform icon, RectTransform from, float t)
        {
            if (icon == null || from == null || currencyIcon == null)
                return;
            Vector3 start = from.position;
            Vector3 end = currencyIcon.position;
            Vector3 control = (start + end) * 0.5f + rewardLayer.TransformVector(new Vector3(-flyArcHeight * 0.4f, flyArcHeight, 0f));
            float u = 1f - t;
            icon.position = u * u * start + 2f * u * t * control + t * t * end;
            icon.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.7f, t * t);
        }

        private void Arrive(int amount)
        {
            if (currencyIcon != null)
            {
                currencyIcon.DOKill(true);
                currencyIcon.localScale = Vector3.one;
                currencyIcon.DOPunchScale(Vector3.one * 0.15f, 0.25f, 6, 0.6f).SetUpdate(true);
            }
            int from = shownSouls;
            shownSouls += amount;
            DOTween.To(() => from, v => SetCurrencyText(v), shownSouls, countUpDuration).SetUpdate(true).SetTarget(this);
            FindFirstObjectByType<CameraFeedbackController>()?.PlayMenuResult(true);
        }
#endif

        private GameObject CreateFlyingIcon(DailyQuestBoxView box)
        {
            var go = new GameObject("FlyingReward", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(rewardLayer != null ? rewardLayer : transform, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.sizeDelta = box.RewardIcon.rect.size * panel.localScale.x;
            Image image = go.GetComponent<Image>();
            image.sprite = box.RewardSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            go.SetActive(false);
            flyingIcons.Add(go);
            return go;
        }

        private void ClearFlyingIcons()
        {
            foreach (GameObject icon in flyingIcons)
                if (icon != null)
                    Destroy(icon);
            flyingIcons.Clear();
        }

        // ---- UI ------------------------------------------------------------------

        private void RefreshBoxes()
        {
            IReadOnlyList<DailyQuestManager.Slot> slots = DailyQuestManager.Slots;
            for (int i = 0; i < boxes.Length && i < slots.Count; i++)
                if (boxes[i] != null)
                    boxes[i].Show(slots[i], dimAlpha, slots[i].Claimed);
        }

        private void UpdateTimer()
        {
            if (resetTimerText != null)
                resetTimerText.text = DailyQuestManager.ResetLabel();
        }

        // The panel art is 1174 tall; shorter screens (tablets) scale it down.
        private void FitToScreen()
        {
            if (panel == null)
                return;
            float available = ((RectTransform)transform).rect.height - 120f;
            float scale = Mathf.Min(1f, available / Mathf.Max(1f, panel.rect.height));
            panel.localScale = Vector3.one * scale;
        }

        private void SetCurrencyHidden()
        {
            if (currencyBox != null)
                currencyBox.anchoredPosition = currencyHome + new Vector2(currencySlideOffsetX, 0f);
        }

        private void SetCurrencyText(int value)
        {
            if (currencyText != null)
                currencyText.text = value.ToString(CultureInfo.InvariantCulture);
        }

        private static int CurrentSouls(CoinWallet wallet) =>
            wallet != null ? wallet.TotalCoins : Mathf.Max(0, PlayerPrefs.GetInt(CoinWallet.TotalCoinsKey, 0));

        private void KillTweens()
        {
#if DEADLINE_DOTWEEN
            if (sequence != null && sequence.IsActive())
                sequence.Kill();
            sequence = null;
            DOTween.Kill(this);
            if (currencyIcon != null)
                currencyIcon.DOKill(true);
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
