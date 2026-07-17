using System.Linq;
using UnityEditor;
using UnityEngine;

public class BuildQuest
{
    [MenuItem("CYBERNOMAD/Build Quest APK")]
    public static void Build()
    {
        // Use scenes from EditorBuildSettings (respects SplashScene index 0 + PLAGA44_Demo index 1)
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("[PLAGA44] BUILD FAILED: no enabled scenes in EditorBuildSettings");
            return;
        }

        Debug.Log($"[PLAGA44] Building {scenes.Length} scenes: {string.Join(", ", scenes)}");

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/plaga44-testbed.apk",
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        // Ensure output dir exists
        System.IO.Directory.CreateDirectory("Builds");

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            Debug.Log($"[PLAGA44] BUILD SUCCESS: {report.summary.outputPath} ({report.summary.totalSize / 1048576}MB)");
        else
            Debug.LogError($"[PLAGA44] BUILD FAILED: {report.summary.totalErrors} errors");
    }
}
