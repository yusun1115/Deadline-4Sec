using System;
using UnityEngine;

namespace Deadline4Sec
{
    public enum SkinCurrency { None, FrostShard, BloodShard, CyberCore, EclipseFragment }
    public enum ConsumableKind { None, Shield, Dash }
    public enum GiftRewardKind { Coin, SkinCurrency, ReviveCoupon, Shield, Dash }

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
