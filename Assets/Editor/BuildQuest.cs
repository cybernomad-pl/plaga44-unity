using UnityEditor;
using UnityEngine;

public class BuildQuest
{
    [MenuItem("CYBERNOMAD/Build Quest APK")]
    public static void Build()
    {
        var scenes = new[] { "Assets/PLAGA44/TESTBED_V6.unity" };
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/plaga44-testbed.apk",
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        System.IO.Directory.CreateDirectory("Builds");

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            Debug.Log($"[PLAGA44] BUILD SUCCESS: {report.summary.outputPath} ({report.summary.totalSize / 1048576}MB)");
        else
            Debug.LogError($"[PLAGA44] BUILD FAILED: {report.summary.totalErrors} errors");
    }
}
