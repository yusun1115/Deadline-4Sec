using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadline4Sec
{
    // Procedural white sprites for the power-up HUD effects, built once and
    // tinted by Image.color. A painted replacement can be dropped into
    // Resources/HUD/<name> (e.g. HUD/fx_frost) and is used instead.
    public static class FxSprites
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => Cache.Clear();

        // Every sprite, so the loading screen can build them before the first run.
        public static readonly Func<Sprite>[] All =
        {
            () => Vignette, () => Frost, () => Glow, () => Ring, () => Shard, () => Crack,
            () => ChainLink, () => Padlock, () => EdgeGlow, () => Streak,
        };

        public static Sprite Vignette => Get("fx_vignette", 256, 256, VignetteAlpha);
        public static Sprite Frost => Get("fx_frost", 256, 256, FrostAlpha);
        public static Sprite Glow => Get("fx_glow", 128, 128, GlowAlpha);
        public static Sprite Ring => Get("fx_ring", 256, 256, RingAlpha);
        public static Sprite Shard => Get("fx_shard", 64, 128, ShardAlpha);
        public static Sprite Crack => Get("fx_crack", 256, 128, null, CrackPixels);
        public static Sprite ChainLink => Get("fx_chain_link", 96, 48, LinkAlpha);
        public static Sprite Padlock => Get("fx_padlock", 96, 112, PadlockAlpha);
        public static Sprite EdgeGlow => Get("fx_edge_glow", 128, 32, EdgeGlowAlpha);
        public static Sprite Streak => Get("fx_streak", 128, 16, StreakAlpha);

        // Bright at the left edge (u = 0) fading inward; rotate for other edges.
        private static float EdgeGlowAlpha(float u, float v)
        {
            float fall = 1f - u;
            return fall * fall * fall * Mathf.Sin(v * Mathf.PI);
        }

        // Thin speed line with soft tapered ends.
        private static float StreakAlpha(float u, float v)
        {
            float across = 1f - Mathf.Abs(v * 2f - 1f);
            return across * across * Mathf.Sin(u * Mathf.PI) * (0.4f + 0.6f * u);
        }

        private static Sprite Get(string name, int w, int h, Func<float, float, float> alpha,
            Func<int, int, float[]> pixels = null)
        {
            if (Cache.TryGetValue(name, out Sprite sprite) && sprite != null)
                return sprite;
            sprite = Resources.Load<Sprite>("HUD/" + name);
            if (sprite == null)
            {
                float[] a = pixels != null ? pixels(w, h) : null;
                var colors = new Color32[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float value = a != null ? a[y * w + x] : alpha((x + 0.5f) / w, (y + 0.5f) / h);
                    colors[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(value) * 255f));
                }
                var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                };
                texture.SetPixels32(colors);
                texture.Apply(false, true);
                sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = name;
            }
            Cache[name] = sprite;
            return sprite;
        }

        // Clear centre, opaque edges (screen-edge pulses).
        private static float VignetteAlpha(float u, float v)
        {
            float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
            float d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * 0.55f + Mathf.Sqrt(dx * dx + dy * dy) * 0.45f;
            return Mathf.SmoothStep(0f, 1f, (d - 0.62f) / 0.4f);
        }

        // Frost creeping in from the edges: ridged noise with needle streaks.
        private static float FrostAlpha(float u, float v)
        {
            // The layer is stretched over a portrait screen, so the vertical
            // distance counts ~1.8x to keep the frost band equally thin all round.
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v) * 1.8f);
            float n = Mathf.PerlinNoise(u * 9f, v * 9f) * 0.6f + Mathf.PerlinNoise(u * 31f, v * 31f) * 0.4f;
            float ridge = 1f - Mathf.Abs(n * 2f - 1f);
            float needles = Mathf.PerlinNoise(u * 80f + v * 3f, v * 6f) * Mathf.PerlinNoise(u * 4f, v * 70f + u * 3f);
            float reach = 0.06f + 0.08f * n;
            float body = Mathf.Clamp01((reach - edge) / reach);
            return body * body * (0.45f + ridge * 0.45f) + Mathf.Clamp01(needles * 2.4f - 0.9f) * body;
        }

        private static float GlowAlpha(float u, float v)
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float a = Mathf.Clamp01(1f - d);
            return a * a;
        }

        private static float RingAlpha(float u, float v)
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            return Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.1f);
        }

        // Elongated ice crystal, brighter along its spine.
        private static float ShardAlpha(float u, float v)
        {
            float half = 0.5f * (1f - Mathf.Abs(v * 2f - 1f)) * (v < 0.5f ? 0.55f : 1f);
            float d = Mathf.Abs(u - 0.5f);
            if (d > half)
                return 0f;
            float spine = 1f - d / Mathf.Max(0.001f, half);
            return Mathf.Clamp01((half - d) * 40f) * (0.55f + 0.45f * spine);
        }

        // Stadium-shaped chain link outline.
        private static float LinkAlpha(float u, float v)
        {
            float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;
            float cx = Mathf.Max(0f, Mathf.Abs(x) - 0.5f);
            float d = Mathf.Sqrt(cx * cx * 4f + y * y);
            return Mathf.Clamp01(1f - Mathf.Abs(d - 0.7f) / 0.22f) * 1.6f;
        }

        // Padlock: shackle arc over a body with a keyhole.
        private static float PadlockAlpha(float u, float v)
        {
            if (v < 0.58f)
            {
                bool body = u > 0.12f && u < 0.88f && v > 0.04f;
                float kx = (u - 0.5f) / 0.08f, ky = (v - 0.32f) / 0.08f;
                bool keyhole = kx * kx + ky * ky < 1f || (Mathf.Abs(u - 0.5f) < 0.035f && v > 0.16f && v < 0.32f);
                return body && !keyhole ? 1f : 0f;
            }
            float dx = (u - 0.5f) / 0.3f, dy = (v - 0.58f) / 0.36f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            return r > 0.68f && r < 1f ? 1f : 0f;
        }

        // Branching crack lines from the centre (random walk).
        private static float[] CrackPixels(int w, int h)
        {
            var a = new float[w * h];
            var random = new System.Random(5);
            for (int branch = 0; branch < 9; branch++)
            {
                float x = w * 0.5f, y = h * 0.5f;
                float angle = branch * Mathf.PI * 2f / 9f + (float)random.NextDouble() * 0.5f;
                int steps = 30 + random.Next(40);
                for (int i = 0; i < steps; i++)
                {
                    angle += ((float)random.NextDouble() - 0.5f) * 0.9f;
                    x += Mathf.Cos(angle) * 2.2f;
                    y += Mathf.Sin(angle) * 1.4f;
                    float width = Mathf.Lerp(2.2f, 0.6f, i / (float)steps);
                    Dot(a, w, h, x, y, width);
                    if (random.NextDouble() < 0.06)
                    {
                        float bx = x, by = y, ba = angle + ((float)random.NextDouble() > 0.5 ? 0.9f : -0.9f);
                        for (int k = 0; k < 12; k++)
                        {
                            bx += Mathf.Cos(ba) * 2f;
                            by += Mathf.Sin(ba) * 1.3f;
                            Dot(a, w, h, bx, by, 0.7f);
                        }
                    }
                }
            }
            return a;
        }

        private static void Dot(float[] a, int w, int h, float cx, float cy, float r)
        {
            for (int y = (int)(cy - r - 1); y <= (int)(cy + r + 1); y++)
            for (int x = (int)(cx - r - 1); x <= (int)(cx + r + 1); x++)
            {
                if (x < 0 || y < 0 || x >= w || y >= h)
                    continue;
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                a[y * w + x] = Mathf.Max(a[y * w + x], Mathf.Clamp01(r + 0.5f - d));
            }
        }
    }
}
