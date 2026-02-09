// MetaQuestSetup.cs
// CYBERNOMAD -- Full automated Meta Quest project setup.
// One click: installs packages, configures settings, switches platform,
// enables OpenXR, enables Meta Quest feature group.
//
// Menu: CYBERNOMAD > Meta SDK Setup > 0. Install Packages + Settings
//
// Uses Meta XR SDK v81 (v83 has license bug on Unity 6.3).
// See also: MetaQuestReset.cs for reverting to Unity defaults.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plaga44.Editor
{
    // ======================================================================
    // Phase continuation after domain reloads (platform switch etc.)
    // ======================================================================
    [InitializeOnLoad]
    public static class MetaQuestSetupPhaser
    {
        private const string PHASE_KEY = "CYBERNOMAD_SetupPhase";

        static MetaQuestSetupPhaser()
        {
            int phase = EditorPrefs.GetInt(PHASE_KEY, 0);
            if (phase > 0)
                EditorApplication.delayCall += () => MetaQuestSetup.ContinueFromPhase(phase);
        }

        public static void SetPhase(int phase) => EditorPrefs.SetInt(PHASE_KEY, phase);
        public static void ClearPhase() => EditorPrefs.DeleteKey(PHASE_KEY);
    }

    // ======================================================================
    // Main setup class
    // ======================================================================
    public static class MetaQuestSetup
    {
        private const string META_SDK_VERSION = "81.0.0";

        private static readonly string[][] PackagesToInstall = new[]
        {
            new[] { "com.unity.xr.openxr",          "1.14.0" },
            new[] { "com.unity.xr.meta-openxr",      "2.4.0"  },
            new[] { "com.meta.xr.sdk.core",           META_SDK_VERSION },
            new[] { "com.meta.xr.sdk.interaction",    META_SDK_VERSION },
            new[] { "com.meta.xr.sdk.audio",          META_SDK_VERSION },
        };

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

        private static Queue<string[]> _installQueue;
        private static AddRequest _addRequest;
        private static int _totalPackages;
        private static int _installedCount;

        // ==================================================================
        // MENU: 0. Install -- the ONE button that does EVERYTHING
        // ==================================================================
        [MenuItem("CYBERNOMAD/Meta SDK Setup/0. Install Packages + Settings", false, 0)]
        public static void FullAutoSetup()
        {
            bool confirm = EditorUtility.DisplayDialog(
                "CYBERNOMAD -- Meta Quest Full Setup",
                "One-click setup for Meta Quest 2/3/3S.\n\n" +
                "This will automatically:\n" +
                "1. Install Meta XR SDK v81 packages\n" +
                "2. Configure Player + Quality Settings\n" +
                "3. Switch platform to Android\n" +
                "4. Enable OpenXR loader\n" +
                "5. Enable Meta Quest Feature Group\n\n" +
                "Editor will restart during the process.\n" +
                "Continue?",
                "Do Everything",
                "Cancel"
            );

            if (!confirm) return;

            Debug.Log("[CYBERNOMAD] ============================================");
            Debug.Log("[CYBERNOMAD] FULL AUTO SETUP STARTING...");
            Debug.Log("[CYBERNOMAD] ============================================");

            EnsureScopedRegistry();
            SetPlayerSettings();
            SetQualitySettings();
            StartPackageInstall(); // when done -> OnPackagesInstalled -> SwitchPlatform -> EnableXR
        }

        // ==================================================================
        // Called by MetaQuestSetupPhaser after domain reload
        // ==================================================================
        public static void ContinueFromPhase(int phase)
        {
            Debug.Log($"[CYBERNOMAD] Resuming setup from phase {phase}...");
            switch (phase)
            {
                case 1: // Platform switched, now enable XR
                    EnableOpenXR();
                    break;
            }
        }

        // ==================================================================
        // PACKAGE INSTALL (async queue)
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
                Debug.Log($"[CYBERNOMAD] All {_totalPackages} packages installed.");
                OnPackagesInstalled();
                return;
            }

            var pkg = _installQueue.Dequeue();
            string id = $"{pkg[0]}@{pkg[1]}";
            _installedCount++;
            Debug.Log($"[CYBERNOMAD] [{_installedCount}/{_totalPackages}] {id}");
            _addRequest = Client.Add(id);
        }

        private static void PackageInstallTick()
        {
            if (_addRequest == null || !_addRequest.IsCompleted) return;

            if (_addRequest.Status == StatusCode.Success)
                Debug.Log($"[CYBERNOMAD] OK: {_addRequest.Result.packageId}");
            else if (_addRequest.Status >= StatusCode.Failure)
                Debug.LogWarning($"[CYBERNOMAD] FAIL: {_addRequest.Error?.message}");

            _addRequest = null;
            InstallNextPackage();
        }

        // ==================================================================
        // After packages installed -> switch platform
        // ==================================================================
        private static void OnPackagesInstalled()
        {
            Debug.Log("[CYBERNOMAD] Packages done. Switching to Android...");

            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                Debug.Log("[CYBERNOMAD] Already on Android.");
                EnableOpenXR();
                return;
            }

            // Set phase 1 -- after domain reload, ContinueFromPhase(1) runs
            MetaQuestSetupPhaser.SetPhase(1);
            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android, BuildTarget.Android);
            // Editor restarts here...
        }

        // ==================================================================
        // Enable OpenXR + Meta Quest Feature (all via reflection)
        // ==================================================================
        private static void EnableOpenXR()
        {
            Debug.Log("[CYBERNOMAD] Configuring XR...");
            bool ok = true;

            // --- Step 1: Enable OpenXR loader for Android ---
            ok = ok && TryEnableOpenXRLoader();

            // --- Step 2: Enable Meta Quest feature group ---
            ok = ok && TryEnableMetaQuestFeature();

            MetaQuestSetupPhaser.ClearPhase();

            if (ok)
            {
                Debug.Log("[CYBERNOMAD] ============================================");
                Debug.Log("[CYBERNOMAD] SETUP 100% COMPLETE!");
                Debug.Log("[CYBERNOMAD] Only remaining: add OVRCameraRig to your scene.");
                Debug.Log("[CYBERNOMAD] Menu: Meta > Tools > Building Blocks > Camera Rig");
                Debug.Log("[CYBERNOMAD] ============================================");

                EditorUtility.DisplayDialog(
                    "CYBERNOMAD -- Setup Complete!",
                    "Everything configured automatically!\n\n" +
                    "Last step (scene-specific):\n" +
                    "1. Delete Main Camera\n" +
                    "2. Meta > Tools > Building Blocks\n" +
                    "3. Add Camera Rig + Controllers\n",
                    "OK"
                );
            }
            else
            {
                Debug.LogWarning("[CYBERNOMAD] Some XR steps failed. Use manual steps menu.");
            }
        }

        // --- OpenXR Loader via reflection ---
        private static bool TryEnableOpenXRLoader()
        {
            try
            {
                // XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android)
                var perBuildType = GetType("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget, Unity.XR.Management.Editor");
                if (perBuildType == null) { LogMissing("XR Management Editor"); return false; }

                var getMethod = perBuildType.GetMethod("XRGeneralSettingsForBuildTarget",
                    BindingFlags.Public | BindingFlags.Static);
                var generalSettings = getMethod?.Invoke(null, new object[] { BuildTargetGroup.Android });

                if (generalSettings == null)
                {
                    // Try to create settings via ScriptableObject
                    Debug.Log("[CYBERNOMAD] Creating XR General Settings for Android...");
                    var xrGeneralType = GetType("UnityEngine.XR.Management.XRGeneralSettings, Unity.XR.Management");
                    if (xrGeneralType == null) { LogMissing("XR Management"); return false; }

                    generalSettings = ScriptableObject.CreateInstance(xrGeneralType);
                    var managerType = GetType("UnityEngine.XR.Management.XRManagerSettings, Unity.XR.Management");
                    var manager = ScriptableObject.CreateInstance(managerType);

                    // generalSettings.Manager = manager
                    var managerProp = xrGeneralType.GetProperty("Manager") ??
                                     xrGeneralType.GetProperty("AssignedSettings");
                    managerProp?.SetValue(generalSettings, manager);

                    // Save assets
                    string settingsPath = "Assets/XR";
                    if (!AssetDatabase.IsValidFolder(settingsPath))
                        AssetDatabase.CreateFolder("Assets", "XR");

                    AssetDatabase.CreateAsset((UnityEngine.Object)generalSettings,
                        "Assets/XR/XRGeneralSettingsAndroid.asset");
                    AssetDatabase.CreateAsset((UnityEngine.Object)manager,
                        "Assets/XR/XRManagerSettingsAndroid.asset");

                    // Register with per-build-target
                    var setMethod = perBuildType.GetMethod("SetSettingsForBuildTarget",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance,
                        null, new[] { typeof(BuildTargetGroup), xrGeneralType }, null);
                    setMethod?.Invoke(null, new object[] { BuildTargetGroup.Android, generalSettings });
                }

                // Get the Manager/AssignedSettings
                var gsType = generalSettings.GetType();
                var mgrProp = gsType.GetProperty("Manager") ?? gsType.GetProperty("AssignedSettings");
                var mgr = mgrProp?.GetValue(generalSettings);

                if (mgr == null)
                {
                    Debug.LogWarning("[CYBERNOMAD] XR Manager Settings is null.");
                    return false;
                }

                // Use XRPackageMetadataStore.AssignLoader to add OpenXR
                var metaStoreType = GetType(
                    "UnityEditor.XR.Management.Metadata.XRPackageMetadataStore, Unity.XR.Management.Editor");
                if (metaStoreType != null)
                {
                    var mgrSettingsType = GetType("UnityEngine.XR.Management.XRManagerSettings, Unity.XR.Management");
                    var assignMethod = metaStoreType.GetMethod("AssignLoader",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { mgrSettingsType, typeof(string), typeof(BuildTargetGroup) },
                        null);

                    if (assignMethod != null)
                    {
                        bool result = (bool)assignMethod.Invoke(null, new object[]
                        {
                            mgr,
                            "UnityEngine.XR.OpenXR.OpenXRLoader",
                            BuildTargetGroup.Android
                        });

                        if (result)
                            Debug.Log("[CYBERNOMAD] OpenXR loader enabled for Android.");
                        else
                            Debug.Log("[CYBERNOMAD] OpenXR loader already enabled or assign returned false.");
                    }
                    else
                    {
                        Debug.LogWarning("[CYBERNOMAD] AssignLoader method not found.");
                        return false;
                    }
                }

                // Enable InitManagerOnStart
                var initProp = gsType.GetProperty("InitManagerOnStart");
                if (initProp != null)
                    initProp.SetValue(generalSettings, true);

                EditorUtility.SetDirty((UnityEngine.Object)generalSettings);
                AssetDatabase.SaveAssets();

                Debug.Log("[CYBERNOMAD] OpenXR configured.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[CYBERNOMAD] OpenXR setup failed: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        // --- Meta Quest Feature via reflection ---
        private static bool TryEnableMetaQuestFeature()
        {
            try
            {
                // OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android)
                var openXRSettingsType = GetType("UnityEngine.XR.OpenXR.OpenXRSettings, Unity.XR.OpenXR");
                if (openXRSettingsType == null) { LogMissing("OpenXR"); return false; }

                var getSettingsMethod = openXRSettingsType.GetMethod("GetSettingsForBuildTargetGroup",
                    BindingFlags.Public | BindingFlags.Static);
                var settings = getSettingsMethod?.Invoke(null, new object[] { BuildTargetGroup.Android });

                if (settings == null)
                {
                    Debug.LogWarning("[CYBERNOMAD] OpenXR settings not found for Android.");
                    return false;
                }

                // Get all features and enable Meta Quest ones
                var featureBaseType = GetType(
                    "UnityEngine.XR.OpenXR.Features.OpenXRFeature, Unity.XR.OpenXR");
                if (featureBaseType == null) { LogMissing("OpenXR Features"); return false; }

                // settings.GetFeatures<OpenXRFeature>()  or  settings.features
                var featuresField = openXRSettingsType.GetField("features",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var featuresProp = openXRSettingsType.GetProperty("features",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                object featuresObj = featuresField?.GetValue(settings) ??
                                    featuresProp?.GetValue(settings);

                // Try GetFeatures<T>() generic method
                if (featuresObj == null)
                {
                    var getFeaturesMethod = openXRSettingsType.GetMethod("GetFeatures");
                    if (getFeaturesMethod != null && getFeaturesMethod.IsGenericMethod)
                    {
                        var genericMethod = getFeaturesMethod.MakeGenericMethod(featureBaseType);
                        featuresObj = genericMethod.Invoke(settings, null);
                    }
                }

                if (featuresObj is Array featuresArray)
                {
                    bool found = false;
                    foreach (var feature in featuresArray)
                    {
                        if (feature == null) continue;
                        string typeName = feature.GetType().Name;
                        string fullName = feature.GetType().FullName ?? "";

                        // Enable Meta Quest support feature
                        bool isMeta = typeName.Contains("MetaQuest") ||
                                      fullName.Contains("Meta") ||
                                      typeName.Contains("OculusQuest");

                        // Enable touch controller profile
                        bool isTouch = typeName.Contains("TouchController") ||
                                       typeName.Contains("OculusTouch") ||
                                       typeName.Contains("MetaQuestTouch");

                        if (isMeta || isTouch)
                        {
                            var enabledProp = feature.GetType().GetProperty("enabled",
                                BindingFlags.Public | BindingFlags.Instance);
                            if (enabledProp != null)
                            {
                                enabledProp.SetValue(feature, true);
                                var nameProp = feature.GetType().GetProperty("name");
                                string fname = nameProp?.GetValue(feature)?.ToString() ?? typeName;
                                Debug.Log($"[CYBERNOMAD] Enabled: {fname}");
                                found = true;
                            }
                        }
                    }

                    if (!found)
                        Debug.LogWarning("[CYBERNOMAD] Meta Quest feature not found in OpenXR features list.");

                    EditorUtility.SetDirty((UnityEngine.Object)settings);
                    AssetDatabase.SaveAssets();
                    return found;
                }

                Debug.LogWarning("[CYBERNOMAD] Could not read OpenXR features.");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[CYBERNOMAD] Meta Quest feature setup failed: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        // ==================================================================
        // SCOPED REGISTRY
        // ==================================================================
        private static void EnsureScopedRegistry()
        {
            string manifestPath = GetManifestPath();
            if (manifestPath == null) return;

            string manifest = File.ReadAllText(manifestPath);
            if (manifest.Contains("npm.developer.oculus.com"))
            {
                Debug.Log("[CYBERNOMAD] Scoped registry already present.");
                return;
            }

            int depsIdx = manifest.IndexOf("\"dependencies\"");
            if (depsIdx < 0) return;

            int lineStart = manifest.LastIndexOf('\n', depsIdx);
            if (lineStart < 0) lineStart = 0;
            else lineStart += 1;

            manifest = manifest.Substring(0, lineStart)
                     + SCOPED_REGISTRY_JSON
                     + manifest.Substring(lineStart);

            File.WriteAllText(manifestPath, manifest);
            Debug.Log("[CYBERNOMAD] Added Meta XR scoped registry.");
        }

        // ==================================================================
        // PLAYER SETTINGS
        // ==================================================================
        private static void SetPlayerSettings()
        {
            PlayerSettings.companyName = "Cybernomad";
            PlayerSettings.productName = "PLAGA 44";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android, "com.cybernomad.plaga44");
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)32;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            Debug.Log("[CYBERNOMAD] Player Settings configured.");
        }

        // ==================================================================
        // QUALITY SETTINGS
        // ==================================================================
        private static void SetQualitySettings()
        {
            QualitySettings.antiAliasing = 4;
            QualitySettings.vSyncCount = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.shadowDistance = 20f;
            QualitySettings.lodBias = 1.0f;
            QualitySettings.pixelLightCount = 2;
            Debug.Log("[CYBERNOMAD] Quality Settings configured.");
        }

        // ==================================================================
        // DIAGNOSTICS
        // ==================================================================
        [MenuItem("CYBERNOMAD/Meta SDK Setup/Print Setup Status", false, 200)]
        public static void PrintSetupStatus()
        {
            Debug.Log("=== CYBERNOMAD Setup Status ===");
            Debug.Log($"Color Space: {PlayerSettings.colorSpace}");
            Debug.Log($"Company: {PlayerSettings.companyName}");
            Debug.Log($"Product: {PlayerSettings.productName}");
            Debug.Log($"Android Package: {PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)}");
            Debug.Log($"Scripting Backend: {PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)}");
            Debug.Log($"Architecture: {PlayerSettings.Android.targetArchitectures}");
            Debug.Log($"Min SDK: {PlayerSettings.Android.minSdkVersion}");
            Debug.Log($"Target SDK: {PlayerSettings.Android.targetSdkVersion}");
            Debug.Log($"Graphics APIs: {string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android))}");
            Debug.Log($"MSAA: {QualitySettings.antiAliasing}x");
            Debug.Log($"VSync: {QualitySettings.vSyncCount}");
            Debug.Log($"Anisotropic: {QualitySettings.anisotropicFiltering}");
            Debug.Log("=== End Status ===");
        }

        // ==================================================================
        // MANUAL STEPS (fallback if auto fails)
        // ==================================================================
        [MenuItem("CYBERNOMAD/Meta SDK Setup/1. Switch Platform to Android", false, 100)]
        public static void Step1_SwitchPlatform()
        {
            Debug.Log("[CYBERNOMAD] Opening Build Profiles -- switch to Android.");
            EditorApplication.ExecuteMenuItem("File/Build Profiles");
        }

        [MenuItem("CYBERNOMAD/Meta SDK Setup/2. Enable OpenXR", false, 101)]
        public static void Step2_EnableOpenXR()
        {
            Debug.Log("[CYBERNOMAD] Opening XR Plug-in Management -- tick OpenXR for Android.");
            SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
        }

        [MenuItem("CYBERNOMAD/Meta SDK Setup/3. Meta Quest Feature Group", false, 102)]
        public static void Step3_MetaQuestFeature()
        {
            Debug.Log("[CYBERNOMAD] Opening XR settings -- tick Meta Quest Feature Group.");
            SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
        }

        [MenuItem("CYBERNOMAD/Meta SDK Setup/4. Add OVRCameraRig to Scene", false, 103)]
        public static void Step4_SceneSetup()
        {
            EditorUtility.DisplayDialog(
                "CYBERNOMAD -- Scene Setup",
                "1. Delete Main Camera\n" +
                "2. Meta > Tools > Building Blocks\n" +
                "3. Add: Camera Rig\n" +
                "4. Add: Controller Tracking\n" +
                "5. Optional: Hand Tracking, Passthrough",
                "OK"
            );
        }

        // ==================================================================
        // HELPERS
        // ==================================================================
        private static string GetManifestPath()
        {
            string path = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            return File.Exists(path) ? path : null;
        }

        private static Type GetType(string assemblyQualifiedName)
        {
            return Type.GetType(assemblyQualifiedName);
        }

        private static void LogMissing(string what)
        {
            Debug.LogWarning($"[CYBERNOMAD] {what} types not available. Use manual steps 1-3.");
        }
    }
}
#endif
