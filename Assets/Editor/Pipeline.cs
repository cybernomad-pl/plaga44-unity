// Pipeline.cs -- CYBERNOMAD Editor Tool
//
// Jeden pipeline asset (Mobile_RPAsset) -- zmienia mu wartosci on-the-fly.
// Presety to zestawy ustawien w kodzie, nie osobne pliki.
//
// Public API:
//   Pipeline.Apply(Pipeline.INITIAL);
//   Pipeline.Apply(new PipelineSettings { hdr = false, msaa = 4, ... });
//   Pipeline.LogCurrent();
//
// Menu: CYBERNOMAD > Pipeline > Apply INITIAL / Apply DEFAULT / Show Current

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Plaga44.Editor
{
    // =========================================================================
    // Settings struct -- przekazujesz caly zestaw naraz
    // =========================================================================

    public struct PipelineSettings
    {
        public bool hdr;
        public int msaa;                    // 1, 2, 4
        public float renderScale;           // 0.5 - 1.5
        public float shadowDistance;
        public int mainShadowResolution;    // 256, 512, 1024, 2048, 4096
        public int addShadowResolution;
        public int addLightsPerObject;      // max additional lights per object
        public bool reflectionProbeBlending;
        public bool reflectionProbeBoxProjection;
        public bool lightLayers;
        public bool lensFlareData;
        public bool lensFlareScreenSpace;
        public int colorGradingLutSize;     // 16, 32
        public bool softShadows;
    }

    // =========================================================================
    // Presets + applicator
    // =========================================================================

    public static class Pipeline
    {
        private const string LOG = "[PLAGA44]";
        private const string ASSET_PATH = "Assets/Settings/Mobile_RPAsset.asset";

        // ---------------------------------------------------------------------
        // Predefiniowane zestawy -- dodawaj kolejne tutaj
        // ---------------------------------------------------------------------

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

        // ---------------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------------

        /// <summary>
        /// Aplikuje zestaw ustawien do Mobile_RPAsset. Przekazujesz gotowy struct.
        /// Przyklad: Pipeline.Apply(Pipeline.INITIAL);
        /// Przyklad: Pipeline.Apply(new PipelineSettings { hdr = false, msaa = 8 });
        /// </summary>
        public static bool Apply(PipelineSettings s)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ASSET_PATH);
            if (asset == null)
            {
                Debug.LogError($"{LOG} Pipeline asset not found: {ASSET_PATH}");
                return false;
            }

            var so = new SerializedObject(asset);

            Set(so, "m_SupportsHDR",                      s.hdr);
            Set(so, "m_MSAA",                              s.msaa);
            Set(so, "m_RenderScale",                       s.renderScale);
            Set(so, "m_ShadowDistance",                    s.shadowDistance);
            Set(so, "m_MainLightShadowmapResolution",     s.mainShadowResolution);
            Set(so, "m_AdditionalLightsShadowmapResolution", s.addShadowResolution);
            Set(so, "m_AdditionalLightsPerObjectLimit",    s.addLightsPerObject);
            Set(so, "m_ReflectionProbeBlending",           s.reflectionProbeBlending);
            Set(so, "m_ReflectionProbeBoxProjection",      s.reflectionProbeBoxProjection);
            Set(so, "m_SupportsLightLayers",               s.lightLayers);
            Set(so, "m_SupportDataDrivenLensFlare",        s.lensFlareData);
            Set(so, "m_SupportScreenSpaceLensFlare",       s.lensFlareScreenSpace);
            Set(so, "m_ColorGradingLutSize",               s.colorGradingLutSize);
            Set(so, "m_SoftShadowsSupported",              s.softShadows);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LOG} Pipeline settings applied to {asset.name}:");
            Debug.Log($"{LOG}   HDR={s.hdr} MSAA={s.msaa}x Scale={s.renderScale} " +
                      $"Shadow={s.shadowDistance}m MainShadRes={s.mainShadowResolution} " +
                      $"AddShadRes={s.addShadowResolution} AddLights={s.addLightsPerObject}");
            return true;
        }

        /// <summary>Loguje aktualne wartosci Mobile_RPAsset.</summary>
        public static void LogCurrent()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ASSET_PATH);
            if (asset == null)
            {
                Debug.LogError($"{LOG} Pipeline asset not found: {ASSET_PATH}");
                return;
            }

            Debug.Log($"{LOG} Current pipeline ({asset.name}):");
            Debug.Log($"{LOG}   HDR={asset.supportsHDR} MSAA={asset.msaaSampleCount}x " +
                      $"Scale={asset.renderScale} Shadow={asset.shadowDistance}m");
        }

        // ---------------------------------------------------------------------
        // Single value tweaks -- zmiana jednej wartosci on-demand
        // ---------------------------------------------------------------------

        /// <summary>Zmien dowolne pole pipeline. Nazwa pola jak w YAML (np. "m_MSAA").</summary>
        public static void SetValue(string field, int value) => Tweak(field, so => Set(so, field, value));
        public static void SetValue(string field, float value) => Tweak(field, so => Set(so, field, value));
        public static void SetValue(string field, bool value) => Tweak(field, so => Set(so, field, value));

        // Wygodne aliasy -- najczesciej uzywane
        public static void SetMSAA(int value) => Tweak("MSAA", so => Set(so, "m_MSAA", value));
        public static void SetHDR(bool value) => Tweak("HDR", so => Set(so, "m_SupportsHDR", value));
        public static void SetRenderScale(float value) => Tweak("RenderScale", so => Set(so, "m_RenderScale", value));
        public static void SetShadowDistance(float value) => Tweak("ShadowDistance", so => Set(so, "m_ShadowDistance", value));
        public static void SetShadowResolution(int value) => Tweak("MainShadowRes", so => Set(so, "m_MainLightShadowmapResolution", value));
        public static void SetSoftShadows(bool value) => Tweak("SoftShadows", so => Set(so, "m_SoftShadowsSupported", value));
        public static void SetAdditionalLights(int value) => Tweak("AddLights", so => Set(so, "m_AdditionalLightsPerObjectLimit", value));

        static void Tweak(string label, System.Action<SerializedObject> action)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ASSET_PATH);
            if (asset == null) { Debug.LogError($"{LOG} Asset not found: {ASSET_PATH}"); return; }

            var so = new SerializedObject(asset);
            action(so);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            Debug.Log($"{LOG} Pipeline tweak: {label}");
        }

        // ---------------------------------------------------------------------
        // Menu
        // ---------------------------------------------------------------------

        [MenuItem("CYBERNOMAD/Pipeline/Apply INITIAL", false, 1)]
        static void MenuInitial() => Apply(INITIAL);

        [MenuItem("CYBERNOMAD/Pipeline/Apply DEFAULT", false, 2)]
        static void MenuDefault() => Apply(DEFAULT);

        [MenuItem("CYBERNOMAD/Pipeline/Show Current", false, 100)]
        static void MenuShow() => LogCurrent();

        // ---------------------------------------------------------------------
        // SerializedObject helpers
        // ---------------------------------------------------------------------

        static void Set(SerializedObject so, string field, bool value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.boolValue = value;
        }

        static void Set(SerializedObject so, string field, int value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.intValue = value;
        }

        static void Set(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
        }
    }
}
