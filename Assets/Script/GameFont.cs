using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    public static class GameFont
    {
        public const string ResourcePath = "Fonts/GFCRedSpirit-Bold SDF";
        // In-game numbers only (labels carrying NumericLabel), slanted (italic).
        public const string NumberResourcePath = "Fonts/HiKR-ExtraBold SDF";
        private static TMP_FontAsset fontAsset;
        private static TMP_FontAsset numberAsset;

        public static TMP_FontAsset Asset => fontAsset != null ? fontAsset :
            fontAsset = Resources.Load<TMP_FontAsset>(ResourcePath) ??
                throw new InvalidOperationException("GFCRedSpirit-Bold SDF font asset is missing.");

        // Falls back to the game font if the number font asset was not generated.
        public static TMP_FontAsset NumberAsset => numberAsset != null ? numberAsset :
            numberAsset = Resources.Load<TMP_FontAsset>(NumberResourcePath) ?? Asset;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => fontAsset = numberAsset = null;

        public static void Apply(TMP_Text text)
        {
            if (text == null)
                return;
            bool number = text.TryGetComponent(out NumericLabel _);
            TMP_FontAsset asset = number ? NumberAsset : Asset;
            text.font = asset;
            text.fontSharedMaterial = asset.material;
            // Both TTFs are already heavy; numbers lean forward (italic).
            text.fontStyle = number ? FontStyles.Italic : FontStyles.Normal;
        }

        public static void Apply(Text text)
        {
            if (text == null)
                return;
            bool number = text.TryGetComponent(out NumericLabel _);
            text.font = (number ? NumberAsset : Asset).sourceFontFile;
            text.fontStyle = number ? FontStyle.Italic : FontStyle.Normal;
        }
    }
}
