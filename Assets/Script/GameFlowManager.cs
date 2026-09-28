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
        public enum GameState { Title, Settings, Ready, Playing, GameOver, Result, Tutorial }

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

        [Header("Authored menus (edit these objects in the scene)")]
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private TMP_Text soundSettingText;
        [SerializeField] private TMP_Text vibrationSettingText;
        [SerializeField] private Button[] menuButtons;

        [Header("Flow timing (unscaled seconds)")]
        [SerializeField, Min(0.1f)] private float introDuration = 2.2f;
        [SerializeField, Min(0f)] private float goDisplayDuration = 0.5f;
        [SerializeField, Min(0f)] private float resultDelay = 0.4f;

        private const string BestScoreKey = "Deadline4Sec.BestScore";
        private const string BestDistanceKey = "Deadline4Sec.BestDistance";
        private const string BestComboKey = "Deadline4Sec.BestCombo";
        private bool gameOverHandled;
        private CameraFeedbackController cameraFeedback;
        private static bool startImmediatelyAfterReload;
        private TutorialController tutorial;

        public GameState State { get; private set; } = GameState.Title;
        public bool IsPlaying => State == GameState.Playing ||
            (State == GameState.Tutorial && tutorial != null && tutorial.IsFeedbackActive);
        public bool CanReceiveInput => State == GameState.Playing ||
            (State == GameState.Tutorial && tutorial != null && tutorial.CanReceiveInput);

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
            BindMenuUI();
            foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                GameFont.Apply(text);
            foreach (Text text in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                GameFont.Apply(text);
            if (countdownText != null)
            {
                RectTransform introLabel = countdownText.rectTransform;
                introLabel.anchorMin = introLabel.anchorMax = new Vector2(0.5f, 0.72f);
                introLabel.anchoredPosition = Vector2.zero;
                introLabel.sizeDelta = new Vector2(520f, 110f);
                countdownText.enableAutoSizing = true;
                countdownText.fontSizeMax = 96f;
                countdownText.fontSizeMin = 48f;
            }
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
            SetText(countdownText, "Ready?");
            float speed = runManager != null ? runManager.BaseForwardSpeed : 10.2f;
            // Integral of the symmetric smooth acceleration below is 0.675.
            float goZ = playerController.transform.position.z + speed * introDuration * 0.675f;
            PatternSpawner spawner = FindFirstObjectByType<PatternSpawner>();
            if (spawner != null)
                spawner.PrepareOpeningPatterns(goZ);
            if (cameraFeedback != null)
                cameraFeedback.BeginRunIntro(introDuration);

            float elapsed = 0f;
            while (elapsed < introDuration)
            {
                float delta = Mathf.Min(Mathf.Min(Time.unscaledDeltaTime, 0.05f), introDuration - elapsed);
                float progress = (elapsed + delta * 0.5f) / introDuration;
                playerController.AdvanceRunIntro(delta,
                    speed * Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0f, 1f, progress)), progress);
                elapsed += delta;
                yield return null;
            }
            playerController.EndRunIntro();
            if (cameraFeedback != null)
                cameraFeedback.CompleteRunIntro();
            SetText(countdownText, "Go!");
            State = GameState.Playing;
            if (runManager != null)
                runManager.BeginRun();
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

        public void StartTutorial()
        {
            if (State != GameState.Title)
                return;
            TutorialCourseAssets assets = Resources.Load<TutorialCourseAssets>("Tutorial/TutorialCourse");
            if (assets == null || !assets.IsValid)
            {
                Debug.LogError("Tutorial course assets are missing. Prepare Tutorial Course before building.");
                return;
            }
            if (tutorial == null)
                tutorial = gameObject.AddComponent<TutorialController>();
            titlePanel.SetActive(false);
            State = GameState.Tutorial;
            SetUI(true, false, false);
            tutorial.Begin(this, playerController, gameTimer, runManager,
                FindFirstObjectByType<PatternSpawner>(), titlePanel.GetComponentInParent<Canvas>(), assets);
        }

        public void FinishTutorial(bool startRun)
        {
            if (State != GameState.Tutorial)
                return;
            tutorial.Stop();
            startImmediatelyAfterReload = startRun;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
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
            if (State == GameState.Tutorial && tutorial != null)
            {
                tutorial.HandleFailure();
                return;
            }
            if (gameOverHandled)
                return;

            gameOverHandled = true;
            State = GameState.GameOver;
            if (cameraFeedback != null)
            {
                cameraFeedback.StopAllFeedback();
                cameraFeedback.PlayGameOver();
            }
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

        private void BindMenuUI()
        {
            if (titlePanel == null || settingsPanel == null)
            {
                Debug.LogError("GameFlowManager: assign the saved TitlePanel and SettingsPanel in the scene.");
                return;
            }
            // Text, layout, colours, and action bindings belong to the saved scene.
            // Only dynamic setting values and click feedback are changed at runtime.
            if (menuButtons != null)
                foreach (Button button in menuButtons)
                    if (button != null)
                        button.onClick.AddListener(PlayMenuClick);
            RefreshSettingsLabels();
        }

        private void PlayMenuClick()
        {
            if (cameraFeedback != null)
                cameraFeedback.PlayUIClick();
        }

        internal static TMP_Text CreateMenuText(Transform parent, string label,
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
            GameFont.Apply(labelText);
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

        internal TMP_Text CreateMenuButton(Transform parent, string label,
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
            button.onClick.AddListener(() =>
            {
                if (cameraFeedback != null)
                    cameraFeedback.PlayUIClick();
                onClick();
            });
            return CreateMenuText(buttonObject.transform, label, Vector2.zero,
                new Vector2(size.x - 20f, size.y - 10f), 36f);
        }

        public void ToggleSound()
        {
            GamePreferences.SoundEnabled = !GamePreferences.SoundEnabled;
            RefreshSettingsLabels();
        }

        public void ToggleVibration()
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
