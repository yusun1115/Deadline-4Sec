using System.Collections.Generic;
using UnityEngine;

namespace Deadline4Sec
{
    // Weapon skin VFX (2026-11-21): the small shapes the skin effects are made
    // of, drawn once in code (no texture assets) and shared: one material per
    // shape and blend, created on first use and kept for the session, so no
    // material is ever made per swing.
    public static class WeaponVfxSprites
    {
        public enum Shape
        {
            Dot, Star, Crystal, Shard, Zero, One, Pixel, Drop, Smoke, Ring,
            TrailSmooth, TrailStars, TrailIce, TrailHeavy, TrailDigital, TrailVoid,
        }

        private const int Size = 32;
        private static readonly Dictionary<int, Material> Materials = new Dictionary<int, Material>();
        private static readonly Dictionary<Shape, Texture2D> Textures = new Dictionary<Shape, Texture2D>();
        private static Shader shader;

        // dark: alpha-blended (a colour that can be darker than what's behind it).
        public static Material Get(Shape shape, bool dark = false)
        {
            int key = (int)shape * 2 + (dark ? 1 : 0);
            if (Materials.TryGetValue(key, out Material cached) && cached != null)
                return cached;
            if (shader == null)
                shader = Resources.Load<Shader>("Feedback/FxSprite");
            if (shader == null)
                return null;
            Material m = new Material(shader) { name = "Weapon Vfx " + shape + (dark ? " Dark" : "") };
            m.SetTexture("_MainTex", Texture(shape));
            m.SetFloat("_Additive", dark ? 0f : 1f);
            m.SetFloat("_SrcBlend", dark ? (float)UnityEngine.Rendering.BlendMode.SrcAlpha : 1f);
            m.SetFloat("_DstBlend", dark ? (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : 1f);
            if (shape == Shape.Zero || shape == Shape.One || shape == Shape.Pixel)
                m.SetFloat("_Boost", 1.8f);
            Materials[key] = m;
            return m;
        }

        public static Texture2D Texture(Shape shape)
        {
            if (Textures.TryGetValue(shape, out Texture2D cached) && cached != null)
                return cached;
            bool trail = shape >= Shape.TrailSmooth;
            int w = trail ? 128 : Size, h = trail ? 32 : Size;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "WeaponVfx " + shape,
                wrapMode = trail ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                filterMode = shape == Shape.Zero || shape == Shape.One || shape == Shape.Pixel || shape == Shape.TrailDigital
                    ? FilterMode.Point : FilterMode.Bilinear,
            };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    float a = Mathf.Clamp01(Alpha(shape, u, v, x, y, w, h));
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            Textures[shape] = tex;
            return tex;
        }

        // 5x7 glyphs for the CYBER DEATH digits.
        private static readonly string[] ZeroGlyph = { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." };
        private static readonly string[] OneGlyph = { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." };

        private static float Alpha(Shape shape, float u, float v, int x, int y, int w, int h)
        {
            float cx = u * 2f - 1f, cy = v * 2f - 1f;
            float r = Mathf.Sqrt(cx * cx + cy * cy);
            switch (shape)
            {
                case Shape.Dot:
                    return Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
                case Shape.Smoke:
                    // Lumpy soft puff.
                    float lump = 0.82f + 0.18f * Mathf.Sin(Mathf.Atan2(cy, cx) * 5f + 1.3f);
                    return Mathf.Pow(Mathf.Clamp01(1f - r / lump), 1.4f) * 0.9f;
                case Shape.Ring:
                    return Mathf.Clamp01(1f - Mathf.Abs(r - 0.72f) * 9f) + Mathf.Pow(Mathf.Clamp01(1f - r), 4f) * 0.3f;
                case Shape.Star:
                {
                    // Four-point twinkle: two thin crossing spikes and a core.
                    float ax = Mathf.Abs(cx), ay = Mathf.Abs(cy);
                    float arm1 = Mathf.Clamp01(1f - ay * 9f - ax * 1.05f);
                    float arm2 = Mathf.Clamp01(1f - ax * 9f - ay * 1.05f);
                    float diag = Mathf.Clamp01(1f - Mathf.Abs(ax - ay) * 7f - (ax + ay) * 1.4f) * 0.5f;
                    float core = Mathf.Exp(-r * r * 30f);
                    return Mathf.Max(Mathf.Max(arm1, arm2), Mathf.Max(core, diag));
                }
                case Shape.Crystal:
                {
                    // Six-armed snowflake with side barbs.
                    float best = 0f;
                    for (int k = 0; k < 6; k++)
                    {
                        float ang = k * Mathf.PI / 3f;
                        Vector2 d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        float along = cx * d.x + cy * d.y;
                        float across = Mathf.Abs(-cx * d.y + cy * d.x);
                        if (along < 0f || along > 0.92f)
                            continue;
                        best = Mathf.Max(best, Mathf.Clamp01(1f - across * 14f));
                        // A barb pair half way out.
                        float bx = along - 0.5f;
                        float barb = Mathf.Abs(Mathf.Abs(bx) - across * 0.9f);
                        if (bx > -0.25f && bx < 0.05f)
                            best = Mathf.Max(best, Mathf.Clamp01(1f - barb * 16f) * 0.8f);
                    }
                    return Mathf.Max(best, Mathf.Exp(-r * r * 40f));
                }
                case Shape.Shard:
                    // Long thin diamond, a sliver of ice / crystal.
                    return Mathf.Clamp01((1f - (Mathf.Abs(cx) * 3.6f + Mathf.Abs(cy) * 1.05f)) * 2.5f);
                case Shape.Drop:
                {
                    // Point up, round at the bottom.
                    float body = Mathf.Clamp01(1f - Mathf.Sqrt(cx * cx + (cy + 0.25f) * (cy + 0.25f)) / 0.55f);
                    float tip = cy > -0.25f ? Mathf.Clamp01(1f - Mathf.Abs(cx) / Mathf.Max(0.01f, 0.55f * (1f - (cy + 0.25f) / 1.15f))) : 0f;
                    return Mathf.Pow(Mathf.Max(body, tip * (cy < 0.9f ? 1f : 0f)), 0.6f);
                }
                case Shape.Pixel:
                    return Mathf.Max(Mathf.Abs(cx), Mathf.Abs(cy)) < 0.62f ? 1f : Mathf.Max(Mathf.Abs(cx), Mathf.Abs(cy)) < 0.8f ? 0.35f : 0f;
                case Shape.Zero:
                case Shape.One:
                {
                    string[] glyph = shape == Shape.Zero ? ZeroGlyph : OneGlyph;
                    // 5x7 cells in the middle of the 32x32 square (4 px cells).
                    int gx = (x - 6) / 4, gy = 6 - (y - 2) / 4;
                    if (x < 6 || gx >= 5 || y < 2 || gy < 0 || gy >= 7)
                        return 0f;
                    return glyph[gy][gx] == '#' ? 1f : 0f;
                }
                // Trails: u along the trail (repeats), v across it.
                case Shape.TrailSmooth:
                    return Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(cy)), 1.6f);
                case Shape.TrailStars:
                {
                    float band = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(cy)), 2.4f) * 0.55f;
                    // Two sparkles per tile.
                    float best = 0f;
                    foreach (Vector2 c in new[] { new Vector2(0.25f, 0.62f), new Vector2(0.72f, 0.3f) })
                    {
                        float dx = Mathf.Abs((u - c.x) * w / h), dy = Mathf.Abs(v - c.y);
                        float s = Mathf.Max(Mathf.Clamp01(1f - dy * 14f - dx * 2.2f), Mathf.Clamp01(1f - dx * 14f - dy * 2.2f));
                        best = Mathf.Max(best, s);
                    }
                    return Mathf.Max(band, best);
                }
                case Shape.TrailIce:
                {
                    // Broken edge: a jagged band, sharp-toothed on both sides.
                    float teeth = 0.55f + 0.35f * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 7f)) * (0.6f + 0.4f * Mathf.Sin(u * 23f));
                    return Mathf.Abs(cy) < teeth ? Mathf.Lerp(1f, 0.55f, Mathf.Abs(cy) / teeth) : 0f;
                }
                case Shape.TrailHeavy:
                    // Thick, solid body with a hot core.
                    return Mathf.Clamp01((1f - Mathf.Abs(cy)) * 1.6f) * 0.85f + Mathf.Exp(-cy * cy * 20f) * 0.3f;
                case Shape.TrailDigital:
                {
                    // Segments with gaps and a stepped thickness: a broken signal.
                    int seg = x / 16;
                    bool gap = (x % 16) >= 12;
                    float half = 0.35f + 0.5f * (((seg * 7) % 5) / 4f);
                    if (gap || Mathf.Abs(cy) > half)
                        return 0f;
                    return (y % 4 == 0) ? 0.45f : 1f; // scan lines
                }
                case Shape.TrailVoid:
                    // Rim bright, core hollow (the dark core is the slash's job).
                    return Mathf.Clamp01(1f - Mathf.Abs(Mathf.Abs(cy) - 0.6f) * 3.2f) + Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(cy)), 3f) * 0.25f;
            }
            return 0f;
        }
    }
}
