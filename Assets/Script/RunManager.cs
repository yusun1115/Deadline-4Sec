using System.Globalization;
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

        public int CurrentScore { get; private set; }
        public int CurrentCombo { get; private set; }
        public int BestCombo { get; private set; }
        public float Distance { get; private set; }
        public float CurrentRunTime { get; private set; }
        public float CurrentForwardSpeed { get; private set; }
        public int ScoreMultiplier => CurrentCombo >= quadrupleComboAt ? quadrupleMultiplier :
            CurrentCombo >= tripleComboAt ? tripleMultiplier :
            CurrentCombo >= doubleComboAt ? doubleMultiplier : 1;

        private void Awake()
        {
            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerController>();
            if (gameTimer == null)
                gameTimer = FindFirstObjectByType<GameTimer>();
        }

        private void Start()
        {
            if (playerController != null)
                startZ = playerController.transform.position.z;
            CurrentForwardSpeed = baseForwardSpeed;
            if (playerController != null)
                playerController.SetRunForwardSpeed(CurrentForwardSpeed);
            UpdateDebugUI();
        }

        private void Update()
        {
            if (playerController == null || gameTimer == null || gameTimer.IsGameOver)
                return;

            CurrentRunTime += Time.deltaTime;
            Distance = Mathf.Max(Distance, playerController.transform.position.z - startZ);

            if (CurrentCombo > 0 && Time.time - lastSuccessTime >= comboTimeout)
                CurrentCombo = 0;

            CurrentForwardSpeed = Mathf.Min(maxForwardSpeed,
                baseForwardSpeed + CurrentRunTime * speedIncreasePerSecond);
            playerController.SetRunForwardSpeed(CurrentForwardSpeed);
            UpdateDebugUI();
        }

        public void RecordEnemyKill(Enemy enemy, bool isStomp)
        {
            if (enemy == null || gameTimer == null || gameTimer.IsGameOver)
                return;

            int points = isStomp ? stompPoints :
                enemy.Type == Enemy.EnemyType.Air ? airEnemyPoints : groundEnemyPoints;
            RecordSuccess(points);
        }

        public void RecordNearMiss()
        {
            if (gameTimer == null || gameTimer.IsGameOver)
                return;

            RecordSuccess(nearMissPoints);
        }

        private void RecordSuccess(int basePoints)
        {
            if (CurrentCombo > 0 && Time.time - lastSuccessTime >= comboTimeout)
                CurrentCombo = 0;

            CurrentCombo++;
            BestCombo = Mathf.Max(BestCombo, CurrentCombo);
            lastSuccessTime = Time.time;
            CurrentScore += basePoints * ScoreMultiplier;
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
