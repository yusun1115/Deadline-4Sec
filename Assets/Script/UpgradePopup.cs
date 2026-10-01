using TMPro;
using UnityEngine;

namespace Deadline4Sec
{
    // Modal result message for the upgrade screen ("업그레이드에 성공하였습니다!" /
    // "Coin이 부족합니다!"). Tapping OK or the dim backdrop closes it.
    public sealed class UpgradePopup : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private RectTransform dialog;

        private float shownAt;

        public void Show(string message, bool success)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (messageText != null)
            {
                messageText.text = message;
                messageText.color = success ? new Color(0.55f, 1f, 0.65f) : new Color(1f, 0.45f, 0.5f);
            }
            shownAt = Time.unscaledTime;
            FindFirstObjectByType<CameraFeedbackController>()?.PlayMenuResult(success);
        }

        public void Hide() => gameObject.SetActive(false);

        private void Update()
        {
            if (dialog == null)
                return;
            float t = Mathf.Clamp01((Time.unscaledTime - shownAt) / 0.18f);
            float s = t < 1f ? Mathf.Lerp(0.7f, 1f, 1f - (1f - t) * (1f - t)) * (1f + Mathf.Sin(t * Mathf.PI) * 0.08f) : 1f;
            dialog.localScale = Vector3.one * s;
        }
    }
}
