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
    // Classic Mode (ClassicMode.md, 2026-10-10): same scene, same content, no
    // 4-second Survival Timer; records kept apart from Deadline's.
    public sealed class ClassicModeAcceptanceTests
    {
        private static readonly string[] RecordKeys =
        {
            "Deadline4Sec.BestScore", "Deadline4Sec.BestDistance", "Deadline4Sec.BestCombo",
            "Deadline4Sec.BestScore_Classic", "Deadline4Sec.BestDistance_Classic", "Deadline4Sec.BestCombo_Classic",
            "TotalCoins",
        };

        private static Type Find(string name)
        {
            Type type = Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
            Assert.IsNotNull(type, name);
            return type;
        }

        private static Component Get(string name) => (Component)UnityEngine.Object.FindFirstObjectByType(Find(name));
        private static object Read(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
        private static string State(Component flow) => Read(flow, "State").ToString();

        private static void SelectMode(string mode) =>
            Find("GameModeState").GetMethod("Select").Invoke(null, new[] { Enum.Parse(Find("GameMode"), mode) });

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

        private static Dictionary<string, object> SavePrefs()
        {
            var saved = new Dictionary<string, object>();
            foreach (string key in RecordKeys)
                saved[key] = !PlayerPrefs.HasKey(key) ? null : key.Contains("Distance") ? (object)PlayerPrefs.GetFloat(key) : PlayerPrefs.GetInt(key);
            return saved;
        }

        private static void RestorePrefs(Dictionary<string, object> saved)
        {
            foreach (var entry in saved)
            {
                if (entry.Value == null)
                    PlayerPrefs.DeleteKey(entry.Key);
                else if (entry.Value is float f)
                    PlayerPrefs.SetFloat(entry.Key, f);
                else
                    PlayerPrefs.SetInt(entry.Key, (int)entry.Value);
            }
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator ClassicRunHasNoSurvivalTimerButKeepsScoringAndFatalContact()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            var saved = SavePrefs();
            try
            {
                Component flow = Get("GameFlowManager");
                Component timer = Get("GameTimer");
                Component run = Get("RunManager");
                Component powerUps = Get("PowerUpManager");
                flow.GetType().GetField("introDuration", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(flow, 0.1f);
                SelectMode("Classic");
                Call(flow, "StartGame");
                Assert.AreEqual("Classic", Read(flow, "CurrentRunMode").ToString());
                yield return Until(() => State(flow) == "Playing");
                int resets = 0;
                EventInfo resetEvent = timer.GetType().GetEvent("TimerReset");
                Action<float> onReset = _ => resets++;
                resetEvent.AddEventHandler(timer, onReset);

                Assert.IsTrue((bool)Read(timer, "IsUntimed"));
                Assert.IsFalse(GameObject.Find("Canvas").transform.Find("GameplayUI/SurvivalTimer").gameObject.activeSelf,
                    "Classic hides the Survival Timer.");
                // Longer than 4 seconds of game time: no countdown, no timer death.
                Time.timeScale = 3f;
                float startRunTime = (float)Read(run, "CurrentRunTime");
                yield return Until(() => (float)Read(run, "CurrentRunTime") > startRunTime + 5f || (bool)Read(timer, "IsGameOver"), 10f);
                Time.timeScale = 1f;
                if ((bool)Read(timer, "IsGameOver"))
                    Assert.AreEqual("Collision", Read(timer, "LastDeathReason").ToString(), "Only contact may end a Classic run.");
                else
                {
                    Assert.AreEqual(4f, (float)Read(timer, "RemainingTime"), 0.0001f);
                    Assert.IsTrue((bool)Read(timer, "IsRunning"));

                    // Near Miss still scores and combos, but never resets a timer.
                    int score = (int)Read(run, "CurrentScore");
                    Call(run, "RecordNearMiss");
                    Assert.Greater((int)Read(run, "CurrentScore"), score);
                    Call(timer, "ResetTimer");
                    Assert.AreEqual(0, resets);

                    // Timer power-ups do nothing; the other four still work.
                    Type item = Find("PowerUpType");
                    Call(powerUps, "Activate", Enum.Parse(item, "FreezeClock"));
                    Call(powerUps, "Activate", Enum.Parse(item, "TimeHeart"));
                    Call(powerUps, "Activate", Enum.Parse(item, "ComboSeal"));
                    Assert.IsFalse((bool)powerUps.GetType().GetMethod("IsActive").Invoke(powerUps, new[] { Enum.Parse(item, "FreezeClock") }));
                    Assert.IsFalse((bool)powerUps.GetType().GetMethod("IsActive").Invoke(powerUps, new[] { Enum.Parse(item, "TimeHeart") }));
                    Assert.IsTrue((bool)powerUps.GetType().GetMethod("IsActive").Invoke(powerUps, new[] { Enum.Parse(item, "ComboSeal") }));
                    Assert.AreEqual(4f, (float)Read(timer, "MaximumTime"), 0.0001f);

                    // Contact is still fatal in Classic.
                    Call(timer, "TriggerFatalContact");
                    Assert.IsTrue((bool)Read(timer, "IsGameOver"));
                }

                // No Freeze Clock / Time Heart pickup was left active in the course.
                foreach (UnityEngine.Object pickup in UnityEngine.Object.FindObjectsByType(Find("PowerUpPickup"), FindObjectsSortMode.None))
                {
                    string type = Read(pickup, "ItemType").ToString();
                    Assert.IsFalse(type == "FreezeClock" || type == "TimeHeart", "Timer pickup spawned in Classic: " + type);
                }
                resetEvent.RemoveEventHandler(timer, onReset);
            }
            finally
            {
                Time.timeScale = 1f;
                SelectMode("Deadline");
                RestorePrefs(saved);
            }
            yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [UnityTest]
        public IEnumerator ClassicAndDeadlineKeepSeparateBestRecords()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            var saved = SavePrefs();
            try
            {
                PlayerPrefs.SetInt("Deadline4Sec.BestScore", 500);
                PlayerPrefs.DeleteKey("Deadline4Sec.BestScore_Classic");
                Component flow = Get("GameFlowManager");
                flow.GetType().GetField("introDuration", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(flow, 0.1f);
                flow.GetType().GetField("resultDelay", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(flow, 0.01f);
                SelectMode("Classic");
                Call(flow, "StartGame");
                yield return Until(() => State(flow) == "Playing");
                Component run = Get("RunManager");
                run.GetType().GetProperty("CurrentScore").SetValue(run, 1234);
                Call(Get("GameTimer"), "TriggerFatalContact");
                yield return Until(() => State(flow) == "RevivePrompt");
                Call(flow, "GiveUpRevive");
                yield return Until(() => State(flow) == "Result");

                Assert.AreEqual(1234, PlayerPrefs.GetInt("Deadline4Sec.BestScore_Classic"), "Classic best saved under the Classic key.");
                Assert.AreEqual(500, PlayerPrefs.GetInt("Deadline4Sec.BestScore"), "A Classic run never touches the Deadline best.");
                Transform result = GameObject.Find("Canvas").transform.Find("ResultPanel");
                Type tmp = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
                string Text(string child) => (string)tmp.GetProperty("text").GetValue(result.Find(child).GetComponent(tmp));
                Assert.AreEqual("CLASSIC SCORE", Text("ScoreLabel"));
                Assert.AreEqual("1,234", Text("BestScoreText"));
            }
            finally
            {
                SelectMode("Deadline");
                RestorePrefs(saved);
            }
            yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
