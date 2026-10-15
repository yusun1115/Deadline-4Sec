using System.Globalization;
using TMPro;
using UnityEngine;

namespace Deadline4Sec
{
    // Title "LEADERBOARD" chooser (TitlePanel/LeaderboardPanel): the title's
    // lankingButton On Click → Open; DEADLINE MODE / CLASSIC MODE buttons →
    // OpenDeadline / OpenClassic, which hand over to Google Play's own
    // leaderboard UI; the backdrop and BackButton → Close. Each button also
    // shows the local best of its mode. The ranking list itself is Google's.
    public sealed class LeaderboardPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform dialog;
        [SerializeField] private TMP_Text deadlineBestText;
        [SerializeField] private TMP_Text classicBestText;
        [SerializeField] private TMP_Text statusText;

        private float shownAt;

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            shownAt = Time.unscaledTime;
            SetBest(deadlineBestText, GameMode.Deadline);
            SetBest(classicBestText, GameMode.Classic);
            SetStatus("", Color.white);
            Click();
        }

        public void Close()
        {
            Click();
            gameObject.SetActive(false);
        }

        public void OpenDeadline() => OpenMode(GameMode.Deadline);
        public void OpenClassic() => OpenMode(GameMode.Classic);

        private void OpenMode(GameMode mode)
        {
            Click();
            string name = GameModeState.DisplayName(mode);
            SetStatus(name + " MODE LEADERBOARD", Color.white);
            GooglePlayService.ShowLeaderboard(mode, opened =>
            {
                if (this == null)
                    return;
                if (!opened)
                    SetStatus("Google Play Games\n로그인이 필요합니다.", new Color(1f, 0.55f, 0.6f));
                else if (GooglePlayService.IsMock)
                    SetStatus(name + " MODE LEADERBOARD\n<size=80%>(에디터: Android 빌드에서 Google Play 화면이 열립니다)</size>",
                        new Color(0.75f, 0.9f, 1f));
            });
        }

        private void Update()
        {
            if (dialog == null)
                return;
            float t = Mathf.Clamp01((Time.unscaledTime - shownAt) / 0.18f);
            float s = t < 1f ? Mathf.Lerp(0.8f, 1f, 1f - (1f - t) * (1f - t)) * (1f + Mathf.Sin(t * Mathf.PI) * 0.06f) : 1f;
            dialog.localScale = Vector3.one * s;
        }

        private static void SetBest(TMP_Text text, GameMode mode)
        {
            if (text == null)
                return;
            int best = Mathf.Max(0, PlayerPrefs.GetInt(RecordKeys.BestScore(mode), 0));
            text.text = "MY BEST  " + best.ToString("N0", CultureInfo.InvariantCulture);
        }

        private void SetStatus(string message, Color color)
        {
            if (statusText == null)
                return;
            statusText.text = message;
            statusText.color = color;
        }

        private void Click()
        {
            CameraFeedbackController feedback = FindFirstObjectByType<CameraFeedbackController>();
            if (feedback != null)
                feedback.PlayUIClick();
        }
    }
}
