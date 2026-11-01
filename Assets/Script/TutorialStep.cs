using System;
using UnityEngine;

namespace Deadline4Sec
{
    // One tutorial step (2026-11-01 onboarding revamp). InfoOnly steps are the
    // briefing before the course (the speaker explains, nothing to do);
    // WaitForInput steps are the ten practice lessons, completed by the real
    // input/kill events of the run (the input itself is never blocked).
    // The lesson course (what spawns where) still follows the lesson order, so
    // the ten lessons keep their completeCondition order; texts, highlight,
    // guide and timing are free to edit in TutorialCourse.asset.
    public enum TutorialStepKind { InfoOnly, WaitForInput }
    public enum TutorialHighlight { None, Timer }
    public enum TutorialInputType { None, SwipeLeft, SwipeRight, SwipeUp, SwipeDown }
    public enum TutorialCondition
    {
        Read, LaneLeftAndBack, JumpOver, SlideUnder, LaneAttackKill, KillOnLowTimer,
        NearMiss, AirHomingKills, FastFallLanding, Stomp, GroundSlam
    }

    [Serializable]
    public sealed class TutorialStep
    {
        public string stepId;
        public TutorialStepKind kind;
        public string title;
        [TextArea(2, 4)] public string dialogueText;
        // Shown once the step's required input has been performed (e.g. "now back right").
        [TextArea(2, 4)] public string followUpText;
        public TutorialHighlight highlightTarget;
        public bool dimBackground;
        public TutorialInputType requiredInputType;
        public bool showSwipeGuide;
        // InfoOnly: go on by itself nextDelay seconds after the text is shown
        // (a tap always goes on). WaitForInput: delay after the success pop.
        public bool autoAdvance = true;
        [Min(0f)] public float nextDelay = 0.6f;
        public TutorialCondition completeCondition;
        // WaitForInput: the game stops when the swipe is due and only that
        // swipe starts it again (other swipes are ignored while it waits).
        public bool pauseForInput;
        // Success pop for this step (empty: the presentation's rotating praise).
        public string successText;

        private static TutorialStep Info(string id, string title, string text, float delay) => new TutorialStep
        {
            stepId = id, kind = TutorialStepKind.InfoOnly, title = title, dialogueText = text,
            highlightTarget = TutorialHighlight.Timer, dimBackground = true, autoAdvance = true,
            nextDelay = delay, completeCondition = TutorialCondition.Read
        };

        private static TutorialStep Lesson(string id, string title, string text, TutorialInputType input,
            TutorialCondition condition, string followUp = null, TutorialHighlight highlight = TutorialHighlight.None) =>
            new TutorialStep
            {
                stepId = id, kind = TutorialStepKind.WaitForInput, title = title, dialogueText = text,
                followUpText = followUp, highlightTarget = highlight, requiredInputType = input,
                showSwipeGuide = true, autoAdvance = true, nextDelay = 0.6f, completeCondition = condition,
                pauseForInput = true,
                successText = condition == TutorialCondition.NearMiss ? "NEAR MISS!" :
                    condition == TutorialCondition.KillOnLowTimer ? "4.00 RESET!" : null
            };

        public static TutorialStep[] CreateDefaults() => new[]
        {
            Info("timer_intro", "4-SECOND RULE", "여기 보이는 숫자가 네 생존 시간이야. 딱 4초뿐이지.", 3f),
            Info("timer_rule", "4-SECOND RULE", "0이 되면 바로 게임오버! 적을 처치하거나 아슬아슬하게 피하면 다시 4초로 돌아가.", 3.5f),
            Lesson("lane_move", "LANE MOVE", "좋아, 이제 직접 움직여 보자. 먼저 왼쪽으로 스와이프해!",
                TutorialInputType.SwipeLeft, TutorialCondition.LaneLeftAndBack,
                "잘했어! 이번엔 오른쪽으로 스와이프해서 가운데로 돌아와."),
            Lesson("jump", "JUMP", "앞에 장애물이 있어. 가까워지면 위로 스와이프해서 뛰어넘어!",
                TutorialInputType.SwipeUp, TutorialCondition.JumpOver),
            Lesson("slide", "SLIDE", "이번엔 높은 장애물이야. 아래로 스와이프해서 밑으로 미끄러져!",
                TutorialInputType.SwipeDown, TutorialCondition.SlideUnder),
            Lesson("lane_attack", "LANE ATTACK", "옆 레인에 적이 있어. 적 쪽으로 스와이프하면 낫으로 베어 버려!",
                TutorialInputType.SwipeRight, TutorialCondition.LaneAttackKill),
            Lesson("timer_reset", "4-SECOND TIMER", "타이머가 줄어드는 걸 봐. 적을 처치하면 다시 4초로 돌아가!",
                TutorialInputType.SwipeRight, TutorialCondition.KillOnLowTimer, null, TutorialHighlight.Timer),
            Lesson("near_miss", "NEAR MISS", "장애물에 부딪히기 직전까지 기다렸다가 왼쪽으로 스와이프! 아슬아슬하게 피해야 니어 미스야.",
                TutorialInputType.SwipeLeft, TutorialCondition.NearMiss),
            Lesson("air_homing", "AIR HOMING", "점프 후 위로 스와이프해서 적을 베고, 바로 오른쪽으로 스와이프해 옆 적까지 이어서 베어!",
                TutorialInputType.SwipeUp, TutorialCondition.AirHomingKills),
            Lesson("fast_fall", "FAST FALL", "점프한 다음, 공중에서 아래로 스와이프하면 빠르게 착지해.",
                TutorialInputType.SwipeUp, TutorialCondition.FastFallLanding, "지금! 아래로 스와이프!"),
            Lesson("stomp", "STOMP", "공중 적을 베고 왼쪽 스와이프로 이어간 뒤, 아래로 스와이프해서 적의 머리를 밟아!",
                TutorialInputType.SwipeUp, TutorialCondition.Stomp),
            Lesson("ground_slam", "GROUND SLAM", "더 높이 올라가서 아래로 스와이프! 땅을 내려찍어 주변 적까지 한 번에!",
                TutorialInputType.SwipeUp, TutorialCondition.GroundSlam),
        };
    }

    // Look and timing of the tutorial speaker, highlight and swipe guide
    // (canvas units at the 800-wide reference; seconds are real time).
    [Serializable]
    public sealed class TutorialPresentation
    {
        [Header("Speaker (top left)")]
        // Top-left corners from the screen's top-left: the bubble sits above
        // and right of the portrait so its tail (bottom left) points at her.
        public Vector2 portraitSize = new Vector2(152f, 143f);
        public Vector2 portraitPosition = new Vector2(0f, -300f);
        public Vector2 bubbleSize = new Vector2(655f, 215f);
        public Vector2 bubblePosition = new Vector2(96f, -194f);
        public float dialogueFontSize = 31f;
        public Color dialogueColor = new Color(0.2f, 0.07f, 0.14f);
        public Color titleColor = new Color(0.86f, 0.12f, 0.45f);
        [Min(0f)] public float portraitEnterDuration = 0.26f;
        [Min(0f)] public float bubbleDelay = 0.1f;
        [Min(0f)] public float bubbleEnterDuration = 0.18f;
        [Tooltip("Typing speed in characters per second; 0 fades the whole line in.")]
        [Min(0f)] public float typingCharsPerSecond = 40f;

        [Header("Highlight")]
        [Range(0f, 1f)] public float dimAlpha = 0.72f;
        public Color timerGlowColor = new Color(1f, 0.3f, 0.6f, 1f);
        [Min(1f)] public float timerPulseScale = 1.08f;
        [Min(0.1f)] public float timerPulsePeriod = 0.9f;
        [Tooltip("GameplayUI children hidden during the tutorial (feedback such as NearMissFeedback stays).")]
        public string[] hiddenHud = {
            "ScorePill", "ScoreText", "ComboText", "DistanceText", "CoinPill", "CoinPillIcon", "RunCoinText",
            "ActivePowerUpsText", "GiftPill", "GiftPillIcon", "GiftBoxCountText", "ConsumableStatusText"
        };

        [Header("Failed step: darken, then retry by itself")]
        [Range(0f, 1f)] public float failFadeAlpha = 0.8f;
        [Min(0f)] public float failFadeDuration = 0.25f;
        [Min(0f)] public float failHold = 0.55f;

        [Header("Swipe guide (bottom center)")]
        public float guideBottomOffset = 600f;
        public Vector2 arrowSize = new Vector2(80f, 172f);
        public Vector2 handSize = new Vector2(92f, 120f);
        public float handTravel = 170f;
        [Min(0.2f)] public float handCycle = 1.05f;

        [Header("Success / end")]
        public string[] successTexts = { "NICE!", "GOOD!", "GREAT!" };
        public Color successColor = new Color(1f, 0.86f, 0.3f);
        [Min(0.1f)] public float successDuration = 0.55f;
        [TextArea(2, 4)] public string failText = "앗, 아쉬워! 이번 단계를 다시 해 보자.";
        [TextArea(2, 4)] public string completeText = "완벽해! 적 처치와 니어 미스로 4초를 계속 되돌려. 이제 실전이야!";
    }
}
