using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;

public static class BuildScript
{
    private static readonly string BuildDir = "Builds";
    private static readonly string ApkName = "plaga44.apk";

    // TESTBED_V6 -- hardcoded target scene (ignores stale EditorBuildSettings).
    private const string TargetScenePath = "Assets/PLAGA44/TESTBED.unity";

    [MenuItem("CYBERNOMAD/Build/Build APK (Quest)")]
    public static void BuildQuest()
    {
        // Prefer the known TESTBED scene; fall back to EditorBuildSettings if it's missing.
        string[] scenes;
        if (File.Exists(TargetScenePath))
        {
            scenes = new[] { TargetScenePath };
        }
        else
        {
            scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && File.Exists(s.path))
                .Select(s => s.path)
                .ToArray();
        }

        if (scenes.Length == 0)
        {
            Debug.LogError("[BuildScript] No valid scenes found!");
            return;
        }

        Debug.Log($"[BuildScript] Building {scenes.Length} scene(s): {string.Join(", ", scenes)}");

        if (!Directory.Exists(BuildDir))
            Directory.CreateDirectory(BuildDir);

        string path = Path.Combine(BuildDir, ApkName);

        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Android, BuildTarget.Android);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = path,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"[BuildScript] BUILD OK -> {path} ({report.summary.totalSize / 1024 / 1024} MB)");
        }
        else
        {
            Debug.LogError($"[BuildScript] BUILD FAILED: {report.summary.totalErrors} error(s)");
            foreach (var step in report.steps)
            {
                foreach (var msg in step.messages)
                {
                    if (msg.type == LogType.Error)
                        Debug.LogError($"  {msg.content}");
                }
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }

    // Entry point for batch mode: -executeMethod BuildScript.Build
    public static void Build()
    {
        // TextureOptimizer is bleeding-edge only -- skip on TESTBED_V6.
        BuildQuest();
    }
}
