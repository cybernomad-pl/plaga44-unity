using UnityEditor;
using UnityEngine;
using System;
using System.Linq;

public class BuildBleedingEdgeAPK
{
    static void PerformBuild()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string outputPath = "/mnt/c/Users/boris/Desktop/PLAGA44/builds/bleeding-edge-" + timestamp + ".apk";

        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            scenes = new string[] { "Assets/Scenes/PLAGA44_Demo.unity" };
        }

        Debug.Log("BUILD SCENES: " + string.Join(", ", scenes));
        Debug.Log("OUTPUT PATH: " + outputPath);

        BuildPlayerOptions opts = new BuildPlayerOptions();
        opts.scenes = scenes;
        opts.locationPathName = outputPath;
        opts.target = BuildTarget.Android;
        opts.options = BuildOptions.Development | BuildOptions.AllowDebugging;

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
