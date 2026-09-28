using System;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    public sealed class AndroidAcceptanceBuild : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        // JNI vibration calls need this permission even though the game does not
        // call Handheld.Vibrate (which Unity otherwise detects automatically).
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            XmlDocument document = new XmlDocument();
            document.Load(manifestPath);
            const string androidNamespace = "http://schemas.android.com/apk/res/android";
            foreach (XmlNode node in document.DocumentElement.SelectNodes("uses-permission"))
                if (node.Attributes["name", androidNamespace]?.Value == "android.permission.VIBRATE")
                    return;
            XmlElement permission = document.CreateElement("uses-permission");
            XmlAttribute name = document.CreateAttribute("android", "name", androidNamespace);
            name.Value = "android.permission.VIBRATE";
            permission.Attributes.Append(name);
            document.DocumentElement.AppendChild(permission);
            document.Save(manifestPath);
        }

        [Serializable]
        private sealed class Evidence
        {
            public string timestampUtc, unityVersion, result, apk, applicationIdentifier;
            public string[] scenes;
            public long apkSizeBytes, totalBuildOutputSizeBytes;
            public double durationSeconds;
            public int warnings, errors;
        }

        [MenuItem("Deadline 4 Sec/Build Android Acceptance APK")]
        public static void BuildDevelopment()
        {
            TutorialCourseAssets tutorial = Resources.Load<TutorialCourseAssets>("Tutorial/TutorialCourse");
            if (tutorial == null || !tutorial.IsValid)
                throw new BuildFailedException("Prepare Tutorial Course before building the acceptance APK.");
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (Path.GetFileName(root) == "ValidationProject")
                root = Path.GetDirectoryName(root);
            string output = Path.Combine(root, "Builds", "Android", "Deadline4Sec-Development.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path).ToArray();
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                target = BuildTarget.Android,
                locationPathName = output,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            });
            BuildSummary summary = report.summary;
            Evidence evidence = new Evidence
            {
                timestampUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                result = summary.result.ToString(),
                apk = output,
                applicationIdentifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                scenes = scenes,
                apkSizeBytes = File.Exists(output) ? new FileInfo(output).Length : 0,
                totalBuildOutputSizeBytes = (long)summary.totalSize,
                durationSeconds = summary.totalTime.TotalSeconds,
                warnings = summary.totalWarnings,
                errors = summary.totalErrors
            };
            Directory.CreateDirectory(Path.Combine(root, "Verification"));
            File.WriteAllText(Path.Combine(root, "Verification", "android-build.json"),
                JsonUtility.ToJson(evidence, true));
            if (summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Android acceptance build: " + summary.result);
            Debug.Log("Android acceptance APK: " + output);
        }
    }
}
