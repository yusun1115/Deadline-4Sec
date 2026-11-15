using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // Title "LEADERBOARD" chooser (TitlePanel/LeaderboardPanel): the title's
    // lankingButton On Click → Open; DEADLINE MODE / CLASSIC MODE buttons →
    // OpenDeadline / OpenClassic, which hand over to Google Play's own
    // leaderboard UI; the backdrop and BackButton → Close. Each button also
    // shows the local best of its mode. The ranking list itself is Google's.
    // Exhibition mode (2026-11-15): a third button, EXHIBITION, under the two,
    // opens the local exhibition ranking (ExhibitionUI); the Google Play
    // buttons are untouched.
    public sealed class LeaderboardPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform dialog;
        [SerializeField] private TMP_Text deadlineBestText;
        [SerializeField] private TMP_Text classicBestText;
        [SerializeField] private TMP_Text statusText;

        private float shownAt;
        private GameObject exhibitionButton;
        private TMP_Text exhibitionInfo;

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            shownAt = Time.unscaledTime;
            SetBest(deadlineBestText, GameMode.Deadline);
            SetBest(classicBestText, GameMode.Classic);
            SetStatus("", Color.white);
            AddExhibitionButton();
            Click();
        }

        public void OpenExhibition()
        {
            Click();
            ExhibitionUI exhibition = FindFirstObjectByType<ExhibitionUI>();
            if (exhibition != null)
                exhibition.ShowRankingView();
        }

        // Runtime only (the scene is not changed): the dialog grows by one
        // button, the Classic button is copied for the look.
        private void AddExhibitionButton()
        {
            if (!ExhibitionMode.IsActive || dialog == null)
                return;
            if (exhibitionButton == null)
            {
                Transform classic = dialog.Find("ClassicButton");
                if (classic == null)
                    return;
                const float Step = 188f;
                const float Shift = Step * 0.5f;
                foreach (RectTransform child in dialog)
                {
                    Vector2 at = child.anchoredPosition;
                    child.anchoredPosition = child.name == "StatusText" || child.name == "BottomAccent"
                        ? at + new Vector2(0f, -Step + Shift)
                        : at + new Vector2(0f, Shift);
                }
                dialog.sizeDelta += new Vector2(0f, Step);
                exhibitionButton = Instantiate(classic.gameObject, dialog);
                exhibitionButton.name = "ExhibitionButton";
                RectTransform rect = (RectTransform)exhibitionButton.transform;
                rect.anchoredPosition = ((RectTransform)classic).anchoredPosition + new Vector2(0f, -Step);
                Button button = exhibitionButton.GetComponent<Button>();
                button.onClick = new Button.ButtonClickedEvent(); // not the copied OpenClassic
                button.onClick.AddListener(OpenExhibition);
                Transform label = exhibitionButton.transform.Find("Label");
                if (label != null && label.TryGetComponent(out TMP_Text title))
                    title.text = "EXHIBITION";
                Transform best = exhibitionButton.transform.Find("BestText");
                if (best != null)
                    best.TryGetComponent(out exhibitionInfo);
                Transform stripe = exhibitionButton.transform.Find("Stripe");
                if (stripe != null && stripe.TryGetComponent(out Image bar))
                    bar.color = new Color(1f, 0.83f, 0.35f);
            }
            if (exhibitionInfo != null)
            {
                var entries = ExhibitionLeaderboardService.Entries;
                exhibitionInfo.text = entries.Count > 0
                    ? "1위  " + entries[0].nickname + "  " + entries[0].score.ToString("N0", CultureInfo.InvariantCulture)
                    : "전시 랭킹";
            }
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
