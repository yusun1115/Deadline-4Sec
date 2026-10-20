using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // Presentation for three run moments (2026-10-05 request):
    //   Near Miss  — edge flash and short speed lines on the obstacle's side,
    //                "NEAR MISS!" popping there, the timer bouncing back to full,
    //                a quick chime over the swish (camera nudge: CameraFeedbackController).
    //   Last second — from 1.5 s left: dark red edge pulse and vignette, the timer
    //                growing and pulsing harder, a red glow at ≤1 s, ticks and
    //                heartbeats that speed up toward zero.
    //   Death      — collision: white-pink impact flash, one hard camera shake, a
    //                0.08 s hit stop into slow motion, a heavy low impact.
    //                Timer expiry: no flash or shake; the 0.00 timer punches and
    //                glows red, the world winds down, a low power-down thud.
    //                Both drain the colours (GameTimer.LastDeathReason decides).
    // Presentation only: reads game state and never changes rules.
    [DefaultExecutionOrder(300)] // after the camera rig has posed the camera
    public sealed class MomentFx : MonoBehaviour
    {
        private static readonly Color NearColor = new Color(0.35f, 1f, 0.85f);
        private static readonly Color PressureRed = new Color(0.45f, 0f, 0.06f);
        private static readonly Color TimerRed = new Color(1f, 0.12f, 0.2f);
        private static readonly Color WallHoldColor = new Color(0.72f, 0.62f, 1f);

        private const float PressureStart = 1.5f;
        private const int StreakCount = 8;

        private GameFlowManager flow;
        private GameTimer timer;
        private PowerUpManager powerUps;
        private CameraFeedbackController feedback;
        private HudJuice juice;
        private SoundLibrary library;
        private AudioSource audioSource;

        private RectTransform hud;
        private RectTransform layer;
        private RectTransform timerFrame;
        private Image edgeFlash;
        private readonly Image[] streaks = new Image[StreakCount];
        private readonly Vector2[] streakVelocity = new Vector2[StreakCount];
        private Image pressureVignette;
        private Image timerGlow;
        // Wall Run hold (2026-10-18): a lavender glow behind the timer and a
        // "❚❚ PAUSE" tag under it while the countdown waits on a wall.
        private Image wallGlow;
        private CanvasGroup wallPauseTag;
        private float wallHold;
        private Image deathFlash;
        private TMP_Text nearLabel;
        private bool nearLabelStyled;
        private float nearStart = -10f;
        private int nearSide; // 0 left, 1 right, 2 top, 3 bottom

        private float pressure;
        private float tickCountdown;
        private int tickCount;
        private float beatStart = -10f;

        private bool dying;
        private bool deathByContact;
        // Wall Run fall (2026-10-18): no flash, hit stop, timer punch or slow-down;
        // the runner simply drops away (CameraFeedbackController plays the wind).
        private bool deathByFall;
        private float deathStart = -10f;
        private bool managingTimeScale;
        private Vector3 shakeDirection;
        private Volume grade;
        private ColorAdjustments gradeColor;

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
            if (found != null && !found.TryGetComponent(out MomentFx _))
                found.gameObject.AddComponent<MomentFx>();
        }

        private void Start()
        {
            flow = GetComponent<GameFlowManager>();
            timer = FindFirstObjectByType<GameTimer>();
            powerUps = FindFirstObjectByType<PowerUpManager>();
            feedback = FindFirstObjectByType<CameraFeedbackController>();
            RunManager run = FindFirstObjectByType<RunManager>();
            if (run != null)
                run.TryGetComponent(out juice);
            if (feedback != null)
                feedback.NearMissed += OnNearMiss;
            library = SoundLibrary.Instance;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            foreach (string name in new[] { "NearChime", "Tick", "Heartbeat", "DeathImpact" })
                SfxSynth.Get(name);
            BuildUi();
            BuildGrade();
        }

        private void OnDestroy()
        {
            if (feedback != null)
                feedback.NearMissed -= OnNearMiss;
            if (managingTimeScale)
                Time.timeScale = 1f;
            if (grade != null && grade.profile != null)
                Destroy(grade.profile);
        }

        // ---------------- setup ----------------

        private void BuildUi()
        {
            Transform canvas = GameObject.Find("Canvas")?.transform;
            hud = canvas != null ? canvas.Find("GameplayUI") as RectTransform : null;
            if (hud == null)
                return;
            timerFrame = hud.Find("TimerFrame") as RectTransform;

            layer = (RectTransform)new GameObject("MomentFx", typeof(RectTransform)).transform;
            layer.SetParent(hud, false);
            Stretch(layer);
            layer.SetAsFirstSibling(); // under the HUD labels

            pressureVignette = Img(layer, "PressureVignette", FxSprites.Vignette, PressureRed);
            Stretch(pressureVignette.rectTransform);
            timerGlow = Img(layer, "TimerDangerGlow", FxSprites.Glow, TimerRed);
            wallGlow = Img(layer, "TimerWallHoldGlow", FxSprites.Glow, WallHoldColor);
            if (timerFrame != null)
                BuildWallPauseTag();
            edgeFlash = Img(layer, "NearMissEdge", FxSprites.EdgeGlow, NearColor);
            edgeFlash.rectTransform.pivot = new Vector2(0f, 0.5f);
            for (int i = 0; i < StreakCount; i++)
                streaks[i] = Img(layer, "NearMissStreak", FxSprites.Streak, Color.white);

            // The impact flash covers everything, HUD included.
            deathFlash = Img(canvas, "DeathFlash", null, Color.white);
            Stretch(deathFlash.rectTransform);
            deathFlash.transform.SetAsLastSibling();
        }

        private void BuildWallPauseTag()
        {
            var rect = (RectTransform)new GameObject("WallRunPause", typeof(RectTransform), typeof(CanvasGroup)).transform;
            rect.SetParent(timerFrame, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -2f);
            rect.sizeDelta = new Vector2(160f, 32f);
            wallPauseTag = rect.GetComponent<CanvasGroup>();
            wallPauseTag.alpha = 0f;
            wallPauseTag.blocksRaycasts = false;
            foreach (float x in new[] { -66f, -54f })
            {
                Image bar = new GameObject("Bar", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                bar.rectTransform.SetParent(rect, false);
                bar.rectTransform.anchoredPosition = new Vector2(x, 0f);
                bar.rectTransform.sizeDelta = new Vector2(7f, 22f);
                bar.color = WallHoldColor;
                bar.raycastTarget = false;
            }
            TextMeshProUGUI label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(rect, false);
            label.rectTransform.anchoredPosition = new Vector2(20f, 0f);
            label.rectTransform.sizeDelta = new Vector2(100f, 32f);
            GameFont.Apply(label);
            label.text = "PAUSE";
            label.fontSize = 24f;
            label.characterSpacing = 4f;
            label.color = WallHoldColor;
            label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            rect.gameObject.SetActive(false);
        }

        private void UpdateWallHold(float now, float dt)
        {
            bool held = flow != null && flow.IsPlaying && timer != null && timer.IsHeldByWallRun;
            wallHold = Mathf.MoveTowards(wallHold, held ? 1f : 0f, dt * 6f);
            float pulse = 0.85f + 0.15f * Mathf.Sin(now * 5f);
            if (wallGlow != null && timerFrame != null)
            {
                RectTransform rect = wallGlow.rectTransform;
                rect.position = timerFrame.TransformPoint(timerFrame.rect.center);
                rect.sizeDelta = new Vector2(timerFrame.rect.width * 1.3f, timerFrame.rect.height * 2.2f);
                Alpha(wallGlow, 0.5f * wallHold * pulse);
            }
            if (wallPauseTag != null)
            {
                wallPauseTag.alpha = wallHold * pulse;
                if (wallPauseTag.gameObject.activeSelf != wallHold > 0.004f)
                    wallPauseTag.gameObject.SetActive(wallHold > 0.004f);
            }
        }

        // Global volume that drains colour after a death (weight animated).
        private void BuildGrade()
        {
            GameObject go = new GameObject("Death Grade");
            go.transform.SetParent(transform, false);
            grade = go.AddComponent<Volume>();
            grade.isGlobal = true;
            grade.priority = 100f;
            grade.weight = 0f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Death Grade (Runtime)";
            gradeColor = profile.Add<ColorAdjustments>(true);
            gradeColor.saturation.Override(-70f);
            gradeColor.postExposure.Override(-0.45f);
            gradeColor.contrast.Override(12f);
            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.38f);
            vignette.color.Override(new Color(0.12f, 0f, 0.04f));
            grade.profile = profile;
        }

        private static Image Img(Transform parent, string name, Sprite sprite, Color color)
        {
            Image image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            image.sprite = sprite;
            image.color = new Color(color.r, color.g, color.b, 0f);
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Alpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
                return;
            Color c = graphic.color;
            c.a = Mathf.Clamp01(alpha);
            graphic.color = c;
            graphic.enabled = c.a > 0.004f;
        }

        private static float Kick(float t) => t < 0f ? 0f : (1f - Mathf.Exp(-t * 60f)) * Mathf.Exp(-t * 9f);

        private void Play(AudioClip authored, string synth, float volume, float pitch = 1f)
        {
            if (!GamePreferences.SoundEnabled || audioSource == null)
                return;
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(authored != null ? authored : SfxSynth.Get(synth),
                volume * (library != null ? library.sfxVolume : 0.8f));
        }

        // ---------------- near miss ----------------

        private void OnNearMiss(Vector3 player, Vector3 hazard)
        {
            Vector3 d = hazard - player;
            nearSide = Mathf.Abs(d.x) > 0.3f ? (d.x < 0f ? 0 : 1) : (d.y > 0f ? 2 : 3);
            nearStart = Time.unscaledTime;
            Play(library != null ? library.nearMissChime : null, "NearChime", 0.7f);
            if (hud == null)
                return;

            // Edge flash: pivot sits on the edge, the sprite fades inward.
            Rect r = hud.rect;
            float band = r.width * 0.24f;
            RectTransform edge = edgeFlash.rectTransform;
            Vector2[] anchors = { new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f) };
            float[] angles = { 0f, 180f, -90f, 90f };
            edge.anchorMin = edge.anchorMax = anchors[nearSide];
            edge.anchoredPosition = Vector2.zero;
            edge.localRotation = Quaternion.Euler(0f, 0f, angles[nearSide]);
            edge.sizeDelta = new Vector2(band, nearSide < 2 ? r.height : r.width);

            // Speed lines in that band, flying outward from the screen centre.
            for (int i = 0; i < StreakCount; i++)
            {
                Vector2 p;
                switch (nearSide)
                {
                    case 0: p = new Vector2(r.xMin + Random.Range(0.02f, 0.2f) * r.width, Random.Range(r.yMin + r.height * 0.15f, r.yMax - r.height * 0.15f)); break;
                    case 1: p = new Vector2(r.xMax - Random.Range(0.02f, 0.2f) * r.width, Random.Range(r.yMin + r.height * 0.15f, r.yMax - r.height * 0.15f)); break;
                    case 2: p = new Vector2(Random.Range(r.xMin, r.xMax), r.yMax - Random.Range(0.02f, 0.14f) * r.height); break;
                    default: p = new Vector2(Random.Range(r.xMin, r.xMax), r.yMin + Random.Range(0.02f, 0.14f) * r.height); break;
                }
                Vector2 dir = (p - r.center).normalized;
                RectTransform rect = streaks[i].rectTransform;
                rect.localPosition = p - dir * 120f;
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                rect.sizeDelta = new Vector2(Random.Range(140f, 280f), Random.Range(5f, 9f));
                streakVelocity[i] = dir * Random.Range(1100f, 1700f);
            }

            // "NEAR MISS!" (RunManager's label) moves to the obstacle's side.
            if (nearLabel == null)
                nearLabel = hud.Find("NearMissFeedback")?.GetComponent<TMP_Text>();
            if (nearLabel != null)
            {
                if (!nearLabelStyled)
                {
                    nearLabelStyled = true;
                    nearLabel.fontSharedMaterial = ComboBadge.Outlined(nearLabel.font.material,
                        new Color(0.02f, 0.2f, 0.18f), 0.22f, true, 0.12f);
                    nearLabel.enableVertexGradient = true;
                    nearLabel.colorGradient = new VertexGradient(Color.white, Color.white, NearColor, NearColor);
                    nearLabel.color = Color.white;
                    nearLabel.fontSize = 46f;
                }
                RectTransform rect = nearLabel.rectTransform;
                Vector2[] labelAnchors = { new Vector2(0.04f, 0.56f), new Vector2(0.96f, 0.56f), new Vector2(0.5f, 0.74f), new Vector2(0.5f, 0.3f) };
                Vector2[] pivots = { new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f) };
                TextAlignmentOptions[] aligns = { TextAlignmentOptions.Left, TextAlignmentOptions.Right, TextAlignmentOptions.Center, TextAlignmentOptions.Center };
                rect.anchorMin = rect.anchorMax = labelAnchors[nearSide];
                rect.pivot = pivots[nearSide];
                rect.sizeDelta = new Vector2(420f, 80f);
                rect.localRotation = Quaternion.Euler(0f, 0f, nearSide == 0 ? 7f : nearSide == 1 ? -7f : 0f);
                nearLabel.alignment = aligns[nearSide];
            }
        }

        private void UpdateNearMiss(float now)
        {
            float t = now - nearStart;
            float flash = 1f - t / 0.26f;
            Alpha(edgeFlash, flash > 0f ? flash * flash * 0.85f : 0f);
            float st = t / 0.22f;
            for (int i = 0; i < StreakCount; i++)
            {
                if (st >= 1f || st < 0f)
                {
                    Alpha(streaks[i], 0f);
                    continue;
                }
                streaks[i].rectTransform.localPosition += (Vector3)(streakVelocity[i] * Time.unscaledDeltaTime);
                Alpha(streaks[i], 0.8f * (1f - st));
            }
            if (nearLabel != null && t < 0.8f)
            {
                float pop = t < 0.14f ? Mathf.Lerp(1.7f, 1f, t / 0.14f) : 1f;
                nearLabel.rectTransform.localScale = Vector3.one * pop;
                nearLabel.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Max(0f, t - 0.3f) * 60f);
            }
        }

        // Timer bounce on the near miss reset: overshoots, then settles.
        private float NearBounce(float now)
        {
            float t = now - nearStart;
            return t < 0f || t > 0.7f ? 0f : 0.42f * Mathf.Exp(-t * 7f) * Mathf.Abs(Mathf.Sin(t * 20f));
        }

        // ---------------- last second ----------------

        private void UpdatePressure(float now, float dt)
        {
            bool counting = flow != null && flow.IsPlaying && timer != null && timer.IsRunning &&
                            (powerUps == null || !powerUps.TimerFrozen) && !timer.IsHeldByWallRun;
            float target = counting ? Mathf.Clamp01((PressureStart - timer.RemainingTime) / PressureStart) : 0f;
            pressure = Mathf.MoveTowards(pressure, target, dt * 6f);

            if (target > 0f)
            {
                // Ticks speed up toward zero; every other one carries a heartbeat.
                tickCountdown -= dt;
                if (tickCountdown <= 0f)
                {
                    tickCountdown = Mathf.Lerp(0.42f, 0.11f, target);
                    beatStart = now;
                    Play(library != null ? library.pressureTick : null, "Tick", 0.55f + 0.35f * target, 1f + target * 0.35f);
                    if (tickCount++ % 2 == 0)
                        Play(library != null ? library.heartbeat : null, "Heartbeat", 0.35f + 0.45f * target, 1f + target * 0.25f);
                }
            }
            else
            {
                tickCountdown = 0f;
                tickCount = 0;
            }

            float beat = Kick(now - beatStart) * 1.6f;
            Alpha(pressureVignette, pressure * (0.4f + 0.3f * beat));
            if (timerGlow != null && timerFrame != null)
            {
                bool critical = counting && timer.RemainingTime <= 1f;
                RectTransform rect = timerGlow.rectTransform;
                rect.position = timerFrame.TransformPoint(timerFrame.rect.center);
                rect.sizeDelta = new Vector2(timerFrame.rect.width * 1.25f, timerFrame.rect.height * 2.2f) *
                                 (1f + 0.15f * beat);
                float expired = dying && !deathByContact && !deathByFall ? Mathf.Clamp01(1f - (now - deathStart - 0.2f) / 0.3f) : 0f;
                Alpha(timerGlow, Mathf.Max((critical ? 0.55f : 0.2f) * pressure + 0.3f * beat * pressure, 0.9f * expired));
            }
            if (juice != null)
                juice.MomentTimerPop = pressure * 0.22f + pressure * 0.16f * beat + NearBounce(now) + TimerDeathPop(now);
        }

        // ---------------- death ----------------

        private void BeginDeath(float now)
        {
            dying = true;
            deathStart = now;
            deathByContact = timer != null && timer.LastDeathReason == GameTimer.DeathReason.Collision;
            deathByFall = timer != null && timer.LastDeathReason == GameTimer.DeathReason.Fall;
            if (deathByFall)
                return;
            Vector2 random = Random.insideUnitCircle.normalized;
            shakeDirection = new Vector3(random.x, random.y * 0.6f, 0f);
            // Collision: a hard hit. Timer expiry: a low power-down thud.
            Play(library != null ? library.deathImpact : null, "DeathImpact",
                deathByContact ? 1f : 0.55f, deathByContact ? 1f : 0.7f);
            if (deathFlash != null)
                deathFlash.color = new Color(1f, 0.86f, 0.93f, 0f); // white-pink impact
        }

        // Timer expiry: the frozen 0.00 punches twice while the HUD holds.
        private float TimerDeathPop(float now) =>
            dying && !deathByContact && !deathByFall ? 0.45f * Kick(now - deathStart) + 0.3f * Kick(now - deathStart - 0.22f) : 0f;

        private void UpdateDeath(float now, float dt)
        {
            bool inDeathFlow = flow != null && (flow.State == GameFlowManager.GameState.Dying ||
                                                flow.State == GameFlowManager.GameState.RevivePrompt ||
                                                flow.State == GameFlowManager.GameState.GameOver ||
                                                flow.State == GameFlowManager.GameState.Result ||
                                                flow.State == GameFlowManager.GameState.GiftBoxes);
            if (!dying && flow != null && flow.State == GameFlowManager.GameState.Dying)
                BeginDeath(now);
            if (dying && !inDeathFlow)
                dying = false; // revived or left for the title

            float t = now - deathStart;

            // Collision only: a short impact flash and one hard shake.
            Alpha(deathFlash, dying && deathByContact && t < 0.1f ? (1f - t / 0.1f) * 0.85f : 0f);
            if (dying && deathByContact && t < 0.45f && Camera.main != null)
            {
                float amount = 0.45f * Mathf.Exp(-t * 9f);
                float wobble = Mathf.Sin(t * 70f);
                Vector3 jitter = new Vector3(Mathf.PerlinNoise(t * 40f, 1.3f) - 0.5f, Mathf.PerlinNoise(2.7f, t * 40f) - 0.5f, 0f);
                Camera.main.transform.localPosition += (shakeDirection * wobble + jitter) * amount;
            }

            // Collision: hit stop, then slow motion easing back to normal speed.
            // Timer expiry: no freeze; the world winds down like it is powering off.
            if (dying && !deathByFall && t < 0.7f && flow.State == GameFlowManager.GameState.Dying)
            {
                if (deathByContact)
                {
                    const float hitStop = 0.08f;
                    Time.timeScale = t < hitStop ? 0f
                        : Mathf.Lerp(0.2f, 1f, Mathf.SmoothStep(0f, 1f, (t - hitStop) / (0.6f - hitStop)));
                }
                else
                {
                    Time.timeScale = t < 0.45f ? Mathf.Lerp(1f, 0.25f, t / 0.45f)
                        : Mathf.Lerp(0.25f, 1f, (t - 0.45f) / 0.25f);
                }
                managingTimeScale = true;
            }
            else if (managingTimeScale)
            {
                Time.timeScale = 1f;
                managingTimeScale = false;
            }

            // Colours drain after the hit and come back on revive / restart.
            if (grade != null)
            {
                float target = dying && t > 0.08f ? 1f : 0f;
                grade.weight = Mathf.MoveTowards(grade.weight, target, dt * (target > 0f ? 2.4f : 5f));
            }
        }

        // ---------------- per frame ----------------

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            if (hud != null)
                UpdateNearMiss(now);
            UpdatePressure(now, dt);
            UpdateWallHold(now, dt);
            UpdateDeath(now, dt);
        }
    }
}
