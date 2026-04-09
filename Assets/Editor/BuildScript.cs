using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;

/// <summary>
/// PLAGA '44 -- Consolidated build script for Meta Quest APK.
///
/// Menu items:
///   CYBERNOMAD > Build > Quest APK         -- standard build
///   CYBERNOMAD > Build > Quest APK (Dev)   -- development build with profiler
///   CYBERNOMAD > Build > Quest APK (Clean) -- delete Library, then build
///
/// Batch mode entry points:
///   -executeMethod BuildScript.Build       -- optimized production build
///   -executeMethod BuildScript.BuildDev    -- development build with profiler
///
/// Output: Builds/plaga44.apk
/// Backup: copies APK with timestamp to Builds/ subfolder
///
/// Integrates with:
///   - BuildInfoWriter (pre-build: writes git info to BuildInfo.txt)
///   - TextureOptimizer (TODO: pre-build texture optimization)
///   - build-quest.sh (bash wrapper for batch mode + ADB deploy)
/// </summary>
public static class BuildScript
{
    private const string LOG = "[BuildScript]";
    private static readonly string BuildDir = "Builds";
    private static readonly string ApkName = "plaga44.apk";

    // ---- Menu items ----

    [MenuItem("CYBERNOMAD/Build/Quest APK", false, 1)]
    public static void BuildQuestMenu()
    {
        BuildInternal(BuildOptions.None, "Release");
    }

    [MenuItem("CYBERNOMAD/Build/Quest APK (Dev)", false, 2)]
    public static void BuildQuestDev()
    {
        BuildInternal(BuildOptions.Development | BuildOptions.ConnectWithProfiler, "Development");
    }

    [MenuItem("CYBERNOMAD/Build/Quest APK (Clean)", false, 3)]
    public static void BuildQuestClean()
    {
        string libraryPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library");
        if (Directory.Exists(libraryPath))
        {
            Debug.Log($"{LOG} Deleting Library cache...");
            Directory.Delete(libraryPath, true);
            Debug.Log($"{LOG} Library deleted. Build will reimport all assets.");
        }
        BuildInternal(BuildOptions.None, "Clean Release");
    }

    // ---- Batch mode entry points ----

    /// <summary>
    /// Production build entry point for batch mode.
    /// Usage: Unity.exe -batchmode -executeMethod BuildScript.Build
    /// </summary>
    public static void Build()
    {
        // TextureOptimizer.OptimizeAll(); // TODO: dodać gdy TextureOptimizer będzie na main
        BuildInternal(BuildOptions.None, "Batch Release");

        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }

    /// <summary>
    /// Development build entry point for batch mode.
    /// Usage: Unity.exe -batchmode -executeMethod BuildScript.BuildDev
    /// </summary>
    public static void BuildDev()
    {
        BuildInternal(BuildOptions.Development | BuildOptions.ConnectWithProfiler, "Batch Dev");

        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }

    // ---- Core build ----

    private static void BuildInternal(BuildOptions buildOptions, string buildType)
    {
        Debug.Log($"{LOG} === PLAGA '44 Quest APK ({buildType}) ===");

        // Resolve scenes
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            // Fallback: try known scene paths
            string[] fallbacks = {
                "Assets/TESTBED_V2.unity",
                "Assets/Scenes/PLAGA44_Demo.unity",
                "Assets/Scenes/testbed.unity"
            };

            foreach (var fb in fallbacks)
            {
                if (File.Exists(fb))
                {
                    scenes = new[] { fb };
                    Debug.LogWarning($"{LOG} No scenes in Build Settings. Using fallback: {fb}");
                    break;
                }
            }

            if (scenes.Length == 0)
            {
                Debug.LogError($"{LOG} No scenes found! Add scenes to Build Settings.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
        }

        Debug.Log($"{LOG} Scenes ({scenes.Length}): {string.Join(", ", scenes)}");

        // Ensure output directory
        if (!Directory.Exists(BuildDir))
            Directory.CreateDirectory(BuildDir);

        string outputPath = Path.Combine(BuildDir, ApkName);

        // Ensure Android target
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.Log($"{LOG} Switching to Android build target...");
            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android, BuildTarget.Android);
        }

        // Build
        var startTime = DateTime.Now;

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = buildOptions
        };

        var report = BuildPipeline.BuildPlayer(options);
        var duration = DateTime.Now - startTime;

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            long sizeMB = (long)(report.summary.totalSize / (1024 * 1024));
            Debug.Log($"{LOG} BUILD SUCCESS");
            Debug.Log($"{LOG}   APK: {outputPath} ({sizeMB} MB)");
            Debug.Log($"{LOG}   Time: {duration.TotalSeconds:F0}s");
            Debug.Log($"{LOG}   Type: {buildType}");

            // Backup with timestamp
            BackupAPK(outputPath);
        }
        else
        {
            Debug.LogError($"{LOG} BUILD FAILED: {report.summary.totalErrors} error(s)");

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

    private static void BackupAPK(string apkPath)
    {
        if (!File.Exists(apkPath)) return;

        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string branch = "unknown";

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", "rev-parse --abbrev-ref HEAD")
            {
                WorkingDirectory = Directory.GetParent(Application.dataPath).FullName,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var proc = System.Diagnostics.Process.Start(psi);
            branch = proc.StandardOutput.ReadToEnd().Trim().Replace("/", "-");
            proc.WaitForExit(3000);
        }
        catch { }

        string backupName = $"plaga44-{branch}-{timestamp}.apk";
        string backupPath = Path.Combine(BuildDir, backupName);

        File.Copy(apkPath, backupPath, true);
        Debug.Log($"{LOG}   Backup: {backupPath}");
    }
}
