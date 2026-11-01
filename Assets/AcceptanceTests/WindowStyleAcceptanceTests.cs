using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec.AcceptanceTests
{
    // 2026-11-01: every window takes the ranking / achievement style (no
    // generated gothic panel or button art left; the in-game timer frame keeps
    // its own art), and the Upgrade screen is one list scrolling the whole
    // screen with the consumables as its last entry.
    public sealed class WindowStyleAcceptanceTests
    {
        [Test]
        public void WindowsUseTheRankingStyleAndUpgradesScrollAsOneList()
        {
            foreach (string path in new[] { "Assets/Scenes/EndlessRun.unity", "Assets/Scenes/GrayboxCourse.unity", "Assets/Scenes/TestScene.unity" })
            {
                EditorSceneManager.OpenScene(path);
                Transform canvas = GameObject.Find("Canvas").transform;
                foreach (Image image in canvas.GetComponentsInChildren<Image>(true))
                    if (image.sprite != null)
                        Assert.IsFalse(image.sprite.name.Contains("gothic"), path + ": " + image.name + " still uses " + image.sprite.name);

                Image timer = canvas.Find("GameplayUI/TimerFrame").GetComponent<Image>();
                Assert.IsNotNull(timer.sprite, "The in-game timer keeps its frame art.");

                foreach (string button in new[] { "SettingsPanel/SoundButton", "ResultPanel/RetryButton", "RevivePanel/GiveUpButton",
                    "TutorialSkipConfirmation/Dialog/YesButton", "UpgradePanel/UpgradePopup/Dialog/OkButton" })
                {
                    Transform b = canvas.Find(button);
                    Assert.IsNotNull(b.Find("Stripe"), path + ": " + button);
                    Assert.IsNotNull(b.GetComponent<Outline>(), path + ": " + button);
                }
                foreach (string panel in new[] { "SettingsPanel/Frame", "ResultPanel", "RevivePanel/Frame", "UpgradePanel/Frame" })
                    Assert.IsNotNull(canvas.Find(panel + "/TopAccent"), path + ": " + panel);

                Transform content = canvas.Find("UpgradePanel/ItemScrollView/Viewport/Content");
                Transform area = content.Find("ConsumableArea");
                Assert.IsNotNull(area, path + ": consumables are inside the scrolling list");
                Assert.AreEqual(content.childCount - 1, area.GetSiblingIndex(), path + ": consumables come last");
                System.Type row = System.Type.GetType("Deadline4Sec.ConsumableShopRow, Assembly-CSharp");
                Assert.AreEqual(2, area.GetComponentsInChildren(row, true).Length, path);
                Assert.IsTrue(canvas.Find("UpgradePanel/ItemScrollView").GetComponent<ScrollRect>().vertical, path);
            }
        }

        // Later play-mode tests build their own runner in the open scene.
        [TearDown]
        public void LeaveAnEmptyScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }
}
