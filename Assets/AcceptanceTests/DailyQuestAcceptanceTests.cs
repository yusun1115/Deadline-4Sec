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
    // DailyQuest.md §45–56: three different quests per local day that survive a
    // restart, progress only from what happens after they are handed out (both
    // modes), completion → red dot, the popup pays each reward once, midnight
    // replaces the quests, and the reset label. Every key touched is restored.
    public sealed class DailyQuestAcceptanceTests
    {
        private const string ScenePath = "Assets/Scenes/EndlessRun.unity";
        private const string BackupKey = "Deadline4Sec.DailyQuestTests.Backup";

        private static Type T(string name)
        {
            Type type = Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
            Assert.IsNotNull(type, name);
            return type;
        }

        private static object S(string type, string method, params object[] args) =>
            T(type).GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);

        private static object Mode(string mode) => Enum.Parse(T("GameMode"), mode);
        private static IList Slots() => (IList)T("DailyQuestManager").GetProperty("Slots").GetValue(null);
        private static object Slot(int i) => Slots()[i];
        private static object F(object o, string field) => o.GetType().GetField(field).GetValue(o);
        private static string Id(int i) => (string)F(F(Slot(i), "Definition"), "id");
        private static int Progress(int i) => (int)F(Slot(i), "Progress");
        private static bool Completed(int i) => (bool)F(Slot(i), "Completed");
        private static bool Claimed(int i) => (bool)F(Slot(i), "Claimed");
        private static bool HasUnclaimed() => (bool)T("DailyQuestManager").GetProperty("HasUnclaimedRewards").GetValue(null);
        private static void SetQuests(params string[] ids) => S("DailyQuestManager", "SetQuests", new object[] { ids });
        private static void Kill() => S("PlayerStats", "RecordEnemyKill", false, false, false);

        private static void SetNow(DateTime now)
        {
            FieldInfo field = T("DailyQuestManager").GetField("Now", BindingFlags.Public | BindingFlags.Static);
            field.SetValue(null, new Func<DateTime>(() => now));
        }

        private static void RealClock() =>
            T("DailyQuestManager").GetField("Now", BindingFlags.Public | BindingFlags.Static)
                .SetValue(null, new Func<DateTime>(() => DateTime.Now));

        // ---- backup (SessionState survives the play-mode domain reload) ----------

        private static IEnumerable<string> Keys()
        {
            yield return "DailyQuest_CurrentDate";
            for (int i = 1; i <= 3; i++)
                foreach (string f in new[] { "ID", "Progress", "Completed", "Claimed" })
                    yield return "DailyQuest" + i + "_" + f;
            yield return "TotalCoins";
            yield return "Deadline4Sec.TutorialCompleted";
            foreach (string name in Enum.GetNames(T("PlayerStat")))
                yield return "Stats_" + name;
            yield return "Stats_LowestTimerBeforeReset";
            foreach (object definition in (Array)T("AchievementCatalog").GetMethod("Defaults").Invoke(null, null))
                yield return "AchievementUnlocked_" + definition.GetType().GetField("id").GetValue(definition);
        }

        [SetUp]
        public void SetUp()
        {
            if (UnityEditor.SessionState.GetString(BackupKey, "") == "")
            {
                var backup = new System.Text.StringBuilder();
                foreach (string key in new List<string>(Keys()))
                {
                    if (!PlayerPrefs.HasKey(key))
                        backup.Append(key).Append("\t-\t\n");
                    else if (key.StartsWith("DailyQuest_") || key.EndsWith("_ID"))
                        backup.Append(key).Append("\ts\t").Append(PlayerPrefs.GetString(key)).Append('\n');
                    else if (key.Contains("LowestTimer"))
                        backup.Append(key).Append("\tf\t").Append(PlayerPrefs.GetFloat(key).ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
                    else
                        backup.Append(key).Append("\ti\t").Append(PlayerPrefs.GetInt(key)).Append('\n');
                }
                UnityEditor.SessionState.SetString(BackupKey, backup.ToString());
            }
            PlayerPrefs.SetInt("TotalCoins", 1000);
            PlayerPrefs.SetInt("Deadline4Sec.TutorialCompleted", 1);
            PlayerPrefs.Save();
            S("PlayerStats", "Reload");
            S("DailyQuestManager", "Reload");
        }

        [TearDown]
        public void TearDown()
        {
            RealClock();
            foreach (string line in UnityEditor.SessionState.GetString(BackupKey, "").Split('\n'))
            {
                string[] parts = line.Split('\t');
                if (parts.Length < 3)
                    continue;
                switch (parts[1])
                {
                    case "-": PlayerPrefs.DeleteKey(parts[0]); break;
                    case "s": PlayerPrefs.SetString(parts[0], parts[2]); break;
                    case "f": PlayerPrefs.SetFloat(parts[0], float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture)); break;
                    default: PlayerPrefs.SetInt(parts[0], int.Parse(parts[2])); break;
                }
            }
            UnityEditor.SessionState.EraseString(BackupKey);
            PlayerPrefs.Save();
            S("PlayerStats", "Reload");
            S("AchievementManager", "Reload");
            S("DailyQuestManager", "Reload");
        }

        // ---- tests -------------------------------------------------------------

        [Test]
        public void ThreeDifferentQuestsPerDayThatSurviveARestart()
        {
            S("DailyQuestManager", "DeleteSave");
            Assert.AreEqual(3, Slots().Count);
            var ids = new HashSet<string> { Id(0), Id(1), Id(2) };
            Assert.AreEqual(3, ids.Count, "No quest twice in a day.");
            string[] before = { Id(0), Id(1), Id(2) };
            S("DailyQuestManager", "Reload");
            CollectionAssert.AreEqual(before, new[] { Id(0), Id(1), Id(2) }, "Same quests after a restart.");
        }

        [Test]
        public void ProgressCountsOnlyTodayInBothModesAndSurvivesRestart()
        {
            PlayerPrefs.SetInt("Stats_TotalEnemyKills", 500); // lifetime total must not leak in
            S("PlayerStats", "Reload");
            SetQuests("DQ_KILL_25", "DQ_SOUL_300", "DQ_COMBO_15");
            Assert.AreEqual(0, Progress(0));

            S("PlayerStats", "BeginRun", Mode("Deadline"));
            for (int i = 0; i < 4; i++)
                Kill();
            S("PlayerStats", "RecordCoins", 120);
            S("PlayerStats", "EndRun", Mode("Deadline"), 0, 0f, 0, 0f);
            S("PlayerStats", "BeginRun", Mode("Classic"));
            for (int i = 0; i < 3; i++)
                Kill();
            S("PlayerStats", "UpdateRun", 4f, 10f, 100, 12);
            S("PlayerStats", "EndRun", Mode("Classic"), 0, 0f, 12, 4f);
            Kill(); // outside a run: not counted
            Assert.AreEqual(7, Progress(0), "7 / 25 from both modes.");
            Assert.AreEqual(120, Progress(1));
            Assert.AreEqual(12, Progress(2), "Best combo of a run.");
            Assert.IsFalse(HasUnclaimed());

            S("DailyQuestManager", "Reload");
            Assert.AreEqual(7, Progress(0), "Progress survives a restart.");
        }

        [Test]
        public void CompletionLightsTheRedDotAndTheRewardPaysOnce()
        {
            SetQuests("DQ_KILL_25", "DQ_GIFT_1", "DQ_COMBO_15");
            S("PlayerStats", "BeginRun", Mode("Deadline"));
            for (int i = 0; i < 25; i++)
                Kill();
            S("PlayerStats", "RecordGiftBoxCollected");
            Assert.IsTrue(Completed(0));
            Assert.IsFalse(Claimed(0));
            Assert.IsTrue(Completed(1));
            Assert.IsTrue(HasUnclaimed(), "Red dot on.");

            Assert.IsTrue((bool)S("DailyQuestManager", "ClaimReward", 0, null));
            Assert.IsFalse((bool)S("DailyQuestManager", "ClaimReward", 0, null), "Never twice.");
            Assert.AreEqual(1350, PlayerPrefs.GetInt("TotalCoins"));
            Assert.IsTrue(HasUnclaimed());
            Assert.IsTrue((bool)S("DailyQuestManager", "ClaimReward", 1, null));
            Assert.IsFalse((bool)S("DailyQuestManager", "ClaimReward", 2, null), "Not completed: nothing to claim.");
            Assert.AreEqual(1650, PlayerPrefs.GetInt("TotalCoins"));
            Assert.IsFalse(HasUnclaimed(), "Red dot off.");

            S("DailyQuestManager", "Reload");
            Assert.IsTrue(Claimed(0) && Claimed(1), "Claimed survives a restart.");
            Assert.IsFalse((bool)S("DailyQuestManager", "ClaimReward", 0, null));
            Assert.AreEqual(1650, PlayerPrefs.GetInt("TotalCoins"));
        }

        [Test]
        public void MidnightReplacesTheQuestsAndUnclaimedRewardsExpire()
        {
            DateTime today = DateTime.Now.Date.AddHours(23).AddMinutes(59);
            SetNow(today);
            SetQuests("DQ_KILL_25", "DQ_GIFT_1", "DQ_COMBO_15");
            S("PlayerStats", "BeginRun", Mode("Deadline"));
            S("PlayerStats", "RecordGiftBoxCollected");
            Assert.IsTrue(HasUnclaimed());

            SetNow(today.AddMinutes(2)); // 00:01 next day
            Assert.IsTrue((bool)S("DailyQuestManager", "CheckDate"));
            Assert.AreEqual(today.Date.AddDays(1), T("DailyQuestManager").GetProperty("QuestDate").GetValue(null));
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(0, Progress(i));
                Assert.IsFalse(Completed(i));
            }
            Assert.IsFalse(HasUnclaimed(), "Yesterday's reward is gone.");
            Assert.AreEqual(today.Date.AddDays(1).ToString("yyyy-MM-dd"), PlayerPrefs.GetString("DailyQuest_CurrentDate"));
        }

        [Test]
        public void ResetLabelCountsDownToLocalMidnight()
        {
            DateTime day = DateTime.Now.Date;
            SetNow(day.AddHours(12).AddMinutes(49));
            Assert.AreEqual("11시간 11분 후 리셋됩니다", S("DailyQuestManager", "ResetLabel"));
            SetNow(day.AddHours(23).AddMinutes(18));
            Assert.AreEqual("42분 후 리셋됩니다", S("DailyQuestManager", "ResetLabel"));
            SetNow(day.AddHours(23).AddMinutes(59).AddSeconds(30));
            Assert.AreEqual("1분 미만 후 리셋됩니다", S("DailyQuestManager", "ResetLabel"));
        }

        // Popup on the real title: rapid taps open it once, each finished
        // quest pays once (sequentially), X stays off until the animation ends,
        // the red dot goes out, and reopening pays nothing.
        [UnityTest]
        public IEnumerator PopupPaysFinishedQuestsOnceAndClosesSafely()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return Until(() => UnityEngine.Object.FindFirstObjectByType(T("GameFlowManager")) != null);
            Component wallet = (Component)UnityEngine.Object.FindFirstObjectByType(T("CoinWallet"));
            int start = (int)wallet.GetType().GetProperty("TotalCoins").GetValue(wallet);

            SetQuests("DQ_KILL_25", "DQ_GIFT_1", "DQ_COMBO_15");
            S("PlayerStats", "BeginRun", Mode("Classic"));
            for (int i = 0; i < 25; i++)
                Kill();
            S("PlayerStats", "RecordGiftBoxCollected");
            S("PlayerStats", "Reload");
            yield return null;
            Transform button = GameObject.Find("Canvas/TitlePanel/QuestButton").transform;
            Assert.IsTrue(button.Find("RedDot").gameObject.activeSelf, "Red dot on.");

            var onClick = (UnityEngine.Events.UnityEvent)button.GetComponent("Button").GetType().GetProperty("onClick").GetValue(button.GetComponent("Button"));
            onClick.Invoke();
            onClick.Invoke();
            onClick.Invoke();
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType(T("DailyQuestPopup"), FindObjectsSortMode.None).Length);
            yield return Until(() => Popup("IsClaiming"));
            Assert.IsFalse(CloseInteractable(), "X is off while rewards play.");
            Assert.AreEqual(start + 650, (int)wallet.GetType().GetProperty("TotalCoins").GetValue(wallet), "Paid before the animation.");
            Call("Close");
            Assert.IsTrue(Popup("IsOpen"), "Close is ignored while rewards play.");
            yield return Until(() => !Popup("IsClaiming"), 10f);
            Assert.IsTrue(CloseInteractable());
            Assert.IsFalse(button.Find("RedDot").gameObject.activeSelf, "Red dot off.");
            Assert.IsTrue(Claimed(0) && Claimed(1) && !Claimed(2));

            Call("Close");
            yield return Until(() => !Popup("IsOpen"));
            onClick.Invoke();
            yield return Until(() => Popup("IsOpen"));
            float end = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < end)
            {
                System.Threading.Thread.Sleep(1);
                yield return null;
            }
            Assert.IsFalse(Popup("IsClaiming"));
            Assert.AreEqual(start + 650, (int)wallet.GetType().GetProperty("TotalCoins").GetValue(wallet), "Reopening pays nothing.");
            yield return new ExitPlayMode();
        }

        private static Component PopupComponent() =>
            (Component)UnityEngine.Object.FindFirstObjectByType(T("DailyQuestPopup"), FindObjectsInactive.Include);

        private static bool Popup(string property) =>
            (bool)PopupComponent().GetType().GetProperty(property).GetValue(PopupComponent());

        private static void Call(string method) => PopupComponent().GetType().GetMethod(method).Invoke(PopupComponent(), null);

        private static bool CloseInteractable()
        {
            Transform x = PopupComponent().transform.Find("Popup_Dailyquest/Xbutton_Dailyquest");
            Component button = x.GetComponent("Button");
            return (bool)button.GetType().GetProperty("interactable").GetValue(button);
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
