using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    [DefaultExecutionOrder(100)]
    public sealed class RunManager : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private GameTimer gameTimer;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text comboText;
        [SerializeField] private Text distanceText;

        [Header("Base points")]
        [SerializeField, Min(0)] private int groundEnemyPoints = 100;
        [SerializeField, Min(0)] private int airEnemyPoints = 100;
        [SerializeField, Min(0)] private int stompPoints = 150;
        [SerializeField, Min(0)] private int groundSlamEnemyPoints = 150;
        [SerializeField, Min(0)] private int nearMissPoints = 150;

        [Header("Combo")]
        [SerializeField, Min(0.1f)] private float comboTimeout = 2.5f;
        [SerializeField, Min(1)] private int doubleComboAt = 5;
        [SerializeField, Min(1)] private int tripleComboAt = 10;
        [SerializeField, Min(1)] private int quadrupleComboAt = 20;
        [SerializeField, Min(1)] private int doubleMultiplier = 2;
        [SerializeField, Min(1)] private int tripleMultiplier = 3;
        [SerializeField, Min(1)] private int quadrupleMultiplier = 4;

        [Header("Base forward speed only")]
        [SerializeField, Min(0f)] private float baseForwardSpeed = 10.2f;
        [SerializeField, Min(0f)] private float maxForwardSpeed = 16f;
        [SerializeField, Min(0f)] private float speedIncreasePerSecond = 0.06f;

        private float startZ;
        private float lastSuccessTime = float.NegativeInfinity;
        private TMP_Text nearMissText;
        private float nearMissVisibleUntil;
        private bool practice;
        private float practiceSpeed;
        private PowerUpManager powerUps;
        private RunInventory inventory;

        public event System.Action NearMissRecorded;

        public int CurrentScore { get; private set; }
        public int CurrentCombo { get; private set; }
        public int BestCombo { get; private set; }
        public float Distance { get; private set; }
        public float CurrentRunTime { get; private set; }
        public float CurrentForwardSpeed { get; private set; }
        public float BaseForwardSpeed => baseForwardSpeed;
        public float MaxForwardSpeed => maxForwardSpeed;
        public int ScoreMultiplier => CurrentCombo >= quadrupleComboAt ? quadrupleMultiplier :
            CurrentCombo >= tripleComboAt ? tripleMultiplier :
            CurrentCombo >= doubleComboAt ? doubleMultiplier : 1;

        private void Awake()
        {
            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerController>();
            if (gameTimer == null)
                gameTimer = FindFirstObjectByType<GameTimer>();
            powerUps = FindFirstObjectByType<PowerUpManager>();
            inventory = FindFirstObjectByType<RunInventory>();
            GameFont.Apply(scoreText);
            GameFont.Apply(comboText);
            GameFont.Apply(distanceText);
        }

        private void Start()
        {
            if (playerController != null)
                startZ = playerController.transform.position.z;
            CurrentForwardSpeed = baseForwardSpeed;
            if (playerController != null)
                playerController.SetRunForwardSpeed(CurrentForwardSpeed);
            CreateNearMissFeedback();
            UpdateDebugUI();
        }

        private void Update()
        {
            if (nearMissText != null && nearMissText.gameObject.activeSelf &&
                (gameTimer == null || !gameTimer.IsRunning ||
                 Time.unscaledTime >= nearMissVisibleUntil))
                nearMissText.gameObject.SetActive(false);
            if (playerController == null || gameTimer == null || !gameTimer.IsRunning)
                return;

            CurrentRunTime += Time.deltaTime;
            Distance = Mathf.Max(Distance, playerController.transform.position.z - startZ);

            if (CurrentCombo > 0)
            {
                if (powerUps != null && powerUps.ComboSealed)
                    lastSuccessTime += Time.deltaTime;
                else if (Time.time - lastSuccessTime >= comboTimeout)
                    CurrentCombo = 0;
            }

            CurrentForwardSpeed = practice ? practiceSpeed : Mathf.Min(maxForwardSpeed,
                baseForwardSpeed + CurrentRunTime * speedIncreasePerSecond) *
                (powerUps != null ? powerUps.SpeedMultiplier : 1f) +
                (inventory != null ? inventory.DashBonusSpeed : 0f);
            playerController.SetRunForwardSpeed(CurrentForwardSpeed);
            UpdateDebugUI();
        }

        public void BeginRun()
        {
            practice = false;
            startZ = playerController != null ? playerController.transform.position.z : 0f;
            CurrentRunTime = 0f;
            Distance = 0f;
            CurrentForwardSpeed = baseForwardSpeed;
            if (playerController != null)
                playerController.SetRunForwardSpeed(CurrentForwardSpeed);
            UpdateDebugUI();
        }

        public void BeginPractice(float speed)
        {
            BeginRun();
            practice = true;
            practiceSpeed = Mathf.Max(0f, speed);
            CurrentForwardSpeed = practiceSpeed;
            CurrentScore = CurrentCombo = BestCombo = 0;
            lastSuccessTime = float.NegativeInfinity;
            if (nearMissText != null)
                nearMissText.gameObject.SetActive(false);
            playerController.SetRunForwardSpeed(practiceSpeed);
            UpdateDebugUI();
        }

        public void RecordEnemyKill(Enemy enemy, bool isStomp)
        {
            if (enemy == null || gameTimer == null || !gameTimer.IsRunning)
                return;

            int points = isStomp ? stompPoints :
                enemy.Type == Enemy.EnemyType.Air ? airEnemyPoints : groundEnemyPoints;
            RecordSuccess(points);
        }

        public void RecordNearMiss()
        {
            if (gameTimer == null || !gameTimer.IsRunning)
                return;

            RecordSuccess(nearMissPoints);
            NearMissRecorded?.Invoke();
            if (nearMissText != null)
            {
                nearMissText.text = "NEAR MISS +" + nearMissPoints;
                nearMissText.gameObject.SetActive(true);
                nearMissVisibleUntil = Time.unscaledTime + 0.75f;
            }
        }

        private void CreateNearMissFeedback()
        {
            if (scoreText == null)
                return;
            GameObject label = new GameObject("NearMissFeedback",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(scoreText.transform.parent, false);
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.72f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(460f, 80f);
            nearMissText = label.GetComponent<TextMeshProUGUI>();
            GameFont.Apply(nearMissText);
            nearMissText.alignment = TextAlignmentOptions.Center;
            nearMissText.fontSize = 38f;
            nearMissText.color = new Color(1f, 0.28f, 0.5f);
            nearMissText.raycastTarget = false;
            label.SetActive(false);
        }

        public void RecordGroundSlamKill(Enemy enemy)
        {
            if (enemy == null || gameTimer == null || !gameTimer.IsRunning)
                return;

            RecordSuccess(groundSlamEnemyPoints);
        }

        private void RecordSuccess(int basePoints)
        {
            if (CurrentCombo > 0 && (powerUps == null || !powerUps.ComboSealed) &&
                Time.time - lastSuccessTime >= comboTimeout)
                CurrentCombo = 0;

            CurrentCombo++;
            BestCombo = Mathf.Max(BestCombo, CurrentCombo);
            lastSuccessTime = Time.time;
            CurrentScore += basePoints * ScoreMultiplier *
                (powerUps != null ? powerUps.ScoreMultiplier : 1);
            UpdateDebugUI();
        }

        private void UpdateDebugUI()
        {
            if (scoreText != null)
                scoreText.text = "SCORE " + CurrentScore.ToString(CultureInfo.InvariantCulture);
            if (comboText != null)
                comboText.text = "COMBO x" + CurrentCombo.ToString(CultureInfo.InvariantCulture);
            if (distanceText != null)
                distanceText.text = "DISTANCE " +
                    Mathf.FloorToInt(Distance).ToString(CultureInfo.InvariantCulture) + "m";
        }

        private void OnValidate()
        {
            tripleComboAt = Mathf.Max(doubleComboAt + 1, tripleComboAt);
            quadrupleComboAt = Mathf.Max(tripleComboAt + 1, quadrupleComboAt);
            maxForwardSpeed = Mathf.Max(baseForwardSpeed, maxForwardSpeed);
        }
    }
}
