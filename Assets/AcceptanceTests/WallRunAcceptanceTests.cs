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
    // Wall Run.md §58–74: attach only on the matching left / right input inside
    // the entry window (never by itself, never together with a lane move), the
    // fall when the input is missing / wrong / too late, wall switches both ways
    // (repeats, same side, jump / slide ignored), wall obstacles (fatal, Shield
    // once), Souls on the wall, the exit back to normal control, revive before
    // the entry, the Deadline timer (held on the wall, no reset from the wall
    // itself, running again after the exit), Classic, and the spawner's
    // special-section rules.
    // Records / Soul / stats / quests / consumables are restored.
    public sealed class WallRunAcceptanceTests
    {
        private const string ScenePath = "Assets/Scenes/EndlessRun.unity";
        private const string Folder = "Assets/Prefab/Pattern/";
        private const string Left = "Pattern_W1_WallRun_Left";
        private const string Right = "Pattern_W2_WallRun_Right";
        // Run speed when wall runs appear (45 s+: 12 + 0.06/s, capped at 16).
        private const float WallRunSpeed = 14.7f;
        private const float WallLine = 3.6f;
        private const float WallFeet = 0.8f;
        private const string BackupKey = "Deadline4Sec.WallRunTests.Backup";

        private static Type T(string name)
        {
            Type type = Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
            Assert.IsNotNull(type, name);
            return type;
        }

        private static Component Get(string name) => (Component)UnityEngine.Object.FindFirstObjectByType(T(name));
        private static object Read(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static void Set(Component target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static object Field(Component target, string field) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Call(Component target, string method) => target.GetType().GetMethod(method).Invoke(target, null);
        private static Component Pattern(string name) => (Component)AssetDatabase.LoadAssetAtPath(Folder + name + ".prefab", T("CoursePattern"));

        // ---- backup ---------------------------------------------------------------

        private static IEnumerable<string> Keys()
        {
            foreach (string key in new[] { "TotalCoins", "Deadline4Sec.TutorialCompleted", "Deadline4Sec.BestScore", "Deadline4Sec.BestDistance",
                         "Deadline4Sec.BestCombo", "Deadline4Sec.BestScore_Classic", "Deadline4Sec.BestDistance_Classic", "Deadline4Sec.BestCombo_Classic",
                         "ReviveCouponCount", "Consumable_Shield_Count", "EquippedConsumable", "DailyQuest_CurrentDate" })
                yield return key;
            for (int i = 1; i <= 3; i++)
                foreach (string part in new[] { "_ID", "_Progress", "_Completed", "_Claimed" })
                    yield return "DailyQuest" + i + part;
            foreach (string name in Enum.GetNames(T("PlayerStat")))
                yield return "Stats_" + name;
            foreach (object definition in (Array)T("AchievementCatalog").GetMethod("Defaults").Invoke(null, null))
                yield return "AchievementUnlocked_" + definition.GetType().GetField("id").GetValue(definition);
        }

        [SetUp]
        public void SetUp()
        {
            if (SessionState.GetString(BackupKey, "") == "")
            {
                var backup = new System.Text.StringBuilder();
                foreach (string key in Keys())
                {
                    if (!PlayerPrefs.HasKey(key))
                    {
                        backup.Append(key).Append("\t-\t\n");
                        continue;
                    }
                    // PlayerPrefs has no type query: a string reads back as its
                    // default from GetInt / GetFloat, so try string first.
                    string text = PlayerPrefs.GetString(key, "\u0001");
                    if (text != "\u0001")
                        backup.Append(key).Append("\ts\t").Append(text.Replace("\t", " ").Replace("\n", " ")).Append('\n');
                    else if (key.Contains("Distance") || key.StartsWith("Stats_Lowest"))
                        backup.Append(key).Append("\tf\t").Append(PlayerPrefs.GetFloat(key).ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
                    else
                        backup.Append(key).Append("\ti\t").Append(PlayerPrefs.GetInt(key)).Append('\n');
                }
                SessionState.SetString(BackupKey, backup.ToString());
            }
            PlayerPrefs.SetInt("Deadline4Sec.TutorialCompleted", 1);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string line in SessionState.GetString(BackupKey, "").Split('\n'))
            {
                string[] parts = line.Split('\t');
                if (parts.Length < 3)
                    continue;
                if (parts[1] == "-")
                    PlayerPrefs.DeleteKey(parts[0]);
                else if (parts[1] == "s")
                    PlayerPrefs.SetString(parts[0], parts[2]);
                else if (parts[1] == "f")
                    PlayerPrefs.SetFloat(parts[0], float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
                else
                    PlayerPrefs.SetInt(parts[0], int.Parse(parts[2]));
            }
            SessionState.EraseString(BackupKey);
            PlayerPrefs.Save();
            Time.captureDeltaTime = 0f;
            T("GameModeState").GetMethod("Select").Invoke(null, new[] { Enum.Parse(T("GameMode"), "Deadline") });
        }

        // ---- data -------------------------------------------------------------------

        [Test]
        public void WallRunPatternsAreBuiltFromAnchorsAndFitTheTimer()
        {
            foreach (string name in new[] { Left, Right })
            {
                Component pattern = Pattern(name);
                Assert.IsNotNull(pattern, name);
                Assert.AreEqual("WallRun", Read(pattern, "Category").ToString());
                Assert.AreEqual("Ground", Read(pattern, "EntryState").ToString());
                Assert.AreEqual("Ground", Read(pattern, "ExitState").ToString());
                Component section = pattern.GetComponentInChildren(T("WallRunSection"), true);
                Assert.IsNotNull(section, name + " has a WallRunSection");
                int side = name == Left ? -1 : 1;
                Assert.AreEqual(side < 0 ? "Left" : "Right", Read(section, "Available").ToString());

                Transform Child(string field) => (Transform)Field(section, field);
                BoxCollider entry = (BoxCollider)Field(section, "entry");
                float gapStart = (float)Read(section, "FloorGapStart"), gapEnd = (float)Read(section, "FloorGapEnd");
                foreach (string anchor in new[] { "leftWallAnchor", "rightWallAnchor", "groundExitAnchor", "reviveCheckpoint", "exit" })
                    Assert.IsNotNull(Child(anchor), name + " " + anchor);
                Assert.AreEqual(-WallLine, Child("leftWallAnchor").localPosition.x, 0.01f);
                Assert.AreEqual(WallLine, Child("rightWallAnchor").localPosition.x, 0.01f);
                Assert.IsTrue(entry.isTrigger);

                // §9: the attach window gives ~0.8–1.5 s at the wall-run speeds,
                // ends just past the floor edge, and the checkpoint is safe ground before it.
                float entryStart = entry.transform.localPosition.z - entry.size.z * 0.5f;
                float entryEnd = entry.transform.localPosition.z + entry.size.z * 0.5f;
                Assert.That((entryEnd - entryStart) / WallRunSpeed, Is.InRange(0.8f, 1.5f), name + " window at 14.7 m/s");
                Assert.GreaterOrEqual((entryEnd - entryStart) / 16f, 0.8f, name + " window at 16 m/s");
                Assert.That(entryEnd - gapStart, Is.InRange(1f, 4f), name + " window ends just past the edge");
                Assert.Less(Child("reviveCheckpoint").localPosition.z, entryStart);
                Assert.Less(Child("exit").localPosition.z, gapEnd);
                Assert.Greater(Child("groundExitAnchor").localPosition.z, gapEnd);

                // Kill-or-fall: a FallDeathTrigger fills the space under the gap.
                Component fall = section.GetComponentInChildren(T("FallDeathTrigger"), true);
                Assert.IsNotNull(fall, name + " fall trigger");
                Bounds fallBox = new Bounds(fall.transform.localPosition, ((BoxCollider)fall.GetComponent<Collider>()).size);
                Assert.Less(fallBox.min.z, gapStart + 1f);
                Assert.Greater(fallBox.max.z, gapEnd - 1f);
                Assert.Less(fallBox.max.y, -3f);

                // Wall obstacles alternate, starting on the entry wall, all inside the gap.
                List<Transform> obstacles = pattern.GetComponentsInChildren(T("Obstacle"), true)
                    .Select(c => c.transform).Where(t => Mathf.Abs(t.localPosition.x) > 2f).OrderBy(t => t.localPosition.z).ToList();
                Assert.GreaterOrEqual(obstacles.Count, 2);
                for (int i = 0; i < obstacles.Count; i++)
                {
                    Assert.AreEqual(i % 2 == 0 ? side : -side, Math.Sign(obstacles[i].localPosition.x), name + " obstacle " + i);
                    Assert.That(obstacles[i].localPosition.z, Is.InRange(gapStart, gapEnd));
                    // The hazard covers the runner on its wall but not the other wall.
                    Bounds b = new Bounds(obstacles[i].localPosition, obstacles[i].localScale);
                    Assert.Less(b.min.y, WallFeet + 0.1f);
                    Assert.Greater(b.max.y, WallFeet + 2.1f);
                    Assert.Greater(Mathf.Abs(b.center.x) - b.extents.x, 2.5f);
                }
                // Even a last-moment attach leaves time before the first obstacle.
                Assert.Greater(obstacles[0].localPosition.z - 0.6f - (entryEnd + 0.25f * WallRunSpeed), 0.3f * WallRunSpeed);
                for (int i = 1; i < obstacles.Count; i++)
                    Assert.GreaterOrEqual(obstacles[i].localPosition.z - obstacles[i - 1].localPosition.z, 0.55f * WallRunSpeed);

                // §42: kill chances before the window and after the landing keep
                // the stretch inside the 4-second rule at the speed wall runs appear.
                List<float> enemies = pattern.GetComponentsInChildren(T("Enemy"), true).Select(c => c.transform.localPosition.z).OrderBy(z => z).ToList();
                float before = enemies.Where(z => z < entryStart).DefaultIfEmpty(float.NaN).Max();
                float after = enemies.Where(z => z > gapEnd).DefaultIfEmpty(float.NaN).Min();
                Assert.IsFalse(float.IsNaN(before) || float.IsNaN(after), name + " has enemies around the section");
                Assert.Less((after - before) / WallRunSpeed, 3.5f, name + " kill-to-kill time");

                // Souls on the wall lines.
                Assert.GreaterOrEqual(pattern.GetComponentsInChildren(T("CoinPickup"), true)
                    .Count(c => Mathf.Abs(Mathf.Abs(c.transform.localPosition.x) - WallLine) < 0.1f), 8, name + " wall Souls");
            }
        }

        // ---- spawner -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator SpawnerPlacesWallRunsAsSpecialSections()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return null;
            Component spawner = Get("PatternSpawner");
            Component run = Get("RunManager");
            MethodInfo choose = spawner.GetType().GetMethod("ChooseNextPattern", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo spawn = spawner.GetType().GetMethod("SpawnPattern", BindingFlags.Instance | BindingFlags.NonPublic);
            PropertyInfo runTime = run.GetType().GetProperty("CurrentRunTime");
            Set(spawner, "mediumStartsAtSeconds", 0f);

            // Before wallRunStartsAtSeconds (45, judged by the estimated arrival
            // time like the air route): no wall run in the first ~250 m.
            var early = new List<Component>();
            for (int i = 0; i < 9; i++)
            {
                var next = (Component)choose.Invoke(spawner, null);
                spawn.Invoke(spawner, new object[] { next });
                early.Add(next);
            }
            Assert.IsFalse(early.Any(p => Read(p, "Category").ToString() == "WallRun"), "No wall run before 45 s.");

            // Later in the run, with air routes due too.
            runTime.SetValue(run, 70f);
            Set(spawner, "airRouteStartsAtSeconds", 0f);
            var sequence = new List<Component>();
            for (int i = 0; i < 90; i++)
            {
                var next = (Component)choose.Invoke(spawner, null);
                Assert.IsNotNull(next, "A compatible pattern exists at step " + i);
                spawn.Invoke(spawner, new object[] { next });
                sequence.Add(next);
            }
            int wallRuns = 0, normalSinceSpecial = 99, sinceWallRun = 99;
            for (int i = 0; i < sequence.Count; i++)
            {
                Component p = sequence[i];
                bool wallRun = Read(p, "Category").ToString() == "WallRun";
                bool special = wallRun || Read(p, "ExitState").ToString() == "Upper" || Read(p, "EntryState").ToString() == "Upper";
                bool startsSpecial = wallRun || (bool)Read(p, "IsAirRouteEntry");
                if (startsSpecial)
                    Assert.GreaterOrEqual(normalSinceSpecial, 3, $"Step {i} {p.name}: ordinary patterns after the last special section.");
                if (wallRun)
                {
                    wallRuns++;
                    if (wallRuns > 1)
                        Assert.GreaterOrEqual(sinceWallRun, 6, "Wall runs are spaced.");
                    sinceWallRun = 0;
                }
                else if (Read(p, "EntryState").ToString() != "Upper")
                    sinceWallRun++;
                normalSinceSpecial = special ? 0 : normalSinceSpecial + 1;
            }
            Assert.GreaterOrEqual(wallRuns, 2, "Wall runs keep appearing.");

            // The spawned floor leaves the gap open and joins on both sides.
            foreach (Component pattern in spawner.GetComponentsInChildren(T("CoursePattern")))
            {
                Component section = pattern.GetComponentInChildren(T("WallRunSection"));
                if (section == null)
                    continue;
                float gapStart = (float)Read(section, "GapStartZ"), gapEnd = (float)Read(section, "GapEndZ");
                List<Transform> floors = pattern.GetComponentsInChildren<Transform>().Where(t => t.name == "Graybox Floor").ToList();
                Assert.AreEqual(2, floors.Count, "Floor before and after the gap.");
                // (Transforms, not collider bounds: these were spawned this frame.)
                float FloorStart(Transform f) => f.position.z - f.lossyScale.z * 0.5f;
                float FloorEnd(Transform f) => f.position.z + f.lossyScale.z * 0.5f;
                foreach (Transform floor in floors)
                    Assert.IsTrue(FloorEnd(floor) <= gapStart + 0.01f || FloorStart(floor) >= gapEnd - 0.01f, "No floor inside the gap.");
                float start = pattern.transform.position.z, end = start + (float)Read(pattern, "PatternLength");
                Assert.AreEqual(start, floors.Min(FloorStart), 0.01f);
                Assert.AreEqual(end, floors.Max(FloorEnd), 0.01f);
            }
            yield return new ExitPlayMode();
        }

        // ---- play helpers --------------------------------------------------------------

        // Lambda state lives in statics (locals captured before EnterPlayMode
        // would be lost in the domain reload).
        private static float sectionZ, waitUntil, minTimer;
        private static Component section;

        private static Component PlayerC => Get("PlayerController");
        private static Vector3 PlayerPos => PlayerC.transform.position;
        private static float Local => PlayerPos.z - sectionZ;
        private static float Feet => PlayerC.GetComponent<CharacterController>().bounds.min.y;
        private static float CenterX => (float)Field(PlayerC, "centerX");
        private static bool Grounded => (bool)Read(PlayerC, "IsGrounded");
        private static bool OnWall => (bool)Read(PlayerC, "IsWallRunning");
        private static string Wall => Read(PlayerC, "CurrentWall").ToString();
        private static int Lane => (int)Read(PlayerC, "LaneIndex");
        private static bool Dead => (bool)Read(Get("GameTimer"), "IsGameOver");
        private static string DeathReason => Read(Get("GameTimer"), "LastDeathReason").ToString();
        private static float Timer => (float)Read(Get("GameTimer"), "RemainingTime");
        private static void Input(string command) => PlayerC.GetType().GetMethod(command).Invoke(PlayerC, null);

        // Starts a run whose first pattern is the wall run, at the speed wall
        // runs appear. `clearEnemies`: the approach / landing enemies are removed
        // (tests that are not about them).
        private static IEnumerator StartWallRun(string mode, string pattern, bool clearEnemies = true)
        {
            Time.captureDeltaTime = 1f / 60f;
            yield return null;
            T("GameModeState").GetMethod("Select").Invoke(null, new[] { Enum.Parse(T("GameMode"), mode) });
            Component spawner = Get("PatternSpawner");
            string[] course = { pattern, "Pattern_A_LaneAttack", "Pattern_B_JumpSlide", "Pattern_F_Crossfire", "Pattern_G_Hurdles", "Pattern_I_ArchWalk" };
            Array prefabs = Array.CreateInstance(T("CoursePattern"), course.Length);
            for (int i = 0; i < course.Length; i++)
                prefabs.SetValue(Pattern(course[i]), i);
            Set(spawner, "patternPrefabs", prefabs);
            Set(spawner, "wallRunStartsAtSeconds", 0f);
            Set(spawner, "nextWallRunGap", 0);
            Set(spawner, "airRouteStartsAtSeconds", 9999f);
            Set(spawner, "mediumStartsAtSeconds", 0f);
            Component run = Get("RunManager");
            Set(run, "baseForwardSpeed", WallRunSpeed);
            Component flow = Get("GameFlowManager");
            Set(flow, "introDuration", 0.1f);
            Call(flow, "StartGame");
            yield return Until(() => Read(Get("GameFlowManager"), "State").ToString() == "Playing");
            section = null;
            foreach (Component s in (IEnumerable)T("WallRunSection").GetField("Active").GetValue(null))
                if (section == null || s.transform.position.z < section.transform.position.z)
                    section = s;
            Assert.IsNotNull(section, "The wall run spawned first.");
            Assert.AreEqual(pattern, section.transform.parent.name);
            sectionZ = section.transform.position.z;
            if (clearEnemies)
                foreach (Component enemy in section.transform.parent.GetComponentsInChildren(T("Enemy")))
                    enemy.GetType().GetMethod("TryKill").Invoke(enemy, new object[] { "Test" });
        }

        private static IEnumerator RunTo(float local) => Until(() => Local >= local || Dead, 6f);

        // Attach → switch → switch back past both obstacles → exit → landing.
        private static IEnumerator RideTheWalls(string entryCommand, string otherCommand)
        {
            if (!OnWall)
            {
                yield return RunTo(15f);
                Input(entryCommand);
            }
            Assert.IsTrue(OnWall, "Attached.");
            yield return RunTo(33f);
            Input(otherCommand);
            yield return RunTo(42f);
            Input(entryCommand);
            yield return Until(() => (!OnWall && Grounded) || Dead, 4f);
            Assert.IsFalse(Dead, "Alive through the wall run.");
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            float guard = Time.realtimeSinceStartup + seconds * 4f + 2f;
            while (Time.time < end && Time.realtimeSinceStartup < guard)
            {
                System.Threading.Thread.Sleep(1);
                yield return null;
            }
        }

        private static IEnumerator Until(Func<bool> condition, float seconds = 6f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                System.Threading.Thread.Sleep(1);
                yield return null;
            }
            Assert.IsTrue(condition(), "Timed out waiting.");
        }

        // ---- §58 / §59 / §61 entry ---------------------------------------------------

        [UnityTest]
        public IEnumerator LeftInputAttachesToTheLeftWallWithoutALaneMove()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return StartWallRun("Classic", Left);
            yield return RunTo(16f);
            Assert.IsFalse(OnWall, "Nothing attaches by itself in the entry window.");
            float x0 = PlayerPos.x;
            Input("RequestMoveLeft");
            Assert.IsTrue(OnWall);
            Assert.AreEqual("Left", Wall);
            Assert.AreEqual(0, Lane, "The swipe attached; it did not also change lane.");
            yield return null;
            Assert.Less(Mathf.Abs(PlayerPos.x - x0), WallLine * 0.5f, "Eases onto the wall, no snap.");
            yield return Wait(0.3f);
            Assert.AreEqual(CenterX - WallLine, PlayerPos.x, 0.05f, "On the left anchor line.");
            Assert.AreEqual(WallFeet, Feet, 0.12f, "Feet on the wall line.");
            Assert.Greater((float)Read(PlayerC, "WallTilt"), 5f, "Leans toward the left wall.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RightWallAcceptsOnlyARightInput()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return StartWallRun("Classic", Right);
            yield return RunTo(16f);
            Input("RequestMoveLeft");
            Assert.IsFalse(OnWall, "Wrong side: no attach.");
            Assert.AreEqual(-1, Lane, "Wrong side stays an ordinary lane move.");
            yield return Wait(0.15f);
            Input("RequestMoveRight");
            Assert.IsTrue(OnWall);
            Assert.AreEqual("Right", Wall);
            yield return Wait(0.3f);
            Assert.AreEqual(CenterX + WallLine, PlayerPos.x, 0.05f, "On the right anchor line.");
            Assert.Less((float)Read(PlayerC, "WallTilt"), -5f, "Leans toward the right wall.");
            yield return RideTheWalls("RequestMoveRight", "RequestMoveLeft");
            Assert.AreEqual(1, Lane, "Lands in the lane next to the right wall.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator NoInputFallsToDeath()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return StartWallRun("Classic", Left);
            yield return Until(() => Dead, 6f);
            Assert.AreEqual("Fall", DeathReason);
            Assert.IsFalse(OnWall);
            Assert.That(Local, Is.InRange(26f, 53f), "Fell into the gap.");
            Assert.Less(Feet, -3f);
            yield return Until(() => Read(Get("GameFlowManager"), "State").ToString() != "Dying", 4f);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RightPatternWithoutInputFallsToo()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return StartWallRun("Classic", Right);
            yield return Until(() => Dead, 6f);
            Assert.AreEqual("Fall", DeathReason);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator WrongSideInputMovesLaneThenFalls()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return StartWallRun("Classic", Left);
            yield return RunTo(18f);
            Input("RequestMoveRight");
            Assert.IsFalse(OnWall);
            Assert.AreEqual(1, Lane);
            Assert.IsFalse(Dead, "A wrong swipe is not an instant Game Over.");
            yield return Until(() => Dead, 6f);
            Assert.AreEqual("Fall", DeathReason);
            yield return new ExitPlayMode();
        }

        // ---- §60 input window --------------------------------------------------------

        [UnityTest]
        public IEnumerator EarlyInputIsALaneMoveAndALastMomentInputStillAttaches()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return StartWallRun("Classic", Left);
            yield return RunTo(12.5f);
            Input("RequestMoveLeft");
            Assert.IsFalse(OnWall, "Before the window: an ordinary lane move.");
            Assert.AreEqual(-1, Lane);
            // Last moment: past the floor edge, just inside the window.
            yield return RunTo(27.9f);
            Assert.Less(Feet, 0f, "Already dropping off the edge.");
            Input("RequestMoveLeft");
            Assert.IsTrue(OnWall, "A last-moment input inside the window attaches.");
            yield return Wait(0.3f);
            Assert.AreEqual(WallFeet, Feet, 0.12f, "Lifted onto the wall line.");
            // There is still time to switch before the first obstacle.
            yield return RideTheWalls("RequestMoveLeft", "RequestMoveRight");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator InputAfterTheWindowDoesNotAttach()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return StartWallRun("Classic", Left);
            yield return RunTo(29.6f);
            Input("RequestMoveLeft");
            Assert.IsFalse(OnWall, "Too late: no attach.");
            yield return Until(() => Dead, 6f);
            Assert.AreEqual("Fall", DeathReason);
            yield return new ExitPlayMode();
        }

        // ---- §62 / §63 / §69 / §70 / §72 switches, exit, controls, Classic ---------------

        [UnityTest]
        public IEnumerator WallSwitchesBothWaysAndControlsReturnAfterTheExit()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return StartWallRun("Classic", Left);
            Assert.IsTrue((bool)Read(Get("GameTimer"), "IsUntimed"), "Classic: same pattern, no timer.");
            yield return RunTo(15f);
            Input("RequestMoveLeft");
            yield return Wait(0.3f);
            float left = PlayerPos.x, y = PlayerPos.y;

            // Same direction: ignored. Jump / slide: ignored on the wall.
            Input("RequestMoveLeft");
            Input("RequestJump");
            Input("RequestSlide");
            yield return Wait(0.1f);
            Assert.IsTrue(OnWall);
            Assert.AreEqual("Left", Wall);
            Assert.AreEqual(left, PlayerPos.x, 0.01f, "No further out than the wall.");
            Assert.AreEqual(y, PlayerPos.y, 0.01f, "No jump on the wall.");
            Assert.IsFalse((bool)Read(PlayerC, "IsFastFalling"), "No fast fall on the wall.");

            // Left → Right; a new input mid-switch is ignored.
            Input("RequestMoveRight");
            Assert.AreEqual("Right", Wall);
            Assert.IsTrue((bool)Read(PlayerC, "IsWallSwitching"));
            yield return null;
            Input("RequestMoveLeft");
            Assert.AreEqual("Right", Wall, "Ignored while switching.");
            float switchStart = Local;
            yield return Until(() => !(bool)Read(PlayerC, "IsWallSwitching"), 1f);
            Assert.Greater(Local - switchStart, 2f, "Keeps running forward through the switch.");
            Assert.AreEqual(CenterX + WallLine, PlayerPos.x, 0.05f);
            yield return Wait(0.2f);
            Assert.Less((float)Read(PlayerC, "WallTilt"), -5f);

            // Right → Left.
            Input("RequestMoveLeft");
            yield return Wait(0.4f);
            Assert.AreEqual("Left", Wall);
            Assert.AreEqual(CenterX - WallLine, PlayerPos.x, 0.05f);
            Assert.Greater((float)Read(PlayerC, "WallTilt"), 5f);

            yield return RideTheWalls("RequestMoveLeft", "RequestMoveRight");
            Assert.AreEqual(-1, Lane, "Lands in the lane next to the left wall.");
            Assert.Less(Feet, 0.2f, "Back on the floor.");
            Assert.Greater(Local, 53f, "Landed after the gap.");
            yield return Wait(0.3f);
            Assert.AreEqual(0f, (float)Read(PlayerC, "WallTilt"), 0.5f, "Upright again.");

            // Normal controls are back.
            Input("RequestMoveRight");
            Assert.AreEqual(0, Lane, "Lane move works.");
            yield return Wait(0.2f);
            Input("RequestJump");
            yield return Wait(0.15f);
            Assert.IsFalse(Grounded, "Jump works.");
            Input("RequestSlide");
            Assert.IsTrue((bool)Read(PlayerC, "IsFastFalling"), "Fast fall works.");
            yield return Until(() => Grounded, 2f);
            yield return Wait(0.05f);
            Input("RequestSlide");
            Assert.IsTrue((bool)Read(PlayerC, "IsSliding"), "Slide works.");
            Assert.IsFalse(Dead);
            yield return new ExitPlayMode();
        }

        // ---- §64 / §65 / §67 obstacles, Souls, Shield --------------------------------

        [UnityTest]
        public IEnumerator StayingOnAnObstacleWallIsFatal()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return StartWallRun("Classic", Left);
            yield return RunTo(15f);
            Input("RequestMoveLeft");
            yield return Until(() => Dead, 4f);
            Assert.AreEqual("Collision", DeathReason);
            Assert.That(Local, Is.InRange(37f, 39f), "Stopped by the first wall obstacle.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SoulsOnTheWallAreCollected()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return StartWallRun("Classic", Left);
            yield return RunTo(15f);
            Input("RequestMoveLeft");
            int before = (int)Read(Get("CoinWallet"), "CurrentRunCoins");
            // Ride the reward line on the left wall up to its obstacle, then switch late.
            yield return RunTo(35.6f);
            int onLeft = (int)Read(Get("CoinWallet"), "CurrentRunCoins") - before;
            Assert.GreaterOrEqual(onLeft, 4, "Souls on the wall line are picked up.");
            Input("RequestMoveRight");
            yield return RunTo(44.9f);
            Assert.IsFalse(Dead, "A late switch still clears the obstacle.");
            Input("RequestMoveLeft");
            yield return Until(() => (!OnWall && Grounded) || Dead, 4f);
            Assert.IsFalse(Dead);
            Assert.GreaterOrEqual((int)Read(Get("CoinWallet"), "CurrentRunCoins") - before, 9, "Right-wall Souls too.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ShieldAbsorbsOneWallObstacleButNotAFall()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return StartWallRun("Classic", Left);
            Set(Get("RunInventory"), "shieldActive", true);
            yield return RunTo(15f);
            Input("RequestMoveLeft");
            yield return Until(() => !(bool)Read(Get("RunInventory"), "ShieldActive") || Dead, 4f);
            Assert.IsFalse(Dead, "The Shield absorbs the wall obstacle.");
            Assert.IsFalse((bool)Read(Get("RunInventory"), "ShieldActive"), "Used up.");
            // Off the blocked wall, the run continues.
            Input("RequestMoveRight");
            yield return Wait(0.4f);
            Assert.IsFalse(Dead);
            yield return RunTo(42f);
            Input("RequestMoveLeft");
            yield return Until(() => (!OnWall && Grounded) || Dead, 4f);
            Assert.IsFalse(Dead, "Finished the section after the Shield hit.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ShieldDoesNotStopAFall()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return StartWallRun("Classic", Left);
            Component inventory = Get("RunInventory");
            Set(inventory, "shieldActive", true);
            yield return Until(() => Dead, 6f);
            Assert.AreEqual("Fall", DeathReason);
            Assert.IsTrue((bool)Read(inventory, "ShieldActive"), "The fall did not use the Shield.");
            yield return new ExitPlayMode();
        }

        // ---- §68 / §40 revive -------------------------------------------------------------

        private static IEnumerator ReviveWithCoupon()
        {
            PlayerPrefs.SetInt("ReviveCouponCount", 1);
            yield return Until(() => Read(Get("GameFlowManager"), "State").ToString() == "RevivePrompt", 4f);
            Call(Get("GameFlowManager"), "UseCouponRevive");
            Assert.AreEqual("Playing", Read(Get("GameFlowManager"), "State").ToString());
            Assert.IsFalse(OnWall);
            Assert.AreEqual(13f, Local, 0.3f, "Back at the checkpoint before the entry.");
            Assert.AreEqual(0, Lane);
            Assert.AreEqual(CenterX, PlayerPos.x, 0.01f);
            yield return Wait(0.3f);
            Assert.IsTrue(Grounded, "On safe ground.");
            Assert.IsFalse(Dead, "No instant second death.");
        }

        [UnityTest]
        public IEnumerator ReviveAfterAFallRestartsBeforeTheEntry()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return StartWallRun("Classic", Left);
            yield return Until(() => Dead, 6f);
            Assert.AreEqual("Fall", DeathReason);
            yield return ReviveWithCoupon();
            yield return RideTheWalls("RequestMoveLeft", "RequestMoveRight");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ReviveAfterAWallHitRestartsBeforeTheEntry()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return StartWallRun("Classic", Left);
            yield return RunTo(15f);
            Input("RequestMoveLeft");
            yield return Until(() => Dead, 4f);
            Assert.AreEqual("Collision", DeathReason);
            yield return ReviveWithCoupon();
            yield return RideTheWalls("RequestMoveLeft", "RequestMoveRight");
            yield return new ExitPlayMode();
        }

        // ---- §71 Deadline -------------------------------------------------------------------

        // Played like a player at the speed wall runs appear: lane attack on the
        // approach enemy, attach, two switches, exit, lane attack on the landing
        // enemy. The countdown holds while on the wall (attach and switches never
        // reset it either) and runs again after the exit.
        [UnityTest]
        public IEnumerator DeadlineTimerPausesOnTheWallAndRunsAgainAfter()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return StartWallRun("Deadline", Left, false);
            Assert.IsFalse((bool)Read(Get("GameTimer"), "IsUntimed"));
            // Approach enemy (lane 0 at 12): step right, then attack back left.
            yield return RunTo(6.5f);
            Input("RequestMoveRight");
            yield return RunTo(9.6f);
            Input("RequestMoveLeft");
            yield return Wait(0.1f);
            Assert.IsFalse(Dead, "Approach enemy killed.");
            Assert.Greater(Timer, 3.8f, "Kill reset.");
            Assert.IsFalse(OnWall);
            float beforeWall = Timer;
            yield return RunTo(15f);
            Assert.Less(Timer, beforeWall - 0.1f, "Counting down on the ground.");

            Input("RequestMoveLeft");
            Assert.IsTrue(OnWall);
            yield return null;
            Assert.IsTrue((bool)Read(Get("GameTimer"), "IsHeldByWallRun"), "Shown as held (pause cue).");
            minTimer = Timer;
            // Held through the attach and both switches (made well before the
            // obstacles: no Near Miss).
            yield return Until(() =>
            {
                Assert.AreEqual(minTimer, Timer, 0.0001f, "Timer holds on the wall.");
                return Local >= 30f || Dead;
            }, 3f);
            Input("RequestMoveRight");
            yield return Until(() =>
            {
                Assert.AreEqual(minTimer, Timer, 0.0001f, "Timer holds through a switch.");
                return Local >= 41.5f || Dead;
            }, 3f);
            Input("RequestMoveLeft");
            yield return Until(() =>
            {
                if (OnWall)
                    Assert.AreEqual(minTimer, Timer, 0.0001f, "Timer holds through a switch.");
                return !OnWall || Dead;
            }, 3f);
            Assert.IsFalse(Dead);
            yield return RunTo(57.3f);
            Assert.IsFalse(Dead, "Timer still running after the exit.");
            Assert.Less(Timer, minTimer - 0.1f, "Counting down again after the exit.");
            Assert.IsFalse((bool)Read(Get("GameTimer"), "IsHeldByWallRun"));
            Assert.AreEqual(-1, Lane);
            // Landing enemy (lane 0 at 59.5): attack from the left lane.
            Input("RequestMoveRight");
            yield return Wait(0.1f);
            Assert.IsFalse(Dead);
            Assert.Greater(Timer, 3.8f, "Landing enemy killed: reset.");
            yield return new ExitPlayMode();
        }
    }
}
