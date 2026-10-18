using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // One box_Dailyquest in Popup_Dailyquest: description, gauge, "7 / 25",
    // reward icon + amount, and the dim overlay shown once completed.
    public sealed class DailyQuestBoxView : MonoBehaviour
    {
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image progressFill;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TMP_Text rewardAmountText;
        [SerializeField] private Image dimOverlay;

        public RectTransform RewardIcon => rewardIcon != null ? rewardIcon.rectTransform : (RectTransform)transform;
        public Sprite RewardSprite => rewardIcon != null ? rewardIcon.sprite : null;
        public Image DimOverlay => dimOverlay;

        // dimmed: how far the overlay is shown right now (0 = clear, 1 = fully dimmed).
        public void Show(DailyQuestManager.Slot slot, float dimAlpha, bool dimmed)
        {
            if (slot == null || slot.Definition == null)
                return;
            int progress = Mathf.Min(slot.Progress, slot.Target);
            if (descriptionText != null)
                descriptionText.text = slot.Definition.description;
            if (progressFill != null)
                progressFill.fillAmount = slot.Progress01;
            if (progressText != null)
                progressText.text = progress + " / " + slot.Target;
            if (rewardAmountText != null)
                rewardAmountText.text = slot.Definition.rewardAmount.ToString("N0", CultureInfo.InvariantCulture);
            SetDim(dimmed ? dimAlpha : 0f);
        }

        public void SetDim(float alpha)
        {
            if (dimOverlay == null)
                return;
            Color c = dimOverlay.color;
            c.a = alpha;
            dimOverlay.color = c;
            dimOverlay.enabled = alpha > 0.001f;
        }
    }
}
