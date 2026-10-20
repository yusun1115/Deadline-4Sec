using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    public sealed class GameTimer : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Text timeText;
        [SerializeField] private Text gameOverText;

        [Header("Timer urgency")]
        [SerializeField] private Color normalTimeColor = new Color(0.94f, 0.96f, 1f);
        [SerializeField] private Color warningTimeColor = new Color(1f, 0.7f, 0.16f);
        [SerializeField] private Color criticalTimeColor = new Color(1f, 0.16f, 0.31f);
        [SerializeField, Min(0f)] private float warningAtSeconds = 2.5f;
        [SerializeField, Min(0f)] private float criticalAtSeconds = 1f;
        // Held on a wall (Wall Run): the number turns this colour while it waits.
        [SerializeField] private Color wallPausedTimeColor = new Color(0.72f, 0.62f, 1f);

        public const float BaseDuration = 4f;
        private float remainingTime;
        private float maximumTime = BaseDuration;
        private bool isGameOver;
        private bool isRunning;
        private int runStartedFrame = -1;
        private GameFlowManager gameFlow;
        private CameraFeedbackController cameraFeedback;
        private bool warningPlayed;

        public float RemainingTime => remainingTime;
        public float MaximumTime => maximumTime;
        public bool IsGameOver => isGameOver;
        public bool IsRunning => isRunning && !isGameOver;
        // Deadline countdown held because the runner is on a wall (Wall Run).
        public bool IsHeldByWallRun => IsRunning && !untimed && playerController != null && playerController.IsWallRunning;
        public event Action<float> TimerReset;
        public enum DeathReason { TimerExpired, Collision, Fall }

        // Presentation only: why the last run ended (collision vs. reaching 0 s).
        public DeathReason LastDeathReason { get; private set; }

        // Classic Mode: the run is live (IsRunning) but the 4-second rule is off —
        // no countdown, reset, warning, Time Heart or timeout, and no timer HUD.
        public bool IsUntimed => untimed;
        private bool untimed;

        public void SetUntimed(bool value)
        {
            untimed = value;
            if (timeText != null)
            {
                timeText.gameObject.SetActive(!value);
                Transform frame = timeText.transform.parent != null ? timeText.transform.parent.Find("TimerFrame") : null;
                if (frame != null)
                    frame.gameObject.SetActive(!value);
            }
            if (value)
            {
                maximumTime = BaseDuration;
                remainingTime = BaseDuration;
            }
        }

        private void Awake()
        {
            remainingTime = BaseDuration;
            gameFlow = GetComponent<GameFlowManager>();
            cameraFeedback = FindFirstObjectByType<CameraFeedbackController>();
            GameFont.Apply(timeText);
            GameFont.Apply(gameOverText);
            if (timeText != null)
            {
                // Anchor to the screen edge so the main timer survives canvas scaling.
                RectTransform rect = timeText.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                // Places the number font's digits in the middle of the frame
                // (-24, was -33: HiKR-ExtraBold sat ~9 units low — 2026-10-11).
                rect.anchoredPosition = new Vector2(0f, -24f);
                rect.sizeDelta = new Vector2(260f, 140f);
                timeText.fontSize = 76;
                timeText.resizeTextForBestFit = true;
                timeText.resizeTextMinSize = 42;
                timeText.resizeTextMaxSize = 76;
                // Font and slant come from GameFont (number font, italic).
                timeText.alignment = TextAnchor.MiddleCenter;
                timeText.raycastTarget = false;
            }
            if (gameOverText != null)
                gameOverText.gameObject.SetActive(false);
            UpdateTimeText();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void Tick(float deltaTime)
        {
            if (!IsRunning || untimed || Time.frameCount == runStartedFrame)
                return;

            // The countdown holds while Freeze Clock runs and while the runner is
            // on a wall (Wall Run, 2026-10-18); it resumes after the exit.
            PowerUpManager powerUps = GetComponent<PowerUpManager>();
            if ((powerUps == null || !powerUps.TimerFrozen) && !IsHeldByWallRun)
                remainingTime = Mathf.Max(0f, remainingTime - deltaTime);
            UpdateTimeText();

            if (!warningPlayed && remainingTime > 0f &&
                remainingTime <= criticalAtSeconds)
            {
                warningPlayed = true;
                if (cameraFeedback != null)
                    cameraFeedback.PlayTimerWarning();
            }

            if (remainingTime <= 0f)
                TriggerGameOver();
        }

        public void BeginRun()
        {
            if (isGameOver || isRunning)
                return;

            maximumTime = BaseDuration;
            remainingTime = maximumTime;
            warningPlayed = false;
            isRunning = true;
            LastDeathReason = DeathReason.TimerExpired;
            runStartedFrame = Time.frameCount;
            UpdateTimeText();
        }

        // Enemy kills and near misses reset only an active run.
        public void ResetTimer()
        {
            if (!IsRunning || untimed)
                return;

            float previous = remainingTime;
            remainingTime = maximumTime;
            warningPlayed = false;
            UpdateTimeText();
            TimerReset?.Invoke(previous);
        }

        public void BeginPractice()
        {
            // The tutorial always teaches the 4-second rule.
            if (untimed)
                SetUntimed(false);
            isGameOver = false;
            isRunning = false;
            if (gameOverText != null)
                gameOverText.gameObject.SetActive(false);
            BeginRun();
        }

        public void SetTemporaryMaximum(float seconds, bool refill)
        {
            if (untimed)
                return;
            maximumTime = Mathf.Max(BaseDuration, seconds);
            remainingTime = refill ? maximumTime : Mathf.Min(remainingTime, maximumTime);
            warningPlayed = false;
            UpdateTimeText();
        }

        public void PausePractice()
        {
            isRunning = false;
            if (gameOverText != null)
                gameOverText.gameObject.SetActive(false);
        }

        public void ReviveRun()
        {
            isGameOver = false;
            isRunning = true;
            LastDeathReason = DeathReason.TimerExpired;
            maximumTime = BaseDuration;
            remainingTime = BaseDuration;
            warningPlayed = false;
            runStartedFrame = Time.frameCount;
            if (gameOverText != null)
                gameOverText.gameObject.SetActive(false);
            UpdateTimeText();
        }

        public void TriggerFatalContact() => TriggerFatalContactFrom(null);

        public void TriggerFatalContactFrom(Collider source)
        {
            if (!IsRunning)
                return;
            RunInventory inventory = GetComponent<RunInventory>();
            if (inventory != null && inventory.AbsorbFatalContact(source))
                return;
            LastDeathReason = DeathReason.Collision;
            TriggerGameOver();
        }

        // Wall Run (2026-10-18): falling into a floor gap. Not a contact, so the
        // Shield (which only blocks enemy / obstacle contact) does not stop it.
        public void TriggerFallDeath()
        {
            if (!IsRunning)
                return;
            LastDeathReason = DeathReason.Fall;
            TriggerGameOver();
        }

        public void TriggerGameOver()
        {
            if (isGameOver || !isRunning)
                return;

            isGameOver = true;
            isRunning = false;
            remainingTime = 0f;
            UpdateTimeText();

            if (playerController != null)
                playerController.enabled = false;
            if (gameOverText != null)
            {
                gameOverText.text = "GAME OVER";
                gameOverText.gameObject.SetActive(true);
            }
            if (gameFlow != null)
                gameFlow.HandleGameOver();
        }

        private void UpdateTimeText()
        {
            if (timeText != null)
            {
                timeText.text = remainingTime.ToString("F2", CultureInfo.InvariantCulture);
                timeText.color = IsHeldByWallRun ? wallPausedTimeColor : remainingTime <= criticalAtSeconds
                    ? criticalTimeColor
                    : remainingTime <= warningAtSeconds
                        ? warningTimeColor
                        : normalTimeColor;
            }
        }

        private void OnValidate()
        {
            warningAtSeconds = Mathf.Max(0f, warningAtSeconds);
            criticalAtSeconds = Mathf.Clamp(criticalAtSeconds, 0f, warningAtSeconds);
        }

    }
}
