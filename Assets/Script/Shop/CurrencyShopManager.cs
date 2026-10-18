using System;
using System.Globalization;
using UnityEngine;

namespace Deadline4Sec
{
    public enum ShopEntrySource { Title, SkinShop, UpgradeShop }

    // Soul shop rules: the rewarded-ad card pays adSoulReward up to
    // adDailyLimit times per local day (same clock and date format as the daily
    // quests, DailyQuestManager.Now); IAP cards pay soulAmount + bonusSoul.
    // Soul is only granted in the success callback, once per request
    // (isProcessingRewardAd / isPurchasing), through SoulBank.GrantSoul.
    // Saved: DailySoulAdDate (yyyy-MM-dd), DailySoulAdCount.
    public static class CurrencyShopManager
    {
        public const string AdDateKey = "DailySoulAdDate";
        public const string AdCountKey = "DailySoulAdCount";
        private const string DateFormat = "yyyy-MM-dd";

        private static CurrencyShopCatalog catalog;
        private static bool isProcessingRewardAd;
        private static bool isPurchasing;

        // Count, remaining or busy state changed.
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            catalog = null;
            isProcessingRewardAd = false;
            isPurchasing = false;
        }

        public static CurrencyShopCatalog Catalog
        {
            get
            {
                if (catalog == null)
                {
                    catalog = Resources.Load<CurrencyShopCatalog>(CurrencyShopCatalog.ResourcePath);
                    if (catalog == null)
                        catalog = ScriptableObject.CreateInstance<CurrencyShopCatalog>();
                }
                return catalog;
            }
        }

        public static bool IsBusy => isProcessingRewardAd || isPurchasing;
        public static int AdDailyLimit => Mathf.Max(1, Catalog.adDailyLimit);

        // Ads watched today (resets when the saved date is not today).
        public static int AdCountToday
        {
            get
            {
                CheckAdDate();
                return Mathf.Clamp(PlayerPrefs.GetInt(AdCountKey, 0), 0, AdDailyLimit);
            }
        }

        public static int AdsRemaining => AdDailyLimit - AdCountToday;

        // Red dots (title button, ad card): an ad reward is still available today.
        public static bool HasAdRewardLeft => AdProduct != null && AdsRemaining > 0;

        public static string TodayKey() => DailyQuestManager.Now().Date.ToString(DateFormat, CultureInfo.InvariantCulture);

        // New day → count 0. Returns true when it reset.
        public static bool CheckAdDate()
        {
            string today = TodayKey();
            string saved = PlayerPrefs.GetString(AdDateKey, "");
            if (saved == today)
                return false;
            if (DateTime.TryParseExact(saved, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime last) &&
                last > DailyQuestManager.Now().Date)
                Debug.LogWarning("Soul shop: the device date went back (" + saved + " → " + today + ").");
            PlayerPrefs.SetString(AdDateKey, today);
            PlayerPrefs.SetInt(AdCountKey, 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        public static CurrencyShopProduct AdProduct =>
            Array.Find(Catalog.products, p => p.productType == CurrencyShopProductType.RewardedAd);

        // Ad card: plays a rewarded ad; on success grants the Soul, counts the
        // view and saves. done(granted) — false for no ad, closed early, limit
        // reached or a request already running (nothing changes then).
        public static void TryWatchSoulRewardAd(Action<bool> done)
        {
            CurrencyShopProduct product = AdProduct;
            if (product == null || isProcessingRewardAd || isPurchasing || AdsRemaining <= 0)
            {
                done?.Invoke(false);
                return;
            }
            isProcessingRewardAd = true;
            Changed?.Invoke();
            bool handled = false;
            RewardedAdService.Show(Catalog, watched =>
            {
                if (handled)
                    return; // a second callback for the same ad pays nothing
                handled = true;
                bool granted = false;
                if (watched && AdsRemaining > 0)
                {
                    PlayerPrefs.SetInt(AdCountKey, AdCountToday + 1);
                    SoulBank.GrantSoul(product.TotalSoul, SoulSource.RewardedAd, false);
                    PlayerPrefs.Save();
                    granted = true;
                }
                isProcessingRewardAd = false;
                Changed?.Invoke();
                done?.Invoke(granted);
            });
        }

        // IAP card: grants soulAmount + bonusSoul only on PurchaseResult.Success.
        public static void Purchase(CurrencyShopProduct product, Action<PurchaseResult> done)
        {
            if (product == null || product.productType != CurrencyShopProductType.IAP || isPurchasing || isProcessingRewardAd)
            {
                done?.Invoke(PurchaseResult.Failed);
                return;
            }
            isPurchasing = true;
            Changed?.Invoke();
            bool handled = false;
            SoulPurchaseService.Purchase(product, Catalog, result =>
            {
                if (handled)
                    return;
                handled = true;
                if (result == PurchaseResult.Success)
                    SoulBank.GrantSoul(product.TotalSoul, SoulSource.Purchase);
                isPurchasing = false;
                Changed?.Invoke();
                done?.Invoke(result);
            });
        }

        // Debug menu: forget today's ad views.
        public static void ResetAdCount()
        {
            PlayerPrefs.DeleteKey(AdDateKey);
            PlayerPrefs.DeleteKey(AdCountKey);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
