using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // Combo HUD (2026-10-04 request): a tier word on a hand-painted brush stroke
    // with the hand-lettered combo count below, bottom-right.
    //   1–9 Cool! (blue)  10–24 Great! (green)  25–49 Fantastic! (orange→red)
    //   50+ Unstoppable! (rainbow)
    // Presentation only: reads RunManager.CurrentCombo, never writes game state.
    // The brush uses Resources/HUD/combo_brush if one is supplied, otherwise a
    // procedural dry-brush stroke.
    public sealed class ComboBadge : MonoBehaviour
    {
        private struct Tier
        {
            public int From;
            public string Word;
            public Color Top;
            public Color Bottom;
            public bool Rainbow;
        }

        private static readonly Tier[] Tiers =
        {
            new Tier { From = 1, Word = "Cool!", Top = new Color(0.55f, 0.85f, 1f), Bottom = new Color(0.12f, 0.38f, 1f) },
            new Tier { From = 10, Word = "Great!", Top = new Color(0.7f, 1f, 0.45f), Bottom = new Color(0.1f, 0.78f, 0.25f) },
            new Tier { From = 25, Word = "Fantastic!", Top = new Color(1f, 0.78f, 0.15f), Bottom = new Color(1f, 0.18f, 0.12f) },
            new Tier { From = 50, Word = "Unstoppable!", Rainbow = true },
        };

        private static readonly Color BrushColor = new Color(0.78f, 0.16f, 0.5f);
        private static readonly Color Ink = new Color(0.06f, 0.02f, 0.08f);
        private static Sprite proceduralBrush;

        private RunManager run;
        private CanvasGroup group;
        private RectTransform root;
        private Image brush;
        private TextMeshProUGUI word;
        private TextMeshProUGUI count;
        private Image gauge;
        private PowerUpManager powerUps;
        private int shownCombo;
        private int tierIndex = -1;
        private float showStart = -10f;
        private float countPunchStart = -10f;
        private float tierPunchStart = -10f;
        private float hideStart = -10f;
        private bool visible;

        public static ComboBadge Create(RectTransform hud, RunManager run)
        {
            GameObject go = new GameObject("ComboBadge", typeof(RectTransform), typeof(CanvasGroup));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(hud, false);
            // Bottom-right, 14% up the screen so it clears the bottom edge in any aspect.
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.14f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-18f, 0f);
            rect.sizeDelta = new Vector2(330f, 190f);
            // 0.85 (was 1.25 — 2026-10-11: kept clear of the runner).
            rect.localScale = Vector3.one * 0.85f;
            ComboBadge badge = go.AddComponent<ComboBadge>();
            badge.Build(run);
            return badge;
        }

        private void Build(RunManager owner)
        {
            run = owner;
            root = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            brush = new GameObject("Brush", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            Place(brush.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(320f, 120f), -5f);
            brush.sprite = Resources.Load<Sprite>("HUD/combo_brush") ?? ProceduralBrush();
            brush.type = Image.Type.Filled;
            brush.fillMethod = Image.FillMethod.Horizontal;
            brush.fillOrigin = (int)Image.OriginHorizontal.Left;
            brush.color = BrushColor;
            brush.raycastTarget = false;

            // HiKR-ExtraBold is already heavy (and italic via GameFont): no extra
            // dilation, just a white rim so the ink digits read on the brush.
            count = Label("Count", new Vector2(0.5f, 0f), new Vector2(18f, 56f), new Vector2(240f, 130f), -4f, 110f, true);
            count.color = Ink;
            count.fontSharedMaterial = Outlined(count.font.material, Color.white, 0.18f, false, 0f);

            // Combo timeout gauge along the bottom of the brush; violet and frozen
            // while a Combo Seal holds the combo.
            Image gaugeBack = new GameObject("GaugeBack", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            Place(gaugeBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(8f, 2f), new Vector2(200f, 9f), -5f);
            gaugeBack.color = new Color(0.05f, 0.02f, 0.08f, 0.75f);
            gaugeBack.raycastTarget = false;
            gauge = new GameObject("Gauge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            gauge.rectTransform.SetParent(gaugeBack.rectTransform, false);
            gauge.rectTransform.anchorMin = Vector2.zero;
            gauge.rectTransform.anchorMax = Vector2.one;
            gauge.rectTransform.pivot = new Vector2(0f, 0.5f);
            gauge.rectTransform.offsetMin = new Vector2(2f, 2f);
            gauge.rectTransform.offsetMax = new Vector2(-2f, -2f);
            gauge.raycastTarget = false;
            powerUps = FindFirstObjectByType<PowerUpManager>();

            word = Label("Word", new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(330f, 84f), 6f, 64f, false);
            word.color = Color.white;
            word.fontSharedMaterial = Outlined(word.font.material, Color.white, 0.22f, true, 0.11f);
            word.enableVertexGradient = true;
        }

        private TextMeshProUGUI Label(string name, Vector2 anchor, Vector2 position, Vector2 size, float angle,
            float fontSize, bool numeric)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            if (numeric)
                go.AddComponent<NumericLabel>();
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            GameFont.Apply(text);
            Place(text.rectTransform, anchor, position, size, angle);
            // Long words ("Unstoppable!") shrink to fit instead of spilling off-screen.
            text.enableAutoSizing = true;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = fontSize * 0.5f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, float angle)
        {
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        // Font stays the shared asset (font rule); only the material gets an
        // outline (and a drop shadow for the tier word).
        internal static Material Outlined(Material source, Color outline, float width, bool shadow, float dilate)
        {
            Material material = new Material(source) { name = source.name + " (Combo)" };
            material.EnableKeyword("OUTLINE_ON");
            material.SetColor(ShaderUtilities.ID_OutlineColor, outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            material.SetFloat(ShaderUtilities.ID_FaceDilate, dilate);
            if (shadow)
            {
                material.EnableKeyword("UNDERLAY_ON");
                material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.1f, 0f, 0.15f, 0.85f));
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
                material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.1f);
            }
            return material;
        }

        private void LateUpdate()
        {
            if (run == null)
                return;
            float now = Time.unscaledTime;
            int combo = run.CurrentCombo;
            if (combo > 0 && combo != shownCombo)
            {
                if (!visible)
                {
                    visible = true;
                    showStart = now;
                }
                countPunchStart = now;
                count.text = combo.ToString();
                int tier = TierFor(combo);
                if (tier != tierIndex)
                {
                    tierIndex = tier;
                    tierPunchStart = now;
                    showStart = Mathf.Max(showStart, now - 0.02f); // re-wipe the brush on a tier up
                    word.text = Tiers[tier].Word;
                    if (!Tiers[tier].Rainbow)
                        word.colorGradient = new VertexGradient(Tiers[tier].Top, Tiers[tier].Top,
                            Tiers[tier].Bottom, Tiers[tier].Bottom);
                    word.enableVertexGradient = !Tiers[tier].Rainbow;
                }
            }
            else if (combo == 0 && visible)
            {
                visible = false;
                hideStart = now;
                tierIndex = -1;
            }
            shownCombo = combo;
            Animate(now);
        }

        private static int TierFor(int combo)
        {
            for (int i = Tiers.Length - 1; i > 0; i--)
                if (combo >= Tiers[i].From)
                    return i;
            return 0;
        }

        private void Animate(float now)
        {
            if (visible)
            {
                float s = Mathf.Clamp01((now - showStart) / 0.16f);
                group.alpha = 1f;
                brush.fillAmount = 1f - (1f - s) * (1f - s);
                root.anchoredPosition = new Vector2(-18f, 0f);
            }
            else
            {
                float h = Mathf.Clamp01((now - hideStart) / 0.25f);
                group.alpha = 1f - h;
                root.anchoredPosition = new Vector2(-18f + h * h * 60f, 0f);
                if (h >= 1f)
                    return;
            }

            float wt = Mathf.Clamp01((now - tierPunchStart) / 0.3f);
            // Kept modest so a long word ("Unstoppable!") never punches past the screen edge.
            word.rectTransform.localScale = Vector3.one * (wt < 1f ? Mathf.LerpUnclamped(1.45f, 1f, OutBack(wt)) : 1f);
            float ct = Mathf.Clamp01((now - countPunchStart) / 0.2f);
            count.rectTransform.localScale = Vector3.one * (ct < 1f ? Mathf.LerpUnclamped(1.55f, 1f, OutBack(ct)) : 1f);
            count.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -4f + (1f - ct) * 10f);

            bool sealedCombo = powerUps != null && powerUps.ComboSealed;
            gauge.rectTransform.localScale = new Vector3(run.ComboTimeLeft01, 1f, 1f);
            gauge.color = sealedCombo ? new Color(0.72f, 0.5f, 1f) : Color.white;

            if (tierIndex >= 0 && Tiers[tierIndex].Rainbow)
                PaintRainbow(now);
        }

        // Per-letter hue that scrolls across the word.
        private void PaintRainbow(float now)
        {
            word.ForceMeshUpdate();
            TMP_TextInfo info = word.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo c = info.characterInfo[i];
                if (!c.isVisible)
                    continue;
                Color32[] colors = info.meshInfo[c.materialReferenceIndex].colors32;
                float hue = Mathf.Repeat(now * 0.6f - i * 0.085f, 1f);
                Color32 top = Color.HSVToRGB(hue, 0.55f, 1f);
                Color32 bottom = Color.HSVToRGB(Mathf.Repeat(hue + 0.06f, 1f), 0.95f, 1f);
                colors[c.vertexIndex] = bottom;
                colors[c.vertexIndex + 1] = top;
                colors[c.vertexIndex + 2] = top;
                colors[c.vertexIndex + 3] = bottom;
            }
            word.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        private static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            float u = t - 1f;
            return 1f + (c1 + 1f) * u * u * u + c1 * u * u;
        }

        // Dry-brush stroke: ragged edges, bristle streaks at both ends, a few
        // flecks. White so the Image color tints it.
        private static Sprite ProceduralBrush()
        {
            if (proceduralBrush != null)
                return proceduralBrush;
            const int w = 512, h = 192;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Combo Brush (Procedural)" };
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    // Thick in the middle, tapering to dry ends.
                    float body = Mathf.Sin(Mathf.Clamp01((u - 0.03f) / 0.94f) * Mathf.PI);
                    float half = 0.42f * Mathf.Pow(body, 0.25f) +
                                 0.04f * (Mathf.PerlinNoise(u * 18f, 3.1f) - 0.5f);
                    float centre = 0.5f + 0.06f * (Mathf.PerlinNoise(u * 3f, 7.7f) - 0.5f);
                    float edge = half - Mathf.Abs(v - centre);
                    float alpha = Mathf.Clamp01(edge * h * 0.5f);
                    // Bristle streaks only where the brush runs dry at the ends.
                    float dryness = Mathf.Clamp01(1f - body * 2.4f);
                    float bristle = Mathf.PerlinNoise(v * 70f, u * 2.5f);
                    if (bristle < dryness)
                        alpha *= 0.15f;
                    // Paint density varies a little along the stroke.
                    alpha *= 0.93f + 0.07f * Mathf.PerlinNoise(u * 9f, v * 4f);
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            }
            var random = new System.Random(7);
            for (int i = 0; i < 26; i++)
            {
                bool right = i % 2 == 0;
                float cx = right ? 0.86f + (float)random.NextDouble() * 0.13f : 0.01f + (float)random.NextDouble() * 0.12f;
                float cy = 0.18f + (float)random.NextDouble() * 0.64f;
                float r = 1.5f + (float)random.NextDouble() * 4f;
                for (int y = (int)(cy * h - r) - 1; y <= (int)(cy * h + r) + 1; y++)
                for (int x = (int)(cx * w - r) - 1; x <= (int)(cx * w + r) + 1; x++)
                {
                    if (x < 0 || y < 0 || x >= w || y >= h)
                        continue;
                    float d = r - Mathf.Sqrt((x - cx * w) * (x - cx * w) + (y - cy * h) * (y - cy * h));
                    byte a = (byte)(Mathf.Clamp01(d) * 255f);
                    if (a > pixels[y * w + x].a)
                        pixels[y * w + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            proceduralBrush = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return proceduralBrush;
        }
    }
}
