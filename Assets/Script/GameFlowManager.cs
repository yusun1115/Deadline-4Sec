using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Deadline4Sec
{
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(GameTimer))]
    public sealed class GameFlowManager : MonoBehaviour
    {
        public enum GameState { Title, Settings, Ready, Countdown, Playing, GameOver, Result }

        [Header("Scene references")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private GameTimer gameTimer;
        [SerializeField] private RunManager runManager;

        [Header("UI")]
        [SerializeField] private GameObject gameplayUI;
        [SerializeField] private GameObject countdownUI;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultScoreText;
        [SerializeField] private TMP_Text resultDistanceText;
        [SerializeField] private TMP_Text resultBestComboText;
        [SerializeField] private TMP_Text resultSurvivalTimeText;
        [SerializeField] private TMP_Text bestScoreText;
        [SerializeField] private TMP_Text bestDistanceText;
        [SerializeField] private TMP_Text newBestText;
        [SerializeField] private Button retryButton;

        [Header("Flow timing (unscaled seconds)")]
        [SerializeField, Min(0f)] private float readyDuration = 0.75f;
        [SerializeField, Min(0.1f)] private float countdownStepDuration = 1f;
        [SerializeField, Min(0f)] private float goDisplayDuration = 0.5f;
        [SerializeField, Min(0f)] private float resultDelay = 0.4f;

        private const string BestScoreKey = "Deadline4Sec.BestScore";
        private const string BestDistanceKey = "Deadline4Sec.BestDistance";
        private const string BestComboKey = "Deadline4Sec.BestCombo";
        private bool gameOverHandled;
        private CameraFeedbackController cameraFeedback;
        private GameObject titlePanel;
        private GameObject settingsPanel;
        private TMP_Text soundSettingText;
        private TMP_Text vibrationSettingText;
        private static bool startImmediatelyAfterReload;

        public GameState State { get; private set; } = GameState.Title;
        public bool IsPlaying => State == GameState.Playing;

        private void Awake()
        {
            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerController>();
            if (gameTimer == null)
                gameTimer = GetComponent<GameTimer>();
            if (runManager == null)
                runManager = GetComponent<RunManager>();
            cameraFeedback = FindFirstObjectByType<CameraFeedbackController>();

            if (playerController != null)
                playerController.enabled = false;
            if (retryButton != null)
                retryButton.onClick.AddListener(Retry);
            BuildMenuUI();
            SetUI(false, false, false);
            ShowTitle();
        }

        private void Start()
        {
            if (!startImmediatelyAfterReload)
                return;

            startImmediatelyAfterReload = false;
            StartGame();
        }

        private void Update()
        {
            if ((State == GameState.GameOver || State == GameState.Result) && RetryKeyPressed())
                Retry();
        }

        private IEnumerator BeginFlow()
        {
            State = GameState.Ready;
            countdownText.text = "READY";
            yield return new WaitForSecondsRealtime(readyDuration);

            State = GameState.Countdown;
            string[] steps = { "3", "2", "1" };
            foreach (string step in steps)
            {
                countdownText.text = step;
                yield return new WaitForSecondsRealtime(countdownStepDuration);
            }

            countdownText.text = "GO!";
            State = GameState.Playing;
            gameTimer.BeginRun();
            if (playerController != null)
                playerController.enabled = true;
            SetUI(true, true, false);
            yield return new WaitForSecondsRealtime(goDisplayDuration);
            if (State == GameState.Playing)
                SetUI(true, false, false);
        }

        public void StartGame()
        {
            if (State != GameState.Title && State != GameState.Settings)
                return;

            if (titlePanel != null)
                titlePanel.SetActive(false);
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
            SetUI(false, true, false);
            StartCoroutine(BeginFlow());
        }

        public void OpenSettings()
        {
            if (State != GameState.Title)
                return;
            State = GameState.Settings;
            RefreshSettingsLabels();
            if (titlePanel != null)
                titlePanel.SetActive(false);
            if (settingsPanel != null)
                settingsPanel.SetActive(true);
        }

        public void CloseSettings()
        {
            if (State == GameState.Settings)
                ShowTitle();
        }

        private void ShowTitle()
        {
            State = GameState.Title;
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
            if (titlePanel != null)
                titlePanel.SetActive(true);
        }

        public void HandleGameOver()
        {
            if (gameOverHandled)
                return;

            gameOverHandled = true;
            State = GameState.GameOver;
            if (cameraFeedback != null)
                cameraFeedback.StopAllFeedback();
            if (playerController != null)
                playerController.enabled = false;
            SaveRecordsAndFillResult();
            StartCoroutine(ShowResultAfterDelay());
        }

        private IEnumerator ShowResultAfterDelay()
        {
            SetUI(false, false, false);
            yield return new WaitForSecondsRealtime(resultDelay);
            State = GameState.Result;
            SetUI(false, false, true);
        }

        private void SaveRecordsAndFillResult()
        {
            int score = runManager != null ? runManager.CurrentScore : 0;
            float distance = runManager != null ? runManager.Distance : 0f;
            int bestCombo = runManager != null ? runManager.BestCombo : 0;
            float runTime = runManager != null ? runManager.CurrentRunTime : 0f;

            int savedScore = PlayerPrefs.GetInt(BestScoreKey, 0);
            float savedDistance = PlayerPrefs.GetFloat(BestDistanceKey, 0f);
            int savedCombo = PlayerPrefs.GetInt(BestComboKey, 0);
            bool newBestScore = score > savedScore;
            bool newBestDistance = distance > savedDistance;

            if (newBestScore)
                PlayerPrefs.SetInt(BestScoreKey, score);
            if (newBestDistance)
                PlayerPrefs.SetFloat(BestDistanceKey, distance);
            if (bestCombo > savedCombo)
                PlayerPrefs.SetInt(BestComboKey, bestCombo);
            PlayerPrefs.Save();

            int bestScore = Mathf.Max(savedScore, score);
            float bestDistance = Mathf.Max(savedDistance, distance);
            SetText(resultScoreText, "SCORE        " + score.ToString("N0", CultureInfo.InvariantCulture));
            SetText(resultDistanceText, "DISTANCE       " + Mathf.FloorToInt(distance) + "m");
            SetText(resultBestComboText, "BEST COMBO      x" + bestCombo);
            SetText(resultSurvivalTimeText, "TIME          " + FormatTime(runTime));
            SetText(bestScoreText, "BEST SCORE   " + bestScore.ToString("N0", CultureInfo.InvariantCulture));
            SetText(bestDistanceText, "BEST DISTANCE  " + Mathf.FloorToInt(bestDistance) + "m");
            if (newBestText != null)
                newBestText.gameObject.SetActive(newBestScore || newBestDistance);
        }

        public void Retry()
        {
            if (State != GameState.GameOver && State != GameState.Result)
                return;
            startImmediatelyAfterReload = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }

        public void MainMenu()
        {
            if (State != GameState.GameOver && State != GameState.Result)
                return;
            startImmediatelyAfterReload = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }

        private void BuildMenuUI()
        {
            Canvas canvas = resultPanel != null
                ? resultPanel.GetComponentInParent<Canvas>()
                : FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("GameFlowManager: menu requires a Canvas.");
                return;
            }

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(800f, 600f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0f;
            }

            titlePanel = CreateOverlay(canvas.transform, "TitlePanel");
            CreateMenuText(titlePanel.transform, "DEADLINE: 4 SEC",
                new Vector2(0f, 185f), new Vector2(680f, 110f), 58f);
            CreateMenuText(titlePanel.transform, "RISK TO LIVE",
                new Vector2(0f, 105f), new Vector2(600f, 65f), 28f);
            CreateMenuButton(titlePanel.transform, "START", new Vector2(0f, -45f),
                new Vector2(360f, 84f), StartGame);
            CreateMenuButton(titlePanel.transform, "SETTINGS", new Vector2(0f, -155f),
                new Vector2(360f, 84f), OpenSettings);

            settingsPanel = CreateOverlay(canvas.transform, "SettingsPanel");
            CreateMenuText(settingsPanel.transform, "SETTINGS",
                new Vector2(0f, 175f), new Vector2(600f, 90f), 52f);
            soundSettingText = CreateMenuButton(settingsPanel.transform, string.Empty,
                new Vector2(0f, 50f), new Vector2(420f, 80f), ToggleSound);
            vibrationSettingText = CreateMenuButton(settingsPanel.transform, string.Empty,
                new Vector2(0f, -50f), new Vector2(420f, 80f), ToggleVibration);
            CreateMenuButton(settingsPanel.transform, "BACK", new Vector2(0f, -175f),
                new Vector2(360f, 80f), CloseSettings);
            RefreshSettingsLabels();
            settingsPanel.SetActive(false);

            if (resultPanel != null)
            {
                if (retryButton != null)
                {
                    RectTransform retryRect = retryButton.GetComponent<RectTransform>();
                    if (retryRect != null)
                        retryRect.anchoredPosition = new Vector2(0f, -255f);
                }
                CreateMenuButton(resultPanel.transform, "MAIN MENU",
                    new Vector2(0f, -350f), new Vector2(360f, 80f), MainMenu);
            }
        }

        private static GameObject CreateOverlay(Transform parent, string objectName)
        {
            GameObject panel = new GameObject(objectName,
                typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.025f, 0.03f, 0.055f, 0.97f);
            return panel;
        }

        private static TMP_Text CreateMenuText(Transform parent, string label,
            Vector2 position, Vector2 size, float fontSize)
        {
            GameObject textObject = new GameObject("Label", typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TextMeshProUGUI labelText = textObject.GetComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = fontSize;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.enableAutoSizing = true;
            labelText.fontSizeMin = 18f;
            labelText.fontSizeMax = fontSize;
            labelText.raycastTarget = false;
            return labelText;
        }

        private static TMP_Text CreateMenuButton(Transform parent, string label,
            Vector2 position, Vector2 size, Action onClick)
        {
            GameObject buttonObject = new GameObject(label + "Button",
                typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image background = buttonObject.GetComponent<Image>();
            background.color = new Color(0.72f, 0.09f, 0.24f, 1f);
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => onClick());
            return CreateMenuText(buttonObject.transform, label, Vector2.zero,
                new Vector2(size.x - 20f, size.y - 10f), 36f);
        }

        private void ToggleSound()
        {
            GamePreferences.SoundEnabled = !GamePreferences.SoundEnabled;
            RefreshSettingsLabels();
        }

        private void ToggleVibration()
        {
            GamePreferences.VibrationEnabled = !GamePreferences.VibrationEnabled;
            RefreshSettingsLabels();
        }

        private void RefreshSettingsLabels()
        {
            if (soundSettingText != null)
                soundSettingText.text = "SOUND   " + (GamePreferences.SoundEnabled ? "ON" : "OFF");
            if (vibrationSettingText != null)
                vibrationSettingText.text = "VIBRATION   " + (GamePreferences.VibrationEnabled ? "ON" : "OFF");
        }

        private void SetUI(bool gameplay, bool countdown, bool result)
        {
            if (gameplayUI != null)
                gameplayUI.SetActive(gameplay);
            if (countdownUI != null)
                countdownUI.SetActive(countdown);
            if (resultPanel != null)
                resultPanel.SetActive(result);
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
                target.text = value;
        }

        private static string FormatTime(float seconds)
        {
            int wholeSeconds = Mathf.FloorToInt(seconds);
            return (wholeSeconds / 60).ToString("00") + ":" + (wholeSeconds % 60).ToString("00");
        }

        private static bool RetryKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.R);
#else
            return false;
#endif
        }
    }

    public static class GamePreferences
    {
        private const string SoundKey = "Deadline4Sec.SoundEnabled";
        private const string VibrationKey = "Deadline4Sec.VibrationEnabled";

        public static bool SoundEnabled
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) != 0;
            set => Save(SoundKey, value);
        }

        public static bool VibrationEnabled
        {
            get => PlayerPrefs.GetInt(VibrationKey, 1) != 0;
            set => Save(VibrationKey, value);
        }

        private static void Save(string key, bool enabled)
        {
            PlayerPrefs.SetInt(key, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
