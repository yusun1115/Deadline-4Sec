using System;
using UnityEngine;

namespace Deadline4Sec
{
    // Lifetime counters (saved as "Stats_<Name>"). Deadline-only values exist
    // only where a Deadline achievement needs them (Achievement.md §6).
    public enum PlayerStat
    {
        TotalEnemyKills,
        TotalGroundEnemyKills,
        TotalAirEnemyKills,
        TotalHomingKills,
        TotalNearMisses,
        TotalDeadlineNearMisses,
        TotalDeadlineTimerResets,
        TotalStomps,
        TotalGroundSlams,
        TotalGroundSlamKills,
        TotalCoinsCollected,
        TotalGiftBoxesCollected,
        TotalGiftBoxesOpened,
        TotalRuns,
        TotalDeadlineRuns,
        TotalClassicRuns,
        TotalDeaths,
        TotalRevives,
        TotalReviveCouponsUsed,
        TotalShieldUses,
        TotalShieldBlocks,
        TotalDashUses,
        TotalPowerUpsCollected,
        TotalItemUpgrades,
        TotalWeaponSkinsPurchased,
    }

    // Values of the current run only; cleared by PlayerStats.BeginRun.
    public sealed class RunStats
    {
        public GameMode Mode;
        public int EnemyKills;
        public int AirKills;
        public int NearMisses;
        public int Stomps;
        public int GroundSlams;
        public int Coins;
        public int GiftBoxes;
        public int PowerUps;
        public int BestCombo;
        public float Time;
        public float Distance;
        public int Score;
        // Homing kills in the current air chain (cleared on landing) and the
        // longest chain / most kills from one Ground Slam this run.
        public int AirChain;
        public int BestAirChain;
        public int BestSlamKills;

        public void Clear(GameMode mode)
        {
            Mode = mode;
            EnemyKills = AirKills = NearMisses = Stomps = GroundSlams = 0;
            Coins = GiftBoxes = PowerUps = BestCombo = Score = 0;
            AirChain = BestAirChain = BestSlamKills = 0;
            Time = Distance = 0f;
        }
    }

    // Player behaviour record shared by achievements (and later daily quests):
    // gameplay → PlayerStats → listeners. It only counts; it never judges.
    // Gameplay values count only between BeginRun and EndRun (never in the
    // tutorial); meta values (upgrades, skins, gift box openings) always count.
    // Values live in memory and are written by Save() at run end, upgrades,
    // skin purchases, gift box openings, achievement unlocks and app pause/quit.
    public static class PlayerStats
    {
        public const string KeyPrefix = "Stats_";
        public const string LowestTimerKey = KeyPrefix + "LowestTimerBeforeReset";

        private static readonly int StatCount = Enum.GetValues(typeof(PlayerStat)).Length;
        private static int[] values;
        private static float lowestTimerBeforeReset;
        private static bool dirty;

        public static RunStats Run { get; } = new RunStats();
        public static bool RunActive { get; private set; }
        public static bool IsDeadlineRun => RunActive && Run.Mode == GameMode.Deadline;
        // Survival Timer value just before the latest reset (Deadline only).
        public static float LastTimerBeforeReset { get; private set; } = -1f;

        // Any change (lifetime or run). Achievements re-check on this.
        public static event Action Changed;
        // One lifetime stat grew by amount, in the given mode (null = not in a
        // run). For future daily quests; achievements do not need it.
        public static event Action<PlayerStat, int, GameMode?> Recorded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            values = null;
            dirty = false;
            RunActive = false;
            LastTimerBeforeReset = -1f;
            Run.Clear(GameMode.Deadline);
        }

        public static int Get(PlayerStat stat)
        {
            EnsureLoaded();
            return values[(int)stat];
        }

        public static float LowestTimerBeforeReset
        {
            get
            {
                EnsureLoaded();
                return lowestTimerBeforeReset;
            }
        }

        public static string KeyOf(PlayerStat stat) => KeyPrefix + stat;

        // ---- Run lifetime -------------------------------------------------

        public static void BeginRun(GameMode mode)
        {
            EnsureLoaded();
            Run.Clear(mode);
            RunActive = true;
            Changed?.Invoke();
        }

        public static void EndRun(GameMode mode, int score, float distance, int bestCombo, float time)
        {
            if (!RunActive)
                return;
            Run.Score = score;
            Run.Distance = distance;
            Run.BestCombo = Mathf.Max(Run.BestCombo, bestCombo);
            Run.Time = Mathf.Max(Run.Time, time);
            Add(PlayerStat.TotalRuns, 1, true);
            Add(mode == GameMode.Classic ? PlayerStat.TotalClassicRuns : PlayerStat.TotalDeadlineRuns, 1, true);
            Changed?.Invoke();
            RunActive = false;
            Save();
        }

        // Live run values sampled by PlayerStatsRecorder; raises Changed only
        // when something an achievement looks at moved on.
        public static void UpdateRun(float time, float distance, int score, int bestCombo)
        {
            if (!RunActive)
                return;
            bool notify = bestCombo > Run.BestCombo || Mathf.FloorToInt(time) > Mathf.FloorToInt(Run.Time);
            Run.Time = time;
            Run.Distance = distance;
            Run.Score = score;
            Run.BestCombo = Mathf.Max(Run.BestCombo, bestCombo);
            if (notify)
                Changed?.Invoke();
        }

        // ---- Gameplay (run only) -------------------------------------------

        // One enemy killed by anything but Ground Slam (that is RecordGroundSlamKill).
        public static void RecordEnemyKill(bool isAir, bool isHoming, bool isStomp)
        {
            if (!RunActive)
                return;
            Run.EnemyKills++;
            if (isAir)
                Run.AirKills++;
            Add(PlayerStat.TotalEnemyKills, 1, false);
            Add(isAir ? PlayerStat.TotalAirEnemyKills : PlayerStat.TotalGroundEnemyKills, 1, false);
            if (isStomp)
            {
                Run.Stomps++;
                Add(PlayerStat.TotalStomps, 1, false);
            }
            if (isHoming && isAir)
            {
                Add(PlayerStat.TotalHomingKills, 1, false);
                Run.AirChain++;
                Run.BestAirChain = Mathf.Max(Run.BestAirChain, Run.AirChain);
            }
            Changed?.Invoke();
        }

        // The air chain ends when the player touches ground (or a roof).
        public static void RecordLanded()
        {
            if (Run.AirChain == 0)
                return;
            if (PlayerStatsDebug.Logs)
                Debug.Log("[Stats] Air chain ended at " + Run.AirChain);
            Run.AirChain = 0;
        }

        // One enemy killed by a Ground Slam impact.
        public static void RecordGroundSlamKill(bool isAir)
        {
            if (!RunActive)
                return;
            Run.EnemyKills++;
            if (isAir)
                Run.AirKills++;
            Add(PlayerStat.TotalEnemyKills, 1, false);
            Add(isAir ? PlayerStat.TotalAirEnemyKills : PlayerStat.TotalGroundEnemyKills, 1, false);
            Add(PlayerStat.TotalGroundSlamKills, 1, false);
            Changed?.Invoke();
        }

        // One Ground Slam landed, killing killedCount enemies (already counted
        // one by one through RecordGroundSlamKill).
        public static void RecordGroundSlam(int killedCount)
        {
            if (!RunActive)
                return;
            Run.GroundSlams++;
            Run.BestSlamKills = Mathf.Max(Run.BestSlamKills, killedCount);
            Add(PlayerStat.TotalGroundSlams, 1, false);
            Changed?.Invoke();
        }

        public static void RecordNearMiss()
        {
            if (!RunActive)
                return;
            Run.NearMisses++;
            Add(PlayerStat.TotalNearMisses, 1, false);
            if (Run.Mode == GameMode.Deadline)
                Add(PlayerStat.TotalDeadlineNearMisses, 1, false);
            Changed?.Invoke();
        }

        // A real Survival Timer reset (Deadline only); remainingBefore is the
        // timer value just before it, for "reset at 0.2 s or less" later.
        public static void RecordTimerReset(float remainingBefore)
        {
            if (!IsDeadlineRun)
                return;
            EnsureLoaded();
            LastTimerBeforeReset = remainingBefore;
            if (lowestTimerBeforeReset < 0f || remainingBefore < lowestTimerBeforeReset)
                lowestTimerBeforeReset = remainingBefore;
            Add(PlayerStat.TotalDeadlineTimerResets, 1, false);
            Changed?.Invoke();
        }

        public static void RecordCoins(int amount)
        {
            if (!RunActive || amount <= 0)
                return;
            Run.Coins += amount;
            Add(PlayerStat.TotalCoinsCollected, amount, false);
            Changed?.Invoke();
        }

        public static void RecordGiftBoxCollected()
        {
            if (!RunActive)
                return;
            Run.GiftBoxes++;
            Add(PlayerStat.TotalGiftBoxesCollected, 1, false);
            Changed?.Invoke();
        }

        public static void RecordPowerUpCollected()
        {
            if (!RunActive)
                return;
            Run.PowerUps++;
            Add(PlayerStat.TotalPowerUpsCollected, 1, false);
            Changed?.Invoke();
        }

        public static void RecordDeath() => RecordRunStat(PlayerStat.TotalDeaths);
        public static void RecordShieldUse() => RecordRunStat(PlayerStat.TotalShieldUses);
        public static void RecordShieldBlock() => RecordRunStat(PlayerStat.TotalShieldBlocks);
        public static void RecordDashUse() => RecordRunStat(PlayerStat.TotalDashUses);

        // Revive with a coupon (the only revive today; ad revives are not built).
        public static void RecordCouponRevive()
        {
            if (!RunActive)
                return;
            Add(PlayerStat.TotalRevives, 1, false);
            Add(PlayerStat.TotalReviveCouponsUsed, 1, false);
            Changed?.Invoke();
        }

        // ---- Meta (any time; saved right away) ------------------------------

        public static void RecordGiftBoxOpened() => RecordMetaStat(PlayerStat.TotalGiftBoxesOpened);
        public static void RecordItemUpgrade() => RecordMetaStat(PlayerStat.TotalItemUpgrades);
        public static void RecordSkinPurchase() => RecordMetaStat(PlayerStat.TotalWeaponSkinsPurchased);

        // ---- Save / load ----------------------------------------------------

        public static void Save()
        {
            if (values == null || !dirty)
                return;
            for (int i = 0; i < StatCount; i++)
                PlayerPrefs.SetInt(KeyOf((PlayerStat)i), values[i]);
            PlayerPrefs.SetFloat(LowestTimerKey, lowestTimerBeforeReset);
            PlayerPrefs.Save();
            dirty = false;
        }

        // Drops the in-memory values and reads them again (tests, debug reset).
        public static void Reload()
        {
            values = null;
            dirty = false;
            RunActive = false;
            Run.Clear(GameMode.Deadline);
            EnsureLoaded();
            Changed?.Invoke();
        }

        // Development only (Deadline 4 Sec/Debug menu).
        public static void DeleteSave()
        {
            for (int i = 0; i < StatCount; i++)
                PlayerPrefs.DeleteKey(KeyOf((PlayerStat)i));
            PlayerPrefs.DeleteKey(LowestTimerKey);
            PlayerPrefs.Save();
            Reload();
        }

        private static void EnsureLoaded()
        {
            if (values != null)
                return;
            values = new int[StatCount];
            for (int i = 0; i < StatCount; i++)
                values[i] = Mathf.Max(0, PlayerPrefs.GetInt(KeyOf((PlayerStat)i), 0));
            lowestTimerBeforeReset = PlayerPrefs.GetFloat(LowestTimerKey, -1f);
        }

        private static void RecordRunStat(PlayerStat stat)
        {
            if (!RunActive)
                return;
            Add(stat, 1, false);
            Changed?.Invoke();
        }

        private static void RecordMetaStat(PlayerStat stat)
        {
            Add(stat, 1, false);
            Changed?.Invoke();
            Save();
        }

        private static void Add(PlayerStat stat, int amount, bool quiet)
        {
            EnsureLoaded();
            int i = (int)stat;
            values[i] = values[i] > int.MaxValue - amount ? int.MaxValue : values[i] + amount;
            dirty = true;
            if (PlayerStatsDebug.Logs && !quiet)
                Debug.Log("[Stats] " + stat + " = " + values[i]);
            Recorded?.Invoke(stat, amount, RunActive ? Run.Mode : (GameMode?)null);
        }
    }

    // Shared switch for stats/achievement debug logs (set from the catalog's
    // showAchievementDebugLogs).
    public static class PlayerStatsDebug
    {
        public static bool Logs;
    }
}
