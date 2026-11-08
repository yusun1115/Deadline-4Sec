using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    // Areas (AreaBiome,Pacing.md, 2026-11-06): one scene, a run through regions.
    public sealed class AreaAcceptanceTests
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private const string ScenePath = "Assets/Scenes/EndlessRun.unity";

        private static Type T(string name) => Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
        private static Component Get(string name) => (Component)UnityEngine.Object.FindFirstObjectByType(T(name));
        private static object Read(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static object Field(object target, string name) => target.GetType().GetField(name).GetValue(target);
        private static IEnumerable<Component> Pool(object area, string field) =>
            ((Array)Field(area, field)).Cast<Component>();

        [Test]
        public void CatalogDescribesTheAreasInOrder()
        {
            ScriptableObject catalog = Resources.Load(T("AreaCatalog").GetField("ResourcePath").GetRawConstantValue().ToString()) as ScriptableObject;
            Assert.IsNotNull(catalog, "Run Deadline 4 Sec/Course/Build Areas.");
            Array areas = (Array)Field(catalog, "areas");
            Assert.AreEqual("SoulRailway", Field(areas.GetValue(0), "areaId").ToString());
            Assert.AreEqual("BrokenCathedral", Field(areas.GetValue(1), "areaId").ToString());
            Assert.AreEqual("ClockworkAbyss", Field(areas.GetValue(2), "areaId").ToString());

            // The cathedral has no trains and owns the wall run.
            object cathedral = areas.GetValue(1);
            Assert.IsFalse(Pool(cathedral, "patternPool").Any(p => p.name.Contains("Train") || p.name.Contains("RampRun")), "No trains in a cathedral.");
            Assert.IsTrue(Pool(cathedral, "specialPatternPool").All(p => (bool)Read(p, "IsWallRun")));
            Assert.IsFalse(Pool(areas.GetValue(0), "patternPool").Any(p => (bool)Read(p, "IsWallRun") || (bool)Read(p, "IsAirRouteEntry")));

            // The abyss owns the air route: entries, upper runs and the way down.
            object abyss = areas.GetValue(2);
            Assert.IsFalse(Pool(abyss, "patternPool").Any(p => p.name.Contains("Train") || p.name.Contains("RampRun")));
            var air = Pool(abyss, "specialPatternPool").ToList();
            Assert.IsTrue(air.Any(p => (bool)Read(p, "IsAirRouteEntry")));
            Assert.IsTrue(air.Any(p => (bool)Read(p, "IsUpperRun")));
            Assert.IsTrue(air.Any(p => (bool)Read(p, "IsUpperReturn")));
            Assert.IsFalse(air.Any(p => (bool)Read(p, "IsWallRun")));

            // The tunnel: a section with a portal, and Timer resets inside it.
            Component transition = (Component)Field(catalog, "transitionPattern");
            Assert.IsTrue((bool)Read(transition, "IsAreaTransition"));
            Assert.IsNotNull(transition.GetComponentInChildren(T("AreaTransitionSection"), true));
            Assert.AreEqual("Any", Read(transition, "EntryState").ToString());
            Assert.AreEqual("Ground", Read(transition, "ExitState").ToString());
            Assert.GreaterOrEqual(transition.GetComponentsInChildren(T("Enemy"), true).Length, 2);
            Assert.AreEqual(0, transition.GetComponentsInChildren(T("Obstacle"), true).Length);
            Renderer portalPrefab = transition.GetComponentInChildren(T("AreaTransitionSection"), true).transform.Find("Portal").GetComponent<Renderer>();
            Assert.AreEqual("Deadline4Sec/AreaPortal", portalPrefab.sharedMaterial.shader.name, "A portal that can open.");
        }

        [UnityTest]
        public IEnumerator CourseMovesThroughAreasWithTransitionsBetween()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return null;
            Component spawner = Get("PatternSpawner");
            Component director = Get("AreaDirector");
            Assert.IsNotNull(director);
            Assert.AreEqual("SoulRailway", Read(director, "CurrentAreaType").ToString());
            Get("RunManager").GetType().GetProperty("CurrentRunTime").SetValue(Get("RunManager"), 30f);
            MethodInfo choose = spawner.GetType().GetMethod("ChooseNextPattern", Hidden);
            MethodInfo spawn = spawner.GetType().GetMethod("SpawnPattern", Hidden);

            var trace = new List<string>();
            int transitions = 0, inArea = 0, wallRunsInCathedral = 0, routesInAbyss = 0, abyssVisits = 0;
            bool onDeck = false;
            var order = new List<string> { "SoulRailway" };
            string area = "SoulRailway";
            HashSet<string> pool = new HashSet<string>(Pool(Read(director, "SpawnArea"), "patternPool").Select(p => p.name));
            for (int i = 0; i < 180; i++)
            {
                var next = (Component)choose.Invoke(spawner, null);
                Assert.IsNotNull(next, "A compatible pattern exists at step " + i + "\n" + string.Join(", ", trace));
                spawn.Invoke(spawner, new object[] { next });
                trace.Add(next.name);
                if ((bool)Read(next, "IsAreaTransition"))
                {
                    transitions++;
                    Assert.IsFalse(onDeck, "A transition never starts up on the deck: " + string.Join(", ", trace));
                    Assert.GreaterOrEqual(inArea, 8, "Area length: " + string.Join(", ", trace));
                    Assert.LessOrEqual(inArea, 16, "Area length: " + string.Join(", ", trace));
                    if (area == "BrokenCathedral")
                    {
                        Assert.GreaterOrEqual(wallRunsInCathedral, 1, "The cathedral's wall run came up: " + string.Join(", ", trace));
                        wallRunsInCathedral = 0;
                    }
                    if (area == "ClockworkAbyss")
                    {
                        abyssVisits++;
                        Assert.GreaterOrEqual(routesInAbyss, 1, "The abyss's air route came up: " + string.Join(", ", trace));
                        routesInAbyss = 0;
                    }
                    inArea = 0;
                    area = Read(director, "SpawnArea").GetType().GetField("areaId").GetValue(Read(director, "SpawnArea")).ToString();
                    pool = new HashSet<string>(Pool(Read(director, "SpawnArea"), "patternPool")
                        .Concat(Pool(Read(director, "SpawnArea"), "specialPatternPool")).Select(p => p.name));
                    order.Add(area);
                    continue;
                }
                Assert.IsTrue(pool.Contains(next.name), next.name + " belongs to " + area);
                string entryState = Read(next, "EntryState").ToString();
                onDeck = Read(next, "ExitState").ToString() == "Upper";
                if (entryState != "Upper")
                    inArea++;
                if (area == "ClockworkAbyss" && (bool)Read(next, "IsAirRouteEntry"))
                    routesInAbyss++;
                if (area == "BrokenCathedral" && (bool)Read(next, "IsWallRun"))
                    wallRunsInCathedral++;
            }
            Assert.GreaterOrEqual(transitions, 5, "Areas keep changing and loop: " + string.Join(", ", trace));
            Assert.GreaterOrEqual(abyssVisits, 1);
            for (int i = 0; i < order.Count; i++)
                Assert.AreEqual(new[] { "SoulRailway", "BrokenCathedral", "ClockworkAbyss" }[i % 3], order[i], "Area order loops.");
            yield return new ExitPlayMode();
        }

        // Through a real tunnel: the world changes while the camera is inside,
        // new obstacles wear the cathedral's models, only two backdrops ever live.
        [UnityTest]
        public IEnumerator WorldChangesInsideTheTunnelAndBackdropsDoNotPileUp()
        {
            PlayerPrefs.SetInt("Deadline4Sec.TutorialCompleted", 1);
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return null;
            Component spawner = Get("PatternSpawner");
            Component director = Get("AreaDirector");
            Component scroller = Get("EnvironmentScroller");
            Color railwayFog = RenderSettings.fogColor;
            MethodInfo spawn = spawner.GetType().GetMethod("SpawnPattern", Hidden);
            object catalog = spawner.GetType().GetField("areaCatalog", Hidden).GetValue(spawner);
            Component transition = (Component)Field(catalog, "transitionPattern");

            for (int cycle = 0; cycle < 7; cycle++)
            {
                string before = Read(director, "CurrentAreaType").ToString();
                spawn.Invoke(spawner, new object[] { transition });
                Component section = spawner.GetComponentsInChildren(T("AreaTransitionSection"), true).Last();
                yield return null;
                Assert.AreEqual(before, Read(director, "CurrentAreaType").ToString(), "The spawn alone changes nothing around the runner.");
                Assert.LessOrEqual((int)Read(scroller, "BackdropCount"), 2);

                // Enter the tunnel: the camera is what decides.
                Transform view = Camera.main.transform;
                view.position = new Vector3(view.position.x, view.position.y, ((float)Read(section, "SwitchZ")) + 0.5f);
                yield return null;
                Assert.AreNotEqual(before, Read(director, "CurrentAreaType").ToString(), "Inside the tunnel the area changes.");
                yield return null;
                yield return null;
                Assert.AreEqual(1, (int)Read(scroller, "BackdropCount"), "The old backdrop is gone.");
                Assert.AreEqual(Read(director, "CurrentAreaType").ToString(), Read(scroller, "ShownArea").ToString());

                // The portal only hides the swap: it is gone long before the exit,
                // so the runner can see the next area's hazards in time.
                Renderer portal = section.transform.Find("Portal").GetComponent<Renderer>();
                Assert.IsTrue(portal.enabled, "Still covering right after the swap.");
                float revealEnd = (float)Read(section, "RevealEndZ");
                view.position = new Vector3(view.position.x, view.position.y, revealEnd + 0.5f);
                yield return null;
                Assert.AreEqual(1f, (float)Read(section, "OpenAmount"), 0.001f);
                Assert.IsFalse(portal.enabled, "Fully open.");
                Assert.Less(revealEnd + 6.4f + 15f, (float)Read(section, "ExitZ"), "Open at least 15 m (1 s) before the runner reaches the exit.");

                if (cycle == 1)
                {
                    object area = Read(director, "SpawnArea");
                    Assert.AreEqual("ClockworkAbyss", Field(area, "areaId").ToString());
                    GameObject hurdles = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Pattern/Pattern_G_Hurdles.prefab");
                    spawn.Invoke(spawner, new object[] { hurdles.GetComponent(T("CoursePattern")) });
                    Component spawned = spawner.GetComponentsInChildren(T("CoursePattern"), true).Last();
                    var models = spawned.GetComponentsInChildren<MeshFilter>(true).Select(m => m.name).ToList();
                    Assert.IsTrue(models.Any(n => n.Contains("Obstacle_Clockwork")), "Clockwork skin on the obstacles: " + string.Join(", ", models));

                    // The upper deck's floor and lit edges follow the area too.
                    GameObject route = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Pattern/Pattern_U2_UpperRun_Combat.prefab");
                    spawn.Invoke(spawner, new object[] { route.GetComponent(T("CoursePattern")) });
                    var parts = spawner.GetComponentsInChildren(T("CoursePattern"), true).Last().GetComponentsInChildren<Renderer>(true);
                    Assert.AreSame(Field(area, "floorMaterial"), parts.First(r => r.name.Contains("Upper Deck")).sharedMaterial);
                    Assert.AreSame(Field(area, "deckEdgeMaterial"), parts.First(r => r.name.Contains("Deck Rail")).sharedMaterial);
                }
                if (cycle == 0)
                {
                    // New course: cathedral floor and obstacle models.
                    object area = Read(director, "SpawnArea");
                    Assert.AreEqual("BrokenCathedral", Field(area, "areaId").ToString());
                    GameObject jump = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Pattern/Pattern_G_Hurdles.prefab");
                    spawn.Invoke(spawner, new object[] { jump.GetComponent(T("CoursePattern")) });
                    Component spawned = spawner.GetComponentsInChildren(T("CoursePattern"), true).Last();
                    var models = spawned.GetComponentsInChildren<MeshFilter>(true).Select(m => m.name).ToList();
                    Assert.IsTrue(models.Any(n => n.Contains("Obstacle_Cathedral")), "Cathedral skin on the obstacles: " + string.Join(", ", models));
                    Renderer floor = spawned.GetComponentsInChildren<Renderer>(true).First(r => r.name == "Graybox Floor");
                    Assert.AreSame(Field(area, "floorMaterial"), floor.sharedMaterial);
                    yield return new WaitForSecondsRealtime(0.8f);
                    Assert.AreNotEqual(railwayFog, RenderSettings.fogColor, "Fog follows the area.");
                }
            }
            Assert.AreEqual(7, (int)Read(director, "AreaChanges"));
            yield return new ExitPlayMode();
        }
    }
}
