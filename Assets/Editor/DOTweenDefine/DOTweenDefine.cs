using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Deadline4Sec.Editor
{
    // Adds DEADLINE_DOTWEEN while DOTween is imported and removes it when it is
    // not, so TitleOutro switches to DOTween without hand-editing Player Settings.
    // Lives in its own editor assembly so it still runs if the game scripts fail
    // to compile (e.g. DOTween deleted while the symbol is still set).
    [InitializeOnLoad]
    internal static class DOTweenDefine
    {
        private const string Symbol = "DEADLINE_DOTWEEN";

        private static readonly NamedBuildTarget[] Targets =
        {
            NamedBuildTarget.Standalone, NamedBuildTarget.Android, NamedBuildTarget.iOS,
        };

        static DOTweenDefine() => EditorApplication.delayCall += Sync;

        private static void Sync()
        {
            bool present = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "DOTween");
            foreach (NamedBuildTarget target in Targets)
            {
                var symbols = PlayerSettings.GetScriptingDefineSymbols(target)
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                if (symbols.Contains(Symbol) == present)
                    continue;
                if (present)
                    symbols.Add(Symbol);
                else
                    symbols.Remove(Symbol);
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
            }
        }
    }
}
