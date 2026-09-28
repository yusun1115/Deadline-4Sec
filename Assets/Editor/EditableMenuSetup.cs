using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Deadline4Sec.Editor
{
    // One-time migration. Runtime never calls this or rebuilds authored menus.
    public static class EditableMenuSetup
    {
        public static void PrepareScenes()
        {
            foreach (string path in new[] { "Assets/Scenes/EndlessRun.unity",
                "Assets/Scenes/GrayboxCourse.unity", "Assets/Scenes/TestScene.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                GameFlowManager flow = UnityEngine.Object.FindFirstObjectByType<GameFlowManager>();
                if (flow == null)
                    throw new InvalidOperationException("Missing GameFlowManager: " + path);
                SerializedObject data = new SerializedObject(flow);
                GameObject result = (GameObject)data.FindProperty("resultPanel").objectReferenceValue;
                Canvas canvas = result.GetComponentInParent<Canvas>();
                List<Button> buttons = new List<Button>();

                Transform title = canvas.transform.Find("TitlePanel");
                if (title == null)
                {
                    title = Overlay(canvas.transform, "TitlePanel").transform;
                    Label(title, "TitleText", "DEADLINE: 4 SEC", new Vector2(0, 185), new Vector2(680, 110), 58);
                    Label(title, "SubtitleText", "RISK TO LIVE", new Vector2(0, 105), new Vector2(600, 65), 28);
                    MenuButton(title, "StartButton", "START", new Vector2(0, -45), new Vector2(360, 84), flow.StartGame);
                    MenuButton(title, "TutorialButton", "TUTORIAL", new Vector2(0, -145), new Vector2(360, 80), flow.StartTutorial);
                    MenuButton(title, "SettingsButton", "SETTINGS", new Vector2(0, -245), new Vector2(360, 84), flow.OpenSettings);
                    CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(800, 600);
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 0;
                }
                Transform settings = canvas.transform.Find("SettingsPanel");
                if (settings == null)
                {
                    settings = Overlay(canvas.transform, "SettingsPanel").transform;
                    Label(settings, "HeadingText", "SETTINGS", new Vector2(0, 175), new Vector2(600, 90), 52);
                    MenuButton(settings, "SoundButton", "SOUND   ON", new Vector2(0, 50), new Vector2(420, 80), flow.ToggleSound);
                    MenuButton(settings, "VibrationButton", "VIBRATION   ON", new Vector2(0, -50), new Vector2(420, 80), flow.ToggleVibration);
                    MenuButton(settings, "BackButton", "BACK", new Vector2(0, -175), new Vector2(360, 80), flow.CloseSettings);
                }
                if (result.transform.Find("MainMenuButton") == null)
                {
                    MenuButton(result.transform, "MainMenuButton", "MAIN MENU", new Vector2(0, -350), new Vector2(360, 80), flow.MainMenu);
                    Button retry = (Button)data.FindProperty("retryButton").objectReferenceValue;
                    retry.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -255);
                }
                data.FindProperty("titlePanel").objectReferenceValue = title.gameObject;
                data.FindProperty("settingsPanel").objectReferenceValue = settings.gameObject;
                data.FindProperty("soundSettingText").objectReferenceValue = settings.Find("SoundButton/Label").GetComponent<TMP_Text>();
                data.FindProperty("vibrationSettingText").objectReferenceValue = settings.Find("VibrationButton/Label").GetComponent<TMP_Text>();
                buttons.AddRange(title.GetComponentsInChildren<Button>(true));
                buttons.AddRange(settings.GetComponentsInChildren<Button>(true));
                buttons.Add(result.transform.Find("MainMenuButton").GetComponent<Button>());
                SerializedProperty buttonList = data.FindProperty("menuButtons");
                buttonList.arraySize = buttons.Count;
                for (int i = 0; i < buttons.Count; i++)
                    buttonList.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
                data.ApplyModifiedPropertiesWithoutUndo();
                title.gameObject.SetActive(true);
                settings.gameObject.SetActive(false);
                foreach (string field in new[] { "gameplayUI", "countdownUI", "resultPanel" })
                    ((GameObject)data.FindProperty(field).objectReferenceValue).SetActive(false);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("Saved editable menu: " + path);
            }
            AssetDatabase.SaveAssets();
        }

        private static GameObject Overlay(Transform parent, string name)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.025f, 0.03f, 0.055f, 0.97f);
            return panel;
        }

        private static TMP_Text Label(Transform parent, string name, string text,
            Vector2 position, Vector2 size, float fontSize)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TMP_Text label = obj.GetComponent<TMP_Text>();
            GameFont.Apply(label);
            label.text = text;
            label.color = Color.white;
            label.fontSize = label.fontSizeMax = fontSize;
            label.fontSizeMin = 18;
            label.enableAutoSizing = true;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static void MenuButton(Transform parent, string name, string text,
            Vector2 position, Vector2 size, UnityAction action)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = obj.GetComponent<Image>();
            image.color = new Color(0.72f, 0.09f, 0.24f, 1);
            Button button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            UnityEventTools.AddPersistentListener(button.onClick, action);
            TMP_Text label = Label(obj.transform, "Label", text, Vector2.zero, size - new Vector2(20, 10), 36);
            // Stretch labels with the button so resizing only needs its RectTransform.
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10, 5);
            labelRect.offsetMax = new Vector2(-10, -5);
        }
    }
}
