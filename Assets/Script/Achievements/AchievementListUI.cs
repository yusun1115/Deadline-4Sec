using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Deadline4Sec
{
    // Title achievement list (TitlePanel/AchievementPanel): achievementButton
    // On Click → Open, BackButton → Close. Rows are cloned from the inactive
    // RowTemplate, one per definition, and refreshed on every Open.
    public sealed class AchievementListUI : MonoBehaviour
    {
        [SerializeField] private AchievementRow rowTemplate;
        [SerializeField] private RectTransform content;
        [SerializeField] private TMP_Text countText;

        private readonly List<AchievementRow> rows = new List<AchievementRow>();

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Refresh();
            Click();
        }

        public void Close()
        {
            Click();
            gameObject.SetActive(false);
        }

        public void Refresh()
        {
            IReadOnlyList<AchievementDefinition> definitions = AchievementManager.Definitions;
            if (rowTemplate != null && content != null)
            {
                rowTemplate.gameObject.SetActive(false);
                while (rows.Count < definitions.Count)
                {
                    AchievementRow row = Instantiate(rowTemplate, content);
                    row.name = "Row_" + rows.Count;
                    rows.Add(row);
                }
                for (int i = 0; i < rows.Count; i++)
                {
                    bool used = i < definitions.Count;
                    rows[i].gameObject.SetActive(used);
                    if (used)
                        rows[i].Show(definitions[i]);
                }
            }
            if (countText != null)
                countText.text = AchievementManager.UnlockedCount + " / " + definitions.Count;
        }

        private void Click()
        {
            CameraFeedbackController feedback = FindFirstObjectByType<CameraFeedbackController>();
            if (feedback != null)
                feedback.PlayUIClick();
        }
    }
}
