using UnityEngine;

namespace Deadline4Sec
{
    // The title's QuestButton (icon_quest): keeps its RedDot on while a
    // completed quest's reward is waiting, and checks the date once a second so
    // a midnight reset shows up while the title is open. On Click → the popup's
    // Open is wired in the Inspector.
    public sealed class DailyQuestButton : MonoBehaviour
    {
        [SerializeField] private GameObject redDot;

        private float nextCheck;

        private void OnEnable()
        {
            DailyQuestManager.Changed += Refresh;
            DailyQuestManager.CheckDate();
            Refresh();
        }

        private void OnDisable() => DailyQuestManager.Changed -= Refresh;

        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
                return;
            nextCheck = Time.unscaledTime + 1f;
            DailyQuestManager.CheckDate(); // raises Changed (→ Refresh) when the day turned
        }

        public void Refresh()
        {
            if (redDot != null)
                redDot.SetActive(DailyQuestManager.HasUnclaimedRewards);
        }
    }
}
