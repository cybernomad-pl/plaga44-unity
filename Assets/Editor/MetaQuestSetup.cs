// MetaQuestSetup.cs
// CYBERNOMAD -- Full automated Meta Quest project setup.
// Three-phase setup: packages+settings, platform+XR, VR scene.
//
// Menu: CYBERNOMAD > Meta SDK Setup > Phase 1/2/3
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
        // PHASE 1: Install packages + configure settings
        // ==================================================================
        [MenuItem("CYBERNOMAD/Meta SDK Setup/1. Install Packages + Settings", false, 0)]
        public static void Phase1_InstallAndConfigure()
        {
            bool confirm = EditorUtility.DisplayDialog(
                "CYBERNOMAD -- Phase 1: Packages + Settings",
                "Install Meta XR SDK v81 + configure project.\n\n" +
                "This will:\n" +
                "- Add Meta scoped registry\n" +
                "- Install 5 packages (OpenXR, Meta XR SDK)\n" +
                "- Configure Player Settings (Linear, IL2CPP, Vulkan)\n" +
                "- Configure Quality Settings (4x MSAA, VSync off)\n\n" +
                "After this completes, run Phase 2.\n" +
                "Continue?",
                "Install",
                "Cancel"
            );

            if (!confirm) return;

            Debug.Log("[CYBERNOMAD] ============================================");
            Debug.Log("[CYBERNOMAD] PHASE 1: PACKAGES + SETTINGS");
            Debug.Log("[CYBERNOMAD] ============================================");

            EnsureScopedRegistry();
            SetPlayerSettings();
            SetQualitySettings();
            StartPackageInstall();
        }

        // ==================================================================
        // PHASE 2: Switch platform + enable XR
        // ==================================================================
        [MenuItem("CYBERNOMAD/Meta SDK Setup/2. Switch to Android + Enable XR", false, 1)]
        public static void Phase2_SwitchAndEnableXR()
        {
            // Check if packages are installed first
            var openXRType = GetType("UnityEngine.XR.OpenXR.OpenXRSettings, Unity.XR.OpenXR");
            if (openXRType == null)
            {
                EditorUtility.DisplayDialog(
                    "CYBERNOMAD -- Phase 2 Error",
                    "OpenXR package not found!\n\n" +
                    "Run Phase 1 first and wait for packages to install.",
                    "OK"
                );
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                Debug.Log("[CYBERNOMAD] Already on Android. Enabling XR...");
                EnableOpenXR();
                return;
            }

            // Check if Android build support is available
            bool androidAvailable = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);

            if (!androidAvailable)
            {
                bool installNow = EditorUtility.DisplayDialog(
                    "CYBERNOMAD -- Android Module Missing!",
                    "Android Build Support is NOT installed.\n\n" +
                    "Install it now via Unity Hub CLI?\n" +
                    "(This will download ~2 GB, may take a few minutes)\n\n" +
                    "Unity must be CLOSED during installation.",
                    "Install Android Module",
                    "Cancel"
                );

                if (installNow)
                    InstallAndroidModule();
                return;
            }

            bool confirm = EditorUtility.DisplayDialog(
                "CYBERNOMAD -- Phase 2: Android + XR",
                "Switch to Android platform + enable OpenXR.\n\n" +
                "This will:\n" +
                "- Switch build target to Android (causes editor reload)\n" +
                "- Enable OpenXR loader for Android\n" +
                "- Enable Meta Quest Feature Group\n\n" +
                "Editor WILL restart. Continue?",
                "Switch + Enable XR",
                "Cancel"
            );

            if (!confirm) return;

            Debug.Log("[CYBERNOMAD] ============================================");
            Debug.Log("[CYBERNOMAD] PHASE 2: SWITCHING TO ANDROID...");
            Debug.Log("[CYBERNOMAD] ============================================");

            // Set phase 1 -- after domain reload, ContinueFromPhase(1) enables XR
            MetaQuestSetupPhaser.SetPhase(1);

            bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android, BuildTarget.Android);

            if (!switched)
            {
                MetaQuestSetupPhaser.ClearPhase();
                Debug.LogError("[CYBERNOMAD] Platform switch FAILED.");
                EditorUtility.DisplayDialog(
                    "CYBERNOMAD -- Switch Failed",
                    "Platform switch to Android failed.\n\n" +
                    "Make sure Android Build Support is installed via Unity Hub.",
                    "OK"
                );
            }
            // If switched, editor reloads and MetaQuestSetupPhaser picks up phase 1
        }

        // ==================================================================
        // PHASE 3: Setup VR Scene (OVRCameraRig + controllers)
        // ==================================================================
        [MenuItem("CYBERNOMAD/Meta SDK Setup/3. Setup VR Scene", false, 2)]
        public static void Phase3_SetupVRScene()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUtility.DisplayDialog(
                    "CYBERNOMAD -- Wrong Platform",
                    "Build target is not Android.\n\nRun Phase 2 first.",
                    "OK");
                return;
            }

            // Check if OVRCameraRig prefab exists (from com.meta.xr.sdk.core)
            string[] guids = AssetDatabase.FindAssets("OVRCameraRig t:prefab");
            if (guids.Length == 0)
            {
                Debug.Log("[CYBERNOMAD] OVRCameraRig not found. Installing Meta XR SDK Core...");

                bool install = EditorUtility.DisplayDialog(
                    "CYBERNOMAD -- Meta SDK Core Required",
                    "OVRCameraRig prefab not found.\n\n" +
                    "Meta XR SDK Core needs to be installed.\n" +
                    "Install now? (editor will reload)",
                    "Install SDK Core",
                    "Cancel");

                if (!install) return;

                _sceneInstallQueue = new Queue<string[]>(new[]
                {
                    new[] { "com.meta.xr.sdk.core",        META_SDK_VERSION },
                    new[] { "com.meta.xr.sdk.interaction",  META_SDK_VERSION },
                    new[] { "com.meta.xr.sdk.audio",        META_SDK_VERSION },
                });
                _sceneInstallTotal = _sceneInstallQueue.Count;
                _sceneInstallCount = 0;
                EditorApplication.update += ScenePackageInstallTick;
                InstallNextScenePackage();
                return;
            }

            DoSceneSetup(guids);
        }

        // ==================================================================
        // Called by MetaQuestSetupPhaser after domain reload
        // ==================================================================
        public static void ContinueFromPhase(int phase)
        {
            Debug.Log($"[CYBERNOMAD] Resuming setup from phase {phase}...");
            switch (phase)
            {
                case 1: // Platform switched to Android, now enable XR
                    EnableOpenXR();
                    break;
                case 2: // Unity reopened after Android module install, run Phase 2
                    MetaQuestSetupPhaser.ClearPhase();
                    Debug.Log("[CYBERNOMAD] Android module should be installed. Running Phase 2...");
                    EditorApplication.delayCall += Phase2_SwitchAndEnableXR;
                    break;
                case 3: // SDK Core packages installed, run scene setup
                    MetaQuestSetupPhaser.ClearPhase();
                    Debug.Log("[CYBERNOMAD] SDK Core installed. Running scene setup...");
                    EditorApplication.delayCall += () =>
                    {
                        string[] guids = AssetDatabase.FindAssets("OVRCameraRig t:prefab");
                        if (guids.Length > 0)
                            DoSceneSetup(guids);
                        else
                            Debug.LogWarning("[CYBERNOMAD] OVRCameraRig still not found. Try Step 3 again.");
                    };
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
                Debug.Log("[CYBERNOMAD] ============================================");
                Debug.Log("[CYBERNOMAD] PHASE 1 COMPLETE.");
                Debug.Log("[CYBERNOMAD] Now run: CYBERNOMAD > Meta SDK Setup > 2. Switch to Android + Enable XR");
                Debug.Log("[CYBERNOMAD] ============================================");

                EditorUtility.DisplayDialog(
                    "CYBERNOMAD -- Phase 1 Complete",
                    "Packages installed + settings configured.\n\n" +
                    "Now run Phase 2:\n" +
                    "CYBERNOMAD > Meta SDK Setup >\n" +
                    "2. Switch to Android + Enable XR",
                    "OK"
                );
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
        // Scene package install (Phase 3 helper)
        // ==================================================================
        private static Queue<string[]> _sceneInstallQueue;
        private static AddRequest _sceneAddRequest;
        private static int _sceneInstallTotal;
        private static int _sceneInstallCount;

        private static void InstallNextScenePackage()
        {
            if (_sceneInstallQueue.Count == 0)
            {
                EditorApplication.update -= ScenePackageInstallTick;
                Debug.Log("[CYBERNOMAD] SDK Core packages installed. Setting up scene...");
                MetaQuestSetupPhaser.SetPhase(3);
                return;
            }

            var pkg = _sceneInstallQueue.Dequeue();
            string id = $"{pkg[0]}@{pkg[1]}";
            _sceneInstallCount++;
            Debug.Log($"[CYBERNOMAD] [{_sceneInstallCount}/{_sceneInstallTotal}] {id}");
            _sceneAddRequest = Client.Add(id);
        }

        private static void ScenePackageInstallTick()
        {
            if (_sceneAddRequest == null || !_sceneAddRequest.IsCompleted) return;

            if (_sceneAddRequest.Status == StatusCode.Success)
                Debug.Log($"[CYBERNOMAD] OK: {_sceneAddRequest.Result.packageId}");
            else if (_sceneAddRequest.Status >= StatusCode.Failure)
                Debug.LogWarning($"[CYBERNOMAD] FAIL: {_sceneAddRequest.Error?.message}");

            _sceneAddRequest = null;
            InstallNextScenePackage();
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

            // --- Step 3: Run Meta Project Setup Tool fixes if available ---
            TryRunMetaProjectFixes();

            MetaQuestSetupPhaser.ClearPhase();

            if (ok)
            {
                Debug.Log("[CYBERNOMAD] ============================================");
                Debug.Log("[CYBERNOMAD] PHASE 2 COMPLETE!");
                Debug.Log("[CYBERNOMAD] XR configured. Now run Step 3 to setup VR scene.");
                Debug.Log("[CYBERNOMAD] ============================================");

                EditorUtility.DisplayDialog(
                    "CYBERNOMAD -- Phase 2 Complete!",
                    "Platform + XR configured.\n\n" +
                    "Now run Step 3:\n" +
                    "CYBERNOMAD > Meta SDK Setup >\n" +
                    "3. Setup VR Scene",
                    "OK"
                );
            }
            else
            {
                Debug.LogWarning("[CYBERNOMAD] Some XR steps failed. Check Console. Try running Phase 2 again.");
            }
        }

        // --- OpenXR Loader via reflection ---
        private static bool TryEnableOpenXRLoader()
        {
            try
            {
                var perBuildType = GetType("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget, Unity.XR.Management.Editor");
                if (perBuildType == null) { LogMissing("XR Management Editor"); return false; }

                // --- Clean up corrupted XR assets if they exist ---
                CleanupCorruptedXRAssets(perBuildType);

                var getMethod = perBuildType.GetMethod("XRGeneralSettingsForBuildTarget",
                    BindingFlags.Public | BindingFlags.Static);
                var generalSettings = getMethod?.Invoke(null, new object[] { BuildTargetGroup.Android });

                if (generalSettings == null)
                {
                    Debug.Log("[CYBERNOMAD] Creating XR General Settings for Android...");
                    var xrGeneralType = GetType("UnityEngine.XR.Management.XRGeneralSettings, Unity.XR.Management");
                    if (xrGeneralType == null) { LogMissing("XR Management"); return false; }
                    var managerType = GetType("UnityEngine.XR.Management.XRManagerSettings, Unity.XR.Management");

                    // --- Get or create the per-build-target container ---
                    object perBuildInstance = null;

                    var tryGetMethods = typeof(EditorBuildSettings).GetMethods(BindingFlags.Public | BindingFlags.Static);
                    MethodInfo tryGetGeneric = null;
                    foreach (var m in tryGetMethods)
                    {
                        if (m.Name == "TryGetConfigObject" && m.IsGenericMethodDefinition)
                        { tryGetGeneric = m; break; }
                    }
                    if (tryGetGeneric != null)
                    {
                        var tryGet = tryGetGeneric.MakeGenericMethod(perBuildType);
                        var args = new object[] { "com.unity.xr.management.loader_settings", null };
                        bool found = (bool)tryGet.Invoke(null, args);
                        if (found && args[1] != null)
                        {
                            perBuildInstance = args[1];
                            Debug.Log("[CYBERNOMAD] Found existing XR settings container.");
                        }
                    }

                    if (perBuildInstance == null)
                    {
                        Debug.Log("[CYBERNOMAD] Creating new XR settings container...");
                        perBuildInstance = ScriptableObject.CreateInstance(perBuildType);

                        if (!AssetDatabase.IsValidFolder("Assets/XR"))
                            AssetDatabase.CreateFolder("Assets", "XR");

                        AssetDatabase.CreateAsset((UnityEngine.Object)perBuildInstance,
                            "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");

                        var addConfigMethod = typeof(EditorBuildSettings).GetMethod("AddConfigObject",
                            BindingFlags.Public | BindingFlags.Static);
                        addConfigMethod?.Invoke(null, new object[] {
                            "com.unity.xr.management.loader_settings",
                            (UnityEngine.Object)perBuildInstance,
                            true
                        });
                    }

                    // --- Create XRGeneralSettings + XRManagerSettings ---
                    generalSettings = ScriptableObject.CreateInstance(xrGeneralType);
                    var manager = ScriptableObject.CreateInstance(managerType);

                    var managerProp = xrGeneralType.GetProperty("Manager") ??
                                     xrGeneralType.GetProperty("AssignedSettings");
                    managerProp?.SetValue(generalSettings, manager);

                    if (!AssetDatabase.IsValidFolder("Assets/XR"))
                        AssetDatabase.CreateFolder("Assets", "XR");

                    AssetDatabase.CreateAsset((UnityEngine.Object)manager,
                        "Assets/XR/XRManagerSettingsAndroid.asset");
                    AssetDatabase.CreateAsset((UnityEngine.Object)generalSettings,
                        "Assets/XR/XRGeneralSettingsAndroid.asset");

                    // Register with per-build-target (INSTANCE method, not static!)
                    var setMethod = perBuildType.GetMethod("SetSettingsForBuildTarget",
                        BindingFlags.Public | BindingFlags.Instance,
                        null, new[] { typeof(BuildTargetGroup), xrGeneralType }, null);
                    setMethod?.Invoke(perBuildInstance, new object[] { BuildTargetGroup.Android, generalSettings });

                    try
                    {
                        EditorUtility.SetDirty((UnityEngine.Object)perBuildInstance);
                        AssetDatabase.SaveAssets();
                    }
                    catch (System.Exception dirtyEx)
                    {
                        Debug.LogWarning($"[CYBERNOMAD] Could not mark per-build container dirty (non-fatal): {dirtyEx.Message}");
                    }
                }

                // Get the Manager/AssignedSettings
                var gsType = generalSettings.GetType();
                var mgrProp = gsType.GetProperty("Manager") ?? gsType.GetProperty("AssignedSettings");
                var mgr = mgrProp?.GetValue(generalSettings);

                if (mgr == null)
                {
                    // Manager lost (Meta SDK reinstall can cause this) -- recreate it
                    Debug.Log("[CYBERNOMAD] XR Manager is null. Creating new one...");
                    var managerType = GetType("UnityEngine.XR.Management.XRManagerSettings, Unity.XR.Management");
                    if (managerType == null) { LogMissing("XR Management"); return false; }

                    mgr = ScriptableObject.CreateInstance(managerType);
                    mgrProp?.SetValue(generalSettings, mgr);

                    if (!AssetDatabase.IsValidFolder("Assets/XR"))
                        AssetDatabase.CreateFolder("Assets", "XR");

                    string mgrPath = "Assets/XR/XRManagerSettingsAndroid.asset";
                    if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(mgrPath) != null)
                        AssetDatabase.DeleteAsset(mgrPath);

                    AssetDatabase.CreateAsset((UnityEngine.Object)mgr, mgrPath);
                    try
                    {
                        EditorUtility.SetDirty((UnityEngine.Object)generalSettings);
                        AssetDatabase.SaveAssets();
                    }
                    catch (System.Exception dirtyEx)
                    {
                        Debug.LogWarning($"[CYBERNOMAD] Could not save after manager creation (non-fatal): {dirtyEx.Message}");
                    }
                    Debug.Log("[CYBERNOMAD] New XR Manager created and assigned.");
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

                // Save -- wrapped in try/catch because XRGeneralSettingsPerBuildTarget
                // can have corrupted serialized Values after SDK reinstall.
                // The OpenXR loader IS already assigned, so save failure is non-fatal.
                try
                {
                    EditorUtility.SetDirty((UnityEngine.Object)generalSettings);
                    if (mgr != null)
                        EditorUtility.SetDirty((UnityEngine.Object)mgr);
                    AssetDatabase.SaveAssets();
                }
                catch (System.Exception saveEx)
                {
                    Debug.LogWarning($"[CYBERNOMAD] Could not save XR settings (non-fatal, loader IS assigned): {saveEx.Message}");
                    try
                    {
                        if (mgr != null)
                        {
                            EditorUtility.SetDirty((UnityEngine.Object)mgr);
                            AssetDatabase.SaveAssets();
                        }
                    }
                    catch { /* truly non-fatal */ }
                }

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

                var featureBaseType = GetType(
                    "UnityEngine.XR.OpenXR.Features.OpenXRFeature, Unity.XR.OpenXR");
                if (featureBaseType == null) { LogMissing("OpenXR Features"); return false; }

                var featuresField = openXRSettingsType.GetField("features",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var featuresProp = openXRSettingsType.GetProperty("features",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                object featuresObj = featuresField?.GetValue(settings) ??
                                    featuresProp?.GetValue(settings);

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

                        bool isMeta = typeName.Contains("MetaQuest") ||
                                      fullName.Contains("Meta") ||
                                      typeName.Contains("OculusQuest");
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
        // CLEANUP CORRUPTED XR ASSETS (after SDK reinstall)
        // ==================================================================
        private static void CleanupCorruptedXRAssets(Type perBuildType)
        {
            try
            {
                var tryGetMethods = typeof(EditorBuildSettings).GetMethods(BindingFlags.Public | BindingFlags.Static);
                MethodInfo tryGetGeneric = null;
                foreach (var m in tryGetMethods)
                {
                    if (m.Name == "TryGetConfigObject" && m.IsGenericMethodDefinition)
                    { tryGetGeneric = m; break; }
                }

                if (tryGetGeneric != null)
                {
                    var tryGet = tryGetGeneric.MakeGenericMethod(perBuildType);
                    var args = new object[] { "com.unity.xr.management.loader_settings", null };
                    bool found = (bool)tryGet.Invoke(null, args);

                    if (found && args[1] != null)
                    {
                        var obj = (UnityEngine.Object)args[1];
                        bool corrupted = false;

                        // Test 1: Try SetDirty -- this is what actually fails on corrupted assets
                        try { EditorUtility.SetDirty(obj); }
                        catch { corrupted = true; }

                        // Test 2: Access the Values dictionary via reflection
                        if (!corrupted)
                        {
                            try
                            {
                                var valuesProp = perBuildType.GetProperty("Values",
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                var valuesField = perBuildType.GetField("Values",
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                                object values = valuesProp?.GetValue(obj) ?? valuesField?.GetValue(obj);
                                if (values == null)
                                    corrupted = true;
                                else if (values is System.Collections.IDictionary dict && dict.Count == 0)
                                    corrupted = true;
                            }
                            catch { corrupted = true; }
                        }

                        // Test 3: SerializedObject -- catches deeper Unity serialization issues
                        if (!corrupted)
                        {
                            try
                            {
                                var so = new SerializedObject(obj);
                                var sp = so.FindProperty("m_Settings");
                                if (sp == null)
                                {
                                    sp = so.GetIterator();
                                    sp.Next(true);
                                }
                            }
                            catch { corrupted = true; }
                        }

                        if (corrupted)
                        {
                            Debug.Log("[CYBERNOMAD] Detected corrupted XR settings. Nuking and recreating...");

                            var removeMethod = typeof(EditorBuildSettings).GetMethod("RemoveConfigObject",
                                BindingFlags.Public | BindingFlags.Static);
                            removeMethod?.Invoke(null, new object[] { "com.unity.xr.management.loader_settings" });

                            // Delete XR asset FILES from disk (bypasses broken AssetDatabase)
                            string xrFolder = Path.Combine(Application.dataPath, "XR");
                            if (Directory.Exists(xrFolder))
                            {
                                string[] files = Directory.GetFiles(xrFolder, "*.asset");
                                foreach (var f in files)
                                {
                                    Debug.Log($"[CYBERNOMAD] Deleting corrupted file: {f}");
                                    File.Delete(f);
                                    string meta = f + ".meta";
                                    if (File.Exists(meta)) File.Delete(meta);
                                }
                            }

                            // Also delete via AssetDatabase for safety
                            string[] assetPaths = new[]
                            {
                                "Assets/XR/XRGeneralSettingsPerBuildTarget.asset",
                                "Assets/XR/XRGeneralSettingsAndroid.asset",
                                "Assets/XR/XRManagerSettingsAndroid.asset"
                            };
                            foreach (var path in assetPaths)
                            {
                                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
                                    AssetDatabase.DeleteAsset(path);
                            }

                            AssetDatabase.Refresh();
                            Debug.Log("[CYBERNOMAD] Corrupted XR assets removed. Will recreate fresh.");
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CYBERNOMAD] XR cleanup warning: {e.Message}");
            }
        }

        // ==================================================================
        // META PROJECT SETUP TOOL -- auto-fix outstanding issues
        // ==================================================================
        private static void TryRunMetaProjectFixes()
        {
            try
            {
                var registryType = GetType("OVRConfigurationTaskRegistry, Meta.XR.Editor") ??
                                   GetType("OVRConfigurationTaskRegistry, Assembly-CSharp-Editor");

                if (registryType != null)
                {
                    var getTasksMethod = registryType.GetMethod("GetTasks",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);

                    if (getTasksMethod != null)
                    {
                        object tasks = null;
                        if (getTasksMethod.IsStatic)
                            tasks = getTasksMethod.Invoke(null, null);
                        else
                        {
                            var instanceProp = registryType.GetProperty("Instance",
                                BindingFlags.Public | BindingFlags.Static);
                            var instance = instanceProp?.GetValue(null);
                            if (instance != null)
                                tasks = getTasksMethod.Invoke(instance, null);
                        }

                        if (tasks is System.Collections.IEnumerable taskList)
                        {
                            int fixedCount = 0;
                            foreach (var task in taskList)
                            {
                                var taskType = task.GetType();
                                var isDoneProp = taskType.GetProperty("IsDone");
                                var fixMethod = taskType.GetMethod("Fix");

                                if (isDoneProp == null || fixMethod == null) continue;

                                bool isDone = (bool)isDoneProp.GetValue(task);
                                if (isDone) continue;

                                try
                                {
                                    var fixParams = fixMethod.GetParameters();
                                    if (fixParams.Length == 1)
                                        fixMethod.Invoke(task, new object[] { BuildTargetGroup.Android });
                                    else if (fixParams.Length == 0)
                                        fixMethod.Invoke(task, null);
                                    fixedCount++;
                                }
                                catch { /* skip individual fix errors */ }
                            }

                            if (fixedCount > 0)
                                Debug.Log($"[CYBERNOMAD] Auto-fixed {fixedCount} Meta Project Setup issues.");
                        }
                    }
                }

                Debug.Log("[CYBERNOMAD] Meta Project Setup fixes applied.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CYBERNOMAD] Meta Project Setup auto-fix skipped: {e.Message}");
            }
        }

        // ==================================================================
        // VR SCENE SETUP: OVRCameraRig + Controller prefabs
        // ==================================================================
        private static void DoSceneSetup(string[] ovrCameraRigGuids)
        {
            Debug.Log("[CYBERNOMAD] ============================================");
            Debug.Log("[CYBERNOMAD] PHASE 3: SETTING UP VR SCENE");
            Debug.Log("[CYBERNOMAD] ============================================");

            string prefabPath = AssetDatabase.GUIDToAssetPath(ovrCameraRigGuids[0]);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                Debug.LogError($"[CYBERNOMAD] Could not load OVRCameraRig at: {prefabPath}");
                return;
            }

            // Check if OVRCameraRig already exists in scene
            var existing = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in existing)
            {
                if (t.name == "OVRCameraRig" || t.name == "XROrigin")
                {
                    Debug.LogWarning($"[CYBERNOMAD] {t.name} already exists in scene. Aborting.");
                    EditorUtility.DisplayDialog(
                        "CYBERNOMAD",
                        $"{t.name} already exists in the scene.\n\nScene setup skipped.",
                        "OK");
                    return;
                }
            }

            // Delete Main Camera
            var cameras = GameObject.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam.gameObject.name == "Main Camera")
                {
                    Undo.DestroyObjectImmediate(cam.gameObject);
                    Debug.Log("[CYBERNOMAD] Deleted Main Camera.");
                    break;
                }
            }

            // Instantiate OVRCameraRig
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rig.transform.position = Vector3.zero;
            rig.transform.rotation = UnityEngine.Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(rig, "Add OVRCameraRig");

            // --- Add controller prefabs to hand anchors ---
            AddControllerPrefabs(rig);

            Selection.activeGameObject = rig;

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("[CYBERNOMAD] OVRCameraRig + controllers added to scene at origin.");
            Debug.Log("[CYBERNOMAD] ============================================");
            Debug.Log("[CYBERNOMAD] VR SCENE READY!");
            Debug.Log("[CYBERNOMAD] ============================================");

            EditorUtility.DisplayDialog(
                "CYBERNOMAD -- VR Scene Ready!",
                "Scene configured:\n\n" +
                "- Main Camera deleted\n" +
                "- OVRCameraRig added at origin\n" +
                "- Left + Right controller models added\n\n" +
                "Press Play to test in editor.\n" +
                "Build & Run to deploy to Quest.",
                "OK");
        }

        // --- Add OVRControllerPrefab to left/right hand anchors ---
        private static void AddControllerPrefabs(GameObject rig)
        {
            // Find OVRControllerPrefab in the project
            string[] ctrlGuids = AssetDatabase.FindAssets("OVRControllerPrefab t:prefab");
            if (ctrlGuids.Length == 0)
            {
                Debug.LogWarning("[CYBERNOMAD] OVRControllerPrefab not found. Controllers not added.");
                Debug.LogWarning("[CYBERNOMAD] Add them manually: Meta > Tools > Building Blocks > Controller Tracking");
                return;
            }

            string ctrlPrefabPath = AssetDatabase.GUIDToAssetPath(ctrlGuids[0]);
            var ctrlPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ctrlPrefabPath);
            if (ctrlPrefab == null)
            {
                Debug.LogWarning("[CYBERNOMAD] Could not load OVRControllerPrefab.");
                return;
            }

            // Find hand anchors in the OVRCameraRig hierarchy
            // OVRCameraRig > TrackingSpace > LeftHandAnchor > LeftControllerAnchor
            // OVRCameraRig > TrackingSpace > RightHandAnchor > RightControllerAnchor
            Transform leftAnchor = FindChildRecursive(rig.transform, "LeftControllerAnchor")
                                ?? FindChildRecursive(rig.transform, "LeftHandAnchor");
            Transform rightAnchor = FindChildRecursive(rig.transform, "RightControllerAnchor")
                                 ?? FindChildRecursive(rig.transform, "RightHandAnchor");

            if (leftAnchor != null)
            {
                var leftCtrl = (GameObject)PrefabUtility.InstantiatePrefab(ctrlPrefab, leftAnchor);
                leftCtrl.name = "LeftControllerModel";
                leftCtrl.transform.localPosition = Vector3.zero;
                leftCtrl.transform.localRotation = UnityEngine.Quaternion.identity;

                // Set controller type to LTouch via reflection (OVRControllerPrefab.m_controller)
                SetControllerType(leftCtrl, "LTouch");
                Undo.RegisterCreatedObjectUndo(leftCtrl, "Add Left Controller");
                Debug.Log("[CYBERNOMAD] Left controller added to LeftControllerAnchor.");
            }
            else
            {
                Debug.LogWarning("[CYBERNOMAD] LeftControllerAnchor not found in OVRCameraRig.");
            }

            if (rightAnchor != null)
            {
                var rightCtrl = (GameObject)PrefabUtility.InstantiatePrefab(ctrlPrefab, rightAnchor);
                rightCtrl.name = "RightControllerModel";
                rightCtrl.transform.localPosition = Vector3.zero;
                rightCtrl.transform.localRotation = UnityEngine.Quaternion.identity;

                SetControllerType(rightCtrl, "RTouch");
                Undo.RegisterCreatedObjectUndo(rightCtrl, "Add Right Controller");
                Debug.Log("[CYBERNOMAD] Right controller added to RightControllerAnchor.");
            }
            else
            {
                Debug.LogWarning("[CYBERNOMAD] RightControllerAnchor not found in OVRCameraRig.");
            }
        }

        // --- Set OVRInput.Controller type on OVRControllerPrefab component ---
        private static void SetControllerType(GameObject ctrlObj, string controllerName)
        {
            // OVRControllerPrefab has a m_controller field of type OVRInput.Controller
            var components = ctrlObj.GetComponents<MonoBehaviour>();
            foreach (var comp in components)
            {
                if (comp == null) continue;
                var compType = comp.GetType();
                if (!compType.Name.Contains("Controller")) continue;

                // Look for m_controller or controller field
                var field = compType.GetField("m_controller",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null)
                    field = compType.GetField("m_controllerType",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (field != null && field.FieldType.IsEnum)
                {
                    try
                    {
                        var enumVal = System.Enum.Parse(field.FieldType, controllerName);
                        field.SetValue(comp, enumVal);
                        Debug.Log($"[CYBERNOMAD] Set controller type to {controllerName}.");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[CYBERNOMAD] Could not set controller type: {e.Message}");
                    }
                    return;
                }

                // Also try via SerializedObject for better undo support
                try
                {
                    var so = new SerializedObject(comp);
                    var prop = so.FindProperty("m_controller") ?? so.FindProperty("m_controllerType");
                    if (prop != null && prop.propertyType == SerializedPropertyType.Enum)
                    {
                        // LTouch = 1, RTouch = 2 in OVRInput.Controller
                        prop.enumValueIndex = controllerName == "LTouch" ? 1 : 2;
                        so.ApplyModifiedProperties();
                        Debug.Log($"[CYBERNOMAD] Set controller type to {controllerName} via SerializedObject.");
                        return;
                    }
                }
                catch { /* fallthrough */ }
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
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)32;    // Meta Quest requires min 32
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)32;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // GameActivity (required on Unity 6+)
            try
            {
                var appEntryType = typeof(PlayerSettings.Android).GetProperty("applicationEntry",
                    BindingFlags.Public | BindingFlags.Static);
                if (appEntryType != null)
                {
                    var enumType = appEntryType.PropertyType;
                    var gameActivity = System.Enum.ToObject(enumType, 1); // GameActivity = 1
                    appEntryType.SetValue(null, gameActivity);
                    Debug.Log("[CYBERNOMAD] Set ApplicationEntry to GameActivity.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CYBERNOMAD] Could not set GameActivity: {e.Message}");
            }

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
            Debug.Log($"Build Target: {EditorUserBuildSettings.activeBuildTarget}");

            // --- XR Status (via reflection) ---
            Debug.Log("--- XR Configuration ---");

            var perBuildType = GetType("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget, Unity.XR.Management.Editor");
            if (perBuildType != null)
            {
                var getMethod = perBuildType.GetMethod("XRGeneralSettingsForBuildTarget",
                    BindingFlags.Public | BindingFlags.Static);
                var generalSettings = getMethod?.Invoke(null, new object[] { BuildTargetGroup.Android });

                if (generalSettings != null)
                {
                    var gsType = generalSettings.GetType();
                    var mgrProp = gsType.GetProperty("Manager") ?? gsType.GetProperty("AssignedSettings");
                    var mgr = mgrProp?.GetValue(generalSettings);

                    if (mgr != null)
                    {
                        var loadersProp = mgr.GetType().GetProperty("activeLoaders") ??
                                         mgr.GetType().GetProperty("loaders");
                        var loaders = loadersProp?.GetValue(mgr) as System.Collections.IList;

                        if (loaders != null && loaders.Count > 0)
                        {
                            foreach (var loader in loaders)
                                Debug.Log($"XR Loader: {loader.GetType().Name} [ENABLED]");
                        }
                        else
                        {
                            Debug.LogWarning("XR Loaders: NONE -- OpenXR not enabled!");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("XR Manager: NOT CONFIGURED");
                    }
                }
                else
                {
                    Debug.LogWarning("XR General Settings for Android: NOT FOUND");
                }
            }
            else
            {
                Debug.LogWarning("XR Management: NOT INSTALLED");
            }

            // Check OpenXR features
            var openXRSettingsType = GetType("UnityEngine.XR.OpenXR.OpenXRSettings, Unity.XR.OpenXR");
            if (openXRSettingsType != null)
            {
                var getSettingsMethod = openXRSettingsType.GetMethod("GetSettingsForBuildTargetGroup",
                    BindingFlags.Public | BindingFlags.Static);
                var settings = getSettingsMethod?.Invoke(null, new object[] { BuildTargetGroup.Android });

                if (settings != null)
                {
                    var featuresField = openXRSettingsType.GetField("features",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var featuresProp = openXRSettingsType.GetProperty("features",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    object featuresObj = featuresField?.GetValue(settings) ??
                                        featuresProp?.GetValue(settings);

                    if (featuresObj is Array featuresArray)
                    {
                        foreach (var feature in featuresArray)
                        {
                            if (feature == null) continue;
                            var enabledProp = feature.GetType().GetProperty("enabled");
                            bool enabled = enabledProp != null && (bool)enabledProp.GetValue(feature);
                            if (enabled)
                            {
                                var nameProp = feature.GetType().GetProperty("name");
                                string fname = nameProp?.GetValue(feature)?.ToString() ?? feature.GetType().Name;
                                Debug.Log($"OpenXR Feature: {fname} [ENABLED]");
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("OpenXR Settings for Android: NOT CONFIGURED");
                }
            }
            else
            {
                Debug.Log("OpenXR: not installed yet");
            }

            // Check scene for VR rig
            var ovrRig = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            bool hasRig = false;
            bool hasControllers = false;
            foreach (var t in ovrRig)
            {
                if (t.name == "OVRCameraRig") hasRig = true;
                if (t.name.Contains("ControllerModel")) hasControllers = true;
            }
            Debug.Log($"Scene OVRCameraRig: {(hasRig ? "YES" : "NO")}");
            Debug.Log($"Scene Controllers: {(hasControllers ? "YES" : "NO")}");

            Debug.Log("=== End Status ===");
        }

        // ==================================================================
        // ANDROID MODULE INSTALL via Unity Hub CLI
        // ==================================================================
        private static void InstallAndroidModule()
        {
            string unityVersion = Application.unityVersion;

            string[] hubPaths = new[]
            {
                @"C:\Program Files\Unity Hub\Unity Hub.exe",
                @"C:\Program Files (x86)\Unity Hub\Unity Hub.exe",
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData)
                    + @"\Programs\Unity Hub\Unity Hub.exe",
            };

            string hubPath = null;
            foreach (var p in hubPaths)
            {
                if (File.Exists(p)) { hubPath = p; break; }
            }

            if (hubPath == null)
            {
                Debug.LogError("[CYBERNOMAD] Unity Hub not found!");
                EditorUtility.DisplayDialog(
                    "CYBERNOMAD -- Unity Hub Not Found",
                    "Could not find Unity Hub.\n\n" +
                    "Install Android Build Support manually:\n" +
                    "Unity Hub > Installs > Add Modules",
                    "OK"
                );
                return;
            }

            Debug.Log($"[CYBERNOMAD] Unity Hub found: {hubPath}");
            Debug.Log($"[CYBERNOMAD] Unity version: {unityVersion}");

            try
            {
                string unityExe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                string projectPath = Path.GetDirectoryName(Application.dataPath);

                MetaQuestSetupPhaser.SetPhase(2);

                string batPath = Path.Combine(Path.GetTempPath(), "cybernomad_android_install.bat");
                string batContent =
                    "@echo off\r\n" +
                    "echo [CYBERNOMAD] Installing Android Build Support...\r\n" +
                    "echo [CYBERNOMAD] This may take 2-5 minutes. Do NOT close this window.\r\n" +
                    "echo.\r\n" +
                    $"\"{hubPath}\" -- --headless install-modules --version {unityVersion} --module android --childModules\r\n" +
                    "echo.\r\n" +
                    "echo [CYBERNOMAD] Android module installed. Reopening Unity...\r\n" +
                    $"start \"\" \"{unityExe}\" -projectPath \"{projectPath}\"\r\n" +
                    "echo [CYBERNOMAD] Done. This window will close in 5 seconds.\r\n" +
                    "timeout /t 5 /nobreak >nul\r\n" +
                    $"del \"{batPath}\"\r\n";

                File.WriteAllText(batPath, batContent);

                var batPsi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batPath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                };
                System.Diagnostics.Process.Start(batPsi);

                Debug.Log("[CYBERNOMAD] Install script launched. Unity will close and reopen automatically.");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CYBERNOMAD] Failed to launch Unity Hub: {e.Message}");
                EditorUtility.DisplayDialog(
                    "CYBERNOMAD -- Install Failed",
                    $"Could not launch Unity Hub:\n{e.Message}\n\n" +
                    "Install Android Build Support manually via Unity Hub.",
                    "OK"
                );
            }
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
            Debug.LogWarning($"[CYBERNOMAD] {what} types not available yet. Run setup again after packages finish installing.");
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
