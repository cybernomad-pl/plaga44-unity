// MetaQuestSetup.cs
// PLAGA '44 -- Full automated Meta Quest project setup.
// Installs Meta XR SDK packages AND configures all project settings.
//
// Usage: Unity Editor menu -> PLAGA44 -> Setup Meta Quest Settings
//
// Installs Meta XR SDK v81 (v83 has license bug on Unity 6.3).
// Bug: https://communityforums.atmeta.com/discussions/Questions_Discussions/unity-6-3---meta-xr-core-license-error/1357387
//
// After running, remaining manual steps:
// - File > Build Profiles > Meta Quest > Switch Platform
// - Edit > Project Settings > XR Plug-in Management > Enable OpenXR (Android)
// - Add Meta Quest Feature Group in OpenXR settings
// - Scene: Building Blocks > Camera Rig + Controller Tracking
//
// See also: MetaQuestReset.cs for reverting to Unity defaults.

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
    public static class MetaQuestSetup
    {
        // ------------------------------------------------------------------
        // Meta XR SDK v81 -- pinned to avoid v83 license bug on Unity 6.3.
        // When Meta fixes the bug, bump these to latest.
        // ------------------------------------------------------------------
        private const string META_SDK_VERSION = "81.0.0";

        private static readonly string[][] PackagesToInstall = new[]
        {
            new[] { "com.unity.xr.openxr",          "1.14.0" },
            new[] { "com.unity.xr.meta-openxr",      "2.4.0"  },
            new[] { "com.meta.xr.sdk.core",           META_SDK_VERSION },
            new[] { "com.meta.xr.sdk.interaction",    META_SDK_VERSION },
            new[] { "com.meta.xr.sdk.audio",          META_SDK_VERSION },
        };

        // Scoped registry block for manifest.json (Meta XR UPM)
        private const string SCOPED_REGISTRY_JSON = @"  ""scopedRegistries"": [
    {
      ""name"": ""Meta XR"",
      ""url"": ""https://npm.developer.oculus.com"",
      ""scopes"": [
        ""com.meta.xr""
      ]
    }
  ],
";

        // Package install queue
        private static Queue<string[]> _installQueue;
        private static AddRequest _addRequest;
        private static int _totalPackages;
        private static int _installedCount;

        // ==================================================================
        // MENU: PLAGA44 > Setup Meta Quest Settings
        // ==================================================================
        [MenuItem("PLAGA44/Setup Meta Quest Settings")]
        public static void SetupMetaQuestSettings()
        {
            string packageList = "";
            foreach (var pkg in PackagesToInstall)
                packageList += $"  {pkg[0]} @ {pkg[1]}\n";

            bool confirm = EditorUtility.DisplayDialog(
                "PLAGA '44 -- Meta Quest Setup",
                "Full automated setup for Meta Quest 2/3/3S.\n\n" +
                "PACKAGES (auto-installed):\n" + packageList + "\n" +
                "SETTINGS:\n" +
                "  Color Space: Linear\n" +
                "  Graphics API: Vulkan (Android)\n" +
                "  Scripting Backend: IL2CPP\n" +
                "  Architecture: ARM64\n" +
                "  Min API: Android 10 (29)\n" +
                "  Quality: 4x MSAA, no VSync\n\n" +
                "Continue?",
                "Apply Everything",
                "Cancel"
            );

            if (!confirm) return;

            Debug.Log("[PLAGA44] ============================================");
            Debug.Log("[PLAGA44] Starting Meta Quest setup...");
            Debug.Log("[PLAGA44] ============================================");

            // 1. Ensure scoped registry in manifest.json
            EnsureScopedRegistry();

            // 2. Install packages (async -- queued via EditorApplication.update)
            StartPackageInstall();

            // 3. Player Settings (immediate)
            SetPlayerSettings();

            // 4. Quality Settings (immediate)
            SetQualitySettings();

            Debug.Log("[PLAGA44] Settings applied. Packages installing in background...");
            Debug.Log("[PLAGA44] Watch Console for install progress.");
        }

        // ==================================================================
        // SCOPED REGISTRY -- ensure Meta XR registry is in manifest.json
        // ==================================================================
        private static void EnsureScopedRegistry()
        {
            string manifestPath = GetManifestPath();
            if (manifestPath == null) return;

            string manifest = File.ReadAllText(manifestPath);

            if (manifest.Contains("npm.developer.oculus.com"))
            {
                Debug.Log("[PLAGA44] Scoped registry already present.");
                return;
            }

            // Insert scopedRegistries before "dependencies"
            int depsIdx = manifest.IndexOf("\"dependencies\"");
            if (depsIdx < 0)
            {
                Debug.LogError("[PLAGA44] Cannot find 'dependencies' in manifest.json");
                return;
            }

            // Find the start of the line containing "dependencies"
            int lineStart = manifest.LastIndexOf('\n', depsIdx);
            if (lineStart < 0) lineStart = 0;
            else lineStart += 1; // skip the newline itself

            manifest = manifest.Substring(0, lineStart)
                     + SCOPED_REGISTRY_JSON
                     + manifest.Substring(lineStart);

            File.WriteAllText(manifestPath, manifest);
            Debug.Log("[PLAGA44] Added Meta XR scoped registry to manifest.json.");
        }

        // ==================================================================
        // PACKAGE INSTALL -- async queue via Client.Add
        // ==================================================================
        private static void StartPackageInstall()
        {
            _installQueue = new Queue<string[]>(PackagesToInstall);
            _totalPackages = PackagesToInstall.Length;
            _installedCount = 0;

            EditorApplication.update += PackageInstallTick;
            InstallNextPackage();
        }

        private static void InstallNextPackage()
        {
            if (_installQueue.Count == 0)
            {
                EditorApplication.update -= PackageInstallTick;
                Debug.Log($"[PLAGA44] All {_totalPackages} packages processed.");
                Debug.Log("[PLAGA44] ============================================");
                Debug.Log("[PLAGA44] SETUP COMPLETE. Remaining manual steps:");
                Debug.Log("[PLAGA44] 1. File > Build Profiles > Meta Quest > Switch Platform");
                Debug.Log("[PLAGA44] 2. Edit > Project Settings > XR Plug-in Management > Android > Enable OpenXR");
                Debug.Log("[PLAGA44] 3. Under OpenXR > Add Meta Quest Feature Group");
                Debug.Log("[PLAGA44] 4. Scene: Meta > Tools > Building Blocks > Camera Rig + Controllers");
                Debug.Log("[PLAGA44] See docs/META_SDK_SETUP.md for details.");
                Debug.Log("[PLAGA44] ============================================");

                EditorUtility.DisplayDialog(
                    "PLAGA '44 -- Setup Complete",
                    "All packages installed. All settings applied.\n\n" +
                    "REMAINING MANUAL STEPS:\n" +
                    "1. Switch Platform to Meta Quest\n" +
                    "2. Enable OpenXR in XR Plug-in Management\n" +
                    "3. Add Meta Quest Feature Group\n" +
                    "4. Setup scene with OVRCameraRig\n\n" +
                    "See docs/META_SDK_SETUP.md for full guide.",
                    "OK"
                );
                return;
            }

            var pkg = _installQueue.Dequeue();
            string identifier = $"{pkg[0]}@{pkg[1]}";
            _installedCount++;

            Debug.Log($"[PLAGA44] [{_installedCount}/{_totalPackages}] Installing {identifier}...");
            _addRequest = Client.Add(identifier);
        }

        private static void PackageInstallTick()
        {
            if (_addRequest == null || !_addRequest.IsCompleted) return;

            if (_addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[PLAGA44] OK: {_addRequest.Result.packageId}");
            }
            else if (_addRequest.Status >= StatusCode.Failure)
            {
                Debug.LogWarning($"[PLAGA44] FAILED: {_addRequest.Error?.message ?? "unknown error"}");
            }

            _addRequest = null;
            InstallNextPackage();
        }

        // ==================================================================
        // PLAYER SETTINGS
        // ==================================================================
        private static void SetPlayerSettings()
        {
            // Company and product
            PlayerSettings.companyName = "Cybernomad";
            PlayerSettings.productName = "PLAGA 44";

            // Color space must be Linear for VR
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // Android package identifier
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android, "com.cybernomad.plaga44");

            // Graphics API: Vulkan only (required for Quest)
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.Android,
                new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);

            // Scripting backend: IL2CPP (required for ARM64)
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            // Architecture: ARM64 only (Quest is ARM64)
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // API levels
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)32;

            // Orientation: Landscape Left (standard for VR)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            // Disable auto-rotation (VR handles orientation)
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            Debug.Log("[PLAGA44] Player Settings configured.");
        }

        // ==================================================================
        // QUALITY SETTINGS
        // ==================================================================
        private static void SetQualitySettings()
        {
            // Anti-Aliasing: 4x MSAA (minimum for VR comfort)
            QualitySettings.antiAliasing = 4;

            // Disable VSync -- Meta Quest runtime manages frame timing
            QualitySettings.vSyncCount = 0;

            // Anisotropic filtering: Force On
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;

            // Texture quality: Full resolution
            QualitySettings.globalTextureMipmapLimit = 0;

            // Shadow distance: conservative for mobile
            QualitySettings.shadowDistance = 20f;

            // LOD bias: slightly aggressive for mobile VR
            QualitySettings.lodBias = 1.0f;

            // Pixel light count: keep low for mobile
            QualitySettings.pixelLightCount = 2;

            Debug.Log("[PLAGA44] Quality Settings configured.");
        }

        // ==================================================================
        // DIAGNOSTICS
        // ==================================================================
        [MenuItem("PLAGA44/Print Setup Status")]
        public static void PrintSetupStatus()
        {
            Debug.Log("=== PLAGA '44 Setup Status ===");
            Debug.Log($"Color Space: {PlayerSettings.colorSpace}");
            Debug.Log($"Company: {PlayerSettings.companyName}");
            Debug.Log($"Product: {PlayerSettings.productName}");
            Debug.Log($"Android Package: {PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)}");
            Debug.Log($"Scripting Backend (Android): {PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)}");
            Debug.Log($"Target Architecture: {PlayerSettings.Android.targetArchitectures}");
            Debug.Log($"Min SDK: {PlayerSettings.Android.minSdkVersion}");
            Debug.Log($"Target SDK: {PlayerSettings.Android.targetSdkVersion}");
            Debug.Log($"Graphics APIs (Android): {string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android))}");
            Debug.Log($"MSAA: {QualitySettings.antiAliasing}x");
            Debug.Log($"VSync: {QualitySettings.vSyncCount}");
            Debug.Log($"Anisotropic: {QualitySettings.anisotropicFiltering}");
            Debug.Log($"Shadow Distance: {QualitySettings.shadowDistance}");
            Debug.Log($"LOD Bias: {QualitySettings.lodBias}");
            Debug.Log($"Pixel Lights: {QualitySettings.pixelLightCount}");
            Debug.Log("=== End Status ===");
        }

        // ==================================================================
        // MANUAL STEPS -- menu navigation helpers
        // Each opens the correct Unity window for that step.
        // ==================================================================
        [MenuItem("PLAGA44/Manual Steps/1. Switch Platform to Android", false, 100)]
        public static void ManualStep1_SwitchPlatform()
        {
            Debug.Log("[PLAGA44] Step 1: File > Build Profiles > Meta Quest > Switch Platform");
            Debug.Log("[PLAGA44] Select 'Android' platform, then click 'Switch Platform'.");
            EditorApplication.ExecuteMenuItem("File/Build Profiles");
        }

        [MenuItem("PLAGA44/Manual Steps/2. Enable OpenXR (XR Plug-in Management)", false, 101)]
        public static void ManualStep2_EnableOpenXR()
        {
            Debug.Log("[PLAGA44] Step 2: Enable OpenXR for Android");
            Debug.Log("[PLAGA44] In the Android tab, tick 'OpenXR'.");
            SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
        }

        [MenuItem("PLAGA44/Manual Steps/3. Add Meta Quest Feature Group", false, 102)]
        public static void ManualStep3_MetaQuestFeature()
        {
            Debug.Log("[PLAGA44] Step 3: Under OpenXR settings, enable Meta Quest Feature Group");
            Debug.Log("[PLAGA44] Click the Android tab > OpenXR > tick 'Meta Quest Feature Group'.");
            Debug.Log("[PLAGA44] Also add 'Oculus Touch Controller Profile' under Interaction Profiles.");
            SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
        }

        [MenuItem("PLAGA44/Manual Steps/4. Add OVRCameraRig to Scene", false, 103)]
        public static void ManualStep4_SceneSetup()
        {
            Debug.Log("[PLAGA44] Step 4: Scene setup");
            Debug.Log("[PLAGA44] 1. Delete the default 'Main Camera' from your scene");
            Debug.Log("[PLAGA44] 2. Meta > Tools > Building Blocks > (+) Camera Rig");
            Debug.Log("[PLAGA44] 3. (+) Controller Tracking");
            Debug.Log("[PLAGA44] 4. Optionally: (+) Hand Tracking, (+) Passthrough");

            EditorUtility.DisplayDialog(
                "PLAGA '44 -- Scene Setup",
                "In your scene:\n\n" +
                "1. Delete 'Main Camera'\n" +
                "2. Menu: Meta > Tools > Building Blocks\n" +
                "3. Add: Camera Rig\n" +
                "4. Add: Controller Tracking\n" +
                "5. Optional: Hand Tracking, Passthrough\n\n" +
                "See docs/META_SDK_SETUP.md for details.",
                "OK"
            );
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
