using System;
using UnityEngine;

namespace Deadline4Sec
{
    public enum SoulSource { DailyQuest, RewardedAd, Purchase, GiftBox, Debug }

    // The one entry point for Soul given outside a run (shop ads, purchases,
    // daily quests). It adds to the existing CoinWallet / "TotalCoins" save, so
    // skins, upgrades and old saves keep working unchanged.
    public static class SoulBank
    {
        // amount, source — after the Soul was added (and saved when asked).
        public static event Action<int, SoulSource> SoulGranted;

        // The live wallet only while playing: outside play mode a scene's
        // wallet has not loaded its balance yet, so the save is used directly.
        private static CoinWallet Wallet() =>
            Application.isPlaying ? UnityEngine.Object.FindFirstObjectByType<CoinWallet>() : null;

        public static int TotalSoul
        {
            get
            {
                CoinWallet wallet = Wallet();
                return wallet != null ? wallet.TotalCoins : Mathf.Max(0, PlayerPrefs.GetInt(CoinWallet.TotalCoinsKey, 0));
            }
        }

        // save: false when the caller saves together with its own state
        // (e.g. a quest's Claimed flag) in one PlayerPrefs.Save.
        public static int GrantSoul(int amount, SoulSource source, bool save = true)
        {
            if (amount <= 0)
                return TotalSoul;
            CoinWallet wallet = Wallet();
            if (wallet != null)
                wallet.CreditTotalCoins(amount, save);
            else
            {
                int total = Mathf.Max(0, PlayerPrefs.GetInt(CoinWallet.TotalCoinsKey, 0));
                PlayerPrefs.SetInt(CoinWallet.TotalCoinsKey, total > int.MaxValue - amount ? int.MaxValue : total + amount);
                if (save)
                    PlayerPrefs.Save();
            }
            SoulGranted?.Invoke(amount, source);
            return TotalSoul;
        }
    }
}
