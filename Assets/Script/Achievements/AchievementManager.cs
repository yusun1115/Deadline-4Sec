using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadline4Sec
{
    // Local achievements: re-checks every definition when PlayerStats change,
    // unlocks each one once (saved as "AchievementUnlocked_<ID>") and queues it
    // for AchievementPopup. Progress is never saved separately — it is read
    // from PlayerStats (and from owned skins / item levels), so spending Soul
    // or restarting never lowers it.
    //
    // DeadlineOnly gate: Counter / OneShot DeadlineOnly achievements read
    // Deadline-only stats (Classic never adds to them, see PlayerStats), and
    // RunCondition DeadlineOnly achievements are only checked while a Deadline
    // run is active (CanProgress below).
    public static class AchievementManager
    {
        public const string UnlockKeyPrefix = "AchievementUnlocked_";

        private static AchievementCatalog catalog;
        private static AchievementDefinition[] definitions;
        private static readonly HashSet<string> unlocked = new HashSet<string>();
        private static readonly Queue<AchievementDefinition> popupQueue = new Queue<AchievementDefinition>();
        private static readonly Dictionary<string, int> loggedProgress = new Dictionary<string, int>();
        private static GameExtrasCatalog extras;
        private static bool initialized;

        // Raised once per unlock (after it is saved).
        public static event Action<AchievementDefinition> Unlocked;

        public static IReadOnlyList<AchievementDefinition> Definitions
        {
            get
            {
                EnsureInitialized();
                return definitions;
            }
        }

        public static float PopupSeconds => catalog != null ? catalog.popupSeconds : 2.6f;
        public static int UnlockedCount
        {
            get
            {
                EnsureInitialized();
                return unlocked.Count;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            initialized = false;
            catalog = null;
            definitions = null;
            extras = null;
            unlocked.Clear();
            popupQueue.Clear();
            loggedProgress.Clear();
            PlayerStats.Changed -= Evaluate;
        }

        public static void EnsureInitialized()
        {
            if (initialized)
                return;
            initialized = true;
            catalog = Resources.Load<AchievementCatalog>(AchievementCatalog.ResourcePath);
            definitions = catalog != null && catalog.achievements != null && catalog.achievements.Length > 0
                ? catalog.achievements
                : AchievementCatalog.Defaults();
            PlayerStatsDebug.Logs = catalog != null && catalog.showAchievementDebugLogs;
            unlocked.Clear();
            foreach (AchievementDefinition definition in definitions)
                if (PlayerPrefs.GetInt(UnlockKeyPrefix + definition.id, 0) != 0)
                    unlocked.Add(definition.id);
            PlayerStats.Changed -= Evaluate;
            PlayerStats.Changed += Evaluate;
            // Picks up what saves already show (skins owned, items upgraded).
            Evaluate();
        }

        public static bool IsUnlocked(string id)
        {
            EnsureInitialized();
            return unlocked.Contains(id);
        }

        public static AchievementDefinition Find(string id)
        {
            EnsureInitialized();
            return Array.Find(definitions, d => d.id == id);
        }

        public static int Target(AchievementDefinition definition) =>
            definition.source == AchievementSource.AllSkinsOwned ? PurchasableSkinCount() : definition.targetValue;

        // Current progress value (not clamped). Run sources read the live run
        // and are 0 outside one.
        public static int Progress(AchievementDefinition definition)
        {
            RunStats run = PlayerStats.Run;
            bool live = PlayerStats.RunActive;
            switch (definition.source)
            {
                case AchievementSource.TotalEnemyKills: return PlayerStats.Get(PlayerStat.TotalEnemyKills);
                case AchievementSource.TotalHomingKills: return PlayerStats.Get(PlayerStat.TotalHomingKills);
                case AchievementSource.TotalStomps: return PlayerStats.Get(PlayerStat.TotalStomps);
                case AchievementSource.TotalGroundSlams: return PlayerStats.Get(PlayerStat.TotalGroundSlams);
                case AchievementSource.DeadlineTimerResets: return PlayerStats.Get(PlayerStat.TotalDeadlineTimerResets);
                case AchievementSource.DeadlineNearMisses: return PlayerStats.Get(PlayerStat.TotalDeadlineNearMisses);
                case AchievementSource.TotalCoinsCollected: return PlayerStats.Get(PlayerStat.TotalCoinsCollected);
                case AchievementSource.TotalGiftBoxesOpened: return PlayerStats.Get(PlayerStat.TotalGiftBoxesOpened);
                case AchievementSource.TotalShieldBlocks: return PlayerStats.Get(PlayerStat.TotalShieldBlocks);
                case AchievementSource.TotalReviveCouponsUsed: return PlayerStats.Get(PlayerStat.TotalReviveCouponsUsed);
                case AchievementSource.OwnedSkins:
                case AchievementSource.AllSkinsOwned: return OwnedSkinCount();
                case AchievementSource.ItemUpgradeLevels: return ItemLevels(false);
                case AchievementSource.HighestItemLevel: return ItemLevels(true);
                case AchievementSource.RunBestCombo: return live ? run.BestCombo : 0;
                case AchievementSource.RunNearMisses: return live ? run.NearMisses : 0;
                case AchievementSource.RunTime: return live ? Mathf.FloorToInt(run.Time) : 0;
                case AchievementSource.RunTimeWithoutPowerUp: return live && run.PowerUps == 0 ? Mathf.FloorToInt(run.Time) : 0;
                case AchievementSource.RunBestAirChain: return live ? run.BestAirChain : 0;
                case AchievementSource.RunBestSlamKills: return live ? run.BestSlamKills : 0;
                default: return 0;
            }
        }

        // Whether this achievement may move right now (the mode gate).
        public static bool CanProgress(AchievementDefinition definition)
        {
            if (definition.progressType != AchievementProgressType.RunCondition)
                return true;
            if (!PlayerStats.RunActive)
                return false;
            return !definition.IsDeadlineOnly || PlayerStats.Run.Mode == GameMode.Deadline;
        }

        public static void Evaluate()
        {
            EnsureInitialized();
            foreach (AchievementDefinition definition in definitions)
            {
                if (unlocked.Contains(definition.id) || !CanProgress(definition))
                    continue;
                int target = Target(definition);
                int progress = Progress(definition);
                if (PlayerStatsDebug.Logs && definition.progressType == AchievementProgressType.Counter &&
                    (!loggedProgress.TryGetValue(definition.id, out int last) || last != progress))
                {
                    loggedProgress[definition.id] = progress;
                    Debug.Log("Achievement Progress: " + definition.id + " " + progress + " / " + target);
                }
                if (target > 0 && progress >= target)
                    UnlockAchievement(definition.id);
            }
        }

        // The single unlock point: local save, then (later) the platform report,
        // then the popup. Returns false when it was already unlocked.
        public static bool UnlockAchievement(string achievementId)
        {
            EnsureInitialized();
            AchievementDefinition definition = Find(achievementId);
            if (definition == null || !unlocked.Add(achievementId))
                return false;
            PlayerPrefs.SetInt(UnlockKeyPrefix + achievementId, 1);
            PlayerStats.Save();
            PlayerPrefs.Save();
            if (PlayerStatsDebug.Logs)
                Debug.Log("Achievement Unlocked: " + achievementId);
            ReportToPlatform(definition);
            popupQueue.Enqueue(definition);
            Unlocked?.Invoke(definition);
            return true;
        }

        public static bool TryDequeuePopup(out AchievementDefinition definition)
        {
            if (popupQueue.Count > 0)
            {
                definition = popupQueue.Dequeue();
                return true;
            }
            definition = null;
            return false;
        }

        public static int PendingPopups => popupQueue.Count;

        // Google Play Games (definition.googlePlayId) through the shared
        // GooglePlayService; no gameplay script calls a platform API directly.
        private static void ReportToPlatform(AchievementDefinition definition) =>
            GooglePlayService.ReportAchievement(definition.googlePlayId);

        // ---- Debug (editor menu only) --------------------------------------

        public static string ProgressReport()
        {
            EnsureInitialized();
            var text = new System.Text.StringBuilder("Achievements " + unlocked.Count + " / " + definitions.Length);
            foreach (AchievementDefinition definition in definitions)
                text.Append('\n').Append(unlocked.Contains(definition.id) ? "[x] " : "[ ] ")
                    .Append(definition.id).Append(' ').Append(Progress(definition)).Append(" / ").Append(Target(definition));
            return text.ToString();
        }

        public static void DeleteSave()
        {
            EnsureInitialized();
            foreach (AchievementDefinition definition in definitions)
                PlayerPrefs.DeleteKey(UnlockKeyPrefix + definition.id);
            PlayerPrefs.Save();
            Reload();
        }

        // Reads the unlock save again (tests, debug reset). Pending popups are dropped.
        public static void Reload()
        {
            initialized = false;
            popupQueue.Clear();
            loggedProgress.Clear();
            EnsureInitialized();
            popupQueue.Clear();
        }

        // ---- Sources outside PlayerStats -----------------------------------

        private static GameExtrasCatalog Extras()
        {
            if (extras == null)
                extras = Resources.Load<GameExtrasCatalog>("GameExtras/GameExtrasCatalog");
            return extras;
        }

        private static int PurchasableSkinCount()
        {
            GameExtrasCatalog skins = Extras();
            return skins != null && skins.Skins != null ? Mathf.Max(0, skins.Skins.Length - 1) : 0;
        }

        private static int OwnedSkinCount()
        {
            GameExtrasCatalog skins = Extras();
            int owned = 0;
            for (int i = 1; skins != null && skins.Skins != null && i < skins.Skins.Length; i++)
                if (RunInventory.IsSkinOwned(skins, i))
                    owned++;
            return owned;
        }

        private static int ItemLevels(bool highest)
        {
            int result = 0;
            foreach (PowerUpType type in (PowerUpType[])Enum.GetValues(typeof(PowerUpType)))
            {
                int level = PowerUpManager.SavedLevel(type);
                result = highest ? Mathf.Max(result, level) : result + level - 1;
            }
            return result;
        }
    }
}
