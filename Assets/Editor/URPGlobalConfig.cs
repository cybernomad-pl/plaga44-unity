// URPGlobalConfig.cs -- CYBERNOMAD Editor Tool
//
// Steruje: Assets/Settings/UniversalRenderPipelineGlobalSettings.asset
//
// Public API:
//   URPGlobalConfig.Apply(URPGlobalConfig.INITIAL);
//   URPGlobalConfig.SetStripUnusedVariants(true);
//   URPGlobalConfig.SetRenderingLayerName(0, "Main Light");
//   URPGlobalConfig.LogCurrent();

using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public struct URPGlobalSettings
    {
        public bool stripUnusedVariants;
        public bool stripUnusedPostProcessing;
        public bool stripDebugShaders;
        public bool stripScreenCoordOverride;
        public bool renderCompatibilityMode;    // false = new Render Graph
        public int shaderVariantLogLevel;       // 0=Disabled, 1=OnlySRPShaders, 2=AllShaders
        public bool exportShaderVariants;
        public string[] renderingLayerNames;    // 8 nazw (indices 0-7)
    }

    public static class URPGlobalConfig
    {
        private const string LOG = "[PLAGA44]";
        private const string ASSET_PATH = "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset";

        // ---------------------------------------------------------------------
        // Presety
        // ---------------------------------------------------------------------

        public static readonly URPGlobalSettings INITIAL = new URPGlobalSettings
        {
            stripUnusedVariants         = true,
            stripUnusedPostProcessing   = true,
            stripDebugShaders           = true,
            stripScreenCoordOverride    = true,
            renderCompatibilityMode     = false,     // Render Graph ON
            shaderVariantLogLevel       = 0,         // Disabled
            exportShaderVariants        = false,     // nie eksportuj do pliku
            renderingLayerNames         = new[] {
                "Default", "Characters", "Environment", "VFX",
                "UI", "Unused5", "Unused6", "Unused7"
            },
        };

        public static readonly URPGlobalSettings DEBUG = new URPGlobalSettings
        {
            stripUnusedVariants         = false,     // wszystkie warianty
            stripUnusedPostProcessing   = false,
            stripDebugShaders           = false,     // debug shadery dostepne
            stripScreenCoordOverride    = false,
            renderCompatibilityMode     = false,
            shaderVariantLogLevel       = 2,         // AllShaders
            exportShaderVariants        = true,      // eksport do analizy
            renderingLayerNames         = null,      // nie zmieniaj
        };

        // ---------------------------------------------------------------------
        // Apply all
        // ---------------------------------------------------------------------

        public static bool Apply(URPGlobalSettings s)
        {
            var so = LoadAsset();
            if (so == null) return false;

            // Shader stripping -- nowy system (nested)
            SetNested(so, "m_URPShaderStrippingSetting", "m_StripUnusedVariants", s.stripUnusedVariants);
            SetNested(so, "m_URPShaderStrippingSetting", "m_StripUnusedPostProcessingVariants", s.stripUnusedPostProcessing);
            SetNested(so, "m_URPShaderStrippingSetting", "m_StripScreenCoordOverrideVariants", s.stripScreenCoordOverride);
            SetNested(so, "m_ShaderStrippingSetting", "m_StripRuntimeDebugShaders", s.stripDebugShaders);
            SetNested(so, "m_ShaderStrippingSetting", "m_ShaderVariantLogLevel", s.shaderVariantLogLevel);
            SetNested(so, "m_ShaderStrippingSetting", "m_ExportShaderVariants", s.exportShaderVariants);

            // Legacy fallback fields (some URP versions use these)
            Set(so, "m_StripUnusedVariants", s.stripUnusedVariants);
            Set(so, "m_StripUnusedPostProcessingVariants", s.stripUnusedPostProcessing);
            Set(so, "m_StripDebugVariants", s.stripDebugShaders);
            Set(so, "m_StripScreenCoordOverrideVariants", s.stripScreenCoordOverride);
            Set(so, "m_ShaderVariantLogLevel", s.shaderVariantLogLevel);
            Set(so, "m_ExportShaderVariants", s.exportShaderVariants);

            // Render Graph compatibility
            Set(so, "m_EnableRenderGraph", !s.renderCompatibilityMode);

            // Rendering layer names
            if (s.renderingLayerNames != null)
            {
                var layerNames = so.FindProperty("m_RenderingLayerNames");
                if (layerNames != null && layerNames.isArray)
                {
                    for (int i = 0; i < s.renderingLayerNames.Length && i < layerNames.arraySize; i++)
                        layerNames.GetArrayElementAtIndex(i).stringValue = s.renderingLayerNames[i];
                }
                // Also set legacy light layer names
                for (int i = 0; i < s.renderingLayerNames.Length && i < 8; i++)
                    Set(so, $"lightLayerName{i}", s.renderingLayerNames[i]);
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(so.targetObject);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LOG} URP Global applied: stripVariants={s.stripUnusedVariants} " +
                      $"stripDebug={s.stripDebugShaders} renderGraph={!s.renderCompatibilityMode} " +
                      $"variantLog={s.shaderVariantLogLevel}");
            return true;
        }

        // ---------------------------------------------------------------------
        // Single value
        // ---------------------------------------------------------------------

        public static void SetStripUnusedVariants(bool v)
        {
            Tweak(so => {
                SetNested(so, "m_URPShaderStrippingSetting", "m_StripUnusedVariants", v);
                Set(so, "m_StripUnusedVariants", v);
            }, $"stripUnusedVariants={v}");
        }

        public static void SetStripPostProcessing(bool v)
        {
            Tweak(so => {
                SetNested(so, "m_URPShaderStrippingSetting", "m_StripUnusedPostProcessingVariants", v);
                Set(so, "m_StripUnusedPostProcessingVariants", v);
            }, $"stripPostProcessing={v}");
        }

        public static void SetStripDebugShaders(bool v)
        {
            Tweak(so => {
                SetNested(so, "m_ShaderStrippingSetting", "m_StripRuntimeDebugShaders", v);
                Set(so, "m_StripDebugVariants", v);
            }, $"stripDebug={v}");
        }

        public static void SetRenderCompatibilityMode(bool v)
        {
            Tweak(so => Set(so, "m_EnableRenderGraph", !v), $"renderCompatibility={v} (renderGraph={!v})");
        }

        public static void SetShaderVariantLog(int level)
        {
            Tweak(so => {
                SetNested(so, "m_ShaderStrippingSetting", "m_ShaderVariantLogLevel", level);
                Set(so, "m_ShaderVariantLogLevel", level);
            }, $"variantLog={level}");
        }

        public static void SetRenderingLayerName(int index, string name)
        {
            if (index < 0 || index > 7) return;
            Tweak(so => {
                var layerNames = so.FindProperty("m_RenderingLayerNames");
                if (layerNames != null && layerNames.isArray && index < layerNames.arraySize)
                    layerNames.GetArrayElementAtIndex(index).stringValue = name;
                Set(so, $"lightLayerName{index}", name);
            }, $"renderingLayer[{index}]={name}");
        }

        // ---------------------------------------------------------------------
        // Log
        // ---------------------------------------------------------------------

        public static void LogCurrent()
        {
            var so = LoadAsset();
            if (so == null) return;

            Debug.Log($"{LOG} URP Global:");
            Debug.Log($"{LOG}   stripVariants={GetBool(so, "m_StripUnusedVariants")} " +
                      $"stripPostProcess={GetBool(so, "m_StripUnusedPostProcessingVariants")} " +
                      $"stripDebug={GetBool(so, "m_StripDebugVariants")}");
            Debug.Log($"{LOG}   renderGraph={GetBool(so, "m_EnableRenderGraph")} " +
                      $"variantLog={GetInt(so, "m_ShaderVariantLogLevel")} " +
                      $"exportVariants={GetBool(so, "m_ExportShaderVariants")}");

            var layerNames = so.FindProperty("m_RenderingLayerNames");
            if (layerNames != null && layerNames.isArray)
            {
                string layers = "";
                for (int i = 0; i < layerNames.arraySize && i < 8; i++)
                    layers += $"[{i}]{layerNames.GetArrayElementAtIndex(i).stringValue} ";
                Debug.Log($"{LOG}   renderingLayers: {layers}");
            }
        }

        // ---------------------------------------------------------------------
        // Menu
        // ---------------------------------------------------------------------

        [MenuItem("CYBERNOMAD/URP Global/Apply INITIAL", false, 1)]
        static void MenuInitial() => Apply(INITIAL);

        [MenuItem("CYBERNOMAD/URP Global/Apply DEBUG", false, 2)]
        static void MenuDebug() => Apply(DEBUG);

        [MenuItem("CYBERNOMAD/URP Global/Show Current", false, 100)]
        static void MenuShow() => LogCurrent();

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        static SerializedObject LoadAsset()
        {
            var obj = AssetDatabase.LoadAssetAtPath<Object>(ASSET_PATH);
            if (obj == null) { Debug.LogError($"{LOG} {ASSET_PATH} not found"); return null; }
            return new SerializedObject(obj);
        }

        static void Tweak(System.Action<SerializedObject> action, string label)
        {
            var so = LoadAsset(); if (so == null) return;
            action(so);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(so.targetObject);
            AssetDatabase.SaveAssets();
            Debug.Log($"{LOG} URP Global tweak: {label}");
        }

        static void Set(SerializedObject so, string f, bool v)   { var p = so.FindProperty(f); if (p != null) p.boolValue = v; }
        static void Set(SerializedObject so, string f, int v)    { var p = so.FindProperty(f); if (p != null) p.intValue = v; }
        static void Set(SerializedObject so, string f, string v) { var p = so.FindProperty(f); if (p != null) p.stringValue = v; }

        static void SetNested(SerializedObject so, string parent, string child, bool v)
        {
            var p = so.FindProperty($"{parent}.{child}");
            if (p != null) p.boolValue = v;
        }

        static void SetNested(SerializedObject so, string parent, string child, int v)
        {
            var p = so.FindProperty($"{parent}.{child}");
            if (p != null) p.intValue = v;
        }

        static bool GetBool(SerializedObject so, string f) { var p = so.FindProperty(f); return p?.boolValue ?? false; }
        static int GetInt(SerializedObject so, string f)    { var p = so.FindProperty(f); return p?.intValue ?? -1; }
    }
}
