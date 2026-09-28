using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    public static class GameFont
    {
        public const string ResourcePath = "Fonts/GFCRedSpirit-Bold SDF";
        private static TMP_FontAsset fontAsset;

        public static TMP_FontAsset Asset => fontAsset != null ? fontAsset :
            fontAsset = Resources.Load<TMP_FontAsset>(ResourcePath) ??
                throw new InvalidOperationException("GFCRedSpirit-Bold SDF font asset is missing.");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => fontAsset = null;

        public static void Apply(TMP_Text text)
        {
            if (text == null)
                return;
            text.font = Asset;
            text.fontSharedMaterial = Asset.material;
            text.fontStyle = FontStyles.Normal; // The source TTF is already Bold.
        }

        public static void Apply(Text text)
        {
            if (text == null)
                return;
            text.font = Asset.sourceFontFile;
            text.fontStyle = FontStyle.Normal;
        }
    }
}
