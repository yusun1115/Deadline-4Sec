using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Deadline4Sec
{
    // Today's three daily quests (DailyQuest.md). Progress comes only from
    // PlayerStats — Recorded(stat, amount, mode) for counters and the live run
    // values for run conditions — so no gameplay script counts anything twice
    // and lifetime totals never leak in. Quests change at local midnight
    // (checked on use and once a second by DailyQuestButton / DailyQuestPopup);
    // yesterday's unclaimed rewards expire with them. Saved in PlayerPrefs:
    // DailyQuest_CurrentDate and DailyQuest{1..3}_ID/_Progress/_Completed/_Claimed.
    public static class DailyQuestManager
    {
        public const int QuestCount = 3;
        public const string DateKey = "DailyQuest_CurrentDate";
        private const string DateFormat = "yyyy-MM-dd";

        public sealed class Slot
        {
            public DailyQuestDefinition Definition;
            public int Progress;
            public bool Completed;
            public bool Claimed;

            public int Target => Definition != null ? Definition.targetValue : 1;
            public float Progress01 => Mathf.Clamp01(Progress / (float)Mathf.Max(1, Target));
            public bool HasUnclaimedReward => Completed && !Claimed;
        }

        private static DailyQuestCatalog catalog;
        private static DailyQuestDefinition[] pool;
        private static readonly Slot[] slots = new Slot[QuestCount];
        private static DateTime questDate;
        private static bool initialized;

        // Local clock; tests replace it to cross midnight.
        public static Func<DateTime> Now = () => DateTime.Now;

        // Quests replaced, progress, completion or claim changed.
        public static event Action Changed;

        public static IReadOnlyList<Slot> Slots
        {
            get
            {
                EnsureInitialized();
                return slots;
            }
        }

        public static DateTime QuestDate
        {
            get
            {
                EnsureInitialized();
                return questDate;
            }
        }

        public static bool HasUnclaimedRewards
        {
            get
            {
                CheckDate();
                foreach (Slot slot in slots)
                    if (slot != null && slot.HasUnclaimedReward)
                        return true;
                return false;
            }
        }

        private static bool Logs => catalog != null && catalog.showDailyQuestDebugLogs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            initialized = false;
            catalog = null;
            pool = null;
            Now = () => DateTime.Now;
            PlayerStats.Recorded -= OnStatRecorded;
            PlayerStats.Changed -= OnStatsChanged;
        }

        // Listening from launch, so progress counts even before any quest UI opens.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitAtLaunch() => EnsureInitialized();

        public static void EnsureInitialized()
        {
            if (initialized)
                return;
            initialized = true;
            catalog = Resources.Load<DailyQuestCatalog>(DailyQuestCatalog.ResourcePath);
            pool = catalog != null && catalog.pool != null && catalog.pool.Length >= QuestCount
                ? catalog.pool
                : DailyQuestCatalog.Defaults();
            Load();
            PlayerStats.Recorded -= OnStatRecorded;
            PlayerStats.Recorded += OnStatRecorded;
            PlayerStats.Changed -= OnStatsChanged;
            PlayerStats.Changed += OnStatsChanged;
            CheckDate();
        }

        // ---- Day handling -------------------------------------------------------

        // New quests when the local date differs from the saved one. Returns
        // true when the quests were replaced.
        public static bool CheckDate()
        {
            EnsureInitialized();
            DateTime today = Now().Date;
            if (today == questDate && slots[0] != null)
                return false;
            if (questDate != default && today < questDate)
                Debug.LogWarning("Daily Quest: the device date went back (" + questDate.ToString(DateFormat) + " → " +
                                 today.ToString(DateFormat) + "). New quests are created for the current date.");
            Generate(today);
            return true;
        }

        // Time left until the next local 00:00.
        public static TimeSpan TimeUntilReset()
        {
            DateTime now = Now();
            return now.Date.AddDays(1) - now;
        }

        public static string ResetLabel()
        {
            TimeSpan left = TimeUntilReset();
            if (left.TotalMinutes < 1)
                return "1분 미만 후 리셋됩니다";
            int hours = (int)left.TotalHours;
            int minutes = left.Minutes;
            return hours > 0 ? hours + "시간 " + minutes + "분 후 리셋됩니다" : minutes + "분 후 리셋됩니다";
        }

        // Three different quests, and never two that count the same thing
        // (e.g. "kill 25" with "kill 50") while the pool allows it.
        private static void Generate(DateTime day)
        {
            var candidates = new List<DailyQuestDefinition>(pool);
            var chosen = new List<DailyQuestDefinition>();
            var usedSources = new HashSet<DailyQuestSource>();
            for (int pass = 0; pass < 2 && chosen.Count < QuestCount; pass++)
            {
                Shuffle(candidates);
                foreach (DailyQuestDefinition definition in candidates)
                {
                    if (chosen.Count == QuestCount)
                        break;
                    if (chosen.Contains(definition) || (pass == 0 && usedSources.Contains(definition.source)))
                        continue;
                    chosen.Add(definition);
                    usedSources.Add(definition.source);
                }
            }
            for (int i = 0; i < QuestCount; i++)
                slots[i] = new Slot { Definition = i < chosen.Count ? chosen[i] : pool[i] };
            questDate = day;
            SaveAll();
            if (Logs)
                Debug.Log("Daily Quest: new quests for " + day.ToString(DateFormat) + ": " +
                          string.Join(", ", Array.ConvertAll(slots, s => s.Definition.id)));
            Changed?.Invoke();
        }

        private static void Shuffle(List<DailyQuestDefinition> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ---- Progress (from PlayerStats only) ----------------------------------

        private static void OnStatRecorded(PlayerStat stat, int amount, GameMode? mode)
        {
            if (!TryMapStat(stat, out DailyQuestSource source))
                return;
            CheckDate();
            bool changed = false;
            foreach (Slot slot in slots)
            {
                if (slot.Completed || slot.Definition.source != source || !slot.Definition.Allows(mode))
                    continue;
                slot.Progress = slot.Progress > int.MaxValue - amount ? int.MaxValue : slot.Progress + amount;
                changed |= UpdateSlot(slot);
            }
            if (changed)
                Changed?.Invoke();
        }

        private static void OnStatsChanged()
        {
            if (!PlayerStats.RunActive)
                return;
            RunStats run = PlayerStats.Run;
            bool changed = false;
            foreach (Slot slot in slots)
            {
                if (slot == null || slot.Completed || !slot.Definition.IsRunCondition || !slot.Definition.Allows(run.Mode))
                    continue;
                int value = slot.Definition.source == DailyQuestSource.RunBestCombo ? run.BestCombo : Mathf.FloorToInt(run.Distance);
                if (value <= slot.Progress)
                    continue;
                slot.Progress = value;
                changed |= UpdateSlot(slot);
            }
            if (changed)
                Changed?.Invoke();
        }

        private static bool TryMapStat(PlayerStat stat, out DailyQuestSource source)
        {
            switch (stat)
            {
                case PlayerStat.TotalEnemyKills: source = DailyQuestSource.EnemyKills; return true;
                case PlayerStat.TotalAirEnemyKills: source = DailyQuestSource.AirEnemyKills; return true;
                case PlayerStat.TotalNearMisses: source = DailyQuestSource.NearMisses; return true;
                case PlayerStat.TotalStomps: source = DailyQuestSource.Stomps; return true;
                case PlayerStat.TotalGroundSlams: source = DailyQuestSource.GroundSlams; return true;
                case PlayerStat.TotalCoinsCollected: source = DailyQuestSource.SoulsCollected; return true;
                case PlayerStat.TotalGiftBoxesCollected: source = DailyQuestSource.GiftBoxesCollected; return true;
                case PlayerStat.TotalPowerUpsCollected: source = DailyQuestSource.PowerUpsCollected; return true;
                case PlayerStat.TotalShieldUses: source = DailyQuestSource.ShieldUses; return true;
                case PlayerStat.TotalDashUses: source = DailyQuestSource.DashUses; return true;
                case PlayerStat.TotalRuns: source = DailyQuestSource.RunsPlayed; return true;
                default: source = default; return false;
            }
        }

        // Writes the slot; returns true (always a change). Completion is saved
        // to disk right away, plain progress with the next PlayerPrefs.Save.
        private static bool UpdateSlot(Slot slot)
        {
            int index = Array.IndexOf(slots, slot);
            if (!slot.Completed && slot.Progress >= slot.Target)
            {
                slot.Completed = true;
                SaveSlot(index);
                PlayerPrefs.Save();
                if (Logs)
                    Debug.Log("Daily Quest completed: " + slot.Definition.id);
                return true;
            }
            SaveSlot(index);
            return true;
        }

        // ---- Reward ------------------------------------------------------------

        // The only place a reward is paid: currency and Claimed are written
        // together and saved at once, so a quest pays exactly one time even if
        // the popup is reopened or the app is killed during the animation.
        // (wallet is kept for callers; SoulBank finds the scene's wallet itself.)
        public static bool ClaimReward(int index, CoinWallet wallet)
        {
            EnsureInitialized();
            if (index < 0 || index >= QuestCount)
                return false;
            Slot slot = slots[index];
            if (slot == null || !slot.Completed || slot.Claimed)
                return false;
            int amount = slot.Definition.rewardAmount;
            switch (slot.Definition.rewardType)
            {
                default:
                    // The shared Soul entry point (finds the wallet itself).
                    SoulBank.GrantSoul(amount, SoulSource.DailyQuest, false);
                    break;
            }
            slot.Claimed = true;
            SaveSlot(index);
            PlayerPrefs.Save();
            if (Logs)
                Debug.Log("Daily Quest reward claimed: " + slot.Definition.id + " +" + amount);
            Changed?.Invoke();
            return true;
        }

        // ---- Save / load -------------------------------------------------------

        private static string Key(int index, string field) => "DailyQuest" + (index + 1) + "_" + field;

        private static void Load()
        {
            string saved = PlayerPrefs.GetString(DateKey, "");
            if (!DateTime.TryParseExact(saved, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out questDate))
            {
                questDate = default;
                Array.Clear(slots, 0, QuestCount);
                return;
            }
            for (int i = 0; i < QuestCount; i++)
            {
                DailyQuestDefinition definition = Array.Find(pool, d => d.id == PlayerPrefs.GetString(Key(i, "ID"), ""));
                if (definition == null)
                {
                    // A quest that left the pool: start the day over.
                    questDate = default;
                    Array.Clear(slots, 0, QuestCount);
                    return;
                }
                slots[i] = new Slot
                {
                    Definition = definition,
                    Progress = Mathf.Max(0, PlayerPrefs.GetInt(Key(i, "Progress"), 0)),
                    Completed = PlayerPrefs.GetInt(Key(i, "Completed"), 0) != 0,
                    Claimed = PlayerPrefs.GetInt(Key(i, "Claimed"), 0) != 0,
                };
            }
        }

        private static void SaveSlot(int index)
        {
            Slot slot = slots[index];
            PlayerPrefs.SetString(Key(index, "ID"), slot.Definition.id);
            PlayerPrefs.SetInt(Key(index, "Progress"), slot.Progress);
            PlayerPrefs.SetInt(Key(index, "Completed"), slot.Completed ? 1 : 0);
            PlayerPrefs.SetInt(Key(index, "Claimed"), slot.Claimed ? 1 : 0);
        }

        private static void SaveAll()
        {
            PlayerPrefs.SetString(DateKey, questDate.ToString(DateFormat, CultureInfo.InvariantCulture));
            for (int i = 0; i < QuestCount; i++)
                SaveSlot(i);
            PlayerPrefs.Save();
        }

        // ---- Tests / debug -------------------------------------------------------

        // Reads the save again (simulates an app restart).
        public static void Reload()
        {
            initialized = false;
            Array.Clear(slots, 0, QuestCount);
            EnsureInitialized();
        }

        // Debug menu and tests: today's quests become these IDs (progress 0).
        public static void SetQuests(params string[] ids)
        {
            EnsureInitialized();
            for (int i = 0; i < QuestCount; i++)
            {
                DailyQuestDefinition definition = Array.Find(pool, d => d.id == ids[i]);
                if (definition == null)
                    throw new ArgumentException("Unknown daily quest " + ids[i]);
                slots[i] = new Slot { Definition = definition };
            }
            questDate = Now().Date;
            SaveAll();
            Changed?.Invoke();
        }

        // Debug: completes every quest (rewards still need the popup).
        public static void CompleteAll()
        {
            EnsureInitialized();
            for (int i = 0; i < QuestCount; i++)
            {
                slots[i].Progress = Mathf.Max(slots[i].Progress, slots[i].Target);
                slots[i].Completed = true;
                SaveSlot(i);
            }
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // Debug: forgets today's quests (new ones on next use).
        public static void DeleteSave()
        {
            PlayerPrefs.DeleteKey(DateKey);
            for (int i = 0; i < QuestCount; i++)
                foreach (string field in new[] { "ID", "Progress", "Completed", "Claimed" })
                    PlayerPrefs.DeleteKey(Key(i, field));
            PlayerPrefs.Save();
            Reload();
        }

        public static string Report()
        {
            EnsureInitialized();
            var text = new System.Text.StringBuilder("Daily Quests " + questDate.ToString(DateFormat) + " (" + ResetLabel() + ")");
            for (int i = 0; i < QuestCount; i++)
                text.Append('\n').Append(i + 1).Append(". ").Append(slots[i].Definition.id).Append(' ')
                    .Append(slots[i].Progress).Append('/').Append(slots[i].Target)
                    .Append(slots[i].Completed ? " completed" : "").Append(slots[i].Claimed ? " claimed" : "");
            return text.ToString();
        }
    }
}
