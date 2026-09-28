using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Deadline4Sec.Editor
{
    public static class GameFontSetup
    {
        private const string SourcePath = "Assets/Font/GFCRedSpirit-Bold.ttf";
        private const string AssetPath = "Assets/Resources/Fonts/GFCRedSpirit-Bold SDF.asset";

        [MenuItem("Deadline 4 Sec/Prepare Game Font")]
        public static void Prepare()
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (source == null)
                throw new InvalidOperationException("Missing font: " + SourcePath);
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Fonts"))
                AssetDatabase.CreateFolder("Assets/Resources", "Fonts");

            TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
            if (asset == null)
            {
                asset = TMP_FontAsset.CreateFontAsset(source, 90, 9,
                    GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                if (asset == null)
                    throw new InvalidOperationException("Failed to create GFCRedSpirit font atlas.");
                asset.name = "GFCRedSpirit-Bold SDF";
                AssetDatabase.CreateAsset(asset, AssetPath);
                AssetDatabase.AddObjectToAsset(asset.material, asset);
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            }

            if (asset.sourceFontFile != source)
                throw new InvalidOperationException("Game font atlas must use GFCRedSpirit-Bold.ttf.");
            asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            asset.isMultiAtlasTexturesEnabled = true;
            asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            Array.Clear(asset.fontWeightTable, 0, asset.fontWeightTable.Length);
            SerializedObject serializedFont = new SerializedObject(asset);
            serializedFont.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            serializedFont.ApplyModifiedPropertiesWithoutUndo();
            const string labels = "DEADLINE: 4 SEC RISK TO LIVE TAP TO START SKIP YES NO START SETTINGS SOUND ON OFF VIBRATION TUTORIAL BACK MAIN MENU RETRY Ready? Go! SCORE DISTANCE BEST COMBO TIME NEW NEAR MISS +0123456789.,:xm() 준비시작설정점수거리최고기록재시도게임오버튜토리얼을 스킵하시겠습니까설정창에서 언제든지 다시 할 수 있습니다예아니오";
            // TMP reports an entire already-populated string as missing on a
            // repeated TryAddCharacters call. Check the atlas before adding.
            if (!asset.HasCharacters(labels))
            {
                if (!asset.TryAddCharacters(labels, out string missing))
                    throw new InvalidOperationException("GFCRedSpirit is missing UI glyphs: " + missing);
            }
            foreach (Texture2D atlas in asset.atlasTextures)
            {
                if (!AssetDatabase.Contains(atlas))
                    AssetDatabase.AddObjectToAsset(atlas, asset);
                EditorUtility.SetDirty(atlas);
            }
            TMP_Settings.defaultFontAsset = asset;
            TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset>();
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(asset.material);
            EditorUtility.SetDirty(TMP_Settings.instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset.material, out string guid, out long materialId);
            Debug.Log("Game font prepared: " + guid + " material=" + materialId);
        }
    }
}
