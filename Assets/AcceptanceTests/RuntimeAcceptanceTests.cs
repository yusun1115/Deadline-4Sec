using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Deadline4Sec.AcceptanceTests
{
    public sealed class RuntimeAcceptanceTests
    {
        private static Type Find(string name)
        {
            Type type = Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
            Assert.IsNotNull(type, name + " must compile in the game assembly.");
            return type;
        }

        private static object Invoke(object target, string method, params object[] args)
        {
            return target.GetType().GetMethod(method,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(target, args);
        }

        private static bool IsGameOver(Component timer)
        {
            return (bool)timer.GetType().GetProperty("IsGameOver").GetValue(timer);
        }

        [Test]
        public void GroundEnemyRejectsJumpAndSlideKill()
        {
            GameObject gameObject = new GameObject("Enemy");
            Component enemy = gameObject.AddComponent(Find("Enemy"));
            MethodInfo canKill = Find("PlayerController").GetMethod("CanKillWithAttack",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(canKill);
            Assert.IsFalse((bool)canKill.Invoke(null, new object[] { enemy, "Jump Attack" }));
            Assert.IsFalse((bool)canKill.Invoke(null, new object[] { enemy, "Slide Attack" }));
            Assert.IsTrue((bool)canKill.Invoke(null, new object[] { enemy, "Lane Attack" }));
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void PatternCandidatesHaveInternalRewardIntervalsBelowFourSeconds()
        {
            string[] names = {
                "Pattern_A_LaneAttack", "Pattern_B_JumpSlide", "Pattern_C_AirCombo",
                "Pattern_D_Stomp", "Pattern_E_MixedRisk"
            };
            foreach (string name in names)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefab/Pattern/" + name + ".prefab");
                Assert.IsNotNull(prefab, name);
                Component pattern = prefab.GetComponent(Find("CoursePattern"));
                Assert.IsNotNull(pattern, name);
                List<float> positions = new List<float>();
                Invoke(pattern, "CollectOpportunityPositions", positions);
                Assert.IsNotEmpty(positions, name + " has no reward opportunity.");
                Assert.That(positions[0], Is.GreaterThanOrEqualTo(0f));
                for (int i = 1; i < positions.Count; i++)
                    Assert.Less(positions[i] - positions[i - 1], 35.7f,
                        name + " contains a reward gap longer than 3.5 seconds at 10.2 m/s.");
            }
        }

        [UnityTest]
        public IEnumerator ForwardGroundEnemyKillsJumpingPlayer()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            Component player = CreatePlayer(Vector3.zero);
            CreateGroundEnemy(new Vector3(0f, 0.5f, 4f));
            Invoke(timer, "BeginRun");
            yield return null;
            Invoke(player, "RequestJump");
            yield return AdvanceSeconds(0.7f);
            Assert.IsTrue(IsGameOver(timer),
                "Jumping through a front enemy must be fatal. Player z=" +
                player.transform.position.z + ", elapsed=" + Time.time);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ObstacleFrontCollisionKillsPlayer()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            CreatePlayer(Vector3.zero);
            CreateObstacle(new Vector3(0f, 0.5f, 4f));
            Invoke(timer, "BeginRun");
            yield return AdvanceSeconds(0.6f);
            Assert.IsTrue(IsGameOver(timer), "Obstacle front contact must end the run.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ObstacleTopLandingIsSafeAndUnrewarded()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            Component run = new GameObject("Run").AddComponent(Find("RunManager"));
            Component player = CreatePlayer(new Vector3(0f, 1.3f, 3.4f));
            Component obstacle = CreateObstacle(new Vector3(0f, 0.5f, 6f));
            obstacle.transform.localScale = new Vector3(1f, 1f, 5f);
            Invoke(timer, "BeginRun");
            yield return AdvanceSeconds(0.4f);
            Assert.IsFalse(IsGameOver(timer),
                "A top landing must be safe. Player=" + player.transform.position);
            Assert.IsTrue((bool)obstacle.GetType().GetField("physicallyTouched",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obstacle),
                "Test must actually touch the upper face.");
            Assert.AreEqual(0, (int)run.GetType().GetProperty("CurrentScore").GetValue(run),
                "Top contact must not award a Near Miss.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ClosePassAwardsOneNearMiss()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            Component run = new GameObject("Run").AddComponent(Find("RunManager"));
            GameObject canvas = new GameObject("Canvas", typeof(Canvas));
            GameObject scoreObject = new GameObject("Score", typeof(RectTransform), typeof(Text));
            scoreObject.transform.SetParent(canvas.transform, false);
            run.GetType().GetField("scoreText", BindingFlags.NonPublic |
                BindingFlags.Instance).SetValue(run, scoreObject.GetComponent<Text>());
            Component player = CreatePlayer(new Vector3(1.4f, 0f, 0f));
            CreateObstacle(new Vector3(0f, 0.5f, 4f));
            yield return null;
            Invoke(timer, "BeginRun");
            yield return AdvanceSeconds(0.25f);
            Assert.AreEqual(0, (int)run.GetType().GetProperty("CurrentScore").GetValue(run),
                "Entering the Near Miss zone is only a candidate.");
            yield return AdvanceSeconds(0.4f);
            Assert.IsFalse(IsGameOver(timer));
            Assert.Greater(player.transform.position.z, 5f);
            Assert.AreEqual(150, (int)run.GetType().GetProperty("CurrentScore").GetValue(run));
            Assert.AreEqual(1, (int)run.GetType().GetProperty("CurrentCombo").GetValue(run));
            Transform feedback = canvas.transform.Find("NearMissFeedback");
            Assert.IsNotNull(feedback);
            Assert.IsTrue(feedback.gameObject.activeSelf);
            Type tmpType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
            Component feedbackText = feedback.GetComponent(tmpType);
            Assert.That((string)tmpType.GetProperty("text").GetValue(feedbackText),
                Does.Contain("NEAR MISS"));
            yield return AdvanceSeconds(0.8f);
            Assert.IsFalse(feedback.gameObject.activeSelf);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SideLaneAttackKillsOneGroundEnemy()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            Component run = new GameObject("Run").AddComponent(Find("RunManager"));
            Component player = CreatePlayer(Vector3.zero);
            Component enemy = CreateGroundEnemy(new Vector3(2.5f, 0.5f, 4f), true);
            Invoke(timer, "BeginRun");
            yield return null;
            Invoke(player, "RequestMoveRight");
            yield return AdvanceSeconds(0.5f);
            Assert.IsFalse(IsGameOver(timer));
            Assert.IsTrue((bool)enemy.GetType().GetProperty("IsKilled").GetValue(enemy));
            Assert.AreEqual(100, (int)run.GetType().GetProperty("CurrentScore").GetValue(run));
            Assert.AreEqual(1, (int)run.GetType().GetProperty("CurrentCombo").GetValue(run));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator IgnoredSideEnemyKillsPlayer()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            CreatePlayer(Vector3.zero);
            CreateGroundEnemy(new Vector3(2.5f, 0.5f, 4f), true);
            Invoke(timer, "BeginRun");
            yield return AdvanceSeconds(0.6f);
            Assert.IsTrue(IsGameOver(timer));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SufficientlyHighSidePassStaysSafe()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            Component player = CreatePlayer(new Vector3(0f, 6f, 0f));
            CreateGroundEnemy(new Vector3(2.5f, 0.5f, 4f), true);
            Invoke(timer, "BeginRun");
            yield return AdvanceSeconds(0.65f);
            Assert.Greater(player.transform.position.z, 5.5f);
            Assert.IsFalse(IsGameOver(timer),
                "Passing completely above a side attack zone must be safe.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator GroundSlamKillsTwoAndResetsTimerOnce()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            Component run = new GameObject("Run").AddComponent(Find("RunManager"));
            Component player = CreatePlayer(new Vector3(0f, 6.2f, 0f));
            Component left = CreateGroundEnemy(new Vector3(-2.5f, 0.5f, 2f));
            Component right = CreateGroundEnemy(new Vector3(2.5f, 0.5f, 2f));
            Invoke(timer, "BeginRun");
            yield return null;
            Invoke(player, "RequestSlide");
            Assert.IsTrue((bool)player.GetType().GetField("isGroundSlamming",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player));
            yield return AdvanceSeconds(0.3f);
            Assert.IsFalse(IsGameOver(timer));
            Assert.IsTrue((bool)left.GetType().GetProperty("IsKilled").GetValue(left));
            Assert.IsTrue((bool)right.GetType().GetProperty("IsKilled").GetValue(right));
            Assert.AreEqual(2, (int)run.GetType().GetProperty("CurrentCombo").GetValue(run));
            Assert.AreEqual(300, (int)run.GetType().GetProperty("CurrentScore").GetValue(run));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ElevatedFloorUsesRelativeHeightForSlam()
        {
            yield return new EnterPlayMode();
            CreateFloor(5f);
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            Component player = CreatePlayer(new Vector3(0f, 6.2f, 0f));
            Invoke(timer, "BeginRun");
            Invoke(player, "RequestSlide");
            Assert.IsFalse((bool)player.GetType().GetField("isGroundSlamming",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player),
                "World Y=6.2 must not start a slam just 1.2m above the floor.");
            Assert.IsTrue((bool)player.GetType().GetField("isFastFalling",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator InvalidSlamContactDoesNotAwardOtherKills()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            Component run = new GameObject("Run").AddComponent(Find("RunManager"));
            Component player = CreatePlayer(Vector3.zero);
            Component inRange = CreateGroundEnemy(new Vector3(2.5f, 0.5f, 0f));
            Component bodyContact = CreateGroundEnemy(new Vector3(0f, 0.5f, 0f));
            Physics.SyncTransforms();
            Invoke(timer, "BeginRun");
            player.GetType().GetField("isGroundSlamming",
                BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, true);
            Invoke(player, "ResolveEnemyContact", bodyContact.GetComponent<Collider>(), false, false, false);
            Assert.IsTrue(IsGameOver(timer));
            Assert.IsFalse((bool)inRange.GetType().GetProperty("IsKilled").GetValue(inRange));
            Assert.AreEqual(0, (int)run.GetType().GetProperty("CurrentScore").GetValue(run));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator HighSlamHeadLandingStompsWithoutPassingThroughOrAoe()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            Component run = new GameObject("Run").AddComponent(Find("RunManager"));
            Component player = CreatePlayer(new Vector3(0f, 7f, 0f));
            Component head = CreateGroundEnemy(new Vector3(0f, 0.5f, 2f));
            Component nearby = CreateGroundEnemy(new Vector3(2.5f, 0.5f, 2f));
            Physics.SyncTransforms();
            float top = head.GetComponent<Collider>().bounds.max.y;
            Invoke(timer, "BeginRun");
            Invoke(player, "RequestSlide");
            Assert.IsTrue((bool)player.GetType().GetField("isGroundSlamming",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player));
            int frames = 0;
            while (!(bool)head.GetType().GetProperty("IsKilled").GetValue(head) && frames++ < 1000)
            {
                yield return null;
                Assert.IsFalse(IsGameOver(timer));
                Assert.GreaterOrEqual(player.GetComponent<CharacterController>().bounds.min.y, top - 0.01f,
                    "The descending feet must never pass through the head.");
            }
            Assert.Less(frames, 1000);
            Assert.IsFalse((bool)nearby.GetType().GetProperty("IsKilled").GetValue(nearby),
                "Head landing must cancel the floor AOE.");
            Assert.AreEqual(150, (int)run.GetType().GetProperty("CurrentScore").GetValue(run));
            Assert.Greater((float)player.GetType().GetField("verticalSpeed",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player), 0f);
            Assert.IsFalse((bool)player.GetType().GetField("isGroundSlamming",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator LargeSlamStepStopsAtFirstHeadIncludingFootprintEdge()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            Component run = new GameObject("Run").AddComponent(Find("RunManager"));
            Component player = CreatePlayer(new Vector3(0.95f, 7f, 0f));
            Component lower = CreateGroundEnemy(new Vector3(0f, 0.5f, 0f));
            Component upper = CreateGroundEnemy(new Vector3(0f, 3f, 0f));
            Physics.SyncTransforms();
            Invoke(timer, "BeginRun");
            Invoke(player, "RequestSlide");
            Bounds before = player.GetComponent<CharacterController>().bounds;
            Bounds after = before;
            after.center += Vector3.down * 10f;
            float upperTop = upper.GetComponent<Collider>().bounds.max.y;
            Assert.IsTrue((bool)Invoke(player, "TryStompEnemy", before, after));
            Assert.IsTrue((bool)upper.GetType().GetProperty("IsKilled").GetValue(upper));
            Assert.IsFalse((bool)lower.GetType().GetProperty("IsKilled").GetValue(lower));
            Assert.GreaterOrEqual(player.GetComponent<CharacterController>().bounds.min.y, upperTop);
            Assert.AreEqual(150, (int)run.GetType().GetProperty("CurrentScore").GetValue(run));
            Assert.IsFalse(IsGameOver(timer));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ObstacleAboveEnemyHeadStopsSlamWithoutStompOrShockwave()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            Component run = new GameObject("Run").AddComponent(Find("RunManager"));
            Component player = CreatePlayer(new Vector3(0f, 7f, 0f));
            Component enemy = CreateGroundEnemy(new Vector3(0f, 0.5f, 0f));
            Component platform = CreateObstacle(new Vector3(0f, 3f, 0f));
            platform.transform.localScale = new Vector3(4f, 2f, 12f);
            Physics.SyncTransforms();
            Invoke(timer, "BeginRun");
            Invoke(player, "RequestSlide");
            Bounds before = player.GetComponent<CharacterController>().bounds;
            Bounds after = before;
            after.center += Vector3.down * 10f;
            Assert.IsTrue((bool)Invoke(player, "TryStompEnemy", before, after));
            Assert.IsFalse(IsGameOver(timer));
            Assert.IsFalse((bool)enemy.GetType().GetProperty("IsKilled").GetValue(enemy));
            Assert.IsFalse((bool)player.GetType().GetField("isGroundSlamming",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player));
            Assert.IsFalse((bool)player.GetType().GetField("hasGroundSlamImpacted",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player));
            Assert.IsTrue((bool)player.GetType().GetField("isGrounded",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player));
            Assert.GreaterOrEqual(player.GetComponent<CharacterController>().bounds.min.y, 3.9f);
            Assert.AreEqual(0, (int)run.GetType().GetProperty("CurrentScore").GetValue(run));
            yield return AdvanceSeconds(0.1f);
            Assert.IsFalse(IsGameOver(timer));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AirHomingChainsTwoActualPrefabEnemies()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            Component run = new GameObject("Run").AddComponent(Find("RunManager"));
            Component player = CreatePlayer(Vector3.zero);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/Enemy/EnemyAir.prefab");
            Assert.IsNotNull(prefab);
            Component first = UnityEngine.Object.Instantiate(prefab,
                new Vector3(0f, 3f, 4f), Quaternion.identity).GetComponent(Find("Enemy"));
            Component second = UnityEngine.Object.Instantiate(prefab,
                new Vector3(0f, 4f, 8f), Quaternion.identity).GetComponent(Find("Enemy"));
            Invoke(timer, "BeginRun");
            yield return null;
            Invoke(player, "RequestJump");
            yield return null;
            Invoke(player, "RequestJump");
            yield return AdvanceSeconds(0.2f);
            Assert.IsFalse(IsGameOver(timer));
            Assert.IsTrue((bool)first.GetType().GetProperty("IsKilled").GetValue(first));
            Invoke(player, "RequestJump");
            yield return AdvanceSeconds(0.2f);
            Assert.IsFalse(IsGameOver(timer));
            Assert.IsTrue((bool)second.GetType().GetProperty("IsKilled").GetValue(second));
            Assert.AreEqual(2, (int)run.GetType().GetProperty("CurrentCombo").GetValue(run));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AirEnemyDownZoneKillsUnresponsivePlayer()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            CreatePlayer(Vector3.zero);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/Enemy/EnemyAir.prefab");
            UnityEngine.Object.Instantiate(prefab, new Vector3(0f, 3f, 4f), Quaternion.identity);
            Invoke(timer, "BeginRun");
            yield return AdvanceSeconds(0.6f);
            Assert.IsTrue(IsGameOver(timer));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AirJumpWithoutTargetDoesNotDoubleJump()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            Component player = CreatePlayer(Vector3.zero);
            Invoke(timer, "BeginRun");
            yield return null;
            Invoke(player, "RequestJump");
            yield return null;
            FieldInfo vertical = player.GetType().GetField("verticalSpeed",
                BindingFlags.NonPublic | BindingFlags.Instance);
            float before = (float)vertical.GetValue(player);
            Invoke(player, "RequestJump");
            Assert.IsFalse((bool)player.GetType().GetField("isHomingDash",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player));
            Assert.That((float)vertical.GetValue(player), Is.EqualTo(before).Within(0.001f));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator FastFallHeadContactStompsBeforeEnemyAttack()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            new GameObject("Run").AddComponent(Find("RunManager"));
            Component player = CreatePlayer(new Vector3(0f, 2.5f, 3f));
            Component enemy = CreateGroundEnemy(new Vector3(0f, 0.5f, 4f));
            Invoke(timer, "BeginRun");
            Invoke(player, "RequestSlide");
            yield return AdvanceSeconds(0.2f);
            Assert.IsFalse(IsGameOver(timer), "A valid fast fall head hit must beat the attack zone.");
            Assert.IsTrue((bool)enemy.GetType().GetProperty("IsKilled").GetValue(enemy));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator MissedFastFallDoesNotIgnoreSideAttack()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            Component player = CreatePlayer(new Vector3(-1.8f, 2.5f, 3f));
            CreateGroundEnemy(new Vector3(0f, 0.5f, 4f), true);
            Invoke(timer, "BeginRun");
            Invoke(player, "RequestSlide");
            yield return AdvanceSeconds(0.2f);
            Assert.IsTrue(IsGameOver(timer),
                "A fast fall outside the stomp footprint must not grant immunity.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ForwardGroundEnemyKillsSlidingPlayer()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            Component player = CreatePlayer(Vector3.zero);
            CreateGroundEnemy(new Vector3(0f, 0.5f, 4f));
            Invoke(timer, "BeginRun");
            yield return null;
            Invoke(player, "RequestSlide");
            yield return AdvanceSeconds(0.6f);
            Assert.IsTrue(IsGameOver(timer));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator FourSecondTimerAndComboAreIndependent()
        {
            yield return new EnterPlayMode();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            Component run = new GameObject("Run").AddComponent(Find("RunManager"));
            GameObject enemyObject = new GameObject("Enemy");
            Component enemy = enemyObject.AddComponent(Find("Enemy"));
            Invoke(timer, "BeginRun");
            yield return null;
            Invoke(timer, "ResetTimer");
            Invoke(timer, "Tick", 1.5f);
            Assert.That((float)timer.GetType().GetProperty("RemainingTime").GetValue(timer),
                Is.EqualTo(2.5f).Within(0.001f));
            for (int i = 0; i < 5; i++)
                Invoke(run, "RecordEnemyKill", enemy, false);
            Assert.AreEqual(600, (int)run.GetType().GetProperty("CurrentScore").GetValue(run));
            Assert.AreEqual(5, (int)run.GetType().GetProperty("CurrentCombo").GetValue(run));
            for (int i = 0; i < 5; i++) Invoke(run, "RecordEnemyKill", enemy, false);
            Assert.AreEqual(1700, (int)run.GetType().GetProperty("CurrentScore").GetValue(run));
            Assert.AreEqual(3, (int)run.GetType().GetProperty("ScoreMultiplier").GetValue(run));
            for (int i = 0; i < 10; i++) Invoke(run, "RecordEnemyKill", enemy, false);
            Assert.AreEqual(4800, (int)run.GetType().GetProperty("CurrentScore").GetValue(run));
            Assert.AreEqual(20, (int)run.GetType().GetProperty("CurrentCombo").GetValue(run));
            Assert.AreEqual(4, (int)run.GetType().GetProperty("ScoreMultiplier").GetValue(run));
            Assert.That((float)timer.GetType().GetProperty("RemainingTime").GetValue(timer),
                Is.EqualTo(2.5f).Within(0.001f), "Score must not change survival time.");
            Invoke(timer, "ResetTimer");
            Assert.AreEqual(4f, (float)timer.GetType().GetProperty("RemainingTime").GetValue(timer));
            Invoke(timer, "Tick", 4f);
            Assert.IsTrue(IsGameOver(timer));
            Invoke(run, "RecordEnemyKill", enemy, false);
            Assert.AreEqual(4800, (int)run.GetType().GetProperty("CurrentScore").GetValue(run),
                "Scoring must stop after Game Over.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator TimerShowsNormalWarningAndCriticalColors()
        {
            yield return new EnterPlayMode();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            GameObject label = new GameObject("Timer Label", typeof(RectTransform), typeof(Text));
            Text display = label.GetComponent<Text>();
            timer.GetType().GetField("timeText", BindingFlags.NonPublic |
                BindingFlags.Instance).SetValue(timer, display);
            Invoke(timer, "BeginRun");
            Assert.AreEqual("4.00", display.text);
            Color normal = display.color;
            yield return null;
            Invoke(timer, "ResetTimer");
            Invoke(timer, "Tick", 1.6f);
            Color warning = display.color;
            Invoke(timer, "Tick", 1.5f);
            Color critical = display.color;
            Assert.AreNotEqual(normal, warning);
            Assert.AreNotEqual(warning, critical);
            Assert.IsFalse(IsGameOver(timer));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DistanceTracksForwardOnlyAndSpeedRises()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            Component player = CreatePlayer(Vector3.zero);
            ((Behaviour)player).enabled = false;
            Component run = new GameObject("Run").AddComponent(Find("RunManager"));
            yield return null;
            Invoke(timer, "BeginRun");
            ((Behaviour)player).enabled = true;
            yield return AdvanceSeconds(0.4f);
            Assert.That((float)run.GetType().GetProperty("Distance").GetValue(run),
                Is.EqualTo(player.transform.position.z).Within(0.5f));
            float baseSpeed = (float)run.GetType().GetProperty("BaseForwardSpeed").GetValue(run);
            Assert.Greater((float)run.GetType().GetProperty("CurrentForwardSpeed").GetValue(run),
                baseSpeed);
            Invoke(player, "RequestMoveRight");
            yield return AdvanceSeconds(0.1f);
            Assert.Greater(player.transform.position.x, 0.5f);
            Assert.That((float)run.GetType().GetProperty("Distance").GetValue(run),
                Is.EqualTo(player.transform.position.z).Within(0.5f),
                "Lane movement must not add to distance.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator EasySpawnerDoesNotRepeatPatternOrLoseRewards()
        {
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            new GameObject("Run").AddComponent(Find("RunManager"));
            CreatePlayer(Vector3.zero);
            Component spawner = new GameObject("Spawner").AddComponent(Find("PatternSpawner"));
            string[] names = {
                "Pattern_A_LaneAttack", "Pattern_B_JumpSlide", "Pattern_C_AirCombo",
                "Pattern_D_Stomp", "Pattern_E_MixedRisk"
            };
            Array prefabs = Array.CreateInstance(Find("CoursePattern"), names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefab/Pattern/" + names[i] + ".prefab");
                prefabs.SetValue(prefab.GetComponent(Find("CoursePattern")), i);
            }
            spawner.GetType().GetField("patternPrefabs",
                BindingFlags.NonPublic | BindingFlags.Instance).SetValue(spawner, prefabs);
            Assert.AreEqual(0, spawner.transform.childCount,
                "Title must wait for an explicit Start/intro preparation.");
            Invoke(timer, "BeginRun");
            yield return null;
            Assert.AreEqual(4, spawner.transform.childCount);
            string previous = null;
            for (int i = 0; i < spawner.transform.childCount; i++)
            {
                Transform child = spawner.transform.GetChild(i);
                Component pattern = child.GetComponent(Find("CoursePattern"));
                Assert.AreEqual("Easy", pattern.GetType().GetProperty("Difficulty").GetValue(pattern).ToString());
                Assert.AreNotEqual(previous, child.name);
                previous = child.name;
            }


            FieldInfo previousPrefab = spawner.GetType().GetField("previousPrefab",
                BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo previousCategory = spawner.GetType().GetField("previousCategory",
                BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo categoryStreak = spawner.GetType().GetField("categoryStreak",
                BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo candidates = spawner.GetType().GetField("candidates",
                BindingFlags.NonPublic | BindingFlags.Instance);
            for (int previousIndex = 0; previousIndex < names.Length; previousIndex++)
            {
                Component preceding = (Component)prefabs.GetValue(previousIndex);
                previousPrefab.SetValue(spawner, preceding);
                previousCategory.SetValue(spawner,
                    preceding.GetType().GetProperty("Category").GetValue(preceding));
                categoryStreak.SetValue(spawner, 2);
                int firstTier = previousIndex < 2 ? 0 : 1;
                for (int tier = firstTier; tier <= 2; tier++)
                {
                    int min = tier == 2 ? 1 : 0;
                    Invoke(spawner, "CollectCandidates", min, tier, true);
                    ICollection available = (ICollection)candidates.GetValue(spawner);
                    Assert.Greater(available.Count, 0,
                        names[previousIndex] + " leaves no pattern in tier " + tier);
                }
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SwipeRunsOnceAndNonButtonHudDoesNotBlockIt()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            yield return new EnterPlayMode();
            CreateFloor();
            Component timer = new GameObject("Timer").AddComponent(Find("GameTimer"));
            Component player = CreatePlayer(Vector3.zero);
            Component input = new GameObject("Swipe").AddComponent(Find("MobileSwipeInput"));
            ((Behaviour)input).enabled = false;
            new GameObject("EventSystem", typeof(EventSystem));
            GameObject canvas = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            GameObject button = CreateUiImage(canvas.transform, new Vector2(0.25f, 0.5f));
            button.AddComponent<Button>();
            CreateUiImage(canvas.transform, new Vector2(0.75f, 0.5f));
            yield return null;
            Vector2 buttonPoint = new Vector2(Screen.width * 0.25f, Screen.height * 0.5f);
            Vector2 hudPoint = new Vector2(Screen.width * 0.75f, Screen.height * 0.5f);
            MethodInfo overUi = input.GetType().GetMethod("IsPointerOverUI",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsTrue((bool)overUi.Invoke(null, new object[] { buttonPoint }));
            Assert.IsFalse((bool)overUi.Invoke(null, new object[] { hudPoint }));

            Invoke(timer, "BeginRun");
            Invoke(input, "BeginTouch", 1, Vector2.zero);
            Invoke(input, "EvaluateSwipe", new Vector2(100f, 0f));
            Assert.AreEqual(1, player.GetType().GetField("laneIndex",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player));
            yield return null;
            Invoke(input, "EvaluateSwipe", new Vector2(200f, 0f));
            Assert.AreEqual(1, player.GetType().GetField("laneIndex",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player));
            Invoke(input, "EndTouch");
            Invoke(input, "BeginTouch", 2, Vector2.zero);
            Invoke(input, "EvaluateSwipe", new Vector2(-100f, 0f));
            Assert.AreEqual(0, player.GetType().GetField("laneIndex",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player));
            yield return new ExitPlayMode();
        }

        private static void CreateFloor(float topY = 0f)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, topY - 0.1f, 0f);
            floor.transform.localScale = new Vector3(10f, 0.2f, 100f);
            Physics.SyncTransforms();
        }

        private static IEnumerator AdvanceSeconds(float seconds)
        {
            float end = Time.time + seconds;
            int frames = 0;
            while (Time.time < end && frames++ < 10000)
                yield return null;
            Assert.Less(frames, 10000, "Play mode simulation did not advance.");
        }

        private static Component CreatePlayer(Vector3 position)
        {
            GameObject player = new GameObject("Player");
            player.transform.position = position;
            CharacterController body = player.AddComponent<CharacterController>();
            body.center = new Vector3(0f, 1f, 0f);
            body.height = 2f;
            body.radius = 0.5f;
            return player.AddComponent(Find("PlayerController"));
        }

        private static Component CreateGroundEnemy(Vector3 position, bool sideZone = false)
        {
            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            enemy.name = "Enemy";
            enemy.transform.position = position;
            enemy.GetComponent<BoxCollider>().isTrigger = true;
            enemy.AddComponent(Find("Enemy"));

            GameObject front = new GameObject("FrontAttackZone");
            front.transform.SetParent(enemy.transform, false);
            front.transform.localPosition = new Vector3(0f, 0.5f, -1.25f);
            BoxCollider zone = front.AddComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.size = new Vector3(1.4f, 3f, 2.5f);
            front.AddComponent(Find("EnemyAttackZone"));
            if (sideZone)
            {
                GameObject side = new GameObject("LeftSideAttackZone");
                side.transform.SetParent(enemy.transform, false);
                side.transform.localPosition = new Vector3(-1.75f, 0.25f, 0f);
                BoxCollider sideCollider = side.AddComponent<BoxCollider>();
                sideCollider.isTrigger = true;
                sideCollider.size = new Vector3(2.5f, 1.5f, 3f);
                Component attackZone = side.AddComponent(Find("EnemyAttackZone"));
                attackZone.GetType().GetField("zoneType",
                    BindingFlags.NonPublic | BindingFlags.Instance).SetValue(attackZone,
                    Enum.ToObject(Find("EnemyAttackZone").GetNestedType("AttackZoneType"), 1));
            }
            return enemy.GetComponent(Find("Enemy"));
        }

        private static Component CreateObstacle(Vector3 position)
        {
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "Obstacle";
            obstacle.transform.position = position;
            return obstacle.AddComponent(Find("Obstacle"));
        }

        private static GameObject CreateUiImage(Transform parent, Vector2 anchor)
        {
            GameObject image = new GameObject("UI Image", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(parent, false);
            RectTransform rect = image.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.sizeDelta = new Vector2(80f, 80f);
            rect.anchoredPosition = Vector2.zero;
            return image;
        }
    }
}
