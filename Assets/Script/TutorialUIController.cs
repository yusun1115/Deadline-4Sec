using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // Tutorial presentation (2026-11-01, Tutorial.md), built at runtime on the
    // TutorialHUD the TutorialController owns:
    //  - speaker: tutorial_ex pops in at the top left, the tutorial_ex_bubble
    //    unfolds to its right and the line is typed in,
    //  - highlight: the real timer pulses with a glow; with dim the rest of the
    //    screen darkens (the dim sits first in GameplayUI, so the timer frame
    //    and number are drawn above it, and the speaker/SKIP above that),
    //  - swipe guide: tutorial_swipe_arrow turned to the asked direction, and
    //    tutorial_swipe_hand swiping along it in a loop,
    //  - success pop, and tap-to-continue for briefing steps.
    // Everything it changed outside its own panel (HUD visibility, timer scale,
    // SKIP button placement) is put back by Cleanup.
    public sealed class TutorialUIController : MonoBehaviour
    {
        private struct HiddenHud
        {
            public CanvasGroup Group;
            public bool Added;
            public float Alpha;
            public bool BlocksRaycasts;
        }

        private TutorialPresentation look;
        private CameraFeedbackController feedback;
        private RectTransform portrait, bubble, guide, arrow, hand;
        private CanvasGroup guideGroup;
        private TMP_Text titleLabel, dialogue, tapHint, keyHint, successLabel;
        private Image dim, timerGlow, screenFade;
        private GameObject tapCatcher;
        private RectTransform timerFrame, timerNumber;
        private Vector3 timerFrameScale, timerNumberScale;
        private readonly List<HiddenHud> hiddenHud = new List<HiddenHud>();
        private RectTransform skipButton;
        private TMP_Text skipLabel;
        private Vector2 skipAnchorMin, skipAnchorMax, skipPivot, skipPosition, skipSize;
        private bool skipAutoSize;
        private float skipFontMax;
        private readonly List<Tween> highlightTweens = new List<Tween>();
        private Tween dimFade, successTween, screenFadeTween;
        private Sequence guideLoop;
        private Tween arrowNudge;
        private float typeStart;
        private int typeTotal;
        private Action onTap;
        private bool speakerShown, cleaned;

        public bool IsTyping { get; private set; }
        public float TextCompletedAt { get; private set; }
        public bool SpeakerVisible => speakerShown && portrait != null && portrait.gameObject.activeSelf;
        public string DialogueText => dialogue != null ? dialogue.text : null;
        public TutorialHighlight Highlight { get; private set; }
        public bool DimVisible => dim != null && dim.gameObject.activeSelf;
        public TutorialController.LessonInput GuideDirection { get; private set; }
        public float ArrowAngle => arrow != null ? arrow.localEulerAngles.z : 0f;
        public bool TapToContinue => tapCatcher != null && tapCatcher.activeSelf;
        public bool ScreenFaded => screenFade != null && screenFade.gameObject.activeSelf;

        public void Build(Canvas canvas, TutorialCourseAssets assets)
        {
            look = assets.presentation ?? new TutorialPresentation();
            feedback = FindFirstObjectByType<CameraFeedbackController>();
            RectTransform panel = (RectTransform)transform;

            // Tap anywhere (below the speaker and buttons) to go on.
            tapCatcher = NewImage("TapToContinue", panel, null, Color.clear).gameObject;
            Stretch((RectTransform)tapCatcher.transform);
            tapCatcher.GetComponent<Image>().raycastTarget = true;
            Button tap = tapCatcher.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(Tap);
            tapCatcher.SetActive(false);

            // Failed step: the screen (not the speaker) darkens while it resets.
            screenFade = NewImage("TutorialRetryFade", panel, null, new Color(0f, 0f, 0f, 0f));
            Stretch(screenFade.rectTransform);
            screenFade.gameObject.SetActive(false);

            // Speaker: portrait at the top left, bubble unfolding to its right.
            portrait = NewImage("tutorial_ex", panel, assets.portrait, Color.white).rectTransform;
            portrait.anchorMin = portrait.anchorMax = portrait.pivot = new Vector2(0f, 1f);
            portrait.sizeDelta = look.portraitSize;
            portrait.anchoredPosition = look.portraitPosition;
            bubble = NewImage("tutorial_ex_bubble", panel, assets.bubble, Color.white).rectTransform;
            bubble.anchorMin = bubble.anchorMax = bubble.pivot = new Vector2(0f, 1f);
            bubble.sizeDelta = look.bubbleSize;
            bubble.anchoredPosition = look.bubblePosition;
            // Text inside the bubble's outline (the tail is at the bottom left).
            Vector2 size = look.bubbleSize;
            titleLabel = BubbleText("Title", 24f, look.titleColor,
                new Vector2(size.x * 0.1f, -size.y * 0.16f), new Vector2(size.x * 0.6f, 30f));
            titleLabel.alignment = TextAlignmentOptions.Left;
            dialogue = BubbleText("Dialogue", look.dialogueFontSize, look.dialogueColor,
                new Vector2(size.x * 0.075f, -size.y * 0.15f - 32f), new Vector2(size.x * 0.84f, size.y * 0.56f));
            dialogue.alignment = TextAlignmentOptions.TopLeft;
            dialogue.textWrappingMode = TextWrappingModes.Normal;
            dialogue.fontSizeMin = 20f;
            tapHint = BubbleText("TapHint", 20f, look.titleColor,
                new Vector2(size.x * 0.6f, -size.y * 0.71f), new Vector2(size.x * 0.25f, 26f));
            tapHint.alignment = TextAlignmentOptions.Right;
            tapHint.text = "탭해서 계속";
            tapHint.gameObject.SetActive(false);
            portrait.gameObject.SetActive(false);
            bubble.gameObject.SetActive(false);

            // Swipe guide at the bottom center.
            guide = new GameObject("SwipeGuide", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            guide.SetParent(panel, false);
            guide.anchorMin = guide.anchorMax = new Vector2(0.5f, 0f);
            guide.anchoredPosition = new Vector2(0f, look.guideBottomOffset);
            guide.sizeDelta = new Vector2(320f, 320f);
            guideGroup = guide.GetComponent<CanvasGroup>();
            guideGroup.blocksRaycasts = false;
            arrow = NewImage("tutorial_swipe_arrow", guide, assets.swipeArrow, Color.white).rectTransform;
            arrow.sizeDelta = look.arrowSize;
            hand = NewImage("tutorial_swipe_hand", guide, assets.swipeHand, Color.white).rectTransform;
            hand.sizeDelta = look.handSize;
            hand.pivot = new Vector2(0.33f, 0.95f); // the fingertip
            keyHint = GameFlowManager.CreateMenuText(guide, "", new Vector2(0f, look.arrowSize.y * 0.5f + 44f),
                new Vector2(420f, 40f), 24f);
            keyHint.color = new Color(1f, 0.85f, 0.4f);
            guide.gameObject.SetActive(false);

            successLabel = GameFlowManager.CreateMenuText(panel, "", Vector2.zero, new Vector2(640f, 150f), 96f);
            successLabel.name = "SuccessPop";
            successLabel.rectTransform.anchorMin = successLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.6f);
            successLabel.color = look.successColor;
            successLabel.gameObject.SetActive(false);

            Transform hud = canvas.transform.Find("GameplayUI");
            if (hud != null)
                PrepareHud(hud);
            Transform skip = canvas.transform.Find("TutorialSkipUI/SkipButton");
            if (skip != null)
                MoveSkipButton((RectTransform)skip);
        }

        // The timer can only be lifted above a dim drawn before it: the dim
        // and glow go first in GameplayUI, the rest of the HUD is hidden.
        private void PrepareHud(Transform hud)
        {
            timerFrame = hud.Find("TimerFrame") as RectTransform;
            timerNumber = hud.Find("SurvivalTimer") as RectTransform;
            if (timerFrame != null)
                timerFrameScale = timerFrame.localScale;
            if (timerNumber != null)
                timerNumberScale = timerNumber.localScale;
            if (look.hiddenHud != null)
                foreach (Transform child in hud)
                {
                    if (Array.IndexOf(look.hiddenHud, child.name) < 0)
                        continue;
                    CanvasGroup group = child.GetComponent<CanvasGroup>();
                    bool added = group == null;
                    if (added)
                        group = child.gameObject.AddComponent<CanvasGroup>();
                    hiddenHud.Add(new HiddenHud { Group = group, Added = added, Alpha = group.alpha, BlocksRaycasts = group.blocksRaycasts });
                    group.alpha = 0f;
                    group.blocksRaycasts = false;
                }
            dim = NewImage("TutorialDim", hud, null, new Color(0f, 0f, 0f, look.dimAlpha));
            Stretch(dim.rectTransform);
            dim.rectTransform.SetAsFirstSibling();
            dim.gameObject.SetActive(false);
            // A glowing outline just around the timer frame (behind it).
            timerGlow = NewImage("TutorialTimerGlow", hud, FxSprites.EdgeGlowBox, look.timerGlowColor);
            timerGlow.type = Image.Type.Sliced;
            timerGlow.preserveAspect = false;
            RectTransform glow = timerGlow.rectTransform;
            glow.anchorMin = glow.anchorMax = new Vector2(0.5f, 1f);
            glow.anchoredPosition = timerFrame != null ? timerFrame.anchoredPosition +
                new Vector2(0f, (0.5f - timerFrame.pivot.y) * timerFrame.sizeDelta.y) : new Vector2(0f, -94f);
            glow.sizeDelta = (timerFrame != null ? timerFrame.sizeDelta : new Vector2(500f, 168f)) + new Vector2(40f, 10f);
            glow.SetSiblingIndex(1);
            timerGlow.gameObject.SetActive(false);
        }

        // SKIP stays tappable, but the highlighted timer needs the top centre:
        // during the tutorial it sits bottom right, opposite MENU.
        private void MoveSkipButton(RectTransform skip)
        {
            skipButton = skip;
            skipAnchorMin = skip.anchorMin;
            skipAnchorMax = skip.anchorMax;
            skipPivot = skip.pivot;
            skipPosition = skip.anchoredPosition;
            skipSize = skip.sizeDelta;
            skip.anchorMin = skip.anchorMax = new Vector2(1f, 0f);
            skip.pivot = new Vector2(0.5f, 0.5f);
            skip.anchoredPosition = new Vector2(-115f, 60f);
            skip.sizeDelta = new Vector2(180f, 65f);
            skipLabel = skip.GetComponentInChildren<TMP_Text>(true);
            if (skipLabel != null)
            {
                skipAutoSize = skipLabel.enableAutoSizing;
                skipFontMax = skipLabel.fontSizeMax;
                skipLabel.enableAutoSizing = true;
                skipLabel.fontSizeMax = Mathf.Min(skipLabel.fontSize, 36f);
            }
        }

        // The tutorial's end returns to the title by itself: no SKIP there.
        public void HideSkip()
        {
            if (skipButton != null)
                skipButton.gameObject.SetActive(false);
        }

        public void ShowDialogue(string title, string text)
        {
            titleLabel.text = title ?? "";
            dialogue.text = text ?? "";
            dialogue.maxVisibleCharacters = 0;
            dialogue.ForceMeshUpdate();
            typeTotal = dialogue.textInfo.characterCount;
            float delay = 0f;
            if (!speakerShown)
            {
                speakerShown = true;
                portrait.gameObject.SetActive(true);
                bubble.gameObject.SetActive(true);
                Vector2 home = portrait.anchoredPosition;
                portrait.anchoredPosition = home + new Vector2(-look.portraitSize.x, 0f);
                portrait.localScale = Vector3.one * 0.8f;
                DOTween.To(() => portrait.anchoredPosition, v => portrait.anchoredPosition = v, home, look.portraitEnterDuration)
                    .SetEase(Ease.OutBack).SetUpdate(true).SetLink(portrait.gameObject);
                portrait.DOScale(1f, look.portraitEnterDuration).SetEase(Ease.OutBack).SetUpdate(true).SetLink(portrait.gameObject);
                bubble.localScale = new Vector3(0f, 1f, 1f);
                bubble.DOScaleX(1f, look.bubbleEnterDuration).SetDelay(look.bubbleDelay).SetEase(Ease.OutQuad)
                    .SetUpdate(true).SetLink(bubble.gameObject);
                delay = look.bubbleDelay + look.bubbleEnterDuration;
            }
            else
            {
                bubble.DOKill(true);
                bubble.localScale = Vector3.one;
                bubble.DOPunchScale(new Vector3(0.03f, 0.05f, 0f), 0.18f, 6, 0.6f).SetUpdate(true).SetLink(bubble.gameObject);
            }
            if (look.typingCharsPerSecond > 0f)
            {
                IsTyping = true;
                typeStart = Time.realtimeSinceStartup + delay;
            }
            else
            {
                dialogue.maxVisibleCharacters = 99999;
                dialogue.alpha = 0f;
                DOTween.To(() => dialogue.alpha, a => dialogue.alpha = a, 1f, 0.2f).SetDelay(delay)
                    .SetUpdate(true).SetLink(dialogue.gameObject);
                FinishTyping();
            }
        }

        public void CompleteTyping()
        {
            if (IsTyping)
                FinishTyping();
        }

        private void FinishTyping()
        {
            IsTyping = false;
            dialogue.maxVisibleCharacters = 99999;
            TextCompletedAt = Time.realtimeSinceStartup;
        }

        public void SetTapToContinue(bool on, Action onContinue)
        {
            onTap = on ? onContinue : null;
            tapCatcher.SetActive(on);
            tapHint.gameObject.SetActive(on);
        }

        // A tap first finishes the line being typed, then goes on.
        public void Tap()
        {
            if (!TapToContinue)
                return;
            feedback?.PlayUIClick();
            if (IsTyping)
                CompleteTyping();
            else
                onTap?.Invoke();
        }

        // Darkens the play screen (the speaker stays readable) or clears it.
        public void SetScreenFade(float alpha, float duration)
        {
            screenFadeTween?.Kill();
            if (alpha > 0f)
                screenFade.gameObject.SetActive(true);
            else if (!screenFade.gameObject.activeSelf)
                return;
            screenFadeTween = DOTween.To(() => screenFade.color.a, a => screenFade.color = new Color(0f, 0f, 0f, a), alpha, duration)
                .SetUpdate(true).SetLink(screenFade.gameObject)
                .OnComplete(() => screenFade.gameObject.SetActive(alpha > 0f));
        }

        public void SetHighlight(TutorialHighlight target, bool dimmed)
        {
            if (dim != null && dimmed != dim.gameObject.activeSelf)
            {
                dimFade?.Kill();
                if (dimmed)
                {
                    dim.gameObject.SetActive(true);
                    dim.color = new Color(0f, 0f, 0f, 0f);
                    dimFade = DOTween.To(() => dim.color.a, a => dim.color = new Color(0f, 0f, 0f, a), look.dimAlpha, 0.25f)
                        .SetUpdate(true).SetLink(dim.gameObject);
                }
                else
                    dimFade = DOTween.To(() => dim.color.a, a => dim.color = new Color(0f, 0f, 0f, a), 0f, 0.2f)
                        .SetUpdate(true).SetLink(dim.gameObject).OnComplete(() => dim.gameObject.SetActive(false));
            }
            if (target == Highlight)
                return;
            Highlight = target;
            StopTimerHighlight();
            if (target != TutorialHighlight.Timer || timerGlow == null)
                return;
            timerGlow.gameObject.SetActive(true);
            Color glow = look.timerGlowColor;
            timerGlow.color = new Color(glow.r, glow.g, glow.b, glow.a * 0.35f);
            float half = look.timerPulsePeriod * 0.5f;
            highlightTweens.Add(DOTween.To(() => timerGlow.color.a, a => timerGlow.color = new Color(glow.r, glow.g, glow.b, a),
                glow.a, half).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(timerGlow.gameObject));
            if (timerFrame != null)
                highlightTweens.Add(timerFrame.DOScale(timerFrameScale * look.timerPulseScale, half)
                    .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(timerFrame.gameObject));
            if (timerNumber != null)
                highlightTweens.Add(timerNumber.DOScale(timerNumberScale * look.timerPulseScale, half)
                    .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(timerNumber.gameObject));
        }

        private void StopTimerHighlight()
        {
            foreach (Tween tween in highlightTweens)
                tween?.Kill();
            highlightTweens.Clear();
            if (timerFrame != null)
                timerFrame.localScale = timerFrameScale;
            if (timerNumber != null)
                timerNumber.localScale = timerNumberScale;
            if (timerGlow != null)
                timerGlow.gameObject.SetActive(false);
        }

        // The arrow points the way to swipe; the hand swipes along it.
        public void SetGuide(TutorialController.LessonInput direction, string keys)
        {
            if (keyHint != null)
                keyHint.text = keys ?? "";
            if (direction == GuideDirection)
                return;
            GuideDirection = direction;
            guideLoop?.Kill();
            arrowNudge?.Kill();
            if (direction == TutorialController.LessonInput.None)
            {
                guide.gameObject.SetActive(false);
                return;
            }
            guide.gameObject.SetActive(true);
            guideGroup.alpha = 0f;
            DOTween.To(() => guideGroup.alpha, a => guideGroup.alpha = a, 1f, 0.12f).SetUpdate(true).SetLink(guide.gameObject);

            Vector2 way = Way(direction);
            arrow.localEulerAngles = new Vector3(0f, 0f, direction == TutorialController.LessonInput.Left ? 90f :
                direction == TutorialController.LessonInput.Right ? -90f :
                direction == TutorialController.LessonInput.Down ? 180f : 0f);
            arrow.anchoredPosition = Vector2.zero;
            arrowNudge = DOTween.To(() => arrow.anchoredPosition, v => arrow.anchoredPosition = v, way * 14f, 0.42f)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(arrow.gameObject);

            // The hand travels beside the arrow (below it for left/right,
            // to its right for up/down) so both stay readable.
            bool sideways = way.y == 0f;
            Vector2 lane = sideways ? new Vector2(0f, -look.arrowSize.x * 0.5f - 20f) : new Vector2(look.arrowSize.x * 0.5f + 34f, 0f);
            Vector2 from = lane - way * look.handTravel * 0.5f;
            Vector2 to = lane + way * look.handTravel * 0.5f;
            Image handImage = hand.GetComponent<Image>();
            float cycle = look.handCycle;
            guideLoop = DOTween.Sequence()
                .AppendCallback(() =>
                {
                    hand.anchoredPosition = from;
                    hand.localScale = Vector3.one * 1.08f;
                    handImage.color = new Color(1f, 1f, 1f, 0f);
                })
                .Append(DOTween.To(() => handImage.color.a, a => handImage.color = new Color(1f, 1f, 1f, a), 1f, cycle * 0.12f))
                .Join(hand.DOScale(1f, cycle * 0.12f)) // the finger presses down
                .Append(DOTween.To(() => hand.anchoredPosition, v => hand.anchoredPosition = v, to, cycle * 0.45f)
                    .SetEase(Ease.OutCubic))
                .Append(DOTween.To(() => handImage.color.a, a => handImage.color = new Color(1f, 1f, 1f, a), 0f, cycle * 0.18f))
                .AppendInterval(cycle * 0.25f)
                .SetLoops(-1, LoopType.Restart).SetUpdate(true).SetLink(hand.gameObject);
        }

        private static Vector2 Way(TutorialController.LessonInput direction) =>
            direction == TutorialController.LessonInput.Left ? Vector2.left :
            direction == TutorialController.LessonInput.Right ? Vector2.right :
            direction == TutorialController.LessonInput.Down ? Vector2.down : Vector2.up;

        public void PlaySuccess(string text)
        {
            successTween?.Kill();
            successLabel.text = text;
            successLabel.gameObject.SetActive(true);
            successLabel.alpha = 1f;
            Transform pop = successLabel.transform;
            pop.localScale = Vector3.one * 0.4f;
            float hold = Mathf.Max(0f, look.successDuration - 0.45f);
            successTween = DOTween.Sequence()
                .Append(pop.DOScale(1f, 0.22f).SetEase(Ease.OutBack, 2.2f))
                .AppendInterval(hold)
                .Append(DOTween.To(() => successLabel.alpha, a => successLabel.alpha = a, 0f, 0.23f))
                .Join(pop.DOScale(1.12f, 0.23f))
                .OnComplete(() => successLabel.gameObject.SetActive(false))
                .SetUpdate(true).SetLink(successLabel.gameObject);
        }

        private void Update()
        {
            if (IsTyping)
            {
                int shown = Mathf.FloorToInt((Time.realtimeSinceStartup - typeStart) * look.typingCharsPerSecond);
                if (shown >= typeTotal)
                    FinishTyping();
                else
                    dialogue.maxVisibleCharacters = Mathf.Max(0, shown);
            }
            if (tapHint != null && tapHint.gameObject.activeSelf)
                tapHint.alpha = IsTyping ? 0f : 0.55f + 0.45f * Mathf.Sin(Time.realtimeSinceStartup * 5f);
        }

        // Puts back everything outside the tutorial's own panel.
        public void Cleanup()
        {
            if (cleaned)
                return;
            cleaned = true;
            guideLoop?.Kill();
            arrowNudge?.Kill();
            successTween?.Kill();
            dimFade?.Kill();
            screenFadeTween?.Kill();
            StopTimerHighlight();
            Highlight = TutorialHighlight.None;
            GuideDirection = TutorialController.LessonInput.None;
            onTap = null;
            if (dim != null)
                Destroy(dim.gameObject);
            if (timerGlow != null)
                Destroy(timerGlow.gameObject);
            foreach (HiddenHud entry in hiddenHud)
            {
                if (entry.Group == null)
                    continue;
                if (entry.Added)
                    Destroy(entry.Group);
                else
                {
                    entry.Group.alpha = entry.Alpha;
                    entry.Group.blocksRaycasts = entry.BlocksRaycasts;
                }
            }
            hiddenHud.Clear();
            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(true);
                skipButton.anchorMin = skipAnchorMin;
                skipButton.anchorMax = skipAnchorMax;
                skipButton.pivot = skipPivot;
                skipButton.anchoredPosition = skipPosition;
                skipButton.sizeDelta = skipSize;
                if (skipLabel != null)
                {
                    skipLabel.enableAutoSizing = skipAutoSize;
                    skipLabel.fontSizeMax = skipFontMax;
                }
                skipButton = null;
            }
        }

        private void OnDestroy() => Cleanup();

        private TMP_Text BubbleText(string name, float size, Color color, Vector2 topLeft, Vector2 box)
        {
            TMP_Text text = GameFlowManager.CreateMenuText(bubble, "", Vector2.zero, box, size);
            text.name = name;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
            text.color = color;
            return text;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = sprite != null;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
