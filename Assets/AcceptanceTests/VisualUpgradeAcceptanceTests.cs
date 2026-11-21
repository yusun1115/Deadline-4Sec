using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    // Visual upgrade (VisualUpgrade.md, 2026-11-15): every area has its own low
    // fog, distance fog, background layers, particles and background light, and
    // they change with the area inside the tunnel. Visual only: no colliders.
    public sealed class VisualUpgradeAcceptanceTests
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private const string ScenePath = "Assets/Scenes/EndlessRun.unity";

        private static Type T(string name) => Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
        private static Component Get(string name) => (Component)UnityEngine.Object.FindFirstObjectByType(T(name));
        private static object Read(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static object Field(object target, string name) => target.GetType().GetField(name).GetValue(target);

        [Test]
        public void EachAreaHasItsOwnProfileAndFloorPattern()
        {
            ScriptableObject catalog = Resources.Load(T("AreaCatalog").GetField("ResourcePath").GetRawConstantValue().ToString()) as ScriptableObject;
            Array areas = (Array)Field(catalog, "areas");
            var styles = new System.Collections.Generic.HashSet<string>();
            var fogColors = new System.Collections.Generic.HashSet<Color>();
            foreach (object area in areas)
            {
                object v = Field(area, "visuals");
                Assert.IsNotNull(v);
                float alpha = (float)Field(v, "lowFogAlpha");
                Assert.That(alpha, Is.InRange(0.3f, 1f), "Low fog on: " + Field(area, "areaId"));
                Assert.Less((float)Field(v, "lowFogBottom"), (float)Field(v, "lowFogTop"));
                Assert.LessOrEqual((float)Field(v, "lowFogTop"), 1.5f, "The low fog stays under the runner's eye line.");
                Assert.Less((float)Field(v, "fogStart"), (float)Field(v, "fogEnd"));
                Assert.GreaterOrEqual((float)Field(v, "fogStart"), 20f, "The play area ahead stays clear of fog.");
                styles.Add(Field(v, "particleStyle").ToString());
                fogColors.Add((Color)Field(v, "lowFogColor"));
            }
            Assert.AreEqual(3, styles.Count, "Neon dust, ash, steam and sparks.");
            Assert.AreEqual(3, fogColors.Count);
            Material cathedral = (Material)Field(areas.GetValue(1), "floorMaterial");
            Material clockwork = (Material)Field(areas.GetValue(2), "floorMaterial");
            Assert.IsTrue(cathedral.IsKeywordEnabled("_PATTERN_CATHEDRAL"));
            Assert.IsTrue(clockwork.IsKeywordEnabled("_PATTERN_CLOCKWORK"));
        }

        [UnityTest]
        public IEnumerator AtmosphereAndLayersFollowTheAreaThroughTheTunnel()
        {
            // Entering play mode reloads the domain and drops locals: the
            // original value waits in the editor session.
            UnityEditor.SessionState.SetInt("VisualUpgradeTest.Tutorial", PlayerPrefs.GetInt("Deadline4Sec.TutorialCompleted", -1));
            PlayerPrefs.SetInt("Deadline4Sec.TutorialCompleted", 1);
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            int tutorial = UnityEditor.SessionState.GetInt("VisualUpgradeTest.Tutorial", -1);
            yield return null;
            yield return null;
            Component spawner = Get("PatternSpawner");
            Component director = Get("AreaDirector");
            Component scroller = Get("EnvironmentScroller");
            Component atmosphere = (Component)Read(scroller, "Atmosphere");
            Assert.IsNotNull(atmosphere, "The atmosphere comes with the backdrop.");
            Assert.AreNotEqual(scroller.transform, atmosphere.transform.parent, "Not under the scroller (its children are the backdrops).");
            Assert.IsTrue((bool)Read(atmosphere, "LowFogVisible"));
            Assert.Greater((int)Read(scroller, "MidLayerCount"), 0);
            Assert.IsTrue((bool)Read(scroller, "HasFarLayer"));

            object railway = Read(director, "CurrentArea");
            object railwayVisuals = Field(railway, "visuals");
            Assert.AreEqual((float)Field(railwayVisuals, "fogEnd"), RenderSettings.fogEndDistance, 0.01f, "The first area's distance fog.");

            yield return new WaitForSeconds(1.5f);
            Assert.Greater((int)Read(atmosphere, "ParticleCount"), 0, "Light particles in the air.");

            MethodInfo spawn = spawner.GetType().GetMethod("SpawnPattern", Hidden);
            object catalog = spawner.GetType().GetField("areaCatalog", Hidden).GetValue(spawner);
            Component transition = (Component)Field(catalog, "transitionPattern");
            spawn.Invoke(spawner, new object[] { transition });
            Component section = spawner.GetComponentsInChildren(T("AreaTransitionSection"), true).Last();
            yield return null;
            Transform view = Camera.main.transform;
            view.position = new Vector3(view.position.x, view.position.y, ((float)Read(section, "SwitchZ")) + 0.5f);
            yield return null;
            Assert.AreEqual("BrokenCathedral", Read(director, "CurrentAreaType").ToString());
            yield return new WaitForSecondsRealtime(0.9f);

            object cathedral = Field(Read(director, "CurrentArea"), "visuals");
            Assert.AreEqual((float)Field(cathedral, "fogEnd"), RenderSettings.fogEndDistance, 0.01f, "Distance fog follows the area.");
            object shown = Read(atmosphere, "Shown");
            Assert.AreEqual("Ash", Field(shown, "particleStyle").ToString());
            Assert.AreEqual((Color)Field(cathedral, "lowFogColor"), (Color)Field(shown, "lowFogColor"));
            Color back = Shader.GetGlobalColor("_AreaBackLight");
            Color want = (Color)Field(cathedral, "backLight");
            Assert.AreEqual(want.r, back.r, 0.01f, "Background light follows the area.");
            Assert.Greater((int)Read(scroller, "MidLayerCount"), 0, "The cathedral has its mid layer too.");
            Assert.IsTrue((bool)Read(scroller, "HasFarLayer"));

            // Visual only: nothing to bump into.
            Assert.AreEqual(0, scroller.GetComponentsInChildren<Collider>(true).Length);
            Assert.AreEqual(0, atmosphere.GetComponentsInChildren<Collider>(true).Length);

            if (tutorial < 0)
                PlayerPrefs.DeleteKey("Deadline4Sec.TutorialCompleted");
            else
                PlayerPrefs.SetInt("Deadline4Sec.TutorialCompleted", tutorial);
            yield return new ExitPlayMode();
        }
    }
}
