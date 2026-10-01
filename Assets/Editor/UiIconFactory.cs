using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Draws the menu icons (settings gear, skin scythe, upgrade arrow, back
    // arrow, round button and skin card frames) from signed distance shapes, in
    // the game's white / hot-pink / ink palette. Replace any PNG with painted art
    // later; the menu setup only references the file names.
    public static class UiIconFactory
    {
        public const string Folder = "Assets/Art/2D/Generated/UI/";
        private const int Size = 256;

        private static readonly Color Ink = new Color(0.07f, 0.03f, 0.1f, 1f);
        private static readonly Color White = new Color(0.97f, 0.94f, 0.97f, 1f);
        private static readonly Color Pink = new Color(1f, 0.2f, 0.55f, 1f);
        private static readonly Color Panel = new Color(0.12f, 0.07f, 0.16f, 0.94f);

        [MenuItem("Deadline 4 Sec/Art/Draw Menu Icons")]
        public static void DrawAll()
        {
            Directory.CreateDirectory(Folder);
            Draw("icon_settings", Gear, 0.06f, true);
            Draw("icon_skin", Scythe, 0.05f, true);
            Draw("icon_upgrade", UpArrow, 0.05f, true);
            Draw("icon_back", BackArrow, 0.05f, false);
            DrawRoundButton();
            DrawCard();
            AssetDatabase.Refresh();
            foreach (string name in new[] { "icon_settings", "icon_skin", "icon_upgrade", "icon_back", "ui_round_button", "ui_skin_card" })
                ImportAsSprite(Folder + name + ".png", name == "ui_skin_card" ? new Vector4(48, 48, 48, 48) : Vector4.zero);
        }

        // ---------- shapes: p in [-0.5, 0.5], y up; negative = inside ----------

        private static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        private static float Box(Vector2 p, Vector2 c, Vector2 half, float round)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + Vector2.one * round;
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - round;
        }

        private static float Segment(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }

        private static Vector2 Rotate(Vector2 p, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(c * p.x - s * p.y, s * p.x + c * p.y);
        }

        // Returns (shape distance, accent distance): accent areas are painted pink.
        private static Vector2 Gear(Vector2 p)
        {
            float d = Circle(p, Vector2.zero, 0.27f);
            for (int i = 0; i < 8; i++)
                d = Mathf.Min(d, Box(Rotate(p, i * 45f), new Vector2(0f, 0.3f), new Vector2(0.065f, 0.085f), 0.02f));
            d = Mathf.Max(d, -Circle(p, Vector2.zero, 0.12f));
            float accent = Mathf.Abs(Circle(p, Vector2.zero, 0.17f)) - 0.025f;
            return new Vector2(d, accent);
        }

        private static Vector2 Scythe(Vector2 p)
        {
            p = Rotate(p, -18f);
            float handle = Segment(p, new Vector2(0.18f, -0.4f), new Vector2(-0.08f, 0.33f), 0.035f);
            float outer = Circle(p, new Vector2(0.06f, 0.08f), 0.32f);
            float inner = Circle(p, new Vector2(0.12f, 0.0f), 0.3f);
            float blade = Mathf.Max(Mathf.Max(outer, -inner), -(p.y - 0.12f));
            blade = Mathf.Max(blade, p.x - 0.0f);
            float d = Mathf.Min(handle, blade);
            float accent = Mathf.Max(blade, -(Circle(p, new Vector2(0.12f, 0.0f), 0.33f)));
            return new Vector2(d, accent);
        }

        private static Vector2 UpArrow(Vector2 p)
        {
            float shaft = Box(p, new Vector2(0f, -0.13f), new Vector2(0.08f, 0.2f), 0.02f);
            Vector2 q = new Vector2(Mathf.Abs(p.x), p.y);
            // Triangle head: inside when below both slanted edges and above its base.
            float edge = Vector2.Dot(q - new Vector2(0f, 0.36f), new Vector2(0.83f, 0.55f).normalized);
            float head = Mathf.Max(edge, -(p.y - 0.02f));
            float d = Mathf.Min(shaft, head);
            float accent = Mathf.Max(head, -(edge + 0.07f));
            return new Vector2(d, accent);
        }

        private static Vector2 BackArrow(Vector2 p)
        {
            float d = Mathf.Min(Segment(p, new Vector2(-0.24f, 0f), new Vector2(0.02f, 0.24f), 0.065f),
                Segment(p, new Vector2(-0.24f, 0f), new Vector2(0.02f, -0.24f), 0.065f));
            d = Mathf.Min(d, Segment(p, new Vector2(-0.2f, 0f), new Vector2(0.28f, 0f), 0.06f));
            float accent = Segment(p, new Vector2(-0.24f, 0f), new Vector2(-0.12f, 0f), 0.04f);
            return new Vector2(d, accent);
        }

        private static void Draw(string name, Func<Vector2, Vector2> shape, float outline, bool gradient)
        {
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            float px = 1f / Size;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / Size - 0.5f, (y + 0.5f) / Size - 0.5f);
                Vector2 s = shape(p);
                float fill = Mathf.Clamp01(0.5f - s.x / px);
                float edge = Mathf.Clamp01(0.5f - (s.x - outline) / px);
                float accent = Mathf.Clamp01(0.5f - s.y / px) * fill;
                Color body = gradient ? Color.Lerp(White, new Color(1f, 0.8f, 0.9f), Mathf.Clamp01(0.5f - p.y)) : White;
                Color c = Color.Lerp(Ink, body, fill);
                c = Color.Lerp(c, Pink, accent);
                c.a = edge;
                tex.SetPixel(x, y, c);
            }
            Save(tex, name);
        }

        private static void DrawRoundButton()
        {
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            float px = 1f / Size;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / Size - 0.5f, (y + 0.5f) / Size - 0.5f);
                float r = p.magnitude;
                float disc = Mathf.Clamp01(0.5f - (r - 0.47f) / px);
                float rimOuter = Mathf.Clamp01(0.5f - (r - 0.44f) / px);
                float rimInner = Mathf.Clamp01(0.5f - (r - 0.4f) / px);
                float pinkRing = Mathf.Clamp01(0.5f - (Mathf.Abs(r - 0.37f) - 0.012f) / px);
                Color c = Ink;
                c = Color.Lerp(c, White, rimOuter - rimInner);
                Color center = Color.Lerp(new Color(0.75f, 0.06f, 0.24f), new Color(0.42f, 0.02f, 0.14f), Mathf.Clamp01(0.5f - p.y));
                c = Color.Lerp(c, center, rimInner);
                c = Color.Lerp(c, Pink, pinkRing * rimInner);
                c.a = disc;
                tex.SetPixel(x, y, c);
            }
            Save(tex, "ui_round_button");
        }

        private static void DrawCard()
        {
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            float px = 1f / Size;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / Size - 0.5f, (y + 0.5f) / Size - 0.5f);
                float outer = Box(p, Vector2.zero, new Vector2(0.48f, 0.48f), 0.07f);
                float rim = Box(p, Vector2.zero, new Vector2(0.43f, 0.43f), 0.05f);
                float line = Mathf.Abs(Box(p, Vector2.zero, new Vector2(0.39f, 0.39f), 0.04f)) - 0.006f;
                float a = Mathf.Clamp01(0.5f - outer / px);
                Color c = Color.Lerp(White, Panel, Mathf.Clamp01(0.5f - rim / px));
                c = Color.Lerp(c, Pink, Mathf.Clamp01(0.5f - line / px));
                c = Color.Lerp(Ink, c, Mathf.Clamp01(0.5f - (outer + 0.012f) / px));
                c.a = a;
                tex.SetPixel(x, y, c);
            }
            Save(tex, "ui_skin_card");
        }

        private static void Save(Texture2D tex, string name)
        {
            tex.Apply();
            File.WriteAllBytes(Folder + name + ".png", tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void ImportAsSprite(string path, Vector4 border)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }
    }
}
