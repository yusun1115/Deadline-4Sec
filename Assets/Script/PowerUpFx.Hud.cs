using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // HUD half of PowerUpFx: iced timer, frost, flashes and shards (Freeze Clock),
    // heartbeat timer and edge pulse (Time Heart), score/combo aura and the
    // "x2 SCORE!" popup (Soul Amplifier), chains around the combo badge (Combo Seal).
    public sealed partial class PowerUpFx
    {
        private const int BitCount = 28;
        private const int LinkCount = 22;
        private static readonly Color IcyText = new Color(0.8f, 0.96f, 1f);
        private static readonly Color HeartText = new Color(1f, 0.55f, 0.78f);
        private static readonly Color Steel = new Color(0.8f, 0.78f, 0.92f);

        private struct Bit
        {
            public Image Image;
            public Vector2 Velocity;
            public float Spin, Start, Life;
            public bool Active;
        }

        private RectTransform hud;
        private RectTransform backRoot;
        private RectTransform frontRoot;
        private Text timerText;
        private RectTransform timerFrame;
        private RectTransform scoreRect;
        private RectTransform badge;
        private CanvasGroup badgeGroup;

        // Freeze Clock
        private Image flash;
        private Color flashColor;
        private float flashStart = -10f;
        private float flashStrength;
        private Image frostEdge;
        private Image iceCover;
        private Image crack;
        private readonly Image[] crystals = new Image[8];
        private float freezeBlend;
        private float freezeStart;

        // Time Heart
        private Image heartEdge;
        private Image heartBorder;
        private float heartBlend;
        private float heartAcquireStart = -10f;
        private float heartEndStart = -10f;

        // Soul Amplifier
        private Image auraScore;
        private Image auraCombo;
        private float ampBlend;
        private TextMeshProUGUI ampPopup;
        private float ampPopupStart = -10f;

        // Combo Seal
        private RectTransform chainRoot;
        private readonly Image[] links = new Image[LinkCount];
        private readonly Vector2[] linkVelocity = new Vector2[LinkCount];
        private readonly float[] linkSpin = new float[LinkCount];
        private Image padlock;
        private Image lockGlow;
        private Vector2 padlockVelocity;
        private TextMeshProUGUI lockText;
        private float sealStart = -10f;
        private float sealBreakStart = -10f;
        private float lockTextStart = -10f;
        private bool sealShown;
        private bool sealBreaking;

        private readonly Bit[] bits = new Bit[BitCount];

        private void BuildHud()
        {
            Transform canvas = GameObject.Find("Canvas")?.transform;
            hud = canvas != null ? canvas.Find("GameplayUI") as RectTransform : null;
            if (hud == null)
                return;
            timerText = hud.Find("SurvivalTimer")?.GetComponent<Text>();
            timerFrame = hud.Find("TimerFrame") as RectTransform;
            scoreRect = hud.Find("ScoreText") as RectTransform;

            backRoot = Layer("PowerUpFxBack");
            backRoot.SetAsFirstSibling(); // under the HUD labels
            frontRoot = Layer("PowerUpFxFront");

            frostEdge = Img(backRoot, "Frost", FxSprites.Frost, Ice);
            Stretch(frostEdge.rectTransform);
            heartEdge = Img(backRoot, "HeartPulse", FxSprites.Vignette, HeartRed);
            Stretch(heartEdge.rectTransform);
            auraScore = Img(backRoot, "ScoreAura", FxSprites.Glow, Gold);
            auraCombo = Img(backRoot, "ComboAura", FxSprites.Glow, Gold);
            if (timerFrame != null)
            {
                heartBorder = Img(backRoot, "TimerHeartGlow", timerFrame.GetComponent<Image>()?.sprite, HeartRed);
                heartBorder.preserveAspect = false;
                iceCover = Img(timerFrame, "Ice", FxSprites.Frost, new Color(0.75f, 0.92f, 1f));
                Inset(iceCover.rectTransform, new Vector2(0.18f, 0.16f), new Vector2(0.82f, 0.84f));
                for (int i = 0; i < crystals.Length; i++)
                    crystals[i] = Img(timerFrame, "Crystal", FxSprites.Shard, new Color(0.82f, 0.95f, 1f));
                crack = Img(timerFrame, "Crack", FxSprites.Crack, Color.white);
                Inset(crack.rectTransform, new Vector2(0.2f, 0.18f), new Vector2(0.8f, 0.82f));
            }
            flash = Img(frontRoot, "Flash", null, Color.white);
            Stretch(flash.rectTransform);
            for (int i = 0; i < BitCount; i++)
            {
                bits[i].Image = Img(frontRoot, "Shard", FxSprites.Shard, Ice);
                bits[i].Image.rectTransform.sizeDelta = new Vector2(16f, 36f);
            }
            ampPopup = Label(frontRoot, "AmplifierPopup", "x2 SCORE!", 84f, Gold);
            RectTransform popupRect = ampPopup.rectTransform;
            popupRect.anchorMin = popupRect.anchorMax = new Vector2(0.5f, 0.66f);
            popupRect.sizeDelta = new Vector2(700f, 120f);
            ampPopup.enableVertexGradient = true;
            ampPopup.colorGradient = new VertexGradient(new Color(1f, 0.97f, 0.7f), new Color(1f, 0.97f, 0.7f), Gold, Gold);
            // Right-aligned to the badge's right edge so it never runs off-screen.
            lockText = Label(frontRoot, "ComboLock", "COMBO LOCK", 38f, SealViolet);
            lockText.rectTransform.sizeDelta = new Vector2(360f, 60f);
            lockText.rectTransform.pivot = new Vector2(1f, 0.5f);
            lockText.alignment = TextAlignmentOptions.Right;
        }

        private RectTransform Layer(string name)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(hud, false);
            Stretch(rect);
            return rect;
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

        private static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color)
        {
            TextMeshProUGUI label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(parent, false);
            GameFont.Apply(label);
            label.fontSharedMaterial = ComboBadge.Outlined(label.font.material, Color.white, 0.2f, true, 0.1f);
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.enabled = false;
            return label;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Inset(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // Hidden images are disabled, so idle full-screen layers cost no fill rate.
        private static void Alpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
                return;
            Color c = graphic.color;
            c.a = Mathf.Clamp01(alpha);
            graphic.color = c;
            graphic.enabled = c.a > 0.004f;
        }

        private static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            float u = Mathf.Clamp01(t) - 1f;
            return 1f + (c1 + 1f) * u * u * u + c1 * u * u;
        }

        // Heart kick: sharp rise, quick decay (0 before it starts).
        private static float Kick(float t) => t < 0f ? 0f : (1f - Mathf.Exp(-t * 60f)) * Mathf.Exp(-t * 9f);

        // ---------------- events ----------------

        private void FreezeAcquired(bool refresh)
        {
            Flash(new Color(0.65f, 0.88f, 1f), 0.6f);
            if (!refresh)
                freezeStart = Time.unscaledTime;
            if (timerFrame != null)
                BurstBits(timerFrame.position, Ice, 12, 260f, 620f, 0.55f);
        }

        private void FreezeEnded(bool playing)
        {
            freezeBlend = 0f;
            if (!playing || timerFrame == null)
                return;
            Flash(new Color(0.75f, 0.92f, 1f), 0.25f);
            BurstBits(timerFrame.position, new Color(0.82f, 0.95f, 1f), 18, 220f, 560f, 0.6f);
        }

        private void HeartAcquired() => heartAcquireStart = Time.unscaledTime;

        private void HeartEnded(bool playing)
        {
            if (playing)
                heartEndStart = Time.unscaledTime;
            else
                heartBlend = 0f;
        }

        private void AmplifierAcquired() => ampPopupStart = Time.unscaledTime;

        private void AmplifierEnded(bool playing)
        {
            if (!playing)
                ampBlend = 0f;
        }

        private void SealAcquired(bool refresh)
        {
            lockTextStart = Time.unscaledTime;
            if (refresh && sealShown && !sealBreaking)
                return;
            sealStart = Time.unscaledTime;
            sealBreaking = false;
            sealShown = true;
        }

        private void SealEnded(bool playing)
        {
            if (!sealShown)
                return;
            if (!playing || chainRoot == null)
            {
                HideChains();
                return;
            }
            sealBreaking = true;
            sealBreakStart = Time.unscaledTime;
            for (int i = 0; i < LinkCount; i++)
            {
                Vector2 outward = links[i].rectTransform.anchoredPosition.normalized;
                linkVelocity[i] = outward * Random.Range(260f, 540f) + Vector2.up * Random.Range(80f, 220f);
                linkSpin[i] = Random.Range(-720f, 720f);
            }
            padlockVelocity = new Vector2(Random.Range(-60f, 60f), 260f);
        }

        private void Flash(Color color, float strength)
        {
            flashColor = color;
            flashStrength = strength;
            flashStart = Time.unscaledTime;
        }

        private void BurstBits(Vector3 world, Color color, int count, float minSpeed, float maxSpeed, float life)
        {
            Vector2 origin = frontRoot.InverseTransformPoint(world);
            for (int n = 0, i = 0; i < BitCount && n < count; i++)
            {
                if (bits[i].Active)
                    continue;
                n++;
                float angle = Random.value * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                bits[i].Active = true;
                bits[i].Start = Time.unscaledTime;
                bits[i].Life = life * Random.Range(0.75f, 1.1f);
                bits[i].Velocity = dir * Random.Range(minSpeed, maxSpeed) + Vector2.up * 120f;
                bits[i].Spin = Random.Range(-600f, 600f);
                RectTransform rect = bits[i].Image.rectTransform;
                rect.localPosition = origin + dir * 20f;
                rect.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
                rect.localScale = Vector3.one * Random.Range(0.7f, 1.4f);
                bits[i].Image.color = color;
                bits[i].Image.enabled = true;
            }
        }

        // ---------------- per frame ----------------

        private void UpdateHud(float dt, bool playing)
        {
            if (hud == null)
                return;
            float now = Time.unscaledTime;
            if (badge == null)
            {
                badge = hud.Find("ComboBadge") as RectTransform;
                if (badge != null)
                {
                    badge.TryGetComponent(out badgeGroup);
                    frontRoot.SetAsLastSibling(); // chains and labels above the badge
                }
            }

            // Flash
            float f = 1f - (now - flashStart) / 0.22f;
            flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
            Alpha(flash, f > 0f ? f * flashStrength : 0f);

            UpdateFreeze(now, dt);
            UpdateHeart(now, dt);
            UpdateAmplifier(now, dt);
            UpdateSeal(now, dt);
            UpdateBits(now, dt);
        }

        private void UpdateFreeze(float now, float dt)
        {
            bool held = Held(PowerUpType.FreezeClock);
            freezeBlend = Mathf.MoveTowards(freezeBlend, held ? 1f : 0f, dt * 6f);
            Alpha(frostEdge, 0.7f * freezeBlend);
            frostEdge.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, freezeBlend);
            if (timerFrame == null)
                return;
            float remaining = manager.Remaining(PowerUpType.FreezeClock);
            float cracking = held ? Mathf.Clamp01((0.7f - remaining) / 0.7f) : 0f;
            Alpha(iceCover, 0.9f * freezeBlend);
            Vector2 shake = cracking > 0f ? Random.insideUnitCircle * (3f * cracking) : Vector2.zero;
            iceCover.rectTransform.anchoredPosition = shake;
            Alpha(crack, cracking * freezeBlend);
            crack.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1f, cracking);

            Rect frame = timerFrame.rect;
            for (int i = 0; i < crystals.Length; i++)
            {
                Image c = crystals[i];
                float angle = (i / (float)crystals.Length) * Mathf.PI * 2f + 0.3f;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                RectTransform rect = c.rectTransform;
                rect.anchoredPosition = new Vector2(dir.x * frame.width * 0.33f, dir.y * frame.height * 0.38f) + shake;
                rect.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f + (i % 2 == 0 ? 12f : -10f));
                float grow = OutBack((now - freezeStart - i * 0.035f) / 0.2f);
                float size = i % 3 == 0 ? 1.25f : i % 3 == 1 ? 0.85f : 1f;
                rect.sizeDelta = new Vector2(22f, 52f) * size;
                rect.localScale = Vector3.one * Mathf.Max(0f, grow) * freezeBlend;
                Alpha(c, freezeBlend);
            }
            if (held && timerText != null)
                timerText.color = IcyText;
        }

        private void UpdateHeart(float now, float dt)
        {
            bool held = Held(PowerUpType.TimeHeart);
            heartBlend = Mathf.MoveTowards(heartBlend, held ? 1f : 0f, dt * 3f);
            // lub-dub every 0.85 s while the heart is held
            float beatPhase = Mathf.Repeat(now, 0.85f);
            float beat = Kick(beatPhase) + 0.6f * Kick(beatPhase - 0.2f);
            Alpha(heartEdge, heartBlend * (0.12f + 0.38f * beat));
            if (heartBorder != null)
            {
                RectTransform rect = heartBorder.rectTransform;
                rect.anchorMin = timerFrame.anchorMin;
                rect.anchorMax = timerFrame.anchorMax;
                rect.pivot = timerFrame.pivot;
                rect.anchoredPosition = timerFrame.anchoredPosition;
                rect.sizeDelta = timerFrame.sizeDelta;
                rect.localScale = timerFrame.localScale * (1.12f + 0.08f * beat);
                Alpha(heartBorder, heartBlend * (0.6f + 0.4f * beat));
            }
            float pop = 0.9f * Kick(now - heartAcquireStart) + 0.6f * Kick(now - heartAcquireStart - 0.22f)
                        + 0.8f * Kick(now - heartEndStart) + 0.06f * beat * heartBlend;
            if (juice != null)
                juice.ExtraTimerPop = pop;
            // Time above the normal 4 s reads in its own colour.
            if (held && timerText != null && timer != null && timer.RemainingTime > GameTimer.BaseDuration)
                timerText.color = HeartText;
        }

        private void UpdateAmplifier(float now, float dt)
        {
            bool held = Held(PowerUpType.SoulAmplifier);
            ampBlend = Mathf.MoveTowards(ampBlend, held ? 1f : 0f, dt * (held ? 3f : 5f));
            float shimmer = 0.8f + 0.2f * Mathf.Sin(now * 5f);
            float grow = held ? OutBack(ampBlend) : ampBlend * ampBlend; // ends by shrinking fast
            if (scoreRect != null)
            {
                RectTransform rect = auraScore.rectTransform;
                rect.position = scoreRect.TransformPoint(scoreRect.rect.center);
                rect.sizeDelta = new Vector2(scoreRect.rect.width * 1.3f, scoreRect.rect.height * 3f);
                rect.localScale = Vector3.one * grow;
                Alpha(auraScore, 0.6f * ampBlend * shimmer);
            }
            if (badge != null)
            {
                RectTransform rect = auraCombo.rectTransform;
                rect.position = badge.TransformPoint(badge.rect.center);
                rect.sizeDelta = badge.rect.size * badge.localScale.x * 1.5f;
                rect.localScale = Vector3.one * grow;
                Alpha(auraCombo, 0.55f * ampBlend * shimmer * (badgeGroup != null ? badgeGroup.alpha : 1f));
            }
            float t = now - ampPopupStart;
            if (t >= 0f && t < 1.3f)
            {
                ampPopup.enabled = true;
                float s = t < 0.25f ? Mathf.LerpUnclamped(0.3f, 1f, OutBack(t / 0.25f)) : 1f;
                ampPopup.rectTransform.localScale = Vector3.one * s;
                ampPopup.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Max(0f, t - 0.7f) * 120f);
                ampPopup.alpha = 1f - Mathf.Clamp01((t - 0.9f) / 0.4f);
            }
            else
            {
                ampPopup.enabled = false;
            }
        }

        private void UpdateSeal(float now, float dt)
        {
            // "COMBO LOCK" pops above the badge on every seal pickup.
            float lt = now - lockTextStart;
            if (lt >= 0f && lt < 1.4f && badge != null)
            {
                lockText.enabled = true;
                Vector3 top = badge.TransformPoint(new Vector3(badge.rect.xMax, badge.rect.yMax, 0f));
                lockText.rectTransform.position = top;
                lockText.rectTransform.anchoredPosition += new Vector2(-6f, 34f + lt * 20f);
                lockText.rectTransform.localScale = Vector3.one * (lt < 0.22f ? Mathf.LerpUnclamped(1.8f, 1f, OutBack(lt / 0.22f)) : 1f);
                lockText.alpha = 1f - Mathf.Clamp01((lt - 1.05f) / 0.35f);
            }
            else
            {
                lockText.enabled = false;
            }

            if (!sealShown)
                return;
            if (chainRoot == null)
            {
                if (badge == null)
                    return;
                BuildChains();
            }
            if (sealBreaking)
            {
                float t = now - sealBreakStart;
                for (int i = 0; i < LinkCount; i++)
                {
                    RectTransform rect = links[i].rectTransform;
                    linkVelocity[i].y -= 1500f * dt;
                    rect.anchoredPosition += linkVelocity[i] * dt;
                    rect.localRotation *= Quaternion.Euler(0f, 0f, linkSpin[i] * dt);
                    Alpha(links[i], 1f - t / 0.55f);
                }
                padlockVelocity.y -= 1500f * dt;
                padlock.rectTransform.anchoredPosition += padlockVelocity * dt;
                padlock.rectTransform.localRotation *= Quaternion.Euler(0f, 0f, 200f * dt);
                Alpha(padlock, 1f - t / 0.6f);
                lockGlow.rectTransform.localScale = Vector3.one * (1f + t * 4f);
                Alpha(lockGlow, 0.9f * (1f - t / 0.3f));
                if (t > 0.65f)
                    HideChains();
                return;
            }
            // Wrap the links on one after another around the badge.
            float rx = 160f, ry = 104f; // the badge hugs the right edge; keep the loop on-screen
            for (int i = 0; i < LinkCount; i++)
            {
                float a = Mathf.PI * 0.5f - i / (float)LinkCount * Mathf.PI * 2f;
                RectTransform rect = links[i].rectTransform;
                rect.anchoredPosition = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                float tangent = Mathf.Atan2(ry * Mathf.Cos(a), -rx * Mathf.Sin(a)) * Mathf.Rad2Deg;
                rect.localRotation = Quaternion.Euler(0f, 0f, tangent);
                float appear = OutBack((now - sealStart - i * 0.02f) / 0.12f);
                float thin = i % 2 == 0 ? 1f : 0.42f; // alternate links seen edge-on
                rect.localScale = new Vector3(Mathf.Max(0f, appear), Mathf.Max(0f, appear) * thin, 1f);
                float glint = 0.85f + 0.15f * Mathf.Sin(now * 4f + i * 0.7f);
                links[i].color = new Color(Steel.r * glint, Steel.g * glint, Steel.b * glint, 0f);
                Alpha(links[i], appear > 0f ? 1f : 0f);
            }
            float lockIn = OutBack((now - sealStart - 0.42f) / 0.2f);
            padlock.rectTransform.anchoredPosition = new Vector2(0f, -ry - 4f);
            padlock.rectTransform.localRotation = Quaternion.identity;
            padlock.rectTransform.localScale = Vector3.one * Mathf.Max(0f, lockIn);
            Alpha(padlock, lockIn > 0f ? 1f : 0f);
            lockGlow.rectTransform.anchoredPosition = padlock.rectTransform.anchoredPosition;
            lockGlow.rectTransform.localScale = Vector3.one * Mathf.Max(0f, lockIn) * (1f + 0.1f * Mathf.Sin(now * 5f));
            Alpha(lockGlow, lockIn > 0f ? 0.55f + 0.25f * Mathf.Sin(now * 5f) : 0f);
        }

        // Chains live on the badge so they follow its punch, slide and fade.
        private void BuildChains()
        {
            chainRoot = (RectTransform)new GameObject("ComboSealChains", typeof(RectTransform)).transform;
            chainRoot.SetParent(badge, false);
            chainRoot.anchorMin = chainRoot.anchorMax = new Vector2(0.5f, 0.5f);
            chainRoot.sizeDelta = Vector2.zero;
            lockGlow = Img(chainRoot, "LockGlow", FxSprites.Glow, SealViolet);
            lockGlow.rectTransform.sizeDelta = new Vector2(130f, 130f);
            for (int i = 0; i < LinkCount; i++)
            {
                links[i] = Img(chainRoot, "Link", FxSprites.ChainLink, Steel);
                links[i].rectTransform.sizeDelta = new Vector2(42f, 21f);
            }
            padlock = Img(chainRoot, "Padlock", FxSprites.Padlock, new Color(0.86f, 0.72f, 1f));
            padlock.rectTransform.sizeDelta = new Vector2(50f, 58f);
        }

        private void HideChains()
        {
            sealShown = false;
            sealBreaking = false;
            if (chainRoot == null)
                return;
            foreach (Image link in links)
                Alpha(link, 0f);
            Alpha(padlock, 0f);
            Alpha(lockGlow, 0f);
        }

        private void UpdateBits(float now, float dt)
        {
            for (int i = 0; i < BitCount; i++)
            {
                if (!bits[i].Active)
                    continue;
                float t = (now - bits[i].Start) / bits[i].Life;
                if (t >= 1f)
                {
                    bits[i].Active = false;
                    bits[i].Image.enabled = false;
                    continue;
                }
                RectTransform rect = bits[i].Image.rectTransform;
                bits[i].Velocity.y -= 1300f * dt;
                rect.localPosition += (Vector3)(bits[i].Velocity * dt);
                rect.localRotation *= Quaternion.Euler(0f, 0f, bits[i].Spin * dt);
                Alpha(bits[i].Image, 1f - t * t);
            }
        }
    }
}
