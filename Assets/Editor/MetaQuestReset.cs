// MetaQuestReset.cs
// PLAGA '44 -- Reset project to Unity defaults.
// Removes Meta XR packages and reverts all settings changed by MetaQuestSetup.cs.
//
// Usage: Unity Editor menu -> PLAGA44 -> Reset to Unity Defaults
//
// This will:
// - Remove all Meta XR SDK packages
// - Remove Meta scoped registry from manifest.json
// - Revert Player Settings to Unity 6 defaults
// - Revert Quality Settings to Unity 6 defaults
//
// After reset, the project is a clean Unity 6 project with no VR configuration.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plaga44.Editor
{
    public static class MetaQuestReset
    {
        // Packages to remove (everything MetaQuestSetup installs)
        private static readonly string[] PackagesToRemove = new[]
        {
            "com.meta.xr.sdk.core",
            "com.meta.xr.sdk.interaction",
            "com.meta.xr.sdk.audio",
            "com.unity.xr.meta-openxr",
            "com.unity.xr.openxr",
        };

        // Remove queue
        private static Queue<string> _removeQueue;
        private static RemoveRequest _removeRequest;
        private static int _totalPackages;
        private static int _removedCount;

        // ==================================================================
        // MENU: PLAGA44 > Reset to Unity Defaults
        // ==================================================================
        [MenuItem("PLAGA44/Reset to Unity Defaults")]
        public static void ResetToDefaults()
        {
            bool confirm = EditorUtility.DisplayDialog(
                "PLAGA '44 -- Reset to Defaults",
                "This will UNDO all Meta Quest setup:\n\n" +
                "- Remove Meta XR SDK packages\n" +
                "- Remove Meta scoped registry\n" +
                "- Reset Player Settings to Unity defaults\n" +
                "- Reset Quality Settings to Unity defaults\n\n" +
                "Company/product names will be reset.\n" +
                "Color space will revert to Gamma.\n\n" +
                "This CANNOT be undone. Continue?",
                "Reset Everything",
                "Cancel"
            );

            if (!confirm) return;

            // Double confirm -- this is destructive
            bool reallyConfirm = EditorUtility.DisplayDialog(
                "Are you sure?",
                "All Meta Quest configuration will be removed.\n" +
                "You will need to run Setup again to restore it.",
                "Yes, Reset",
                "No, Keep Settings"
            );

            if (!reallyConfirm) return;

            Debug.Log("[PLAGA44] ============================================");
            Debug.Log("[PLAGA44] Starting reset to Unity defaults...");
            Debug.Log("[PLAGA44] ============================================");

            // 1. Revert Player Settings (immediate)
            ResetPlayerSettings();

            // 2. Revert Quality Settings (immediate)
            ResetQualitySettings();

            // 3. Remove scoped registry from manifest.json
            RemoveScopedRegistry();

            // 4. Remove packages (async -- queued)
            StartPackageRemoval();

            Debug.Log("[PLAGA44] Settings reverted. Packages removing in background...");
        }

        // ==================================================================
        // PLAYER SETTINGS -- revert to Unity 6 defaults
        // ==================================================================
        private static void ResetPlayerSettings()
        {
            // Company and product -- Unity defaults
            PlayerSettings.companyName = "DefaultCompany";
            PlayerSettings.productName = "My project";

            // Color space -- Unity default is Gamma
            PlayerSettings.colorSpace = ColorSpace.Gamma;

            // Android package -- generic default
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android, "com.DefaultCompany.Myproject");

            // Graphics API: back to Auto (Unity decides)
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, true);

            // Scripting backend: Mono (Unity default for editor)
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Android, ScriptingImplementation.Mono2x);

            // Architecture: ARM64 (Unity 6 default)
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // API levels -- Unity 6 defaults (min supported is 25)
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // Orientation: Auto Rotation (Unity default)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;

            // Re-enable all rotations
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            Debug.Log("[PLAGA44] Player Settings reverted to Unity defaults.");
        }

        // ==================================================================
        // QUALITY SETTINGS -- revert to Unity 6 defaults
        // ==================================================================
        private static void ResetQualitySettings()
        {
            // Anti-Aliasing: disabled (Unity default)
            QualitySettings.antiAliasing = 0;

            // VSync: Every V Blank (Unity default)
            QualitySettings.vSyncCount = 1;

            // Anisotropic filtering: Per Texture (Unity default)
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;

            // Texture quality: Full resolution
            QualitySettings.globalTextureMipmapLimit = 0;

            // Shadow distance: Unity default
            QualitySettings.shadowDistance = 150f;

            // LOD bias: Unity default
            QualitySettings.lodBias = 2.0f;

            // Pixel light count: Unity default
            QualitySettings.pixelLightCount = 4;

            Debug.Log("[PLAGA44] Quality Settings reverted to Unity defaults.");
        }

        // ==================================================================
        // SCOPED REGISTRY -- remove Meta XR from manifest.json
        // ==================================================================
        private static void RemoveScopedRegistry()
        {
            string manifestPath = GetManifestPath();
            if (manifestPath == null) return;

            string manifest = File.ReadAllText(manifestPath);

            if (!manifest.Contains("npm.developer.oculus.com"))
            {
                Debug.Log("[PLAGA44] No Meta scoped registry found.");
                return;
            }

            // Line-by-line removal -- regex can't handle nested JSON brackets safely.
            // Strategy: find "scopedRegistries" line, track bracket depth, remove
            // everything from that line until depth returns to 0, plus trailing comma.
            var lines = new List<string>(manifest.Split('\n'));
            var output = new List<string>();
            bool inScopedBlock = false;
            int bracketDepth = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];

                if (!inScopedBlock && line.Contains("\"scopedRegistries\""))
                {
                    inScopedBlock = true;
                    bracketDepth = 0;
                    // Count brackets on this line
                    foreach (char c in line)
                    {
                        if (c == '[') bracketDepth++;
                        if (c == ']') bracketDepth--;
                    }
                    // If closed on same line (unlikely), we're done
                    if (bracketDepth <= 0) inScopedBlock = false;
                    continue; // skip this line
                }

                if (inScopedBlock)
                {
                    foreach (char c in line)
                    {
                        if (c == '[') bracketDepth++;
                        if (c == ']') bracketDepth--;
                    }
                    if (bracketDepth <= 0)
                    {
                        inScopedBlock = false;
                        // Check if next non-empty line starts with comma (clean it)
                        // Also handle "], " on this line -- skip it entirely
                    }
                    continue; // skip lines inside scopedRegistries block
                }

                output.Add(line);
            }

            manifest = string.Join("\n", output);

            // Also remove any Meta XR package entries from dependencies
            foreach (var pkg in PackagesToRemove)
            {
                // Remove line containing this package key
                string pkgPattern = $@"[ \t]*""{Regex.Escape(pkg)}""[ \t]*:[ \t]*""[^""]*""[ \t]*,?[ \t]*\n?";
                manifest = Regex.Replace(manifest, pkgPattern, "");
            }

            // Fix trailing commas before closing brace: ,\n  }  ->  \n  }
            manifest = Regex.Replace(manifest, @",(\s*\n\s*\})", "$1");

            // Remove empty lines
            manifest = Regex.Replace(manifest, @"\n\n+", "\n");

            File.WriteAllText(manifestPath, manifest);
            Client.Resolve();
            Debug.Log("[PLAGA44] Removed Meta scoped registry and package entries from manifest.json.");
        }

        // ==================================================================
        // PACKAGE REMOVAL -- async queue via Client.Remove
        // ==================================================================
        private static void StartPackageRemoval()
        {
            _removeQueue = new Queue<string>(PackagesToRemove);
            _totalPackages = PackagesToRemove.Length;
            _removedCount = 0;

            EditorApplication.update += PackageRemoveTick;
            RemoveNextPackage();
        }

        private static void RemoveNextPackage()
        {
            if (_removeQueue.Count == 0)
            {
                EditorApplication.update -= PackageRemoveTick;
                Debug.Log($"[PLAGA44] All {_totalPackages} packages processed.");
                Debug.Log("[PLAGA44] ============================================");
                Debug.Log("[PLAGA44] RESET COMPLETE.");
                Debug.Log("[PLAGA44] Project is now a clean Unity 6 project.");
                Debug.Log("[PLAGA44] Run 'PLAGA44 > Setup Meta Quest Settings' to reconfigure.");
                Debug.Log("[PLAGA44] ============================================");

                EditorUtility.DisplayDialog(
                    "PLAGA '44 -- Reset Complete",
                    "All Meta Quest configuration removed.\n" +
                    "Project is now a clean Unity 6 project.\n\n" +
                    "To reconfigure for Meta Quest, run:\n" +
                    "PLAGA44 > Setup Meta Quest Settings",
                    "OK"
                );
                return;
            }

            var pkg = _removeQueue.Dequeue();
            _removedCount++;

            Debug.Log($"[PLAGA44] [{_removedCount}/{_totalPackages}] Removing {pkg}...");
            _removeRequest = Client.Remove(pkg);
        }

        private static void PackageRemoveTick()
        {
            if (_removeRequest == null || !_removeRequest.IsCompleted) return;

            if (_removeRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[PLAGA44] Removed: {_removeRequest.PackageIdOrName}");
            }
            else if (_removeRequest.Status >= StatusCode.Failure)
            {
                // Not an error -- package might not have been installed
                Debug.Log($"[PLAGA44] Skip (not installed): {_removeRequest.Error?.message ?? "ok"}");
            }

            _removeRequest = null;
            RemoveNextPackage();
        }

        // ==================================================================
        // HELPERS
        // ==================================================================
        private static string GetManifestPath()
        {
            string path = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            if (!File.Exists(path))
            {
                Debug.LogError("[PLAGA44] Packages/manifest.json not found!");
                return null;
            }
            return path;
        }
    }
}
#endif
