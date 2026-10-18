using System;
using UnityEngine;
#if DEADLINE_DOTWEEN
using DG.Tweening;
#endif

namespace Deadline4Sec
{
    public enum PurchaseResult { Success, Cancelled, Failed }

    // Where a rewarded ad SDK (AdMob / Unity Ads / …) plugs in later: one call,
    // one callback — true only when the ad was watched to the end. No SDK is
    // installed, so it reports "not available", except the development
    // simulation (CurrencyShopCatalog.simulateRewardedAdForTesting), which is
    // compiled out of release builds.
    public static class RewardedAdService
    {
        public static void Show(CurrencyShopCatalog settings, Action<bool> done)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (settings != null && settings.simulateRewardedAdForTesting)
            {
                bool watched = settings.simulatedAdSucceeds;
                Debug.Log("Rewarded ad (simulated): " + (watched ? "watched" : "closed early"));
                Later(settings.simulatedResponseSeconds, () => done?.Invoke(watched));
                return;
            }
#endif
            Debug.Log("Rewarded ad not available (no ad SDK yet)");
            Later(0f, () => done?.Invoke(false));
        }

        internal static void Later(float seconds, Action action)
        {
#if DEADLINE_DOTWEEN
            if (seconds > 0f)
            {
                DOVirtual.DelayedCall(seconds, () => action(), true);
                return;
            }
#endif
            action();
        }
    }

    // Where Google Play Billing / Unity IAP plugs in later: Purchase(product)
    // → PurchaseResult. Until then only the Editor can "buy" (debug, free);
    // players' builds report Failed so nothing is ever given away.
    public static class SoulPurchaseService
    {
        public static void Purchase(CurrencyShopProduct product, CurrencyShopCatalog settings, Action<PurchaseResult> done)
        {
#if UNITY_EDITOR
            if (settings != null && settings.debugPurchaseInEditor)
            {
                Debug.Log("Purchase (Editor debug, free): " + product.productId + " / " + product.iapProductId);
                RewardedAdService.Later(settings.simulatedResponseSeconds, () => done?.Invoke(PurchaseResult.Success));
                return;
            }
#endif
            Debug.Log("Purchase not available yet (no IAP): " + product.productId);
            RewardedAdService.Later(0f, () => done?.Invoke(PurchaseResult.Failed));
        }
    }
}
