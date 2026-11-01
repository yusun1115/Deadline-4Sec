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
        // Briefing (2026-11-01): the InfoOnly steps the speaker explains before the lessons.
        public enum LessonState { Inactive, Preparing, Active, Success, Failed, Complete, Briefing }
        public enum LessonInput { None, Left, Right, Up, Down }
        public const int LessonCount = 10;
        // Near Miss lesson: the gap (m) to the block when the dodge is asked for.
        // At the practice speed (6 m/s) a dodge counts from ~0.45 to ~1.0 m.
        private const float NearMissCueDistance = 0.75f;

        private GameFlowManager flow;
        private PlayerController player;
        private GameTimer timer;
        private RunManager run;
        private CameraFeedbackController feedback;
        private TutorialCourseAssets assets;
        private Vector3 startPose;
        private GameObject floor, stageRoot, panel, continueButton;
        private TutorialUIController ui;
        private TutorialStep[] briefing, lessons;
        private bool briefingAdvance, followUpShown;
        private readonly List<Enemy> enemies = new List<Enemy>();
        private Obstacle obstacle;
        private bool left, right, leftLaneReached, jumped, slid, fastFall, laneKill, nearMiss, stomp, slam;
        private bool jumpCoveredObstacle, slideCoveredObstacle, recoveredLowTimer;
        private int airKills, slamKills;
        private float missedFor, releasedAt = float.NegativeInfinity;
        private LessonInput releasedInput;
        private bool releasedGrounded;

        public LessonState State { get; private set; }
        public int StepIndex { get; private set; }
        public int CompletedSteps { get; private set; }
        public int FailureCount { get; private set; }
        public LessonInput PromptInput { get; private set; }
        // The step shown now (a briefing step, then the lesson at StepIndex).
        public TutorialStep CurrentStep { get; private set; }
        public int BriefingIndex { get; private set; }
        public TutorialUIController UI => ui;
        // The game is stopped until the swipe asked for (PromptInput) comes.
        public bool IsHoldingForInput { get; private set; }
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
            TutorialStep[] steps = assets.ResolveSteps(LessonCount);
            int briefingCount = steps.Length - LessonCount;
            briefing = new TutorialStep[briefingCount];
            lessons = new TutorialStep[LessonCount];
            System.Array.Copy(steps, briefing, briefingCount);
            System.Array.Copy(steps, briefingCount, lessons, 0, LessonCount);
            CreateUI(canvas);
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Tutorial Floor";
            floor.transform.position = new Vector3(startPose.x, -0.1f, startPose.z + 30f);
            floor.transform.localScale = new Vector3(8.6f, 0.2f, 90f);
            floor.GetComponent<Renderer>().sharedMaterial = assets.floorMaterial;
            if (briefing.Length > 0)
                StartCoroutine(RunBriefing());
            else
                PrepareStep();
        }

        // The rules first: the speaker explains the real (paused, full) timer
        // while the rest of the screen is dimmed. A tap or a short wait goes on.
        private IEnumerator RunBriefing()
        {
            State = LessonState.Briefing;
            PromptInput = LessonInput.None;
            player.ResetPracticePose(startPose, 0);
            timer.BeginPractice();
            timer.PausePractice(); // shows a full 4.00
            for (BriefingIndex = 0; BriefingIndex < briefing.Length; BriefingIndex++)
            {
                TutorialStep step = CurrentStep = briefing[BriefingIndex];
                briefingAdvance = false;
                ui.ShowDialogue(step.title, step.dialogueText);
                ui.SetHighlight(step.highlightTarget, step.dimBackground);
                ui.SetGuide(LessonInput.None, null);
                ui.SetTapToContinue(true, () => briefingAdvance = true);
                float readySince = -1f;
                while (!briefingAdvance)
                {
                    if (ui.IsTyping || (flow != null && flow.IsSkipConfirmationOpen))
                        readySince = -1f;
                    else if (readySince < 0f)
                        readySince = Time.realtimeSinceStartup;
                    else if (step.autoAdvance && Time.realtimeSinceStartup - readySince >= step.nextDelay)
                        break;
                    yield return null;
                }
            }
            ui.SetTapToContinue(false, null);
            PrepareStep();
        }

        // Same as tapping the screen during the briefing.
        public void AdvanceBriefing()
        {
            if (State == LessonState.Briefing && ui != null)
                ui.Tap();
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
            missedFor = 0f;
            ReleaseHold();
            continueButton.SetActive(false);
            CurrentStep = lessons[StepIndex];
            followUpShown = false;
            ui.ShowDialogue(LessonTitle, CurrentStep.dialogueText);
            ui.SetHighlight(CurrentStep.highlightTarget, CurrentStep.dimBackground);
            ui.SetGuide(LessonInput.None, null);
            stageRoot = new GameObject("Tutorial Step " + (StepIndex + 1));
            switch (StepIndex)
            {
                case 1: AddObstacle(assets.jumpObstacle, 0f, 0.38f, 8f); break;
                case 2: AddObstacle(assets.slideObstacle, 0f, 2.56f, 8f); break;
                case 3: AddEnemy(assets.groundEnemy, 2.5f, 0.74f, 7f); break;
                case 4: AddEnemy(assets.groundEnemy, 2.5f, 0.74f, 20f); break;
                // Right in the runner's lane: only a last-moment dodge is a Near Miss.
                case 5: AddObstacle(assets.laneObstacle, 2.5f, 1.02f, 8f); break;
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
            if (IsHoldingForInput)
            {
                Time.timeScale = 0f; // also over a hit-stop that ended meanwhile
                return;              // the asked swipe and its guide stay as they are
            }
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
            switch (CurrentStep.completeCondition)
            {
                case TutorialCondition.LaneLeftAndBack: done = leftLaneReached && right && player.LaneIndex == 0 &&
                    Mathf.Abs(player.transform.position.x - startPose.x) < 0.03f; break;
                case TutorialCondition.JumpOver: done = jumpCoveredObstacle && passed; break;
                case TutorialCondition.SlideUnder: done = slideCoveredObstacle && passed; break;
                case TutorialCondition.LaneAttackKill: done = laneKill; break;
                case TutorialCondition.KillOnLowTimer: done = laneKill && recoveredLowTimer; break;
                case TutorialCondition.NearMiss: done = left && nearMiss; break;
                case TutorialCondition.AirHomingKills: done = airKills >= 2; break;
                case TutorialCondition.FastFallLanding: done = fastFall && player.IsGrounded; break;
                case TutorialCondition.Stomp: done = stomp; break;
                case TutorialCondition.GroundSlam: done = slam && slamKills >= 2; break;
            }
            if (done)
            {
                CompleteStep();
                return;
            }
            // A wrong move that carries the runner past the lesson's obstacle
            // or target fails the try at once instead of waiting out the timer.
            missedFor = MissedLesson(body, passed) ? missedFor + Time.deltaTime : 0f;
            if (missedFor >= 0.25f)
            {
                HandleFailure();
                return;
            }
            UpdateCue(body);
            UpdateHold();
        }

        private bool MissedLesson(Bounds body, bool passed)
        {
            if (obstacle != null)
                return passed;
            if (enemies.Count == 0)
                return false;
            Enemy next = NextEnemy;
            return next == null || body.min.z > next.transform.position.z + 2.5f;
        }

        // When the swipe is due the game stops (guide showing) and only that
        // swipe starts it again — beginners do not have to hit the timing.
        private void UpdateHold()
        {
            if (!CurrentStep.pauseForInput || PromptInput == LessonInput.None)
                return;
            // The same swipe just given is still being carried out — except a
            // jump that has left the ground: the next up is the homing attack.
            float since = Time.time - releasedAt;
            bool newPhase = releasedGrounded && !player.IsGrounded && since >= 0.1f;
            if (PromptInput == releasedInput && since < 0.25f && !newPhase)
                return;
            IsHoldingForInput = true;
            Time.timeScale = 0f;
        }

        // Player input asks first (PlayerController.Request*): while holding,
        // only the swipe asked for gets through, and it resumes the game.
        public bool TryConsumeInput(LessonInput input)
        {
            if (!IsHoldingForInput)
                return true;
            if (input != PromptInput)
                return false;
            ReleaseHold(input);
            return true;
        }

        private void ReleaseHold(LessonInput input = LessonInput.None)
        {
            if (!IsHoldingForInput)
                return;
            IsHoldingForInput = false;
            Time.timeScale = 1f;
            releasedInput = input;
            releasedAt = Time.time;
            releasedGrounded = player != null && player.IsGrounded;
        }

        private void UpdateCue(Bounds body)
        {
            PromptInput = LessonInput.None;
            if (StepIndex == 0)
                PromptInput = !left ? LessonInput.Left : leftLaneReached && !right ? LessonInput.Right : LessonInput.None;
            else if (StepIndex == 1 || StepIndex == 2)
            {
                // Only in the block's lane: a runner who dodged aside is not
                // asked (or held) to jump / slide, and fails passing it.
                float dz = obstacle.transform.position.z - player.transform.position.z;
                bool sameLane = Mathf.Abs(obstacle.transform.position.x - player.transform.position.x) < 1f;
                if (dz <= 3.2f && dz > -0.5f && sameLane && player.IsGrounded &&
                    (StepIndex == 1 ? !jumped : !slid))
                    PromptInput = StepIndex == 1 ? LessonInput.Up : LessonInput.Down;
            }
            else if (StepIndex == 3 || StepIndex == 4)
            {
                // The lane attack needs the enemy in the next lane to the right.
                float dx = NextEnemy != null ? NextEnemy.transform.position.x - player.transform.position.x : 0f;
                if (NextEnemy != null && NextEnemy.transform.position.z - player.transform.position.z <= 2.6f &&
                    dx > 1.5f && dx < 3.5f)
                    PromptInput = LessonInput.Right;
            }
            else if (StepIndex == 5)
            {
                // Asked (and held) only just before the hit, so the dodge is a real close call.
                Bounds block = obstacle.GetComponent<Collider>().bounds;
                bool sameLane = Mathf.Abs(obstacle.transform.position.x - player.transform.position.x) < 1f;
                if (!left && sameLane && block.min.z - body.max.z <= NearMissCueDistance)
                    PromptInput = LessonInput.Left;
            }
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
                    {
                        // Right after an air kill, a swipe toward the next enemy's
                        // side chains to it; one straight above is reached with up.
                        float dx = NextEnemy.transform.position.x - player.transform.position.x;
                        PromptInput = player.IsAirComboReady && Mathf.Abs(dx) > 1.5f ?
                            (dx > 0f ? LessonInput.Right : LessonInput.Left) : LessonInput.Up;
                    }
                }
                else if (StepIndex == 8 && NextEnemy != null &&
                    NextEnemy.transform.position.z - player.transform.position.z <= 3.4f && !player.IsFastFalling)
                    PromptInput = LessonInput.Down;
                else if (StepIndex == 9 && !slam && !player.IsFastFalling)
                    PromptInput = LessonInput.Down;
            }
            // The arrow and swiping hand show the input asked for right now.
            LessonInput guide = CurrentStep.showSwipeGuide ? PromptInput : LessonInput.None;
            string keys = null;
#if UNITY_EDITOR || UNITY_STANDALONE
            keys = guide == LessonInput.Left ? "A / LEFT" : guide == LessonInput.Right ? "D / RIGHT" :
                guide == LessonInput.Up ? "SPACE / UP" : guide == LessonInput.Down ? "S / DOWN" : null;
#endif
            ui.SetGuide(guide, keys);
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
            if (!followUpShown && !string.IsNullOrEmpty(CurrentStep.followUpText) &&
                Matches(CurrentStep.requiredInputType, action))
            {
                followUpShown = true;
                ui.ShowDialogue(LessonTitle, CurrentStep.followUpText);
            }
        }

        private static bool Matches(TutorialInputType input, PlayerController.PlayerAction action)
        {
            switch (input)
            {
                case TutorialInputType.SwipeLeft: return action == PlayerController.PlayerAction.Left;
                case TutorialInputType.SwipeRight: return action == PlayerController.PlayerAction.Right;
                case TutorialInputType.SwipeUp: return action == PlayerController.PlayerAction.Jump ||
                    action == PlayerController.PlayerAction.Homing;
                case TutorialInputType.SwipeDown: return action == PlayerController.PlayerAction.Slide ||
                    action == PlayerController.PlayerAction.FastFall || action == PlayerController.PlayerAction.SlamStart;
                default: return false;
            }
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
            ReleaseHold();
            State = LessonState.Success;
            CompletedSteps++;
            PromptInput = LessonInput.None;
            timer.PausePractice();
            ui.SetGuide(LessonInput.None, null);
            ui.CompleteTyping();
            string[] praise = Look.successTexts;
            string pop = !string.IsNullOrEmpty(CurrentStep.successText) ? CurrentStep.successText :
                praise != null && praise.Length > 0 ? praise[(CompletedSteps - 1) % praise.Length] : "NICE!";
            ui.PlaySuccess(pop);
            StartCoroutine(AdvanceStep());
        }

        private IEnumerator AdvanceStep()
        {
            yield return new WaitForSecondsRealtime(Mathf.Clamp(CurrentStep.nextDelay, 0.3f, 2f));
            while (flow != null && flow.IsSkipConfirmationOpen)
                yield return null;
            if (State != LessonState.Success)
                yield break;
            if (++StepIndex < LessonCount)
                PrepareStep();
            else
            {
                State = LessonState.Complete;
                player.enabled = false;
                if (feedback != null)
                    feedback.StopAllFeedback();
                ui.SetHighlight(TutorialHighlight.None, false);
                ui.ShowDialogue("TUTORIAL COMPLETE", Look.completeText);
                continueButton.SetActive(true);
                PlayerPrefs.SetInt("Deadline4Sec.TutorialCompleted", 1);
                PlayerPrefs.Save();
            }
        }

        public void HandleFailure()
        {
            if (State != LessonState.Active)
                return;
            ReleaseHold();
            State = LessonState.Failed;
            FailureCount++;
            PromptInput = LessonInput.None;
            player.enabled = false;
            timer.PausePractice();
            if (feedback != null)
            {
                feedback.StopAllFeedback();
                feedback.PlayMenuResult(false);
            }
            ui.SetGuide(LessonInput.None, null);
            ui.ShowDialogue(LessonTitle, Look.failText);
            StartCoroutine(RetryAfterFade());
        }

        // A failed try: the screen darkens a moment and the same lesson
        // starts again by itself (no retry button).
        private IEnumerator RetryAfterFade()
        {
            TutorialPresentation look = Look;
            ui.SetScreenFade(look.failFadeAlpha, look.failFadeDuration);
            yield return new WaitForSecondsRealtime(look.failFadeDuration + look.failHold);
            while (flow != null && flow.IsSkipConfirmationOpen)
                yield return null;
            RetryStep();
        }

        public void RetryStep()
        {
            if (State != LessonState.Failed)
                return;
            PrepareStep();
            ui.SetScreenFade(0f, 0.3f);
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
            // Speaker, highlight and swipe guide (below the button). A failed
            // try retries by itself and SKIP is the only way out, so there is
            // no TRY AGAIN / MENU button (2026-11-01).
            ui = panel.AddComponent<TutorialUIController>();
            ui.Build(canvas, assets);
            TMP_Text next = flow.CreateMenuButton(panel.transform, "START RUN", Vector2.zero,
                new Vector2(360f, 80f), ContinueToRun);
            continueButton = next.transform.parent.gameObject;
            BottomButton(continueButton, new Vector2(0.5f, 0f), new Vector2(0f, 60f));
            continueButton.SetActive(false);
        }

        private TutorialPresentation Look => assets.presentation ?? new TutorialPresentation();
        private string LessonTitle => (StepIndex + 1) + "/" + LessonCount + "  " + CurrentStep.title;

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
            ReleaseHold();
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
            if (ui != null)
                ui.Cleanup();
            if (panel != null)
                Destroy(panel);
        }

        private void OnDestroy() => Stop();
    }
}
