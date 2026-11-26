using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    // First-run help (2026-11-11): the target marker shows while the counter
    // swipe would kill. Checked against the real swipe: made on the first and
    // the last frame the marker shows, it kills; made just before, it doesn't.
    public sealed class RunAssistAcceptanceTests
    {
        private static Type Find(string name) => Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
        private static bool Is(Component target, string property) => (bool)target.GetType().GetProperty(property).GetValue(target);
        private static void Call(Component target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);

        private static Component Rig(bool air, float laneX, float enemyZ, out Component timer, out Component enemy)
        {
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                UnityEngine.Object.DestroyImmediate(root);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.1f, 40f);
            floor.transform.localScale = new Vector3(10f, 0.2f, 120f);
            timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            GameObject player = new GameObject("Player");
            CharacterController body = player.AddComponent<CharacterController>();
            body.center = new Vector3(0f, 1f, 0f);
            body.height = 2f;
            body.radius = 0.5f;
            Component controller = player.AddComponent(Find("PlayerController"));
            string path = air ? "Assets/Prefab/Enemy/EnemyAir.prefab" : "Assets/Prefab/Enemy/Enemy.prefab";
            enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),
                new Vector3(laneX, air ? 2.5f : 0.74f, enemyZ), Quaternion.identity).GetComponent(Find("Enemy"));
            Physics.SyncTransforms();
            return controller;
        }

        // Open now: a swipe on this frame kills (not merely coming up).
        private static bool Window(Component player, Component enemy)
        {
            object[] args = { enemy, 0f };
            return (bool)player.GetType().GetMethod("TryGetKillWindow").Invoke(player, args) && (float)args[1] <= 0f;
        }

        // The "!" (2026-11-26): shown while the window has at most this long left.
        private const float ExclaimLead = 0.45f;

        private static bool Warning(Component player, Component enemy)
        {
            object[] args = { enemy, 0f, 0f };
            return (bool)player.GetType().GetMethod("TryGetKillWindowSpan").Invoke(player, args) &&
                (float)args[2] <= ExclaimLead;
        }

        // Runs past the enemy; swipes on frame `swipeAt` (-1: never).
        // Returns the frames the window was open, and whether the enemy died.
        private static IEnumerator Run(bool air, float laneX, int swipeAt, List<int> openFrames, bool[] killed, bool[] died,
            List<int> warnFrames = null)
        {
            Component player = Rig(air, laneX, 20f, out Component timer, out Component enemy);
            yield return null;
            Call(timer, "BeginRun");
            Call(player, "SetRunForwardSpeed", 12f);
            for (int frame = 0; frame < 240 && !Is(timer, "IsGameOver") && player.transform.position.z < 26f; frame++)
            {
                if (!Is(enemy, "IsKilled") && Window(player, enemy))
                    openFrames?.Add(frame);
                if (!Is(enemy, "IsKilled") && Warning(player, enemy))
                    warnFrames?.Add(frame);
                if (frame == swipeAt)
                    Call(player, air ? "RequestJump" : laneX > 0f ? "RequestMoveRight" : "RequestMoveLeft");
                yield return null;
            }
            killed[0] = Is(enemy, "IsKilled");
            died[0] = Is(timer, "IsGameOver");
        }

        private static IEnumerator Check(bool air, float laneX)
        {
            float oldDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                var open = new List<int>();
                var warn = new List<int>();
                bool[] killed = new bool[1], died = new bool[1];
                yield return Run(air, laneX, -1, open, killed, died, warn);
                Assert.Greater(open.Count, 2, "The marker shows for a while.");
                for (int i = 1; i < open.Count; i++)
                    Assert.AreEqual(open[i - 1] + 1, open[i], "One unbroken window.");
                Debug.Log($"RunAssist window air={air} lane={laneX}: frames {open[0]}..{open[open.Count - 1]}, " +
                    $"\"!\" {(warn.Count > 0 ? warn[0] + ".." + warn[warn.Count - 1] : "never")}");
                // The "!" comes up just before the enemy's attack reaches the runner:
                // still in time to swipe, and no earlier than ExclaimLead before that.
                Assert.Greater(warn.Count, 0, "The \"!\" shows.");
                Assert.LessOrEqual(warn[0], open[open.Count - 1], "…while the swipe still kills.");
                Assert.AreEqual(open[open.Count - 1], warn[warn.Count - 1], "…until the window shuts.");
                Assert.GreaterOrEqual(warn[0], open[open.Count - 1] - Mathf.CeilToInt(ExclaimLead * 60f) - 1,
                    "…and not long before.");

                foreach (int frame in new[] { open[0], open[open.Count / 2], open[open.Count - 1] })
                {
                    yield return Run(air, laneX, frame, null, killed, died);
                    Assert.IsTrue(killed[0], $"A swipe on frame {frame} (window {open[0]}..{open[open.Count - 1]}) kills.");
                    Assert.IsFalse(died[0], $"…and the runner lives (frame {frame}).");
                }
                yield return Run(air, laneX, open[0] - 3, null, killed, died);
                Assert.IsFalse(killed[0] && !died[0] && !air, "Before the window opens the swipe misses.");
                if (air)
                    Assert.IsFalse(killed[0], "Before the window opens the jump misses.");
            }
            finally
            {
                Time.captureDeltaTime = oldDelta;
            }
        }

        [UnityTest]
        public IEnumerator LaneAttackWindowMatchesTheRealSwipeOnTheRight()
        {
            yield return new EnterPlayMode();
            yield return Check(false, 2.5f);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator LaneAttackWindowMatchesTheRealSwipeOnTheLeft()
        {
            yield return new EnterPlayMode();
            yield return Check(false, -2.5f);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator JumpAttackWindowMatchesTheRealSwipe()
        {
            yield return new EnterPlayMode();
            yield return Check(true, 0f);
            yield return new ExitPlayMode();
        }

        [Test]
        public void TheReticleIsDrawn()
        {
            Assert.IsNotNull(Find("FxSprites").GetProperty("Reticle").GetValue(null));
            var alpha = Find("FxSprites").GetMethod("ReticleAlpha", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Func<float, float, float> at = (u, v) => (float)alpha.Invoke(null, new object[] { u, v });
            Assert.Greater(at(0.5f, 0.87f), 0.5f, "Ring on top.");
            Assert.Greater(at(0.5f, 0.95f), 0.5f, "A tick crosses it.");
            Assert.Greater(at(0.95f, 0.5f), 0.5f, "Ticks on the sides.");
            Assert.Less(at(0.5f, 0.5f), 0.05f, "Open centre.");
        }

        // 2026-11-26: the neon halo follows the ring; the "!" is a bar over a dot.
        [Test]
        public void TheNeonHaloAndTheExclaimAreDrawn()
        {
            Type sprites = Find("FxSprites");
            foreach (string name in new[] { "ReticleGlow", "Exclaim", "ExclaimOutline", "ExclaimGlow" })
                Assert.IsNotNull(sprites.GetProperty(name).GetValue(null), name);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            var glow = sprites.GetMethod("ReticleGlowAlpha", flags);
            float span = (float)sprites.GetField("ReticleGlowSpan").GetValue(null);
            Func<float, float, float> halo = (u, v) => (float)glow.Invoke(null, new object[] { u, v });
            float ringV = 0.5f + 0.74f / span * 0.5f;
            Assert.Greater(halo(0.5f, ringV), 0.9f, "Brightest on the ring.");
            Assert.Greater(halo(0.5f, ringV + 0.04f), 0.1f, "…and spreading past it.");
            Assert.Less(halo(0.5f, 0.5f), 0.05f, "Dark in the middle.");
            var shape = sprites.GetMethod("ExclaimShape", flags);
            Func<float, float, float> mark = (u, v) => (float)shape.Invoke(null, new object[] { u, v, 0f });
            Assert.Greater(mark(0.5f, 0.235f), 0.5f, "The dot.");
            Assert.Greater(mark(0.5f, 0.65f), 0.5f, "The bar.");
            Assert.Less(mark(0.5f, 0.335f), 0.1f, "A gap between them.");
            Assert.Less(mark(0.12f, 0.65f), 0.05f, "Narrow.");
        }
    }
}
