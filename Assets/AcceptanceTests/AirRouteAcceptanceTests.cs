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
    // Upper Platform.md §45–56: air-enemy chain up to the deck, ordinary play on
    // the deck, the drop back to the ground, a safe lower route when the chain
    // is missed or abandoned (fast fall), Deadline timer resets on the chain,
    // the same route in Classic, the camera following big height changes only,
    // and the spawner's Upper rules. Records / Soul / stats are restored.
    public sealed class AirRouteAcceptanceTests
    {
        private const string ScenePath = "Assets/Scenes/EndlessRun.unity";
        private const string Folder = "Assets/Prefab/Pattern/";
        private const float Deck = 8f;
        private static readonly string[] AirRoutePatterns =
        {
            "Pattern_U1_AirRouteEntry", "Pattern_U5_AirRouteEntry_Weave", "Pattern_U2_UpperRun_Combat",
            "Pattern_U3_UpperRun_Hurdles", "Pattern_U4_UpperReturn",
        };
        private const string BackupKey = "Deadline4Sec.AirRouteTests.Backup";

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
                         "Deadline4Sec.BestCombo", "Deadline4Sec.BestScore_Classic", "Deadline4Sec.BestDistance_Classic", "Deadline4Sec.BestCombo_Classic" })
                yield return key;
            foreach (string name in Enum.GetNames(T("PlayerStat")))
                yield return "Stats_" + name;
            foreach (object definition in (Array)T("AchievementCatalog").GetMethod("Defaults").Invoke(null, null))
                yield return "AchievementUnlocked_" + definition.GetType().GetField("id").GetValue(definition);
        }

        [SetUp]
        public void SetUp()
        {
            if (UnityEditor.SessionState.GetString(BackupKey, "") == "")
            {
                var backup = new System.Text.StringBuilder();
                foreach (string key in new List<string>(Keys()))
                {
                    bool isFloat = key.Contains("Distance");
                    if (!PlayerPrefs.HasKey(key))
                        backup.Append(key).Append("\t-\t\n");
                    else
                        backup.Append(key).Append('\t').Append(isFloat ? "f" : "i").Append('\t')
                            .Append(isFloat ? PlayerPrefs.GetFloat(key).ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                                : PlayerPrefs.GetInt(key).ToString()).Append('\n');
                }
                UnityEditor.SessionState.SetString(BackupKey, backup.ToString());
            }
            PlayerPrefs.SetInt("Deadline4Sec.TutorialCompleted", 1);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string line in UnityEditor.SessionState.GetString(BackupKey, "").Split('\n'))
            {
                string[] parts = line.Split('\t');
                if (parts.Length < 3)
                    continue;
                if (parts[1] == "-")
                    PlayerPrefs.DeleteKey(parts[0]);
                else if (parts[1] == "f")
                    PlayerPrefs.SetFloat(parts[0], float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
                else
                    PlayerPrefs.SetInt(parts[0], int.Parse(parts[2]));
            }
            UnityEditor.SessionState.EraseString(BackupKey);
            PlayerPrefs.Save();
            Time.captureDeltaTime = 0f;
            T("GameModeState").GetMethod("Select").Invoke(null, new[] { Enum.Parse(T("GameMode"), "Deadline") });
        }

        // ---- data -------------------------------------------------------------------

        [Test]
        public void PatternsJoinOnOneDeckHeightAndBothLevelsHaveBeats()
        {
            foreach (string name in AirRoutePatterns)
            {
                Component pattern = Pattern(name);
                Assert.IsNotNull(pattern, name);
                string entry = Read(pattern, "EntryState").ToString();
                string exit = Read(pattern, "ExitState").ToString();
                float length = (float)Read(pattern, "PatternLength");
                List<Transform> decks = pattern.GetComponentsInChildren<Transform>(true).Where(t => t.name.EndsWith("Upper Deck")).ToList();
                Assert.IsNotEmpty(decks, name + " has a deck");
                foreach (Transform deck in decks)
                {
                    Collider body = deck.GetComponent<Collider>();
                    Assert.IsNotNull(body, "The deck is solid floor.");
                    Assert.IsNull(deck.GetComponentInParent(T("Obstacle")), "The deck is floor, not an Obstacle.");
                    Assert.AreEqual(Deck, deck.localPosition.y + deck.localScale.y * 0.5f, 0.001f, name + " deck top");
                }
                float deckStart = decks.Min(d => d.localPosition.z - d.localScale.z * 0.5f);
                float deckEnd = decks.Max(d => d.localPosition.z + d.localScale.z * 0.5f);
                if (entry == "Upper")
                    Assert.AreEqual(0f, deckStart, 0.001f, name + " continues the deck from its start");
                if (exit == "Upper")
                    Assert.AreEqual(length, deckEnd, 0.001f, name + " carries the deck to its end");
                if (exit != "Upper")
                    Assert.Less(deckEnd, length - 15f, name + " ends the deck early enough to land on the ground");

                // Kill / Near Miss chances on both levels (Deadline timer).
                var beats = new List<Transform>();
                foreach (Component c in pattern.GetComponentsInChildren(T("Enemy"), true))
                    beats.Add(c.transform);
                foreach (Component c in pattern.GetComponentsInChildren(T("Obstacle"), true))
                    beats.Add(c.transform);
                Assert.IsTrue(beats.Any(b => b.localPosition.y >= Deck), name + " has upper beats");
                Assert.IsTrue(beats.Any(b => b.localPosition.y < Deck - 3f && !IsAir(b)) || entry != "Upper", name + " has lower-route beats");

                if (entry != "Upper" && exit == "Upper")
                {
                    // The chain: air enemies in rising order, never the same lane twice in a row,
                    // the last one above the deck and before it, so its bounce lands on top.
                    List<Transform> chain = beats.Where(IsAir).Where(b => b.localPosition.z < deckStart).OrderBy(b => b.localPosition.z).ToList();
                    Assert.GreaterOrEqual(chain.Count, 3, name + " chain length");
                    for (int i = 1; i < chain.Count; i++)
                    {
                        Assert.Greater(chain[i].localPosition.y, chain[i - 1].localPosition.y, name + " climbs");
                        Assert.Greater(Mathf.Abs(chain[i].localPosition.x - chain[i - 1].localPosition.x), 0.01f, name + " alternates lanes");
                        Assert.LessOrEqual(chain[i].localPosition.z - chain[i - 1].localPosition.z, 6f, name + " next enemy within reach");
                    }
                    Transform last = chain[chain.Count - 1];
                    Assert.Greater(last.localPosition.y, Deck + 0.5f, "Last enemy above the deck.");
                    Assert.Less(deckStart - last.localPosition.z, 2.5f, "Deck right behind the last enemy.");
                    // A runner missing the last link falls from the previous one: head under the deck edge.
                    Transform previous = chain[chain.Count - 2];
                    float t = (deckStart - (previous.localPosition.z - 0.5f)) / 11.2f;
                    float head = previous.localPosition.y + 5.5f * t - 12.5f * t * t + 1f;
                    Assert.Less(head, Deck - 0.5f, name + ": a missed last link passes under the deck front");
                }
            }
        }

        private static bool IsAir(Transform t)
        {
            Component enemy = t.GetComponent(T("Enemy"));
            return enemy != null && enemy.GetType().GetProperty("Type").GetValue(enemy).ToString() == "Air";
        }

        // ---- spawner -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator SpawnerKeepsTheDeckConnectedAndSpacesAirRoutes()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return null;
            Component spawner = Get("PatternSpawner");
            Set(spawner, "airRouteStartsAtSeconds", 0f);
            Set(spawner, "mediumStartsAtSeconds", 0f);
            Set(spawner, "nextAirRouteGap", 0);
            MethodInfo choose = spawner.GetType().GetMethod("ChooseNextPattern", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo spawn = spawner.GetType().GetMethod("SpawnPattern", BindingFlags.Instance | BindingFlags.NonPublic);
            var sequence = new List<Component>();
            for (int i = 0; i < 70; i++)
            {
                var next = (Component)choose.Invoke(spawner, null);
                Assert.IsNotNull(next, "A compatible pattern exists at step " + i);
                spawn.Invoke(spawner, new object[] { next });
                sequence.Add(next);
            }
            int routes = 0, upperRuns = 0, sinceRoute = 99;
            for (int i = 0; i < sequence.Count; i++)
            {
                string entry = Read(sequence[i], "EntryState").ToString();
                string exit = Read(sequence[i], "ExitState").ToString();
                string previousExit = i > 0 ? Read(sequence[i - 1], "ExitState").ToString() : "Ground";
                Assert.AreEqual(previousExit == "Upper", entry == "Upper",
                    $"Step {i} {sequence[i].name}: Upper follows Upper and only Upper.");
                if (entry != "Upper" && exit == "Upper")
                {
                    routes++;
                    Assert.GreaterOrEqual(sinceRoute, i == FirstRouteIndex(sequence) ? 0 : 5, "Ordinary patterns between air routes.");
                    upperRuns = 0;
                }
                else if (entry == "Upper" && exit == "Upper")
                    upperRuns++;
                else if (entry == "Upper")
                {
                    Assert.That(upperRuns, Is.InRange(1, 2), "1–2 upper runs before the return.");
                    sinceRoute = 0;
                    continue;
                }
                if (entry != "Upper")
                    sinceRoute++;
            }
            Assert.GreaterOrEqual(routes, 2, "Air routes keep appearing.");
            yield return new ExitPlayMode();
        }

        private static int FirstRouteIndex(List<Component> sequence) =>
            sequence.FindIndex(p => Read(p, "ExitState").ToString() == "Upper" && Read(p, "EntryState").ToString() != "Upper");

        // ---- play --------------------------------------------------------------------

        // Starts a run whose course is: U1 entry → U2 upper run → U4 return → ordinary.
        // (Each test enters play mode itself: the domain reload must happen in
        // the test method, not in a nested coroutine.)
        private static IEnumerator StartAirRouteRun(string mode)
        {
            // Fixed 60 fps steps (like the course replay): batch mode runs
            // uncapped, and the chain timing must not depend on that.
            Time.captureDeltaTime = 1f / 60f;
            yield return null;
            T("GameModeState").GetMethod("Select").Invoke(null, new[] { Enum.Parse(T("GameMode"), mode) });
            Component spawner = Get("PatternSpawner");
            // Ordinary patterns of mixed categories so the spawner always finds one.
            string[] course = { "Pattern_U1_AirRouteEntry", "Pattern_U2_UpperRun_Combat", "Pattern_U4_UpperReturn",
                "Pattern_A_LaneAttack", "Pattern_B_JumpSlide", "Pattern_F_Crossfire", "Pattern_G_Hurdles", "Pattern_I_ArchWalk" };
            Array prefabs = Array.CreateInstance(T("CoursePattern"), course.Length);
            for (int i = 0; i < course.Length; i++)
                prefabs.SetValue(Pattern(course[i]), i);
            Set(spawner, "patternPrefabs", prefabs);
            Set(spawner, "airRouteStartsAtSeconds", 0f);
            Set(spawner, "mediumStartsAtSeconds", 0f);
            Set(spawner, "nextAirRouteGap", 0);
            Set(spawner, "upperPatternsMax", 1);
            Component flow = Get("GameFlowManager");
            Set(flow, "introDuration", 0.1f);
            Call(flow, "StartGame");
            yield return Until(() => Read(Get("GameFlowManager"), "State").ToString() == "Playing");
        }

        // Lambda state lives in statics: locals captured by a lambda would be
        // created before EnterPlayMode and lost in the domain reload.
        private static float routeEnd, routeStart, trackedMaxY;

        private static Component PlayerC => Get("PlayerController");
        private static Vector3 PlayerPos => PlayerC.transform.position;
        private static bool Grounded => (bool)Read(PlayerC, "IsGrounded");
        private static bool Dead => (bool)Read(Get("GameTimer"), "IsGameOver");

        private static Component RouteInstance(string name)
        {
            foreach (Component p in Get("PatternSpawner").GetComponentsInChildren(T("CoursePattern")))
                if (p.name == name)
                    return p;
            return null;
        }

        private static float RouteZ(string name) => RouteInstance(name).transform.position.z;

        // Next live air enemy ahead (and above the floor under the runner).
        private static Component NextAir(float maxAhead)
        {
            Component best = null;
            float bestDz = maxAhead;
            foreach (Component e in UnityEngine.Object.FindObjectsByType(T("Enemy"), FindObjectsSortMode.None))
            {
                if ((bool)Read(e, "IsKilled") || Read(e, "Type").ToString() != "Air")
                    continue;
                float dz = e.transform.position.z - PlayerPos.z;
                if (dz > -0.5f && dz < bestDz)
                {
                    bestDz = dz;
                    best = e;
                }
            }
            return best;
        }

        // Plays the chain like a player: jump at the first air enemy, then a
        // jump (homing) whenever the next one is close, up to `links` kills.
        private static IEnumerator ClimbChain(int links, List<float> timerAfterKills)
        {
            int kills = 0;
            Component timer = Get("GameTimer");
            float lastAction = -1f;
            float end = Time.realtimeSinceStartup + 8f;
            int killedBefore = KilledAir();
            while (kills < links && !Dead && Time.realtimeSinceStartup < end)
            {
                bool homing = (bool)Field(PlayerC, "isHomingDash");
                Component next = NextAir(7f);
                if (next != null && !homing && Time.time - lastAction > 0.12f)
                {
                    float dz = next.transform.position.z - PlayerPos.z;
                    if ((Grounded && dz < 4.2f) || !Grounded)
                    {
                        PlayerC.GetType().GetMethod("RequestJump").Invoke(PlayerC, null);
                        lastAction = Time.time;
                    }
                }
                System.Threading.Thread.Sleep(1);
                yield return null;
                int killed = KilledAir();
                if (killed > killedBefore)
                {
                    kills += killed - killedBefore;
                    killedBefore = killed;
                    timerAfterKills?.Add((float)Read(timer, "RemainingTime"));
                }
            }
            Assert.IsFalse(Dead, "Alive through the chain.");
            Assert.AreEqual(links, kills, "Chain links killed.");
        }

        // Plays the lower route like a player (ground enemies need a side
        // attack, obstacles a jump / slide / lane change) until `done`. Only
        // beats on the ground level count; deck beats are 8m up.
        private static IEnumerator RunLowerRoute(Func<bool> done, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            lastGuardAction = -1f;
            while (!done() && !Dead && Time.realtimeSinceStartup < end)
            {
                GuardStep();
                System.Threading.Thread.Sleep(1);
                yield return null;
            }
        }

        private static float lastGuardAction;

        private static void GuardStep()
        {
            if (Time.time - lastGuardAction < 0.15f)
                return;
            Component player = PlayerC;
            Component next = null;
            float nextDz = 8f;
            foreach (string type in new[] { "Enemy", "Obstacle" })
            foreach (Component c in UnityEngine.Object.FindObjectsByType(T(type), FindObjectsSortMode.None))
            {
                if (type == "Enemy" && ((bool)Read(c, "IsKilled") || Read(c, "Type").ToString() == "Air"))
                    continue;
                Collider body = c.GetComponent<Collider>();
                if (body == null || body.bounds.min.y > 3f)
                    continue;
                float dz = c.transform.position.z - PlayerPos.z;
                if (dz < -0.8f || dz >= nextDz)
                    continue;
                nextDz = dz;
                next = c;
            }
            if (next == null)
                return;
            int target = Mathf.Clamp(Mathf.RoundToInt((next.transform.position.x - (float)Field(player, "centerX")) / 2.5f), -1, 1);
            int lane = (int)Field(player, "laneIndex");
            string away = lane <= 0 ? "RequestMoveRight" : "RequestMoveLeft";
            string toward = target > lane ? "RequestMoveRight" : "RequestMoveLeft";
            string command = null;
            if (next.GetType().Name == "Enemy")
            {
                if (lane == target && nextDz > 3.5f && nextDz < 7f)
                    command = away;
                else if (Mathf.Abs(lane - target) == 1 && nextDz <= 2.6f)
                    command = toward;
            }
            else if (lane == target)
            {
                string kind = Field(next, "obstacleType").ToString();
                if (kind == "JumpObstacle" && Grounded && nextDz <= 11f * 0.14f + 1f)
                    command = "RequestJump";
                else if (kind == "SlideObstacle" && nextDz <= 11f * 0.25f + 1f && !(bool)Field(player, "isSliding"))
                    command = "RequestSlide";
                else if (kind != "JumpObstacle" && kind != "SlideObstacle" && nextDz <= 4.5f)
                    command = away;
            }
            if (command == null)
                return;
            lastGuardAction = Time.time;
            player.GetType().GetMethod(command).Invoke(player, null);
        }

        // Killed enemies vanish within the frame, so count air kills from the
        // run's own stats (PlayerStats.Run.AirKills).
        private static int KilledAir()
        {
            object run = T("PlayerStats").GetProperty("Run").GetValue(null);
            return (int)run.GetType().GetField("AirKills").GetValue(run);
        }

        [UnityTest]
        public IEnumerator DeadlineChainClimbsToTheDeckAndPlaysThere()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return StartAirRouteRun("Deadline");
            var timers = new List<float>();
            yield return ClimbChain(4, timers);
            Assert.That(timers, Has.All.GreaterThan(3.5f), "Each homing kill resets the 4-second timer.");
            yield return Until(() => Grounded || Dead, 3f);
            Assert.IsFalse(Dead);
            Assert.Greater(PlayerPos.y, Deck + 0.5f, "Landed on the deck.");
            float deckY = PlayerPos.y;

            // Ordinary controls on the deck.
            float x = PlayerPos.x;
            PlayerC.GetType().GetMethod("RequestMoveRight").Invoke(PlayerC, null);
            yield return Wait(0.3f);
            Assert.Greater(PlayerPos.x, x + 1f, "Lane move on the deck.");
            PlayerC.GetType().GetMethod("RequestJump").Invoke(PlayerC, null);
            yield return Wait(0.2f);
            Assert.IsFalse(Grounded, "Jump on the deck.");
            Assert.Greater(PlayerPos.y, deckY + 0.5f);
            yield return Until(() => Grounded, 2f);
            Assert.AreEqual(deckY, PlayerPos.y, 0.2f, "Back on the deck.");
            Assert.IsFalse(Dead);
            // Camera settled on the new level (offset back to normal).
            yield return Wait(0.8f);
            Transform cam = Camera.main.transform;
            Assert.AreEqual(cam.parent.position.y + 4.31f, cam.position.y, 0.6f, "Camera at the usual height above the deck.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ClassicUsesTheSameRouteWithoutATimer()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return StartAirRouteRun("Classic");
            Assert.IsTrue((bool)Read(Get("GameTimer"), "IsUntimed"));
            yield return ClimbChain(4, null);
            yield return Until(() => Grounded || Dead, 3f);
            Assert.IsFalse(Dead);
            Assert.Greater(PlayerPos.y, Deck + 0.5f, "Classic lands on the deck too.");
            yield return new ExitPlayMode();
        }

        // Missed chain: one kill, then no input — the lower route carries on
        // to the end of the entry pattern (alive, on the ground, under the deck).
        [UnityTest]
        public IEnumerator MissedChainContinuesOnTheLowerRoute()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            // Classic: this checks the route for unfair deaths, without the
            // 4-second rule (the script makes no kills down there).
            yield return StartAirRouteRun("Classic");
            yield return ClimbChain(1, null);
            routeEnd = RouteZ("Pattern_U1_AirRouteEntry") + 40f;
            trackedMaxY = 0f;
            yield return RunLowerRoute(() =>
            {
                trackedMaxY = Mathf.Max(trackedMaxY, PlayerPos.y);
                return PlayerPos.z > routeEnd;
            }, 8f);
            Assert.IsFalse(Dead, "No death on the lower route after a missed chain.");
            Assert.Greater(PlayerPos.z, routeEnd);
            Assert.Less(PlayerPos.y, 2f, "Still on the lower route.");
            Assert.Less(trackedMaxY, Deck - 2f, "Never reached deck height.");
            yield return new ExitPlayMode();
        }

        // Fast fall in the middle of the chain drops to the lower route safely.
        [UnityTest]
        public IEnumerator FastFallMidChainLandsOnTheLowerRoute()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            // Classic: this checks the route for unfair deaths, without the
            // 4-second rule (the script makes no kills down there).
            yield return StartAirRouteRun("Classic");
            yield return ClimbChain(3, null);
            PlayerC.GetType().GetMethod("RequestSlide").Invoke(PlayerC, null);
            routeEnd = RouteZ("Pattern_U1_AirRouteEntry") + 40f;
            yield return Until(() => Dead || (Grounded && PlayerPos.y < 2f), 3f);
            Assert.IsFalse(Dead);
            Assert.Less(PlayerPos.y, 2f, "Fast fall reached the lower route.");
            yield return RunLowerRoute(() => PlayerPos.z > routeEnd, 8f);
            Assert.IsFalse(Dead, "Lower route stays safe to the end of the entry.");
            Assert.Greater(PlayerPos.z, routeEnd);
            yield return new ExitPlayMode();
        }

        // The return: on the deck past its enemy, run off the end and land on the ground.
        [UnityTest]
        public IEnumerator ReturnDropsBackToTheGround()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            // Classic: this checks the route for unfair deaths, without the
            // 4-second rule (the script makes no kills down there).
            yield return StartAirRouteRun("Classic");
            yield return Until(() => RouteInstance("Pattern_U4_UpperReturn") != null, 3f);
            routeStart = RouteZ("Pattern_U4_UpperReturn");
            float start = routeStart;
            // Put the runner on the return deck, centre lane, past its enemy.
            Component player = PlayerC;
            CharacterController body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = new Vector3((float)Field(player, "centerX"), Deck + 1.08f, start + 11f); // clear of the deck enemy at 6m
            Set(player, "laneIndex", 0);
            body.enabled = true;
            yield return Until(() => Grounded, 1f);
            Assert.Greater(PlayerPos.y, Deck);
            yield return Until(() => !Grounded || Dead, 2f);
            Assert.IsFalse(Dead);
            Assert.Greater(PlayerPos.z, start + 13.5f, "Left the deck at its end.");
            yield return Until(() => (Grounded && PlayerPos.y < 2f) || Dead, 3f);
            Assert.IsFalse(Dead, "Landed safely.");
            Assert.Less(PlayerPos.y, 2f, "Back on the ground route.");
            yield return Wait(1f);
            Transform cam = Camera.main.transform;
            Assert.AreEqual(cam.parent.position.y + 4.31f, cam.position.y, 0.3f, "Camera back at ground framing.");
            yield return RunLowerRoute(() => PlayerPos.z > routeStart + 34f, 3f);
            Assert.IsFalse(Dead, "Ground route after the return is playable.");
            Assert.Greater(PlayerPos.z, routeStart + 34f);
            yield return new ExitPlayMode();
        }

        // Game-time wait (with a real-time guard).
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
    }
}
