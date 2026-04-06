// Pipeline.cs -- CYBERNOMAD Editor Tool
//
// Dwa pipeline assety: VR (Mobile_RPAsset) i PC (PC_RPAsset).
// Wspolny struct PipelineSettings, osobne klasy VRPipeline / PCPipeline.
//
// Public API:
//   VRPipeline.Apply(VRPipeline.INITIAL);
//   VRPipeline.SetMSAA(8);
//   PCPipeline.Apply(PCPipeline.INITIAL);
//   PCPipeline.SetShadowDistance(100f);

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Plaga44.Editor
{
    // =========================================================================
    // Wspolny struct ustawien -- ten sam dla VR i PC
    // =========================================================================

    public struct PipelineSettings
    {
        public bool hdr;
        public int msaa;                    // 1, 2, 4, 8
        public float renderScale;           // 0.5 - 1.5
        public float shadowDistance;
        public int mainShadowResolution;    // 256, 512, 1024, 2048, 4096
        public int addShadowResolution;
        public int addLightsPerObject;
        public bool reflectionProbeBlending;
        public bool reflectionProbeBoxProjection;
        public bool lightLayers;
        public bool lensFlareData;
        public bool lensFlareScreenSpace;
        public int colorGradingLutSize;     // 16, 32
        public bool softShadows;
    }

    // =========================================================================
    // VR Pipeline (Mobile_RPAsset) -- Quest
    // =========================================================================

    public static class VRPipeline
    {
        private const string LOG = "[PLAGA44/VR]";
        private const string ASSET_PATH = "Assets/Settings/Mobile_RPAsset.asset";

        public static readonly PipelineSettings INITIAL = new PipelineSettings
        {
            hdr                         = false,
            msaa                        = 4,
            renderScale                 = 1.0f,
            shadowDistance               = 20f,
            mainShadowResolution        = 1024,
            addShadowResolution         = 512,
            addLightsPerObject          = 2,
            reflectionProbeBlending     = false,
            reflectionProbeBoxProjection = false,
            lightLayers                 = false,
            lensFlareData               = false,
            lensFlareScreenSpace        = false,
            colorGradingLutSize         = 16,
            softShadows                 = false,
        };

        public static readonly PipelineSettings DEFAULT = new PipelineSettings
        {
            hdr                         = true,
            msaa                        = 1,
            renderScale                 = 0.8f,
            shadowDistance               = 50f,
            mainShadowResolution        = 1024,
            addShadowResolution         = 2048,
            addLightsPerObject          = 4,
            reflectionProbeBlending     = true,
            reflectionProbeBoxProjection = true,
            lightLayers                 = true,
            lensFlareData               = true,
            lensFlareScreenSpace        = true,
            colorGradingLutSize         = 32,
            softShadows                 = false,
        };

        public static bool Apply(PipelineSettings s) => PipelineCore.Apply(ASSET_PATH, s, LOG);
        public static void LogCurrent() => PipelineCore.LogCurrent(ASSET_PATH, LOG);

        public static void SetMSAA(int v) => PipelineCore.Tweak(ASSET_PATH, "m_MSAA", v, LOG);
        public static void SetHDR(bool v) => PipelineCore.Tweak(ASSET_PATH, "m_SupportsHDR", v, LOG);
        public static void SetRenderScale(float v) => PipelineCore.Tweak(ASSET_PATH, "m_RenderScale", v, LOG);
        public static void SetShadowDistance(float v) => PipelineCore.Tweak(ASSET_PATH, "m_ShadowDistance", v, LOG);
        public static void SetShadowResolution(int v) => PipelineCore.Tweak(ASSET_PATH, "m_MainLightShadowmapResolution", v, LOG);
        public static void SetSoftShadows(bool v) => PipelineCore.Tweak(ASSET_PATH, "m_SoftShadowsSupported", v, LOG);
        public static void SetAdditionalLights(int v) => PipelineCore.Tweak(ASSET_PATH, "m_AdditionalLightsPerObjectLimit", v, LOG);
        public static void SetValue(string field, int v) => PipelineCore.Tweak(ASSET_PATH, field, v, LOG);
        public static void SetValue(string field, float v) => PipelineCore.Tweak(ASSET_PATH, field, v, LOG);
        public static void SetValue(string field, bool v) => PipelineCore.Tweak(ASSET_PATH, field, v, LOG);

        [MenuItem("CYBERNOMAD/Presets/Quest/VR Pipeline INITIAL", false, 1)]
        static void MenuInitial() => Apply(INITIAL);
        [MenuItem("CYBERNOMAD/Presets/Quest/VR Pipeline DEFAULT", false, 2)]
        static void MenuDefault() => Apply(DEFAULT);
        [MenuItem("CYBERNOMAD/Status/VR Pipeline", false, 100)]
        static void MenuShow() => LogCurrent();
    }

    // =========================================================================
    // PC Pipeline (PC_RPAsset) -- Editor / Standalone
    // =========================================================================

    public static class PCPipeline
    {
        private const string LOG = "[PLAGA44/PC]";
        private const string ASSET_PATH = "Assets/Settings/PC_RPAsset.asset";

        public static readonly PipelineSettings INITIAL = new PipelineSettings
        {
            hdr                         = true,
            msaa                        = 4,
            renderScale                 = 1.0f,
            shadowDistance               = 50f,
            mainShadowResolution        = 2048,
            addShadowResolution         = 1024,
            addLightsPerObject          = 4,
            reflectionProbeBlending     = true,
            reflectionProbeBoxProjection = true,
            lightLayers                 = true,
            lensFlareData               = true,
            lensFlareScreenSpace        = true,
            colorGradingLutSize         = 32,
            softShadows                 = true,
        };

        public static readonly PipelineSettings DEFAULT = new PipelineSettings
        {
            hdr                         = true,
            msaa                        = 1,
            renderScale                 = 1.0f,
            shadowDistance               = 50f,
            mainShadowResolution        = 2048,
            addShadowResolution         = 2048,
            addLightsPerObject          = 4,
            reflectionProbeBlending     = true,
            reflectionProbeBoxProjection = true,
            lightLayers                 = true,
            lensFlareData               = true,
            lensFlareScreenSpace        = true,
            colorGradingLutSize         = 32,
            softShadows                 = false,
        };

        public static bool Apply(PipelineSettings s) => PipelineCore.Apply(ASSET_PATH, s, LOG);
        public static void LogCurrent() => PipelineCore.LogCurrent(ASSET_PATH, LOG);

        public static void SetMSAA(int v) => PipelineCore.Tweak(ASSET_PATH, "m_MSAA", v, LOG);
        public static void SetHDR(bool v) => PipelineCore.Tweak(ASSET_PATH, "m_SupportsHDR", v, LOG);
        public static void SetRenderScale(float v) => PipelineCore.Tweak(ASSET_PATH, "m_RenderScale", v, LOG);
        public static void SetShadowDistance(float v) => PipelineCore.Tweak(ASSET_PATH, "m_ShadowDistance", v, LOG);
        public static void SetShadowResolution(int v) => PipelineCore.Tweak(ASSET_PATH, "m_MainLightShadowmapResolution", v, LOG);
        public static void SetSoftShadows(bool v) => PipelineCore.Tweak(ASSET_PATH, "m_SoftShadowsSupported", v, LOG);
        public static void SetAdditionalLights(int v) => PipelineCore.Tweak(ASSET_PATH, "m_AdditionalLightsPerObjectLimit", v, LOG);
        public static void SetValue(string field, int v) => PipelineCore.Tweak(ASSET_PATH, field, v, LOG);
        public static void SetValue(string field, float v) => PipelineCore.Tweak(ASSET_PATH, field, v, LOG);
        public static void SetValue(string field, bool v) => PipelineCore.Tweak(ASSET_PATH, field, v, LOG);

        [MenuItem("CYBERNOMAD/Presets/PC/Pipeline INITIAL", false, 1)]
        static void MenuInitial() => Apply(INITIAL);
        [MenuItem("CYBERNOMAD/Presets/PC/Pipeline DEFAULT", false, 2)]
        static void MenuDefault() => Apply(DEFAULT);
        [MenuItem("CYBERNOMAD/Status/PC Pipeline", false, 100)]
        static void MenuShow() => LogCurrent();
    }

    // =========================================================================
    // Wspolna logika -- nie duplikujemy kodu
    // =========================================================================

    internal static class PipelineCore
    {
        public static bool Apply(string assetPath, PipelineSettings s, string log)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null) { Debug.LogError($"{log} Asset not found: {assetPath}"); return false; }

            var so = new SerializedObject(asset);
            Set(so, "m_SupportsHDR",                        s.hdr);
            Set(so, "m_MSAA",                                s.msaa);
            Set(so, "m_RenderScale",                         s.renderScale);
            Set(so, "m_ShadowDistance",                      s.shadowDistance);
            Set(so, "m_MainLightShadowmapResolution",       s.mainShadowResolution);
            Set(so, "m_AdditionalLightsShadowmapResolution", s.addShadowResolution);
            Set(so, "m_AdditionalLightsPerObjectLimit",      s.addLightsPerObject);
            Set(so, "m_ReflectionProbeBlending",             s.reflectionProbeBlending);
            Set(so, "m_ReflectionProbeBoxProjection",        s.reflectionProbeBoxProjection);
            Set(so, "m_SupportsLightLayers",                 s.lightLayers);
            Set(so, "m_SupportDataDrivenLensFlare",          s.lensFlareData);
            Set(so, "m_SupportScreenSpaceLensFlare",         s.lensFlareScreenSpace);
            Set(so, "m_ColorGradingLutSize",                 s.colorGradingLutSize);
            Set(so, "m_SoftShadowsSupported",                s.softShadows);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            Debug.Log($"{log} Applied to {asset.name}: HDR={s.hdr} MSAA={s.msaa}x " +
                      $"Scale={s.renderScale} Shadow={s.shadowDistance}m SoftShadow={s.softShadows}");
            return true;
        }

        public static void LogCurrent(string assetPath, string log)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null) { Debug.LogError($"{log} Asset not found: {assetPath}"); return; }
            Debug.Log($"{log} {asset.name}: HDR={asset.supportsHDR} MSAA={asset.msaaSampleCount}x " +
                      $"Scale={asset.renderScale} Shadow={asset.shadowDistance}m");
        }

        public static void Tweak(string assetPath, string field, int value, string log)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null) { Debug.LogError($"{log} Asset not found: {assetPath}"); return; }
            var so = new SerializedObject(asset);
            Set(so, field, value); so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            Debug.Log($"{log} {field}={value}");
        }

        public static void Tweak(string assetPath, string field, float value, string log)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null) { Debug.LogError($"{log} Asset not found: {assetPath}"); return; }
            var so = new SerializedObject(asset);
            Set(so, field, value); so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            Debug.Log($"{log} {field}={value}");
        }

        public static void Tweak(string assetPath, string field, bool value, string log)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null) { Debug.LogError($"{log} Asset not found: {assetPath}"); return; }
            var so = new SerializedObject(asset);
            Set(so, field, value); so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            Debug.Log($"{log} {field}={value}");
        }

        static void Set(SerializedObject so, string f, bool v)  { var p = so.FindProperty(f); if (p != null) p.boolValue = v; }
        static void Set(SerializedObject so, string f, int v)   { var p = so.FindProperty(f); if (p != null) p.intValue = v; }
        static void Set(SerializedObject so, string f, float v) { var p = so.FindProperty(f); if (p != null) p.floatValue = v; }
    }
}
