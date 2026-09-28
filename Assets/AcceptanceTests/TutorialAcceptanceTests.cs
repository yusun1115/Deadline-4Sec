using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    public sealed class TutorialAcceptanceTests
    {
        private static Type Find(string name) => Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
        private static Component Get(string name) => (Component)UnityEngine.Object.FindFirstObjectByType(Find(name));
        private static object Read(Component target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static void Call(Component target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
        private static readonly string[] RecordKeys = {
            "Deadline4Sec.BestScore", "Deadline4Sec.BestDistance", "Deadline4Sec.BestCombo", "Deadline4Sec.TutorialCompleted"
        };

        [UnityTest]
        public IEnumerator DisplayedCuesCompleteTenRealLessonsAndEnterNormalRun()
        {
            return Replay(60, "tutorial-replay.txt");
        }

        [UnityTest]
        public IEnumerator DisplayedCuesAlsoCompleteAtThirtyFramesPerSecond()
        {
            return Replay(30, "tutorial-replay-30fps.txt");
        }

        private static IEnumerator Replay(int fps, string evidenceFile)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            float oldDelta = Time.captureDeltaTime;
            var saved = SavePrefs();
            var trace = new StringBuilder($"EndlessRun tutorial, public input only, {fps} FPS\n");
            try
            {
                SetRecordSentinels();
                Component flow = Get("GameFlowManager");
                Call(flow, "StartTutorial");
                Component tutorial = Get("TutorialController");
                Component player = Get("PlayerController");
                Assert.AreEqual("Tutorial", Read(flow, "State").ToString());
                SceneFlowAcceptanceTests.AssertAllTextUsesGameFont();
                int previousStep = -1, frames = 0;
                float lastInput = float.NegativeInfinity;
                while (Read(tutorial, "State").ToString() != "Complete" && frames++ < 20000)
                {
                    string state = Read(tutorial, "State").ToString();
                    int step = (int)Read(tutorial, "StepIndex");
                    if (step != previousStep)
                    {
                        trace.AppendLine($"STEP {step + 1} t={Time.time:F2}");
                        previousStep = step;
                        lastInput = float.NegativeInfinity;
                        if (step == 0 || step == 8 || step == 9)
                            SceneFlowAcceptanceTests.CaptureIntroFrame(Camera.main, "tutorial-step-" + (step + 1));
                    }
                    if (state == "Failed")
                        Assert.Fail("Actual lesson failed. " + trace + Describe(player, tutorial));
                    Time.captureDeltaTime = state == "Active" ? 1f / fps : 0f;
                    if (state == "Active" && Time.time - lastInput >= 0.14f)
                    {
                        string prompt = Read(tutorial, "PromptInput").ToString();
                        if (prompt != "None")
                        {
                            trace.AppendLine($"INPUT {prompt} {Describe(player, tutorial)}");
                            if (prompt == "Down" && step == 9)
                            {
                                object[] ground = { 0f, Vector3.zero };
                                bool found = (bool)player.GetType().GetMethod("TryGetGroundDistance",
                                    BindingFlags.NonPublic | BindingFlags.Instance).Invoke(player, ground);
                                trace.AppendLine($"GROUND found={found} distance={ground[0]} point={ground[1]}");
                            }
                            Call(player, prompt == "Left" ? "RequestMoveLeft" : prompt == "Right" ?
                                "RequestMoveRight" : prompt == "Up" ? "RequestJump" : "RequestSlide");
                            lastInput = Time.time;
                        }
                    }
                    if (state != "Active")
                        System.Threading.Thread.Sleep(1);
                    yield return null;
                }
                Assert.Less(frames, 20000, trace.ToString());
                Assert.AreEqual(10, Read(tutorial, "CompletedSteps"));
                Assert.AreEqual(0, Read(tutorial, "FailureCount"));
                AssertRecordsUnchanged();
                Assert.AreEqual(1, PlayerPrefs.GetInt("Deadline4Sec.TutorialCompleted"));
                SceneFlowAcceptanceTests.AssertAllTextUsesGameFont();
                SceneFlowAcceptanceTests.CaptureIntroFrame(Camera.main, "tutorial-complete");
                Time.captureDeltaTime = 0f;
                Call(tutorial, "ContinueToRun");
                yield return null;
                yield return null;
                flow = Get("GameFlowManager");
                Assert.AreEqual("Ready", Read(flow, "State").ToString());
                Assert.IsNull(Get("TutorialController"));
                Assert.IsNull(GameObject.Find("TutorialHUD"));
                Assert.IsNull(GameObject.Find("Tutorial Floor"));
                frames = 0;
                while (Read(flow, "State").ToString() == "Ready" && frames++ < 10000)
                {
                    System.Threading.Thread.Sleep(1);
                    yield return null;
                }
                Assert.Less(frames, 10000);
                Assert.AreEqual("Playing", Read(flow, "State").ToString());
                Assert.AreEqual(0, Read(Get("RunManager"), "CurrentScore"));
                Assert.Less((float)Read(Get("RunManager"), "Distance"), 1f);
                Assert.Greater((float)Read(Get("GameTimer"), "RemainingTime"), 3.8f);
                Assert.IsTrue(((Behaviour)Get("PatternSpawner")).enabled);
                AssertRecordsUnchanged();
                trace.AppendLine("PASS: 10 actual lessons, no failure, records preserved, normal Ready/Go run.");
            }
            finally
            {
                WriteEvidence(trace.ToString(), evidenceFile);
                RestorePrefs(saved);
                Time.captureDeltaTime = oldDelta;
            }
            yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [UnityTest]
        public IEnumerator ExpiredPracticeRetriesSameLessonAndMenuRestoresTitle()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            float oldDelta = Time.captureDeltaTime;
            var saved = SavePrefs();
            try
            {
                SetRecordSentinels();
                Component flow = Get("GameFlowManager");
                Call(flow, "StartTutorial");
                Component tutorial = Get("TutorialController");
                Time.captureDeltaTime = 0f;
                yield return WaitForState(tutorial, "Active");
                Time.captureDeltaTime = 1f / 60f;
                yield return WaitForState(tutorial, "Failed");
                Assert.AreEqual(0, Read(tutorial, "StepIndex"));
                Assert.AreEqual(0, Read(tutorial, "CompletedSteps"));
                Assert.AreEqual(1, Read(tutorial, "FailureCount"));
                AssertRecordsUnchanged();
                Time.captureDeltaTime = 0f;
                Call(tutorial, "RetryStep");
                Assert.AreEqual("Preparing", Read(tutorial, "State").ToString());
                yield return WaitForState(tutorial, "Active");
                Assert.AreEqual(0, Read(tutorial, "StepIndex"));
                Assert.IsFalse((bool)Read(Get("GameTimer"), "IsGameOver"));
                Assert.Greater((float)Read(Get("GameTimer"), "RemainingTime"), 3.8f);
                Assert.IsTrue(((Behaviour)Get("PlayerController")).enabled);
                Call(flow, "FinishTutorial", false);
                yield return null;
                yield return null;
                Assert.AreEqual("Title", Read(Get("GameFlowManager"), "State").ToString());
                Assert.IsFalse((bool)Read(Get("GameTimer"), "IsRunning"));
                Assert.IsNull(GameObject.Find("TutorialHUD"));
                Assert.IsNull(GameObject.Find("Tutorial Floor"));
                AssertRecordsUnchanged();
            }
            finally
            {
                RestorePrefs(saved);
                Time.captureDeltaTime = oldDelta;
            }
            yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static IEnumerator WaitForState(Component tutorial, string desired)
        {
            int frames = 0;
            while (Read(tutorial, "State").ToString() != desired && frames++ < 10000)
            {
                System.Threading.Thread.Sleep(1);
                yield return null;
            }
            Assert.Less(frames, 10000, "Expected " + desired);
        }

        private static string Describe(Component player, Component tutorial) =>
            $"step={(int)Read(tutorial, "StepIndex") + 1} t={Time.time:F2} pos={player.transform.position} " +
            $"feet={player.GetComponent<CharacterController>().bounds.min.y:F2} " +
            $"ground={Read(player, "IsGrounded")} homing={Read(player, "IsHoming")} " +
            $"timer={Read(Get("GameTimer"), "RemainingTime")} score={Read(Get("RunManager"), "CurrentScore")}";

        private static Dictionary<string, string> SavePrefs()
        {
            var saved = new Dictionary<string, string>();
            foreach (string key in RecordKeys)
                saved[key] = !PlayerPrefs.HasKey(key) ? null : key.EndsWith("Distance") ?
                    PlayerPrefs.GetFloat(key).ToString("R", System.Globalization.CultureInfo.InvariantCulture) :
                    PlayerPrefs.GetInt(key).ToString();
            return saved;
        }
        private static void RestorePrefs(Dictionary<string, string> saved)
        {
            foreach (var entry in saved)
                if (entry.Value == null) PlayerPrefs.DeleteKey(entry.Key);
                else if (entry.Key.EndsWith("Distance")) PlayerPrefs.SetFloat(entry.Key,
                    float.Parse(entry.Value, System.Globalization.CultureInfo.InvariantCulture));
                else PlayerPrefs.SetInt(entry.Key, int.Parse(entry.Value));
            PlayerPrefs.Save();
        }
        private static void SetRecordSentinels()
        {
            PlayerPrefs.SetInt(RecordKeys[0], 314159);
            PlayerPrefs.SetFloat(RecordKeys[1], 2718.25f);
            PlayerPrefs.SetInt(RecordKeys[2], 123);
            PlayerPrefs.DeleteKey(RecordKeys[3]);
        }
        private static void AssertRecordsUnchanged()
        {
            Assert.AreEqual(314159, PlayerPrefs.GetInt(RecordKeys[0]));
            Assert.AreEqual(2718.25f, PlayerPrefs.GetFloat(RecordKeys[1]));
            Assert.AreEqual(123, PlayerPrefs.GetInt(RecordKeys[2]));
        }
        private static void WriteEvidence(string text, string file)
        {
            string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
            if (System.IO.Path.GetFileName(root) == "ValidationProject") root = System.IO.Path.GetDirectoryName(root);
            System.IO.File.WriteAllText(System.IO.Path.Combine(root, "Verification", file), text);
        }
    }
}
