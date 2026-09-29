using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deadline4Sec.AcceptanceTests
{
    public sealed class EditableMenuAcceptanceTests
    {
        private const string ScenePath = "Assets/Scenes/EndlessRun.unity";
        private const string BackupPath = "Temp/EditableMenuScene.backup";
        private const string TutorialBackupPath = "Temp/EditableMenuTutorialPreference.backup";
        private const string TutorialKey = "Deadline4Sec.TutorialCompleted";

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
                Assert.IsNull(title.Find("StartButton"));
                Assert.IsNull(title.Find("TutorialButton"));
                Assert.IsNotNull(title.Find("TapToStartText"));
                AssertAction(title.Find("TitleTapArea"), flow, "StartFromTitle");
                AssertAction(title.Find("SettingsButton"), flow, "OpenSettings");
                Transform settings = ((GameObject)data.FindProperty("settingsPanel").objectReferenceValue).transform;
                AssertAction(settings.Find("SoundButton"), flow, "ToggleSound");
                AssertAction(settings.Find("VibrationButton"), flow, "ToggleVibration");
                AssertAction(settings.Find("TutorialButton"), flow, "StartTutorial");
                AssertAction(settings.Find("BackButton"), flow, "CloseSettings");
                Transform skipUI = ((GameObject)data.FindProperty("tutorialSkipUI").objectReferenceValue).transform;
                Transform confirmation = ((GameObject)data.FindProperty("tutorialSkipConfirmation").objectReferenceValue).transform;
                AssertAction(skipUI.Find("SkipButton"), flow, "ShowSkipConfirmation");
                AssertAction(confirmation.Find("Dialog/YesButton"), flow, "ConfirmSkipTutorial");
                AssertAction(confirmation.Find("Dialog/NoButton"), flow, "CancelSkipTutorial");
                Assert.IsFalse(skipUI.gameObject.activeSelf);
                Assert.IsFalse(confirmation.gameObject.activeSelf);
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
            BackupTutorialCompletion();
            PlayerPrefs.SetInt(TutorialKey, 1);
            PlayerPrefs.Save();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Transform title = GameObject.Find("Canvas/TitlePanel").transform;
            Component label = title.Find("TapToStartText").GetComponent(TextType());
            TextType().GetProperty("text").SetValue(label, "MY CUSTOM TAP");
            TextType().GetProperty("enableAutoSizing").SetValue(label, false);
            TextType().GetProperty("fontSize").SetValue(label, 44f);
            TextType().GetProperty("color").SetValue(label, Color.cyan);
            title.Find("TapToStartText").GetComponent<RectTransform>().anchoredPosition = new Vector2(35, -65);
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
            flow.GetType().GetField("deathPresentationDuration", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(flow, 0.5f);
            Click("Canvas/TitlePanel/TitleTapArea");
            Assert.AreEqual("Ready", State(flow));
            float end = Time.realtimeSinceStartup + 0.3f;
            while (Time.realtimeSinceStartup < end) { yield return null; System.Threading.Thread.Sleep(1); }
            Assert.AreEqual("Playing", State(flow));
            Component timer = (Component)UnityEngine.Object.FindFirstObjectByType(Type.GetType("Deadline4Sec.GameTimer, Assembly-CSharp"));
            timer.GetType().GetMethod("TriggerGameOver").Invoke(timer, null);
            end = Time.realtimeSinceStartup + 0.65f;
            while (Time.realtimeSinceStartup < end) { yield return null; System.Threading.Thread.Sleep(1); }
            Assert.AreEqual("RevivePrompt", State(flow));
            flow.GetType().GetMethod("GiveUpRevive").Invoke(flow, null);
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

        [UnityTest]
        public IEnumerator FirstTapStartsTutorialAndSkipRequiresConfirmation()
        {
            BackupTutorialCompletion();
            PlayerPrefs.DeleteKey(TutorialKey);
            PlayerPrefs.Save();
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            Component flow = Flow();
            Canvas.ForceUpdateCanvases();
            GraphicRaycaster raycaster = UnityEngine.Object.FindFirstObjectByType<Canvas>()
                .GetComponent<GraphicRaycaster>();
            var pointer = new PointerEventData(EventSystem.current)
                { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            raycaster.Raycast(pointer, hits);
            Assert.IsNotEmpty(hits);
            Assert.AreEqual("TitleTapArea", hits[0].gameObject.name,
                "Decorative title images must not intercept taps.");
            hits.Clear();
            pointer.position = RectTransformUtility.WorldToScreenPoint(null,
                GameObject.Find("Canvas/TitlePanel/SettingsButton").transform.position);
            raycaster.Raycast(pointer, hits);
            Assert.IsNotEmpty(hits);
            Assert.AreEqual("SettingsButton", hits[0].gameObject.name,
                "Settings must receive taps above the full-screen start area.");
            Click("Canvas/TitlePanel/TitleTapArea");
            Assert.AreEqual("Tutorial", State(flow));
            Assert.AreEqual(0, PlayerPrefs.GetInt(TutorialKey, 0));
            GameObject skip = GameObject.Find("Canvas/TutorialSkipUI");
            Assert.IsNotNull(skip);
            Component tutorial = (Component)UnityEngine.Object.FindFirstObjectByType(
                Type.GetType("Deadline4Sec.TutorialController, Assembly-CSharp"));
            float deadline = Time.realtimeSinceStartup + 2f;
            while (tutorial.GetType().GetProperty("State").GetValue(tutorial).ToString() != "Active" &&
                   Time.realtimeSinceStartup < deadline)
            {
                System.Threading.Thread.Sleep(1);
                yield return null;
            }
            Assert.AreEqual("Active", tutorial.GetType().GetProperty("State").GetValue(tutorial).ToString());
            Component timer = (Component)UnityEngine.Object.FindFirstObjectByType(
                Type.GetType("Deadline4Sec.GameTimer, Assembly-CSharp"));
            Click("Canvas/TutorialSkipUI/SkipButton");
            Assert.IsTrue((bool)flow.GetType().GetProperty("IsSkipConfirmationOpen").GetValue(flow));
            Assert.IsNotNull(GameObject.Find("Canvas/TutorialSkipConfirmation"));
            SceneFlowAcceptanceTests.CaptureIntroFrame(Camera.main, "tutorial-skip-confirmation");
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsFalse((bool)flow.GetType().GetProperty("CanReceiveInput").GetValue(flow));
            float remaining = (float)timer.GetType().GetProperty("RemainingTime").GetValue(timer);
            Vector3 position = ((Component)UnityEngine.Object.FindFirstObjectByType(
                Type.GetType("Deadline4Sec.PlayerController, Assembly-CSharp"))).transform.position;
            deadline = Time.realtimeSinceStartup + 0.15f;
            while (Time.realtimeSinceStartup < deadline) { System.Threading.Thread.Sleep(1); yield return null; }
            Assert.AreEqual(remaining, timer.GetType().GetProperty("RemainingTime").GetValue(timer));
            Assert.AreEqual(position, ((Component)UnityEngine.Object.FindFirstObjectByType(
                Type.GetType("Deadline4Sec.PlayerController, Assembly-CSharp"))).transform.position);
            Click("Canvas/TutorialSkipConfirmation/Dialog/NoButton");
            Assert.AreEqual("Tutorial", State(flow));
            Assert.AreEqual(0, PlayerPrefs.GetInt(TutorialKey, 0));
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsTrue((bool)flow.GetType().GetProperty("CanReceiveInput").GetValue(flow));
            Click("Canvas/TutorialSkipUI/SkipButton");
            Click("Canvas/TutorialSkipConfirmation/Dialog/YesButton");
            yield return null;
            Assert.AreEqual(1, PlayerPrefs.GetInt(TutorialKey, 0));
            Assert.AreEqual("Ready", State(Flow()));
            Assert.IsNull(GameObject.Find("TutorialHUD"));
            Assert.AreEqual(1f, Time.timeScale);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SettingsTutorialButtonReplaysPracticeEvenAfterCompletion()
        {
            BackupTutorialCompletion();
            PlayerPrefs.SetInt(TutorialKey, 1);
            PlayerPrefs.Save();
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            Click("Canvas/TitlePanel/SettingsButton");
            Assert.AreEqual("Settings", State(Flow()));
            Click("Canvas/SettingsPanel/TutorialButton");
            Assert.AreEqual("Tutorial", State(Flow()));
            Assert.IsNotNull(GameObject.Find("Canvas/TutorialSkipUI/SkipButton"));
            Flow().GetType().GetMethod("FinishTutorial").Invoke(Flow(), new object[] { false });
            yield return null;
            Assert.AreEqual("Title", State(Flow()));
            Click("Canvas/TitlePanel/TitleTapArea");
            Assert.AreEqual("Ready", State(Flow()));
            yield return new ExitPlayMode();
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
            if (File.Exists(TutorialBackupPath))
            {
                string original = File.ReadAllText(TutorialBackupPath);
                if (original == "missing") PlayerPrefs.DeleteKey(TutorialKey);
                else PlayerPrefs.SetInt(TutorialKey, int.Parse(original));
                PlayerPrefs.Save();
                File.Delete(TutorialBackupPath);
            }
        }

        private static void AssertAppearance()
        {
            Transform title = GameObject.Find("Canvas/TitlePanel").transform;
            Component label = title.Find("TapToStartText").GetComponent(TextType());
            Assert.AreEqual("MY CUSTOM TAP", TextType().GetProperty("text").GetValue(label));
            Assert.AreEqual(false, TextType().GetProperty("enableAutoSizing").GetValue(label));
            Assert.AreEqual(44f, TextType().GetProperty("fontSize").GetValue(label));
            Assert.AreEqual(Color.cyan, TextType().GetProperty("color").GetValue(label));
            Assert.AreEqual(new Vector2(35, -65), title.Find("TapToStartText").GetComponent<RectTransform>().anchoredPosition);
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
        private static void BackupTutorialCompletion() => File.WriteAllText(TutorialBackupPath,
            PlayerPrefs.HasKey(TutorialKey) ? PlayerPrefs.GetInt(TutorialKey).ToString() : "missing");
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
