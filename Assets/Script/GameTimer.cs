using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Deadline4Sec
{
    public sealed class GameTimer : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Text timeText;
        [SerializeField] private Text gameOverText;

        private const float Duration = 4f;
        private float remainingTime;
        private bool isGameOver;

        public float RemainingTime => remainingTime;
        public bool IsGameOver => isGameOver;

        private void Start()
        {
            ResetTimer();
        }

        private void Update()
        {
            if (ResetKeyPressed())
            {
                ResetTimer();
                return;
            }

            if (isGameOver)
                return;

            remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
            UpdateTimeText();

            if (remainingTime <= 0f)
                TriggerGameOver();
        }

        // Enemy kills and near misses can call this later.
        public void ResetTimer()
        {
            remainingTime = Duration;
            isGameOver = false;

            if (playerController != null)
                playerController.enabled = true;
            if (gameOverText != null)
                gameOverText.gameObject.SetActive(false);

            UpdateTimeText();
        }

        public void TriggerGameOver()
        {
            if (isGameOver)
                return;

            isGameOver = true;
            remainingTime = 0f;
            UpdateTimeText();

            if (playerController != null)
                playerController.enabled = false;
            if (gameOverText != null)
            {
                gameOverText.text = "GAME OVER";
                gameOverText.gameObject.SetActive(true);
            }
        }

        private void UpdateTimeText()
        {
            if (timeText != null)
                timeText.text = remainingTime.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static bool ResetKeyPressed()
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
}
