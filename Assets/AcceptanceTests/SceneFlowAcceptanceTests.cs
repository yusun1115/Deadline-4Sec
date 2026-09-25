using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    public sealed class SceneFlowAcceptanceTests
    {
        private static Type Find(string name)
        {
            Type type = Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
            Assert.IsNotNull(type, name);
            return type;
        }

        [UnityTest]
        public IEnumerator TitleStartWaitsForGoBeforeRunning()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();

            Component flow = (Component)UnityEngine.Object.FindFirstObjectByType(Find("GameFlowManager"));
            Component timer = (Component)UnityEngine.Object.FindFirstObjectByType(Find("GameTimer"));
            Component player = (Component)UnityEngine.Object.FindFirstObjectByType(Find("PlayerController"));
            Component spawner = (Component)UnityEngine.Object.FindFirstObjectByType(Find("PatternSpawner"));
            Assert.IsNotNull(flow);
            Assert.AreEqual("Title", flow.GetType().GetProperty("State").GetValue(flow).ToString());
            Assert.IsFalse((bool)timer.GetType().GetProperty("IsRunning").GetValue(timer));
            Assert.IsFalse(((Behaviour)player).enabled);
            Assert.AreEqual(0, spawner.transform.childCount);

            Type preferences = Find("GamePreferences");
            PropertyInfo sound = preferences.GetProperty("SoundEnabled");
            PropertyInfo vibration = preferences.GetProperty("VibrationEnabled");
            bool oldSound = (bool)sound.GetValue(null);
            bool oldVibration = (bool)vibration.GetValue(null);
            flow.GetType().GetMethod("OpenSettings").Invoke(flow, null);
            Assert.AreEqual("Settings", flow.GetType().GetProperty("State").GetValue(flow).ToString());
            flow.GetType().GetMethod("ToggleSound",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(flow, null);
            flow.GetType().GetMethod("ToggleVibration",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(flow, null);
            Assert.AreEqual(!oldSound, (bool)sound.GetValue(null));
            Assert.AreEqual(!oldVibration, (bool)vibration.GetValue(null));
            sound.SetValue(null, oldSound);
            vibration.SetValue(null, oldVibration);
            flow.GetType().GetMethod("CloseSettings").Invoke(flow, null);
            Assert.AreEqual("Title", flow.GetType().GetProperty("State").GetValue(flow).ToString());

            SetFloat(flow, "readyDuration", 0.01f);
            SetFloat(flow, "countdownStepDuration", 0.01f);
            SetFloat(flow, "goDisplayDuration", 0.01f);
            flow.GetType().GetMethod("StartGame").Invoke(flow, null);
            Assert.AreEqual("Ready", flow.GetType().GetProperty("State").GetValue(flow).ToString());
            Assert.IsFalse((bool)timer.GetType().GetProperty("IsRunning").GetValue(timer));
            yield return AdvanceSeconds(0.25f);
            Assert.AreEqual("Playing", flow.GetType().GetProperty("State").GetValue(flow).ToString());
            Assert.IsTrue((bool)timer.GetType().GetProperty("IsRunning").GetValue(timer));
            Assert.IsTrue(((Behaviour)player).enabled);
            Assert.Greater(spawner.transform.childCount, 0);
            yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [UnityTest]
        public IEnumerator ResultRetryStartsFreshCountdown()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            Component flow = Get("GameFlowManager");
            Component timer = Get("GameTimer");
            SetFloat(flow, "readyDuration", 0.01f);
            SetFloat(flow, "countdownStepDuration", 0.01f);
            SetFloat(flow, "resultDelay", 0.01f);
            flow.GetType().GetMethod("StartGame").Invoke(flow, null);
            yield return AdvanceSeconds(0.2f);
            Assert.AreEqual("Playing", State(flow));
            timer.GetType().GetMethod("TriggerGameOver").Invoke(timer, null);
            yield return AdvanceSeconds(0.1f);
            Assert.AreEqual("Result", State(flow));
            flow.GetType().GetMethod("Retry").Invoke(flow, null);
            yield return null;
            Component freshFlow = Get("GameFlowManager");
            Component freshTimer = Get("GameTimer");
            Assert.That(State(freshFlow), Is.EqualTo("Ready").Or.EqualTo("Countdown"));
            Assert.IsFalse((bool)freshTimer.GetType().GetProperty("IsRunning").GetValue(freshTimer));
            yield return AdvanceRealtimeSeconds(4f);
            Assert.AreEqual("Playing", State(freshFlow));
            Assert.IsTrue((bool)freshTimer.GetType().GetProperty("IsRunning").GetValue(freshTimer));
            Assert.IsFalse((bool)freshTimer.GetType().GetProperty("IsGameOver").GetValue(freshTimer));
            yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [UnityTest]
        public IEnumerator ResultMainMenuReturnsToTitle()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            Component flow = Get("GameFlowManager");
            Component timer = Get("GameTimer");
            SetFloat(flow, "readyDuration", 0.01f);
            SetFloat(flow, "countdownStepDuration", 0.01f);
            SetFloat(flow, "resultDelay", 0.01f);
            flow.GetType().GetMethod("StartGame").Invoke(flow, null);
            yield return AdvanceSeconds(0.2f);
            timer.GetType().GetMethod("TriggerGameOver").Invoke(timer, null);
            yield return AdvanceSeconds(0.1f);
            Assert.AreEqual("Result", State(flow));
            flow.GetType().GetMethod("MainMenu").Invoke(flow, null);
            yield return null;
            Component freshFlow = Get("GameFlowManager");
            Component freshTimer = Get("GameTimer");
            Assert.AreEqual("Title", State(freshFlow));
            Assert.IsFalse((bool)freshTimer.GetType().GetProperty("IsRunning").GetValue(freshTimer));
            yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static Component Get(string name)
        {
            Component component = (Component)UnityEngine.Object.FindFirstObjectByType(Find(name));
            Assert.IsNotNull(component, name);
            return component;
        }

        private static string State(Component flow)
        {
            return flow.GetType().GetProperty("State").GetValue(flow).ToString();
        }

        private static void SetFloat(Component target, string name, float value)
        {
            target.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        }

        private static IEnumerator AdvanceSeconds(float seconds)
        {
            float end = Time.time + seconds;
            int frames = 0;
            while (Time.time < end && frames++ < 2000)
                yield return null;
            Assert.Less(frames, 2000);
        }

        private static IEnumerator AdvanceRealtimeSeconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            int frames = 0;
            while (Time.realtimeSinceStartup < end && frames++ < 10000)
            {
                System.Threading.Thread.Sleep(1);
                yield return null;
            }
            Assert.Less(frames, 10000);
        }
    }
}
