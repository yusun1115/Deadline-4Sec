using System;
using UnityEngine;

namespace Deadline4Sec
{
    // What a daily quest counts. Counter / collection sources add what
    // PlayerStats records after the quest was handed out (never lifetime
    // totals); Run* sources are the best value reached inside one run.
    public enum DailyQuestSource
    {
        EnemyKills,
        AirEnemyKills,
        NearMisses,
        Stomps,
        GroundSlams,
        SoulsCollected,
        GiftBoxesCollected,
        PowerUpsCollected,
        ShieldUses,
        DashUses,
        RunsPlayed,
        RunBestCombo,
        RunDistance,
    }

    public enum DailyQuestRewardType { Soul }

    // Any: both modes. Deadline / Classic: only runs of that mode count.
    public enum DailyQuestModeRestriction { Any, DeadlineOnly, ClassicOnly }

    [Serializable]
    public sealed class DailyQuestDefinition
    {
        public string id;
        public string description;
        public DailyQuestSource source;
        [Min(1)] public int targetValue = 1;
        public DailyQuestRewardType rewardType = DailyQuestRewardType.Soul;
        [Min(1)] public int rewardAmount = 100;
        public DailyQuestModeRestriction modeRestriction = DailyQuestModeRestriction.Any;

        public bool IsRunCondition => source == DailyQuestSource.RunBestCombo || source == DailyQuestSource.RunDistance;

        public bool Allows(GameMode? mode) =>
            modeRestriction == DailyQuestModeRestriction.Any ||
            (mode == GameMode.Deadline && modeRestriction == DailyQuestModeRestriction.DeadlineOnly) ||
            (mode == GameMode.Classic && modeRestriction == DailyQuestModeRestriction.ClassicOnly);

        public DailyQuestDefinition() { }

        public DailyQuestDefinition(string id, string description, DailyQuestSource source, int target, int reward)
        {
            this.id = id;
            this.description = description;
            this.source = source;
            targetValue = target;
            rewardAmount = reward;
        }
    }
}
