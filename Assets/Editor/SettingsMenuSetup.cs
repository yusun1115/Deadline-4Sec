using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec.Editor
{
    // Settings screen (2026-11-04): SOUND and VIBRATION become two square icon
    // buttons side by side (SettingToggleIcon swaps ON / OFF art), TUTORIAL and
    // BACK stay wide buttons below. The buttons keep their names and On Click
    // (GameFlowManager.ToggleSound / ToggleVibration); the old text labels are
    // only hidden (GameFlowManager still writes them).
    // Draws the four icons first if they are missing. The open scene is changed
    // in place (left for you to save if it was already dirty); the other
    // gameplay scenes are opened, saved and closed.
    public static class SettingsMenuSetup
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/EndlessRun.unity",
            "Assets/Scenes/GrayboxCourse.unity",
            "Assets/Scenes/TestScene.unity",
        };

        private const float Square = 190f;

        [MenuItem("Deadline 4 Sec/UI/Apply Settings Icon Buttons")]
        public static void Apply()
        {
            if (Load("icon_sound_on") == null || Load("icon_vibration_off") == null)
                UiIconFactory.DrawSettingIcons();

            var open = EditorSceneManager.GetActiveScene();
            bool openWasDirty = open.isDirty;
            foreach (string path in ScenePaths)
            {
                bool isOpen = open.path == path;
                var scene = isOpen ? open : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.name == "Canvas" && root.transform.Find("SettingsPanel") is Transform panel)
                        Build(panel);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!(isOpen && openWasDirty))
                    EditorSceneManager.SaveScene(scene);
                if (!isOpen)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void Build(Transform panel)
        {
            IconButton(panel.Find("SoundButton") as RectTransform, SettingToggleIcon.Setting.Sound,
                new Vector2(-Square * 0.5f - 30f, 60f), "icon_sound_on", "icon_sound_off");
            IconButton(panel.Find("VibrationButton") as RectTransform, SettingToggleIcon.Setting.Vibration,
                new Vector2(Square * 0.5f + 30f, 60f), "icon_vibration_on", "icon_vibration_off");
            if (panel.Find("TutorialButton") is RectTransform tutorial)
                tutorial.anchoredPosition = new Vector2(0f, -120f);
            if (panel.Find("BackButton") is RectTransform back)
                back.anchoredPosition = new Vector2(0f, -240f);
        }

        private static void IconButton(RectTransform button, SettingToggleIcon.Setting setting, Vector2 position,
            string onName, string offName)
        {
            if (button == null)
                return;
            button.sizeDelta = new Vector2(Square, Square);
            button.anchoredPosition = position;
            foreach (Transform child in button)
                if (child.name == "Label" || child.name == "Stripe")
                    child.gameObject.SetActive(false);

            Transform existing = button.Find("Icon");
            GameObject go = existing != null ? existing.gameObject : new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(button, false);
            go.layer = button.gameObject.layer;
            Image icon = go.GetComponent<Image>();
            icon.sprite = Load(onName);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            RectTransform rect = icon.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(Square * 0.86f, Square * 0.86f);

            SettingToggleIcon toggle = button.TryGetComponent(out SettingToggleIcon found) ? found : button.gameObject.AddComponent<SettingToggleIcon>();
            SerializedObject data = new SerializedObject(toggle);
            data.FindProperty("setting").enumValueIndex = (int)setting;
            data.FindProperty("icon").objectReferenceValue = icon;
            data.FindProperty("onSprite").objectReferenceValue = Load(onName);
            data.FindProperty("offSprite").objectReferenceValue = Load(offName);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(UiIconFactory.Folder + name + ".png");
    }
}
