using UnityEngine;
using UnityEngine.UI;
#if DEADLINE_DOTWEEN
using DG.Tweening;
#endif

namespace Deadline4Sec
{
    // Title -> run transition (2026-10-04 request): the moment a run starts the
    // title buttons vanish, the logo and top frame fly up off-screen and the
    // bottom frame drops away. The saved TitlePanel is still hidden at once as
    // before (flow tests and the Inspector-edited layout rely on that); stand-in
    // copies on a temporary overlay do the motion and are destroyed afterwards.
    //
    // Motion uses DOTween when the package is imported (Editor/DOTweenDefine adds
    // DEADLINE_DOTWEEN automatically); otherwise the same InBack curve runs here.
    public sealed class TitleOutro : MonoBehaviour
    {
        private const float Duration = 0.5f;
        private const float Overshoot = 1.2f; // a short wind-up before the fly-out
        private static readonly string[] FlyUp = { "Titlelogo", "TitleFrame" };
        private static readonly string[] FlyDown = { "TitleFrame (1)" };

        private struct Flight
        {
            public RectTransform Rect;
            public Vector2 From;
            public Vector2 To;
            public float Delay;
        }

        private readonly System.Collections.Generic.List<Flight> flights =
            new System.Collections.Generic.List<Flight>();
        private float elapsed;
        private float lifetime;

        public static void Play(GameObject titlePanel)
        {
            if (titlePanel == null || !titlePanel.activeInHierarchy)
                return;
            RectTransform panel = (RectTransform)titlePanel.transform;
            RectTransform canvas = panel.parent as RectTransform;
            if (canvas == null)
                return;

            GameObject overlay = new GameObject("TitleOutro", typeof(RectTransform), typeof(CanvasGroup));
            RectTransform rect = (RectTransform)overlay.transform;
            rect.SetParent(canvas, false);
            rect.SetSiblingIndex(panel.GetSiblingIndex() + 1);
            rect.anchorMin = panel.anchorMin;
            rect.anchorMax = panel.anchorMax;
            rect.pivot = panel.pivot;
            rect.anchoredPosition = panel.anchoredPosition;
            rect.sizeDelta = panel.sizeDelta;
            rect.localScale = panel.localScale;
            CanvasGroup group = overlay.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            TitleOutro outro = overlay.AddComponent<TitleOutro>();
            // Walk the panel in hierarchy order so the copies keep the saved
            // draw order (the logo stays above the frames).
            const float stagger = 0.05f;
            foreach (Transform child in panel)
            {
                int up = System.Array.IndexOf(FlyUp, child.name);
                if (up >= 0)
                    outro.Launch(child, rect, 1f, up * stagger);
                else if (System.Array.IndexOf(FlyDown, child.name) >= 0)
                    outro.Launch(child, rect, -1f, 0.02f);
            }
            outro.lifetime = Duration + FlyUp.Length * stagger + 0.05f;
        }

        private void Launch(Transform source, RectTransform overlay, float direction, float delay)
        {
            if (!source.gameObject.activeSelf)
                return;
            RectTransform copy = (RectTransform)Instantiate(source.gameObject, overlay, false).transform;
            copy.name = source.name;
            foreach (Graphic graphic in copy.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            Vector2 from = copy.anchoredPosition;
            // Just far enough for the image's near edge to clear the screen edge,
            // so the InBack wind-up stays a small dip.
            Vector3[] corners = new Vector3[4];
            copy.GetWorldCorners(corners);
            RectTransform canvas = (RectTransform)overlay.parent;
            float bottom = canvas.InverseTransformPoint(corners[0]).y;
            float top = canvas.InverseTransformPoint(corners[1]).y;
            float travel = direction > 0f ? canvas.rect.yMax - bottom : top - canvas.rect.yMin;
            Vector2 to = from + new Vector2(0f, direction * (Mathf.Max(0f, travel) + 40f));
#if DEADLINE_DOTWEEN
            DOTween.To(() => copy.anchoredPosition, v => copy.anchoredPosition = v, to, Duration)
                .SetDelay(delay).SetEase(Ease.InBack, Overshoot).SetUpdate(true).SetLink(copy.gameObject);
#else
            flights.Add(new Flight { Rect = copy, From = from, To = to, Delay = delay });
#endif
        }

        private void Update()
        {
            // Clamped so the hitch of spawning the opening course does not skip the motion.
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
                return;
            }
            foreach (Flight flight in flights)
            {
                if (flight.Rect == null)
                    continue;
                float t = Mathf.Clamp01((elapsed - flight.Delay) / Duration);
                flight.Rect.anchoredPosition = Vector2.LerpUnclamped(flight.From, flight.To, EaseInBack(t));
            }
        }

        // Same curve as DOTween's Ease.InBack with the overshoot above.
        private static float EaseInBack(float t) => t * t * ((Overshoot + 1f) * t - Overshoot);
    }
}
