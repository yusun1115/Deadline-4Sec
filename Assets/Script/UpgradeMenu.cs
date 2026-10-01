using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

namespace Deadline4Sec
{
    public sealed class UpgradeMenu : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleCoinText;
        [SerializeField] private TMP_Text upgradeCoinText;
        [SerializeField] private TMP_Text skinCoinText;
        [SerializeField] private TMP_Text gameplayCoinText;
        [SerializeField] private TMP_Text activeEffectsText;
        [SerializeField] private PowerUpUpgradeRow[] rows;
        private CoinWallet wallet;
        private PowerUpManager powerUps;
        private readonly StringBuilder effectsBuilder = new StringBuilder(128);

        private void Start()
        {
            wallet = GetComponent<CoinWallet>();
            powerUps = GetComponent<PowerUpManager>();
            if (wallet != null)
                wallet.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (wallet != null)
                wallet.Changed -= Refresh;
        }

        private void Update()
        {
            if (activeEffectsText == null || powerUps == null)
                return;
            effectsBuilder.Clear();
            for (int i = 0; i < 6; i++)
            {
                PowerUpType type = (PowerUpType)i;
                if (!powerUps.IsActive(type))
                    continue;
                if (effectsBuilder.Length > 0)
                    effectsBuilder.Append('\n');
                effectsBuilder.Append(type).Append(' ')
                    .Append(powerUps.Remaining(type).ToString("0.0", CultureInfo.InvariantCulture));
            }
            activeEffectsText.text = effectsBuilder.ToString();
        }

        public void Refresh()
        {
            if (wallet == null)
                wallet = GetComponent<CoinWallet>();
            if (powerUps == null)
                powerUps = GetComponent<PowerUpManager>();
            if (wallet == null)
                return;
            string balance = "COIN " + wallet.TotalCoins.ToString("N0", CultureInfo.InvariantCulture);
            if (titleCoinText != null)
                titleCoinText.text = balance;
            // Upgrade/Skin screens show a coin icon next to the number.
            string amount = wallet.TotalCoins.ToString("N0", CultureInfo.InvariantCulture);
            if (upgradeCoinText != null)
                upgradeCoinText.text = amount;
            if (skinCoinText != null)
                skinCoinText.text = amount;
            if (gameplayCoinText != null)
                gameplayCoinText.text = "COIN " + wallet.CurrentRunCoins.ToString("N0", CultureInfo.InvariantCulture);
            if (rows != null)
                foreach (PowerUpUpgradeRow row in rows)
                    if (row != null)
                        row.Refresh(powerUps, wallet);
        }
    }

}
