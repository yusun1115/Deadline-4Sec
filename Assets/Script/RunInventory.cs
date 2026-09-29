using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadline4Sec
{
    public sealed class RunInventory : MonoBehaviour
    {
        public const string EquippedSkinKey = "EquippedWeaponSkin";
        public const string EquippedConsumableKey = "EquippedConsumable";
        public const string ShieldCountKey = "Consumable_Shield_Count";
        public const string DashCountKey = "Consumable_Dash_Count";
        public const string CouponCountKey = "ReviveCouponCount";
        public const string PendingRewardKey = "PendingGiftReward";
        public const string PendingRewardAmountKey = "PendingGiftRewardAmount";
        public const string PendingRewardCurrencyKey = "PendingGiftRewardCurrency";
        public const string PendingRewardAppliedKey = "PendingGiftRewardApplied";

        [SerializeField] private GameExtrasCatalog catalog;
        [SerializeField] private CoinWallet wallet;
        [SerializeField] private WeaponSkinVisual weaponVisual;
        [Header("Consumable dash")]
        [SerializeField, Min(0.1f)] private float dashDistance = 8f;
        [SerializeField, Min(0.05f)] private float dashDuration = 0.28f;
        [Header("Collision protection")]
        [SerializeField, Range(1.5f, 2f)] private float reviveProtectionSeconds = 1.75f;
        [SerializeField, Min(0f)] private float shieldBreakGraceSeconds = 0.3f;

        private float dashUntil;
        private float collisionProtectionUntil;
        private float shieldContactGraceUntil;
        private Collider shieldBlockedCollider;
        private Component shieldBlockedHazard;
        private CharacterController playerCollider;
        private bool shieldActive;
        public event Action Changed;
        public GameExtrasCatalog Catalog => catalog;
        public int CurrentRunGiftBoxes { get; private set; }
        public bool ReviveUsed { get; private set; }
        public bool ShieldActive => shieldActive;
        public bool CollisionProtected => Time.time < collisionProtectionUntil;
        public float DashBonusSpeed => Time.time < dashUntil ? dashDistance / dashDuration : 0f;
        public int EquippedSkinIndex => Mathf.Clamp(PlayerPrefs.GetInt(EquippedSkinKey, 0), 0,
            catalog != null && catalog.Skins != null ? catalog.Skins.Length - 1 : 0);
        public ConsumableKind EquippedConsumable => (ConsumableKind)Mathf.Clamp(
            PlayerPrefs.GetInt(EquippedConsumableKey, 0), 0, 2);
        public int ShieldCount => Mathf.Max(0, PlayerPrefs.GetInt(ShieldCountKey, 0));
        public int DashCount => Mathf.Max(0, PlayerPrefs.GetInt(DashCountKey, 0));
        public int CouponCount => Mathf.Max(0, PlayerPrefs.GetInt(CouponCountKey, 0));
        public bool HasPendingReward => PlayerPrefs.GetInt(PendingRewardKey, -1) >= 0;

        private void Awake()
        {
            if (catalog == null)
                catalog = Resources.Load<GameExtrasCatalog>("GameExtras/GameExtrasCatalog");
            if (wallet == null)
                wallet = GetComponent<CoinWallet>();
            if (weaponVisual == null)
                weaponVisual = FindFirstObjectByType<WeaponSkinVisual>();
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null)
                playerCollider = player.GetComponent<CharacterController>();
            if (catalog == null)
                Debug.LogError("GameExtrasCatalog is missing.", this);
            ApplySkinVisual();
        }

        public void BeginRun()
        {
            CurrentRunGiftBoxes = 0;
            ReviveUsed = false;
            shieldActive = false;
            dashUntil = collisionProtectionUntil = 0f;
            shieldBlockedCollider = null;
            shieldBlockedHazard = null;
            shieldContactGraceUntil = 0f;
            Changed?.Invoke();
        }

        public void CollectGiftBox()
        {
            CurrentRunGiftBoxes++;
            Changed?.Invoke();
        }

        public bool OwnsSkin(int index) => index == 0 ||
            (catalog != null && index > 0 && index < catalog.Skins.Length &&
             PlayerPrefs.GetInt("SkinOwned_" + catalog.Skins[index].id, 0) != 0);

        public int CurrencyCount(SkinCurrency currency) => currency == SkinCurrency.None ? 0 :
            Mathf.Max(0, PlayerPrefs.GetInt("Currency_" + currency, 0));

        public bool TryBuyOrEquipSkin(int index)
        {
            if (catalog == null || wallet == null || index < 0 || index >= catalog.Skins.Length)
                return false;
            GameExtrasCatalog.Skin skin = catalog.Skins[index];
            if (!OwnsSkin(index))
            {
                if (wallet.TotalCoins < skin.coinCost || CurrencyCount(skin.currency) < skin.currencyCost)
                    return false;
                if (!wallet.TrySpend(skin.coinCost, false))
                    return false;
                if (skin.currency != SkinCurrency.None)
                    PlayerPrefs.SetInt("Currency_" + skin.currency,
                        CurrencyCount(skin.currency) - skin.currencyCost);
                PlayerPrefs.SetInt("SkinOwned_" + skin.id, 1);
            }
            PlayerPrefs.SetInt(EquippedSkinKey, index);
            PlayerPrefs.Save();
            ApplySkinVisual();
            Changed?.Invoke();
            return true;
        }

        public void EquipConsumable(ConsumableKind kind)
        {
            PlayerPrefs.SetInt(EquippedConsumableKey, (int)kind);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public bool TryUseEquipped()
        {
            if (GetComponent<GameTimer>() is GameTimer timer && !timer.IsRunning)
                return false;
            switch (EquippedConsumable)
            {
                case ConsumableKind.Shield when ShieldCount > 0 && !shieldActive:
                    PlayerPrefs.SetInt(ShieldCountKey, ShieldCount - 1);
                    shieldActive = true;
                    Debug.Log("Shield active");
                    break;
                case ConsumableKind.Dash when DashCount > 0 && Time.time >= dashUntil:
                    PlayerPrefs.SetInt(DashCountKey, DashCount - 1);
                    dashUntil = Time.time + dashDuration;
                    Debug.Log("Dash consumable used");
                    break;
                default:
                    return false;
            }
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        // Called only for enemy and obstacle contact; timer expiry uses GameTimer.TriggerGameOver.
        public bool AbsorbFatalContact(Collider source = null)
        {
            if (CollisionProtected)
                return true;
            Component hazard = source != null ? HazardOf(source) : null;
            bool sameHazard = source != null &&
                (hazard != null && hazard == shieldBlockedHazard ||
                 hazard == null && source == shieldBlockedCollider);
            if (sameHazard)
            {
                if (Time.time < shieldContactGraceUntil || IsStillTouching(source) ||
                    IsStillTouching(shieldBlockedCollider))
                    return true;
                shieldBlockedCollider = null;
                shieldBlockedHazard = null;
            }
            if (!shieldActive)
                return false;
            shieldActive = false;
            shieldBlockedCollider = source;
            shieldBlockedHazard = hazard;
            shieldContactGraceUntil = Time.time + shieldBreakGraceSeconds;
            Debug.Log("Shield break: fatal contact absorbed");
            Changed?.Invoke();
            return true;
        }

        public void Revive()
        {
            ReviveUsed = true;
            shieldActive = false;
            dashUntil = 0f;
            shieldBlockedCollider = null;
            shieldBlockedHazard = null;
            collisionProtectionUntil = Time.time + reviveProtectionSeconds;
            Changed?.Invoke();
        }

        public bool TrySpendCoupon()
        {
            if (ReviveUsed || CouponCount <= 0)
                return false;
            PlayerPrefs.SetInt(CouponCountKey, CouponCount - 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        public string RollAndGrantGiftReward()
        {
            if (catalog == null || catalog.Rewards == null || catalog.Rewards.Length == 0)
                return "NO REWARD TABLE";
            if (!HasPendingReward)
            {
                int index = catalog.RollRewardIndex(UnityEngine.Random.Range(0, catalog.TotalRewardWeight));
                GameExtrasCatalog.Reward entry = catalog.Rewards[index];
                SkinCurrency currency = SkinCurrency.None;
                if (entry.kind == GiftRewardKind.SkinCurrency)
                {
                    List<SkinCurrency> eligible = new List<SkinCurrency>();
                    for (int i = 1; i < catalog.Skins.Length; i++)
                        if (catalog.Skins[i].currency != SkinCurrency.None && !OwnsSkin(i) &&
                            !eligible.Contains(catalog.Skins[i].currency))
                            eligible.Add(catalog.Skins[i].currency);
                    if (eligible.Count > 0)
                        currency = eligible[UnityEngine.Random.Range(0, eligible.Count)];
                }
                PlayerPrefs.SetInt(PendingRewardKey, index);
                PlayerPrefs.SetInt(PendingRewardAmountKey, entry.amount);
                PlayerPrefs.SetInt(PendingRewardCurrencyKey, (int)currency);
                PlayerPrefs.SetInt(PendingRewardAppliedKey, 0);
                PlayerPrefs.Save();
            }
            return ApplyPendingReward();
        }

        public string ApplyPendingReward()
        {
            int index = PlayerPrefs.GetInt(PendingRewardKey, -1);
            if (catalog == null || index < 0 || index >= catalog.Rewards.Length)
                return "";
            GameExtrasCatalog.Reward reward = catalog.Rewards[index];
            int amount = PlayerPrefs.GetInt(PendingRewardAmountKey, reward.amount);
            SkinCurrency currency = (SkinCurrency)PlayerPrefs.GetInt(PendingRewardCurrencyKey, 0);
            if (PlayerPrefs.GetInt(PendingRewardAppliedKey, 0) == 0)
            {
                switch (reward.kind)
                {
                    case GiftRewardKind.Coin:
                        wallet.CreditTotalCoins(amount, false);
                        break;
                    case GiftRewardKind.SkinCurrency:
                        if (currency == SkinCurrency.None)
                            wallet.CreditTotalCoins(300, false);
                        else
                            PlayerPrefs.SetInt("Currency_" + currency, AddSafe(CurrencyCount(currency), amount));
                        break;
                    case GiftRewardKind.ReviveCoupon:
                        PlayerPrefs.SetInt(CouponCountKey, AddSafe(CouponCount, amount));
                        break;
                    case GiftRewardKind.Shield:
                        PlayerPrefs.SetInt(ShieldCountKey, AddSafe(ShieldCount, amount));
                        break;
                    case GiftRewardKind.Dash:
                        PlayerPrefs.SetInt(DashCountKey, AddSafe(DashCount, amount));
                        break;
                }
                PlayerPrefs.SetInt(PendingRewardAppliedKey, 1);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
            return reward.kind == GiftRewardKind.SkinCurrency
                ? currency == SkinCurrency.None ? "COIN +300" : currency + " +" + amount
                : reward.kind == GiftRewardKind.Coin ? "COIN +" + amount
                : reward.kind == GiftRewardKind.ReviveCoupon ? "REVIVE COUPON +" + amount
                : reward.kind.ToString().ToUpperInvariant() + " +" + amount;
        }

        public void ClearPendingReward()
        {
            PlayerPrefs.DeleteKey(PendingRewardKey);
            PlayerPrefs.DeleteKey(PendingRewardAmountKey);
            PlayerPrefs.DeleteKey(PendingRewardCurrencyKey);
            PlayerPrefs.DeleteKey(PendingRewardAppliedKey);
            PlayerPrefs.Save();
        }

        private void ApplySkinVisual()
        {
            if (weaponVisual != null && catalog != null && catalog.Skins.Length > 0)
                weaponVisual.Apply(catalog.Skins[EquippedSkinIndex].weaponMaterial);
        }

        private bool IsStillTouching(Collider source)
        {
            if (playerCollider == null || source == null)
                return false;
            Bounds zone = source.bounds;
            zone.Expand(new Vector3(0.2f, 0.2f, 0.2f));
            return zone.Intersects(playerCollider.bounds);
        }

        private static Component HazardOf(Collider source)
        {
            Enemy enemy = source.GetComponentInParent<Enemy>();
            if (enemy != null)
                return enemy;
            return source.GetComponentInParent<Obstacle>();
        }

        private static int AddSafe(int current, int amount) =>
            current > int.MaxValue - amount ? int.MaxValue : current + amount;
    }
}
