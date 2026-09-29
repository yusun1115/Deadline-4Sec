using System;
using UnityEngine;

namespace Deadline4Sec
{
    public sealed class CoinWallet : MonoBehaviour
    {
        public const string TotalCoinsKey = "TotalCoins";
        private bool banked;
        public int TotalCoins { get; private set; }
        public int CurrentRunCoins { get; private set; }
        public event Action Changed;

        private void Awake() => TotalCoins = Mathf.Max(0, PlayerPrefs.GetInt(TotalCoinsKey, 0));

        public void BeginRun()
        {
            CurrentRunCoins = 0;
            banked = false;
            Changed?.Invoke();
        }

        public void Collect(int amount)
        {
            if (amount <= 0 || banked)
                return;
            CurrentRunCoins = CurrentRunCoins > int.MaxValue - amount
                ? int.MaxValue : CurrentRunCoins + amount;
            Changed?.Invoke();
        }

        public void BankRunCoins()
        {
            if (banked)
                return;
            banked = true;
            TotalCoins = TotalCoins > int.MaxValue - CurrentRunCoins
                ? int.MaxValue : TotalCoins + CurrentRunCoins;
            PlayerPrefs.SetInt(TotalCoinsKey, TotalCoins);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public bool TrySpend(int cost, bool save = true)
        {
            if (cost < 0 || TotalCoins < cost)
                return false;
            TotalCoins -= cost;
            PlayerPrefs.SetInt(TotalCoinsKey, TotalCoins);
            if (save)
                PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        public void CreditTotalCoins(int amount, bool save = true)
        {
            if (amount <= 0)
                return;
            TotalCoins = TotalCoins > int.MaxValue - amount ? int.MaxValue : TotalCoins + amount;
            PlayerPrefs.SetInt(TotalCoinsKey, TotalCoins);
            if (save)
                PlayerPrefs.Save();
            Changed?.Invoke();
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Add 1000 Total Coins")]
        private void AddTestCoins()
        {
            TotalCoins = TotalCoins > int.MaxValue - 1000 ? int.MaxValue : TotalCoins + 1000;
            PlayerPrefs.SetInt(TotalCoinsKey, TotalCoins);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
#endif
    }
}
