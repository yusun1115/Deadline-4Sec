using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    public sealed class PowerUpUpgradeRow : MonoBehaviour
    {
        [SerializeField] private PowerUpType type;
        [SerializeField] private TMP_Text nameLevelText;
        [SerializeField] private TMP_Text effectText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private Button upgradeButton;

        public void Upgrade()
        {
            PowerUpManager manager = FindFirstObjectByType<PowerUpManager>();
            manager?.TryUpgrade(type);
            FindFirstObjectByType<UpgradeMenu>()?.Refresh();
        }

        public void Refresh(PowerUpManager manager, CoinWallet wallet)
        {
            if (manager == null || manager.Balance == null || wallet == null)
                return;
            PowerUpBalance balance = manager.Balance;
            int level = manager.GetLevel(type);
            if (nameLevelText != null)
                nameLevelText.text = DisplayName(type) + "  Lv." + level + (level == 7 ? " MAX" : "");
            if (effectText != null)
                effectText.text = "NOW  " + balance.EffectDescription(type, level) +
                    (level < 7 ? "\nNEXT  " + balance.EffectDescription(type, level + 1) : "");
            if (costText != null)
            {
                costText.text = level == 7 ? "MAX" :
                    balance.UpgradeCost(level).ToString("N0", CultureInfo.InvariantCulture) + " COIN";
                costText.color = level < 7 && wallet.TotalCoins < balance.UpgradeCost(level)
                    ? new Color(1f, 0.45f, 0.45f) : Color.white;
            }
            if (upgradeButton != null)
            {
                upgradeButton.interactable = level < 7;
                TMP_Text buttonLabel = upgradeButton.GetComponentInChildren<TMP_Text>();
                if (buttonLabel != null)
                    buttonLabel.text = level == 7 ? "MAX" : "UPGRADE";
            }
        }

        private static string DisplayName(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.FreezeClock: return "FREEZE CLOCK";
                case PowerUpType.SoulAmplifier: return "SOUL AMPLIFIER";
                case PowerUpType.ReaperRush: return "REAPER RUSH";
                case PowerUpType.SoulMagnet: return "SOUL MAGNET";
                case PowerUpType.ComboSeal: return "COMBO SEAL";
                default: return "TIME HEART";
            }
        }
    }
}
