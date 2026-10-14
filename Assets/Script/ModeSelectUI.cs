using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // Full-screen mode-select popup (2026-10-11 request, replaces the title's
    // "< DEADLINE >" selector): opened by the title's ModeSelect_Btn. Classic art
    // on the left, Deadline art on the right, split by the deco line. Tapping a
    // side (On Click → ChooseClassic / ChooseDeadline) shows its name and
    // description in the bottom panel and turns the other side black-and-white;
    // SELECT (On Click → Confirm) applies the mode and returns to the title.
    // The texts per mode are edited in the Inspector.
    public sealed class ModeSelectUI : MonoBehaviour
    {
        [Serializable]
        public sealed class ModeText
        {
            public GameMode mode;
            public string title = "DEADLINE";
            public string tagline = "4 Second Survival";
            [TextArea(2, 3)] public string description = "";
            public Color accent = Color.white;
        }

        [SerializeField] private Image deadlineArt;
        [SerializeField] private Image classicArt;
        [SerializeField] private RectTransform infoPanel;
        [SerializeField] private RectTransform selectButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text taglineText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField, Range(0f, 1f)] private float unselectedBrightness = 0.62f;
        [SerializeField] private ModeText[] modes =
        {
            new ModeText
            {
                mode = GameMode.Deadline, title = "DEADLINE", tagline = "4 Second Survival",
                description = "적을 처치하거나 Near Miss에 성공해\n4초의 생존 시간을 계속 갱신하세요.",
                accent = new Color(1f, 0.32f, 0.6f),
            },
            new ModeText
            {
                mode = GameMode.Classic, title = "CLASSIC", tagline = "No Time Limit",
                description = "시간제한 없이 게임의 액션과\nEndless Run을 즐길 수 있습니다.",
                accent = new Color(1f, 0.66f, 0.82f),
            },
        };

        private const float IntroDuration = 0.35f;
        private static readonly int GrayId = Shader.PropertyToID("_Gray");

        private GameMode selected;
        private Material deadlineMaterial;
        private Material classicMaterial;
        private float deadlineGray;
        private float classicGray;
        private float openedAt = -10f;
        private float chosenAt = -10f;
        private Vector2 deadlineHome;
        private Vector2 classicHome;
        private Vector2 panelHome;
        private Vector2 buttonHome;
        private bool homesCaptured;
        private CameraFeedbackController feedback;

        public GameMode Selected => selected;

        private void Awake()
        {
            Shader shader = Shader.Find("Deadline4Sec/UIGrayscale");
            if (shader != null)
            {
                if (deadlineArt != null)
                    deadlineArt.material = deadlineMaterial = new Material(shader);
                if (classicArt != null)
                    classicArt.material = classicMaterial = new Material(shader);
            }
            CaptureHomes();
        }

        private void OnDestroy()
        {
            if (deadlineMaterial != null)
                Destroy(deadlineMaterial);
            if (classicMaterial != null)
                Destroy(classicMaterial);
        }

        public void Open()
        {
            CaptureHomes();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            selected = GameModeState.CurrentGameMode;
            deadlineGray = selected == GameMode.Deadline ? 0f : 1f;
            classicGray = 1f - deadlineGray;
            openedAt = Time.unscaledTime;
            chosenAt = -10f;
            Refresh();
            Animate();
            Click();
        }

        public void Close() => gameObject.SetActive(false);

        public void ChooseDeadline() => Choose(GameMode.Deadline);
        public void ChooseClassic() => Choose(GameMode.Classic);

        public void Confirm()
        {
            GameModeState.Select(selected);
            Click();
            Close();
        }

        private void Choose(GameMode mode)
        {
            if (mode != selected)
                chosenAt = Time.unscaledTime;
            selected = mode;
            Refresh();
            Click();
        }

        private void Click()
        {
            if (feedback == null)
                feedback = FindFirstObjectByType<CameraFeedbackController>();
            if (feedback != null)
                feedback.PlayUIClick();
        }

        private void CaptureHomes()
        {
            if (homesCaptured)
                return;
            homesCaptured = true;
            if (deadlineArt != null)
                deadlineHome = deadlineArt.rectTransform.anchoredPosition;
            if (classicArt != null)
                classicHome = classicArt.rectTransform.anchoredPosition;
            if (infoPanel != null)
                panelHome = infoPanel.anchoredPosition;
            if (selectButton != null)
                buttonHome = selectButton.anchoredPosition;
        }

        private void Refresh()
        {
            ModeText text = Array.Find(modes, m => m.mode == selected);
            if (text == null)
                return;
            if (titleText != null)
                titleText.text = text.title;
            if (taglineText != null)
            {
                taglineText.text = text.tagline;
                taglineText.color = text.accent;
            }
            if (descriptionText != null)
                descriptionText.text = text.description;
        }

        private void Update() => Animate();

        private void Animate()
        {
            float step = Mathf.Clamp01(Time.unscaledDeltaTime * 10f);
            deadlineGray = Mathf.Lerp(deadlineGray, selected == GameMode.Deadline ? 0f : 1f, step);
            classicGray = Mathf.Lerp(classicGray, selected == GameMode.Classic ? 0f : 1f, step);
            Tint(deadlineArt, deadlineMaterial, deadlineGray);
            Tint(classicArt, classicMaterial, classicGray);

            // Intro: the two sides slide in from their own edges, the panel and
            // SELECT rise from the bottom.
            float t = Mathf.Clamp01((Time.unscaledTime - openedAt) / IntroDuration);
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            float slide = (1f - ease) * 260f;
            if (deadlineArt != null)
                deadlineArt.rectTransform.anchoredPosition = deadlineHome + new Vector2(slide, 0f);
            if (classicArt != null)
                classicArt.rectTransform.anchoredPosition = classicHome - new Vector2(slide, 0f);
            float rise = (1f - ease) * 420f;
            if (infoPanel != null)
                infoPanel.anchoredPosition = panelHome - new Vector2(0f, rise);
            if (selectButton != null)
                selectButton.anchoredPosition = buttonHome - new Vector2(0f, rise);

            // A short punch on the mode name whenever the choice changes.
            if (titleText != null)
            {
                float p = (Time.unscaledTime - chosenAt) / 0.22f;
                titleText.rectTransform.localScale = Vector3.one * (p < 1f ? 1f + Mathf.Sin(p * Mathf.PI) * 0.16f : 1f);
            }
        }

        private void Tint(Image art, Material material, float gray)
        {
            if (art == null)
                return;
            if (material != null)
                material.SetFloat(GrayId, gray);
            float brightness = Mathf.Lerp(1f, unselectedBrightness, gray);
            art.color = new Color(brightness, brightness, brightness, 1f);
        }
    }
}
