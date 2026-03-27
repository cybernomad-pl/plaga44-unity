// AUTO-DISABLED: not needed for demo
#if PLAGA44_FULL_SDK
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    /// <summary>
    /// Default Asset Browser -- menu CYBERNOMAD / Assets / Browse SDK Assets
    ///
    /// Scans PackageCache for prefabs shipped with Meta XR Interaction SDK
    /// (com.meta.xr.sdk.interaction) and lists them in the Console with full paths.
    ///
    /// Useful for quickly locating ready-made assets:
    ///   - Environment prefabs: LocomotionEnvironment, RoomEnvironment, SmallRoomEnvironment
    ///   - Grabbable props:     Box, BigStone, ChessPiece, Mug, Torch, Key, CoffeeCup
    ///   - Interactor templates: Template_HandGrabInteractor, Template_ControllerGrabInteractor
    ///   - Stone shapes:        StoneCube, StoneTetrahedron, etc.
    ///
    /// Click any logged path to highlight the asset in the Project window.
    ///
    /// Related issues: #17 (default assets review), #46 (ISDK integration)
    /// </summary>
    public static class DefaultAssetBrowser
    {
        private const string LOG = "[DefaultAssetBrowser]";

        // Known interesting asset categories + name patterns
        // Each entry: (category label, name substring or exact name)
        private static readonly (string category, string pattern)[] InterestingPatterns =
        {
            // Environments
            ("ENVIRONMENT",  "LocomotionEnvironment"),
            ("ENVIRONMENT",  "RoomEnvironment"),
            ("ENVIRONMENT",  "SmallRoomEnvironment"),
            ("ENVIRONMENT",  "LargeRoom"),
            ("ENVIRONMENT",  "Desk"),
            // Props -- grabs
            ("PROP-GRAB",    "Box"),
            ("PROP-GRAB",    "BigStone"),
            ("PROP-GRAB",    "ChessPiece"),
            ("PROP-GRAB",    "Doll"),
            ("PROP-GRAB",    "Mug"),
            ("PROP-GRAB",    "CoffeeCup"),
            ("PROP-GRAB",    "Torch"),
            ("PROP-GRAB",    "Key"),
            // Stone polyhedra
            ("PROP-STONE",   "StoneCube"),
            ("PROP-STONE",   "StoneTetrahedron"),
            ("PROP-STONE",   "StoneOctahedron"),
            ("PROP-STONE",   "StoneDodecahedron"),
            ("PROP-STONE",   "StoneIcosahedron"),
            ("PROP-STONE",   "StonePolyhedron"),
            // Interaction templates
            ("TEMPLATE",     "Template_HandGrabInteractor"),
            ("TEMPLATE",     "Template_HandPokeInteractor"),
            ("TEMPLATE",     "Template_HandRayInteractor"),
            ("TEMPLATE",     "Template_HandDistanceGrabInteractor"),
            ("TEMPLATE",     "Template_ControllerGrabInteractor"),
            ("TEMPLATE",     "Template_ControllerPokeInteractor"),
            ("TEMPLATE",     "Template_HandGrabInteraction"),
            ("TEMPLATE",     "Template_PokeInteraction"),
            ("TEMPLATE",     "BaseInteractors"),
            // Interactive
            ("PROP-INTERACT", "BigRedButton"),
            ("PROP-INTERACT", "Keypad"),
            ("PROP-INTERACT", "PingPongBall"),
            ("PROP-INTERACT", "PictureFrame"),
        };

        // Target SDK packages to scan
        private static readonly string[] TargetPackages =
        {
            "com.meta.xr.sdk.interaction",
            "com.meta.xr.sdk.interaction.ovr",
            "com.meta.xr.sdk.core",
        };

        [MenuItem("CYBERNOMAD/Assets/Browse SDK Assets", false, 100)]
        public static void BrowseSDKAssets()
        {
            Debug.Log($"{LOG} ======== Meta XR SDK Default Asset Browser ========");
            Debug.Log($"{LOG} Scanning PackageCache for Meta XR prefabs...");

            string packageCachePath = FindPackageCachePath();
            if (packageCachePath == null)
            {
                Debug.LogError($"{LOG} Could not locate Library/PackageCache. Is the project opened in Unity?");
                return;
            }

            Debug.Log($"{LOG} PackageCache: {packageCachePath}");
            Debug.Log($"{LOG} ---------------------------------------------------");

            // Find package directories
            var packageDirs = FindPackageDirectories(packageCachePath);
            if (packageDirs.Count == 0)
            {
                Debug.LogWarning($"{LOG} No Meta XR packages found in PackageCache. " +
                                 "Have packages been resolved? Run CYBERNOMAD / Meta SDK Setup / 1. Setup Meta SDK");
                return;
            }

            Debug.Log($"{LOG} Found {packageDirs.Count} Meta XR package(s) in cache:");
            foreach (var dir in packageDirs)
                Debug.Log($"{LOG}   {Path.GetFileName(dir)}");

            Debug.Log($"{LOG} ---------------------------------------------------");

            // Scan for all .prefab files
            var allPrefabs = new List<(string fullPath, string packageName)>();
            foreach (var dir in packageDirs)
            {
                string pkgName = ExtractPackageName(dir);
                var prefabs = Directory.GetFiles(dir, "*.prefab", SearchOption.AllDirectories);
                foreach (var p in prefabs)
                    allPrefabs.Add((p, pkgName));
            }

            Debug.Log($"{LOG} Total prefabs found: {allPrefabs.Count}");
            Debug.Log($"{LOG} ---------------------------------------------------");

            // Match and print by category
            var reported = new HashSet<string>();
            var categorized = new Dictionary<string, List<(string path, string pkg)>>();

            foreach (var (category, pattern) in InterestingPatterns)
            {
                foreach (var (fullPath, pkgName) in allPrefabs)
                {
                    string fileName = Path.GetFileNameWithoutExtension(fullPath);
                    if (!string.Equals(fileName, pattern, System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    string key = fullPath;
                    if (reported.Contains(key))
                        continue;
                    reported.Add(key);

                    if (!categorized.ContainsKey(category))
                        categorized[category] = new List<(string, string)>();
                    categorized[category].Add((fullPath, pkgName));
                }
            }

            // Print categorized results
            int totalFound = 0;
            foreach (var cat in categorized.Keys.OrderBy(k => k))
            {
                Debug.Log($"{LOG} [{cat}]");
                foreach (var (path, pkg) in categorized[cat].OrderBy(x => Path.GetFileName(x.path)))
                {
                    // Convert to project-relative path for clickable console link
                    string relPath = ToProjectRelativePath(path);
                    if (relPath != null)
                        Debug.Log($"{LOG}   {Path.GetFileName(path),-40} @ {relPath}", AssetDatabase.LoadAssetAtPath<Object>(relPath));
                    else
                        Debug.Log($"{LOG}   {Path.GetFileName(path),-40} @ {path}");
                    totalFound++;
                }
            }

            Debug.Log($"{LOG} ---------------------------------------------------");
            Debug.Log($"{LOG} Highlighted: {totalFound} / {reported.Count} interesting prefabs found.");

            // Report any of the patterns that were NOT found
            var notFound = InterestingPatterns
                .Select(p => p.pattern)
                .Distinct()
                .Where(pattern => !categorized.Values.SelectMany(v => v).Any(x =>
                    string.Equals(Path.GetFileNameWithoutExtension(x.path), pattern, System.StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (notFound.Count > 0)
            {
                Debug.Log($"{LOG} NOT FOUND (may not be in installed SDK version):");
                foreach (var p in notFound)
                    Debug.Log($"{LOG}   - {p}");
            }

            Debug.Log($"{LOG} ====================================================");
            Debug.Log($"{LOG} TIP: Click a path above to select asset in Project window.");
            Debug.Log($"{LOG} TIP: Use CYBERNOMAD/Meta SDK Setup/Check SDK Versions to verify installed versions.");
        }

        // -------------------------------------------------------------------------
        // Path Helpers
        // -------------------------------------------------------------------------

        private static string FindPackageCachePath()
        {
            // Application.dataPath is .../Assets, PackageCache is in .../Library/PackageCache
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string cachePath = Path.Combine(projectRoot, "Library", "PackageCache");
            return Directory.Exists(cachePath) ? cachePath : null;
        }

        private static List<string> FindPackageDirectories(string cachePath)
        {
            var result = new List<string>();
            if (!Directory.Exists(cachePath))
                return result;

            foreach (string dir in Directory.GetDirectories(cachePath))
            {
                string dirName = Path.GetFileName(dir);
                foreach (var pkg in TargetPackages)
                {
                    // PackageCache dirs are named: com.meta.xr.sdk.interaction@<hash>
                    if (dirName.StartsWith(pkg))
                    {
                        result.Add(dir);
                        break;
                    }
                }
            }
            return result;
        }

        private static string ExtractPackageName(string dir)
        {
            string name = Path.GetFileName(dir);
            int atIdx = name.IndexOf('@');
            return atIdx > 0 ? name.Substring(0, atIdx) : name;
        }

        /// <summary>
        /// Convert an absolute filesystem path inside PackageCache to a Unity package: path
        /// that AssetDatabase can load (e.g. "Packages/com.meta.xr.sdk.interaction/...").
        /// </summary>
        private static string ToProjectRelativePath(string absolutePath)
        {
            // Normalize separators
            string norm = absolutePath.Replace('\\', '/');
            string projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');

            // Check if it's inside Library/PackageCache
            string cachePrefix = projectRoot + "/Library/PackageCache/";
            if (!norm.StartsWith(cachePrefix))
                return null;

            string relative = norm.Substring(cachePrefix.Length);

            // Extract package name (strip hash suffix from directory name)
            int slashIdx = relative.IndexOf('/');
            if (slashIdx < 0)
                return null;

            string dirName = relative.Substring(0, slashIdx);
            string rest    = relative.Substring(slashIdx); // includes leading /

            int atIdx = dirName.IndexOf('@');
            string pkgName = atIdx > 0 ? dirName.Substring(0, atIdx) : dirName;

            return $"Packages/{pkgName}{rest}";
        }
    }
}
#endif
#endif
