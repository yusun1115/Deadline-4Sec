using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // Skin shop card theme (2026-10-30): a light touch per weapon on the shared
    // card — an inner border line, a glow behind the icon and a few small
    // animated bits — so the cards stay one family:
    //   STARLIGHT gold glow + twinkles, FROST BITE frost edges + snow,
    //   BLOOD MOON red cracks + embers, CYBER DEATH hologram scan lines +
    //   corner brackets, ECLIPSE violet ring + gold corner diamonds.
    // Built at runtime from FxSprites (catalog order; the default card stays plain).
    public sealed class SkinCardTheme : MonoBehaviour
    {
        private enum Motion { Twinkle, Fall, Rise, Blink, Scan, Spin, Pulse }

        private sealed class Bit
        {
            public RectTransform Rect;
            public Image Image;
            public Motion Motion;
            public float Phase, Speed, BaseAlpha;
            public Vector2 Home;
        }

        private readonly List<Bit> bits = new List<Bit>();
        private RectTransform back;
        private RectTransform front;
        private Vector2 iconCenter;
        private Vector2 half;

        private int themeIndex;
        private RectTransform themeIcon;
        private bool built;

        // Built on the first frame the card has its laid-out size.
        public void Build(int skinIndex, RectTransform icon)
        {
            themeIndex = skinIndex;
            themeIcon = icon;
        }

        private void BuildNow()
        {
            built = true;
            int skinIndex = themeIndex;
            RectTransform icon = themeIcon;
            RectTransform card = (RectTransform)transform;
            half = card.rect.size * 0.5f;
            iconCenter = icon != null ? icon.anchoredPosition : new Vector2(0f, 62f);
            int iconSibling = icon != null ? icon.GetSiblingIndex() : transform.childCount;
            back = Layer("ThemeBack", iconSibling);
            front = Layer("ThemeFront", (icon != null ? icon.GetSiblingIndex() : transform.childCount - 1) + 1);

            switch (skinIndex)
            {
                case 1: // STARLIGHT
                    Border(new Color(1f, 0.85f, 0.35f, 0.7f));
                    Add(back, FxSprites.Glow, iconCenter, new Vector2(190f, 190f), new Color(1f, 0.8f, 0.25f), 0.45f, Motion.Pulse, 2.2f);
                    for (int i = 0; i < 6; i++)
                        Add(front, FxSprites.Glow, iconCenter + Random(70f, 60f), Vector2.one * UnityEngine.Random.Range(12f, 22f),
                            i % 2 == 0 ? Color.white : new Color(1f, 0.88f, 0.4f), 1f, Motion.Twinkle, UnityEngine.Random.Range(2.2f, 3.6f));
                    break;
                case 2: // FROST BITE
                    Border(new Color(0.55f, 0.88f, 1f, 0.7f));
                    Add(back, FxSprites.Frost, Vector2.zero, half * 2f, new Color(0.6f, 0.9f, 1f), 0.55f, Motion.Pulse, 0.8f);
                    Add(back, FxSprites.Glow, iconCenter, new Vector2(170f, 170f), new Color(0.4f, 0.8f, 1f), 0.3f, Motion.Pulse, 1.5f);
                    for (int i = 0; i < 7; i++)
                        Add(front, FxSprites.Glow, new Vector2(UnityEngine.Random.Range(-half.x + 16f, half.x - 16f), UnityEngine.Random.Range(-half.y, half.y)),
                            Vector2.one * UnityEngine.Random.Range(6f, 11f), new Color(0.85f, 0.97f, 1f), 0.8f, Motion.Fall, UnityEngine.Random.Range(18f, 32f));
                    break;
                case 3: // BLOOD MOON
                    Border(new Color(0.85f, 0.08f, 0.16f, 0.75f));
                    Add(back, FxSprites.Glow, iconCenter, new Vector2(170f, 170f), new Color(1f, 0.1f, 0.15f), 0.32f, Motion.Pulse, 1.4f);
                    Add(back, FxSprites.Crack, new Vector2(-half.x + 56f, half.y - 40f), new Vector2(150f, 75f), new Color(1f, 0.12f, 0.2f), 0.5f, Motion.Pulse, 1f);
                    Add(back, FxSprites.Crack, new Vector2(half.x - 46f, iconCenter.y - 34f), new Vector2(110f, 55f), new Color(1f, 0.12f, 0.2f), 0.4f, Motion.Pulse, 0.7f);
                    for (int i = 0; i < 6; i++)
                        Add(front, FxSprites.Glow, iconCenter + Random(60f, 70f), Vector2.one * UnityEngine.Random.Range(7f, 12f),
                            new Color(1f, 0.35f, 0.15f), 0.9f, Motion.Rise, UnityEngine.Random.Range(22f, 36f));
                    break;
                case 4: // CYBER DEATH
                    Border(new Color(0.3f, 1f, 0.45f, 0.65f));
                    Add(back, FxSprites.Glow, iconCenter, new Vector2(170f, 170f), new Color(0.25f, 1f, 0.4f), 0.25f, Motion.Pulse, 3f);
                    for (int i = 0; i < 3; i++)
                        Add(front, null, new Vector2(0f, iconCenter.y + 60f - i * 40f), new Vector2(half.x * 2f - 28f, 2f),
                            new Color(0.35f, 1f, 0.5f), 0.45f, Motion.Scan, 46f + i * 9f);
                    for (int i = 0; i < 8; i++)
                        Add(front, null, iconCenter + Random(78f, 68f), Vector2.one * UnityEngine.Random.Range(4f, 7f),
                            new Color(0.4f, 1f, 0.55f), 0.9f, Motion.Blink, UnityEngine.Random.Range(5f, 9f));
                    Brackets(new Color(0.35f, 1f, 0.5f, 0.85f));
                    break;
                case 5: // ECLIPSE
                    Border(new Color(0.66f, 0.32f, 1f, 0.8f));
                    Border(new Color(1f, 0.82f, 0.4f, 0.55f), 11f, 1.5f);
                    Add(back, FxSprites.Glow, iconCenter, new Vector2(180f, 180f), new Color(0.6f, 0.25f, 1f), 0.4f, Motion.Pulse, 1.2f);
                    Add(back, FxSprites.Ring, iconCenter, new Vector2(128f, 128f), new Color(1f, 0.78f, 0.4f), 0.35f, Motion.Spin, 18f);
                    foreach (Vector2 corner in new[] { new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, -1f) })
                    {
                        Bit diamond = Add(front, null, new Vector2(corner.x * (half.x - 11f), corner.y * (half.y - 11f)), new Vector2(11f, 11f),
                            new Color(1f, 0.82f, 0.4f), 1f, Motion.Pulse, 2f);
                        diamond.Rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    }
                    for (int i = 0; i < 4; i++)
                        Add(front, FxSprites.Glow, iconCenter + Random(70f, 60f), Vector2.one * UnityEngine.Random.Range(8f, 13f),
                            new Color(0.75f, 0.45f, 1f), 0.9f, Motion.Twinkle, UnityEngine.Random.Range(1.6f, 2.6f));
                    break;
            }
        }

        private RectTransform Layer(string name, int sibling)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.SetSiblingIndex(Mathf.Clamp(sibling, 0, transform.childCount - 1));
            return rect;
        }

        private static Vector2 Random(float x, float y) =>
            new Vector2(UnityEngine.Random.Range(-x, x), UnityEngine.Random.Range(-y, y));

        private Bit Add(RectTransform layer, Sprite sprite, Vector2 position, Vector2 size, Color color, float alpha,
            Motion motion, float speed)
        {
            Image image = new GameObject("Bit", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(layer, false);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            image.sprite = sprite;
            image.raycastTarget = false;
            image.color = new Color(color.r, color.g, color.b, alpha);
            var bit = new Bit
            {
                Rect = image.rectTransform, Image = image, Motion = motion, Speed = speed, BaseAlpha = alpha,
                Phase = UnityEngine.Random.value * 10f, Home = position,
            };
            bits.Add(bit);
            return bit;
        }

        // Thin line just inside the card edge (four bars).
        private void Border(Color color, float inset = 7f, float thickness = 2f)
        {
            float w = half.x * 2f - inset * 2f, h = half.y * 2f - inset * 2f;
            Bar(new Vector2(0f, half.y - inset), new Vector2(w, thickness), color);
            Bar(new Vector2(0f, -half.y + inset), new Vector2(w, thickness), color);
            Bar(new Vector2(-half.x + inset, 0f), new Vector2(thickness, h), color);
            Bar(new Vector2(half.x - inset, 0f), new Vector2(thickness, h), color);
        }

        // Hologram corner brackets (L shapes).
        private void Brackets(Color color)
        {
            const float Arm = 22f, Thick = 3f, Inset = 12f;
            foreach (Vector2 c in new[] { new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, -1f) })
            {
                Vector2 corner = new Vector2(c.x * (half.x - Inset), c.y * (half.y - Inset));
                Bar(corner - new Vector2(c.x * Arm * 0.5f, 0f), new Vector2(Arm, Thick), color);
                Bar(corner - new Vector2(0f, c.y * Arm * 0.5f), new Vector2(Thick, Arm), color);
            }
        }

        private void Bar(Vector2 position, Vector2 size, Color color)
        {
            Image bar = new GameObject("Line", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            bar.rectTransform.SetParent(back, false);
            bar.rectTransform.anchoredPosition = position;
            bar.rectTransform.sizeDelta = size;
            bar.color = color;
            bar.raycastTarget = false;
        }

        private void Update()
        {
            if (!built)
            {
                // Wait for the list layout (cards are 290 tall once laid out).
                if (((RectTransform)transform).rect.height < 200f)
                    return;
                BuildNow();
            }
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            foreach (Bit bit in bits)
            {
                float t = now + bit.Phase;
                float alpha = bit.BaseAlpha;
                switch (bit.Motion)
                {
                    case Motion.Twinkle:
                        float tw = Mathf.Max(0f, Mathf.Sin(t * bit.Speed));
                        alpha *= tw * tw;
                        bit.Rect.localScale = Vector3.one * (0.6f + 0.6f * tw);
                        break;
                    case Motion.Pulse:
                        alpha *= 0.75f + 0.25f * Mathf.Sin(t * bit.Speed);
                        break;
                    case Motion.Fall:
                    case Motion.Rise:
                        float dir = bit.Motion == Motion.Fall ? -1f : 1f;
                        Vector2 p = bit.Rect.anchoredPosition + new Vector2(Mathf.Sin(t * 1.3f) * 6f * dt, dir * bit.Speed * dt);
                        if (p.y < -half.y + 6f || p.y > half.y - 6f)
                            p = new Vector2(bit.Home.x + UnityEngine.Random.Range(-20f, 20f), dir < 0f ? half.y - 8f : -half.y * 0.2f + UnityEngine.Random.Range(-20f, 20f));
                        bit.Rect.anchoredPosition = p;
                        float edge = Mathf.Clamp01((half.y - Mathf.Abs(p.y)) / 40f);
                        alpha *= edge * (dir > 0f ? Mathf.Clamp01((half.y * 0.8f - p.y) / (half.y * 0.8f)) : 1f);
                        break;
                    case Motion.Blink:
                        alpha *= Mathf.Sin(t * bit.Speed) > 0.35f ? 1f : 0.1f;
                        break;
                    case Motion.Scan:
                        float y = bit.Rect.anchoredPosition.y - bit.Speed * dt;
                        if (y < iconCenter.y - 70f)
                            y = iconCenter.y + 70f;
                        bit.Rect.anchoredPosition = new Vector2(0f, y);
                        alpha *= 0.6f + 0.4f * Mathf.PerlinNoise(t * 9f, bit.Phase);
                        break;
                    case Motion.Spin:
                        bit.Rect.localRotation = Quaternion.Euler(0f, 0f, -t * bit.Speed);
                        alpha *= 0.8f + 0.2f * Mathf.Sin(t * 1.5f);
                        break;
                }
                Color c = bit.Image.color;
                c.a = alpha;
                bit.Image.color = c;
            }
        }
    }
}
