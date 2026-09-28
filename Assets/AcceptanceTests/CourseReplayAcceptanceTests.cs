using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    // Uses the production scene, spawned prefab geometry and public player inputs.
    // No enemy kills, timer resets, velocity changes or teleports are injected.
    public sealed class CourseReplayAcceptanceTests
    {
        private static Type Find(string name) => Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
        private static Component Get(string name) => (Component)UnityEngine.Object.FindFirstObjectByType(Find(name));
        private static object Read(Component target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static T Field<T>(Component target, string name) => (T)target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Input(Component player, string name) => player.GetType().GetMethod(name).Invoke(player, null);

        [UnityTest]
        public IEnumerator FixedSeedProductionCourseHasAPlayableSixtySecondRoute()
        {
            return Replay(12345, 60, "course-replay.txt");
        }

        [UnityTest]
        public IEnumerator ProductionCourseAlsoWorksAtThirtyFramesPerSecond()
        {
            return Replay(12345, 30, "course-replay-30fps.txt");
        }

        [UnityTest]
        public IEnumerator AnotherSeedHasAPlayableSixtySecondRoute()
        {
            return Replay(1, 60, "course-replay-seed-1.txt");
        }

        [UnityTest]
        public IEnumerator LongRunMaintainsRewardsAndTransitionsAtMaximumSpeed()
        {
            return Replay(98765, 30, "course-replay-long.txt", 180f);
        }

        private static IEnumerator Replay(int seed, int framesPerSecond, string evidenceFile, float duration = 60f)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            float oldCaptureDelta = Time.captureDeltaTime;
            StringBuilder trace = new StringBuilder($"seed={seed}, timestep=1/{framesPerSecond}, duration={duration}, scene=EndlessRun\n");
            Component player = Get("PlayerController");
            Component timer = Get("GameTimer");
            Component run = Get("RunManager");
            Component flow = Get("GameFlowManager");
            Component spawner = Get("PatternSpawner");
            spawner.GetType().GetField("random", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(spawner, new System.Random(seed));
            float centerX = Field<float>(player, "centerX");
            HashSet<string> entered = new HashSet<string>();
            HashSet<int> seenPatternIds = new HashSet<int>();
            Dictionary<int, float> actionTimes = new Dictionary<int, float>();
            HashSet<int> bypassedEnemies = new HashSet<int>();
            int previousScore = 0;
            float lastRewardTime = 0f;
            int rewardCount = 0;
            float maximumRewardGap = 0f;
            string previousPattern = null;
            string previousCategory = null;
            int categoryStreak = 0;
            int frames = 0;
            // Hit stops advance real time while accelerated game time is paused.
            // Keep the harness timeout proportional to the requested run length.
            int frameLimit = Mathf.CeilToInt(20000f * duration / 60f);
            try
            {
                Time.captureDeltaTime = 1f / framesPerSecond;
                flow.GetType().GetMethod("StartGame").Invoke(flow, null);
                while (!(bool)Read(timer, "IsRunning") && frames++ < 10000)
                    yield return null;
                Assert.Less(frames, 10000);
                frames = 0;
                while ((float)Read(run, "CurrentRunTime") < duration && frames++ < frameLimit)
                {
                    float time = (float)Read(run, "CurrentRunTime");
                    float z = player.transform.position.z;
                    foreach (Component pattern in spawner.GetComponentsInChildren(Find("CoursePattern")))
                    {
                        if (z < pattern.transform.position.z || !seenPatternIds.Add(pattern.GetInstanceID()))
                            continue;
                        entered.Add(pattern.name);
                        trace.AppendLine($"ENTER t={time:F2} z={z:F2} {pattern.name} tier={Read(pattern, "Difficulty")} feet={player.GetComponent<CharacterController>().bounds.min.y:F2}");
                        string difficulty = Read(pattern, "Difficulty").ToString();
                        string category = Read(pattern, "Category").ToString();
                        Assert.AreNotEqual(previousPattern, pattern.name,
                            "The same prefab appeared consecutively.");
                        categoryStreak = category == previousCategory ? categoryStreak + 1 : 1;
                        Assert.LessOrEqual(categoryStreak, 2,
                            "The same category appeared three times consecutively.");
                        previousPattern = pattern.name;
                        previousCategory = category;
                        Assert.IsFalse(difficulty == "Medium" && time < 20f,
                            "Medium entered before the actual run-time unlock.");
                        Assert.IsFalse(difficulty == "Hard" && time < 45f,
                            "Hard entered before the actual run-time unlock.");
                        Assert.IsFalse(difficulty == "Easy" && time >= 45f,
                            "Easy entered after the hard-tier transition.");
                    }
                    int score = (int)Read(run, "CurrentScore");
                    if (score > previousScore)
                    {
                        trace.AppendLine($"REWARD t={time:F2} z={z:F2} score={score} gap={time-lastRewardTime:F2}");
                        maximumRewardGap = Mathf.Max(maximumRewardGap, time - lastRewardTime);
                        Assert.LessOrEqual(time - lastRewardTime, 4f);
                        lastRewardTime = time;
                        previousScore = score;
                        rewardCount++;
                    }
                    if ((bool)Read(timer, "IsGameOver"))
                    {
                        trace.AppendLine($"DEATH t={time:F2} z={z:F2} x={player.transform.position.x-centerX:F2} feet={player.GetComponent<CharacterController>().bounds.min.y:F2}");
                        Assert.Fail($"Production course replay died. See Verification/{evidenceFile}. " + trace.ToString().Substring(Math.Max(0, trace.Length - 1800)));
                    }
                    if (Time.timeScale > 0f)
                        Drive(player, centerX, time, actionTimes, bypassedEnemies, trace);
                    else
                        System.Threading.Thread.Sleep(1);
                    yield return null;
                }
                Assert.Less(frames, frameLimit);
                Assert.IsFalse((bool)Read(timer, "IsGameOver"));
                Assert.Greater(rewardCount, 20);
                Assert.That(entered, Does.Contain("Pattern_A_LaneAttack"));
                Assert.That(entered, Does.Contain("Pattern_B_JumpSlide"));
                Assert.That(entered, Does.Contain("Pattern_C_AirCombo"));
                Assert.That(entered, Does.Contain("Pattern_D_Stomp"));
                Assert.That(entered, Does.Contain("Pattern_E_MixedRisk"));
                trace.AppendLine($"FINISH time={Read(run, "CurrentRunTime")} score={Read(run, "CurrentScore")} distance={Read(run, "Distance")} rewards={rewardCount} maximumRewardGap={maximumRewardGap.ToString("F2", CultureInfo.InvariantCulture)}");
            }
            finally
            {
                Time.captureDeltaTime = oldCaptureDelta;
                string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
                if (System.IO.Path.GetFileName(root) == "ValidationProject")
                    root = System.IO.Path.GetDirectoryName(root);
                System.IO.File.WriteAllText(System.IO.Path.Combine(root, "Verification", evidenceFile), trace.ToString());
            }
            yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void Drive(Component player, float centerX, float time,
            Dictionary<int, float> actions, HashSet<int> bypassedEnemies, StringBuilder trace)
        {
            Component next = null;
            float nextDistance = float.PositiveInfinity;
            foreach (string type in new[] { "Enemy", "Obstacle" })
            foreach (Component candidate in UnityEngine.Object.FindObjectsByType(Find(type), FindObjectsSortMode.None))
            {
                if (type == "Enemy" && (bool)Read(candidate, "IsKilled"))
                    continue;
                if (bypassedEnemies.Contains(candidate.GetInstanceID()))
                    continue;
                float dz = candidate.transform.position.z - player.transform.position.z;
                if (dz < -0.8f || dz >= nextDistance)
                    continue;
                nextDistance = dz;
                next = candidate;
            }
            if (next == null || Field<bool>(player, "isHomingDash"))
                return;
            float speed = (float)player.GetType().GetField("runForwardSpeed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
            int targetLane = Mathf.Clamp(Mathf.RoundToInt((next.transform.position.x - centerX) / 2.5f), -1, 1);
            int lane = Field<int>(player, "laneIndex");
            bool grounded = Field<bool>(player, "isGrounded");
            Bounds bounds = player.GetComponent<CharacterController>().bounds;
            string command = null;
            if (next.GetType().Name == "Enemy")
            {
                bool air = Read(next, "Type").ToString() == "Air";
                if (air)
                {
                    if (grounded)
                    {
                        if (lane != targetLane && nextDistance > 5f)
                            command = targetLane > lane ? "RequestMoveRight" : "RequestMoveLeft";
                        else if (nextDistance <= 4.2f)
                            command = "RequestJump";
                    }
                    else if (nextDistance < 7f)
                        command = "RequestJump";
                }
                else if (!grounded && bounds.min.y > next.GetComponent<Collider>().bounds.max.y + 0.1f)
                {
                    float riseSpeed = Mathf.Max(0f, Field<float>(player, "verticalSpeed"));
                    float peakFeet = bounds.min.y + riseSpeed * riseSpeed /
                        (2f * Mathf.Abs(Field<float>(player, "gravity")));
                    // A low air-combo bounce cannot clear the GroundFront zone.
                    // Use the legal high-side bypass instead of assuming a Stomp
                    // can ignore the frontal attack before reaching the head.
                    float frontTop = 0f;
                    float sideTop = 0f;
                    foreach (Component zone in next.GetComponentsInChildren(Find("EnemyAttackZone")))
                    {
                        string zoneType = Field<object>(zone, "zoneType").ToString();
                        if (zoneType == "GroundFront")
                            frontTop = Mathf.Max(frontTop, zone.GetComponent<Collider>().bounds.max.y);
                        else if (zoneType == "GroundSide")
                            sideTop = Mathf.Max(sideTop, zone.GetComponent<Collider>().bounds.max.y);
                    }
                    if (peakFeet < frontTop + 0.05f && bounds.min.y > sideTop + 0.05f &&
                        nextDistance > 3.2f && nextDistance < 6f)
                    {
                        if (lane == targetLane)
                            command = lane <= 0 ? "RequestMoveRight" : "RequestMoveLeft";
                        bypassedEnemies.Add(next.GetInstanceID());
                        trace.AppendLine($"BYPASS t={time:F2} {next.name} peakFeet={peakFeet:F2}");
                    }
                    else if (lane != targetLane)
                        command = targetLane > lane ? "RequestMoveRight" : "RequestMoveLeft";
                    else if (nextDistance <= speed * (bounds.min.y - next.GetComponent<Collider>().bounds.max.y) / 22f + 0.8f &&
                        !Field<bool>(player, "isFastFalling"))
                        command = "RequestSlide";
                }
                else
                {
                    if (lane == targetLane && nextDistance > 3.5f && nextDistance < 7f)
                        command = lane <= 0 ? "RequestMoveRight" : "RequestMoveLeft";
                    else if (lane != targetLane && nextDistance <= 2.6f)
                        command = targetLane > lane ? "RequestMoveRight" : "RequestMoveLeft";
                }
            }
            else
            {
                string obstacleType = Field<object>(next, "obstacleType").ToString();
                if (lane != targetLane && nextDistance > 4.5f)
                    command = targetLane > lane ? "RequestMoveRight" : "RequestMoveLeft";
                else if (obstacleType == "SlideObstacle" && nextDistance <= speed * 0.25f + 1f &&
                    !Field<bool>(player, "isSliding") && !Field<bool>(player, "isFastFalling"))
                {
                    // Down in the air is the normal fast-fall input; landing then
                    // continues into a ground slide without a second input.
                    command = "RequestSlide";
                }
                else if (obstacleType == "LaneBlocker" && lane == targetLane && nextDistance <= 4.5f)
                {
                    command = lane <= 0 ? "RequestMoveRight" : "RequestMoveLeft";
                }
                else if (grounded && nextDistance <= speed * 0.14f + 1f)
                {
                    if (obstacleType == "JumpObstacle")
                        command = "RequestJump";
                }
            }
            if (command == null)
                return;
            int key = next.GetInstanceID() * 31 + command.GetHashCode();
            if (actions.TryGetValue(key, out float last) && time - last < 0.12f)
                return;
            actions[key] = time;
            trace.AppendLine($"INPUT t={time:F2} z={player.transform.position.z:F2} lane={lane} feet={bounds.min.y:F2} {command} -> {next.name} dz={nextDistance:F2}");
            Input(player, command);
        }
    }
}
