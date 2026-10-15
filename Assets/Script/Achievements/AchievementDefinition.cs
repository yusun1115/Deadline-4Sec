using System;
using UnityEngine;

namespace Deadline4Sec
{
    public enum AchievementCategory { Combat, Movement, Risk, Survival, Collection, Upgrade, Meta }

    // Global: any mode. DeadlineOnly: progress and unlock in Deadline only.
    public enum AchievementModeRestriction { Global, DeadlineOnly }

    // OneShot: one action. Counter: lifetime total. RunCondition: inside one run.
    public enum AchievementProgressType { OneShot, Counter, RunCondition }

    // Where an achievement reads its progress. Counter / OneShot sources are
    // lifetime values (DeadlineOnly ones use Deadline-only stats, so Classic
    // never moves them); Run* sources are read only during a run.
    public enum AchievementSource
    {
        TotalEnemyKills,
        TotalHomingKills,
        TotalStomps,
        TotalGroundSlams,
        DeadlineTimerResets,
        DeadlineNearMisses,
        TotalCoinsCollected,
        TotalGiftBoxesOpened,
        TotalShieldBlocks,
        TotalReviveCouponsUsed,
        OwnedSkins,        // purchased Weapon Skins (default skin excluded)
        AllSkinsOwned,     // same, target = every purchasable skin
        ItemUpgradeLevels, // levels bought across all power-ups
        HighestItemLevel,  // best power-up level (MAX = 7)
        RunBestCombo,
        RunNearMisses,
        RunTime,
        RunTimeWithoutPowerUp,
        RunBestAirChain,
        RunBestSlamKills,
    }

    [Serializable]
    public sealed class AchievementDefinition
    {
        public string id;
        public string displayName;
        [TextArea(1, 3)] public string description;
        public AchievementCategory category;
        public AchievementModeRestriction modeRestriction;
        public AchievementProgressType progressType;
        public AchievementSource source;
        [Min(1)] public int targetValue = 1;
        public bool isHidden;
        // Filled in when Google Play Games is connected (not used yet).
        public string googlePlayId;

        public bool IsDeadlineOnly => modeRestriction == AchievementModeRestriction.DeadlineOnly;

        public AchievementDefinition() { }

        public AchievementDefinition(string id, string name, string description, AchievementCategory category,
            AchievementModeRestriction mode, AchievementProgressType type, AchievementSource source, int target,
            bool hidden = false)
        {
            this.id = id;
            displayName = name;
            this.description = description;
            this.category = category;
            modeRestriction = mode;
            progressType = type;
            this.source = source;
            targetValue = target;
            isHidden = hidden;
        }
    }
}
