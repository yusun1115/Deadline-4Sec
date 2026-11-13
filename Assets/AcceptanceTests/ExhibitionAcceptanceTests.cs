using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    // Exhibition local ranking (ExhibitionRanking.md, 2026-11-13).
    public sealed class ExhibitionAcceptanceTests
    {
        private static Type T(string name) => Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
        private static Type Service => T("ExhibitionLeaderboardService");
        private static Type Session => T("ExhibitionSession");
        private static Type Mode => T("ExhibitionMode");
        private static object S(Type type, string member, params object[] args)
        {
            MethodInfo method = type.GetMethod(member, BindingFlags.Public | BindingFlags.Static);
            if (method != null)
                return method.Invoke(null, args);
            return type.GetProperty(member, BindingFlags.Public | BindingFlags.Static).GetValue(null);
        }
        private static object Read(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static Component Get(string name) => (Component)UnityEngine.Object.FindFirstObjectByType(T(name));

        private string folder;

        [SetUp]
        public void UseTempFolder()
        {
            folder = Path.Combine(Path.GetTempPath(), "deadline-exhibition-" + Guid.NewGuid().ToString("N"));
            S(Service, "UseFolderForTests", folder);
        }

        [TearDown]
        public void Restore()
        {
            S(Service, "UseFolderForTests", new object[] { null });
            S(Mode, "OverrideForTests", new object[] { null });
            S(Session, "ResetExhibitionSession");
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        private static int Count() => ((System.Collections.ICollection)S(Service, "Entries")).Count;
        private static object Entry(int index) => ((System.Collections.IList)S(Service, "Entries"))[index];
        private static string Name(object entry) => (string)entry.GetType().GetField("nickname").GetValue(entry);
        private static int Score(object entry) => (int)entry.GetType().GetField("score").GetValue(entry);

        [Test]
        public void AMissingFileIsAnEmptyRanking()
        {
            Assert.AreEqual(0, Count());
            Assert.AreEqual(0, (int)S(Service, "RankOf", "NUNI"));
        }

        [Test]
        public void OneLinePerNicknameKeepingTheBestScore()
        {
            S(Service, "Register", "NUNI", 84200);
            S(Service, "Register", "REAPER", 182300);
            Assert.AreEqual(2, (int)S(Service, "Register", "nuni", 113500), "A higher score updates the line (2nd).");
            S(Service, "Register", " NUNI ", 50000);
            Assert.AreEqual(2, Count(), "Case and outer spaces don't make a new line.");
            Assert.AreEqual("REAPER", Name(Entry(0)));
            Assert.AreEqual("NUNI", Name(Entry(1)));
            Assert.AreEqual(113500, Score(Entry(1)), "The best score stays.");
        }

        [Test]
        public void TiesGoToTheEarlierRegistration()
        {
            S(Service, "Register", "FIRST", 1000);
            S(Service, "Register", "SECOND", 1000);
            S(Service, "Register", "THIRD", 2000);
            Assert.AreEqual(new[] { "THIRD", "FIRST", "SECOND" }, Enumerable.Range(0, 3).Select(i => Name(Entry(i))).ToArray());
        }

        [Test]
        public void TheFileKeepsTheTopHundredAndSurvivesARestart()
        {
            for (int i = 0; i < 105; i++)
                S(Service, "Register", "P" + i, i * 10);
            Assert.AreEqual(100, Count());
            Assert.AreEqual("P104", Name(Entry(0)));
            Assert.IsTrue(File.Exists(Path.Combine(folder, "exhibition_ranking.json")));
            S(Service, "Reload"); // a new app start reads the file again
            Assert.AreEqual(100, Count());
            Assert.AreEqual(1040, Score(Entry(0)));
            Assert.AreEqual(0, (int)S(Service, "RankOf", "P0"), "Dropped below the kept entries.");
            Assert.AreEqual(17, (int)S(Service, "RankOf", "P88"), "Ranks outside the top ten are known.");
        }

        [Test]
        public void ADamagedFileFallsBackToTheBackupThenToEmpty()
        {
            S(Service, "Register", "NUNI", 100);
            S(Service, "Register", "SOUL", 200); // the backup now holds NUNI
            File.WriteAllText(Path.Combine(folder, "exhibition_ranking.json"), "{ broken");
            S(Service, "Reload");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("unreadable"));
            Assert.AreEqual(1, Count(), "The backup was read.");
            Assert.AreEqual("NUNI", Name(Entry(0)));

            File.WriteAllText(Path.Combine(folder, "exhibition_ranking_backup.json"), "not json");
            S(Service, "Reload");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("unreadable"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("unreadable"));
            Assert.AreEqual(0, Count(), "Both unreadable: an empty ranking, no crash.");
        }

        [Test]
        public void NicknamesAreCheckedBeforeRegistering()
        {
            foreach (string bad in new[] { "", "   ", "A", "ABCDEFGHI", "AB CD", "AB\nC" })
                Assert.AreNotEqual("", (string)S(Service, "Validate", bad), "Rejected: [" + bad + "]");
            foreach (string good in new[] { "NUNI", " NUNI ", "누니", "Reaper_1" })
                Assert.AreEqual("", (string)S(Service, "Validate", good), "Accepted: [" + good + "]");
        }

        [Test]
        public void TheSettingsKeepOnlyTheExhibitionSwitches()
        {
            Type settings = T("ExhibitionSettings");
            foreach (string gone in new[] { "powerUpLevel", "disableConsumables", "noMetaRewards", "adminTaps" })
                Assert.IsNull(settings.GetField(gone), gone + " was removed (the normal game's items and souls).");
        }

        [Test]
        public void ClearingRemovesTheFileAndTheBackup()
        {
            S(Service, "Register", "NUNI", 100);
            S(Service, "Register", "SOUL", 200);
            S(Service, "ClearExhibitionLeaderboard");
            Assert.AreEqual(0, Count());
            Assert.IsFalse(File.Exists(Path.Combine(folder, "exhibition_ranking.json")));
            Assert.IsFalse(File.Exists(Path.Combine(folder, "exhibition_ranking_backup.json")));
        }

        private static IEnumerator Until(Func<bool> condition, string what, float seconds = 20f)
        {
            float giveUp = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < giveUp)
                yield return null;
            Assert.IsTrue(condition(), "Timed out waiting for: " + what);
        }

        private static string State() => Read(Get("GameFlowManager"), "State").ToString();
        private static Component Ui() => Get("ExhibitionUI");
        private static string Flow() => Ui() != null ? Read(Ui(), "Current").ToString() : "none";
        private static void Call(Component target, string method, params object[] args) =>
            target.GetType().GetMethod(method).Invoke(target, args);

        private static readonly string[] PrefKeys = { "Deadline4Sec.TutorialCompleted", "ItemLevel_SoulMagnet" };

        // Scenarios A-D and the fairness rules, through the real scene.
        [UnityTest]
        public IEnumerator VisitorsComeAndGoWhileTheRankingStays()
        {
            S(Mode, "OverrideForTests", true);
            S(Session, "ResetExhibitionSession");
            // Kept in SessionState: play mode reloads the domain (locals don't survive).
            foreach (string key in PrefKeys)
                UnityEditor.SessionState.SetInt("exhibition_test_" + key, PlayerPrefs.GetInt(key, -1));
            PlayerPrefs.SetInt("Deadline4Sec.TutorialCompleted", 1); // this device finished it long ago
            PlayerPrefs.SetInt("ItemLevel_SoulMagnet", 6);
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
                yield return new EnterPlayMode();
                // Entering play mode reloads the domain: the switches again, and
                // the screens the scene load would have added with the mode on.
                S(Mode, "OverrideForTests", true);
                S(Service, "UseFolderForTests", folder);
                S(Session, "ResetExhibitionSession");
                T("ExhibitionUI").GetMethod("OnSceneLoaded", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { default(UnityEngine.SceneManagement.Scene), default(UnityEngine.SceneManagement.LoadSceneMode) });
                yield return Until(() => Flow() == "WaitingForPlayer", "the start screen");

                // A: the first visitor is sent through the tutorial (SKIP stays, as in the game).
                Assert.IsNotNull(GameObject.Find("Canvas/TitlePanel/UpgradeButton"), "The normal title, menus included.");
                Call(Ui(), "TapStart");
                yield return Until(() => State() == "Tutorial", "the tutorial");
                Assert.IsNotNull(GameObject.Find("TutorialSkipUI"), "SKIP as in the normal game.");
                Call(Get("GameFlowManager"), "FinishTutorial", false);
                yield return Until(() => State() == "Playing", "the visitor's first run");
                Assert.IsTrue((bool)S(Session, "TutorialCompleted"));
                Assert.AreEqual(6, Call<int>(Get("PowerUpManager"), "GetLevel", Enum.Parse(T("PowerUpType"), "SoulMagnet")),
                    "Upgrades are the device's own, as in the normal game.");

                // One free revive.
                Call(Get("GameTimer"), "TriggerGameOver");
                yield return Until(() => State() == "RevivePrompt" && Flow() == "RevivePrompt", "ONE MORE CHANCE");
                Call(Ui(), "Revive");
                yield return Until(() => State() == "Playing", "the revived run");
                Call(Get("GameTimer"), "TriggerGameOver");
                yield return Until(() => Flow() == "NicknameEntry", "the nickname entry (no second revive)");
                Assert.AreEqual("Result", State());

                Assert.IsFalse((bool)Ui().GetType().GetMethod("SubmitNickname").Invoke(Ui(), new object[] { "A" }));
                Assert.AreNotEqual("", (string)Read(Ui(), "NicknameError"));
                Assert.IsTrue((bool)Ui().GetType().GetMethod("SubmitNickname").Invoke(Ui(), new object[] { "NUNI" }));
                Assert.AreEqual("Ranking", Flow());
                Assert.AreEqual(1, Count());
                Assert.IsTrue(File.Exists((string)S(Service, "FilePath")), "Saved straight away.");

                // B: RETRY keeps the name, skips the tutorial, gives the free revive back.
                Call(Ui(), "RetryAsSameVisitor");
                yield return Until(() => State() == "Playing", "the retry run");
                Assert.AreEqual(1, (int)S(Session, "RetryCount"));
                Assert.IsFalse((bool)S(Session, "ReviveUsed"));
                Call(Get("GameTimer"), "TriggerGameOver");
                yield return Until(() => Flow() == "RevivePrompt", "the free revive again");
                Call(Ui(), "FinishRun");
                yield return Until(() => Flow() == "Ranking", "the ranking without a nickname entry");
                Assert.AreEqual(1, Count(), "Still one line for NUNI.");

                // Admin (Ctrl+Shift+Delete on Windows): the panel, then the asked reset.
                Call(Ui(), "OpenAdmin");
                Assert.IsTrue((bool)Read(Ui(), "AdminOpen"));
                Call(Ui(), "CloseAdmin");
                Assert.IsFalse((bool)Read(Ui(), "AdminOpen"));
                Assert.AreEqual("Ranking", Flow(), "Closing the admin panel leaves the ranking as it was.");

                // D: left alone, the ranking finishes the session by itself.
                Assert.Greater((float)Read(Ui(), "AutoFinishRemaining"), 30f);
                Ui().GetType().GetField("autoFinishAt", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(Ui(), Time.unscaledTime + 0.2f);
                yield return Until(() => Flow() == "WaitingForPlayer", "the start screen for the next visitor");

                // C: the next visitor starts from nothing; NUNI stays on the board.
                Assert.IsFalse((bool)S(Session, "HasNickname"));
                Assert.IsFalse((bool)S(Session, "TutorialCompleted"));
                Assert.AreEqual(0, (int)S(Session, "RetryCount"));
                S(Service, "Reload");
                Assert.AreEqual("NUNI", Name(Entry(0)));
                Call(Ui(), "TapStart");
                yield return Until(() => State() == "Tutorial", "the tutorial for the next visitor");
                Assert.AreEqual(1, PlayerPrefs.GetInt("Deadline4Sec.TutorialCompleted"), "The device's own tutorial flag is untouched.");
                // (Leaving play mode mid-tutorial logs a known teardown error; end it first.)
                Call(Get("GameFlowManager"), "FinishTutorial", false);
                yield return Until(() => State() == "Playing", "the next visitor's run");
                yield return new ExitPlayMode();
            }
            finally
            {
                foreach (string key in PrefKeys)
                {
                    int before = UnityEditor.SessionState.GetInt("exhibition_test_" + key, -1);
                    if (before < 0)
                        PlayerPrefs.DeleteKey(key);
                    else
                        PlayerPrefs.SetInt(key, before);
                }
            }
        }

        private static TR Call<TR>(Component target, string method, params object[] args) =>
            (TR)target.GetType().GetMethod(method).Invoke(target, args);

        // Classic in the exhibition: a notice first, then it plays (unranked).
        [UnityTest]
        public IEnumerator ChoosingClassicAsksForAcknowledgementFirst()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            S(Mode, "OverrideForTests", true);
            S(Session, "ResetExhibitionSession");
            T("ExhibitionUI").GetMethod("OnSceneLoaded", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { default(UnityEngine.SceneManagement.Scene), default(UnityEngine.SceneManagement.LoadSceneMode) });
            yield return null;
            Component select = (Component)UnityEngine.Object.FindFirstObjectByType(T("ModeSelectUI"), FindObjectsInactive.Include);
            Type state = T("GameModeState");
            Call(select, "Open");
            Call(select, "ChooseDeadline");
            Call(select, "Confirm");
            Assert.IsFalse((bool)Read(Ui(), "NoticeOpen"), "Deadline: no notice.");

            Call(select, "Open");
            Call(select, "ChooseClassic");
            Call(select, "Confirm");
            Assert.IsTrue((bool)Read(Ui(), "NoticeOpen"), "Classic: the notice.");
            Assert.AreEqual("Deadline", state.GetProperty("CurrentGameMode").GetValue(null).ToString(), "Not applied yet.");
            GameObject ok = GameObject.Find("확인했어요Button");
            Assert.IsNotNull(ok);
            Type tmp = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
            StringAssert.Contains("랭킹에 등록되지 않습니다", string.Join(" ",
                ok.transform.parent.GetComponentsInChildren(tmp).Select(t => (string)Read(t, "text"))));
            ok.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.IsFalse((bool)Read(Ui(), "NoticeOpen"));
            Assert.AreEqual("Classic", state.GetProperty("CurrentGameMode").GetValue(null).ToString(), "Acknowledged: Classic.");
            Assert.IsFalse(select.gameObject.activeSelf, "Back to the title.");
            S(Session, "ResetExhibitionSession");
            Assert.AreEqual("Deadline", state.GetProperty("CurrentGameMode").GetValue(null).ToString(), "The next visitor starts on Deadline.");
            yield return new ExitPlayMode();
        }

        // Admin (Ctrl+Shift+Delete): grants and the whole-save reset.
        [UnityTest]
        public IEnumerator AdminGrantsAndTheFullReset()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            string[] keys = { "TotalCoins", "Currency_FrostShard", "Currency_BloodShard", "Currency_CyberCore", "Currency_EclipseFragment" };
            S(Mode, "OverrideForTests", true);
            S(Service, "UseFolderForTests", folder);
            T("ExhibitionUI").GetMethod("OnSceneLoaded", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { default(UnityEngine.SceneManagement.Scene), default(UnityEngine.SceneManagement.LoadSceneMode) });
            yield return null;
            int[] before = keys.Select(k => PlayerPrefs.GetInt(k, 0)).ToArray();
            Call(Ui(), "OpenAdmin");
            Ui().GetType().GetMethod("GrantSkinCurrencies").Invoke(Ui(), new object[] { 50 });
            Ui().GetType().GetMethod("GrantSouls").Invoke(Ui(), new object[] { 10000 });
            Assert.AreEqual(before[0] + 10000, PlayerPrefs.GetInt("TotalCoins"));
            for (int i = 1; i < keys.Length; i++)
                Assert.AreEqual(before[i] + 50, PlayerPrefs.GetInt(keys[i]), keys[i]);
            for (int i = 0; i < keys.Length; i++)
                PlayerPrefs.SetInt(keys[i], before[i]); // the grants are undone; the reset itself is not run here
            PlayerPrefs.Save();
            Assert.IsNotNull(Ui().GetType().GetMethod("ResetAllData"));
            yield return new ExitPlayMode();
        }

        // Off: none of it exists, the game is as before.
        [UnityTest]
        public IEnumerator WithTheModeOffNothingChanges()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            // Whatever the saved switch says now, a scene loaded with the mode off:
            S(Mode, "OverrideForTests", false);
            UnityEngine.SceneManagement.SceneManager.LoadScene("Assets/Scenes/EndlessRun.unity");
            yield return null;
            yield return null;
            Assert.IsNull(Ui(), "No exhibition screens.");
            Assert.IsNotNull(Get("LeaderboardRunSubmitter"), "The Google Play submit is still attached.");
            yield return new ExitPlayMode();
        }
    }
}
