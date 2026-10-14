using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Deadline4Sec
{
    // Result screen presentation (gameover.md, 2026-10-09). GameFlowManager still
    // decides when the result opens, saves records and fills the values; this only
    // stages what is already there:
    //   dark overlay + frame pop (85% → 103% → 100%)
    //   → GAME OVER → score counts up → distance/combo/time
    //   → NEW BEST (only after the count, only if a record fell) and personal bests
    //   → run reward (coins, gift boxes) → buttons.
    // Two buttons side by side, MAIN MENU | RETRY, whether or not boxes were
    // earned (both already open the earned boxes first via GameFlowManager).
    // A tap anywhere skips straight to the end state.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ResultPresenter : MonoBehaviour
    {
        [Header("Overlay (sibling behind the panel)")]
        [SerializeField] private Image overlay;
        [SerializeField, Range(0f, 1f)] private float overlayAlpha = 0.6f;

        [Header("Panel pop")]
        [SerializeField, Min(0.05f)] private float resultAppearDuration = 0.23f;
        [SerializeField, Range(0.5f, 1f)] private float resultStartScale = 0.85f;
        [SerializeField, Range(1f, 1.2f)] private float resultOvershootScale = 1.03f;

        [Header("Sequence")]
        [SerializeField, Min(0.1f)] private float scoreCountDuration = 0.65f;
        [SerializeField, Min(0f)] private float stepDelay = 0.08f;

        [Header("Parts (each has a CanvasGroup)")]
        [SerializeField] private CanvasGroup gameOver;
        [SerializeField] private CanvasGroup[] scoreBlock;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private CanvasGroup[] stats;
        [SerializeField] private CanvasGroup newBest;
        [SerializeField] private CanvasGroup[] personalBest;
        [SerializeField] private CanvasGroup[] reward;
        [SerializeField] private TMP_Text rewardCoinText;
        [SerializeField] private TMP_Text rewardGiftText;
        [SerializeField] private CanvasGroup mainMenuButton;
        [SerializeField] private CanvasGroup retryButton;

        [Header("Sound hooks (optional; empty = placeholder synth or silent)")]
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip scoreTickSound;
        [SerializeField] private AudioClip newBestSound;
        [SerializeField] private AudioClip buttonSound;

        private CanvasGroup panel;
        private AudioSource audioSource;
        private int finalScore;
        private bool isNewBest;
        private int runCoins;
        private int runGiftBoxes;
        private bool playing;
        private bool skipRequested;
        private float fitScale = 1f;

        public void Prepare(int score, bool newBestRecord, int coins, int giftBoxes, GameMode mode = GameMode.Deadline)
        {
            finalScore = Mathf.Max(0, score);
            isNewBest = newBestRecord;
            runCoins = Mathf.Max(0, coins);
            runGiftBoxes = Mathf.Max(0, giftBoxes);
            // The mode names the score and the personal bests it is compared with.
            string name = GameModeState.DisplayName(mode);
            SetCaption("ScoreLabel", name + " SCORE");
            SetCaption("PersonalBestLabel", name + " BEST");
        }

        private void SetCaption(string child, string text)
        {
            Transform t = transform.Find(child);
            if (t != null && t.TryGetComponent(out TMP_Text label))
                label.text = text;
        }

        public void Play()
        {
            if (!isActiveAndEnabled)
                return;
            StopAllCoroutines();
            StartCoroutine(Sequence());
        }

        private void Awake()
        {
            panel = GetComponent<CanvasGroup>();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            foreach (CanvasGroup button in new[] { mainMenuButton, retryButton })
                if (button != null && button.TryGetComponent(out Button b))
                    b.onClick.AddListener(() => Sound(buttonSound, null, 0.6f));
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            playing = false;
            if (overlay != null)
                overlay.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (playing && PointerPressed())
                skipRequested = true;
        }

        // ---------------- sequence ----------------

        private IEnumerator Sequence()
        {
            playing = true;
            skipRequested = false;
            ApplyRewardsAndButtons();
            fitScale = FitToScreen();
            SetAll(0f);
            panel.alpha = 0f;
            if (scoreText != null)
                scoreText.text = "0";
            if (overlay != null)
            {
                overlay.gameObject.SetActive(true);
                SetAlpha(overlay, 0f);
            }
            Sound(openSound, null, 0.7f);

            // Overlay fades in under a frame pop: start → overshoot → 1.
            float popIn = resultAppearDuration * 0.65f, settle = resultAppearDuration - popIn;
            for (float t = 0f; t < resultAppearDuration && !skipRequested; t += Time.unscaledDeltaTime)
            {
                float s = t < popIn ? Mathf.Lerp(resultStartScale, resultOvershootScale, EaseOut(t / popIn))
                    : Mathf.Lerp(resultOvershootScale, 1f, (t - popIn) / settle);
                transform.localScale = Vector3.one * (s * fitScale);
                panel.alpha = Mathf.Clamp01(t / popIn);
                SetAlpha(overlay, overlayAlpha * Mathf.Clamp01(t / popIn));
                yield return null;
            }
            transform.localScale = Vector3.one * fitScale;
            panel.alpha = 1f;
            SetAlpha(overlay, overlayAlpha);

            yield return Wait(stepDelay);
            StartCoroutine(Pop(gameOver, 1.3f, 0.16f));
            yield return Wait(stepDelay);

            // Score counts up; the run stats slide in alongside.
            foreach (CanvasGroup part in scoreBlock)
                StartCoroutine(Fade(part, 0.1f));
            StartCoroutine(Stagger(stats, 0.15f, 0.06f));
            float nextTick = 0f;
            for (float t = 0f; t < scoreCountDuration && !skipRequested; t += Time.unscaledDeltaTime)
            {
                float k = EaseOut(t / scoreCountDuration);
                if (scoreText != null)
                    scoreText.text = Mathf.RoundToInt(finalScore * k).ToString("N0", CultureInfo.InvariantCulture);
                if (t >= nextTick && finalScore > 0)
                {
                    nextTick = t + 0.06f;
                    Sound(scoreTickSound, "Tick", 0.25f, 1f + k * 0.6f);
                }
                yield return null;
            }
            if (scoreText != null)
            {
                scoreText.text = finalScore.ToString("N0", CultureInfo.InvariantCulture);
                StartCoroutine(Punch(scoreText.rectTransform, 1.12f, 0.14f));
            }

            // Records: NEW BEST only after the count, personal bests stay quiet.
            if (newBest != null)
            {
                newBest.gameObject.SetActive(isNewBest);
                if (isNewBest)
                {
                    StartCoroutine(Pop(newBest, 1.7f, 0.22f));
                    Sound(newBestSound, "NearChime", 0.8f, 1.1f);
                }
            }
            StartCoroutine(Stagger(personalBest, 0.12f, 0.03f));
            yield return Wait(stepDelay);
            StartCoroutine(Stagger(reward, 0.12f, 0.03f));
            yield return Wait(stepDelay);

            // Buttons last, then they take taps.
            foreach (CanvasGroup button in new[] { mainMenuButton, retryButton })
            {
                if (button == null || !button.gameObject.activeSelf)
                    continue;
                StartCoroutine(Pop(button, 1.2f, 0.14f));
                yield return Wait(0.04f);
            }
            Finish();
        }

        // End state; also where a skip lands.
        private void Finish()
        {
            StopAllCoroutines();
            playing = false;
            transform.localScale = Vector3.one * fitScale;
            panel.alpha = 1f;
            SetAlpha(overlay, overlayAlpha);
            SetAll(1f);
            if (scoreText != null)
            {
                scoreText.text = finalScore.ToString("N0", CultureInfo.InvariantCulture);
                scoreText.rectTransform.localScale = Vector3.one;
            }
            if (newBest != null)
                newBest.gameObject.SetActive(isNewBest);
        }

        private IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                if (skipRequested)
                {
                    Finish();
                    yield break;
                }
                yield return null;
            }
        }

        // ---------------- helpers ----------------

        private void ApplyRewardsAndButtons()
        {
            if (rewardCoinText != null)
                rewardCoinText.text = "+" + runCoins.ToString("N0", CultureInfo.InvariantCulture);
            if (rewardGiftText != null)
                rewardGiftText.text = "x" + runGiftBoxes;
        }

        // Shrinks the frame on short screens (e.g. landscape PC) so nothing is cut off.
        private float FitToScreen()
        {
            var parent = transform.parent as RectTransform;
            var self = (RectTransform)transform;
            if (parent == null)
                return 1f;
            float height = parent.rect.height - 40f, width = parent.rect.width - 24f;
            return Mathf.Min(1f, height / Mathf.Max(1f, self.rect.height), width / Mathf.Max(1f, self.rect.width));
        }

        private void SetAll(float alpha)
        {
            Set(gameOver, alpha);
            foreach (CanvasGroup[] list in new[] { scoreBlock, stats, personalBest, reward })
                if (list != null)
                    foreach (CanvasGroup part in list)
                        Set(part, alpha);
            Set(newBest, alpha);
            Set(mainMenuButton, alpha);
            Set(retryButton, alpha);
        }

        // Hidden parts don't take taps; revealed ones do.
        private static void Set(CanvasGroup group, float alpha)
        {
            if (group == null)
                return;
            group.alpha = alpha;
            group.interactable = group.blocksRaycasts = alpha > 0.5f;
            group.transform.localScale = Vector3.one;
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
                return;
            Color c = graphic.color;
            c.a = alpha;
            graphic.color = c;
        }

        private IEnumerator Fade(CanvasGroup group, float duration)
        {
            if (group == null)
                yield break;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                group.alpha = t / duration;
                yield return null;
            }
            Set(group, 1f);
        }

        private IEnumerator Stagger(CanvasGroup[] groups, float duration, float gap)
        {
            if (groups == null)
                yield break;
            foreach (CanvasGroup group in groups)
            {
                StartCoroutine(Fade(group, duration));
                for (float t = 0f; t < gap; t += Time.unscaledDeltaTime)
                    yield return null;
            }
        }

        // Fade in while scaling from `from` down to 1 with a little overshoot.
        private IEnumerator Pop(CanvasGroup group, float from, float duration)
        {
            if (group == null)
                yield break;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                group.alpha = Mathf.Clamp01(k * 2f);
                group.transform.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, OutBack(k));
                yield return null;
            }
            Set(group, 1f);
        }

        private IEnumerator Punch(RectTransform rect, float peak, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                rect.localScale = Vector3.one * Mathf.Lerp(peak, 1f, t / duration);
                yield return null;
            }
            rect.localScale = Vector3.one;
        }

        private void Sound(AudioClip clip, string synth, float volume, float pitch = 1f)
        {
            if (!GamePreferences.SoundEnabled || audioSource == null)
                return;
            AudioClip play = clip != null ? clip : synth != null ? SfxSynth.Get(synth) : null;
            if (play == null)
                return;
            audioSource.pitch = pitch;
            SoundLibrary library = SoundLibrary.Instance;
            audioSource.PlayOneShot(play, volume * (library != null ? library.sfxVolume : 0.8f));
        }

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        private static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            float u = Mathf.Clamp01(t) - 1f;
            return 1f + (c1 + 1f) * u * u * u + c1 * u * u;
        }

        private static bool PointerPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }
    }
}
