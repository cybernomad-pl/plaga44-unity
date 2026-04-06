// PipelinePresets.cs -- CYBERNOMAD Editor Tool
//
// Public API: PipelinePresets.ApplyPreset("PIPELINE_INITIAL")
// Menu: CYBERNOMAD > Pipeline Preset > Apply... (dialog)
//
// Switches the URP Render Pipeline Asset on the "Mobile" quality level.
// Presets are .asset files in Assets/Settings/ (any UniversalRenderPipelineAsset).
//
// To add a new preset: duplicate a pipeline asset, tweak in Inspector, done.
// Call ApplyPreset("YourAssetName") from code or pick it from the menu dialog.

using System.Collections.Generic;
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
        // Public API -- call from anywhere
        // =====================================================================

        /// <summary>
        /// Apply a URP pipeline preset by asset name (without .asset extension).
        /// Asset must exist in Assets/Settings/.
        /// Example: PipelinePresets.ApplyPreset("PIPELINE_INITIAL");
        /// </summary>
        public static bool ApplyPreset(string assetName)
        {
            string path = SETTINGS_DIR + assetName + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);

            if (asset == null)
            {
                Debug.LogError($"{LOG} Pipeline asset not found: {path}");
                return false;
            }

            int mobileIndex = FindMobileQualityIndex();
            if (mobileIndex < 0)
            {
                Debug.LogError($"{LOG} 'Mobile' quality level not found in QualitySettings.");
                return false;
            }

            QualitySettings.SetQualityLevel(mobileIndex, applyExpensiveChanges: true);
            QualitySettings.renderPipeline = asset;

            var urp = asset as UniversalRenderPipelineAsset;
            Debug.Log($"{LOG} Pipeline preset applied: {assetName}");
            if (urp != null)
            {
                Debug.Log($"{LOG}   HDR={urp.supportsHDR} MSAA={urp.msaaSampleCount}x " +
                          $"RenderScale={urp.renderScale} ShadowDist={urp.shadowDistance}m");
            }
            return true;
        }

        /// <summary>
        /// Returns the name of the currently active pipeline asset on the Mobile quality level.
        /// </summary>
        public static string GetActivePresetName()
        {
            int mobileIndex = FindMobileQualityIndex();
            if (mobileIndex < 0) return null;

            var current = QualitySettings.GetRenderPipelineAssetAt(mobileIndex);
            return current != null ? current.name : null;
        }

        /// <summary>
        /// Returns all URP pipeline assets found in Assets/Settings/.
        /// </summary>
        public static List<string> ListAvailablePresets()
        {
            var result = new List<string>();
            string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets/Settings" });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
                if (asset != null) result.Add(asset.name);
            }
            return result;
        }

        // =====================================================================
        // Menu
        // =====================================================================

        [MenuItem("CYBERNOMAD/Pipeline Preset/Apply...", false, 1)]
        static void MenuApply()
        {
            var presets = ListAvailablePresets();
            if (presets.Count == 0)
            {
                Debug.LogError($"{LOG} No pipeline assets found in {SETTINGS_DIR}");
                return;
            }

            string active = GetActivePresetName() ?? "(none)";
            var menu = new GenericMenu();
            foreach (var name in presets)
            {
                bool isCurrent = (name == active);
                menu.AddItem(new GUIContent(name + (isCurrent ? "  [active]" : "")),
                    isCurrent, () => ApplyPreset(name));
            }
            menu.ShowAsContext();
        }

        [MenuItem("CYBERNOMAD/Pipeline Preset/Show Active", false, 2)]
        static void MenuShowActive()
        {
            string name = GetActivePresetName();
            if (name != null)
                Debug.Log($"{LOG} Active pipeline preset: {name}");
            else
                Debug.LogWarning($"{LOG} No pipeline asset assigned to Mobile quality level.");
        }

        // =====================================================================
        // Internal
        // =====================================================================

        static int FindMobileQualityIndex()
        {
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
                if (names[i] == "Mobile") return i;
            return -1;
        }
    }
}
