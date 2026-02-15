// MetaQuestSetup.cs
// PLAGA '44 -- Step-by-step Meta Quest project setup (7 steps).
//
// Menu: CYBERNOMAD > Meta SDK Setup > Step 1..7
//
// Each step does ONE thing, validates that the previous step was completed,
// logs the result, and tells you what to run next.
//
// WHY 7 steps instead of 1:
// Adding HAS_META_XR scripting define BEFORE the editor restart causes
// recompilation while Meta SDK types aren't fully loaded -> build errors.
// Changing Input Handler to "Both" requires an editor restart.
// Splitting into steps ensures each operation completes cleanly.
//
// Steps:
//   1. Add Meta Registry           -- only manifest.json
//   2. Install Packages            -- async queue, nothing else
//   3. Configure Project Settings  -- player + quality + input handler
//   4. Restart Editor              -- required after input handler change
//   5. Add Scripting Defines       -- HAS_META_XR (now safe: packages loaded)
//   6. Switch to Android + XR      -- platform switch + OpenXR guidance
//   7. Setup VR Scene              -- OVRCameraRig + controllers
//
// Installs Meta XR SDK v81 (v83 has license bug on Unity 6.3).
// Bug: https://communityforums.atmeta.com/discussions/Questions_Discussions/
//      unity-6-3---meta-xr-core-license-error/1357387
//
// See also: MetaQuestReset.cs for reverting to Unity defaults.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
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

        // Package install queue (async state)
        private static Queue<string[]> _installQueue;
        private static AddRequest _addRequest;
        private static int _totalPackages;
        private static int _installedCount;

        private const string LOG = "[PLAGA44]";
        private const int MENU_BASE = 100;

        // ==================================================================
        // STEP 1: Add Meta Registry
        // Only touches Packages/manifest.json -- adds scoped registry.
        // No prerequisites.
        // ==================================================================
        [MenuItem("CYBERNOMAD/Meta SDK Setup/Step 1 -- Add Meta Registry", false, MENU_BASE)]
        public static void Step1_AddRegistry()
        {
            LogHeader("Step 1: Add Meta Registry");

            EnsureScopedRegistry();

            Debug.Log($"{LOG} Step 1 DONE.");
            Debug.Log($"{LOG} Next: CYBERNOMAD > Meta SDK Setup > Step 2 -- Install Packages");
        }

        // ==================================================================
        // STEP 2: Install Packages
        // Async package install queue. Does NOT change any settings.
        // Prerequisite: Step 1 (registry in manifest.json).
        // ==================================================================
        [MenuItem("CYBERNOMAD/Meta SDK Setup/Step 2 -- Install Packages", false, MENU_BASE + 1)]
        public static void Step2_InstallPackages()
        {
            if (!HasScopedRegistry())
            {
                ShowBlocked(2, "Run Step 1 first (Add Meta Registry).",
                    "Meta XR scoped registry not found in manifest.json.");
                return;
            }

            LogHeader("Step 2: Install Packages");

            string packageList = "";
            foreach (var pkg in PackagesToInstall)
                packageList += $"  {pkg[0]} @ {pkg[1]}\n";

            bool confirm = EditorUtility.DisplayDialog(
                "Step 2: Install Packages",
                "Will install these packages:\n\n" + packageList +
                "\nThis runs in the background. Watch Console for progress.",
                "Install",
                "Cancel"
            );

            if (!confirm) return;

            StartPackageInstall();
            // Completion message is in OnAllPackagesInstalled()
        }

        // ==================================================================
        // STEP 3: Configure Project Settings
        // Player Settings + Quality Settings + Input Handler -> Both.
        // Prerequisite: Step 2 (packages in manifest).
        // ==================================================================
        [MenuItem("CYBERNOMAD/Meta SDK Setup/Step 3 -- Configure Project Settings", false, MENU_BASE + 2)]
        public static void Step3_ConfigureSettings()
        {
            if (!ArePackagesInManifest())
            {
                ShowBlocked(3, "Run Step 2 first (Install Packages).",
                    "Required packages not found in manifest.json.\n" +
                    "Wait for Step 2 to finish installing all packages.");
                return;
            }

            LogHeader("Step 3: Configure Project Settings");

            SetPlayerSettings();
            SetQualitySettings();
            SetActiveInputHandlerBoth();

            Debug.Log($"{LOG} Step 3 DONE.");
            Debug.Log($"{LOG} Input Handler changed to 'Both' -- RESTART REQUIRED.");
            Debug.Log($"{LOG} Next: CYBERNOMAD > Meta SDK Setup > Step 4 -- Restart Editor");

            EditorUtility.DisplayDialog(
                "Step 3 Done -- Restart Needed",
                "Player Settings, Quality Settings, Input Handler configured.\n\n" +
                "Input Handler was set to 'Both'.\n" +
                "You MUST restart the editor before continuing.\n\n" +
                "Next: Step 4 -- Restart Editor",
                "OK");
        }

        // ==================================================================
        // STEP 4: Restart Editor
        // Required after Input Handler change in Step 3.
        // No validation -- always safe to restart.
        // ==================================================================
        [MenuItem("CYBERNOMAD/Meta SDK Setup/Step 4 -- Restart Editor", false, MENU_BASE + 3)]
        public static void Step4_RestartEditor()
        {
            bool confirm = EditorUtility.DisplayDialog(
                "Step 4: Restart Editor",
                "This will save all assets and restart the Unity Editor.\n\n" +
                "Required after Input Handler change in Step 3.\n\n" +
                "After restart, run:\n" +
                "CYBERNOMAD > Meta SDK Setup > Step 5 -- Add Scripting Defines",
                "Restart Now",
                "Cancel");

            if (!confirm) return;

            LogHeader("Step 4: Restarting Editor...");
            Debug.Log($"{LOG} After restart: CYBERNOMAD > Meta SDK Setup > Step 5");

            AssetDatabase.SaveAssets();
            EditorApplication.OpenProject(GetProjectPath());
        }

        // ==================================================================
        // STEP 5: Add Scripting Defines
        // Adds HAS_META_XR. Safe because packages are loaded after restart.
        // Prerequisite: Input Handler == Both (Step 3 + restart done).
        // ==================================================================
        [MenuItem("CYBERNOMAD/Meta SDK Setup/Step 5 -- Add Scripting Defines", false, MENU_BASE + 4)]
        public static void Step5_AddDefines()
        {
            if (!IsInputHandlerBoth())
            {
                ShowBlocked(5,
                    "Run Steps 3 and 4 first.",
                    "Input Handler must be 'Both' (Step 3) and editor restarted (Step 4).\n" +
                    "Current Input Handler value: " + GetActiveInputHandler());
                return;
            }

            LogHeader("Step 5: Add Scripting Defines");

            EnsureScriptingDefine("HAS_META_XR");

            Debug.Log($"{LOG} Step 5 DONE. Scripts will recompile.");
            Debug.Log($"{LOG} Wait for recompilation to finish.");
            Debug.Log($"{LOG} Next: CYBERNOMAD > Meta SDK Setup > Step 6 -- Switch to Android + XR");

            EditorUtility.DisplayDialog(
                "Step 5 Done",
                "HAS_META_XR scripting define added.\n\n" +
                "Unity will recompile scripts. Wait for it to finish.\n\n" +
                "Next: Step 6 -- Switch to Android + XR",
                "OK");
        }

        // ==================================================================
        // STEP 6: Switch to Android + XR
        // Platform switch to Android + guidance for OpenXR setup.
        // Prerequisite: HAS_META_XR defined (Step 5).
        // ==================================================================
        [MenuItem("CYBERNOMAD/Meta SDK Setup/Step 6 -- Switch to Android + XR", false, MENU_BASE + 5)]
        public static void Step6_SwitchToAndroidXR()
        {
            if (!HasScriptingDefine("HAS_META_XR"))
            {
                ShowBlocked(6, "Run Step 5 first (Add Scripting Defines).",
                    "HAS_META_XR must be in scripting defines.");
                return;
            }

            LogHeader("Step 6: Switch to Android + XR");

            // --- Platform switch ---
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log($"{LOG} Switching build target to Android...");
                Debug.Log($"{LOG} This may take a moment (asset reimport).");
                EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android, BuildTarget.Android);
                Debug.Log($"{LOG} Platform switched to Android.");
            }
            else
            {
                Debug.Log($"{LOG} Already on Android platform.");
            }

            // --- XR Plugin Management ---
            // Auto-setup of OpenXR loader via reflection is fragile.
            // Better to guide the user through the 3 clicks it takes.
            Debug.Log($"{LOG} Step 6 DONE. Platform is Android.");
            Debug.Log($"{LOG}");
            PrintManualXRSteps();

            Debug.Log($"{LOG} Next: CYBERNOMAD > Meta SDK Setup > Step 7 -- Setup VR Scene");

            EditorUtility.DisplayDialog(
                "Step 6 Done -- Configure XR",
                "Platform switched to Android.\n\n" +
                "NOW configure XR manually (3 clicks):\n\n" +
                "1. Edit > Project Settings > XR Plug-in Management\n" +
                "   -> Install XR Plugin Management (if needed)\n" +
                "2. Android tab > Enable 'OpenXR'\n" +
                "3. OpenXR > Meta Quest Feature Group > Enable\n\n" +
                "After that: Step 7 -- Setup VR Scene",
                "OK");
        }

        // ==================================================================
        // STEP 7: Setup VR Scene
        // Adds OVRCameraRig and controllers to current scene.
        // Prerequisite: Android platform (Step 6).
        // ==================================================================
        [MenuItem("CYBERNOMAD/Meta SDK Setup/Step 7 -- Setup VR Scene", false, MENU_BASE + 6)]
        public static void Step7_SetupVRScene()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                ShowBlocked(7, "Run Step 6 first (Switch to Android + XR).",
                    "Build target must be Android.\n" +
                    "Current target: " + EditorUserBuildSettings.activeBuildTarget);
                return;
            }

            LogHeader("Step 7: Setup VR Scene");

            SetupVRSceneObjects();

            LogFooter("ALL 7 STEPS COMPLETE");
            Debug.Log($"{LOG} Build and deploy to Meta Quest to test.");
        }

        // ==================================================================
        // DIAGNOSTICS
        // ==================================================================
        [MenuItem("CYBERNOMAD/Meta SDK Setup/Print Setup Status", false, MENU_BASE + 20)]
        public static void PrintSetupStatus()
        {
            Debug.Log($"{LOG} === PLAGA '44 Setup Status ===");
            Debug.Log($"{LOG} [Step 1] Registry in manifest:  {HasScopedRegistry()}");
            Debug.Log($"{LOG} [Step 2] Packages in manifest:  {ArePackagesInManifest()}");
            Debug.Log($"{LOG} [Step 3] Input Handler (0=Old, 1=New, 2=Both): {GetActiveInputHandler()}");
            Debug.Log($"{LOG} [Step 5] HAS_META_XR defined:   {HasScriptingDefine("HAS_META_XR")}");
            Debug.Log($"{LOG} [Step 6] Build target:          {EditorUserBuildSettings.activeBuildTarget}");
            Debug.Log($"{LOG} ---");
            Debug.Log($"{LOG} Color Space: {PlayerSettings.colorSpace}");
            Debug.Log($"{LOG} Company: {PlayerSettings.companyName}");
            Debug.Log($"{LOG} Product: {PlayerSettings.productName}");
            Debug.Log($"{LOG} Android Package: {PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)}");
            Debug.Log($"{LOG} Scripting Backend (Android): {PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android)}");
            Debug.Log($"{LOG} Target Architecture: {PlayerSettings.Android.targetArchitectures}");
            Debug.Log($"{LOG} Min SDK: {PlayerSettings.Android.minSdkVersion}");
            Debug.Log($"{LOG} Target SDK: {PlayerSettings.Android.targetSdkVersion}");
            Debug.Log($"{LOG} Graphics APIs (Android): {string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android))}");
            Debug.Log($"{LOG} MSAA: {QualitySettings.antiAliasing}x");
            Debug.Log($"{LOG} VSync: {QualitySettings.vSyncCount}");
            Debug.Log($"{LOG} Anisotropic: {QualitySettings.anisotropicFiltering}");
            Debug.Log($"{LOG} Shadow Distance: {QualitySettings.shadowDistance}");
            Debug.Log($"{LOG} LOD Bias: {QualitySettings.lodBias}");
            Debug.Log($"{LOG} Pixel Lights: {QualitySettings.pixelLightCount}");
            Debug.Log($"{LOG} === End Status ===");
        }

        // ==================================================================
        // VALIDATION HELPERS
        // ==================================================================

        private static bool HasScopedRegistry()
        {
            string manifestPath = GetManifestPath();
            if (manifestPath == null) return false;
            return File.ReadAllText(manifestPath).Contains("npm.developer.oculus.com");
        }

        private static bool ArePackagesInManifest()
        {
            string manifestPath = GetManifestPath();
            if (manifestPath == null) return false;
            string manifest = File.ReadAllText(manifestPath);
            // Check key packages -- if these are there, the rest should be too
            return manifest.Contains("com.unity.xr.openxr")
                && manifest.Contains("com.meta.xr.sdk.core");
        }

        private static int GetActiveInputHandler()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return -1;
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            return prop?.intValue ?? -1;
        }

        private static bool IsInputHandlerBoth()
        {
            return GetActiveInputHandler() == 2;
        }

        private static bool HasScriptingDefine(string define)
        {
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
            return defines.Split(';').Any(d => d.Trim() == define);
        }

        private static void ShowBlocked(int step, string reason, string detail)
        {
            Debug.LogError($"{LOG} Step {step} BLOCKED: {reason}");
            EditorUtility.DisplayDialog(
                $"Step {step} Blocked",
                $"{reason}\n\n{detail}",
                "OK");
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
                Debug.Log($"{LOG} Scoped registry already present.");
                return;
            }

            // Insert scopedRegistries before "dependencies"
            int depsIdx = manifest.IndexOf("\"dependencies\"");
            if (depsIdx < 0)
            {
                Debug.LogError($"{LOG} Cannot find 'dependencies' in manifest.json");
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
            Debug.Log($"{LOG} Added Meta XR scoped registry to manifest.json.");
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
                OnAllPackagesInstalled();
                return;
            }

            var pkg = _installQueue.Dequeue();
            string identifier = $"{pkg[0]}@{pkg[1]}";
            _installedCount++;

            Debug.Log($"{LOG} [{_installedCount}/{_totalPackages}] Installing {identifier}...");
            _addRequest = Client.Add(identifier);
        }

        private static void PackageInstallTick()
        {
            if (_addRequest == null || !_addRequest.IsCompleted) return;

            if (_addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"{LOG} OK: {_addRequest.Result.packageId}");
            }
            else if (_addRequest.Status >= StatusCode.Failure)
            {
                Debug.LogWarning($"{LOG} FAILED: {_addRequest.Error?.message ?? "unknown error"}");
            }

            _addRequest = null;
            InstallNextPackage();
        }

        private static void OnAllPackagesInstalled()
        {
            Debug.Log($"{LOG} All {_totalPackages} packages processed.");
            Debug.Log($"{LOG} Step 2 DONE.");
            Debug.Log($"{LOG} Next: CYBERNOMAD > Meta SDK Setup > Step 3 -- Configure Project Settings");

            EditorUtility.DisplayDialog(
                "Step 2 Done",
                $"All {_totalPackages} packages installed.\n\n" +
                "Next: Step 3 -- Configure Project Settings",
                "OK");
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
                BuildTargetGroup.Android, "com.cybernomad.plaga44");

            // Graphics API: Vulkan only (required for Quest)
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.Android,
                new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);

            // Scripting backend: IL2CPP (required for ARM64)
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

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

            Debug.Log($"{LOG} Player Settings configured.");
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

            Debug.Log($"{LOG} Quality Settings configured.");
        }

        // ==================================================================
        // INPUT HANDLER -- set to Both (Old + New Input System)
        // ==================================================================
        private static void SetActiveInputHandlerBoth()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError($"{LOG} Cannot load ProjectSettings.asset");
                return;
            }

            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null)
            {
                Debug.LogError($"{LOG} Cannot find activeInputHandler property in ProjectSettings");
                return;
            }

            if (prop.intValue == 2)
            {
                Debug.Log($"{LOG} Input Handler already set to 'Both'.");
                return;
            }

            prop.intValue = 2; // 0 = Old (Input Manager), 1 = New (Input System), 2 = Both
            so.ApplyModifiedProperties();
            Debug.Log($"{LOG} Input Handler set to 'Both'. Editor restart required.");
        }

        // ==================================================================
        // SCRIPTING DEFINES
        // ==================================================================
        private static void EnsureScriptingDefine(string define)
        {
            var groups = new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone };
            foreach (var group in groups)
            {
                string current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
                var defines = current.Split(';')
                    .Select(d => d.Trim())
                    .Where(d => !string.IsNullOrEmpty(d))
                    .ToList();

                if (defines.Contains(define))
                {
                    Debug.Log($"{LOG} {define} already defined for {group}.");
                    continue;
                }

                defines.Add(define);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", defines));
                Debug.Log($"{LOG} Added {define} to {group} scripting defines.");
            }
        }

        // ==================================================================
        // XR SETUP GUIDANCE
        // ==================================================================
        private static void PrintManualXRSteps()
        {
            Debug.Log($"{LOG} --- XR Setup (manual -- 3 clicks) ---");
            Debug.Log($"{LOG} 1. Edit > Project Settings > XR Plug-in Management");
            Debug.Log($"{LOG}    -> Click 'Install XR Plugin Management' if prompted");
            Debug.Log($"{LOG} 2. Android tab > Enable 'OpenXR'");
            Debug.Log($"{LOG} 3. OpenXR > Enabled Interaction Profiles > Meta Quest Touch Pro");
            Debug.Log($"{LOG}    OpenXR > OpenXR Feature Groups > Meta Quest (enable)");
            Debug.Log($"{LOG} ---");
        }

        // ==================================================================
        // VR SCENE SETUP
        // ==================================================================
        private static void SetupVRSceneObjects()
        {
#if HAS_META_XR
            SetupVRSceneWithMetaSDK();
#else
            Debug.LogWarning($"{LOG} HAS_META_XR not defined -- cannot auto-setup VR scene.");
            Debug.Log($"{LOG} If you just ran Step 5, wait for recompilation and try again.");
            PrintManualVRSceneSteps();

            EditorUtility.DisplayDialog(
                "Step 7 -- Manual Setup Required",
                "HAS_META_XR is not active in this compilation.\n\n" +
                "If you just ran Step 5, wait for scripts to recompile,\n" +
                "then run Step 7 again.\n\n" +
                "Or setup manually:\n" +
                "1. Delete default Main Camera\n" +
                "2. Meta > Tools > Building Blocks > Camera Rig\n" +
                "3. Meta > Tools > Building Blocks > Controller Tracking",
                "OK");
#endif
        }

        private static void PrintManualVRSceneSteps()
        {
            Debug.Log($"{LOG} --- Manual VR Scene Setup ---");
            Debug.Log($"{LOG} 1. Delete the default 'Main Camera' from the scene");
            Debug.Log($"{LOG} 2. Meta > Tools > Building Blocks > Camera Rig");
            Debug.Log($"{LOG} 3. Meta > Tools > Building Blocks > Controller Tracking");
            Debug.Log($"{LOG} ---");
        }

#if HAS_META_XR
        private static void SetupVRSceneWithMetaSDK()
        {
            // Remove default Main Camera if present
            var mainCam = Camera.main;
            if (mainCam != null && mainCam.gameObject.name == "Main Camera")
            {
                Debug.Log($"{LOG} Removing default Main Camera.");
                Undo.DestroyObjectImmediate(mainCam.gameObject);
            }

            // Find OVRCameraRig prefab in Meta SDK package
            string[] prefabGuids = AssetDatabase.FindAssets("OVRCameraRig t:Prefab");
            if (prefabGuids.Length > 0)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[0]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instance.transform.position = Vector3.zero;
                    Undo.RegisterCreatedObjectUndo(instance, "Add OVRCameraRig");
                    Debug.Log($"{LOG} Added OVRCameraRig to scene from {prefabPath}.");
                }
                else
                {
                    Debug.LogWarning($"{LOG} Found OVRCameraRig GUID but could not load prefab.");
                    PrintManualVRSceneSteps();
                }
            }
            else
            {
                Debug.LogWarning($"{LOG} OVRCameraRig prefab not found in project.");
                Debug.Log($"{LOG} Add manually via Meta > Tools > Building Blocks > Camera Rig");
            }

            // Controllers -- Building Blocks is the best approach
            Debug.Log($"{LOG} For controllers, use:");
            Debug.Log($"{LOG}   Meta > Tools > Building Blocks > Controller Tracking");

            EditorUtility.DisplayDialog(
                "Step 7 Done -- Setup Complete!",
                "VR scene configured.\n\n" +
                "For controllers, use:\n" +
                "Meta > Tools > Building Blocks > Controller Tracking\n\n" +
                "Build and deploy to Meta Quest to test!",
                "OK");
        }
#endif

        // ==================================================================
        // LOGGING HELPERS
        // ==================================================================
        private static void LogHeader(string title)
        {
            Debug.Log($"{LOG} ============================================");
            Debug.Log($"{LOG} {title}");
            Debug.Log($"{LOG} ============================================");
        }

        private static void LogFooter(string title)
        {
            Debug.Log($"{LOG} ============================================");
            Debug.Log($"{LOG} {title}");
            Debug.Log($"{LOG} ============================================");
        }

        // ==================================================================
        // PATH HELPERS
        // ==================================================================
        private static string GetProjectPath()
        {
            return Path.GetDirectoryName(Application.dataPath);
        }

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
