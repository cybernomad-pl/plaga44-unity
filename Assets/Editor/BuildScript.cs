// =============================================================================
// BuildScript.cs
// CYBERNOMAD -- APK build Quest. Menu: CYBERNOMAD > Build > Build APK (Quest).
// Batch mode: unity -executeMethod Plaga44.Editor.BuildScript.Build
// =============================================================================
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class BuildScript
    {
        private const string LOG = "[BuildScript]";
        private const string BuildDir = "Builds";
        private const string ApkName = "plaga44.apk";
        private const string TargetScenePath = "Assets/PLAGA44/TESTBED.unity";

        [MenuItem("CYBERNOMAD/Build/Build APK (Quest)")]
        public static void BuildQuest()
        {
            string[] scenes = ResolveBuildScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError($"{LOG} No valid scenes found!");
                return;
            }
            Debug.Log($"{LOG} Building {scenes.Length} scene(s): {string.Join(", ", scenes)}");

            EnsureBuildDir();
            SwitchToAndroid();
            BuildApk(scenes, Path.Combine(BuildDir, ApkName));
        }

        /// <summary>Batch mode entry point: -executeMethod Plaga44.Editor.BuildScript.Build</summary>
        public static void Build() => BuildQuest(); // TextureOptimizer is bleeding-edge only -- skip na TESTBED

        private static string[] ResolveBuildScenes()
        {
            if (File.Exists(TargetScenePath))
                return new[] { TargetScenePath };
            return EditorBuildSettings.scenes
                .Where(s => s.enabled && File.Exists(s.path))
                .Select(s => s.path)
                .ToArray();
        }

        private static void EnsureBuildDir()
        {
            if (!Directory.Exists(BuildDir)) Directory.CreateDirectory(BuildDir);
        }

        private static void SwitchToAndroid()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }

        private static void BuildApk(string[] scenes, string path)
        {
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log($"{LOG} BUILD OK -> {path} ({report.summary.totalSize / 1024 / 1024} MB)");
            else
                LogFailedBuild(report);
        }

        private static void LogFailedBuild(BuildReport report)
        {
            Debug.LogError($"{LOG} BUILD FAILED: {report.summary.totalErrors} error(s)");
            foreach (var step in report.steps)
                foreach (var msg in step.messages)
                    if (msg.type == LogType.Error)
                        Debug.LogError($"  {msg.content}");

            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }
}
