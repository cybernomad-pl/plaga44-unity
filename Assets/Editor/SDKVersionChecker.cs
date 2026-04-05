// SDKVersionChecker.cs -- CYBERNOMAD Editor Tool
//
// Menu: CYBERNOMAD > Meta SDK Setup > Check SDK Versions
//
// Reads manifest.json and packages-lock.json, lists installed Meta XR packages,
// flags known version bugs, recommends upgrade/stay.
//
// Known bugs database -- update as new issues are found.
// Recommended version: see RecommendedVersion constant below.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class SDKVersionChecker
    {
        private const string LOG = "[SDKVersionChecker]";
        private const string RecommendedVersion = "81.0.0";

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

        // (versionPrefix, description, severity)
        private static readonly (string prefix, string desc, string severity)[] KnownBugs =
        {
            ("83.", "License validation bug: build may fail with 'Oculus license error'. Fix: delete Library/il2cpp_android_arm64 and rebuild.", "CRITICAL"),
            ("82.", "OVRSkeleton.Bones may return empty list on first frame. Add 1-frame delay in Start().", "WARN"),
            ("85.", "Audio SDK v85 + URP 17.x compatibility not confirmed. Test spatialization before upgrading.", "INFO"),
        };

        // =====================================================================
        // Menu
        // =====================================================================

        [MenuItem("CYBERNOMAD/Meta SDK Setup/Check SDK Versions", false, 50)]
        public static void CheckSDKVersions()
        {
            Debug.Log($"{LOG} ======== Meta XR SDK Version Check ========");
            Debug.Log($"{LOG} Recommended: v{RecommendedVersion}");

            var manifestVersions = ReadVersionsFromJson("manifest.json");
            var lockVersions = ReadVersionsFromJson("packages-lock.json");

            foreach (string pkg in MetaPackages)
            {
                manifestVersions.TryGetValue(pkg, out string mVer);
                lockVersions.TryGetValue(pkg, out string lVer);
                string installed = lVer ?? mVer;

                if (installed == null)
                {
                    Debug.Log($"{LOG}   {pkg,-45} -- not installed");
                    continue;
                }

                string status = CompareVersions(installed, RecommendedVersion) switch
                {
                    0 => "OK",
                    < 0 => "BEHIND",
                    _ => "AHEAD"
                };

                string bug = CheckBugs(installed);
                if (bug != null)
                    Debug.LogWarning($"{LOG}   {pkg,-45} v{installed} ({status})  !! {bug}");
                else
                    Debug.Log($"{LOG}   {pkg,-45} v{installed} ({status})");
            }

            Debug.Log($"{LOG} ============================================");
        }

        // =====================================================================
        // Parsing
        // =====================================================================

        static Dictionary<string, string> ReadVersionsFromJson(string filename)
        {
            var result = new Dictionary<string, string>();
            string path = Path.Combine(Application.dataPath, "..", "Packages", filename);
            if (!File.Exists(path)) return result;

            string text = File.ReadAllText(path);
            var regex = new Regex(@"""(com\.meta\.xr[^""]+)""\s*:\s*(?:\{[^}]*?""version""\s*:\s*)?""([^""]+)""",
                RegexOptions.Singleline);

            foreach (Match m in regex.Matches(text))
                result[m.Groups[1].Value] = m.Groups[2].Value;

            return result;
        }

        // =====================================================================
        // Bug database
        // =====================================================================

        static string CheckBugs(string version)
        {
            foreach (var (prefix, desc, severity) in KnownBugs)
                if (version.StartsWith(prefix))
                    return $"[{severity}] {desc}";
            return null;
        }

        // =====================================================================
        // Version compare (x.y.z numeric)
        // =====================================================================

        static int CompareVersions(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return -1;
            if (string.IsNullOrEmpty(b)) return 1;

            var pa = a.Split('.'); var pb = b.Split('.');
            int len = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < len; i++)
            {
                int na = i < pa.Length && int.TryParse(pa[i], out int x) ? x : 0;
                int nb = i < pb.Length && int.TryParse(pb[i], out int y) ? y : 0;
                if (na != nb) return na.CompareTo(nb);
            }
            return 0;
        }
    }
}
