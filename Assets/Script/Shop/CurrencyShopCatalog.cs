using System;
using UnityEngine;

namespace Deadline4Sec
{
    public enum CurrencyShopProductType { RewardedAd, IAP }

    // One card of the Soul shop. Paid Soul = soulAmount + bonusSoul; the card
    // shows that total ("X5,500") and the bonus badge ("+500") from the same data.
    [Serializable]
    public sealed class CurrencyShopProduct
    {
        public string productId;
        public CurrencyShopProductType productType;
        [Min(0)] public int soulAmount;
        [Min(0)] public int bonusSoul;
        public string displayPrice;
        [Tooltip("Google Play Billing product ID (used when IAP is connected).")]
        public string iapProductId;
        public Sprite icon;

        public int TotalSoul => soulAmount + bonusSoul;

        public CurrencyShopProduct() { }

        public CurrencyShopProduct(string id, CurrencyShopProductType type, int soul, int bonus, string price, string iapId)
        {
            productId = id;
            productType = type;
            soulAmount = soul;
            bonusSoul = bonus;
            displayPrice = price;
            iapProductId = iapId;
        }
    }

    // Soul shop data (Currencyshop.md), edited in the Inspector at
    // Resources/Shop/CurrencyShopCatalog. Without the asset the defaults apply.
    [CreateAssetMenu(menuName = "Deadline 4 Sec/Currency Shop Catalog", fileName = "CurrencyShopCatalog")]
    public sealed class CurrencyShopCatalog : ScriptableObject
    {
        public const string ResourcePath = "Shop/CurrencyShopCatalog";

        public CurrencyShopProduct[] products = Defaults();
        [Min(1)] public int adDailyLimit = 3;

        [Header("Development only (Editor / development builds; never in release)")]
        [Tooltip("No ad SDK yet: pretend the rewarded ad was watched.")]
        public bool simulateRewardedAdForTesting;
        [Tooltip("With the simulation on: true = watched to the end, false = closed early (no reward).")]
        public bool simulatedAdSucceeds = true;
        [Tooltip("No IAP yet: in the Editor a purchase succeeds without paying.")]
        public bool debugPurchaseInEditor = true;
        [Tooltip("How long the simulated ad / debug purchase takes before its result.")]
        [Min(0f)] public float simulatedResponseSeconds = 0.4f;

        public static CurrencyShopProduct[] Defaults() => new[]
        {
            new CurrencyShopProduct("soul_ad_100", CurrencyShopProductType.RewardedAd, 100, 0, "FREE", ""),
            new CurrencyShopProduct("soul_pack_1000", CurrencyShopProductType.IAP, 1000, 0, "₩1,100", "soul_pack_1000"),
            new CurrencyShopProduct("soul_pack_5500", CurrencyShopProductType.IAP, 5000, 500, "₩5,500", "soul_pack_5500"),
            new CurrencyShopProduct("soul_pack_12000", CurrencyShopProductType.IAP, 10000, 2000, "₩11,000", "soul_pack_12000"),
        };
    }
}
