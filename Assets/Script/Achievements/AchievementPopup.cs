using TMPro;
using UnityEngine;

namespace Deadline4Sec
{
    // Prototype "ACHIEVEMENT UNLOCKED" banner under the top HUD (Canvas/
    // AchievementPopup, built by Deadline 4 Sec/UI/Apply Achievements). Shows
    // AchievementManager's queue one at a time: drops in, holds, fades out.
    // It never takes taps. Final art will replace the children; the texts are
    // assigned here.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class AchievementPopup : MonoBehaviour
    {
        [SerializeField] private RectTransform body;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.25f;
        [SerializeField] private float dropDistance = 46f;

        private CanvasGroup group;
        private Vector2 home;
        private float shownAt = -100f;
        private float holdSeconds;
        private bool showing;

        public bool IsShowing => showing;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            if (body != null)
                home = body.anchoredPosition;
        }

        private void OnEnable() => AchievementManager.EnsureInitialized();

        private void Update()
        {
            float now = Time.unscaledTime;
            if (!showing)
            {
                if (AchievementManager.TryDequeuePopup(out AchievementDefinition next))
                    Show(next, now);
                return;
            }

            float t = now - shownAt;
            float fadeIn = Mathf.Clamp01(t / fadeSeconds);
            float fadeOut = Mathf.Clamp01((t - fadeSeconds - holdSeconds) / fadeSeconds);
            group.alpha = Mathf.Min(fadeIn, 1f - fadeOut);
            if (body != null)
            {
                float ease = 1f - Mathf.Pow(1f - fadeIn, 3f);
                body.anchoredPosition = home + new Vector2(0f, (1f - ease) * dropDistance + fadeOut * dropDistance * 0.4f);
            }
            if (fadeOut >= 1f)
            {
                showing = false;
                group.alpha = 0f;
            }
        }

        private void Show(AchievementDefinition definition, float now)
        {
            showing = true;
            shownAt = now;
            holdSeconds = Mathf.Max(0.5f, AchievementManager.PopupSeconds - fadeSeconds * 2f);
            if (nameText != null)
                nameText.text = definition.displayName;
            if (descriptionText != null)
                descriptionText.text = definition.description;
            transform.SetAsLastSibling();
            CameraFeedbackController feedback = FindFirstObjectByType<CameraFeedbackController>();
            if (feedback != null)
                feedback.PlayMenuResult(true);
        }
    }
}
