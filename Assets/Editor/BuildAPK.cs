using UnityEditor;
using UnityEngine;
using System.Linq;

public class BuildAPK
{
    static void PerformBuild()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();
        
        if (scenes.Length == 0)
        {
            scenes = new string[] { "Assets/Scenes/PLAGA44_Demo.unity" };
        }

        Debug.Log("BUILD SCENES: " + string.Join(", ", scenes));

        BuildPlayerOptions opts = new BuildPlayerOptions();
        opts.scenes = scenes;
        opts.locationPathName = "Builds/plaga44.apk";
        opts.target = BuildTarget.Android;
        opts.options = BuildOptions.None;

        var report = BuildPipeline.BuildPlayer(opts);
        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log("BUILD SUCCESS: " + report.summary.outputPath + " (" + report.summary.totalSize + " bytes)");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError("BUILD FAILED: " + report.summary.totalErrors + " errors");
            EditorApplication.Exit(1);
        }
    }
}
