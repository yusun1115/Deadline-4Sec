using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    public sealed class GameExtrasUI : MonoBehaviour
    {
        [SerializeField] private GameObject revivePanel;
        [SerializeField] private TMP_Text reviveCountdownText;
        [SerializeField] private TMP_Text couponText;
        [SerializeField] private Button couponButton;
        [SerializeField] private GameObject giftPanel;
        [SerializeField] private TMP_Text giftProgressText;
        [SerializeField] private TMP_Text giftRewardText;
        [SerializeField] private TMP_Text giftActionText;
        [SerializeField] private GameObject giftOpenButton;
        [SerializeField] private GameObject giftConfirmButton;
        [SerializeField] private TMP_Text giftHudText;
        [SerializeField] private TMP_Text consumableHudText;
        [SerializeField] private SkinShopRow[] skinRows;
        [SerializeField] private ConsumableShopRow[] consumableRows;

        private RunInventory inventory;

        private void Awake()
        {
            inventory = GetComponent<RunInventory>();
            if (revivePanel != null)
                revivePanel.SetActive(false);
            if (giftPanel != null)
                giftPanel.SetActive(false);
        }

        private void Start()
        {
            if (inventory != null)
                inventory.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (inventory != null)
                inventory.Changed -= Refresh;
        }

        private void Update()
        {
            if (inventory == null || consumableHudText == null)
                return;
            string active = inventory.ShieldActive ? "SHIELD ACTIVE" :
                inventory.CollisionProtected ? "PROTECTED" :
                inventory.DashBonusSpeed > 0f ? "DASH" : "";
            consumableHudText.text = inventory.EquippedConsumable switch
            {
                ConsumableKind.Shield => "SHIELD x" + inventory.ShieldCount,
                ConsumableKind.Dash => "DASH x" + inventory.DashCount,
                _ => "ITEM NONE"
            };
            if (active.Length > 0)
                consumableHudText.text += "\n" + active;
        }

        public void Refresh()
        {
            if (inventory == null)
                inventory = GetComponent<RunInventory>();
            if (inventory == null)
                return;
            if (giftHudText != null)
                giftHudText.text = "GIFT BOX x" + inventory.CurrentRunGiftBoxes;
            if (couponText != null)
                couponText.text = "REVIVE COUPON x" + inventory.CouponCount;
            if (couponButton != null)
                couponButton.interactable = inventory.CouponCount > 0;
            if (skinRows != null)
                foreach (SkinShopRow row in skinRows)
                    if (row != null)
                        row.Refresh(inventory);
            if (consumableRows != null)
                foreach (ConsumableShopRow row in consumableRows)
                    if (row != null)
                        row.Refresh(inventory);
        }

        public void ShowRevive(float seconds)
        {
            if (revivePanel != null)
                revivePanel.SetActive(true);
            SetReviveCountdown(seconds);
            Refresh();
        }

        public void SetReviveCountdown(float seconds)
        {
            if (reviveCountdownText != null)
                reviveCountdownText.text = Mathf.CeilToInt(Mathf.Max(0f, seconds)).ToString();
        }

        public void HideRevive()
        {
            if (revivePanel != null)
                revivePanel.SetActive(false);
        }

        public void ShowGiftPanel(int current, int total, string reward, bool allOpened)
        {
            if (giftPanel == null)
                return;
            giftPanel.SetActive(true);
            if (giftProgressText != null)
                giftProgressText.text = allOpened ? "ALL BOXES OPENED" : "GIFT BOX " + current + " / " + total;
            if (giftRewardText != null)
                giftRewardText.text = reward;
            if (giftActionText != null)
                giftActionText.text = reward.Length == 0 ? "TAP TO OPEN" : "TAP TO CONTINUE";
            if (giftOpenButton != null)
                giftOpenButton.SetActive(!allOpened);
            if (giftConfirmButton != null)
                giftConfirmButton.SetActive(allOpened);
            if (giftPanel.TryGetComponent(out GiftChestPresenter presenter))
                presenter.Show(reward, allOpened);
        }

        public void HideGiftPanel()
        {
            if (giftPanel == null)
                return;
            if (giftPanel.TryGetComponent(out GiftChestPresenter presenter))
                presenter.Hide();
            giftPanel.SetActive(false);
        }
    }
}
