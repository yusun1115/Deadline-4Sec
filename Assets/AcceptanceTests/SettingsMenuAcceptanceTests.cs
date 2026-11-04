using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Deadline4Sec.AcceptanceTests
{
    // 2026-11-04: Settings has two square icon buttons for sound and vibration;
    // a tap toggles the setting and the icon shows ON / OFF art.
    public sealed class SettingsMenuAcceptanceTests
    {
        private const string SoundKey = "Deadline4Sec.SoundEnabled";
        private const string VibrationKey = "Deadline4Sec.VibrationEnabled";
        private const string TutorialKey = "Deadline4Sec.TutorialCompleted";
        private static readonly Type Toggle = Type.GetType("Deadline4Sec.SettingToggleIcon, Assembly-CSharp");

        [Test]
        public void AllScenesHaveSquareSoundAndVibrationIconButtons()
        {
            foreach (string path in new[] { "Assets/Scenes/EndlessRun.unity", "Assets/Scenes/GrayboxCourse.unity", "Assets/Scenes/TestScene.unity" })
            {
                EditorSceneManager.OpenScene(path);
                Transform settings = GameObject.Find("Canvas").transform.Find("SettingsPanel");
                foreach (string name in new[] { "SoundButton", "VibrationButton" })
                {
                    RectTransform button = (RectTransform)settings.Find(name);
                    Assert.IsNotNull(button.GetComponent(Toggle), path + ": " + name);
                    Assert.AreEqual(button.sizeDelta.x, button.sizeDelta.y, path + ": " + name + " is square");
                    Assert.IsNotNull(button.Find("Icon").GetComponent<Image>().sprite, path + ": " + name);
                    Assert.IsFalse(button.Find("Label").gameObject.activeSelf, path + ": no text label");
                }
            }
        }

        [UnityTest]
        public IEnumerator TappingAnIconButtonTogglesTheSettingAndItsIcon()
        {
            PlayerPrefs.SetInt(TutorialKey, 1); // no first-run tutorial in the way
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            int sound = PlayerPrefs.GetInt(SoundKey, 1), vibration = PlayerPrefs.GetInt(VibrationKey, 1);
            try
            {
                PlayerPrefs.SetInt(SoundKey, 1);
                PlayerPrefs.SetInt(VibrationKey, 1);
                Type flowType = Type.GetType("Deadline4Sec.GameFlowManager, Assembly-CSharp");
                Component flow = null;
                for (int frames = 0; frames < 600; frames++)
                {
                    flow = (Component)UnityEngine.Object.FindFirstObjectByType(flowType);
                    if (flow != null && flowType.GetProperty("State").GetValue(flow).ToString() == "Title")
                        break;
                    yield return null;
                }
                flowType.GetMethod("OpenSettings").Invoke(flow, null);
                yield return null;
                Assert.AreEqual("Settings", flowType.GetProperty("State").GetValue(flow).ToString());
                Transform settings = GameObject.Find("Canvas/SettingsPanel").transform;
                foreach (var (name, key, on, off) in new[] {
                    ("SoundButton", SoundKey, "icon_sound_on", "icon_sound_off"),
                    ("VibrationButton", VibrationKey, "icon_vibration_on", "icon_vibration_off") })
                {
                    Transform button = settings.Find(name);
                    Image icon = button.Find("Icon").GetComponent<Image>();
                    Assert.AreEqual(on, icon.sprite.name, name);
                    button.GetComponent<Button>().onClick.Invoke();
                    Assert.AreEqual(0, PlayerPrefs.GetInt(key, 1), name + " turned off");
                    Assert.AreEqual(off, icon.sprite.name, name);
                    button.GetComponent<Button>().onClick.Invoke();
                    Assert.AreEqual(1, PlayerPrefs.GetInt(key, 1), name + " turned on");
                    Assert.AreEqual(on, icon.sprite.name, name);
                }
            }
            finally
            {
                PlayerPrefs.SetInt(SoundKey, sound);
                PlayerPrefs.SetInt(VibrationKey, vibration);
                PlayerPrefs.Save();
            }
            yield return new ExitPlayMode();
        }

        // SessionState survives the play-mode domain reload.
        [SetUp]
        public void BackupTutorialFlag() =>
            UnityEditor.SessionState.SetInt(TutorialKey, PlayerPrefs.HasKey(TutorialKey) ? PlayerPrefs.GetInt(TutorialKey) : -1);

        [TearDown]
        public void Restore()
        {
            int tutorial = UnityEditor.SessionState.GetInt(TutorialKey, -1);
            if (tutorial < 0)
                PlayerPrefs.DeleteKey(TutorialKey);
            else
                PlayerPrefs.SetInt(TutorialKey, tutorial);
            PlayerPrefs.Save();
            if (!Application.isPlaying)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
