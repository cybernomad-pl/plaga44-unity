// MetaQuestReset.cs
// PLAGA '44 -- Reset project to Unity defaults.
// Removes Meta XR packages and reverts ALL settings changed by MetaQuestSetup.cs.
//
// Menu: CYBERNOMAD > Meta SDK Setup > Reset to Unity Defaults
//
// This will:
// - Remove HAS_META_XR scripting define
// - Reset Input Handler to Old (Input Manager)
// - Remove all Meta XR SDK packages
// - Remove Meta scoped registry from manifest.json
// - Revert Player Settings to Unity 6 defaults
// - Revert Quality Settings to Unity 6 defaults
//
// After reset, the project is a clean Unity 6 project with no VR configuration.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
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

        private const string LOG = "[PLAGA44]";

        // ==================================================================
        // MENU: CYBERNOMAD > Meta SDK Setup > Reset to Unity Defaults
        // ==================================================================
        [MenuItem("CYBERNOMAD/Meta SDK Setup/Reset to Unity Defaults", false, 200)]
        public static void ResetToDefaults()
        {
            bool confirm = EditorUtility.DisplayDialog(
                "PLAGA '44 -- Reset to Defaults",
                "This will UNDO all Meta Quest setup:\n\n" +
                "- Remove HAS_META_XR scripting define\n" +
                "- Reset Input Handler to Old (Input Manager)\n" +
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
                "You will need to run Setup Steps 1-7 again to restore it.",
                "Yes, Reset",
                "No, Keep Settings"
            );

            if (!reallyConfirm) return;

            Debug.Log($"{LOG} ============================================");
            Debug.Log($"{LOG} Starting reset to Unity defaults...");
            Debug.Log($"{LOG} ============================================");

            // 1. Remove HAS_META_XR scripting define (FIRST -- before removing packages)
            RemoveScriptingDefine("HAS_META_XR");

            // 2. Reset Input Handler to Old (Input Manager)
            ResetInputHandler();

            // 3. Revert Player Settings (immediate)
            ResetPlayerSettings();

            // 4. Revert Quality Settings (immediate)
            ResetQualitySettings();

            // 5. Remove scoped registry from manifest.json
            RemoveScopedRegistry();

            // 6. Remove packages (async -- queued)
            StartPackageRemoval();

            Debug.Log($"{LOG} Settings reverted. Packages removing in background...");
        }

        // ==================================================================
        // SCRIPTING DEFINES -- remove HAS_META_XR
        // ==================================================================
        private static void RemoveScriptingDefine(string define)
        {
            var groups = new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone };
            foreach (var group in groups)
            {
                string current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
                var defines = current.Split(';')
                    .Select(d => d.Trim())
                    .Where(d => !string.IsNullOrEmpty(d) && d != define)
                    .ToList();

                string updated = string.Join(";", defines);
                if (updated != current)
                {
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(group, updated);
                    Debug.Log($"{LOG} Removed {define} from {group} scripting defines.");
                }
                else
                {
                    Debug.Log($"{LOG} {define} not found in {group} defines (already clean).");
                }
            }
        }

        // ==================================================================
        // INPUT HANDLER -- reset to Old (Input Manager)
        // ==================================================================
        private static void ResetInputHandler()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning($"{LOG} Cannot load ProjectSettings.asset to reset Input Handler.");
                return;
            }

            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null)
            {
                Debug.LogWarning($"{LOG} Cannot find activeInputHandler property.");
                return;
            }

            if (prop.intValue == 0)
            {
                Debug.Log($"{LOG} Input Handler already set to 'Old' (Input Manager).");
                return;
            }

            prop.intValue = 0; // 0 = Old (Input Manager)
            so.ApplyModifiedProperties();
            Debug.Log($"{LOG} Input Handler reset to 'Old' (Input Manager). Restart may be needed.");
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
                BuildTargetGroup.Android, "com.DefaultCompany.Myproject");

            // Graphics API: back to Auto (Unity decides)
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, true);

            // Scripting backend: Mono (Unity default for editor)
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.Android, ScriptingImplementation.Mono2x);

            // Architecture: ARMv7 + ARM64 (Unity default)
            PlayerSettings.Android.targetArchitectures =
                AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;

            // API levels -- Unity 6 defaults
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // Orientation: Auto Rotation (Unity default)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;

            // Re-enable all rotations
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            Debug.Log($"{LOG} Player Settings reverted to Unity defaults.");
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

            Debug.Log($"{LOG} Quality Settings reverted to Unity defaults.");
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
                Debug.Log($"{LOG} No Meta scoped registry found.");
                return;
            }

            // Remove the entire scopedRegistries block
            // Pattern: "scopedRegistries": [ ... ], (with optional trailing comma/whitespace)
            string pattern = @"""scopedRegistries""\s*:\s*\[[\s\S]*?\]\s*,?\s*\n?";
            manifest = Regex.Replace(manifest, pattern, "");

            // Also remove any Meta XR package entries from dependencies
            foreach (var pkg in PackagesToRemove)
            {
                string pkgPattern = $@"\s*""{Regex.Escape(pkg)}""\s*:\s*""[^""]*""\s*,?\n?";
                manifest = Regex.Replace(manifest, pkgPattern, "");
            }

            // Clean up: fix trailing commas in dependencies block
            // Remove comma before closing brace: ,\n  }  ->  \n  }
            manifest = Regex.Replace(manifest, @",(\s*\n\s*\})", "$1");

            // Clean up: remove double newlines
            manifest = Regex.Replace(manifest, @"\n\n\n+", "\n\n");

            File.WriteAllText(manifestPath, manifest);
            Client.Resolve();
            Debug.Log($"{LOG} Removed Meta scoped registry and package entries from manifest.json.");
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
                Debug.Log($"{LOG} All {_totalPackages} packages processed.");
                Debug.Log($"{LOG} ============================================");
                Debug.Log($"{LOG} RESET COMPLETE.");
                Debug.Log($"{LOG} Project is now a clean Unity 6 project.");
                Debug.Log($"{LOG} Run 'CYBERNOMAD > Meta SDK Setup > Step 1' to reconfigure.");
                Debug.Log($"{LOG} ============================================");

                EditorUtility.DisplayDialog(
                    "PLAGA '44 -- Reset Complete",
                    "All Meta Quest configuration removed.\n" +
                    "Project is now a clean Unity 6 project.\n\n" +
                    "To reconfigure for Meta Quest, start from:\n" +
                    "CYBERNOMAD > Meta SDK Setup > Step 1",
                    "OK"
                );
                return;
            }

            var pkg = _removeQueue.Dequeue();
            _removedCount++;

            Debug.Log($"{LOG} [{_removedCount}/{_totalPackages}] Removing {pkg}...");
            _removeRequest = Client.Remove(pkg);
        }

        private static void PackageRemoveTick()
        {
            if (_removeRequest == null || !_removeRequest.IsCompleted) return;

            if (_removeRequest.Status == StatusCode.Success)
            {
                Debug.Log($"{LOG} Removed: {_removeRequest.PackageIdOrName}");
            }
            else if (_removeRequest.Status >= StatusCode.Failure)
            {
                // Not an error -- package might not have been installed
                Debug.Log($"{LOG} Skip (not installed): {_removeRequest.Error?.message ?? "ok"}");
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
                Debug.LogError($"{LOG} Packages/manifest.json not found!");
                return null;
            }
            return path;
        }
    }
}
#endif
