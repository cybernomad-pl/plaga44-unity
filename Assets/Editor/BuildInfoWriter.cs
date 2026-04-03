using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;
using System.Diagnostics;

/// <summary>
/// Pre-build step: writes git branch, timestamp, and short commit hash
/// to Assets/Resources/BuildInfo.txt so VersionHUD can read it at runtime.
///
/// Also provides a manual menu item under CYBERNOMAD > Build Info > Write BuildInfo.txt
/// for testing in the editor without doing a full build.
/// </summary>
public class BuildInfoWriter : IPreprocessBuildWithReport
{
    public int callbackOrder => -100; // run early

    private static readonly string OutputPath = "Assets/Resources/BuildInfo.txt";

    public void OnPreprocessBuild(BuildReport report)
    {
        WriteBuildInfo();
    }

    [MenuItem("CYBERNOMAD/Build Info/Write BuildInfo.txt")]
    public static void WriteBuildInfo()
    {
        // Ensure Resources folder exists
        string dir = Path.GetDirectoryName(OutputPath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string branch    = RunGit("rev-parse --abbrev-ref HEAD");
        string commit    = RunGit("rev-parse --short HEAD");
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        if (string.IsNullOrEmpty(branch)) branch = "unknown";
        if (string.IsNullOrEmpty(commit)) commit = "no-hash";

        // 3 lines: branch, timestamp, commit
        string content = $"{branch}\n{timestamp}\n{commit}\n";
        File.WriteAllText(OutputPath, content);
        AssetDatabase.Refresh();

        UnityEngine.Debug.Log($"[BuildInfoWriter] Wrote {OutputPath}: {branch} | {timestamp} | {commit}");
    }

    static string RunGit(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = Directory.GetParent(Application.dataPath).FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var proc = Process.Start(psi);
            string output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);
            return output;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning($"[BuildInfoWriter] git failed: {e.Message}");
            return "";
        }
    }
}
