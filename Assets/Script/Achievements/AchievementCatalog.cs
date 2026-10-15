using UnityEngine;
using static Deadline4Sec.AchievementCategory;
using static Deadline4Sec.AchievementModeRestriction;
using static Deadline4Sec.AchievementProgressType;
using static Deadline4Sec.AchievementSource;

namespace Deadline4Sec
{
    // The achievement list (Achievement.md §12–19), edited in the Inspector at
    // Resources/Achievements/AchievementCatalog. Without the asset the built-in
    // defaults below are used, so the list exists exactly once in code.
    [CreateAssetMenu(menuName = "Deadline 4 Sec/Achievement Catalog", fileName = "AchievementCatalog")]
    public sealed class AchievementCatalog : ScriptableObject
    {
        public const string ResourcePath = "Achievements/AchievementCatalog";

        [Tooltip("Logs stat changes, achievement progress and unlocks to the Console.")]
        public bool showAchievementDebugLogs;
        [Min(0.5f)] public float popupSeconds = 2.6f;
        public AchievementDefinition[] achievements = Defaults();

        public static AchievementDefinition[] Defaults() => new[]
        {
            // A. 기본 / 튜토리얼형
            new AchievementDefinition("ACH_FIRST_KILL", "첫 수확", "적을 처음으로 처치했습니다.",
                Combat, Global, OneShot, TotalEnemyKills, 1),
            new AchievementDefinition("ACH_FIRST_TIMER_RESET", "아직 4초 남았다", "Deadline 모드에서 생존 타이머를 처음으로 갱신했습니다.",
                Survival, DeadlineOnly, OneShot, DeadlineTimerResets, 1),
            new AchievementDefinition("ACH_FIRST_NEAR_MISS", "위험하게 살아남기", "Deadline 모드에서 Near Miss에 처음 성공했습니다.",
                Risk, DeadlineOnly, OneShot, DeadlineNearMisses, 1),
            new AchievementDefinition("ACH_FIRST_HOMING", "공중 사냥꾼", "호밍 공격으로 공중 적을 처음 처치했습니다.",
                Combat, Global, OneShot, TotalHomingKills, 1),
            new AchievementDefinition("ACH_FIRST_STOMP", "머리를 조심해", "적을 처음으로 밟아 처치했습니다.",
                Movement, Global, OneShot, TotalStomps, 1),
            new AchievementDefinition("ACH_FIRST_GROUND_SLAM", "중력의 사신", "Ground Slam에 처음 성공했습니다.",
                Movement, Global, OneShot, TotalGroundSlams, 1),

            // 전투 / 콤보
            new AchievementDefinition("ACH_AIR_COMBO_3", "공중 콤보!", "공중 적 3마리를 연속으로 처치했습니다.",
                Combat, Global, RunCondition, RunBestAirChain, 3),
            new AchievementDefinition("ACH_AIR_COMBO_10", "하늘은 내 영역", "착지하지 않고 공중 적 10마리를 연속으로 처치했습니다.",
                Combat, Global, RunCondition, RunBestAirChain, 10),
            new AchievementDefinition("ACH_GROUND_SLAM_TRIPLE", "충격파", "Ground Slam 한 번으로 적 3마리 이상을 처치했습니다.",
                Combat, Global, RunCondition, RunBestSlamKills, 3),
            new AchievementDefinition("ACH_COMBO_10", "콤보 시작", "한 판에서 콤보 10을 달성했습니다.",
                Combat, Global, RunCondition, RunBestCombo, 10),
            new AchievementDefinition("ACH_COMBO_25", "멈추지 마", "한 판에서 콤보 25를 달성했습니다.",
                Combat, Global, RunCondition, RunBestCombo, 25),
            new AchievementDefinition("ACH_COMBO_50", "DEADLINE BREAKER", "Deadline 모드에서 콤보 50을 달성했습니다.",
                Combat, DeadlineOnly, RunCondition, RunBestCombo, 50),

            // Near Miss
            new AchievementDefinition("ACH_NEAR_MISS_5_RUN", "간발의 차이", "Deadline 모드 한 판에서 Near Miss 5회에 성공했습니다.",
                Risk, DeadlineOnly, RunCondition, RunNearMisses, 5),
            new AchievementDefinition("ACH_NEAR_MISS_100", "죽음과의 거리 1cm", "Deadline 모드에서 Near Miss를 누적 100회 성공했습니다.",
                Risk, DeadlineOnly, Counter, DeadlineNearMisses, 100),

            // Survival
            new AchievementDefinition("ACH_SURVIVE_10", "10초 생존자", "Deadline 모드 한 판에서 10초 동안 살아남았습니다.",
                Survival, DeadlineOnly, RunCondition, RunTime, 10),
            new AchievementDefinition("ACH_SURVIVE_60", "1분의 사신", "Deadline 모드 한 판에서 60초 동안 살아남았습니다.",
                Survival, DeadlineOnly, RunCondition, RunTime, 60),
            new AchievementDefinition("ACH_SURVIVE_180", "시간을 거스른 자", "Deadline 모드 한 판에서 180초 동안 살아남았습니다.",
                Survival, DeadlineOnly, RunCondition, RunTime, 180),

            // Collection (in-game the currency is called Soul)
            new AchievementDefinition("ACH_COIN_1000", "첫 월급", "Soul을 누적 1,000개 획득했습니다.",
                Collection, Global, Counter, TotalCoinsCollected, 1000),
            new AchievementDefinition("ACH_COIN_10000", "소울 사냥꾼", "Soul을 누적 10,000개 획득했습니다.",
                Collection, Global, Counter, TotalCoinsCollected, 10000),
            new AchievementDefinition("ACH_GIFT_BOX_10", "수집가", "선물상자를 누적 10개 열었습니다.",
                Collection, Global, Counter, TotalGiftBoxesOpened, 10),
            new AchievementDefinition("ACH_GIFT_BOX_50", "선물 중독", "선물상자를 누적 50개 열었습니다.",
                Collection, Global, Counter, TotalGiftBoxesOpened, 50),

            // Upgrade / Skin
            new AchievementDefinition("ACH_FIRST_SKIN", "새 낫이 필요해", "무기 스킨을 처음 구매했습니다.",
                Upgrade, Global, OneShot, OwnedSkins, 1),
            new AchievementDefinition("ACH_ALL_SKINS", "사신의 옷장", "모든 무기 스킨을 구매했습니다.",
                Upgrade, Global, Counter, AllSkinsOwned, 1),
            new AchievementDefinition("ACH_FIRST_UPGRADE", "강화의 시작", "아이템을 처음으로 업그레이드했습니다.",
                Upgrade, Global, OneShot, ItemUpgradeLevels, 1),
            new AchievementDefinition("ACH_MAX_UPGRADE", "완벽한 강화", "아이템 하나를 Lv.7 MAX까지 강화했습니다.",
                Upgrade, Global, Counter, HighestItemLevel, 7),

            // Consumable / Revive
            new AchievementDefinition("ACH_FIRST_SHIELD_BLOCK", "방패는 한 번뿐", "Shield로 치명적인 충돌을 처음 막아냈습니다.",
                Meta, Global, OneShot, TotalShieldBlocks, 1),
            new AchievementDefinition("ACH_FIRST_REVIVE", "한 번 더", "부활 쿠폰으로 처음 부활했습니다.",
                Meta, Global, OneShot, TotalReviveCouponsUsed, 1),

            // Hidden
            new AchievementDefinition("ACH_NO_POWERUP_60", "빈손으로 끝까지", "Deadline 모드에서 파워업 없이 60초 동안 살아남았습니다.",
                Survival, DeadlineOnly, RunCondition, RunTimeWithoutPowerUp, 60, hidden: true),
        };
    }
}
