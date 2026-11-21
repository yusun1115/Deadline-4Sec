using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    // Obstacle Pattern Rework (2026-11-21, first-time-player playtest): no
    // Train Wagon, nothing hidden behind a slide beam, lane blockers and trains
    // from the first seconds, ridden train sections that are fair at top speed.
    public sealed class ObstaclePatternReworkAcceptanceTests
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private const float SlideClearance = 14f;

        private static readonly string[] Reworked =
        {
            "Pattern_LaneBlocker_Single_Left", "Pattern_LaneBlocker_Single_Center", "Pattern_LaneBlocker_Double",
            "Pattern_LaneBlocker_Jump", "Pattern_LaneBlocker_Enemy",
            "Pattern_TrainRamp_Simple", "Pattern_TrainRamp_DoubleTrain", "Pattern_TrainRoof_Jump", "Pattern_TrainRamp_Triple",
            "Pattern_B_JumpSlide", "Pattern_G_Hurdles", "Pattern_Q_ObstacleRush", "Pattern_N_Gauntlet", "Pattern_R_TrainCorridor",
        };

        private static Type T(string name) => Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
        private static Component Get(string name) => (Component)UnityEngine.Object.FindFirstObjectByType(T(name));
        private static object Read(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static object Field(object target, string name) => target.GetType().GetField(name).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Hidden).SetValue(target, value);

        private static IEnumerable<object> Areas()
        {
            ScriptableObject catalog = Resources.Load(T("AreaCatalog").GetField("ResourcePath").GetRawConstantValue().ToString()) as ScriptableObject;
            return ((Array)Field(catalog, "areas")).Cast<object>();
        }

        private static List<Component> Pool(object area, bool withSpecials)
        {
            var list = ((Array)Field(area, "patternPool")).Cast<Component>().ToList();
            if (withSpecials && Field(area, "specialPatternPool") is Array specials)
                list.AddRange(specials.Cast<Component>());
            return list;
        }

        private static bool HasWagon(Component pattern) =>
            pattern.GetComponentsInChildren(T("Obstacle"), true).Any(o =>
                o.name.Contains("Train Wagon") || ((BoxCollider)o.GetComponent<BoxCollider>()).size.x * o.transform.lossyScale.x > 6f);

        [Test]
        public void NoTrainWagonIsSpawnedAnywhere()
        {
            foreach (object area in Areas())
                foreach (Component pattern in Pool(area, true))
                    Assert.IsFalse(HasWagon(pattern), pattern.name + " in " + Field(area, "areaId") + " uses the Train Wagon.");
            // The scene's fallback library too.
            string scene = File.ReadAllText("Assets/Scenes/EndlessRun.unity");
            foreach (string name in new[] { "Pattern_H_TrainHop", "Pattern_K_TrainRun", "Pattern_O_TrainCombo" })
            {
                string guid = AssetDatabase.AssetPathToGUID("Assets/Prefab/Pattern/" + name + ".prefab");
                Assert.IsFalse(!string.IsNullOrEmpty(guid) && scene.Contains(guid), name + " is still in the scene's pattern list.");
            }
        }

        // A slide beam (1.3–3.8 m) hides anything low behind it in its lane.
        [Test]
        public void NothingStandsHiddenBehindASlideBeam()
        {
            foreach (object area in Areas())
            {
                List<Component> pool = Pool(area, true);
                foreach (Component pattern in pool)
                {
                    var threats = Threats(pattern);
                    foreach (var beam in threats.Where(t => t.Slide))
                        foreach (var t in threats)
                            if (t.Z > beam.Z && t.Z - beam.Z < SlideClearance && Mathf.Abs(t.X - beam.X) < 1.2f && Mathf.Abs(t.Y - beam.Y) < 4f)
                                Assert.Fail($"{pattern.name}: {t.Name} {t.Z - beam.Z:F1} m behind the slide beam {beam.Name}.");
                }
                // Joins: the next pattern's first threat, after the last one of the previous.
                foreach (Component a in pool)
                foreach (Component b in pool)
                {
                    if (a == b || !Connects(Read(a, "ExitState").ToString(), Read(b, "EntryState").ToString()) || Read(a, "ExitState").ToString() == "Air")
                        continue;
                    object[] endA = { 0f, 0f, false }, endB = { 0f, 0f, false };
                    a.GetType().GetMethod("TryGetThreatEnds").Invoke(a, endA);
                    b.GetType().GetMethod("TryGetThreatEnds").Invoke(b, endB);
                    float gap = (float)Read(a, "PatternLength") - (float)endA[1] + (float)endB[0];
                    bool beam = (bool)endA[2];
                    Assert.GreaterOrEqual(gap, beam ? SlideClearance : 6f, $"{Field(area, "areaId")}: {a.name} -> {b.name}" + (beam ? " (after a slide beam)" : ""));
                }
            }
        }

        private static bool Connects(string exit, string entry)
        {
            if (exit == "Upper" || entry == "Upper")
                return exit == entry;
            return exit == "Any" || entry == "Any" || exit == entry;
        }

        private struct Threat
        {
            public float X, Y, Z;
            public string Name;
            public bool Slide;
        }

        private static List<Threat> Threats(Component pattern)
        {
            var list = new List<Threat>();
            foreach (Component o in pattern.GetComponentsInChildren(T("Obstacle"), true))
            {
                Vector3 p = pattern.transform.InverseTransformPoint(o.transform.position);
                list.Add(new Threat { X = p.x, Y = p.y, Z = p.z, Name = o.name, Slide = Read(o, "Type").ToString() == "SlideObstacle" });
            }
            foreach (Component e in pattern.GetComponentsInChildren(T("Enemy"), true))
            {
                Vector3 p = pattern.transform.InverseTransformPoint(e.transform.position);
                list.Add(new Threat { X = p.x, Y = p.y, Z = p.z, Name = e.name });
            }
            return list;
        }

        // Coming down off a ridden roof (often high, after an air kill) the
        // runner needs room: no ground Soul within 12 m of where a roof ends,
        // unless the next car's ramp carries on from there.
        [Test]
        public void NoGroundSoulWaitsWhereARoofEnds()
        {
            foreach (object area in Areas())
                foreach (Component pattern in Pool(area, false))
                {
                    var roofs = pattern.GetComponentsInChildren(T("Obstacle"), true)
                        .Where(o => Read(o, "Type").ToString() == "RampTrain")
                        .Select(o =>
                        {
                            BoxCollider box = o.GetComponent<BoxCollider>();
                            Vector3 back = box.center + Vector3.forward * box.size.z * 0.5f;
                            return pattern.transform.InverseTransformPoint(o.transform.TransformPoint(back)).z;
                        }).ToList();
                    foreach (float end in roofs)
                    {
                        if (roofs.Any(other => Mathf.Abs(other - 14f - 7.5f - end) < 0.5f))
                            continue; // the next car's ramp starts here
                        foreach (Component enemy in pattern.GetComponentsInChildren(T("Enemy"), true))
                        {
                            Vector3 p = pattern.transform.InverseTransformPoint(enemy.transform.position);
                            if (p.y < 1.5f && p.z > end && p.z - end < 12f)
                                Assert.Fail($"{pattern.name}: {enemy.name} {p.z - end:F1} m after a roof ends.");
                        }
                    }
                }
        }

        // A run's first seconds (Easy tier, low pacing targets) can bring a lane
        // blocker in every area, and a train in the railway.
        [Test]
        public void LaneBlockersAndTrainsAreCandidatesFromTheStart()
        {
            foreach (object area in Areas())
            {
                string id = Field(area, "areaId").ToString();
                List<Component> pool = Pool(area, false);
                bool Blocker(Component p) => p.GetComponentsInChildren(T("Obstacle"), true).Any(o => Read(o, "Type").ToString() == "LaneBlocker" && o.name.Contains("Lane Blocker / Near Miss"));
                bool Easy(Component p) => Read(p, "Difficulty").ToString() == "Easy";
                foreach (int intensity in new[] { 1, 2 })
                    Assert.IsTrue(pool.Any(p => Easy(p) && Blocker(p) && (int)Read(p, "Intensity") == intensity), $"{id}: an easy lane blocker pattern of intensity {intensity}.");
                Assert.IsTrue(pool.Any(p => !Easy(p) && Blocker(p) && p.GetComponentsInChildren(T("Obstacle"), true).Length > 1),
                    id + ": lane blocker combinations for later.");
                var trains = pool.Where(p => Read(p, "Category").ToString() == "Train").ToList();
                if (id == "SoulRailway")
                {
                    Assert.GreaterOrEqual(trains.Count, 6, "The railway has many train sections.");
                    Assert.GreaterOrEqual(trains.Count(p => Easy(p) && (int)Read(p, "Intensity") <= 2), 2, "Simple trains from the first seconds.");
                    Assert.IsTrue(trains.Any(p => p.name.Contains("TrainRamp_Simple")), "The ramp is introduced alone first.");
                }
                else
                    Assert.AreEqual(0, trains.Count, id + " has no trains.");
            }
        }

        // Every reworked pattern on its own, again and again, at top speed,
        // played by the course replay bot through the real Deadline timer: no
        // death, rewards keep coming (the Deadline gap holds on train roofs).
        // DEADLINE_PATTERN_SHOTS=<folder>: also saves what the camera sees.
        [UnityTest]
        public IEnumerator EachReworkedPatternIsPlayableAtTopSpeed()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            // Read in play mode: entering it reloads the domain and drops earlier locals.
            int tutorial = PlayerPrefs.GetInt("Deadline4Sec.TutorialCompleted", -1);
            string shots = Environment.GetEnvironmentVariable("DEADLINE_PATTERN_SHOTS");
            if (string.IsNullOrEmpty(shots))
                shots = null;
            float oldCapture = Time.captureDeltaTime;
            var trace = new StringBuilder();
            try
            {
                PlayerPrefs.SetInt("Deadline4Sec.TutorialCompleted", 1);
                Time.captureDeltaTime = 1f / 60f;
                Component player = Get("PlayerController");
                Component spawner = Get("PatternSpawner");
                Component run = Get("RunManager");
                Component timer = Get("GameTimer");
                Set(run, "baseForwardSpeed", 16f);
                Set(run, "maxForwardSpeed", 16f);
                Set(spawner, "patternsAhead", 2);
                Set(spawner, "debugRepeatPattern", Pattern(Reworked[0]));
                Component flow = Get("GameFlowManager");
                flow.GetType().GetMethod("StartGame").Invoke(flow, null);
                float guard = Time.realtimeSinceStartup + 20f;
                while (!(bool)Read(timer, "IsRunning") && Time.realtimeSinceStartup < guard)
                    yield return null;
                Assert.IsTrue((bool)Read(timer, "IsRunning"));
                float centerX = (float)player.GetType().GetField("centerX", Hidden).GetValue(player);
                var actions = new Dictionary<int, float>();
                var bypassed = new HashSet<int>();
                MethodInfo drive = typeof(CourseReplayAcceptanceTests).GetMethod("Drive", BindingFlags.NonPublic | BindingFlags.Static);
                var seen = new HashSet<int>();
                var passes = Reworked.ToDictionary(n => n, n => 0);
                var shotTaken = new HashSet<string>();
                int index = 0;
                int frames = 0;
                while (index < Reworked.Length && frames++ < 60 * 60 * 6)
                {
                    string current = Reworked[index];
                    float z = player.transform.position.z;
                    foreach (Component pattern in spawner.GetComponentsInChildren(T("CoursePattern")))
                    {
                        float local = z - pattern.transform.position.z;
                        if (local >= 0f && seen.Add(pattern.GetInstanceID()))
                        {
                            trace.AppendLine($"ENTER {pattern.name} t={Read(run, "CurrentRunTime"):F1}");
                            if (passes.ContainsKey(pattern.name))
                                passes[pattern.name]++;
                        }
                        if (shots != null && pattern.name == current)
                            foreach (float at in ShotPoints(current))
                                if (local >= at && shotTaken.Add(current + at))
                                    Shoot(Path.Combine(shots, $"{current.Replace("Pattern_", "")}_{at:+00;-00}.png"));
                    }
                    // Two full passes of this pattern, then the next one.
                    if (passes[current] >= 3)
                    {
                        index++;
                        if (index < Reworked.Length)
                            Set(spawner, "debugRepeatPattern", Pattern(Reworked[index]));
                    }
                    if ((bool)Read(timer, "IsGameOver"))
                        Assert.Fail($"Died in {current} ({Read(timer, "LastDeathReason")}).\n" + trace);
                    if (Time.timeScale > 0f)
                        drive.Invoke(null, new object[] { player, centerX, (float)Read(run, "CurrentRunTime"), actions, bypassed, trace });
                    yield return null;
                }
                Assert.AreEqual(Reworked.Length, index, "All patterns were played.\n" + trace);
                Debug.Log("PATTERNDRILL ok " + string.Join(", ", passes.Select(p => p.Key.Replace("Pattern_", "") + "=" + p.Value)));
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

        private static Component Pattern(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Pattern/" + name + ".prefab").GetComponent(T("CoursePattern"));

        // Runner positions (m into the pattern, negative: before it) for the pictures.
        private static float[] ShotPoints(string pattern)
        {
            if (pattern.Contains("TrainRamp_Simple")) return new[] { -8f, 9f, 20f };
            if (pattern.Contains("DoubleTrain")) return new[] { 4f, 22f, 30f };
            if (pattern.Contains("TrainRoof_Jump")) return new[] { 10f, 16f };
            if (pattern.Contains("Triple")) return new[] { 40f };
            if (pattern.Contains("LaneBlocker")) return new[] { -10f, -2f };
            if (pattern.Contains("B_JumpSlide")) return new[] { 0f, 8f };
            if (pattern.Contains("G_Hurdles")) return new[] { -6f, 9f };
            if (pattern.Contains("Q_Obstacle") || pattern.Contains("N_Gauntlet")) return new[] { 7f, 15f };
            return new float[0];
        }

        private static void Shoot(string file)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            Camera cam = Camera.main;
            var rt = new RenderTexture(360, 640, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(360, 640, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 360, 640), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(file, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            rt.Release();
        }
    }
}
