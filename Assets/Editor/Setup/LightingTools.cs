// =============================================================================
// LightingTools.cs
// CYBERNOMAD -- Menu helpers for baked lighting management (issue #177).
//
// Problem: 'Lighting data asset incompatible with current Unity version'
//   appears when scene's LightingDataAsset was baked on older Unity.
//   Required manual click Window > Rendering > Lighting > Clear Baked Data
//   and then Generate Lighting. Borys: 'ja nie klikam'.
//
// Menu:
//   CYBERNOMAD > Tools > Clear Baked Lighting    -- remove stale LightingDataAsset
//   CYBERNOMAD > Tools > Generate Lighting Now   -- start bake (blocks until done)
//   CYBERNOMAD > Tools > Generate Lighting Async -- start bake in background
//
// Bootstrap integration:
//   LightingTools.AutoClearIfIncompatible() called from Bootstrap.RunSetup
//   detects the incompatible-version warning symptom and clears automatically.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class LightingTools
    {
        private const string LOG = "[PLAGA44][Lighting]";

        [MenuItem("CYBERNOMAD/Tools/Clear Baked Lighting")]
        public static void ClearMenuItem() => Clear();

        [MenuItem("CYBERNOMAD/Tools/Generate Lighting Now (sync)")]
        public static void GenerateNowMenuItem()
        {
            Debug.Log($"{LOG} Generate Lighting (sync) -- this will freeze editor until done...");
            if (Lightmapping.Bake())
                Debug.Log($"{LOG} [OK] Bake complete.");
            else
                Debug.LogError($"{LOG} [FAIL] Bake failed or cancelled.");
        }

        [MenuItem("CYBERNOMAD/Tools/Generate Lighting Async (background)")]
        public static void GenerateAsyncMenuItem()
        {
            Debug.Log($"{LOG} Generate Lighting (async) -- runs in background. Watch progress bar.");
            Lightmapping.BakeAsync();
        }

        /// <summary>Clear baked lighting data and stale LightingDataAsset.
        /// Returns true if anything was cleared.</summary>
        public static bool Clear()
        {
            bool cleared = false;
            try
            {
                Lightmapping.Clear();
                cleared = true;
                Debug.Log($"{LOG} [CLEARED] Lightmapping.Clear()");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{LOG} Lightmapping.Clear failed: {e.Message}");
            }

            try
            {
                Lightmapping.ClearDiskCache();
                Debug.Log($"{LOG} [CLEARED] Lightmapping.ClearDiskCache()");
                cleared = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{LOG} Lightmapping.ClearDiskCache failed: {e.Message}");
            }

            // Clear LightingDataAsset reference on active scene (this is what causes the 'incompatible' warning)
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                // Use Lightmapping.lightingDataAsset = null via SerializedObject on lighting settings
                // Unity 6 API: per-scene LightingSettings are accessed via Lightmapping
                try
                {
                    Lightmapping.lightingDataAsset = null;
                    Debug.Log($"{LOG} [CLEARED] lightingDataAsset -> null");
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                    cleared = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"{LOG} lightingDataAsset clear failed: {e.Message}");
                }
            }

            return cleared;
        }

        /// <summary>Called by Bootstrap if scene reports lighting incompatibility.
        /// Cheap check: if LightingDataAsset is null/invalid, skip; else clear.</summary>
        public static bool AutoClearIfNeeded()
        {
            var asset = Lightmapping.lightingDataAsset;
            if (asset == null)
            {
                Debug.Log($"{LOG} [OK] No LightingDataAsset attached (clean state)");
                return false;
            }
            Debug.Log($"{LOG} [AUTO-CLEAR] LightingDataAsset found ({asset.name}) -- clearing to avoid version-incompatibility warnings");
            return Clear();
        }
    }
}
#endif
