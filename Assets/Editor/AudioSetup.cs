#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Plaga44.Audio;

namespace Plaga44.Editor
{
    /// <summary>
    /// Editor tool for verifying and configuring Meta XR Audio spatializer.
    /// Menu: CYBERNOMAD/Audio/Setup Spatial Audio
    /// </summary>
    public static class AudioSetup
    {
        private const string LOG = "[PLAGA44][AudioSetup]";
        private const string EXPECTED_SPATIALIZER = "MetaXRAudioSpatializerUnity";
        private const string AUDIO_PKG = "com.meta.xr.sdk.audio";
        private const string MANAGER_NAME = "SpatialAudioManager";

        // ------------------------------------------------------------------ //
        //  Menu items
        // ------------------------------------------------------------------ //

        [MenuItem("CYBERNOMAD/Audio/Setup Spatial Audio", false, 200)]
        public static void SetupSpatialAudio()
        {
            Debug.Log($"{LOG} === Setup Spatial Audio ===");

            bool pkgOk    = CheckPackage();
            bool spatzOk  = CheckAndApplySpatializer();
            bool managerOk = CheckOrCreateManager();

            Debug.Log($"{LOG} === Spatial Audio Setup Complete ===");
            Debug.Log($"{LOG}   Package  : {Status(pkgOk)}");
            Debug.Log($"{LOG}   Spatializer : {Status(spatzOk)}");
            Debug.Log($"{LOG}   Manager  : {Status(managerOk)}");

            if (pkgOk && spatzOk && managerOk)
                Debug.Log($"{LOG} All checks passed. Spatial audio ready.");
            else
                Debug.LogWarning($"{LOG} Some checks failed -- see messages above.");
        }

        [MenuItem("CYBERNOMAD/Audio/Verify Spatializer Status", false, 201)]
        public static void VerifySpatializerStatus()
        {
            Debug.Log($"{LOG} === Spatializer Status ===");

            string active = AudioSettings.GetSpatializerPluginName();
            if (string.IsNullOrEmpty(active))
            {
                Debug.LogWarning($"{LOG} No spatializer plugin active. " +
                                 "Run CYBERNOMAD/Audio/Setup Spatial Audio.");
            }
            else if (active == EXPECTED_SPATIALIZER)
            {
                Debug.Log($"{LOG} Meta XR Audio spatializer is ACTIVE: \"{active}\"");
            }
            else
            {
                Debug.LogWarning($"{LOG} Unexpected spatializer: \"{active}\". " +
                                 $"Expected \"{EXPECTED_SPATIALIZER}\".");
            }

            // Check in-scene manager
            var manager = Object.FindAnyObjectByType<SpatialAudioManager>();
            if (manager == null)
                Debug.LogWarning($"{LOG} SpatialAudioManager NOT found in scene.");
            else
                Debug.Log($"{LOG} SpatialAudioManager found: \"{manager.gameObject.name}\" " +
                          $"(spatializer active: {manager.IsSpatializerActive()})");
        }

        // ------------------------------------------------------------------ //
        //  Checks
        // ------------------------------------------------------------------ //

        private static bool CheckPackage()
        {
            // Check manifest for com.meta.xr.sdk.audio
            string manifestPath = System.IO.Path.Combine(
                Application.dataPath, "..", "Packages", "manifest.json");

            if (!System.IO.File.Exists(manifestPath))
            {
                Debug.LogError($"{LOG} manifest.json not found at: {manifestPath}");
                return false;
            }

            string content = System.IO.File.ReadAllText(manifestPath);
            if (content.Contains(AUDIO_PKG))
            {
                Debug.Log($"{LOG} Package \"{AUDIO_PKG}\" found in manifest.json.");
                return true;
            }

            Debug.LogError($"{LOG} Package \"{AUDIO_PKG}\" NOT found in manifest.json. " +
                           "Add it via CYBERNOMAD/Meta SDK Setup or Package Manager.");
            return false;
        }

        private static bool CheckAndApplySpatializer()
        {
            string current = AudioSettings.GetSpatializerPluginName();

            if (current == EXPECTED_SPATIALIZER)
            {
                Debug.Log($"{LOG} Spatializer already set to \"{EXPECTED_SPATIALIZER}\".");
                return true;
            }

            Debug.Log($"{LOG} Spatializer is \"{current}\" -- attempting to set \"{EXPECTED_SPATIALIZER}\"...");

            // Project Settings > Audio > Spatializer Plugin is stored in AudioManager asset.
            // We set it via AudioSettings API for the current session; the persistent setting
            // must be changed by the user in Project Settings > Audio > Spatializer Plugin.
            bool ok = AudioSettings.SetSpatializerPluginName(EXPECTED_SPATIALIZER);
            if (ok)
            {
                Debug.Log($"{LOG} Spatializer set successfully for this session. " +
                          "To persist: Project Settings > Audio > Spatializer Plugin = MetaXRAudioSpatializerUnity");
                return true;
            }

            Debug.LogWarning($"{LOG} Could not set spatializer via AudioSettings API. " +
                             "Set manually: Project Settings > Audio > Spatializer Plugin.");
            LogSpatializerManualInstructions();
            return false;
        }

        private static bool CheckOrCreateManager()
        {
            var existing = Object.FindAnyObjectByType<SpatialAudioManager>();
            if (existing != null)
            {
                Debug.Log($"{LOG} SpatialAudioManager already in scene: \"{existing.gameObject.name}\".");
                return true;
            }

            // Create in the active scene
            var go = new GameObject(MANAGER_NAME);
            go.AddComponent<SpatialAudioManager>();
            Undo.RegisterCreatedObjectUndo(go, "Create SpatialAudioManager");

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} SpatialAudioManager created in scene. Save the scene to persist.");
            return true;
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        private static string Status(bool ok) => ok ? "OK" : "FAIL";

        private static void LogSpatializerManualInstructions()
        {
            Debug.Log($"{LOG} Manual spatializer setup:");
            Debug.Log($"{LOG}   1. Edit > Project Settings > Audio");
            Debug.Log($"{LOG}   2. Spatializer Plugin = MetaXRAudioSpatializerUnity");
            Debug.Log($"{LOG}   3. Ambisonic Decoder Plugin = (leave empty unless using ambisonic clips)");
            Debug.Log($"{LOG}   If the plugin is not listed, ensure com.meta.xr.sdk.audio v81+ is installed.");
        }
    }
}
#endif
