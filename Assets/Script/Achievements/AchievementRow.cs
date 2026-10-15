using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // One line of the title's achievement list (template built by
    // Deadline 4 Sec/UI/Apply Achievements; AchievementListUI clones it).
    public sealed class AchievementRow : MonoBehaviour
    {
        [SerializeField] private Image stateIcon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text tagText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private GameObject progressBar;
        [SerializeField] private Image progressFill;
        [SerializeField] private Color unlockedColor = new Color(1f, 0.32f, 0.62f);
        [SerializeField] private Color lockedNameColor = new Color(0.72f, 0.64f, 0.78f);

        public void Show(AchievementDefinition definition)
        {
            bool done = AchievementManager.IsUnlocked(definition.id);
            bool secret = definition.isHidden && !done;
            int target = Mathf.Max(1, AchievementManager.Target(definition));
            int progress = Mathf.Clamp(AchievementManager.Progress(definition), 0, target);
            // Lifetime progress is worth a bar; run conditions only show the goal.
            bool counter = !done && !secret && definition.progressType == AchievementProgressType.Counter;

            if (nameText != null)
            {
                nameText.text = secret ? "???" : definition.displayName;
                nameText.color = done ? Color.white : lockedNameColor;
            }
            if (descriptionText != null)
                descriptionText.text = secret ? "숨겨진 업적입니다." : definition.description;
            if (tagText != null)
                tagText.text = definition.IsDeadlineOnly ? "DEADLINE" : "";
            if (stateIcon != null)
                stateIcon.color = done ? Color.white : new Color(1f, 1f, 1f, 0.22f);
            if (progressBar != null)
                progressBar.SetActive(counter);
            if (progressFill != null)
                progressFill.fillAmount = progress / (float)target;
            if (progressText != null)
            {
                progressText.text = done ? "CLEAR" : counter ? progress.ToString("N0") + " / " + target.ToString("N0") : "";
                progressText.color = done ? unlockedColor : Color.white;
            }
        }
    }
}
