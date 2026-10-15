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
    // leaderboard.md §32: one leaderboard per mode, the final score of a run is
    // submitted once (not at a revived death, not again on Retry), 0 points and
    // a missing sign-in are skipped without breaking the result flow. The
    // Editor mock stands in for Google Play (real API: Android build).
    public sealed class LeaderboardAcceptanceTests
    {
        private const string ScenePath = "Assets/Scenes/EndlessRun.unity";
        private const string BackupKey = "Deadline4Sec.LeaderboardTests.Backup";
        private static readonly string[] SavedKeys =
        {
            "Deadline4Sec.BestScore", "Deadline4Sec.BestDistance", "Deadline4Sec.BestCombo",
            "Deadline4Sec.BestScore_Classic", "Deadline4Sec.BestDistance_Classic", "Deadline4Sec.BestCombo_Classic",
            "TotalCoins", "ReviveCouponCount", "Deadline4Sec.TutorialCompleted",
        };

        // Submits seen by the handler (static: survives nothing, re-subscribed after EnterPlayMode).
        private static readonly List<string> submits = new List<string>();

        private static Type T(string name)
        {
            Type type = Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
            Assert.IsNotNull(type, name);
            return type;
        }

        private static object S(string type, string method, params object[] args) =>
            T(type).GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);

        private static object Mode(string mode) => Enum.Parse(T("GameMode"), mode);
        private static Component Get(string name) => (Component)UnityEngine.Object.FindFirstObjectByType(T(name));
        private static string State() => Get("GameFlowManager").GetType().GetProperty("State").GetValue(Get("GameFlowManager")).ToString();
        private static void Call(Component target, string method) => target.GetType().GetMethod(method).Invoke(target, null);
        private static ScriptableObject Settings() => (ScriptableObject)T("GooglePlayService").GetProperty("Settings").GetValue(null);
        private static void SetSetting(string field, object value) => Settings().GetType().GetField(field).SetValue(Settings(), value);
        private static object GetSetting(string field) => Settings().GetType().GetField(field).GetValue(Settings());

        // ---- capture GooglePlayService.ScoreSubmitted --------------------------

        private static Delegate handler;

        private static void OnSubmitted<TMode>(TMode mode, long score, string id) => submits.Add(mode + "|" + score + "|" + id);

        private static void Listen(bool on)
        {
            EventInfo e = T("GooglePlayService").GetEvent("ScoreSubmitted");
            if (handler == null)
            {
                MethodInfo method = typeof(LeaderboardAcceptanceTests)
                    .GetMethod(nameof(OnSubmitted), BindingFlags.NonPublic | BindingFlags.Static)
                    .MakeGenericMethod(T("GameMode"));
                handler = Delegate.CreateDelegate(e.EventHandlerType, method);
            }
            if (on)
                e.AddEventHandler(null, handler);
            else
                e.RemoveEventHandler(null, handler);
        }

        // ---- save backup (SessionState survives the play-mode domain reload) ----

        private static IEnumerable<string> Keys()
        {
            foreach (string key in SavedKeys)
                yield return key;
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
                    bool isFloat = key.Contains("Distance") || key.Contains("LowestTimer");
                    if (!PlayerPrefs.HasKey(key))
                        backup.Append(key).Append("\t-\t\n");
                    else
                        backup.Append(key).Append('\t').Append(isFloat ? "f" : "i").Append('\t')
                            .Append(isFloat ? PlayerPrefs.GetFloat(key).ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                                : PlayerPrefs.GetInt(key).ToString()).Append('\n');
                }
                backup.Append("#settings\t").Append(GetSetting("simulateSignedOutInEditor")).Append('\n');
                UnityEditor.SessionState.SetString(BackupKey, backup.ToString());
            }
            PlayerPrefs.SetInt("Deadline4Sec.TutorialCompleted", 1);
            PlayerPrefs.Save();
            submits.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Listen(false);
            foreach (string line in UnityEditor.SessionState.GetString(BackupKey, "").Split('\n'))
            {
                string[] parts = line.Split('\t');
                if (parts.Length < 2)
                    continue;
                if (parts[0] == "#settings")
                    SetSetting("simulateSignedOutInEditor", parts[1] == "True");
                else if (parts.Length < 3)
                    continue;
                else if (parts[1] == "-")
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
            S("GameModeState", "Select", Mode("Deadline"));
        }

        // ---- tests ------------------------------------------------------------

        [Test]
        public void EachModeHasItsOwnLeaderboardAndBadScoresAreSkipped()
        {
            string deadline = (string)GetSetting("deadlineLeaderboardId");
            string classic = (string)GetSetting("classicLeaderboardId");
            try
            {
                SetSetting("deadlineLeaderboardId", "TEST_DEADLINE");
                SetSetting("classicLeaderboardId", "TEST_CLASSIC");
                Assert.AreEqual("TEST_DEADLINE", S("GooglePlayService", "GetLeaderboardId", Mode("Deadline")));
                Assert.AreEqual("TEST_CLASSIC", S("GooglePlayService", "GetLeaderboardId", Mode("Classic")));
                Listen(true);
                S("GooglePlayService", "SubmitScore", Mode("Deadline"), 10000L);
                S("GooglePlayService", "SubmitScore", Mode("Classic"), 20000L);
                S("GooglePlayService", "SubmitScore", Mode("Deadline"), 0L);
                CollectionAssert.AreEqual(new[] { "Deadline|10000|TEST_DEADLINE", "Classic|20000|TEST_CLASSIC" }, submits,
                    "Each mode goes to its own board; 0 points are not sent.");

                SetSetting("simulateSignedOutInEditor", true);
                Assert.DoesNotThrow(() => S("GooglePlayService", "SubmitScore", Mode("Classic"), 5000L));
                Assert.AreEqual(2, submits.Count, "Signed out: skipped, no error.");
                Assert.IsFalse((bool)T("GooglePlayService").GetProperty("IsAuthenticated").GetValue(null));
            }
            finally
            {
                SetSetting("deadlineLeaderboardId", deadline);
                SetSetting("classicLeaderboardId", classic);
            }
        }

        // Death → revive → final death: only the final score is sent, once,
        // to the Deadline board; Retry is a new run that sends its own score.
        [UnityTest]
        public IEnumerator DeadlineRunSubmitsOnlyTheFinalScoreOnce()
        {
            PlayerPrefs.SetInt("ReviveCouponCount", 1);
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return Until(() => Get("GameFlowManager") != null);
            submits.Clear();
            Listen(true);
            Shorten();
            S("GameModeState", "Select", Mode("Deadline"));
            Call(Get("GameFlowManager"), "StartGame");
            yield return Until(() => State() == "Playing");
            Call(Get("RunManager"), "RecordNearMiss");
            int firstDeathScore = Score();
            Assert.Greater(firstDeathScore, 0);
            Call(Get("GameTimer"), "TriggerGameOver");
            yield return Until(() => State() == "RevivePrompt");
            Assert.AreEqual(0, submits.Count, "The first death of a revivable run sends nothing.");

            Call(Get("GameFlowManager"), "UseCouponRevive");
            yield return Until(() => State() == "Playing");
            Call(Get("RunManager"), "RecordNearMiss");
            Call(Get("RunManager"), "RecordNearMiss");
            int finalScore = Score();
            Assert.Greater(finalScore, firstDeathScore);
            Call(Get("GameTimer"), "TriggerGameOver");
            yield return Until(() => State() == "Result");
            yield return null;
            string id = (string)S("GooglePlayService", "GetLeaderboardId", Mode("Deadline"));
            CollectionAssert.AreEqual(new[] { "Deadline|" + finalScore + "|" + id }, submits);

            // Retry: a new run; the old score is not sent again.
            Call(Get("GameFlowManager"), "Retry");
            yield return Until(() => Get("GameFlowManager") != null && State() == "Playing");
            Shorten();
            Assert.AreEqual(1, submits.Count, "Retry does not re-send the previous run.");
            Call(Get("RunManager"), "RecordNearMiss");
            int retryScore = Score();
            Call(Get("GameTimer"), "TriggerGameOver");
            yield return Until(() => State() == "RevivePrompt" || State() == "Result");
            if (State() == "RevivePrompt")
                Call(Get("GameFlowManager"), "GiveUpRevive");
            yield return Until(() => State() == "Result");
            yield return null;
            Assert.AreEqual(2, submits.Count);
            Assert.AreEqual("Deadline|" + retryScore + "|" + id, submits[1]);
            Listen(false);
            yield return new ExitPlayMode();
        }

        // A Classic run goes to the Classic board only; signed out, the result
        // flow (Result, Retry) still works and nothing is sent.
        [UnityTest]
        public IEnumerator ClassicRunUsesClassicBoardAndSignedOutStillPlays()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return Until(() => Get("GameFlowManager") != null);
            submits.Clear();
            Listen(true);
            Shorten();
            S("GameModeState", "Select", Mode("Classic"));
            Call(Get("GameFlowManager"), "StartGame");
            yield return Until(() => State() == "Playing");
            Call(Get("RunManager"), "RecordNearMiss");
            int score = Score();
            Call(Get("GameTimer"), "TriggerGameOver");
            yield return Until(() => State() == "RevivePrompt" || State() == "Result");
            if (State() == "RevivePrompt")
                Call(Get("GameFlowManager"), "GiveUpRevive");
            yield return Until(() => State() == "Result");
            yield return null;
            string classicId = (string)S("GooglePlayService", "GetLeaderboardId", Mode("Classic"));
            CollectionAssert.AreEqual(new[] { "Classic|" + score + "|" + classicId }, submits, "Classic never reaches the Deadline board.");

            SetSetting("simulateSignedOutInEditor", true);
            Call(Get("GameFlowManager"), "Retry");
            yield return Until(() => Get("GameFlowManager") != null && State() == "Playing");
            Shorten();
            Call(Get("RunManager"), "RecordNearMiss");
            Call(Get("GameTimer"), "TriggerGameOver");
            yield return Until(() => State() == "RevivePrompt" || State() == "Result");
            if (State() == "RevivePrompt")
                Call(Get("GameFlowManager"), "GiveUpRevive");
            yield return Until(() => State() == "Result");
            Assert.AreEqual(1, submits.Count, "Signed out: the score is skipped.");
            Call(Get("GameFlowManager"), "Retry");
            yield return Until(() => Get("GameFlowManager") != null && State() == "Playing");
            SetSetting("simulateSignedOutInEditor", false);
            Listen(false);
            yield return new ExitPlayMode();
        }

        private static int Score()
        {
            Component run = Get("RunManager");
            return (int)run.GetType().GetProperty("CurrentScore").GetValue(run);
        }

        private static void Shorten()
        {
            Component flow = Get("GameFlowManager");
            BindingFlags hidden = BindingFlags.NonPublic | BindingFlags.Instance;
            flow.GetType().GetField("introDuration", hidden).SetValue(flow, 0.1f);
            flow.GetType().GetField("resultDelay", hidden).SetValue(flow, 0.01f);
            flow.GetType().GetField("deathPresentationDuration", hidden).SetValue(flow, 0.2f);
        }

        private static IEnumerator Until(Func<bool> condition, float seconds = 8f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!SafeCheck(condition) && Time.realtimeSinceStartup < end)
            {
                System.Threading.Thread.Sleep(1);
                yield return null;
            }
            Assert.IsTrue(SafeCheck(condition), "Timed out waiting.");
        }

        // During a scene reload the objects briefly do not exist.
        private static bool SafeCheck(Func<bool> condition)
        {
            try
            {
                return condition();
            }
            catch (TargetInvocationException)
            {
                return false;
            }
            catch (NullReferenceException)
            {
                return false;
            }
        }
    }
}
