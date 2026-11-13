using System.IO;
using UnityEditor;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Exhibition build switches (ExhibitionRanking.md). The mode is a flag on
    // Resources/Exhibition/ExhibitionSettings: off for store builds.
    public static class ExhibitionMenu
    {
        private const string Folder = "Assets/Resources/Exhibition";
        private const string AssetPath = Folder + "/ExhibitionSettings.asset";
        private const string OnItem = "Deadline 4 Sec/Exhibition/Exhibition Mode";

        public static ExhibitionSettings LoadOrCreate()
        {
            ExhibitionSettings settings = AssetDatabase.LoadAssetAtPath<ExhibitionSettings>(AssetPath);
            if (settings != null)
                return settings;
            Directory.CreateDirectory(Folder);
            settings = ScriptableObject.CreateInstance<ExhibitionSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        [MenuItem(OnItem, false, 300)]
        private static void Toggle() => SetMode(!LoadOrCreate().exhibitionMode);

        [MenuItem(OnItem, true)]
        private static bool ToggleCheck()
        {
            ExhibitionSettings settings = AssetDatabase.LoadAssetAtPath<ExhibitionSettings>(AssetPath);
            Menu.SetChecked(OnItem, settings != null && settings.exhibitionMode);
            return true;
        }

        public static void SetMode(bool on)
        {
            ExhibitionSettings settings = LoadOrCreate();
            settings.exhibitionMode = on;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("Exhibition mode " + (on ? "ON" : "OFF"));
        }

        // A Windows player with the exhibition flow, in Builds/WindowsExhibition.
        // The flag goes back to what it was afterwards.
        [MenuItem("Deadline 4 Sec/Exhibition/Build Windows (Exhibition)", false, 311)]
        private static void BuildWindows()
        {
            bool before = LoadOrCreate().exhibitionMode;
            SetMode(true);
            try
            {
                WindowsBuild.BuildTo("WindowsExhibition");
            }
            finally
            {
                SetMode(before);
            }
        }

        [MenuItem("Deadline 4 Sec/Exhibition/Open Ranking Folder", false, 322)]
        private static void OpenFolder() => EditorUtility.RevealInFinder(ExhibitionLeaderboardService.FilePath);

        // Admin: deletes every exhibition score on this computer (the player
        // builds on this PC share the file; a phone has its own: use the
        // in-game admin panel there).
        [MenuItem("Deadline 4 Sec/Exhibition/Clear Exhibition Ranking...", false, 323)]
        private static void Clear()
        {
            int count = ExhibitionLeaderboardService.Entries.Count;
            if (EditorUtility.DisplayDialog("Exhibition Ranking",
                    $"정말 전시 랭킹을 삭제하시겠습니까?\n기록 {count}개가 지워지고 되돌릴 수 없습니다.\n\n{ExhibitionLeaderboardService.FilePath}",
                    "삭제", "취소"))
                ExhibitionLeaderboardService.ClearExhibitionLeaderboard();
        }
    }
}
