using UnityEngine;

namespace Deadline4Sec
{
    // The title's CurrencyshopButton: its RedDot is on while today's ad Soul
    // can still be collected and off once every view is used. Checks the date
    // once a second so the dot comes back after midnight while the title is
    // open. On Click → CurrencyShopPanel.OpenFromTitle is wired in the Inspector.
    public sealed class CurrencyShopButton : MonoBehaviour
    {
        [SerializeField] private GameObject redDot;

        private float nextCheck;

        private void OnEnable()
        {
            CurrencyShopManager.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => CurrencyShopManager.Changed -= Refresh;

        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
                return;
            nextCheck = Time.unscaledTime + 1f;
            if (!CurrencyShopManager.CheckAdDate()) // raises Changed (→ Refresh) on a new day
                Refresh();
        }

        public void Refresh()
        {
            if (redDot != null)
                redDot.SetActive(CurrencyShopManager.HasAdRewardLeft);
        }
    }
}
