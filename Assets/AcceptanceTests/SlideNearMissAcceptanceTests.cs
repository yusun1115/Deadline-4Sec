using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    // 2026-11-22: sliding under a Slide Obstacle at the last moment is a Near
    // Miss. The slide lowers the runner the moment it starts, so the "heading
    // into it" check for a beam reaches further than for other obstacles; an
    // early slide (long before the beam) still earns nothing.
    public sealed class SlideNearMissAcceptanceTests
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

        private static Type T(string name) => Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
        private static Component Get(string name) => (Component)UnityEngine.Object.FindFirstObjectByType(T(name));
        private static object Read(object target, string name) => target.GetType().GetProperty(name).GetValue(target);

        [Test]
        public void TheSlideObstaclePrefabCarriesTheSlideWindow()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Obstacle/Slide Obstacle.prefab");
            var data = new SerializedObject(prefab.GetComponent(T("Obstacle")));
            Assert.AreEqual("SlideObstacle", data.FindProperty("obstacleType").enumNames[data.FindProperty("obstacleType").enumValueIndex]);
            Assert.GreaterOrEqual(data.FindProperty("slideLastMomentDistance").floatValue, 4f);
        }

        [UnityTest]
        public IEnumerator ALastMomentSlideUnderTheBeamIsANearMiss()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            int tutorial = PlayerPrefs.GetInt("Deadline4Sec.TutorialCompleted", -1);
            float oldCapture = Time.captureDeltaTime;
            try
            {
                PlayerPrefs.SetInt("Deadline4Sec.TutorialCompleted", 1);
                Time.captureDeltaTime = 1f / 60f;
                Component run = Get("RunManager");
                Component spawner = Get("PatternSpawner");
                Component timer = Get("GameTimer");
                Component player = Get("PlayerController");
                run.GetType().GetField("baseForwardSpeed", Hidden).SetValue(run, 14f);
                run.GetType().GetField("maxForwardSpeed", Hidden).SetValue(run, 14f);
                // Open ground: a rest pattern, its Souls cleared as they come.
                spawner.GetType().GetField("debugRepeatPattern", Hidden).SetValue(spawner,
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Pattern/Pattern_V2_SoulWalk.prefab").GetComponent(T("CoursePattern")));
                int nearMisses = 0;
                Action counted = () => nearMisses++;
                run.GetType().GetEvent("NearMissRecorded").AddEventHandler(run, counted);
                Component flow = Get("GameFlowManager");
                flow.GetType().GetMethod("StartGame").Invoke(flow, null);
                float guard = Time.realtimeSinceStartup + 20f;
                while (!(bool)Read(timer, "IsRunning") && Time.realtimeSinceStartup < guard)
                    yield return null;
                Assert.IsTrue((bool)Read(timer, "IsRunning"));
                CharacterController body = player.GetComponent<CharacterController>();
                GameObject beamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Obstacle/Slide Obstacle.prefab");

                // Slide this far (m, runner front to beam front) before the beam.
                foreach ((float slideAt, bool expected) in new[] { (1.5f, true), (4f, true), (9f, false) })
                {
                    int before = nearMisses;
                    GameObject beam = UnityEngine.Object.Instantiate(beamPrefab,
                        new Vector3(player.transform.position.x, 2.56f, player.transform.position.z + 28f), Quaternion.identity);
                    Collider hazard = beam.GetComponent<Collider>();
                    yield return null;
                    bool slid = false;
                    int frames = 0;
                    while (body.bounds.min.z < hazard.bounds.max.z + 2f && frames++ < 600)
                    {
                        ClearSouls();
                        if (!slid && hazard.bounds.min.z - body.bounds.max.z <= slideAt)
                        {
                            player.GetType().GetMethod("RequestSlide").Invoke(player, null);
                            slid = true;
                        }
                        Assert.IsFalse((bool)Read(timer, "IsGameOver"), $"Slid {slideAt} m before the beam and hit it.");
                        yield return null;
                    }
                    Assert.IsTrue(slid);
                    Component o = beam.GetComponent(T("Obstacle"));
                    string flags = string.Join(" ", new[] { "wasHeadingInto", "wasNearWhilePassing", "physicallyTouched", "resolved" }
                        .Select(f => f + "=" + o.GetType().GetField(f, Hidden).GetValue(o)));
                    Assert.AreEqual(expected ? before + 1 : before, nearMisses,
                        (expected ? $"A slide {slideAt} m before the beam is a last-moment escape." : $"A slide {slideAt} m before the beam is not.") + " " + flags);
                    UnityEngine.Object.Destroy(beam);
                    // Back on its feet before the next one.
                    float until = Time.time + 1f;
                    while (Time.time < until)
                    {
                        ClearSouls();
                        yield return null;
                    }
                }
                run.GetType().GetEvent("NearMissRecorded").RemoveEventHandler(run, counted);
            }
            finally
            {
                Time.captureDeltaTime = oldCapture;
                if (tutorial < 0)
                    PlayerPrefs.DeleteKey("Deadline4Sec.TutorialCompleted");
                else
                    PlayerPrefs.SetInt("Deadline4Sec.TutorialCompleted", tutorial);
            }
            yield return new ExitPlayMode();
        }

        private static void ClearSouls()
        {
            foreach (Component enemy in UnityEngine.Object.FindObjectsByType(T("Enemy"), FindObjectsSortMode.None))
                if (!(bool)Read(enemy, "IsKilled"))
                    enemy.GetType().GetMethod("TryKill").Invoke(enemy, new object[] { "Test" });
        }
    }
}
