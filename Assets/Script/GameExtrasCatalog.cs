using System;
using UnityEngine;

namespace Deadline4Sec
{
    public enum SkinCurrency { None, FrostShard, BloodShard, CyberCore, EclipseFragment }
    public enum ConsumableKind { None, Shield, Dash }
    public enum GiftRewardKind { Coin, SkinCurrency, ReviveCoupon, Shield, Dash }

    // A weapon skin's own effects (2026-11-21, Weapon Skin Unique VFX.md). The
    // style picks the built-in procedural set (idle layers, swing shape, trail
    // texture, hit burst); the optional prefabs replace a part of it.
    [Serializable]
    public sealed class WeaponVfxProfile
    {
        public WeaponSkinFx.Effect style = WeaponSkinFx.Effect.NeonReaper;
        public Color primaryColor = new Color(1f, 0.22f, 0.55f);
        public Color secondaryColor = new Color(0.62f, 0.3f, 1f);
        [Tooltip("Idle effect strength (rate): idle stays subtle.")]
        [Range(0f, 2f)] public float idleIntensity = 1f;
        [Tooltip("Idle particle size multiplier.")]
        [Range(0.25f, 3f)] public float idleVfxScale = 1f;
        [Tooltip("Swing effect size multiplier.")]
        [Range(0.25f, 3f)] public float attackVfxScale = 1f;
        [Tooltip("How long the swing effect stays (visual only, the attack itself is unchanged).")]
        [Range(0.1f, 0.6f)] public float attackDuration = 0.16f;
        [Tooltip("Optional: replaces the style's trail material.")]
        public Material trailMaterial;
        [Tooltip("Optional: shown at the idle anchor instead of the style's idle layers.")]
        public GameObject idleVfxPrefab;
        [Tooltip("Optional: spawned (pooled) with each swing, besides the style's slash.")]
        public GameObject attackVfxPrefab;
        [Tooltip("Optional: spawned (pooled) on a kill, besides the style's hit burst.")]
        public GameObject hitVfxPrefab;
    }

    [CreateAssetMenu(menuName = "Deadline 4 Sec/Game Extras Catalog")]
    public sealed class GameExtrasCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Skin
        {
            public string id;
            public string displayName;
            public int coinCost;
            public SkinCurrency currency;
            public int currencyCost;
            public Material weaponMaterial;
            // Weapon models (2026-10-30): the skin's own weapon (null keeps the
            // character's built-in scythe), its colour for the swing trail and
            // kill sparks, and its shop-card icon.
            public GameObject weaponPrefab;
            public Color accentColor = new Color(1f, 0.22f, 0.55f);
            public Sprite icon;
            // Weapon Skin Unique VFX (2026-11-21): idle, swing, trail and hit
            // effects, all from here (WeaponVfxController).
            public WeaponVfxProfile vfx = new WeaponVfxProfile();
        }

        [Serializable]
        public sealed class Reward
        {
            public GiftRewardKind kind;
            public int amount;
            public int weight;
        }

        [SerializeField] private Skin[] skins;
        [SerializeField] private Reward[] rewards;
        public Skin[] Skins => skins;
        public Reward[] Rewards => rewards;

        public int RollRewardIndex(int ticket)
        {
            int total = 0;
            foreach (Reward reward in rewards)
                total += Mathf.Max(0, reward.weight);
            if (total <= 0)
                throw new InvalidOperationException("Gift reward table has no positive weights.");
            ticket = ((ticket % total) + total) % total;
            for (int i = 0; i < rewards.Length; i++)
            {
                ticket -= Mathf.Max(0, rewards[i].weight);
                if (ticket < 0)
                    return i;
            }
            return rewards.Length - 1;
        }

        public int TotalRewardWeight
        {
            get
            {
                int total = 0;
                foreach (Reward reward in rewards)
                    total += Mathf.Max(0, reward.weight);
                return total;
            }
        }
    }
}
