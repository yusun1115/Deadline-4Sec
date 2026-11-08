using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    // 2026-11-07, reported in play: Pattern_E_MixedRisk puts a ground enemy 3.5 m
    // behind an air enemy in the same lane. Homing into the air enemy left the
    // runner high above the ground enemy's head, yet inside its tall front zone
    // ~0.1 s later: a death nobody could see or answer. Flying well above the
    // head is safe; a plain jump over a ground enemy stays fatal (spec 11.1).
    public sealed class AirKillOverGroundEnemyAcceptanceTests
    {
        private static Type Find(string name) => Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
        private static bool Is(Component target, string property) => (bool)target.GetType().GetProperty(property).GetValue(target);
        private static void Call(Component target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);

        private static GameObject Place(string path, Vector3 position) =>
            UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), position, Quaternion.identity);

        private static Component CreateCourse(float airZ, float groundZ, out Component timer, out Component air, out Component ground)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.1f, 40f);
            floor.transform.localScale = new Vector3(10f, 0.2f, 120f);
            Physics.SyncTransforms();
            timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            GameObject player = new GameObject("Player");
            player.transform.position = Vector3.zero;
            CharacterController body = player.AddComponent<CharacterController>();
            body.center = new Vector3(0f, 1f, 0f);
            body.height = 2f;
            body.radius = 0.5f;
            Component controller = player.AddComponent(Find("PlayerController"));
            air = airZ > 0f ? Place("Assets/Prefab/Enemy/EnemyAir.prefab", new Vector3(0f, 3.2f, airZ)).GetComponent(Find("Enemy")) : null;
            ground = Place("Assets/Prefab/Enemy/Enemy.prefab", new Vector3(0f, 0.74f, groundZ)).GetComponent(Find("Enemy"));
            Physics.SyncTransforms();
            return controller;
        }

        private static IEnumerator HomeIntoAirEnemyThenDoNothing(float speed)
        {
            Component player = CreateCourse(14f, 17.5f, out Component timer, out Component air, out Component ground);
            float oldDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                yield return null;
                Call(timer, "BeginRun");
                Call(player, "SetRunForwardSpeed", speed);
                bool jumped = false;
                int frames = 0;
                while (player.transform.position.z < 30f && !Is(timer, "IsGameOver") && frames++ < 1200)
                {
                    float z = player.transform.position.z;
                    if (!jumped && z >= 14f - speed * 0.3f)
                    {
                        Call(player, "RequestJump");
                        jumped = true;
                    }
                    else if (jumped && !Is(air, "IsKilled") && !Is(player, "IsGrounded") && !Is(player, "IsHoming"))
                        Call(player, "RequestJump"); // the second up: homing
                    yield return null;
                }
                Assert.IsTrue(Is(air, "IsKilled"), "The air enemy was reached.");
                Assert.IsFalse(Is(timer, "IsGameOver"),
                    $"Killed high above the ground enemy's head at {speed} m/s, died at z={player.transform.position.z:F2} " +
                    $"feet={player.GetComponent<CharacterController>().bounds.min.y:F2}");
                Assert.IsFalse(Is(ground, "IsKilled"), "Nothing touched the ground enemy.");
            }
            finally
            {
                Time.captureDeltaTime = oldDelta;
            }
        }

        [UnityTest]
        public IEnumerator FlyingOverAfterAnAirKillIsSafeAtBaseSpeed()
        {
            yield return new EnterPlayMode();
            yield return HomeIntoAirEnemyThenDoNothing(12f);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator FlyingOverAfterAnAirKillIsSafeAtMaximumSpeed()
        {
            yield return new EnterPlayMode();
            yield return HomeIntoAirEnemyThenDoNothing(16f);
            yield return new ExitPlayMode();
        }

        // Wherever the ground enemy stands behind the air enemy, the runner either
        // flies past or has time to see it coming and swipe down.
        [UnityTest]
        public IEnumerator AnyDeathAfterAnAirKillLeavesTimeToAnswer()
        {
            yield return new EnterPlayMode();
            float oldDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                foreach (float delta in new[] { 1f / 60f, 1f / 30f })
                foreach (float speed in new[] { 12f, 13f, 14f, 16f })
                {
                    Time.captureDeltaTime = delta;
                    foreach (float gap in new[] { 3.5f, 4.5f, 5.5f, 6.5f, 7.5f, 8.5f, 10f, 12f, 15f })
                    {
                        Component player = CreateCourse(14f, 14f + gap, out Component timer, out Component air, out _);
                        yield return null;
                        Call(timer, "BeginRun");
                        Call(player, "SetRunForwardSpeed", speed);
                        bool jumped = false;
                        int frames = 0;
                        float seconds = 0f;
                        while (player.transform.position.z < 14f + gap + 3f && !Is(timer, "IsGameOver") && frames++ < 1200)
                        {
                            float z = player.transform.position.z;
                            if (!jumped && z >= 14f - speed * 0.3f)
                            {
                                Call(player, "RequestJump");
                                jumped = true;
                            }
                            else if (jumped && !Is(air, "IsKilled") && !Is(player, "IsGrounded") && !Is(player, "IsHoming"))
                                Call(player, "RequestJump");
                            if (Is(air, "IsKilled"))
                                seconds += Time.deltaTime;
                            yield return null;
                        }
                        Assert.IsTrue(Is(air, "IsKilled"), $"The air enemy was reached at {speed} m/s, gap {gap} m.");
                        Debug.Log($"AirKillSweep fps={1f / delta:F0} speed={speed} gap={gap} died={Is(timer, "IsGameOver")} afterKill={seconds:F2}s feet={player.GetComponent<CharacterController>().bounds.min.y:F2}");
                        if (Is(timer, "IsGameOver"))
                            Assert.GreaterOrEqual(seconds, 0.4f, $"Died too soon after the kill at {speed} m/s, gap {gap} m.");
                        if (gap <= 3.5f)
                            Assert.IsFalse(Is(timer, "IsGameOver"), $"The reported layout must be flown over at {speed} m/s, {1f / delta:F0} fps.");
                        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                            UnityEngine.Object.DestroyImmediate(root);
                        yield return null;
                    }
                }
            }
            finally
            {
                Time.captureDeltaTime = oldDelta;
            }
            yield return new ExitPlayMode();
        }

        // Spec 11.1: however it is timed, a plain jump can't clear a ground enemy.
        [UnityTest]
        public IEnumerator PlainJumpOverAGroundEnemyStaysFatal()
        {
            yield return new EnterPlayMode();
            float oldDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                foreach (float jumpAtDistance in new[] { 5f, 4f, 3f, 2.2f })
                {
                    Component player = CreateCourse(0f, 30f, out Component timer, out _, out Component ground);
                    yield return null;
                    Call(timer, "BeginRun");
                    Call(player, "SetRunForwardSpeed", 12f);
                    float head = ground.transform.position.z;
                    player.transform.position = new Vector3(0f, 0.08f, head - 12f);
                    Physics.SyncTransforms();
                    bool jumped = false;
                    int frames = 0;
                    while (player.transform.position.z < head + 3f && !Is(timer, "IsGameOver") && frames++ < 600)
                    {
                        if (!jumped && player.transform.position.z >= head - jumpAtDistance)
                        {
                            Call(player, "RequestJump");
                            jumped = true;
                        }
                        yield return null;
                    }
                    Assert.IsTrue(Is(timer, "IsGameOver"), "Jump from " + jumpAtDistance + " m must not clear the enemy.");
                    foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                        UnityEngine.Object.DestroyImmediate(root);
                    yield return null;
                }
            }
            finally
            {
                Time.captureDeltaTime = oldDelta;
            }
            yield return new ExitPlayMode();
        }
    }
}
