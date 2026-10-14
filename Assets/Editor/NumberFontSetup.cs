using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Deadline4Sec.Editor
{
    // In-game numbers use HiKR-ExtraBold, italic (2026-10-11; replaced
    // CallifontSharpie-Bold from 2026-10-04): builds its TMP SDF asset next to
    // the game font and marks the HUD number labels with NumericLabel so
    // GameFont gives them that font and slant at runtime too.
    public static class NumberFontSetup
    {
        private const string SourcePath = "Assets/Font/HiKR-ExtraBold.ttf";
        private const string AssetPath = "Assets/Resources/Fonts/HiKR-ExtraBold SDF.asset";

        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/EndlessRun.unity",
            "Assets/Scenes/GrayboxCourse.unity",
            "Assets/Scenes/TestScene.unity",
        };

        // GameplayUI labels that show numbers. Sizes are owned by the HUD layout
        // (HudLayoutSetup pills, the timer's best fit), so 0 = keep.
        private static readonly string[] NumberLabels =
        {
            "SurvivalTimer", "ScoreText", "DistanceText", "RunCoinText", "GiftBoxCountText",
        };
        private static readonly int[] NumberLabelSizes = { 0, 0, 0, 0, 0 };

        [MenuItem("Deadline 4 Sec/UI/Apply Number Font To HUD")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the open scene before changing HUD fonts.");
            EnsureFontAsset();
            string original = EditorSceneManager.GetActiveScene().path;
            foreach (string path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path);
                Transform hud = GameObject.Find("Canvas")?.transform.Find("GameplayUI");
                if (hud == null)
                    continue;
                for (int i = 0; i < NumberLabels.Length; i++)
                {
                    Transform label = hud.Find(NumberLabels[i]);
                    if (label == null)
                        continue;
                    if (!label.TryGetComponent(out NumericLabel _))
                        label.gameObject.AddComponent<NumericLabel>();
                    TMP_Text tmp = label.GetComponent<TMP_Text>();
                    Text legacy = label.GetComponent<Text>();
                    GameFont.Apply(tmp);
                    GameFont.Apply(legacy);
                    int size = NumberLabelSizes[i];
                    if (size <= 0)
                        continue;
                    if (tmp != null)
                        tmp.fontSize = tmp.fontSizeMax = size;
                    if (legacy != null)
                    {
                        legacy.fontSize = size;
                        legacy.verticalOverflow = VerticalWrapMode.Overflow;
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (!string.IsNullOrEmpty(original))
                EditorSceneManager.OpenScene(original);
        }

        // Same atlas settings as the GFCRedSpirit asset (dynamic, 90pt, pad 9, 1024²).
        public static TMP_FontAsset EnsureFontAsset()
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
            if (existing != null)
                return existing;
            Font source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (source == null)
                throw new InvalidOperationException(SourcePath + " is missing.");
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic);
            font.name = System.IO.Path.GetFileNameWithoutExtension(AssetPath);
            AssetDatabase.CreateAsset(font, AssetPath);
            font.atlasTexture.name = font.name + " Atlas";
            AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
            Material material = new Material(Shader.Find("TextMeshPro/Mobile/Distance Field"))
            {
                name = font.name + " Material",
            };
            material.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
            material.SetFloat(ShaderUtilities.ID_TextureWidth, font.atlasWidth);
            material.SetFloat(ShaderUtilities.ID_TextureHeight, font.atlasHeight);
            material.SetFloat(ShaderUtilities.ID_GradientScale, font.atlasPadding + 1);
            material.SetFloat(ShaderUtilities.ID_WeightNormal, font.normalStyle);
            material.SetFloat(ShaderUtilities.ID_WeightBold, font.boldStyle);
            font.material = material;
            AssetDatabase.AddObjectToAsset(material, font);
            // Pre-bake printable ASCII so the HUD never waits on the dynamic atlas.
            var ascii = new System.Text.StringBuilder();
            for (char c = ' '; c <= '~'; c++)
                ascii.Append(c);
            font.TryAddCharacters(ascii.ToString(), out string missing);
            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning(source.name + " lacks: " + missing);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetPath);
            return font;
        }
    }
}
