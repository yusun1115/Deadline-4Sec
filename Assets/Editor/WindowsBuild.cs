using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Windows (x64) player of the enabled build scenes, in
    // Builds/Windows/Deadline4Sec.exe. The game is a portrait phone layout, so
    // the window opens 540x960 (resizable) instead of a landscape full screen.
    public static class WindowsBuild
    {
        [MenuItem("Deadline 4 Sec/Build Windows")]
        public static void Build()
        {
            TutorialCourseAssets tutorial = Resources.Load<TutorialCourseAssets>("Tutorial/TutorialCourse");
            if (tutorial == null || !tutorial.IsValid)
                throw new BuildFailedException("Prepare Tutorial Course before building.");
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (Path.GetFileName(root) == "ValidationProject")
                root = Path.GetDirectoryName(root);
            string output = Path.Combine(root, "Builds", "Windows", "Deadline4Sec.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.resizableWindow = true;

            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path).ToArray();
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                target = BuildTarget.StandaloneWindows64,
                locationPathName = output,
                options = BuildOptions.None,
            });
            BuildSummary summary = report.summary;
            Debug.Log("Windows build " + summary.result + ": " + output + " (" + summary.totalSize / (1024 * 1024) +
                      " MB, " + summary.totalTime.TotalSeconds.ToString("F0") + " s, " + summary.totalErrors + " errors)");
            if (summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Windows build " + summary.result);
        }
    }
}
