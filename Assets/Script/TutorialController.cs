using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    [DefaultExecutionOrder(250)]
    public sealed class TutorialController : MonoBehaviour
    {
        public enum LessonState { Inactive, Preparing, Active, Success, Failed, Complete }
        public enum LessonInput { None, Left, Right, Up, Down }
        private static readonly string[] Titles = {
            "LANE MOVE", "JUMP", "SLIDE", "LANE ATTACK", "4-SECOND TIMER",
            "NEAR MISS", "AIR HOMING", "FAST FALL", "STOMP", "GROUND SLAM"
        };
        private static readonly string[] Instructions = {
            "Swipe left, then right.",
            "Swipe up to jump over the block.",
            "Swipe down to slide under the bar.",
            "Swipe toward the enemy to attack.",
            "Watch the timer. A kill resets it to 4.00.",
            "Swipe left. Pass close without touching the block.",
            "Jump, then swipe up in the air to homing attack.",
            "Jump, then swipe down in the air to fall faster.",
            "Homing up, then swipe down onto the enemy's head.",
            "Homing higher, then slam the empty floor with down."
        };

        private GameFlowManager flow;
        private PlayerController player;
        private GameTimer timer;
        private RunManager run;
        private CameraFeedbackController feedback;
        private TutorialCourseAssets assets;
        private Vector3 startPose;
        private GameObject floor, stageRoot, panel, retryButton, continueButton;
        private TMP_Text heading, instruction, cue;
        private readonly List<Enemy> enemies = new List<Enemy>();
        private Obstacle obstacle;
        private bool left, right, leftLaneReached, jumped, slid, fastFall, laneKill, nearMiss, stomp, slam;
        private bool jumpCoveredObstacle, slideCoveredObstacle, recoveredLowTimer;
        private int airKills, slamKills;

        public LessonState State { get; private set; }
        public int StepIndex { get; private set; }
        public int CompletedSteps { get; private set; }
        public int FailureCount { get; private set; }
        public LessonInput PromptInput { get; private set; }
        public bool CanReceiveInput => State == LessonState.Active;
        public bool IsFeedbackActive => State == LessonState.Active || State == LessonState.Success;
        public Enemy NextEnemy
        {
            get
            {
                foreach (Enemy enemy in enemies)
                    if (enemy != null && !enemy.IsKilled)
                        return enemy;
                return null;
            }
        }

        public void Begin(GameFlowManager owner, PlayerController runner, GameTimer clock,
            RunManager stats, PatternSpawner spawner, Canvas canvas, TutorialCourseAssets course)
        {
            flow = owner;
            player = runner;
            timer = clock;
            run = stats;
            assets = course;
            feedback = FindFirstObjectByType<CameraFeedbackController>();
            startPose = player.transform.position;
            StepIndex = CompletedSteps = FailureCount = 0;
            if (spawner != null)
                spawner.enabled = false;
            // Test/graybox scenes may have manually placed actors. A reload
            // restores these after practice; the production scene starts empty.
            foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
                enemy.gameObject.SetActive(false);
            foreach (Obstacle block in FindObjectsByType<Obstacle>(FindObjectsSortMode.None))
                block.gameObject.SetActive(false);
            player.ActionPerformed += OnAction;
            player.EnemyKilled += OnKill;
            player.SlamResolved += OnSlam;
            timer.TimerReset += OnTimerReset;
            run.NearMissRecorded += OnNearMiss;
            CreateUI(canvas);
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Tutorial Floor";
            floor.transform.position = new Vector3(startPose.x, -0.1f, startPose.z + 30f);
            floor.transform.localScale = new Vector3(8.6f, 0.2f, 90f);
            floor.GetComponent<Renderer>().sharedMaterial = assets.floorMaterial;
            PrepareStep();
        }

        private void PrepareStep()
        {
            State = LessonState.Preparing;
            PromptInput = LessonInput.None;
            timer.PausePractice();
            if (feedback != null)
                feedback.StopAllFeedback();
            ClearStage();
            player.ResetPracticePose(startPose, StepIndex == 5 ? 1 : 0);
            run.BeginPractice(StepIndex >= 8 ? 12f : 6f);
            left = right = jumped = slid = fastFall = laneKill = nearMiss = stomp = slam = false;
            leftLaneReached = false;
            jumpCoveredObstacle = slideCoveredObstacle = recoveredLowTimer = false;
            airKills = slamKills = 0;
            retryButton.SetActive(false);
            continueButton.SetActive(false);
            heading.text = (StepIndex + 1) + "/10  " + Titles[StepIndex];
            instruction.text = Instructions[StepIndex];
            cue.text = "GET READY";
            stageRoot = new GameObject("Tutorial Step " + (StepIndex + 1));
            switch (StepIndex)
            {
                case 1: AddObstacle(assets.jumpObstacle, 0f, 0.38f, 8f); break;
                case 2: AddObstacle(assets.slideObstacle, 0f, 2.56f, 8f); break;
                case 3: AddEnemy(assets.groundEnemy, 2.5f, 0.74f, 7f); break;
                case 4: AddEnemy(assets.groundEnemy, 2.5f, 0.74f, 20f); break;
                case 5: AddObstacle(assets.laneObstacle, 1.9f, 1.02f, 8f); break;
                case 6:
                    AddEnemy(assets.airEnemy, 0f, 2.4f, 6f);
                    AddEnemy(assets.airEnemy, 2.5f, 3.5f, 12f);
                    break;
                case 8:
                    AddEnemy(assets.airEnemy, 0f, 2.4f, 6f);
                    AddEnemy(assets.airEnemy, -2.5f, 6f, 11.5f);
                    AddEnemy(assets.groundEnemy, -2.5f, 0.74f, 15.5f);
                    break;
                case 9:
                    AddEnemy(assets.airEnemy, 0f, 2.4f, 6f);
                    AddEnemy(assets.airEnemy, 0f, 5.5f, 12f);
                    AddEnemy(assets.airEnemy, 0f, 8.5f, 18f);
                    // Side targets can be hit by the shockwave without crossing
                    // a head or entering a ground enemy's frontal/side attack.
                    AddEnemy(assets.airEnemy, -2.5f, 2.2f, 21.5f);
                    AddEnemy(assets.airEnemy, 2.5f, 2.2f, 21.5f);
                    break;
            }
            Physics.SyncTransforms();
            StartCoroutine(ActivateStep());
        }

        private IEnumerator ActivateStep()
        {
            yield return new WaitForSecondsRealtime(0.45f);
            while (flow != null && flow.IsSkipConfirmationOpen)
                yield return null;
            if (State != LessonState.Preparing)
                yield break;
            run.BeginPractice(StepIndex >= 8 ? 12f : 6f);
            timer.BeginPractice();
            State = LessonState.Active;
            player.enabled = true;
        }

        private void AddEnemy(GameObject prefab, float x, float y, float z)
        {
            GameObject actor = Instantiate(prefab, startPose + new Vector3(x, y - startPose.y, z),
                Quaternion.identity, stageRoot.transform);
            enemies.Add(actor.GetComponent<Enemy>());
        }

        private void AddObstacle(GameObject prefab, float x, float y, float z)
        {
            GameObject actor = Instantiate(prefab, startPose + new Vector3(x, y - startPose.y, z),
                Quaternion.identity, stageRoot.transform);
            obstacle = actor.GetComponent<Obstacle>();
        }

        private void LateUpdate()
        {
            if (State != LessonState.Active || flow.IsSkipConfirmationOpen)
                return;
            Bounds body = player.GetComponent<CharacterController>().bounds;
            if (StepIndex == 0 && left && player.LaneIndex == -1 &&
                Mathf.Abs(player.transform.position.x - (startPose.x - 2.5f)) < 0.03f)
                leftLaneReached = true;
            bool passed = obstacle != null && body.min.z > obstacle.GetComponent<Collider>().bounds.max.z;
            if (obstacle != null && Mathf.Abs(obstacle.transform.position.z - player.transform.position.z) < 1.5f)
            {
                jumpCoveredObstacle |= jumped && body.min.y >= obstacle.GetComponent<Collider>().bounds.max.y - 0.12f;
                slideCoveredObstacle |= slid && player.IsSliding;
            }
            bool done = false;
            switch (StepIndex)
            {
                case 0: done = leftLaneReached && right && player.LaneIndex == 0 &&
                    Mathf.Abs(player.transform.position.x - startPose.x) < 0.03f; break;
                case 1: done = jumpCoveredObstacle && passed; break;
                case 2: done = slideCoveredObstacle && passed; break;
                case 3: done = laneKill; break;
                case 4: done = laneKill && recoveredLowTimer; break;
                case 5: done = left && nearMiss; break;
                case 6: done = airKills >= 2; break;
                case 7: done = fastFall && player.IsGrounded; break;
                case 8: done = stomp; break;
                case 9: done = slam && slamKills >= 2; break;
            }
            if (done)
            {
                CompleteStep();
                return;
            }
            UpdateCue(body);
        }

        private void UpdateCue(Bounds body)
        {
            PromptInput = LessonInput.None;
            if (StepIndex == 0)
                PromptInput = !left ? LessonInput.Left : leftLaneReached && !right ? LessonInput.Right : LessonInput.None;
            else if (StepIndex == 1 || StepIndex == 2)
            {
                float dz = obstacle.transform.position.z - player.transform.position.z;
                if (dz <= 3.2f && dz > -0.5f && player.IsGrounded &&
                    (StepIndex == 1 ? !jumped : !slid))
                    PromptInput = StepIndex == 1 ? LessonInput.Up : LessonInput.Down;
            }
            else if (StepIndex == 3 || StepIndex == 4)
            {
                if (NextEnemy != null && NextEnemy.transform.position.z - player.transform.position.z <= 2.6f)
                    PromptInput = LessonInput.Right;
            }
            else if (StepIndex == 5)
                PromptInput = !left ? LessonInput.Left : LessonInput.None;
            else if (StepIndex == 7)
                PromptInput = player.IsGrounded && !jumped ? LessonInput.Up :
                    !player.IsGrounded && body.min.y > 0.6f && !fastFall ? LessonInput.Down : LessonInput.None;
            else if (!player.IsHoming)
            {
                int climbKills = StepIndex == 9 ? 3 : 2;
                if (airKills < climbKills && NextEnemy != null)
                {
                    if (player.IsGrounded)
                    {
                        if (NextEnemy.transform.position.z - player.transform.position.z <= 4.2f)
                            PromptInput = LessonInput.Up;
                    }
                    else if (Vector3.Distance(body.center, NextEnemy.transform.position) <= 9.5f)
                        PromptInput = LessonInput.Up;
                }
                else if (StepIndex == 8 && NextEnemy != null &&
                    NextEnemy.transform.position.z - player.transform.position.z <= 3.4f && !player.IsFastFalling)
                    PromptInput = LessonInput.Down;
                else if (StepIndex == 9 && !slam && !player.IsFastFalling)
                    PromptInput = LessonInput.Down;
            }
            cue.text = PromptInput == LessonInput.None ? "WATCH THE COURSE" :
                "SWIPE " + PromptInput.ToString().ToUpperInvariant() + " NOW";
#if UNITY_EDITOR || UNITY_STANDALONE
            if (PromptInput != LessonInput.None)
                cue.text += PromptInput == LessonInput.Left ? "  (A / LEFT)" :
                    PromptInput == LessonInput.Right ? "  (D / RIGHT)" :
                    PromptInput == LessonInput.Up ? "  (SPACE / UP)" : "  (S / DOWN)";
#endif
        }

        private void OnAction(PlayerController.PlayerAction action)
        {
            if (!CanReceiveInput)
                return;
            left |= action == PlayerController.PlayerAction.Left;
            right |= action == PlayerController.PlayerAction.Right && (StepIndex != 0 || leftLaneReached);
            jumped |= action == PlayerController.PlayerAction.Jump;
            slid |= action == PlayerController.PlayerAction.Slide;
            fastFall |= action == PlayerController.PlayerAction.FastFall;
        }

        private void OnKill(Enemy enemy, string attack)
        {
            if (!CanReceiveInput || !enemies.Contains(enemy))
                return;
            airKills += enemy.Type == Enemy.EnemyType.Air ? 1 : 0;
            laneKill |= attack == "Lane Attack";
            stomp |= attack == "Stomp Attack" && enemy.Type == Enemy.EnemyType.Ground;
        }

        private void OnSlam(int kills)
        {
            if (!CanReceiveInput)
                return;
            slam = true;
            slamKills = kills;
        }

        private void OnTimerReset(float before)
        {
            if (CanReceiveInput)
                recoveredLowTimer |= before <= 2.5f;
        }

        private void OnNearMiss()
        {
            if (CanReceiveInput)
                nearMiss = true;
        }

        private void CompleteStep()
        {
            State = LessonState.Success;
            CompletedSteps++;
            PromptInput = LessonInput.None;
            timer.PausePractice();
            cue.text = StepIndex == 4 ? "NICE! TIMER RESET TO 4.00" : "NICE!";
            StartCoroutine(AdvanceStep());
        }

        private IEnumerator AdvanceStep()
        {
            yield return new WaitForSecondsRealtime(0.65f);
            while (flow != null && flow.IsSkipConfirmationOpen)
                yield return null;
            if (State != LessonState.Success)
                yield break;
            if (++StepIndex < Titles.Length)
                PrepareStep();
            else
            {
                State = LessonState.Complete;
                player.enabled = false;
                if (feedback != null)
                    feedback.StopAllFeedback();
                heading.text = "TUTORIAL COMPLETE";
                instruction.text = "Kills and Near Miss reset your 4-second timer.\nRisk to live!";
                cue.text = "READY FOR A RUN?";
                continueButton.SetActive(true);
                PlayerPrefs.SetInt("Deadline4Sec.TutorialCompleted", 1);
                PlayerPrefs.Save();
            }
        }

        public void HandleFailure()
        {
            if (State != LessonState.Active)
                return;
            State = LessonState.Failed;
            FailureCount++;
            PromptInput = LessonInput.None;
            player.enabled = false;
            timer.PausePractice();
            if (feedback != null)
            {
                feedback.StopAllFeedback();
                feedback.PlayGameOver();
            }
            cue.text = "TRY THIS STEP AGAIN";
            retryButton.SetActive(true);
        }

        public void RetryStep()
        {
            if (State == LessonState.Failed)
                PrepareStep();
        }

        public void ContinueToRun()
        {
            if (State == LessonState.Complete)
                flow.FinishTutorial(true);
        }

        private void CreateUI(Canvas canvas)
        {
            panel = new GameObject("TutorialHUD", typeof(RectTransform));
            panel.transform.SetParent(canvas.transform, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            heading = TopText("", 170f, 60f, 32f);
            instruction = TopText("", 235f, 108f, 36f);
            cue = TopText("GET READY", 350f, 85f, 30f);
            cue.color = new Color(1f, 0.75f, 0.2f);
            TMP_Text retry = flow.CreateMenuButton(panel.transform, "TRY AGAIN", Vector2.zero,
                new Vector2(360f, 80f), RetryStep);
            retryButton = retry.transform.parent.gameObject;
            BottomButton(retryButton, new Vector2(0.5f, 0f), new Vector2(0f, 60f));
            TMP_Text next = flow.CreateMenuButton(panel.transform, "START RUN", Vector2.zero,
                new Vector2(360f, 80f), ContinueToRun);
            continueButton = next.transform.parent.gameObject;
            BottomButton(continueButton, new Vector2(0.5f, 0f), new Vector2(0f, 60f));
            TMP_Text menu = flow.CreateMenuButton(panel.transform, "MENU", Vector2.zero,
                new Vector2(180f, 65f), () => flow.FinishTutorial(false));
            BottomButton(menu.transform.parent.gameObject, Vector2.zero, new Vector2(115f, 60f));
        }

        private TMP_Text TopText(string text, float offset, float height, float size)
        {
            TMP_Text label = GameFlowManager.CreateMenuText(panel.transform, text,
                Vector2.zero, new Vector2(700f, height), size);
            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -offset);
            return label;
        }

        private static void BottomButton(GameObject button, Vector2 anchor, Vector2 position)
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
        }

        private void ClearStage()
        {
            enemies.Clear();
            obstacle = null;
            if (stageRoot != null)
            {
                stageRoot.SetActive(false);
                Destroy(stageRoot);
            }
        }

        public void Stop()
        {
            StopAllCoroutines();
            State = LessonState.Inactive;
            if (player != null)
            {
                player.ActionPerformed -= OnAction;
                player.EnemyKilled -= OnKill;
                player.SlamResolved -= OnSlam;
                player.enabled = false;
            }
            if (timer != null)
            {
                timer.TimerReset -= OnTimerReset;
                timer.PausePractice();
            }
            if (run != null)
                run.NearMissRecorded -= OnNearMiss;
            ClearStage();
            if (floor != null)
                Destroy(floor);
            if (panel != null)
                Destroy(panel);
        }

        private void OnDestroy() => Stop();
    }
}
