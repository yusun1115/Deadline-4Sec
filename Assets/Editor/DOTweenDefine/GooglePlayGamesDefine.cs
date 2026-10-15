using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Deadline4Sec.Editor
{
    // Adds DEADLINE_GPGS (Android) while the Google Play Games plugin is
    // imported and removes it when it is not, so GooglePlayService switches
    // from the mock to the real API without hand-editing Player Settings.
    // Same editor assembly as DOTweenDefine, so it runs even if game scripts
    // fail to compile.
    [InitializeOnLoad]
    internal static class GooglePlayGamesDefine
    {
        private const string Symbol = "DEADLINE_GPGS";

        static GooglePlayGamesDefine() => EditorApplication.delayCall += Sync;

        private static void Sync()
        {
            // By assembly: the plugin compiles its classes only for an Android
            // target, so the types are missing while the editor targets Windows.
            bool present = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetName().Name == "Google.Play.Games");
            var symbols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (symbols.Contains(Symbol) == present)
                return;
            if (present)
                symbols.Add(Symbol);
            else
                symbols.Remove(Symbol);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, string.Join(";", symbols));
        }
    }
}
