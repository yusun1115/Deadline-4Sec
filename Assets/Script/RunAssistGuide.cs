using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // First-run help (2026-11-11, play-test feedback: new players could not
    // tell when a swipe kills). Every run:
    //  - the first GuidedEnemies enemies show a target marker for the swipe that
    //    counters them (PlayerController.TryGetKillWindow). A lane kill is only
    //    ~0.2 s long, so the marker comes in large and faint LeadSeconds before,
    //    closes in, and is small and sharp exactly while the swipe kills;
    //    (2026-11-26, "hard to see": it now comes in bigger, spinning, and
    //    settles square on the kill; a neon halo lights its edge; and a "!"
    //    pops over the enemy ExclaimLead before its attack reaches the runner);
    //  - the first wall run shows the tutorial's swipe guide while the runner is
    //    inside its entry window, pointing at the open wall.
    // Real runs only (not the tutorial). Retry reloads the scene: help again.
    public sealed class RunAssistGuide : MonoBehaviour
    {
        private const int GuidedEnemies = 10;
        private const float RegisterAhead = 45f;
        private const float LeadSeconds = 0.75f;
        // The "!" shows this long before the enemy's attack reaches the runner
        // (the kill window's near end), until the enemy dies or is passed.
        private const float ExclaimLead = 0.45f;
        private const float SpinDegrees = 300f;
        private static readonly Color FarColor = new Color(1f, 0.62f, 0.88f);
        private static readonly Color NearColor = new Color(1f, 0.16f, 0.62f);
        private static readonly Color EdgeColor = new Color(0.12f, 0.02f, 0.1f);
        private static readonly Color NeonColor = new Color(1f, 0.3f, 0.78f);
        private static readonly Color BangColor = new Color(1f, 0.88f, 0.3f);
        private static readonly Color BangGlowColor = new Color(1f, 0.42f, 0.22f);

        // One marker's UI: the reticle (halo, dark edge, ring; it turns) and the
        // "!" over the head (halo, dark edge, fill; upright).
        private sealed class Visual
        {
            public RectTransform Root;
            public Image Glow, Edge, Ring;
            public RectTransform Bang;
            public Image BangGlow, BangEdge, BangFill;
        }

        private sealed class Marker
        {
            public Enemy Enemy;
            public Collider Body;
            public Renderer[] Parts; // what the player sees (bigger than the hit box)
            public Visual View;
            public float Shown;
            public float Closeness;
            public bool Now;
            public float Bang; // the "!": 0 hidden .. 1 shown
            public float BangAge;
        }

        private readonly List<Marker> markers = new List<Marker>();
        private readonly List<Visual> pool = new List<Visual>();
        private readonly HashSet<Enemy> registered = new HashSet<Enemy>();
        private readonly List<Enemy> scan = new List<Enemy>();
        private GameFlowManager flow;
        private PlayerController player;
        private Canvas canvas;
        private RectTransform layer;
        private float nextScan;

        private RectTransform guide, arrow, hand;
        private CanvasGroup guideGroup;
        private Sequence guideLoop;
        private Tween arrowNudge;
        private TutorialPresentation look;
        private int guideSide;
        private bool wallGuideShown, wallGuideDone;

        public int GuidedCount => registered.Count;
        public int VisibleMarkers
        {
            get
            {
                int count = 0;
                foreach (Marker marker in markers)
                    if (marker.View.Edge != null && marker.View.Edge.enabled)
                        count++;
                return count;
            }
        }
        public int VisibleExclaims
        {
            get
            {
                int count = 0;
                foreach (Marker marker in markers)
                    if (marker.View.Bang != null && marker.View.Bang.gameObject.activeSelf)
                        count++;
                return count;
            }
        }
        public bool WallGuideVisible => guide != null && guide.gameObject.activeSelf;
        public int WallGuideSide => guideSide;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(default, default);
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
        {
            GameFlowManager found = FindFirstObjectByType<GameFlowManager>();
            if (found != null && !found.TryGetComponent(out RunAssistGuide _))
                found.gameObject.AddComponent<RunAssistGuide>();
        }

        private void Start()
        {
            flow = GetComponent<GameFlowManager>();
            player = FindFirstObjectByType<PlayerController>();
            Transform canvasObject = GameObject.Find("Canvas")?.transform;
            RectTransform hud = canvasObject != null ? canvasObject.Find("GameplayUI") as RectTransform : null;
            if (hud == null)
                return;
            canvas = hud.GetComponentInParent<Canvas>();
            layer = (RectTransform)new GameObject("RunAssistGuide", typeof(RectTransform)).transform;
            layer.SetParent(hud, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            layer.SetAsFirstSibling(); // under the HUD labels
            BuildWallGuide();
        }

        private void OnDestroy()
        {
            guideLoop?.Kill();
            arrowNudge?.Kill();
        }

        private void LateUpdate()
        {
            bool playing = flow != null && player != null && layer != null &&
                flow.State == GameFlowManager.GameState.Playing;
            UpdateMarkers(playing);
            UpdateWallGuide(playing);
        }

        // ---------------- target markers ----------------

        private void UpdateMarkers(bool playing)
        {
            if (!playing)
            {
                foreach (Marker marker in markers)
                {
                    Show(marker, false);
                    ShowBang(marker, false);
                }
                return;
            }
            float z = player.transform.position.z;
            if (registered.Count < GuidedEnemies && Time.unscaledTime >= nextScan)
                Register(z);

            Camera view = Camera.main;
            float dt = Time.unscaledDeltaTime;
            for (int i = markers.Count - 1; i >= 0; i--)
            {
                Marker marker = markers[i];
                bool gone = marker.Enemy == null || marker.Enemy.IsKilled || marker.Enemy.transform.position.z < z - 3f;
                float until = 0f, shut = 0f;
                bool window = !gone && player.TryGetKillWindowSpan(marker.Enemy, out until, out shut);
                bool open = window && until <= LeadSeconds;
                marker.Now = open && until <= 0f;
                if (open)
                    marker.Closeness = Mathf.Clamp01(1f - until / LeadSeconds);
                marker.Shown = Mathf.MoveTowards(marker.Shown, open ? 1f : 0f, dt * (open ? 8f : 6f));
                bool warn = window && shut <= ExclaimLead;
                if (warn && marker.Bang <= 0f)
                    marker.BangAge = 0f; // pops in afresh
                marker.Bang = Mathf.MoveTowards(marker.Bang, warn ? 1f : 0f, dt * (warn ? 14f : 7f));
                marker.BangAge += dt;
                if (gone && marker.Shown <= 0f && marker.Bang <= 0f)
                {
                    Show(marker, false);
                    ShowBang(marker, false);
                    pool.Add(marker.View);
                    markers.RemoveAt(i);
                    continue;
                }
                Place(marker, view);
            }
        }

        // The next enemies down the course, in the order they come, up to the limit.
        private void Register(float z)
        {
            nextScan = Time.unscaledTime + 0.15f;
            scan.Clear();
            foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            {
                float ahead = enemy.transform.position.z - z;
                if (!enemy.IsKilled && !registered.Contains(enemy) && ahead > -1f && ahead <= RegisterAhead)
                    scan.Add(enemy);
            }
            scan.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
            foreach (Enemy enemy in scan)
            {
                if (registered.Count >= GuidedEnemies)
                    break;
                registered.Add(enemy);
                markers.Add(new Marker { Enemy = enemy, Body = enemy.GetComponent<Collider>(), View = TakeVisual() });
            }
        }

        private Visual TakeVisual()
        {
            if (pool.Count > 0)
            {
                Visual reused = pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1);
                return reused;
            }
            var visual = new Visual();
            visual.Root = (RectTransform)new GameObject("TargetMarker", typeof(RectTransform)).transform;
            visual.Root.SetParent(layer, false);
            // Halo under the dark edge under the pink ring.
            visual.Glow = Layer(visual.Root, "Glow", FxSprites.ReticleGlow, FxSprites.ReticleGlowSpan);
            visual.Edge = Layer(visual.Root, "Edge", FxSprites.ReticleOutline, 1f);
            visual.Ring = Layer(visual.Root, "Ring", FxSprites.Reticle, 1f);
            visual.Bang = (RectTransform)new GameObject("TargetExclaim", typeof(RectTransform)).transform;
            visual.Bang.SetParent(layer, false);
            visual.Bang.pivot = new Vector2(0.5f, 0f); // stands on the head
            visual.BangGlow = Layer(visual.Bang, "Glow", FxSprites.ExclaimGlow, 1f);
            visual.BangEdge = Layer(visual.Bang, "Edge", FxSprites.ExclaimOutline, 1f);
            visual.BangFill = Layer(visual.Bang, "Fill", FxSprites.Exclaim, 1f);
            visual.Bang.gameObject.SetActive(false);
            return visual;
        }

        // A full-size child (span > 1: that much bigger, still centred).
        private static Image Layer(RectTransform parent, string name, Sprite sprite, float span)
        {
            Image image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            RectTransform rect = image.rectTransform;
            rect.SetParent(parent, false);
            float grow = (span - 1f) * 0.5f;
            rect.anchorMin = new Vector2(-grow, -grow);
            rect.anchorMax = new Vector2(1f + grow, 1f + grow);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.sprite = sprite;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        private void Place(Marker marker, Camera view)
        {
            if (view == null || marker.Enemy == null || marker.Body == null ||
                marker.Shown <= 0.001f && marker.Bang <= 0.001f)
            {
                Show(marker, false);
                ShowBang(marker, false);
                return;
            }
            Bounds body = SeenBounds(marker);
            Vector3 center = view.WorldToScreenPoint(body.center);
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (center.z <= 0.1f ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, center, eventCamera, out Vector2 local))
            {
                Show(marker, false);
                ShowBang(marker, false);
                return;
            }
            Vector3 top = view.WorldToScreenPoint(body.center + Vector3.up * body.extents.y);
            Vector3 side = view.WorldToScreenPoint(body.center + Vector3.right * body.extents.x);
            float bodyPixels = Mathf.Max(Mathf.Abs(top.y - center.y), Mathf.Abs(side.x - center.x)) * 2f;
            float bodySize = bodyPixels / Mathf.Max(0.01f, canvas.scaleFactor);
            PlaceReticle(marker, local, bodySize);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, top, eventCamera, out Vector2 head);
            PlaceBang(marker, head, bodySize);
        }

        private static void PlaceReticle(Marker marker, Vector2 local, float bodySize)
        {
            Visual visual = marker.View;
            if (marker.Shown <= 0.001f)
            {
                Show(marker, false);
                return;
            }
            float c = marker.Closeness;
            float settle = 1f - (1f - c) * (1f - c); // spins fast at first, then eases square
            float now = Time.unscaledTime;
            float pulse = marker.Now ? 1f + 0.08f * Mathf.Sin(now * 40f) : 1f;
            // Bigger than before (2.1 -> 0.85 of the body), and it pops in.
            float size = bodySize * Mathf.Lerp(2.6f, 1.05f, c) * pulse * Mathf.Lerp(0.7f, 1f, marker.Shown);
            Show(marker, true);
            visual.Root.anchoredPosition = local;
            visual.Root.sizeDelta = new Vector2(size, size);
            visual.Root.localEulerAngles = new Vector3(0f, 0f, -SpinDegrees * (1f - settle));
            float alpha = marker.Shown * (marker.Now ? 1f : Mathf.Lerp(0.6f, 0.9f, c));
            Color color = Color.Lerp(FarColor, NearColor, c);
            // A neon tube: a touch of white in the core while it kills.
            if (marker.Now)
                color = Color.Lerp(color, Color.white, 0.18f);
            color.a = alpha;
            visual.Ring.color = color;
            visual.Edge.color = new Color(EdgeColor.r, EdgeColor.g, EdgeColor.b, alpha * 0.55f);
            float flicker = marker.Now ? 0.85f + 0.15f * Mathf.Sin(now * 30f) : 1f;
            Color neon = Color.Lerp(NeonColor, NearColor, c * 0.5f);
            neon.a = marker.Shown * Mathf.Lerp(0.45f, 0.8f, c) * flicker;
            visual.Glow.color = neon;
        }

        private static void PlaceBang(Marker marker, Vector2 head, float bodySize)
        {
            Visual visual = marker.View;
            if (marker.Bang <= 0.001f)
            {
                ShowBang(marker, false);
                return;
            }
            ShowBang(marker, true);
            float height = Mathf.Clamp(bodySize * 0.75f, 72f, 140f);
            float age = marker.BangAge;
            // Pops up past full size and settles, then bobs.
            float pop = age < 0.2f ? EaseOutBack(age / 0.2f) : 1f + 0.05f * Mathf.Sin((age - 0.2f) * 16f);
            visual.Bang.sizeDelta = new Vector2(height * 160f / 256f, height);
            visual.Bang.anchoredPosition = head + new Vector2(0f, height * 0.12f + 4f * Mathf.Sin(age * 9f));
            visual.Bang.localScale = Vector3.one * Mathf.Max(0f, pop);
            float a = marker.Bang;
            visual.BangFill.color = new Color(BangColor.r, BangColor.g, BangColor.b, a);
            visual.BangEdge.color = new Color(EdgeColor.r, EdgeColor.g, EdgeColor.b, a * 0.85f);
            visual.BangGlow.color = new Color(BangGlowColor.r, BangGlowColor.g, BangGlowColor.b,
                a * (0.7f + 0.15f * Mathf.Sin(age * 22f)));
        }

        private static float EaseOutBack(float t)
        {
            const float Overshoot = 2.2f;
            t -= 1f;
            return 1f + t * t * ((Overshoot + 1f) * t + Overshoot);
        }

        private static void Show(Marker marker, bool visible)
        {
            Visual visual = marker.View;
            visual.Glow.enabled = visual.Edge.enabled = visual.Ring.enabled = visible;
        }

        private static void ShowBang(Marker marker, bool visible)
        {
            Visual visual = marker.View;
            if (visual.Bang.gameObject.activeSelf != visible)
                visual.Bang.gameObject.SetActive(visible);
            visual.BangGlow.enabled = visual.BangEdge.enabled = visual.BangFill.enabled = visible;
        }

        private static Bounds SeenBounds(Marker marker)
        {
            if (marker.Parts == null || marker.Parts.Length == 0)
            {
                Transform visual = marker.Enemy.transform.Find("Visual");
                marker.Parts = visual != null ? visual.GetComponentsInChildren<Renderer>() : new Renderer[0];
            }
            Bounds seen = marker.Body.bounds;
            bool any = false;
            foreach (Renderer part in marker.Parts)
            {
                if (part == null || !part.enabled || part is ParticleSystemRenderer)
                    continue;
                if (!any)
                    seen = part.bounds;
                else
                    seen.Encapsulate(part.bounds);
                any = true;
            }
            return seen;
        }

        // ---------------- first wall run ----------------

        private void BuildWallGuide()
        {
            TutorialCourseAssets assets = Resources.Load<TutorialCourseAssets>("Tutorial/TutorialCourse");
            if (assets == null || assets.swipeArrow == null || assets.swipeHand == null)
                return;
            look = assets.presentation ?? new TutorialPresentation();
            guide = new GameObject("WallRunSwipeGuide", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            guide.SetParent(layer, false);
            guide.anchorMin = guide.anchorMax = new Vector2(0.5f, 0f);
            guide.anchoredPosition = new Vector2(0f, look.guideBottomOffset);
            guide.sizeDelta = new Vector2(320f, 320f);
            guideGroup = guide.GetComponent<CanvasGroup>();
            guideGroup.blocksRaycasts = false;
            arrow = NewImage("tutorial_swipe_arrow", assets.swipeArrow, look.arrowSize);
            hand = NewImage("tutorial_swipe_hand", assets.swipeHand, look.handSize);
            hand.pivot = new Vector2(0.33f, 0.95f); // the fingertip
            guide.gameObject.SetActive(false);
        }

        private RectTransform NewImage(string name, Sprite sprite, Vector2 size)
        {
            Image image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(guide, false);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = size;
            return image.rectTransform;
        }

        private void UpdateWallGuide(bool playing)
        {
            if (guide == null)
                return;
            int side = 0;
            if (playing && !wallGuideDone)
            {
                if (player.IsWallRunning)
                    wallGuideDone = wallGuideShown;
                else
                {
                    CharacterController body = player.GetComponent<CharacterController>();
                    WallRunSection section = body != null ? WallRunSection.FindEntry(body.bounds.center) : null;
                    if (section != null)
                        side = section.Available == WallRunSection.AvailableWall.Right ? 1 : -1;
                    else if (wallGuideShown)
                        wallGuideDone = true;
                }
            }
            ShowGuide(side);
        }

        // The tutorial's guide: the arrow points the way, the hand swipes along it.
        private void ShowGuide(int side)
        {
            if (side == guideSide)
                return;
            guideSide = side;
            guideLoop?.Kill();
            arrowNudge?.Kill();
            if (side == 0)
            {
                guide.gameObject.SetActive(false);
                return;
            }
            wallGuideShown = true;
            guide.gameObject.SetActive(true);
            guideGroup.alpha = 0f;
            DOTween.To(() => guideGroup.alpha, a => guideGroup.alpha = a, 1f, 0.12f).SetUpdate(true).SetLink(guide.gameObject);

            Vector2 way = side > 0 ? Vector2.right : Vector2.left;
            arrow.localEulerAngles = new Vector3(0f, 0f, side > 0 ? -90f : 90f);
            arrow.anchoredPosition = Vector2.zero;
            arrowNudge = DOTween.To(() => arrow.anchoredPosition, v => arrow.anchoredPosition = v, way * 14f, 0.42f)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(arrow.gameObject);

            Vector2 lane = new Vector2(0f, -look.arrowSize.x * 0.5f - 20f);
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
                .Join(hand.DOScale(1f, cycle * 0.12f))
                .Append(DOTween.To(() => hand.anchoredPosition, v => hand.anchoredPosition = v, to, cycle * 0.45f)
                    .SetEase(Ease.OutCubic))
                .Append(DOTween.To(() => handImage.color.a, a => handImage.color = new Color(1f, 1f, 1f, a), 0f, cycle * 0.18f))
                .AppendInterval(cycle * 0.25f)
                .SetLoops(-1, LoopType.Restart).SetUpdate(true).SetLink(hand.gameObject);
        }
    }
}
