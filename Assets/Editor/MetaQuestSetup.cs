// =============================================================================
// MetaQuestSetup.cs
// CYBERNOMAD -- Auto-detect + install Meta XR SDK, enable OpenXR, switch Android.
// Menu: CYBERNOMAD > Meta SDK Setup > 1. Setup Meta SDK / 2. Switch to Android.
// Wersja SDK w META_SDK_VERSION -- zmiana upgraduje wszystkie Meta XR paczki razem.
// =============================================================================
using System.IO;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;

namespace Plaga44.Editor
{
    [InitializeOnLoad]
    public static class MetaQuestSetup
    {
        private const string LOG = "[PLAGA44]";
        private const string META_SDK_VERSION = "81.0.0";
        private const string SESSION_KEY = "PLAGA44_SDK_CHECK_DONE";

        // ---- Manifest / registry ------------------------------------------
        private const string DependenciesToken = "\"dependencies\"";
        private const string ScopedRegistryUrl = "https://npm.developer.oculus.com";
        private const string ScopedRegistryScope = "com.meta.xr";
        private const string ScopedRegistryMarker = "npm.developer.oculus.com";
        private const string MetaCoreMarker = "com.meta.xr.sdk.core";

        // ---- XR Plug-in Management paths ----------------------------------
        private const string XrSettingsFolder = "Assets/XR/Settings";
        private const string XrPerBuildTargetAsset = XrSettingsFolder + "/XRGeneralSettingsPerBuildTarget.asset";
        private const string XrAndroidSettingsAsset = XrSettingsFolder + "/XRGeneralSettings_Android.asset";
        private const string XrAndroidManagerAsset = XrSettingsFolder + "/XRManager_Android.asset";
        private const string OpenXRLoaderTypeName = "Unity.XR.OpenXR.OpenXRLoader";

        private static readonly (string id, string version)[] PackagesToInstall =
        {
            ("com.unity.xr.openxr",                "1.14.0"),
            ("com.unity.xr.meta-openxr",            "2.4.0"),
            ("com.meta.xr.sdk.core",                META_SDK_VERSION),
            ("com.meta.xr.sdk.interaction",         META_SDK_VERSION),
            ("com.meta.xr.sdk.interaction.ovr",     META_SDK_VERSION),
            ("com.meta.xr.sdk.audio",               META_SDK_VERSION),
        };

        // =====================================================================
        // Auto-check on editor load
        // =====================================================================

        static MetaQuestSetup()
        {
            if (SessionState.GetBool(SESSION_KEY, false)) return;
            SessionState.SetBool(SESSION_KEY, true);
            EditorApplication.delayCall += AutoCheck;
        }

        private static void AutoCheck()
        {
            if (EditorApplication.isPlaying) return;

            if (IsMetaXRInstalled())
            {
                Debug.Log($"{LOG} Meta XR SDK detected -- OK.");
                OfferAndroidSwitchIfNeeded();
                return;
            }
            OfferFullSetup();
        }

        private static void OfferAndroidSwitchIfNeeded()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) return;
            bool doSwitch = EditorUtility.DisplayDialog(
                "PLAGA '44 -- Switch to Android?",
                "Meta XR SDK is installed but build target is not Android.\n\nSwitch now?",
                "Switch to Android", "Not now");
            if (doSwitch) SwitchToAndroid();
        }

        private static void OfferFullSetup()
        {
            bool doSetup = EditorUtility.DisplayDialog(
                "PLAGA '44 -- Meta XR SDK not found",
                "This project requires Meta XR SDK for Quest development.\n\n" +
                "Install Meta XR SDK + configure project settings automatically?",
                "Setup Everything", "Skip");
            if (doSetup)
            {
                SetupMetaSDK();
                SwitchToAndroid();
            }
        }

        private static bool IsMetaXRInstalled()
        {
            string manifest = ReadManifest();
            return manifest != null && manifest.Contains(MetaCoreMarker);
        }

        // =====================================================================
        // Menu items
        // =====================================================================

        [MenuItem("CYBERNOMAD/Meta SDK Setup/1. Setup Meta SDK", false, 1)]
        public static void SetupMetaSDK()
        {
            Debug.Log($"{LOG} === Setup Meta SDK ===");
            AddScopedRegistry();
            AddPackagesToManifest();
            EnableOpenXRLoader();
            Debug.Log($"{LOG} === DONE -- Unity will now resolve packages ===");
        }

        [MenuItem("CYBERNOMAD/Meta SDK Setup/2. Switch to Android", false, 2)]
        public static void SwitchToAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                Debug.Log($"{LOG} Already on Android.");
                return;
            }
            Debug.Log($"{LOG} Switching to Android...");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }

        // =====================================================================
        // 1. Scoped Registry -- dodaje blok "scopedRegistries" przed "dependencies"
        // =====================================================================

        private static void AddScopedRegistry()
        {
            string path = GetManifestPath();
            if (path == null) return;

            string manifest = File.ReadAllText(path);
            if (manifest.Contains(ScopedRegistryMarker))
            {
                Debug.Log($"{LOG} Registry already present.");
                return;
            }

            int insertAt = FindDependenciesLineStart(manifest);
            if (insertAt < 0)
            {
                Debug.LogError($"{LOG} Cannot find 'dependencies' in manifest.json");
                return;
            }

            File.WriteAllText(path, manifest.Substring(0, insertAt) + BuildRegistryBlock() + manifest.Substring(insertAt));
            Debug.Log($"{LOG} Added Meta XR scoped registry.");
        }

        private static int FindDependenciesLineStart(string manifest)
        {
            int depsIdx = manifest.IndexOf(DependenciesToken);
            if (depsIdx < 0) return -1;
            int lineStart = manifest.LastIndexOf('\n', depsIdx);
            return lineStart < 0 ? 0 : lineStart + 1;
        }

        private static string BuildRegistryBlock() =>
            "  \"scopedRegistries\": [\n" +
            "    {\n" +
            $"      \"name\": \"Meta XR\",\n" +
            $"      \"url\": \"{ScopedRegistryUrl}\",\n" +
            $"      \"scopes\": [\n" +
            $"        \"{ScopedRegistryScope}\"\n" +
            "      ]\n" +
            "    }\n" +
            "  ],\n";

        // =====================================================================
        // 2. Packages -- wstrzykuje entries do "dependencies"
        // =====================================================================

        private static void AddPackagesToManifest()
        {
            string path = GetManifestPath();
            if (path == null) return;

            string manifest = File.ReadAllText(path);
            bool changed = false;
            foreach (var (id, version) in PackagesToInstall)
            {
                if (manifest.Contains(id))
                {
                    Debug.Log($"{LOG} {id} already in manifest.");
                    continue;
                }
                if (!TryInsertPackage(ref manifest, id, version)) continue;
                changed = true;
                Debug.Log($"{LOG} Added {id}@{version}");
            }

            if (changed)
            {
                File.WriteAllText(path, manifest);
                Debug.Log($"{LOG} Packages added to manifest. Resolving...");
                UnityEditor.PackageManager.Client.Resolve();
            }
            else
            {
                Debug.Log($"{LOG} All packages already in manifest.");
            }
        }

        private static bool TryInsertPackage(ref string manifest, string id, string version)
        {
            int depsIdx = manifest.IndexOf(DependenciesToken);
            int braceIdx = manifest.IndexOf('{', depsIdx);
            if (braceIdx < 0) return false;
            string entry = $"\n    \"{id}\": \"{version}\",";
            manifest = manifest.Substring(0, braceIdx + 1) + entry + manifest.Substring(braceIdx + 1);
            return true;
        }

        // =====================================================================
        // 3. OpenXR Loader (XR Plugin Management)
        // =====================================================================

        private static void EnableOpenXRLoader()
        {
            var perBuildTarget = LoadOrCreatePerBuildTargetSettings();
            var generalSettings = EnsureAndroidGeneralSettings(perBuildTarget);
            AssignOpenXRLoader(generalSettings);
            AssetDatabase.SaveAssets();
        }

        private static XRGeneralSettingsPerBuildTarget LoadOrCreatePerBuildTargetSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));

            EnsureDirectory(XrSettingsFolder);
            var asset = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(asset, XrPerBuildTargetAsset);
            return asset;
        }

        private static XRGeneralSettings EnsureAndroidGeneralSettings(XRGeneralSettingsPerBuildTarget perBuildTarget)
        {
            var settings = perBuildTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (settings != null) return settings;

            EnsureDirectory(XrSettingsFolder);
            settings = ScriptableObject.CreateInstance<XRGeneralSettings>();
            var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
            settings.Manager = manager;

            AssetDatabase.CreateAsset(settings, XrAndroidSettingsAsset);
            AssetDatabase.CreateAsset(manager, XrAndroidManagerAsset);
            perBuildTarget.SetSettingsForBuildTarget(BuildTargetGroup.Android, settings);
            EditorUtility.SetDirty(perBuildTarget);

            Debug.Log($"{LOG} Created XR General Settings for Android.");
            return settings;
        }

        private static void AssignOpenXRLoader(XRGeneralSettings generalSettings)
        {
            bool assigned = XRPackageMetadataStore.AssignLoader(
                generalSettings.Manager, OpenXRLoaderTypeName, BuildTargetGroup.Android);
            if (assigned)
                Debug.Log($"{LOG} OpenXR Loader enabled for Android.");
            else
                Debug.LogWarning($"{LOG} Could not enable OpenXR Loader automatically. " +
                    "Do it manually: Project Settings > XR Plug-in Management > Android > OpenXR.");
        }

        // =====================================================================
        // Utilities
        // =====================================================================

        private static string ReadManifest()
        {
            string path = GetManifestPath();
            return path != null ? File.ReadAllText(path) : null;
        }

        private static string GetManifestPath()
        {
            string p = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            if (File.Exists(p)) return p;
            Debug.LogError($"{LOG} manifest.json not found!");
            return null;
        }

        private static void EnsureDirectory(string assetPath)
        {
            string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);
        }
    }
}
