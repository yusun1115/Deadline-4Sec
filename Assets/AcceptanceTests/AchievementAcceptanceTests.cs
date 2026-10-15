using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    // Achievement.md §30: PlayerStats → AchievementManager (Global / DeadlineOnly,
    // unlock once, save, popup queue). Every stat / unlock / skin / item level
    // key the tests touch is backed up and restored.
    public sealed class AchievementAcceptanceTests
    {
        private const string ScenePath = "Assets/Scenes/EndlessRun.unity";
        private static readonly string[] RecordKeys =
        {
            "Deadline4Sec.BestScore", "Deadline4Sec.BestDistance", "Deadline4Sec.BestCombo",
            "Deadline4Sec.BestScore_Classic", "Deadline4Sec.BestDistance_Classic", "Deadline4Sec.BestCombo_Classic",
            "TotalCoins", "Deadline4Sec.TutorialCompleted",
        };

        private static Type T(string name)
        {
            Type type = Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
            Assert.IsNotNull(type, name);
            return type;
        }

        private static object S(string type, string method, params object[] args) =>
            T(type).GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);

        private static object Mode(string mode) => Enum.Parse(T("GameMode"), mode);
        private static int Stat(string name) => (int)S("PlayerStats", "Get", Enum.Parse(T("PlayerStat"), name));
        private static bool Unlocked(string id) => (bool)S("AchievementManager", "IsUnlocked", id);
        private static object Run() => T("PlayerStats").GetProperty("Run").GetValue(null);
        private static int RunField(string field) => (int)Run().GetType().GetField(field).GetValue(Run());
        private static int Pending() => (int)T("AchievementManager").GetProperty("PendingPopups").GetValue(null);

        private static object Definition(string id) => S("AchievementManager", "Find", id);
        private static int Progress(string id) => (int)S("AchievementManager", "Progress", Definition(id));

        private static void Begin(string mode) => S("PlayerStats", "BeginRun", Mode(mode));
        private static void Kill(bool air = false, bool homing = false, bool stomp = false) =>
            S("PlayerStats", "RecordEnemyKill", air, homing, stomp);
        private static void UpdateRun(float time, int combo) => S("PlayerStats", "UpdateRun", time, 0f, 0, combo);
        private static void End(string mode) => S("PlayerStats", "EndRun", Mode(mode), 0, 0f, 0, 0f);

        private static IEnumerable<string> Keys()
        {
            foreach (string name in Enum.GetNames(T("PlayerStat")))
                yield return "Stats_" + name;
            yield return "Stats_LowestTimerBeforeReset";
            foreach (object definition in (Array)T("AchievementCatalog").GetMethod("Defaults").Invoke(null, null))
                yield return "AchievementUnlocked_" + definition.GetType().GetField("id").GetValue(definition);
            foreach (string type in Enum.GetNames(T("PowerUpType")))
                yield return "ItemLevel_" + type;
            ScriptableObject extras = Resources.Load("GameExtras/GameExtrasCatalog") as ScriptableObject;
            Array skins = (Array)extras.GetType().GetProperty("Skins").GetValue(extras);
            foreach (object skin in skins)
                yield return "SkinOwned_" + skin.GetType().GetField("id").GetValue(skin);
            foreach (string key in RecordKeys)
                yield return key;
        }

        // The backup lives in SessionState so it survives the domain reload of
        // the play-mode test below. Keys are listed before any is deleted.
        private const string BackupKey = "Deadline4Sec.AchievementTests.Backup";

        [SetUp]
        public void SetUp()
        {
            var backup = new System.Text.StringBuilder();
            foreach (string key in new List<string>(Keys()))
            {
                if (PlayerPrefs.HasKey(key))
                {
                    bool isFloat = key.Contains("Distance") || key.Contains("LowestTimer");
                    backup.Append(key).Append('\t').Append(isFloat ? "f" : "i").Append('\t')
                        .Append(isFloat ? PlayerPrefs.GetFloat(key).ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                            : PlayerPrefs.GetInt(key).ToString()).Append('\n');
                }
                else
                    backup.Append(key).Append("\t-\t\n");
                if (!Array.Exists(RecordKeys, k => k == key))
                    PlayerPrefs.DeleteKey(key);
            }
            // Never overwrite a backup that is still pending (a resumed test).
            if (UnityEditor.SessionState.GetString(BackupKey, "") == "")
                UnityEditor.SessionState.SetString(BackupKey, backup.ToString());
            PlayerPrefs.Save();
            S("PlayerStats", "Reload");
            S("AchievementManager", "Reload");
        }

        [TearDown]
        public void TearDown()
        {
            string backup = UnityEditor.SessionState.GetString(BackupKey, "");
            foreach (string line in backup.Split('\n'))
            {
                string[] parts = line.Split('\t');
                if (parts.Length < 3)
                    continue;
                if (parts[1] == "-")
                    PlayerPrefs.DeleteKey(parts[0]);
                else if (parts[1] == "f")
                    PlayerPrefs.SetFloat(parts[0], float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
                else
                    PlayerPrefs.SetInt(parts[0], int.Parse(parts[2]));
            }
            UnityEditor.SessionState.EraseString(BackupKey);
            PlayerPrefs.Save();
            S("PlayerStats", "Reload");
            S("AchievementManager", "Reload");
        }

        [Test]
        public void FirstKillUnlocksAndKillsOutsideARunDoNotCount()
        {
            Kill();
            Assert.AreEqual(0, Stat("TotalEnemyKills"), "Kills outside a run (title / tutorial) are not counted.");
            Begin("Deadline");
            Kill();
            Assert.AreEqual(1, Stat("TotalEnemyKills"));
            Assert.AreEqual(1, Stat("TotalGroundEnemyKills"));
            Assert.IsTrue(Unlocked("ACH_FIRST_KILL"));
        }

        [Test]
        public void ClassicCountsGlobalButNeverDeadlineProgress()
        {
            Begin("Classic");
            Kill();
            S("PlayerStats", "RecordTimerReset", 1.5f);
            for (int i = 0; i < 6; i++)
                S("PlayerStats", "RecordNearMiss");
            UpdateRun(61f, 60);
            Assert.IsTrue(Unlocked("ACH_FIRST_KILL"), "Global achievements work in Classic.");
            Assert.AreEqual(6, Stat("TotalNearMisses"));
            Assert.AreEqual(0, Stat("TotalDeadlineNearMisses"));
            Assert.AreEqual(0, Stat("TotalDeadlineTimerResets"));
            Assert.AreEqual(0, Progress("ACH_NEAR_MISS_100"), "Classic Near Miss must not move the Deadline counter.");
            foreach (string id in new[] { "ACH_FIRST_TIMER_RESET", "ACH_FIRST_NEAR_MISS", "ACH_NEAR_MISS_5_RUN",
                         "ACH_SURVIVE_10", "ACH_SURVIVE_60", "ACH_COMBO_50", "ACH_NO_POWERUP_60" })
                Assert.IsFalse(Unlocked(id), id + " is DeadlineOnly.");
            Assert.IsTrue(Unlocked("ACH_COMBO_10"));
            Assert.IsTrue(Unlocked("ACH_COMBO_25"));
        }

        [Test]
        public void DeadlineTimerResetNearMissAndSurvival()
        {
            Begin("Deadline");
            S("PlayerStats", "RecordTimerReset", 0.4f);
            S("PlayerStats", "RecordTimerReset", 1.9f);
            Assert.IsTrue(Unlocked("ACH_FIRST_TIMER_RESET"));
            Assert.AreEqual(2, Stat("TotalDeadlineTimerResets"));
            Assert.AreEqual(0.4f, (float)T("PlayerStats").GetProperty("LowestTimerBeforeReset").GetValue(null), 0.0001f);

            for (int i = 0; i < 4; i++)
                S("PlayerStats", "RecordNearMiss");
            Assert.IsTrue(Unlocked("ACH_FIRST_NEAR_MISS"));
            Assert.IsFalse(Unlocked("ACH_NEAR_MISS_5_RUN"));
            S("PlayerStats", "RecordNearMiss");
            Assert.IsTrue(Unlocked("ACH_NEAR_MISS_5_RUN"));
            Assert.AreEqual(5, Progress("ACH_NEAR_MISS_100"));

            UpdateRun(59.5f, 0);
            Assert.IsTrue(Unlocked("ACH_SURVIVE_10"));
            Assert.IsFalse(Unlocked("ACH_SURVIVE_60"));
            UpdateRun(60.2f, 0);
            Assert.IsTrue(Unlocked("ACH_SURVIVE_60"));
            Assert.IsTrue(Unlocked("ACH_NO_POWERUP_60"), "No power-up was collected this run.");
            Assert.IsFalse(Unlocked("ACH_SURVIVE_180"));
        }

        [Test]
        public void PowerUpBlocksTheHiddenNoPowerUpAchievement()
        {
            Begin("Deadline");
            S("PlayerStats", "RecordPowerUpCollected");
            UpdateRun(61f, 0);
            Assert.IsTrue(Unlocked("ACH_SURVIVE_60"));
            Assert.IsFalse(Unlocked("ACH_NO_POWERUP_60"));
        }

        [Test]
        public void AirChainCountsConsecutiveHomingKillsAndResetsOnLanding()
        {
            Begin("Classic");
            Kill(air: true, homing: true);
            Kill(air: true, homing: true);
            S("PlayerStats", "RecordLanded");
            Kill(air: true, homing: true);
            Kill(air: true, homing: true);
            Assert.IsTrue(Unlocked("ACH_FIRST_HOMING"));
            Assert.IsFalse(Unlocked("ACH_AIR_COMBO_3"), "Landing ends the chain.");
            Kill(air: true, homing: true);
            Assert.IsTrue(Unlocked("ACH_AIR_COMBO_3"));
            Assert.AreEqual(3, RunField("BestAirChain"));
            for (int i = 0; i < 6; i++)
                Kill(air: true, homing: true);
            Assert.IsFalse(Unlocked("ACH_AIR_COMBO_10"));
            Kill(air: true, homing: true);
            Assert.IsTrue(Unlocked("ACH_AIR_COMBO_10"));
            Assert.AreEqual(12, Stat("TotalHomingKills"));
            Assert.AreEqual(12, Stat("TotalAirEnemyKills"));
        }

        [Test]
        public void StompAndGroundSlamStatsAreSeparate()
        {
            Begin("Deadline");
            Kill(stomp: true);
            Assert.IsTrue(Unlocked("ACH_FIRST_STOMP"));
            Assert.AreEqual(1, Stat("TotalStomps"));

            for (int i = 0; i < 2; i++)
                S("PlayerStats", "RecordGroundSlamKill", false);
            S("PlayerStats", "RecordGroundSlam", 2);
            Assert.IsTrue(Unlocked("ACH_FIRST_GROUND_SLAM"));
            Assert.IsFalse(Unlocked("ACH_GROUND_SLAM_TRIPLE"));
            for (int i = 0; i < 3; i++)
                S("PlayerStats", "RecordGroundSlamKill", i == 2);
            S("PlayerStats", "RecordGroundSlam", 3);
            Assert.IsTrue(Unlocked("ACH_GROUND_SLAM_TRIPLE"));
            Assert.AreEqual(2, Stat("TotalGroundSlams"));
            Assert.AreEqual(5, Stat("TotalGroundSlamKills"));
            Assert.AreEqual(6, Stat("TotalEnemyKills"), "1 stomp kill + 5 slam kills");
            Assert.AreEqual(1, Stat("TotalAirEnemyKills"));
        }

        [Test]
        public void ComboFiftyNeedsDeadline()
        {
            Begin("Classic");
            UpdateRun(5f, 55);
            Assert.IsTrue(Unlocked("ACH_COMBO_10"));
            Assert.IsTrue(Unlocked("ACH_COMBO_25"));
            Assert.IsFalse(Unlocked("ACH_COMBO_50"));
            End("Classic");
            Begin("Deadline");
            UpdateRun(5f, 50);
            Assert.IsTrue(Unlocked("ACH_COMBO_50"));
        }

        [Test]
        public void SoulsAreCumulativeAndSurviveARestart()
        {
            Begin("Deadline");
            S("PlayerStats", "RecordCoins", 600);
            End("Deadline");
            Assert.IsFalse(Unlocked("ACH_COIN_1000"));
            Begin("Classic");
            S("PlayerStats", "RecordCoins", 450);
            End("Classic");
            Assert.IsTrue(Unlocked("ACH_COIN_1000"));
            Assert.AreEqual(2, Stat("TotalRuns"));
            Assert.AreEqual(1, Stat("TotalClassicRuns"));

            // Spending the wallet does not touch the lifetime count; a restart keeps it.
            PlayerPrefs.SetInt("TotalCoins", 0);
            S("PlayerStats", "Reload");
            S("AchievementManager", "Reload");
            Assert.AreEqual(1050, Stat("TotalCoinsCollected"));
            Assert.AreEqual(1050, Progress("ACH_COIN_10000"));
            Assert.IsTrue(Unlocked("ACH_COIN_1000"));
        }

        [Test]
        public void GiftBoxOpeningsSkinsUpgradesShieldAndRevive()
        {
            for (int i = 0; i < 10; i++)
                S("PlayerStats", "RecordGiftBoxOpened");
            Assert.AreEqual(10, Stat("TotalGiftBoxesOpened"));
            Assert.IsTrue(Unlocked("ACH_GIFT_BOX_10"));

            ScriptableObject extras = Resources.Load("GameExtras/GameExtrasCatalog") as ScriptableObject;
            Array skins = (Array)extras.GetType().GetProperty("Skins").GetValue(extras);
            string firstPaid = (string)skins.GetValue(1).GetType().GetField("id").GetValue(skins.GetValue(1));
            PlayerPrefs.SetInt("SkinOwned_" + firstPaid, 1);
            S("PlayerStats", "RecordSkinPurchase");
            Assert.IsTrue(Unlocked("ACH_FIRST_SKIN"));
            Assert.IsFalse(Unlocked("ACH_ALL_SKINS"));
            for (int i = 1; i < skins.Length; i++)
                PlayerPrefs.SetInt("SkinOwned_" + skins.GetValue(i).GetType().GetField("id").GetValue(skins.GetValue(i)), 1);
            S("PlayerStats", "RecordSkinPurchase");
            Assert.IsTrue(Unlocked("ACH_ALL_SKINS"));

            PlayerPrefs.SetInt("ItemLevel_SoulMagnet", 6);
            S("PlayerStats", "RecordItemUpgrade");
            Assert.IsTrue(Unlocked("ACH_FIRST_UPGRADE"));
            Assert.IsFalse(Unlocked("ACH_MAX_UPGRADE"));
            PlayerPrefs.SetInt("ItemLevel_SoulMagnet", 7);
            S("PlayerStats", "RecordItemUpgrade");
            Assert.IsTrue(Unlocked("ACH_MAX_UPGRADE"));

            Begin("Classic");
            S("PlayerStats", "RecordShieldUse");
            S("PlayerStats", "RecordShieldBlock");
            S("PlayerStats", "RecordCouponRevive");
            Assert.IsTrue(Unlocked("ACH_FIRST_SHIELD_BLOCK"));
            Assert.IsTrue(Unlocked("ACH_FIRST_REVIVE"));
            Assert.AreEqual(1, Stat("TotalRevives"));
            Assert.AreEqual(1, Stat("TotalReviveCouponsUsed"));
        }

        [Test]
        public void UnlockHappensOnceAndPopupsQueueInOrder()
        {
            Assert.AreEqual(0, Pending());
            Begin("Deadline");
            S("PlayerStats", "RecordGiftBoxOpened");
            Kill(stomp: true); // first kill + first stomp at the same moment
            Assert.AreEqual(2, Pending());
            Assert.IsFalse((bool)S("AchievementManager", "UnlockAchievement", "ACH_FIRST_KILL"));
            Kill(stomp: true);
            Assert.AreEqual(2, Pending(), "Already unlocked achievements never queue again.");

            S("PlayerStats", "Reload");
            S("AchievementManager", "Reload");
            Assert.IsTrue(Unlocked("ACH_FIRST_KILL"), "Unlocks are saved.");
            Assert.Greater(Stat("TotalStomps"), 0, "An unlock saves the stats with it.");
            Assert.AreEqual(0, Pending());
            Begin("Deadline");
            Kill();
            Assert.AreEqual(0, Pending(), "No popup again after a restart.");
        }

        [Test]
        public void PopupQueueHandsOutOneAtATime()
        {
            Begin("Deadline");
            Kill(stomp: true);
            object[] args = { null };
            MethodInfo dequeue = T("AchievementManager").GetMethod("TryDequeuePopup");
            Assert.IsTrue((bool)dequeue.Invoke(null, args));
            Assert.AreEqual("ACH_FIRST_KILL", args[0].GetType().GetField("id").GetValue(args[0]));
            Assert.IsTrue((bool)dequeue.Invoke(null, args));
            Assert.AreEqual("ACH_FIRST_STOMP", args[0].GetType().GetField("id").GetValue(args[0]));
            Assert.IsFalse((bool)dequeue.Invoke(null, args));
        }

        // The recorder wires the real game events: a Deadline run counts timer
        // resets and Near Miss, the run end is recorded, and nothing counts
        // after it. A Classic run never resets the timer.
        [UnityTest]
        public IEnumerator RealRunFeedsStatsThroughTheRecorder()
        {
            PlayerPrefs.SetInt("Deadline4Sec.TutorialCompleted", 1);
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            // The first play session of a batch run may need a frame for the scene.
            yield return Until(() => UnityEngine.Object.FindFirstObjectByType(T("GameFlowManager")) != null);
            S("PlayerStats", "Reload");
            S("AchievementManager", "Reload");
            // Lambdas below must not capture locals: the closure object would be
            // created before EnterPlayMode and is not restored after the reload.
            Component flow = (Component)UnityEngine.Object.FindFirstObjectByType(T("GameFlowManager"));
            Assert.IsNotNull(flow.GetComponent(T("PlayerStatsRecorder")), "Recorder attaches itself to the game flow.");
            BindingFlags hidden = BindingFlags.NonPublic | BindingFlags.Instance;
            flow.GetType().GetField("introDuration", hidden).SetValue(flow, 0.1f);
            flow.GetType().GetField("resultDelay", hidden).SetValue(flow, 0.01f);
            flow.GetType().GetField("deathPresentationDuration", hidden).SetValue(flow, 0.2f);
            S("GameModeState", "Select", Mode("Deadline"));
            flow.GetType().GetMethod("StartGame").Invoke(flow, null);
            yield return Until(() => FlowState() == "Playing");
            Assert.IsTrue((bool)T("PlayerStats").GetProperty("RunActive").GetValue(null));

            Component timer = (Component)UnityEngine.Object.FindFirstObjectByType(T("GameTimer"));
            Component run = (Component)UnityEngine.Object.FindFirstObjectByType(T("RunManager"));
            int resetsBefore = Stat("TotalDeadlineTimerResets");
            timer.GetType().GetMethod("ResetTimer").Invoke(timer, null);
            run.GetType().GetMethod("RecordNearMiss").Invoke(run, null);
            Assert.AreEqual(resetsBefore + 1, Stat("TotalDeadlineTimerResets"));
            Assert.AreEqual(1, Stat("TotalDeadlineNearMisses"));
            Assert.IsTrue(Unlocked("ACH_FIRST_TIMER_RESET"));
            Assert.IsTrue(Unlocked("ACH_FIRST_NEAR_MISS"));

            timer.GetType().GetMethod("TriggerGameOver").Invoke(timer, null);
            yield return Until(() => FlowState() == "RevivePrompt" || FlowState() == "Result");
            Assert.AreEqual(1, Stat("TotalDeaths"));
            if (FlowState() == "RevivePrompt")
                flow.GetType().GetMethod("GiveUpRevive").Invoke(flow, null);
            yield return Until(() => !(bool)T("PlayerStats").GetProperty("RunActive").GetValue(null));
            Assert.AreEqual(1, Stat("TotalRuns"));
            Assert.AreEqual(1, Stat("TotalDeadlineRuns"));
            run.GetType().GetMethod("RecordNearMiss").Invoke(run, null);
            Assert.AreEqual(1, Stat("TotalNearMisses"), "Nothing counts after the run ended.");
            yield return new ExitPlayMode();
            S("GameModeState", "Select", Mode("Deadline"));
        }

        private static string FlowState()
        {
            UnityEngine.Object flow = UnityEngine.Object.FindFirstObjectByType(T("GameFlowManager"));
            return flow.GetType().GetProperty("State").GetValue(flow).ToString();
        }

        private static IEnumerator Until(Func<bool> condition, float seconds = 6f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                System.Threading.Thread.Sleep(1);
                yield return null;
            }
            Assert.IsTrue(condition(), "Timed out waiting.");
        }
    }
}
