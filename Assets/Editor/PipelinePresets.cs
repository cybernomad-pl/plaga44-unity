// PipelinePresets.cs -- CYBERNOMAD Editor Tool
//
// Menu: CYBERNOMAD > Pipeline Preset > [preset name]
//
// Switches the active URP Render Pipeline Asset on the "Mobile" quality level.
// Each preset is a separate .asset file in Assets/Settings/.
//
// To add a new preset:
//   1. Duplicate an existing pipeline asset in Assets/Settings/
//   2. Rename it (e.g. PIPELINE_HIEND.asset)
//   3. Tweak values in Inspector
//   4. Add a MenuItem below pointing to it
//
// Active preset applies to Android builds (Quest).
// PC quality level keeps PC_RPAsset unchanged.

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Plaga44.Editor
{
    public static class PipelinePresets
    {
        private const string LOG = "[PLAGA44]";
        private const string SETTINGS_DIR = "Assets/Settings/";

        // =====================================================================
        // Presets
        // =====================================================================

        [MenuItem("CYBERNOMAD/Pipeline Preset/INITIAL (Quest optimized)", false, 1)]
        public static void ApplyInitial()
        {
            ApplyPreset("PIPELINE_INITIAL");
        }

        [MenuItem("CYBERNOMAD/Pipeline Preset/DEFAULT (URP template)", false, 2)]
        public static void ApplyDefault()
        {
            ApplyPreset("Mobile_RPAsset");
        }

        // Add more presets here:
        // [MenuItem("CYBERNOMAD/Pipeline Preset/HIEND (max quality)", false, 3)]
        // public static void ApplyHiEnd() => ApplyPreset("PIPELINE_HIEND");

        // [MenuItem("CYBERNOMAD/Pipeline Preset/SAFE (min spec)", false, 4)]
        // public static void ApplySafe() => ApplyPreset("PIPELINE_SAFE");

        // =====================================================================
        // Show current preset
        // =====================================================================

        [MenuItem("CYBERNOMAD/Pipeline Preset/-- Show Active --", false, 100)]
        public static void ShowActive()
        {
            int mobileIndex = FindMobileQualityIndex();
            if (mobileIndex < 0)
            {
                Debug.LogError($"{LOG} Mobile quality level not found.");
                return;
            }

            var current = QualitySettings.GetRenderPipelineAssetAt(mobileIndex);
            if (current != null)
            {
                string path = AssetDatabase.GetAssetPath(current);
                Debug.Log($"{LOG} Active pipeline preset: {current.name} ({path})");
            }
            else
            {
                Debug.LogWarning($"{LOG} No pipeline asset assigned to Mobile quality level.");
            }
        }

        // =====================================================================
        // Core
        // =====================================================================

        static void ApplyPreset(string assetName)
        {
            string path = SETTINGS_DIR + assetName + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);

            if (asset == null)
            {
                Debug.LogError($"{LOG} Pipeline asset not found: {path}");
                return;
            }

            int mobileIndex = FindMobileQualityIndex();
            if (mobileIndex < 0)
            {
                Debug.LogError($"{LOG} Mobile quality level not found in QualitySettings.");
                return;
            }

            QualitySettings.SetQualityLevel(mobileIndex, applyExpensiveChanges: true);
            QualitySettings.renderPipeline = asset;

            Debug.Log($"{LOG} Pipeline preset applied: {assetName}");
            Debug.Log($"{LOG}   HDR: {(asset as UniversalRenderPipelineAsset)?.supportsHDR}");
            Debug.Log($"{LOG}   MSAA: {(asset as UniversalRenderPipelineAsset)?.msaaSampleCount}x");
            Debug.Log($"{LOG}   Render Scale: {(asset as UniversalRenderPipelineAsset)?.renderScale}");
            Debug.Log($"{LOG}   Shadow Distance: {(asset as UniversalRenderPipelineAsset)?.shadowDistance}");
        }

        static int FindMobileQualityIndex()
        {
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == "Mobile") return i;
            }
            return -1;
        }
    }
}
