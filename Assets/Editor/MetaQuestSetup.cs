// MetaQuestSetup.cs -- CYBERNOMAD Editor Tool
//
// Auto-detects missing Meta XR SDK on editor open and offers to install it.
// Manual triggers: CYBERNOMAD > Meta SDK Setup > 1. Setup Meta SDK / 2. Switch to Android
//
// What it does:
//   1. Adds Meta XR scoped registry to manifest.json
//   2. Adds required packages (OpenXR, Meta XR Core, Interaction, Audio)
//   3. Enables OpenXR Loader in XR Plugin Management
//   4. Switches build target to Android
//
// Player/Quality settings are baked into ProjectSettings/*.asset in the repo.
// Version: META_SDK_VERSION constant below. Change it to upgrade all packages at once.

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

        private static readonly string[][] PackagesToInstall = new[]
        {
            new[] { "com.unity.xr.openxr",                "1.14.0" },
            new[] { "com.unity.xr.meta-openxr",            "2.4.0"  },
            new[] { "com.meta.xr.sdk.core",                META_SDK_VERSION },
            new[] { "com.meta.xr.sdk.interaction",         META_SDK_VERSION },
            new[] { "com.meta.xr.sdk.interaction.ovr",     META_SDK_VERSION },
            new[] { "com.meta.xr.sdk.audio",               META_SDK_VERSION },
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

        static void AutoCheck()
        {
            // Otwórz TESTBED_V2 i postaw scene
            SceneSetup.LoadTestbed();

            if (IsMetaXRInstalled())
            {
                Debug.Log($"{LOG} Meta XR SDK detected -- OK.");

                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                {
                    bool doSwitch = EditorUtility.DisplayDialog(
                        "PLAGA '44 -- Switch to Android?",
                        "Meta XR SDK is installed but build target is not Android.\n\nSwitch now?",
                        "Switch to Android", "Not now");
                    if (doSwitch) SwitchToAndroid();
                }
                return;
            }

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

        static bool IsMetaXRInstalled()
        {
            string manifest = ReadManifest();
            return manifest != null && manifest.Contains("com.meta.xr.sdk.core");
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
            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android, BuildTarget.Android);
        }

        // =====================================================================
        // 1. Scoped Registry (npm.developer.oculus.com)
        // =====================================================================

        static void AddScopedRegistry()
        {
            string path = GetManifestPath();
            if (path == null) return;

            string manifest = File.ReadAllText(path);
            if (manifest.Contains("npm.developer.oculus.com"))
            {
                Debug.Log($"{LOG} Registry already present.");
                return;
            }

            int depsIdx = manifest.IndexOf("\"dependencies\"");
            if (depsIdx < 0)
            {
                Debug.LogError($"{LOG} Cannot find 'dependencies' in manifest.json");
                return;
            }

            int lineStart = manifest.LastIndexOf('\n', depsIdx);
            if (lineStart < 0) lineStart = 0;
            else lineStart += 1;

            string registry = @"  ""scopedRegistries"": [
    {
      ""name"": ""Meta XR"",
      ""url"": ""https://npm.developer.oculus.com"",
      ""scopes"": [
        ""com.meta.xr""
      ]
    }
  ],
";
            manifest = manifest.Substring(0, lineStart) + registry + manifest.Substring(lineStart);
            File.WriteAllText(path, manifest);
            Debug.Log($"{LOG} Added Meta XR scoped registry.");
        }

        // =====================================================================
        // 2. Packages (manifest.json)
        // =====================================================================

        static void AddPackagesToManifest()
        {
            string path = GetManifestPath();
            if (path == null) return;

            string manifest = File.ReadAllText(path);
            bool changed = false;

            foreach (var pkg in PackagesToInstall)
            {
                if (manifest.Contains(pkg[0]))
                {
                    Debug.Log($"{LOG} {pkg[0]} already in manifest.");
                    continue;
                }

                int depsIdx = manifest.IndexOf("\"dependencies\"");
                int braceIdx = manifest.IndexOf('{', depsIdx);
                if (braceIdx < 0) continue;

                string entry = $"\n    \"{pkg[0]}\": \"{pkg[1]}\",";
                manifest = manifest.Substring(0, braceIdx + 1) + entry + manifest.Substring(braceIdx + 1);
                changed = true;
                Debug.Log($"{LOG} Added {pkg[0]}@{pkg[1]}");
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

        // =====================================================================
        // 3. OpenXR Loader (XR Plugin Management)
        // =====================================================================

        static void EnableOpenXRLoader()
        {
            // Find existing settings asset or create one
            string[] guids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
            XRGeneralSettingsPerBuildTarget perBuildTarget = null;

            if (guids.Length > 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                perBuildTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(assetPath);
            }

            if (perBuildTarget == null)
            {
                EnsureDirectory("Assets/XR/Settings");
                perBuildTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perBuildTarget, "Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset");
            }

            // Ensure Android general settings exist
            var generalSettings = perBuildTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (generalSettings == null)
            {
                EnsureDirectory("Assets/XR/Settings");
                generalSettings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                generalSettings.Manager = manager;

                AssetDatabase.CreateAsset(generalSettings, "Assets/XR/Settings/XRGeneralSettings_Android.asset");
                AssetDatabase.CreateAsset(manager, "Assets/XR/Settings/XRManager_Android.asset");

                perBuildTarget.SetSettingsForBuildTarget(BuildTargetGroup.Android, generalSettings);
                EditorUtility.SetDirty(perBuildTarget);
                Debug.Log($"{LOG} Created XR General Settings for Android.");
            }

            // Assign the loader
            bool assigned = XRPackageMetadataStore.AssignLoader(
                generalSettings.Manager, "Unity.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android);

            if (assigned)
                Debug.Log($"{LOG} OpenXR Loader enabled for Android.");
            else
                Debug.LogWarning($"{LOG} Could not enable OpenXR Loader automatically. " +
                    "Do it manually: Project Settings > XR Plug-in Management > Android > OpenXR.");

            AssetDatabase.SaveAssets();
        }

        // =====================================================================
        // Utilities
        // =====================================================================

        static string ReadManifest()
        {
            string path = GetManifestPath();
            return path != null ? File.ReadAllText(path) : null;
        }

        static string GetManifestPath()
        {
            string p = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            if (!File.Exists(p))
            {
                Debug.LogError($"{LOG} manifest.json not found!");
                return null;
            }
            return p;
        }

        /// <summary>
        /// Czyści brakujace tree/detail prototypy z terrain data assetow na dysku
        /// ZANIM scena sie zaladuje (zeby uniknac warningow "Tree prefab at index X is missing").
        /// </summary>
        static void CleanTerrainDataAssets()
        {
            string[] tilePaths = new string[10];
            for (int i = 0; i < 9; i++)
                tilePaths[i] = $"Assets/Level/Terrain/Tile_{i}.asset";
            tilePaths[9] = "Assets/Level/Terrain/Scene_A_Terrain.asset";

            int cleaned = 0;
            foreach (var path in tilePaths)
            {
                var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
                if (data == null) continue;
                if (data.treePrototypes.Length == 0 && data.detailPrototypes.Length == 0) continue;

                data.treeInstances = new TreeInstance[0];
                data.treePrototypes = new TreePrototype[0];
                data.detailPrototypes = new DetailPrototype[0];
                EditorUtility.SetDirty(data);
                cleaned++;
            }

            if (cleaned > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"{LOG} Wyczyszczono drzewa/detale z {cleaned} terrain data assetow.");
            }
        }

        static void EnsureDirectory(string assetPath)
        {
            string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);
        }
    }
}
