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
                int previousStep = -1, frames = 0, holds = 0;
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
                    if (state == "Briefing")
                        Call(tutorial, "AdvanceBriefing"); // the player taps through the rules
                    Time.captureDeltaTime = state == "Active" ? 1f / fps : 0f;
                    // A held lesson (game stopped for the swipe) is answered at once.
                    bool held = state == "Active" && (bool)Read(tutorial, "IsHoldingForInput");
                    if (held)
                        holds++;
                    if (state == "Active" && (held || Time.time - lastInput >= 0.14f))
                    {
                        string prompt = Read(tutorial, "PromptInput").ToString();
                        if (prompt != "None")
                        {
                            trace.AppendLine($"INPUT {prompt}{(held ? " (held)" : "")} {Describe(player, tutorial)}");
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
                Assert.GreaterOrEqual(holds, 10, "Every lesson stops the game for its swipe.\n" + trace);
                Assert.AreEqual(1f, Time.timeScale);
                Assert.AreEqual(10, Read(tutorial, "CompletedSteps"));
                Assert.AreEqual(0, Read(tutorial, "FailureCount"));
                AssertRecordsUnchanged();
                Assert.AreEqual(1, PlayerPrefs.GetInt("Deadline4Sec.TutorialCompleted"));
                SceneFlowAcceptanceTests.AssertAllTextUsesGameFont();
                SceneFlowAcceptanceTests.CaptureIntroFrame(Camera.main, "tutorial-complete");
                // 2026-11-05: no START RUN / SKIP at the end; after the last line
                // the screen fades out to the title and fades back in.
                Assert.IsNull(GameObject.Find("START RUNButton"));
                Assert.IsNull(GameObject.Find("SkipButton"));
                Time.captureDeltaTime = 0f;
                frames = 0;
                while ((Get("GameFlowManager") == null || Read(Get("GameFlowManager"), "State").ToString() != "Title" ||
                        GameObject.Find("ScreenFade") != null) && frames++ < 20000)
                {
                    System.Threading.Thread.Sleep(1);
                    yield return null;
                }
                Assert.Less(frames, 20000, "The tutorial returns to the title by itself.");
                Assert.IsNull(Get("TutorialController"));
                Assert.IsNull(GameObject.Find("TutorialHUD"));
                Assert.IsNull(GameObject.Find("Tutorial Floor"));
                Assert.IsFalse((bool)Read(Get("GameTimer"), "IsRunning"));
                Assert.AreEqual(1f, Time.timeScale);
                AssertRecordsUnchanged();
                trace.AppendLine("PASS: 10 actual lessons, no failure, records preserved, back to the title.");
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

        // 2026-11-01: each lesson stops the game for its swipe and ignores the
        // others; a wrong move past the obstacle fails at once, the screen
        // darkens and the same lesson restarts by itself (no TRY AGAIN / MENU).
        [UnityTest]
        public IEnumerator HeldLessonTakesOnlyItsSwipeAndAWrongMoveRetriesByItself()
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
                Component player = Get("PlayerController");
                Component ui = (Component)Read(tutorial, "UI");
                Time.captureDeltaTime = 0f;
                yield return PassBriefing(tutorial);
                yield return WaitForState(tutorial, "Active");
                Assert.IsNull(GameObject.Find("TRY AGAINButton"));
                Assert.IsNull(GameObject.Find("MENUButton"));
                Time.captureDeltaTime = 1f / 60f;

                // Lesson 1 holds for LEFT: RIGHT is ignored, LEFT resumes.
                yield return WaitForHold(tutorial, "Left");
                Assert.AreEqual(0f, Time.timeScale);
                Assert.AreEqual("Left", Read(ui, "GuideDirection").ToString());
                Call(player, "RequestMoveRight");
                yield return null;
                Assert.IsTrue((bool)Read(tutorial, "IsHoldingForInput"));
                Assert.AreEqual(0, Read(player, "LaneIndex"));
                Call(player, "RequestMoveLeft");
                Assert.IsFalse((bool)Read(tutorial, "IsHoldingForInput"));
                Assert.AreEqual(1f, Time.timeScale);
                yield return WaitForHold(tutorial, "Right");
                Call(player, "RequestMoveRight");
                int frames = 0;
                while ((int)Read(tutorial, "StepIndex") != 1 && frames++ < 10000)
                {
                    System.Threading.Thread.Sleep(1);
                    yield return null;
                }
                yield return WaitForState(tutorial, "Active");

                // Lesson 2 (jump): dodging sideways past the block fails at once.
                Call(player, "RequestMoveLeft");
                yield return WaitForState(tutorial, "Failed");
                Assert.AreEqual(1, Read(tutorial, "StepIndex"));
                Assert.AreEqual(1, Read(tutorial, "CompletedSteps"));
                Assert.AreEqual(1, Read(tutorial, "FailureCount"));
                Assert.Greater((float)Read(Get("GameTimer"), "RemainingTime"), 1.5f, "no wait for the timer");
                Assert.IsTrue((bool)Read(ui, "ScreenFaded"));
                AssertRecordsUnchanged();
                Time.captureDeltaTime = 0f;
                yield return WaitForState(tutorial, "Preparing");
                yield return WaitForState(tutorial, "Active");
                Assert.AreEqual(1, Read(tutorial, "StepIndex"));
                Assert.IsFalse((bool)Read(Get("GameTimer"), "IsGameOver"));
                Assert.Greater((float)Read(Get("GameTimer"), "RemainingTime"), 3.8f);
                Assert.IsTrue(((Behaviour)player).enabled);
                float until = Time.realtimeSinceStartup + 0.5f;
                while (Time.realtimeSinceStartup < until) { System.Threading.Thread.Sleep(1); yield return null; }
                Assert.IsFalse((bool)Read(ui, "ScreenFaded"));
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

        // Tutorial.md onboarding: the speaker briefs the 4-second rule on the
        // dimmed screen with the real timer highlighted, the swipe guide shows
        // the asked direction, and SKIP clears everything into a normal run.
        [UnityTest]
        public IEnumerator SpeakerBriefsTimerThenSwipeGuideFollowsPromptAndSkipRestoresHud()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            float oldDelta = Time.captureDeltaTime;
            var saved = SavePrefs();
            try
            {
                SetRecordSentinels();
                Component flow = Get("GameFlowManager");
                Transform nearMiss = GameObject.Find("Canvas").transform.Find("GameplayUI/NearMissFeedback");
                CanvasGroup nearMissGroup = nearMiss != null ? nearMiss.GetComponent<CanvasGroup>() : null;
                float nearMissAlpha = nearMissGroup != null ? nearMissGroup.alpha : 1f;
                Call(flow, "StartTutorial");
                Component tutorial = Get("TutorialController");
                Component ui = (Component)Read(tutorial, "UI");
                Assert.AreEqual("Briefing", Read(tutorial, "State").ToString());
                Assert.AreEqual(0, Read(tutorial, "BriefingIndex"));
                Assert.IsTrue((bool)Read(ui, "SpeakerVisible"));
                Assert.IsTrue(GameObject.Find("tutorial_ex").GetComponent<UnityEngine.UI.Image>().sprite != null);
                Assert.IsTrue(GameObject.Find("tutorial_ex_bubble").GetComponent<UnityEngine.UI.Image>().sprite != null);
                Assert.AreEqual("Timer", Read(ui, "Highlight").ToString());
                Assert.IsTrue((bool)Read(ui, "DimVisible"));
                Assert.IsTrue((bool)Read(ui, "TapToContinue"));
                Assert.AreEqual("None", Read(ui, "GuideDirection").ToString());
                // The real timer is drawn above the dim; score / Soul / gift are hidden.
                Transform hud = GameObject.Find("Canvas").transform.Find("GameplayUI");
                Assert.Less(hud.Find("TutorialDim").GetSiblingIndex(), hud.Find("TimerFrame").GetSiblingIndex());
                Assert.Less(hud.Find("TutorialDim").GetSiblingIndex(), hud.Find("SurvivalTimer").GetSiblingIndex());
                foreach (string hidden in new[] { "ScoreText", "RunCoinText", "GiftBoxCountText" })
                    Assert.AreEqual(0f, hud.Find(hidden).GetComponent<CanvasGroup>().alpha, hidden);
                // Lesson feedback (the NEAR MISS label) stays visible.
                if (nearMiss != null)
                {
                    CanvasGroup group = nearMiss.GetComponent<CanvasGroup>();
                    Assert.AreEqual(nearMissAlpha, group != null ? group.alpha : 1f);
                }
                Assert.IsTrue(GameObject.Find("SkipButton").activeInHierarchy);

                Call(tutorial, "AdvanceBriefing"); // finishes the typed line
                Assert.AreEqual(0, Read(tutorial, "BriefingIndex"));
                Assert.IsFalse((bool)Read(ui, "IsTyping"));
                object step = Read(tutorial, "CurrentStep");
                Assert.AreEqual(step.GetType().GetField("dialogueText").GetValue(step), Read(ui, "DialogueText"));
                Call(tutorial, "AdvanceBriefing");
                for (int wait = 0; wait < 10 && (int)Read(tutorial, "BriefingIndex") == 0; wait++)
                    yield return null;
                Assert.AreEqual(1, Read(tutorial, "BriefingIndex"));
                Assert.IsTrue((bool)Read(ui, "DimVisible"));

                Time.captureDeltaTime = 0f;
                yield return PassBriefing(tutorial);
                yield return WaitForState(tutorial, "Active");
                Assert.AreEqual("None", Read(ui, "Highlight").ToString());
                Assert.IsFalse((bool)Read(ui, "TapToContinue"));
                Time.captureDeltaTime = 1f / 60f;
                int frames = 0;
                while (Read(tutorial, "PromptInput").ToString() != "Left" && frames++ < 600)
                    yield return null;
                yield return null;
                Assert.AreEqual("Left", Read(ui, "GuideDirection").ToString());
                Assert.AreEqual(90f, (float)Read(ui, "ArrowAngle"), 0.5f);
                Assert.IsTrue((bool)Read(tutorial, "IsHoldingForInput"));
                Assert.AreEqual(0f, Time.timeScale);
                Assert.IsTrue(GameObject.Find("tutorial_swipe_hand") != null);
                Call(Get("PlayerController"), "RequestMoveLeft");
                frames = 0;
                while (Read(tutorial, "PromptInput").ToString() != "Right" && frames++ < 600)
                    yield return null;
                yield return null;
                Assert.AreEqual("Right", Read(ui, "GuideDirection").ToString());
                Assert.AreEqual(270f, (float)Read(ui, "ArrowAngle"), 0.5f);
                step = Read(tutorial, "CurrentStep");
                Assert.AreEqual(step.GetType().GetField("followUpText").GetValue(step), Read(ui, "DialogueText"));

                Time.captureDeltaTime = 0f;
                Call(flow, "ShowSkipConfirmation");
                Call(flow, "ConfirmSkipTutorial");
                yield return null;
                yield return null;
                Assert.AreEqual(1f, Time.timeScale);
                Assert.IsNull(GameObject.Find("TutorialHUD"));
                Assert.IsNull(GameObject.Find("tutorial_ex"));
                Assert.IsNull(GameObject.Find("tutorial_swipe_arrow"));
                Assert.IsNull(GameObject.Find("TutorialDim"));
                Assert.IsNull(GameObject.Find("TutorialTimerGlow"));
                hud = GameObject.Find("Canvas").transform.Find("GameplayUI");
                Assert.AreEqual(Vector3.one, hud.Find("TimerFrame").localScale);
                Assert.IsNull(hud.Find("ScoreText").GetComponent<CanvasGroup>());
                Assert.AreNotEqual("Tutorial", Read(Get("GameFlowManager"), "State").ToString());
                Assert.AreEqual(1, PlayerPrefs.GetInt("Deadline4Sec.TutorialCompleted"));
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

        private static IEnumerator WaitForHold(Component tutorial, string input)
        {
            int frames = 0;
            while (!((bool)Read(tutorial, "IsHoldingForInput") && Read(tutorial, "PromptInput").ToString() == input) &&
                   Read(tutorial, "State").ToString() == "Active" && frames++ < 10000)
            {
                System.Threading.Thread.Sleep(1);
                yield return null;
            }
            Assert.IsTrue((bool)Read(tutorial, "IsHoldingForInput"), "Expected a hold for " + input);
            Assert.AreEqual(input, Read(tutorial, "PromptInput").ToString());
        }

        // Taps through the rules briefing like a player would.
        private static IEnumerator PassBriefing(Component tutorial)
        {
            int frames = 0;
            while (Read(tutorial, "State").ToString() == "Briefing" && frames++ < 1000)
            {
                Call(tutorial, "AdvanceBriefing");
                yield return null;
            }
            Assert.Less(frames, 1000, "Briefing did not end");
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
