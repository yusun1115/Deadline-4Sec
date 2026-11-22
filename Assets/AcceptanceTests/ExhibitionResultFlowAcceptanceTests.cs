using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Deadline4Sec.AcceptanceTests
{
    // Exhibition Result → Ranking flow (2026-11-22): the exhibition result is a
    // summary without buttons; nothing (buttons, shortcuts, repeated calls)
    // leaves it before the score is registered and the ranking shows RETRY /
    // FINISH. NEW BEST is the score's record. The normal result keeps MAIN /
    // RETRY.
    public sealed class ExhibitionResultFlowAcceptanceTests
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
        private static Component Flow() => Get("GameFlowManager");
        private static string State() => Read(Flow(), "State").ToString();
        private static Component Ui() => Get("ExhibitionUI");
        private static string UiFlow() => Ui() != null ? Read(Ui(), "Current").ToString() : "none";
        private static void Call(Component target, string method, params object[] args) =>
            target.GetType().GetMethod(method).Invoke(target, args);
        private static int Count() => ((ICollection)S(Service, "Entries")).Count;

        private static readonly string[] IntKeys = { "Deadline4Sec.TutorialCompleted", "Deadline4Sec.BestScore", "Deadline4Sec.BestCombo" };
        private const string DistanceKey = "Deadline4Sec.BestDistance";

        private static IEnumerator Until(Func<bool> condition, string what, float seconds = 20f)
        {
            float giveUp = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < giveUp)
                yield return null;
            Assert.IsTrue(condition(), "Timed out waiting for: " + what);
        }

        // Records and the tutorial flag come back as they were (kept in the
        // editor session: entering play mode drops locals).
        private static void SavePrefs()
        {
            foreach (string key in IntKeys)
                UnityEditor.SessionState.SetInt("exr_" + key, PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : int.MinValue);
            UnityEditor.SessionState.SetFloat("exr_" + DistanceKey, PlayerPrefs.HasKey(DistanceKey) ? PlayerPrefs.GetFloat(DistanceKey) : -1f);
        }

        private static void RestorePrefs()
        {
            foreach (string key in IntKeys)
            {
                int value = UnityEditor.SessionState.GetInt("exr_" + key, int.MinValue);
                if (value == int.MinValue)
                    PlayerPrefs.DeleteKey(key);
                else
                    PlayerPrefs.SetInt(key, value);
            }
            float distance = UnityEditor.SessionState.GetFloat("exr_" + DistanceKey, -1f);
            if (distance < 0f)
                PlayerPrefs.DeleteKey(DistanceKey);
            else
                PlayerPrefs.SetFloat(DistanceKey, distance);
            PlayerPrefs.Save();
        }

        private static Component Presenter() =>
            (Component)UnityEngine.Object.FindFirstObjectByType(T("ResultPresenter"), FindObjectsInactive.Include);

        private static GameObject ResultChild(string name) => Presenter().transform.Find(name)?.gameObject;

        [UnityTest]
        public IEnumerator TheExhibitionResultCannotBeLeftBeforeTheRanking()
        {
            string folder = Path.Combine(Path.GetTempPath(), "deadline-exr-" + Guid.NewGuid().ToString("N"));
            UnityEditor.SessionState.SetString("exr_folder", folder);
            SavePrefs();
            try
            {
                S(Mode, "OverrideForTests", true);
                EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
                yield return new EnterPlayMode();
                folder = UnityEditor.SessionState.GetString("exr_folder", folder);
                S(Mode, "OverrideForTests", true);
                S(Service, "UseFolderForTests", folder);
                S(Session, "ResetExhibitionSession");
                T("ExhibitionUI").GetMethod("OnSceneLoaded", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { default(UnityEngine.SceneManagement.Scene), default(UnityEngine.SceneManagement.LoadSceneMode) });
                PlayerPrefs.SetInt("Deadline4Sec.BestScore", 999999); // no NEW BEST this time
                yield return Until(() => UiFlow() == "WaitingForPlayer", "the start screen");
                Call(Ui(), "TapStart");
                yield return Until(() => State() == "Tutorial", "the tutorial");
                Call(Flow(), "FinishTutorial", false);
                yield return Until(() => State() == "Playing", "the run");

                // Final death: the revive is turned down.
                Call(Get("GameTimer"), "TriggerGameOver");
                yield return Until(() => UiFlow() == "RevivePrompt", "ONE MORE CHANCE");
                Call(Ui(), "FinishRun");
                Assert.IsTrue((bool)Read(Flow(), "ExhibitionResultLocked"), "Locked from the final death.");
                yield return Until(() => State() == "Result", "the result summary");
                float resultAt = Time.realtimeSinceStartup;

                // A summary: no MAIN / RETRY / CONFIRM, the bigger score, the waiting line.
                Assert.IsTrue((bool)Read(Presenter(), "ExhibitionLayout"));
                foreach (string button in new[] { "MainMenuButton", "RetryButton", "ConfirmButton" })
                    Assert.IsFalse(ResultChild(button) != null && ResultChild(button).activeInHierarchy, button + " is hidden.");
                foreach (string part in new[] { "BestScoreLabel", "BestDistanceLabel", "BestScoreText", "BestDistanceText", "PersonalBestLabel" })
                    Assert.IsFalse(ResultChild(part).activeSelf, part + ": the personal best block is folded away.");
                Type tmp = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
                Component comboLabel = ResultChild("ComboLabel").GetComponent(tmp);
                Assert.AreEqual("MAX COMBO", (string)tmp.GetProperty("text").GetValue(comboLabel));

                // Tapping where the buttons were, the old shortcuts, calling them
                // straight away, again and again: the result stays.
                for (int i = 0; i < 5; i++)
                {
                    Call(Flow(), "Retry");
                    Call(Flow(), "MainMenu");
                    Call(Flow(), "ConfirmResult");
                    Call(Ui(), "FinishSession");
                    Call(Ui(), "RetryAsSameVisitor");
                    ResultChild("MainMenuButton").GetComponent<Button>().onClick.Invoke();
                    ResultChild("RetryButton").GetComponent<Button>().onClick.Invoke();
                    yield return null;
                    Assert.AreEqual("Result", State(), "Nothing leaves the result before the ranking.");
                }
                yield return Until(() => ResultChild("ExhibitionPreparingLabel") != null && ResultChild("ExhibitionPreparingLabel").activeSelf,
                    "랭킹 등록 준비 중...", 4f);
                Assert.IsFalse((bool)Read(Presenter(), "NewBestShown"), "Lower than the best: no NEW BEST.");
                Assert.IsTrue(ResultChild("ExhibitionBestScoreLine").activeSelf, "The best score in one small line instead.");

                // Then the nickname entry, never before ~1.5 s.
                yield return Until(() => UiFlow() == "NicknameEntry", "the nickname entry");
                Assert.GreaterOrEqual(Time.realtimeSinceStartup - resultAt, 1.4f, "The summary plays first.");
                Call(Flow(), "Retry");
                Call(Flow(), "MainMenu");
                Assert.AreEqual("Result", State());
                Assert.AreEqual("NicknameEntry", UiFlow(), "The entry can't be skipped either.");
                Assert.IsTrue((bool)Ui().GetType().GetMethod("SubmitNickname").Invoke(Ui(), new object[] { "TESTER" }));
                Assert.AreEqual("Ranking", UiFlow());
                Assert.AreEqual(1, Count(), "Registered.");
                Assert.IsFalse(Presenter().gameObject.activeInHierarchy, "The result closes behind the ranking.");

                // RETRY: same name, a new run, its result goes straight to the ranking.
                Call(Ui(), "RetryAsSameVisitor");
                yield return Until(() => State() == "Playing", "the retry run");
                Call(Get("GameTimer"), "TriggerGameOver");
                yield return Until(() => UiFlow() == "RevivePrompt", "the free revive again");
                Call(Ui(), "FinishRun");
                bool askedName = false;
                float giveUp = Time.realtimeSinceStartup + 20f;
                while (UiFlow() != "Ranking" && Time.realtimeSinceStartup < giveUp)
                {
                    askedName |= UiFlow() == "NicknameEntry";
                    if (State() == "Result")
                        Assert.IsTrue((bool)Read(Flow(), "ExhibitionResultLocked"));
                    yield return null;
                }
                Assert.AreEqual("Ranking", UiFlow());
                Assert.IsFalse(askedName, "A retry doesn't ask for the name again.");
                Assert.AreEqual(1, Count(), "Still one line for TESTER.");

                // FINISH: the next visitor starts from the tutorial.
                Call(Ui(), "FinishSession");
                yield return Until(() => UiFlow() == "WaitingForPlayer", "the start screen for the next visitor");
                Assert.IsFalse((bool)S(Session, "HasNickname"));
                Call(Ui(), "TapStart");
                yield return Until(() => State() == "Tutorial", "the next visitor's tutorial");
                Call(Flow(), "FinishTutorial", false);
                yield return Until(() => State() == "Playing", "the next visitor's run");
                yield return new ExitPlayMode();
            }
            finally
            {
                S(Service, "UseFolderForTests", new object[] { null });
                S(Mode, "OverrideForTests", new object[] { null });
                S(Session, "ResetExhibitionSession");
                RestorePrefs();
                string dir = UnityEditor.SessionState.GetString("exr_folder", "");
                if (dir.Length > 0 && Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
        }

        // The normal game: MAIN / RETRY as before, and NEW BEST only for the score.
        [UnityTest]
        public IEnumerator TheNormalResultKeepsItsButtonsAndNewBestIsTheScoresRecord()
        {
            SavePrefs();
            try
            {
                S(Mode, "OverrideForTests", false);
                EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
                yield return new EnterPlayMode();
                S(Mode, "OverrideForTests", false);
                PlayerPrefs.SetInt("Deadline4Sec.TutorialCompleted", 1);
                // A score record far above, no distance yet: only the distance falls.
                PlayerPrefs.SetInt("Deadline4Sec.BestScore", 999999);
                PlayerPrefs.SetFloat(DistanceKey, 0f);
                Call(Flow(), "StartGame");
                yield return Until(() => State() == "Playing", "the run");
                yield return new WaitForSeconds(1f);
                Call(Get("GameTimer"), "TriggerGameOver");
                yield return Until(() => State() == "RevivePrompt" || State() == "GameOver" || State() == "Result", "the end of the run");
                if (State() == "RevivePrompt")
                    Call(Flow(), "GiveUpRevive");
                Assert.IsFalse((bool)Read(Flow(), "ExhibitionResultLocked"));
                yield return Until(() => State() == "Result", "the result");
                yield return new WaitForSecondsRealtime(2f);
                Assert.IsFalse((bool)Read(Presenter(), "ExhibitionLayout"));
                Assert.IsTrue(ResultChild("MainMenuButton").activeInHierarchy, "MAIN as before.");
                Assert.IsTrue(ResultChild("RetryButton").activeInHierarchy, "RETRY as before.");
                Assert.IsTrue(ResultChild("BestScoreText").activeInHierarchy, "The personal best block as before.");
                Assert.AreEqual("NEW DISTANCE BEST!", (string)Read(Presenter(), "NewBestCaption"),
                    "A distance record is named as such, not NEW BEST next to a lower score.");
                Assert.IsFalse((bool)Presenter().GetType().GetField("isNewBest", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Presenter()));
                yield return new ExitPlayMode();
            }
            finally
            {
                S(Mode, "OverrideForTests", new object[] { null });
                RestorePrefs();
            }
        }
    }
}
