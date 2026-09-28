using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Deadline4Sec.AcceptanceTests
{
    public sealed class EditableMenuAcceptanceTests
    {
        private const string ScenePath = "Assets/Scenes/EndlessRun.unity";
        private const string BackupPath = "Temp/EditableMenuScene.backup";

        [Test]
        public void AllGameplayScenesContainSavedMenusAndInspectorButtonActions()
        {
            foreach (string path in new[] { ScenePath, "Assets/Scenes/GrayboxCourse.unity", "Assets/Scenes/TestScene.unity" })
            {
                EditorSceneManager.OpenScene(path);
                Component flow = Flow();
                SerializedObject data = new SerializedObject(flow);
                Transform title = GameObject.Find("Canvas/TitlePanel").transform;
                Assert.AreSame(title.gameObject, data.FindProperty("titlePanel").objectReferenceValue);
                Assert.IsNotNull(title.Find("TitleText"));
                Assert.IsNotNull(title.Find("SubtitleText"));
                AssertAction(title.Find("StartButton"), flow, "StartGame");
                AssertAction(title.Find("TutorialButton"), flow, "StartTutorial");
                AssertAction(title.Find("SettingsButton"), flow, "OpenSettings");
                Transform settings = ((GameObject)data.FindProperty("settingsPanel").objectReferenceValue).transform;
                AssertAction(settings.Find("SoundButton"), flow, "ToggleSound");
                AssertAction(settings.Find("VibrationButton"), flow, "ToggleVibration");
                AssertAction(settings.Find("BackButton"), flow, "CloseSettings");
                Transform result = ((GameObject)data.FindProperty("resultPanel").objectReferenceValue).transform;
                AssertAction(result.Find("MainMenuButton"), flow, "MainMenu");
                SceneFlowAcceptanceTests.AssertAllTextUsesGameFont();
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [UnityTest]
        public IEnumerator InspectorAppearanceEditsSurvivePlayAndMainMenuReload()
        {
            File.Copy(ScenePath, BackupPath, true);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Transform title = GameObject.Find("Canvas/TitlePanel").transform;
            Component label = title.Find("TitleText").GetComponent(TextType());
            TextType().GetProperty("text").SetValue(label, "MY CUSTOM TITLE");
            TextType().GetProperty("enableAutoSizing").SetValue(label, false);
            TextType().GetProperty("fontSize").SetValue(label, 44f);
            TextType().GetProperty("color").SetValue(label, Color.cyan);
            title.Find("StartButton").GetComponent<RectTransform>().anchoredPosition = new Vector2(35, -65);
            title.GetComponent<Image>().color = new Color(0.12f, 0.05f, 0.2f, 0.9f);
            CanvasScaler scaler = title.GetComponentInParent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(900, 650);
            scaler.matchWidthOrHeight = 0.35f;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.OpenScene(ScenePath);
            AssertAppearance();

            yield return new EnterPlayMode();
            Component flow = Flow();
            AssertAppearance();
            Assert.AreEqual(1, CountTitlePanels(), "Play must use the saved panel without creating duplicates.");
            Click("Canvas/TitlePanel/SettingsButton");
            Assert.AreEqual("Settings", State(flow));
            Click("Canvas/SettingsPanel/BackButton");
            Assert.AreEqual("Title", State(flow));
            AssertAppearance();
            flow.GetType().GetField("introDuration", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(flow, 0.1f);
            flow.GetType().GetField("resultDelay", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(flow, 0.01f);
            Click("Canvas/TitlePanel/StartButton");
            Assert.AreEqual("Ready", State(flow));
            float end = Time.realtimeSinceStartup + 0.3f;
            while (Time.realtimeSinceStartup < end) { yield return null; System.Threading.Thread.Sleep(1); }
            Assert.AreEqual("Playing", State(flow));
            Component timer = (Component)UnityEngine.Object.FindFirstObjectByType(Type.GetType("Deadline4Sec.GameTimer, Assembly-CSharp"));
            timer.GetType().GetMethod("TriggerGameOver").Invoke(timer, null);
            end = Time.realtimeSinceStartup + 0.1f;
            while (Time.realtimeSinceStartup < end) { yield return null; System.Threading.Thread.Sleep(1); }
            Assert.AreEqual("Result", State(flow));
            Click("Canvas/ResultPanel/MainMenuButton");
            yield return null;
            Assert.AreEqual("Title", State(Flow()));
            AssertAppearance();
            yield return new ExitPlayMode();
            EditorSceneManager.OpenScene(ScenePath);
            AssertAppearance();
        }

        [UnityTearDown]
        public IEnumerator RestoreScene()
        {
            if (EditorApplication.isPlaying)
                yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (File.Exists(BackupPath))
            {
                File.Copy(BackupPath, ScenePath, true);
                File.Delete(BackupPath);
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
            }
        }

        private static void AssertAppearance()
        {
            Transform title = GameObject.Find("Canvas/TitlePanel").transform;
            Component label = title.Find("TitleText").GetComponent(TextType());
            Assert.AreEqual("MY CUSTOM TITLE", TextType().GetProperty("text").GetValue(label));
            Assert.AreEqual(false, TextType().GetProperty("enableAutoSizing").GetValue(label));
            Assert.AreEqual(44f, TextType().GetProperty("fontSize").GetValue(label));
            Assert.AreEqual(Color.cyan, TextType().GetProperty("color").GetValue(label));
            Assert.AreEqual(new Vector2(35, -65), title.Find("StartButton").GetComponent<RectTransform>().anchoredPosition);
            Assert.AreEqual(new Color(0.12f, 0.05f, 0.2f, 0.9f), title.GetComponent<Image>().color);
            CanvasScaler scaler = title.GetComponentInParent<CanvasScaler>();
            Assert.AreEqual(new Vector2(900, 650), scaler.referenceResolution);
            Assert.AreEqual(0.35f, scaler.matchWidthOrHeight);
        }

        private static int CountTitlePanels()
        {
            int count = 0;
            foreach (Transform child in UnityEngine.Object.FindFirstObjectByType<Canvas>().transform)
                if (child.name == "TitlePanel") count++;
            return count;
        }
        private static Type TextType() => Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
        private static Component Flow() => (Component)UnityEngine.Object.FindFirstObjectByType(Type.GetType("Deadline4Sec.GameFlowManager, Assembly-CSharp"));
        private static string State(Component flow) => flow.GetType().GetProperty("State").GetValue(flow).ToString();
        private static void Click(string path) => GameObject.Find(path).GetComponent<Button>().onClick.Invoke();
        private static void AssertAction(Transform obj, Component flow, string method)
        {
            Button button = obj.GetComponent<Button>();
            Assert.AreEqual(1, button.onClick.GetPersistentEventCount(), obj.name);
            Assert.AreSame(flow, button.onClick.GetPersistentTarget(0));
            Assert.AreEqual(method, button.onClick.GetPersistentMethodName(0));
        }
    }
}
