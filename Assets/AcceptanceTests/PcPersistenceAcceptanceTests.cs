using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    public sealed class PcPersistenceAcceptanceTests
    {
        private static Type Find(string name) => Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
        private static Component Get(string name) => (Component)UnityEngine.Object.FindFirstObjectByType(Find(name));
        private static void Call(Component component, string name) => component.GetType().GetMethod(name).Invoke(component, null);
        private static void Probe(string name, params object[] args) => Type.GetType(
            "Deadline4Sec.Editor.PcPersistenceProbe, Assembly-CSharp-Editor").GetMethod(name).Invoke(null, args);

        [UnityTest]
        [Explicit("Two-process probe: run this exact test, then PcPersistenceProbe.VerifyAndRestore in a fresh Unity process.")]
        public IEnumerator ActualKillResultLeavesSavedRecordsForASecondProcess()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            float oldDelta = Time.captureDeltaTime;
            bool readyForRestart = false;
            try
            {
                Probe("Prepare");
                Component flow = Get("GameFlowManager"), player = Get("PlayerController"), spawner = Get("PatternSpawner");
                spawner.GetType().GetField("random", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(spawner, new System.Random(12345));
                Time.captureDeltaTime = 1f / 60f;
                Call(flow, "StartGame");
                int frames = 0;
                while (flow.GetType().GetProperty("State").GetValue(flow).ToString() != "Playing" && frames++ < 10000)
                { System.Threading.Thread.Sleep(1); yield return null; }
                Assert.Less(frames, 10000);
                Component firstEnemy = null;
                foreach (Component enemy in UnityEngine.Object.FindObjectsByType(Find("Enemy"), FindObjectsSortMode.None))
                    if (firstEnemy == null || enemy.transform.position.z < firstEnemy.transform.position.z) firstEnemy = enemy;
                Assert.IsNotNull(firstEnemy);
                while (firstEnemy.transform.position.z - player.transform.position.z > 2.6f && frames++ < 12000)
                    yield return null;
                Assert.Less(frames, 12000);
                Call(player, "RequestMoveRight");
                yield return null;
                Component run = Get("RunManager");
                Assert.AreEqual(100, run.GetType().GetProperty("CurrentScore").GetValue(run));
                float distance = (float)run.GetType().GetProperty("Distance").GetValue(run);
                Call(Get("GameTimer"), "TriggerGameOver");
                Probe("CaptureExpected", distance);
                readyForRestart = true;
            }
            finally
            {
                Time.captureDeltaTime = oldDelta;
                if (!readyForRestart) Probe("Restore");
            }
            yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
