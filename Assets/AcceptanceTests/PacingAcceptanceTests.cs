using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    // Pacing (AreaBiome,Pacing.md, steps 14-28 and 42-47): the course rises and
    // falls instead of climbing to Hard and staying there.
    public sealed class PacingAcceptanceTests
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private const string ScenePath = "Assets/Scenes/EndlessRun.unity";

        private static Type T(string name) => Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
        private static Component Get(string name) => (Component)UnityEngine.Object.FindFirstObjectByType(T(name));
        private static object Read(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static object Field(object target, string name) => target.GetType().GetField(name).GetValue(target);

        private sealed class Step
        {
            public string Name, Area, Block, Category;
            public int Target, Actual;
            public bool OnDeck, Transition, Special;
            public float First, Last, Length, LongestInside;
            public override string ToString() => $"{Area} {Block} target={Target} actual={Actual} {Name}";
        }

        private static Component CreateSeededSpawner(int seed)
        {
            GameObject host = new GameObject("Seeded Spawner");
            host.SetActive(false);
            Component spawner = host.AddComponent(T("PatternSpawner"));
            spawner.GetType().GetField("useFixedSeed", Hidden).SetValue(spawner, true);
            spawner.GetType().GetField("fixedSeed", Hidden).SetValue(spawner, seed);
            spawner.GetType().GetField("createFloorUnderPatterns", Hidden).SetValue(spawner, false);
            host.SetActive(true);
            return spawner;
        }

        private static List<Step> Build(Component spawner, int count)
        {
            MethodInfo choose = spawner.GetType().GetMethod("ChooseNextPattern", Hidden);
            MethodInfo spawn = spawner.GetType().GetMethod("SpawnPattern", Hidden);
            object pacing = spawner.GetType().GetField("pacing", Hidden).GetValue(spawner);
            Component director = spawner.GetComponent(T("AreaDirector"));
            var steps = new List<Step>();
            for (int i = 0; i < count; i++)
            {
                var next = (Component)choose.Invoke(spawner, null);
                Assert.IsNotNull(next, "A compatible pattern exists at step " + i + ":\n" + string.Join("\n", steps));
                object[] range = { 0f, 0f };
                next.GetType().GetMethod("TryGetOpportunityRange").Invoke(next, range);
                var positions = new List<float>();
                next.GetType().GetMethod("CollectOpportunityPositions").Invoke(next, new object[] { positions });
                float longestInside = 0f;
                for (int p = 1; p < positions.Count; p++)
                    longestInside = Mathf.Max(longestInside, positions[p] - positions[p - 1]);
                bool transition = (bool)Read(next, "IsAreaTransition");
                bool onDeck = Read(next, "EntryState").ToString() == "Upper";
                steps.Add(new Step
                {
                    Name = next.name,
                    Area = Field(Read(director, "SpawnArea"), "areaId").ToString(),
                    Block = (string)Read(pacing, "BlockName"),
                    Target = (int)Read(pacing, "TargetIntensity"),
                    Actual = (int)Read(next, "Intensity"),
                    Category = Read(next, "Category").ToString(),
                    OnDeck = onDeck,
                    Transition = transition,
                    Special = (bool)Read(next, "IsWallRun") || (bool)Read(next, "IsAirRouteEntry"),
                    First = (float)range[0],
                    Last = (float)range[1],
                    Length = (float)Read(next, "PatternLength"),
                    LongestInside = longestInside,
                });
                spawn.Invoke(spawner, new object[] { next });
            }
            return steps;
        }

        [Test]
        public void EveryPatternHasAnIntensityAndEveryAreaCanRest()
        {
            ScriptableObject catalog = Resources.Load(T("AreaCatalog").GetField("ResourcePath").GetRawConstantValue().ToString()) as ScriptableObject;
            foreach (object area in (Array)Field(catalog, "areas"))
            {
                List<Component> pool = ((Array)Field(area, "patternPool")).Cast<Component>()
                    .Concat(((Array)Field(area, "specialPatternPool")).Cast<Component>()).ToList();
                foreach (Component pattern in pool)
                {
                    int intensity = (int)Read(pattern, "Intensity");
                    Assert.That(intensity, Is.InRange(1, 5), pattern.name);
                }
                Assert.IsTrue(pool.Any(p => (int)Read(p, "Intensity") == 1), Field(area, "areaId") + " has a breather.");
                Assert.IsTrue(pool.Any(p => (int)Read(p, "Intensity") == 5), Field(area, "areaId") + " has a peak.");
                // Step 28: a breather still offers Timer resets.
                foreach (Component breather in pool.Where(p => (int)Read(p, "Intensity") == 1))
                    Assert.GreaterOrEqual(breather.GetComponentsInChildren(T("Enemy"), true).Length, 1, breather.name);
                Array blocks = (Array)Field(area, "pacingBlocks");
                Assert.GreaterOrEqual(blocks.Length, 3, Field(area, "areaId") + " has its own blocks.");
            }
        }

        // Pacing brings easy patterns back at full speed. A jump stays in the air
        // ~0.72 s, 11.5 m at 16 m/s: a pattern ending on a jump obstacle must leave
        // that much before the next pattern's first threat (3 m in at the soonest),
        // or the runner lands too late to jump again (2026-11-08, B -> Q).
        [Test]
        public void APatternEndingOnAJumpLeavesRoomToLandAtFullSpeed()
        {
            ScriptableObject catalog = Resources.Load(T("AreaCatalog").GetField("ResourcePath").GetRawConstantValue().ToString()) as ScriptableObject;
            int checkedPatterns = 0;
            foreach (object area in (Array)Field(catalog, "areas"))
            foreach (Component pattern in ((Array)Field(area, "patternPool")).Cast<Component>())
            {
                if ((string)Read(pattern, "ExitState").ToString() != "Ground")
                    continue;
                float lastZ = float.NegativeInfinity;
                bool endsOnJump = false;
                foreach (Component obstacle in pattern.GetComponentsInChildren(T("Obstacle"), true))
                {
                    float z = pattern.transform.InverseTransformPoint(obstacle.transform.position).z;
                    if (z <= lastZ)
                        continue;
                    lastZ = z;
                    endsOnJump = Read(obstacle, "Type").ToString() == "JumpObstacle";
                }
                foreach (Component enemy in pattern.GetComponentsInChildren(T("Enemy"), true))
                    if (pattern.transform.InverseTransformPoint(enemy.transform.position).z > lastZ)
                        endsOnJump = false;
                if (!endsOnJump)
                    continue;
                checkedPatterns++;
                float tail = (float)Read(pattern, "PatternLength") - lastZ;
                Assert.GreaterOrEqual(tail + 3f, 14.5f, pattern.name + " leaves no room to land and jump again.");
            }
            Assert.GreaterOrEqual(checkedPatterns, 1);
        }

        // Step 19: a block is not followed by itself, by weight or by chance.
        [Test]
        public void ABlockIsNeverFollowedByItself()
        {
            Type blockType = T("PacingBlock");
            Func<string, int, int[], object> block = (name, weight, targets) =>
                Activator.CreateInstance(blockType, name, weight, targets);
            Array blocks = Array.CreateInstance(blockType, 3);
            blocks.SetValue(block("A", 50, new[] { 2, 3 }), 0);
            blocks.SetValue(block("B", 1, new[] { 3, 2, 1 }), 1);
            blocks.SetValue(block("C", 1, new[] { 2, 4 }), 2);
            object director = Activator.CreateInstance(T("PacingDirector"), new System.Random(3), 4, 2);
            director.GetType().GetMethod("UseBlocks").Invoke(director, new object[] { blocks });
            Assert.AreEqual("A", Read(director, "BlockName"), "The first block opens the area.");
            var lengths = new Dictionary<string, int> { { "A", 2 }, { "B", 3 }, { "C", 2 } };
            var seen = new HashSet<string>();
            string previous = null;
            for (int i = 0; i < 300; i++)
            {
                string name = (string)Read(director, "BlockName");
                Assert.AreNotEqual(previous, name, "Block " + i);
                seen.Add(name);
                for (int step = 0; step < lengths[name]; step++)
                    director.GetType().GetMethod("Record").Invoke(director, new object[] { 2, true });
                previous = name;
            }
            Assert.AreEqual(3, seen.Count);
        }

        [UnityTest]
        public IEnumerator IntensityRisesAndFallsAndNeverStaysHigh()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return null;
            Component run = Get("RunManager");
            run.GetType().GetProperty("CurrentRunTime").SetValue(run, 120f);
            foreach (int seed in new[] { 12345, 1, 98765, 2024 })
            {
                Component spawner = CreateSeededSpawner(seed);
                List<Step> steps = Build(spawner, 150);
                Debug.Log($"PacingTrace seed={seed}\n" + string.Join("\n", steps.Take(40)));
                string all = string.Join("\n", steps);

                int changes = 0, highRun = 0, repeats = 0, recent = 0;
                for (int i = 0; i < steps.Count; i++)
                {
                    int actual = steps[i].Actual;
                    highRun = actual >= 4 ? highRun + 1 : 0;
                    Assert.LessOrEqual(highRun, 2, $"seed {seed}, step {i}: too many hard patterns in a row.\n{all}");
                    if (i > 0 && actual == 5)
                        Assert.Less(steps[i - 1].Actual, 5, $"seed {seed}, step {i}: 5 right after 5.\n{all}");
                    if (i > 0 && actual != steps[i - 1].Actual)
                        changes++;
                    if (steps[i].OnDeck || steps[i].Transition)
                        continue;
                    if (i > 0 && steps[i - 1].Name == steps[i].Name)
                        repeats++;
                    if (steps.Skip(Math.Max(0, i - 3)).Take(i - Math.Max(0, i - 3)).Any(s => s.Name == steps[i].Name))
                        recent++;
                }
                Assert.AreEqual(0, repeats, "The same prefab never comes twice in a row.\n" + all);
                Assert.LessOrEqual(recent, steps.Count / 25, "Recent prefabs are left out.\n" + all);
                Assert.GreaterOrEqual(changes, steps.Count / 2, "Intensity moves up and down.\n" + all);

                // Step 44: every 5 comes back down soon.
                for (int i = 0; i + 2 < steps.Count; i++)
                    if (steps[i].Actual == 5)
                        Assert.LessOrEqual(Mathf.Min(steps[i + 1].Actual, steps[i + 2].Actual), 3, $"Recovery after the peak at {i}.\n{all}");

                // Step 21: the target is honoured, closest match first.
                List<Step> ordinary = steps.Where(s => !s.OnDeck && !s.Transition && !s.Special).ToList();
                int near = ordinary.Count(s => Mathf.Abs(s.Actual - s.Target) <= 1);
                int exact = ordinary.Count(s => s.Actual == s.Target);
                Debug.Log($"PacingSummary seed={seed} ordinary={ordinary.Count} exact={exact} within1={near} counts=" +
                    string.Join(",", Enumerable.Range(1, 5).Select(v => v + ":" + steps.Count(s => s.Actual == v))) +
                    $" air={steps.Count(s => s.Category == "AirCombat" || s.Category == "Stomp")} ground={steps.Count(s => s.Category == "GroundCombat" || s.Category == "Obstacle" || s.Category == "Mixed")}");
                Assert.GreaterOrEqual(near * 100, ordinary.Count * 85, "Patterns follow the target.\n" + all);

                // Every level of the curve is really reached.
                foreach (int level in new[] { 1, 2, 3, 4, 5 })
                    Assert.IsTrue(steps.Any(s => s.Actual == level), $"Intensity {level} appears.\n{all}");

                Assert.GreaterOrEqual(steps.Select(s => s.Block).Distinct().Count(), 4, "Several blocks are used.\n" + all);

                // Step 24: at least three ordinary patterns between special sections.
                int sinceSpecial = int.MaxValue / 2;
                foreach (Step step in steps)
                {
                    if (step.Special)
                    {
                        Assert.GreaterOrEqual(sinceSpecial, 3, $"Special cooldown before {step}.\n{all}");
                        sinceSpecial = 0;
                    }
                    else if (step.OnDeck)
                        sinceSpecial = 0;
                    else
                        sinceSpecial++;
                }

                // Step 27: no pair of patterns leaves a long wait for a reset.
                float speed = Mathf.Max((float)Read(run, "BaseForwardSpeed"), (float)Read(run, "CurrentForwardSpeed"));
                float worst = 0f;
                string worstPair = "";
                for (int i = 0; i < steps.Count; i++)
                {
                    float wait = i > 0 ? (steps[i - 1].Length - steps[i - 1].Last + steps[i].First) / speed : 0f;
                    if (wait > worst)
                    {
                        worst = wait;
                        worstPair = steps[i - 1].Name + " -> " + steps[i].Name;
                    }
                    if (steps[i].LongestInside / speed > worst)
                    {
                        worst = steps[i].LongestInside / speed;
                        worstPair = "inside " + steps[i].Name;
                    }
                }
                Debug.Log($"PacingGap seed={seed} worst={worst:F2}s at {speed} m/s: {worstPair}");
                Assert.LessOrEqual(worst, 3.5f + 0.01f, worstPair);
                UnityEngine.Object.Destroy(spawner.gameObject);
                yield return null;
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SameSeedRepeatsTheSameAreasAndPatterns()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return null;
            Component run = Get("RunManager");
            run.GetType().GetProperty("CurrentRunTime").SetValue(run, 120f);

            Func<int, List<string>> course = seed =>
            {
                Component spawner = CreateSeededSpawner(seed);
                List<string> steps = Build(spawner, 80).Select(s => s.ToString()).ToList();
                UnityEngine.Object.DestroyImmediate(spawner.gameObject);
                return steps;
            };
            List<string> first = course(777);
            CollectionAssert.AreEqual(first, course(777), "The same seed builds the same course.");
            CollectionAssert.AreNotEqual(first, course(778), "Another seed builds another one.");
            yield return new ExitPlayMode();
        }

        // Step 29: Classic builds the same course from the same pools.
        [UnityTest]
        public IEnumerator ClassicBuildsTheSameCourseAsDeadline()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return null;
            Component run = Get("RunManager");
            run.GetType().GetProperty("CurrentRunTime").SetValue(run, 120f);
            Type mode = T("GameModeState");
            MethodInfo lockRun = mode.GetMethod("LockRun");
            Type modeType = T("GameMode");
            object before = mode.GetProperty("CurrentRunMode").GetValue(null);
            try
            {
                lockRun.Invoke(null, new[] { Enum.Parse(modeType, "Deadline") });
                Component deadline = CreateSeededSpawner(99);
                List<string> deadlineCourse = Build(deadline, 60).Select(s => s.ToString()).ToList();
                UnityEngine.Object.DestroyImmediate(deadline.gameObject);
                lockRun.Invoke(null, new[] { Enum.Parse(modeType, "Classic") });
                Component classic = CreateSeededSpawner(99);
                CollectionAssert.AreEqual(deadlineCourse, Build(classic, 60).Select(s => s.ToString()).ToList());
                UnityEngine.Object.DestroyImmediate(classic.gameObject);
            }
            finally
            {
                lockRun.Invoke(null, new[] { before });
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator TheFirstSecondsStayGentle()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return null;
            Component run = Get("RunManager");
            run.GetType().GetProperty("CurrentRunTime").SetValue(run, 5f);
            Component spawner = CreateSeededSpawner(5);
            foreach (Step step in Build(spawner, 7))
                Assert.LessOrEqual(step.Actual, 3, "Nothing hard before the run has started: " + step);
            yield return new ExitPlayMode();
        }
    }
}
