using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // One upgrade row: icon, level pips, item name, "now → next" value, an
    // upgrade arrow button and its coin cost. Text is kept to the minimum.
    public sealed class PowerUpUpgradeRow : MonoBehaviour
    {
        private const int MaxLevel = 7;
        private static readonly Color PipOn = new Color(1f, 0.25f, 0.6f);
        private static readonly Color PipOff = new Color(1f, 1f, 1f, 0.16f);

        [SerializeField] private PowerUpType type;
        [SerializeField] private TMP_Text nameLevelText;
        [SerializeField] private TMP_Text effectText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Image[] levelPips;
        [SerializeField] private GameObject costIcon;

        public void Upgrade()
        {
            PowerUpManager manager = FindFirstObjectByType<PowerUpManager>();
            if (manager == null || manager.GetLevel(type) >= MaxLevel)
                return;
            bool upgraded = manager.TryUpgrade(type);
            FindFirstObjectByType<UpgradeMenu>()?.Refresh();
            UpgradePopup popup = FindFirstObjectByType<UpgradePopup>(FindObjectsInactive.Include);
            if (popup != null)
                popup.Show(upgraded ? "업그레이드에 성공하였습니다!" : "Coin이 부족합니다!", upgraded);
        }

        public void Refresh(PowerUpManager manager, CoinWallet wallet)
        {
            if (manager == null || manager.Balance == null || wallet == null)
                return;
            PowerUpBalance balance = manager.Balance;
            int level = manager.GetLevel(type);
            bool max = level >= MaxLevel;
            if (nameLevelText != null)
                nameLevelText.text = DisplayName(type);
            if (effectText != null)
                effectText.text = max
                    ? Value(balance, level) + "  <color=#FF5AA0>MAX</color>"
                    : Value(balance, level) + "  <color=#B9A7C9>→</color>  <color=#7CFF9A>" + Value(balance, level + 1) + "</color>";
            if (costText != null)
            {
                costText.text = max ? "MAX" : balance.UpgradeCost(level).ToString("N0", CultureInfo.InvariantCulture);
                costText.color = !max && wallet.TotalCoins < balance.UpgradeCost(level)
                    ? new Color(1f, 0.45f, 0.45f) : new Color(1f, 0.85f, 0.4f);
            }
            if (costIcon != null)
                costIcon.SetActive(!max);
            if (upgradeButton != null)
                upgradeButton.interactable = !max;
            if (levelPips != null)
                for (int i = 0; i < levelPips.Length; i++)
                    if (levelPips[i] != null)
                        levelPips[i].color = i < level ? PipOn : PipOff;
        }

        // The headline number of each item: how long it lasts, or for Time Heart
        // how high the survival timer can go.
        private string Value(PowerUpBalance balance, int level)
        {
            float v = type == PowerUpType.TimeHeart ? balance.TimeHeartMaximum(level) : balance.Duration(type, level);
            return v.ToString("0.#", CultureInfo.InvariantCulture) + "s";
        }

        private static string DisplayName(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.FreezeClock: return "TIMER FREEZE";
                case PowerUpType.SoulAmplifier: return "SCORE x2";
                case PowerUpType.ReaperRush: return "REAPER RUSH";
                case PowerUpType.SoulMagnet: return "COIN MAGNET";
                case PowerUpType.ComboSeal: return "COMBO HOLD";
                default: return "TIME HEART";
            }
        }
    }
}
