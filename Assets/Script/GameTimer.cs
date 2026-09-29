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
        public event Action<float> TimerReset;

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
                rect.anchoredPosition = new Vector2(0f, -24f);
                rect.sizeDelta = new Vector2(260f, 140f);
                timeText.fontSize = 76;
                timeText.resizeTextForBestFit = true;
                timeText.resizeTextMinSize = 42;
                timeText.resizeTextMaxSize = 76;
                timeText.fontStyle = FontStyle.Normal;
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
            if (!IsRunning || Time.frameCount == runStartedFrame)
                return;

            PowerUpManager powerUps = GetComponent<PowerUpManager>();
            if (powerUps == null || !powerUps.TimerFrozen)
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
            runStartedFrame = Time.frameCount;
            UpdateTimeText();
        }

        // Enemy kills and near misses reset only an active run.
        public void ResetTimer()
        {
            if (!IsRunning)
                return;

            float previous = remainingTime;
            remainingTime = maximumTime;
            warningPlayed = false;
            UpdateTimeText();
            TimerReset?.Invoke(previous);
        }

        public void BeginPractice()
        {
            isGameOver = false;
            isRunning = false;
            if (gameOverText != null)
                gameOverText.gameObject.SetActive(false);
            BeginRun();
        }

        public void SetTemporaryMaximum(float seconds, bool refill)
        {
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
                timeText.color = remainingTime <= criticalAtSeconds
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
