using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // The image model returns icons on an opaque white background. Every icon is
    // drawn with a closed dark outline, so flood-filling near-white pixels from
    // the image border removes the background without touching white parts
    // inside the outline. Edge pixels get partial alpha from their whiteness.
    public static class IconBackgroundRemover
    {
        private const string Folder = "Assets/Art/2D/Generated/UI/AI/";
        private const int OutputSize = 256;

        [MenuItem("Deadline 4 Sec/Art/Cut White Background From AI Icons")]
        public static void ProcessAll()
        {
            foreach (string path in Directory.GetFiles(Folder, "ai_*.png"))
                Process(path.Replace('\\', '/'));
            AssetDatabase.Refresh();
        }

        public static void Process(string path)
        {
            Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes(path));
            int w = source.width, h = source.height;
            Color[] px = source.GetPixels();
            if (px[0].a < 0.5f)
                return; // already transparent

            bool[] background = new bool[px.Length];
            Queue<int> queue = new Queue<int>();
            void Seed(int i)
            {
                if (!background[i] && Whiteness(px[i]) > 0.82f)
                {
                    background[i] = true;
                    queue.Enqueue(i);
                }
            }
            for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }
            Drain(queue, w, h, Seed);

            // Some outputs come framed by dark rounded-square corners; clear dark
            // pixels connected to the four image corners as well.
            void SeedDark(int i)
            {
                if (!background[i] && Whiteness(px[i]) < 0.2f && Mathf.Max(px[i].r, Mathf.Max(px[i].g, px[i].b)) < 0.25f)
                {
                    background[i] = true;
                    queue.Enqueue(i);
                }
            }
            SeedDark(0); SeedDark(w - 1); SeedDark((h - 1) * w); SeedDark(h * w - 1);
            Drain(queue, w, h, SeedDark);

            for (int i = 0; i < px.Length; i++)
            {
                if (background[i])
                {
                    px[i] = new Color(0.1f, 0.03f, 0.12f, 0f); // ink-colored so downscaling leaves no white halo
                    continue;
                }
                // Anti-aliased rim: foreground pixels touching the background fade
                // by how close they still are to white, and lose the white tint.
                int x = i % w, y = i / w;
                bool rim = (x > 0 && background[i - 1]) || (x < w - 1 && background[i + 1]) ||
                           (y > 0 && background[i - w]) || (y < h - 1 && background[i + w]);
                if (!rim)
                    continue;
                float a = Mathf.Clamp01((1f - Whiteness(px[i])) / 0.5f);
                Color c = px[i];
                c.a = Mathf.Max(a, 0.15f);
                px[i] = c;
            }

            Texture2D result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            result.SetPixels(px);
            result.Apply();
            Texture2D scaled = Downscale(result, OutputSize);
            File.WriteAllBytes(path, scaled.EncodeToPNG());
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(result);
            Object.DestroyImmediate(scaled);
            AssetDatabase.ImportAsset(path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        private static void Drain(Queue<int> queue, int w, int h, System.Action<int> seed)
        {
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % w, y = i / w;
                if (x > 0) seed(i - 1);
                if (x < w - 1) seed(i + 1);
                if (y > 0) seed(i - w);
                if (y < h - 1) seed(i + w);
            }
        }

        // Minimum channel ≈ how white the pixel is (pure colors stay low).
        private static float Whiteness(Color c) => Mathf.Min(c.r, Mathf.Min(c.g, c.b));

        private static Texture2D Downscale(Texture2D src, int size)
        {
            RenderTexture rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D dst = new Texture2D(size, size, TextureFormat.RGBA32, false);
            dst.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            dst.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return dst;
        }
    }
}
