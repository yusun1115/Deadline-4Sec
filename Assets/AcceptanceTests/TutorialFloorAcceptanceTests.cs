using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    // 2026-11-13: the tutorial floor lay in the same plane as the scene's Start
    // Floor; the two flickered under the runner. Only one surface may show.
    public sealed class TutorialFloorAcceptanceTests
    {
        [UnityTest]
        public IEnumerator OnlyOneFloorSurfaceUnderTheTutorial()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            yield return null;
            Component flow = (Component)UnityEngine.Object.FindFirstObjectByType(Type.GetType("Deadline4Sec.GameFlowManager, Assembly-CSharp"));
            flow.GetType().GetMethod("StartTutorial").Invoke(flow, null);
            yield return null;
            GameObject tutorialFloor = GameObject.Find("Tutorial Floor");
            Assert.IsNotNull(tutorialFloor);
            Bounds floor = tutorialFloor.GetComponent<Renderer>().bounds;
            foreach (Renderer other in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (other.gameObject == tutorialFloor || !other.enabled)
                    continue;
                Bounds b = other.bounds;
                bool coplanar = Mathf.Abs(b.max.y - floor.max.y) < 0.02f && b.size.y < 1f &&
                    b.min.x < floor.max.x && b.max.x > floor.min.x && b.min.z < floor.max.z && b.max.z > floor.min.z;
                Assert.IsFalse(coplanar, other.name + " shows in the tutorial floor's plane.");
            }
            Assert.IsFalse(GameObject.Find("Start Floor").GetComponent<Renderer>().enabled);
            flow.GetType().GetMethod("FinishTutorial").Invoke(flow, new object[] { false });
            yield return null;
            yield return null;
            Assert.IsTrue(GameObject.Find("Start Floor").GetComponent<Renderer>().enabled, "Back after the tutorial.");
            yield return new ExitPlayMode();
        }
    }
}
