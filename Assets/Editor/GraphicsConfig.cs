// GraphicsConfig.cs -- CYBERNOMAD Editor Tool
//
// Steruje: ProjectSettings/GraphicsSettings.asset
//
// Public API:
//   GraphicsConfig.Apply(GraphicsConfig.INITIAL);
//   GraphicsConfig.SetFogStripping(1);
//   GraphicsConfig.LogCurrent();

using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public struct GraphicsSettings_
    {
        public int transparencySortMode;    // 0=Default, 1=Perspective, 2=Orthographic, 3=CustomAxis
        public int lightmapStripping;       // 0=Automatic, 1=Custom
        public int fogStripping;            // 0=Automatic, 1=Custom
        public int instancingStripping;     // 0=StripUnused, 1=StripAll
        public int brgStripping;            // 0=Automatic, 1=Custom
        public int videoShadersIncludeMode; // 0=Never, 1=Referenced, 2=Always
        public int preloadShadersBatchTimeLimit; // ms, -1 = no limit
    }

    public static class GraphicsConfig
    {
        private const string LOG = "[PLAGA44]";
        private const string ASSET = "ProjectSettings/GraphicsSettings.asset";

        // ---------------------------------------------------------------------
        // Presety
        // ---------------------------------------------------------------------

        public static readonly GraphicsSettings_ INITIAL = new GraphicsSettings_
        {
            transparencySortMode       = 0,     // Default
            lightmapStripping          = 0,     // Automatic
            fogStripping               = 0,     // Automatic
            instancingStripping        = 0,     // StripUnused
            brgStripping               = 0,     // Automatic
            videoShadersIncludeMode    = 0,     // Never (VR nie uzywa video shaderow)
            preloadShadersBatchTimeLimit = 50,   // 50ms limit (default -1 = stall)
        };

        public static readonly GraphicsSettings_ DEFAULT = new GraphicsSettings_
        {
            transparencySortMode       = 0,
            lightmapStripping          = 0,
            fogStripping               = 0,
            instancingStripping        = 0,
            brgStripping               = 0,
            videoShadersIncludeMode    = 2,     // Always
            preloadShadersBatchTimeLimit = -1,
        };

        // ---------------------------------------------------------------------
        // Apply
        // ---------------------------------------------------------------------

        public static void Apply(GraphicsSettings_ s)
        {
            var so = LoadAsset();
            if (so == null) return;

            Set(so, "m_TransparencySortMode", s.transparencySortMode);
            Set(so, "m_LightmapStripping", s.lightmapStripping);
            Set(so, "m_FogStripping", s.fogStripping);
            Set(so, "m_InstancingStripping", s.instancingStripping);
            Set(so, "m_BrgStripping", s.brgStripping);
            Set(so, "m_VideoShadersIncludeMode", s.videoShadersIncludeMode);
            Set(so, "m_PreloadShadersBatchTimeLimit", s.preloadShadersBatchTimeLimit);

            so.ApplyModifiedProperties();
            Debug.Log($"{LOG} Graphics applied: videoShaders={s.videoShadersIncludeMode} " +
                      $"shaderPreloadLimit={s.preloadShadersBatchTimeLimit}ms " +
                      $"fogStrip={s.fogStripping} instancingStrip={s.instancingStripping}");
        }

        // ---------------------------------------------------------------------
        // Single value
        // ---------------------------------------------------------------------

        public static void SetTransparencySortMode(int v) => Tweak("m_TransparencySortMode", v);
        public static void SetLightmapStripping(int v)    => Tweak("m_LightmapStripping", v);
        public static void SetFogStripping(int v)         => Tweak("m_FogStripping", v);
        public static void SetInstancingStripping(int v)  => Tweak("m_InstancingStripping", v);
        public static void SetVideoShaders(int v)         => Tweak("m_VideoShadersIncludeMode", v);
        public static void SetShaderPreloadLimit(int ms)  => Tweak("m_PreloadShadersBatchTimeLimit", ms);

        // ---------------------------------------------------------------------
        // Log
        // ---------------------------------------------------------------------

        public static void LogCurrent()
        {
            var so = LoadAsset();
            if (so == null) return;
            Debug.Log($"{LOG} Graphics: videoShaders={GetInt(so, "m_VideoShadersIncludeMode")} " +
                      $"shaderPreload={GetInt(so, "m_PreloadShadersBatchTimeLimit")}ms " +
                      $"fogStrip={GetInt(so, "m_FogStripping")} " +
                      $"instStrip={GetInt(so, "m_InstancingStripping")}");
        }

        // ---------------------------------------------------------------------
        // Menu
        // ---------------------------------------------------------------------

        [MenuItem("CYBERNOMAD/Presets/Quest/Graphics INITIAL", false, 1)]
        static void MenuInitial() => Apply(INITIAL);
        [MenuItem("CYBERNOMAD/Status/Graphics", false, 100)]
        static void MenuShow() => LogCurrent();

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        static SerializedObject LoadAsset()
        {
            var obj = AssetDatabase.LoadAllAssetsAtPath(ASSET);
            if (obj == null || obj.Length == 0) { Debug.LogError($"{LOG} {ASSET} not found"); return null; }
            return new SerializedObject(obj[0]);
        }

        static void Tweak(string field, int value)
        {
            var so = LoadAsset(); if (so == null) return;
            Set(so, field, value); so.ApplyModifiedProperties();
            Debug.Log($"{LOG} Graphics tweak: {field}={value}");
        }

        static void Set(SerializedObject so, string f, int v) { var p = so.FindProperty(f); if (p != null) p.intValue = v; }
        static int GetInt(SerializedObject so, string f)       { var p = so.FindProperty(f); return p?.intValue ?? -1; }
    }
}
