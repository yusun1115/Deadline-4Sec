using UnityEngine;
using static Deadline4Sec.DailyQuestSource;

namespace Deadline4Sec
{
    // The daily quest pool (DailyQuest.md §7), edited in the Inspector at
    // Resources/DailyQuests/DailyQuestCatalog. Without the asset the defaults
    // below are used. Every entry is something PlayerStats already tracks.
    [CreateAssetMenu(menuName = "Deadline 4 Sec/Daily Quest Catalog", fileName = "DailyQuestCatalog")]
    public sealed class DailyQuestCatalog : ScriptableObject
    {
        public const string ResourcePath = "DailyQuests/DailyQuestCatalog";

        public bool showDailyQuestDebugLogs;
        public DailyQuestDefinition[] pool = Defaults();

        public static DailyQuestDefinition[] Defaults() => new[]
        {
            new DailyQuestDefinition("DQ_KILL_25", "적 25마리 처치하기", EnemyKills, 25, 350),
            new DailyQuestDefinition("DQ_KILL_50", "적 50마리 처치하기", EnemyKills, 50, 600),
            new DailyQuestDefinition("DQ_AIR_KILL_10", "공중 적 10마리 처치하기", AirEnemyKills, 10, 400),
            new DailyQuestDefinition("DQ_NEAR_MISS_5", "Near Miss 5회 성공하기", NearMisses, 5, 300),
            new DailyQuestDefinition("DQ_NEAR_MISS_10", "Near Miss 10회 성공하기", NearMisses, 10, 500),
            new DailyQuestDefinition("DQ_STOMP_3", "Stomp 3회 성공하기", Stomps, 3, 300),
            new DailyQuestDefinition("DQ_STOMP_5", "Stomp 5회 성공하기", Stomps, 5, 450),
            new DailyQuestDefinition("DQ_SLAM_3", "Ground Slam 3회 사용하기", GroundSlams, 3, 350),
            new DailyQuestDefinition("DQ_SOUL_100", "Soul 100개 획득하기", SoulsCollected, 100, 250),
            new DailyQuestDefinition("DQ_SOUL_300", "Soul 300개 획득하기", SoulsCollected, 300, 500),
            new DailyQuestDefinition("DQ_GIFT_1", "선물상자 1개 획득하기", GiftBoxesCollected, 1, 300),
            new DailyQuestDefinition("DQ_GIFT_2", "선물상자 2개 획득하기", GiftBoxesCollected, 2, 500),
            new DailyQuestDefinition("DQ_COMBO_15", "콤보 15 달성하기", RunBestCombo, 15, 350),
            new DailyQuestDefinition("DQ_COMBO_25", "콤보 25 달성하기", RunBestCombo, 25, 550),
            new DailyQuestDefinition("DQ_POWERUP_3", "파워업 3개 획득하기", PowerUpsCollected, 3, 300),
            new DailyQuestDefinition("DQ_SHIELD_1", "Shield 1회 사용하기", ShieldUses, 1, 250),
            new DailyQuestDefinition("DQ_DASH_1", "Dash 1회 사용하기", DashUses, 1, 250),
            new DailyQuestDefinition("DQ_RUN_3", "3판 플레이하기", RunsPlayed, 3, 300),
            new DailyQuestDefinition("DQ_DISTANCE_500", "한 판에 500m 달리기", RunDistance, 500, 400),
        };
    }
}
