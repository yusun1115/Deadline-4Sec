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
        public enum GameState { Title, Settings, Ready, Playing, GameOver, Result, Tutorial, Upgrades,
            Dying, RevivePrompt, GiftBoxes, Skins }

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
        [SerializeField] private GameObject tutorialSkipUI;
        [SerializeField] private GameObject tutorialSkipConfirmation;
        [SerializeField] private TMP_Text soundSettingText;
        [SerializeField] private TMP_Text vibrationSettingText;
        [SerializeField] private Button[] menuButtons;
        [SerializeField] private GameObject upgradePanel;
        [SerializeField] private GameObject skinPanel;

        [Header("Flow timing (unscaled seconds)")]
        [SerializeField, Min(0.1f)] private float introDuration = 2.2f;
        [SerializeField, Min(0f)] private float goDisplayDuration = 0.5f;
        [SerializeField, Min(0f)] private float resultDelay = 0.4f;
        [SerializeField, Range(0.5f, 1f)] private float deathPresentationDuration = 0.75f;
        [SerializeField, Min(1f)] private float revivePromptSeconds = 4f;

        private const string BestScoreKey = "Deadline4Sec.BestScore";
        private const string BestDistanceKey = "Deadline4Sec.BestDistance";
        private const string BestComboKey = "Deadline4Sec.BestCombo";
        private const string TutorialCompletedKey = "Deadline4Sec.TutorialCompleted";
        private bool gameOverHandled;
        private CameraFeedbackController cameraFeedback;
        private static bool startImmediatelyAfterReload;
        private TutorialController tutorial;
        private bool skipConfirmationOpen;
        private float timeScaleBeforeSkip;
        private CoinWallet wallet;
        private PowerUpManager powerUps;
        private UpgradeMenu upgradeMenu;
        private RunInventory inventory;
        private GameExtrasUI extrasUI;
        private int giftBoxIndex;
        private int giftBoxTotal;
        private bool giftBoxRevealed;
        private bool retryAfterBoxes;

        public GameState State { get; private set; } = GameState.Title;
        public bool IsPlaying => State == GameState.Playing ||
            (State == GameState.Tutorial && !skipConfirmationOpen && tutorial != null && tutorial.IsFeedbackActive);
        public bool CanReceiveInput => State == GameState.Playing ||
            (State == GameState.Tutorial && !skipConfirmationOpen && tutorial != null && tutorial.CanReceiveInput);
        public bool IsSkipConfirmationOpen => skipConfirmationOpen;

        private void Awake()
        {
            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerController>();
            if (gameTimer == null)
                gameTimer = GetComponent<GameTimer>();
            if (runManager == null)
                runManager = GetComponent<RunManager>();
            wallet = GetComponent<CoinWallet>();
            powerUps = GetComponent<PowerUpManager>();
            upgradeMenu = GetComponent<UpgradeMenu>();
            inventory = GetComponent<RunInventory>();
            extrasUI = GetComponent<GameExtrasUI>();
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
            if (tutorialSkipUI != null)
                tutorialSkipUI.SetActive(false);
            if (tutorialSkipConfirmation != null)
                tutorialSkipConfirmation.SetActive(false);
            if (upgradePanel != null)
                upgradePanel.SetActive(false);
            if (skinPanel != null)
                skinPanel.SetActive(false);
            ShowTitle();
        }

        private void Start()
        {
            if (inventory != null && inventory.HasPendingReward)
            {
                State = GameState.GiftBoxes;
                giftBoxTotal = giftBoxIndex = 1;
                giftBoxRevealed = true;
                if (titlePanel != null)
                    titlePanel.SetActive(false);
                extrasUI?.ShowGiftPanel(1, 1, inventory.ApplyPendingReward(), false);
                return;
            }
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
            if (wallet != null)
                wallet.BeginRun();
            if (powerUps != null)
                powerUps.ResetRunEffects();
            inventory?.BeginRun();
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

        public void StartFromTitle()
        {
            if (State != GameState.Title)
                return;
            if (PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 0)
                StartTutorial();
            else
                StartGame();
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

        public void OpenUpgrades()
        {
            if (State != GameState.Title || upgradePanel == null)
                return;
            State = GameState.Upgrades;
            if (titlePanel != null)
                titlePanel.SetActive(false);
            upgradePanel.SetActive(true);
            if (upgradeMenu != null)
                upgradeMenu.Refresh();
        }

        public void CloseUpgrades()
        {
            if (State == GameState.Upgrades)
                ShowTitle();
        }

        public void OpenSkins()
        {
            if (State != GameState.Title || skinPanel == null)
                return;
            State = GameState.Skins;
            if (titlePanel != null)
                titlePanel.SetActive(false);
            skinPanel.SetActive(true);
            if (upgradeMenu != null)
                upgradeMenu.Refresh();
            extrasUI?.Refresh();
        }

        public void CloseSkins()
        {
            if (State == GameState.Skins)
                ShowTitle();
        }

        public void StartTutorial()
        {
            if (State != GameState.Title && State != GameState.Settings)
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
            settingsPanel.SetActive(false);
            State = GameState.Tutorial;
            SetUI(true, false, false);
            tutorial.Begin(this, playerController, gameTimer, runManager,
                FindFirstObjectByType<PatternSpawner>(), titlePanel.GetComponentInParent<Canvas>(), assets);
            if (tutorialSkipUI != null)
            {
                tutorialSkipUI.SetActive(true);
                tutorialSkipUI.transform.SetAsLastSibling();
            }
            if (tutorialSkipConfirmation != null)
            {
                tutorialSkipConfirmation.SetActive(false);
                tutorialSkipConfirmation.transform.SetAsLastSibling();
            }
        }

        public void ShowSkipConfirmation()
        {
            if (State != GameState.Tutorial || skipConfirmationOpen || tutorialSkipConfirmation == null)
                return;
            skipConfirmationOpen = true;
            timeScaleBeforeSkip = Time.timeScale;
            Time.timeScale = 0f;
            tutorialSkipConfirmation.SetActive(true);
        }

        public void CancelSkipTutorial()
        {
            if (State == GameState.Tutorial && skipConfirmationOpen)
                CloseSkipConfirmation();
        }

        public void ConfirmSkipTutorial()
        {
            if (State != GameState.Tutorial || !skipConfirmationOpen)
                return;
            PlayerPrefs.SetInt(TutorialCompletedKey, 1);
            PlayerPrefs.Save();
            FinishTutorial(true);
        }

        private void CloseSkipConfirmation()
        {
            if (!skipConfirmationOpen)
                return;
            skipConfirmationOpen = false;
            Time.timeScale = timeScaleBeforeSkip;
            if (tutorialSkipConfirmation != null)
                tutorialSkipConfirmation.SetActive(false);
        }

        private void OnDestroy() => CloseSkipConfirmation();

        public void FinishTutorial(bool startRun)
        {
            if (State != GameState.Tutorial)
                return;
            CloseSkipConfirmation();
            if (tutorialSkipUI != null)
                tutorialSkipUI.SetActive(false);
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
            if (upgradePanel != null)
                upgradePanel.SetActive(false);
            if (skinPanel != null)
                skinPanel.SetActive(false);
            if (titlePanel != null)
                titlePanel.SetActive(true);
            if (upgradeMenu != null)
                upgradeMenu.Refresh();
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
            State = GameState.Dying;
            if (cameraFeedback != null)
            {
                cameraFeedback.StopAllFeedback();
                cameraFeedback.PlayGameOver();
            }
            if (playerController != null)
                playerController.enabled = false;
            if (powerUps != null)
                powerUps.ResetRunEffects();
            StartCoroutine(ShowDeathAndPrompt());
        }

        private IEnumerator ShowDeathAndPrompt()
        {
            SetUI(false, false, false);
            float elapsed = 0f;
            while (elapsed < deathPresentationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                playerController?.SetDeathPose(elapsed / deathPresentationDuration);
                yield return null;
            }
            if (inventory != null && !inventory.ReviveUsed)
            {
                State = GameState.RevivePrompt;
                extrasUI?.ShowRevive(revivePromptSeconds);
                float remaining = revivePromptSeconds;
                while (State == GameState.RevivePrompt && remaining > 0f)
                {
                    remaining -= Time.unscaledDeltaTime;
                    extrasUI?.SetReviveCountdown(remaining);
                    yield return null;
                }
                if (State != GameState.RevivePrompt)
                    yield break;
                extrasUI?.HideRevive();
            }
            FinalizeRun();
        }

        public void UseCouponRevive()
        {
            if (State != GameState.RevivePrompt || inventory == null || !inventory.TrySpendCoupon())
                return;
            inventory.Revive();
            extrasUI?.HideRevive();
            playerController?.RestoreDeathPose();
            gameTimer.ReviveRun();
            if (playerController != null)
                playerController.enabled = true;
            gameOverHandled = false;
            State = GameState.Playing;
            SetUI(true, false, false);
        }

        public void ClickRewardedAd()
        {
            if (State == GameState.RevivePrompt)
                Debug.Log("Rewarded Ad not implemented yet");
        }

        public void GiveUpRevive()
        {
            if (State != GameState.RevivePrompt)
                return;
            extrasUI?.HideRevive();
            FinalizeRun();
        }

        private void FinalizeRun()
        {
            State = GameState.GameOver;
            wallet?.BankRunCoins();
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
            if (inventory != null && inventory.CurrentRunGiftBoxes > 0)
            {
                retryAfterBoxes = true;
                ConfirmResult();
                return;
            }
            startImmediatelyAfterReload = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }

        public void MainMenu()
        {
            if (State != GameState.GameOver && State != GameState.Result)
                return;
            ConfirmResult();
        }

        public void ConfirmResult()
        {
            if (State != GameState.Result && State != GameState.GameOver)
                return;
            giftBoxTotal = inventory != null ? inventory.CurrentRunGiftBoxes : 0;
            if (giftBoxTotal <= 0)
            {
                ReturnToTitle();
                return;
            }
            State = GameState.GiftBoxes;
            SetUI(false, false, false);
            giftBoxIndex = 1;
            giftBoxRevealed = false;
            extrasUI?.ShowGiftPanel(giftBoxIndex, giftBoxTotal, "", false);
        }

        public void OpenOrContinueGiftBox()
        {
            if (State != GameState.GiftBoxes || inventory == null || giftBoxIndex > giftBoxTotal)
                return;
            if (!giftBoxRevealed)
            {
                string reward = inventory.RollAndGrantGiftReward();
                giftBoxRevealed = true;
                extrasUI?.ShowGiftPanel(giftBoxIndex, giftBoxTotal, reward, false);
                return;
            }
            inventory.ClearPendingReward();
            giftBoxIndex++;
            giftBoxRevealed = false;
            extrasUI?.ShowGiftPanel(giftBoxIndex, giftBoxTotal, "", giftBoxIndex > giftBoxTotal);
        }

        public void ConfirmAllGiftBoxes()
        {
            if (State != GameState.GiftBoxes || giftBoxIndex <= giftBoxTotal)
                return;
            ReturnToTitle();
        }

        private void ReturnToTitle()
        {
            extrasUI?.HideGiftPanel();
            startImmediatelyAfterReload = false;
            if (retryAfterBoxes)
                startImmediatelyAfterReload = true;
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
