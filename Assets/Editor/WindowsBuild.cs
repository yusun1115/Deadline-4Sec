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
        public static void Build() => BuildTo("Windows");

        // folder: under Builds/ (the exhibition build goes to its own).
        public static void BuildTo(string folder)
        {
            TutorialCourseAssets tutorial = Resources.Load<TutorialCourseAssets>("Tutorial/TutorialCourse");
            if (tutorial == null || !tutorial.IsValid)
                throw new BuildFailedException("Prepare Tutorial Course before building.");
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (Path.GetFileName(root) == "ValidationProject")
                root = Path.GetDirectoryName(root);
            string output = Path.Combine(root, "Builds", folder, "Deadline4Sec.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            // URP keeps the list of pipeline assets a build uses (CoreBuildData)
            // until a build ends. A stale copy from a mobile build made the
            // Windows player strip the PC renderer's post-processing / SSAO
            // resources and draw only black (2026-11-03): start from fresh data
            // for the Windows target.
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            UnityEditor.Rendering.CoreBuildData.instance.Dispose();

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
