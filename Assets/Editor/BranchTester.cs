using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// PLAGA44 > Branch Tester
/// Przelacza branche git z poziomu Unity Editor.
/// Workflow: wybierz branch -> Unity refreshuje -> testujesz -> wracasz do main.
/// </summary>
public class BranchTester : EditorWindow
{
    private string[] branches = new string[0];
    private string currentBranch = "";
    private Vector2 scrollPos;
    private string lastOutput = "";
    private bool fetching = false;

    [MenuItem("PLAGA44/Branch Tester")]
    static void ShowWindow()
    {
        var win = GetWindow<BranchTester>("Branch Tester");
        win.minSize = new Vector2(340, 400);
        win.RefreshBranchList();
    }

    void OnGUI()
    {
        EditorGUILayout.Space(4);

        // Current branch
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Aktualny branch:", EditorStyles.boldLabel, GUILayout.Width(120));
        EditorGUILayout.LabelField(currentBranch, EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // Buttons row
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Fetch + Refresh", GUILayout.Height(28)))
        {
            FetchAndRefresh();
        }
        GUI.backgroundColor = currentBranch == "main" ? Color.gray : Color.white;
        if (GUILayout.Button("Wroc do main", GUILayout.Height(28)))
        {
            SwitchToBranch("main");
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Dostepne branche:", EditorStyles.boldLabel);

        // Branch list
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        foreach (var branch in branches)
        {
            if (string.IsNullOrWhiteSpace(branch)) continue;
            string clean = branch.Trim();
            if (clean.StartsWith("origin/HEAD")) continue;

            // Strip origin/ prefix for display
            string display = clean.StartsWith("origin/") ? clean.Substring(7) : clean;
            bool isCurrent = display == currentBranch;

            EditorGUILayout.BeginHorizontal();

            if (isCurrent)
            {
                EditorGUILayout.LabelField(">>", GUILayout.Width(20));
            }
            else
            {
                EditorGUILayout.LabelField("  ", GUILayout.Width(20));
            }

            EditorGUILayout.LabelField(display);

            GUI.enabled = !isCurrent;
            if (GUILayout.Button("Testuj", GUILayout.Width(60)))
            {
                SwitchToBranch(display);
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        // Output log
        if (!string.IsNullOrEmpty(lastOutput))
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Log:", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(lastOutput, MessageType.Info);
        }
    }

    void FetchAndRefresh()
    {
        lastOutput = RunGit("fetch --all --prune");
        RefreshBranchList();
        Repaint();
    }

    void RefreshBranchList()
    {
        currentBranch = RunGit("rev-parse --abbrev-ref HEAD").Trim();

        string raw = RunGit("branch -r --sort=-committerdate");
        branches = raw.Split('\n')
            .Select(b => b.Trim())
            .Where(b => !string.IsNullOrWhiteSpace(b) && !b.Contains("HEAD"))
            .ToArray();

        Repaint();
    }

    void SwitchToBranch(string branchName)
    {
        // Stash local changes if any
        string status = RunGit("status --porcelain");
        bool hadChanges = !string.IsNullOrWhiteSpace(status);

        if (hadChanges)
        {
            RunGit("stash push -m \"BranchTester auto-stash\"");
        }

        // Checkout
        string result = RunGit($"checkout {branchName}");

        if (branchName != "main" && !branchName.StartsWith("feature/"))
        {
            // Try tracking remote
            RunGit($"checkout -b {branchName} origin/{branchName} 2>&1");
        }

        // Pull latest
        RunGit("pull --ff-only 2>&1");

        lastOutput = $"Przelaczono na: {branchName}\n{result}";

        if (hadChanges)
        {
            lastOutput += "\n[!] Lokalne zmiany zapisane w git stash";
        }

        // Refresh Unity
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        RefreshBranchList();

        UnityEngine.Debug.Log($"[BranchTester] Przelaczono na branch: {branchName}");
    }

    static string RunGit(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = System.IO.Path.GetDirectoryName(Application.dataPath),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            var proc = Process.Start(psi);
            string output = proc.StandardOutput.ReadToEnd();
            string error = proc.StandardError.ReadToEnd();
            proc.WaitForExit(10000);
            return string.IsNullOrEmpty(output) ? error : output;
        }
        catch (System.Exception ex)
        {
            return $"Git error: {ex.Message}";
        }
    }
}
