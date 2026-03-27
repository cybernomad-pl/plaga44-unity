// AUTO-DISABLED: not needed for demo
#if PLAGA44_FULL_SDK
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    /// <summary>
    /// SDK Version Checker -- menu CYBERNOMAD / Meta SDK Setup / Check SDK Versions
    ///
    /// Reads manifest.json and packages-lock.json, lists installed Meta XR package versions,
    /// flags known bugs, and recommends upgrade/stay based on current project requirements.
    ///
    /// Known bugs database (update as new issues are found):
    ///   v83.x -- Oculus License validation bug (build fails with license error on some machines)
    ///   v84.x -- No known critical bugs (as of 2026-02)
    ///   v85.x -- Latest; evaluation recommended (check Audio SDK compatibility)
    ///
    /// Related issues: #13 (SDK v85 eval), #17 (default assets review), #46 (ISDK integration)
    /// </summary>
    public static class SDKVersionChecker
    {
        private const string LOG = "[SDKVersionChecker]";

        // Meta XR packages to check
        private static readonly string[] MetaPackages =
        {
            "com.meta.xr.sdk.core",
            "com.meta.xr.sdk.interaction",
            "com.meta.xr.sdk.interaction.ovr",
            "com.meta.xr.sdk.audio",
            "com.meta.xr.sdk.movement",
            "com.meta.xr.sdk.avatars",
            "com.meta.xr.sdk.platform",
            "com.meta.xr.sdk.utilities",
        };

        // Known bugs per version prefix -- (versionPrefix, bugDescription, severity)
        // severity: "CRITICAL" / "WARN" / "INFO"
        private static readonly (string prefix, string description, string severity)[] KnownBugs =
        {
            ("83.", "License validation bug: Editor build may fail with 'Oculus license error' on some machines. Workaround: delete Library/il2cpp_android_arm64 and rebuild.", "CRITICAL"),
            ("83.0", "Same as 83.x -- see above.", "CRITICAL"),
            ("82.", "OVRSkeleton.Bones may return empty list on first frame -- add 1-frame delay in Start().", "WARN"),
            ("85.", "Audio SDK (com.meta.xr.sdk.audio) v85 compatibility with URP 17.x not yet confirmed. Test spatialization before upgrading.", "INFO"),
        };

        // Recommended version for this project
        private const string RecommendedVersion = "81.0.0";
        private const string EvalVersion        = "85.0.0"; // Under evaluation per issue #13

        [MenuItem("CYBERNOMAD/Meta SDK Setup/Check SDK Versions", false, 50)]
        public static void CheckSDKVersions()
        {
            Debug.Log($"{LOG} ======== Meta XR SDK Version Check ========");
            Debug.Log($"{LOG} Date: {DateTime.Now:yyyy-MM-dd HH:mm}");
            Debug.Log($"{LOG} Current project version: {RecommendedVersion} | Eval version: {EvalVersion}");
            Debug.Log($"{LOG} -------------------------------------------");

            var manifestVersions = ReadManifestVersions();
            var lockVersions     = ReadLockfileVersions();

            bool anyIssue    = false;
            bool anyMissing  = false;

            foreach (string pkg in MetaPackages)
            {
                bool inManifest = manifestVersions.TryGetValue(pkg, out string manifestVer);
                bool inLock     = lockVersions.TryGetValue(pkg, out string lockVer);

                if (!inManifest && !inLock)
                {
                    Debug.Log($"{LOG}   {pkg,-45} NOT IN PROJECT");
                    anyMissing = true;
                    continue;
                }

                string installed = inLock ? lockVer : manifestVer;
                string bugs      = CheckKnownBugs(installed, pkg);

                string status = "";
                if (CompareVersions(installed, RecommendedVersion) > 0)
                    status = "(AHEAD of recommended)";
                else if (CompareVersions(installed, RecommendedVersion) < 0)
                    status = "(BEHIND recommended)";
                else
                    status = "(OK -- recommended)";

                if (bugs != null)
                {
                    Debug.LogWarning($"{LOG}   {pkg,-45} v{installed} {status}\n           !! {bugs}");
                    anyIssue = true;
                }
                else
                {
                    Debug.Log($"{LOG}   {pkg,-45} v{installed} {status}");
                }
            }

            Debug.Log($"{LOG} -------------------------------------------");
            PrintUpgradeRecommendation(manifestVersions, anyIssue, anyMissing);
            Debug.Log($"{LOG} ============================================");
        }

        // -------------------------------------------------------------------------
        // Manifest / Lockfile Parsing
        // -------------------------------------------------------------------------

        private static Dictionary<string, string> ReadManifestVersions()
        {
            var result = new Dictionary<string, string>();
            string path = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

            if (!File.Exists(path))
            {
                Debug.LogError($"{LOG} manifest.json not found at: {path}");
                return result;
            }

            string text = File.ReadAllText(path);
            ParsePackageVersionsFromJson(text, result);
            return result;
        }

        private static Dictionary<string, string> ReadLockfileVersions()
        {
            var result = new Dictionary<string, string>();
            string path = Path.Combine(Application.dataPath, "..", "Packages", "packages-lock.json");

            if (!File.Exists(path))
            {
                // Not an error -- lock file may not exist yet
                Debug.Log($"{LOG} packages-lock.json not found (packages not yet resolved).");
                return result;
            }

            string text = File.ReadAllText(path);
            // Lock file uses "version": "x.y.z" inside package blocks
            // Parse with regex: find package name then "version" nearby
            var blockRegex = new Regex(
                @"""(com\.meta\.xr[^""]+)""\s*:\s*\{[^}]*?""version""\s*:\s*""([^""]+)""",
                RegexOptions.Singleline);

            foreach (Match m in blockRegex.Matches(text))
            {
                string pkg = m.Groups[1].Value;
                string ver = m.Groups[2].Value;
                result[pkg] = ver;
            }

            return result;
        }

        private static void ParsePackageVersionsFromJson(string json, Dictionary<string, string> output)
        {
            // Simple regex: "com.meta.xr.*": "version"
            var regex = new Regex(@"""(com\.meta\.xr[^""]+)""\s*:\s*""([^""]+)""");
            foreach (Match m in regex.Matches(json))
            {
                string pkg = m.Groups[1].Value;
                string ver = m.Groups[2].Value;
                output[pkg] = ver;
            }
        }

        // -------------------------------------------------------------------------
        // Bug Check
        // -------------------------------------------------------------------------

        private static string CheckKnownBugs(string version, string pkg)
        {
            if (string.IsNullOrEmpty(version))
                return null;

            foreach (var (prefix, description, severity) in KnownBugs)
            {
                if (version.StartsWith(prefix))
                    return $"[{severity}] v{prefix}* bug: {description}";
            }
            return null;
        }

        // -------------------------------------------------------------------------
        // Upgrade Recommendation
        // -------------------------------------------------------------------------

        private static void PrintUpgradeRecommendation(
            Dictionary<string, string> installed, bool anyIssue, bool anyMissing)
        {
            installed.TryGetValue("com.meta.xr.sdk.core", out string coreVer);

            Debug.Log($"{LOG} RECOMMENDATION:");

            if (string.IsNullOrEmpty(coreVer))
            {
                Debug.LogWarning($"{LOG}   Core SDK not installed. Run CYBERNOMAD / Meta SDK Setup / 1. Setup Meta SDK");
                return;
            }

            int cmpRecommended = CompareVersions(coreVer, RecommendedVersion);
            int cmpEval        = CompareVersions(coreVer, EvalVersion);

            if (cmpEval == 0)
            {
                Debug.Log($"{LOG}   Currently on eval version {EvalVersion}. " +
                          "Run full interaction test suite before promoting to main.");
            }
            else if (cmpRecommended == 0 && !anyIssue)
            {
                Debug.Log($"{LOG}   Version {RecommendedVersion} -- stable, no known bugs. NO UPGRADE NEEDED.");
                Debug.Log($"{LOG}   To evaluate v85: change all com.meta.xr.* to 85.0.0 in manifest.json and run this check again.");
            }
            else if (cmpRecommended == 0 && anyIssue)
            {
                Debug.LogWarning($"{LOG}   Version {RecommendedVersion} has known issues (see above). Consider patching or upgrading.");
            }
            else if (cmpRecommended < 0)
            {
                Debug.LogWarning($"{LOG}   Installed v{coreVer} is OLDER than recommended {RecommendedVersion}. Upgrade recommended.");
            }
            else
            {
                // Newer than recommended but not eval
                Debug.Log($"{LOG}   Installed v{coreVer} is newer than pinned {RecommendedVersion}. " +
                          "Monitor for breaking changes. Run interaction tests.");
            }

            if (anyMissing)
            {
                Debug.Log($"{LOG}   Some optional Meta packages (Movement SDK, Avatars, Platform) are not installed. " +
                          "Add them to manifest.json when their features are needed.");
            }
        }

        // -------------------------------------------------------------------------
        // Version Compare (simple numeric, handles x.y.z)
        // -------------------------------------------------------------------------

        private static int CompareVersions(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return -1;
            if (string.IsNullOrEmpty(b)) return 1;

            var partsA = a.Split('.');
            var partsB = b.Split('.');

            int len = Math.Max(partsA.Length, partsB.Length);
            for (int i = 0; i < len; i++)
            {
                int numA = i < partsA.Length && int.TryParse(partsA[i], out int pa) ? pa : 0;
                int numB = i < partsB.Length && int.TryParse(partsB[i], out int pb) ? pb : 0;

                if (numA != numB)
                    return numA.CompareTo(numB);
            }
            return 0;
        }
    }
}
#endif
#endif
