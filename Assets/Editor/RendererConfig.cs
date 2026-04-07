// RendererConfig.cs -- CYBERNOMAD Editor Tool
//
// Steruje: Mobile_Renderer.asset (VR) i PC_Renderer.asset (PC)
// Renderer Data = jak URP renderuje (forward/deferred, native render pass, depth, stencil)
// Renderer Features (SSAO, decals, custom passes) = dodawane reczenie w edytorze,
//   tu sterujemy tylko ustawieniami renderera.
//
// Public API:
//   VRRenderer.Apply(VRRenderer.INITIAL);
//   VRRenderer.SetRenderingMode(0); // 0=Forward, 1=ForwardPlus, 2=Deferred
//   PCRenderer.Apply(PCRenderer.INITIAL);

using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public struct RendererSettings
    {
        public int renderingMode;           // 0=Forward, 1=ForwardPlus, 2=Deferred
        public bool nativeRenderPass;       // Vulkan native render pass (Quest = true)
        public int depthPrimingMode;        // 0=Disabled, 1=Auto, 2=Forced
        public int copyDepthMode;           // 0=AfterOpaques, 1=AfterTransparents, 2=ForcePrepass
        public bool shadowTransparentReceive;
        public int intermediateTextureMode; // 0=Auto, 1=Always
    }

    // =========================================================================
    // VR Renderer (Mobile_Renderer)
    // =========================================================================

    public static class VRRenderer
    {
        private const string LOG = "[PLAGA44/VR]";
        private const string ASSET_PATH = "Assets/Settings/Mobile_Renderer.asset";

        public static readonly RendererSettings INITIAL = new RendererSettings
        {
            renderingMode           = 0,        // Forward (jedyny sensowny na Quest)
            nativeRenderPass        = true,     // Vulkan native pass = mniej bandwidth
            depthPrimingMode        = 0,        // Disabled (Forward nie potrzebuje)
            copyDepthMode           = 0,        // AfterOpaques
            shadowTransparentReceive = false,   // oszczednosc GPU
            intermediateTextureMode = 0,        // Auto
        };

        public static void Apply(RendererSettings s) => RendererCore.Apply(ASSET_PATH, s, LOG);
        public static void LogCurrent() => RendererCore.LogCurrent(ASSET_PATH, LOG);

        public static void SetRenderingMode(int v) => RendererCore.Tweak(ASSET_PATH, "m_RenderingMode", v, LOG);
        public static void SetNativeRenderPass(bool v) => RendererCore.Tweak(ASSET_PATH, "m_UseNativeRenderPass", v, LOG);
        public static void SetDepthPriming(int v) => RendererCore.Tweak(ASSET_PATH, "m_DepthPrimingMode", v, LOG);
        public static void SetShadowTransparent(bool v) => RendererCore.Tweak(ASSET_PATH, "m_ShadowTransparentReceive", v, LOG);

        [MenuItem("CYBERNOMAD/Presets/Quest/VR Renderer INITIAL", false, 1)]
        static void MenuInitial() => Apply(INITIAL);
        [MenuItem("CYBERNOMAD/Status/VR Renderer", false, 100)]
        static void MenuShow() => LogCurrent();
    }

    // =========================================================================
    // PC Renderer (PC_Renderer)
    // =========================================================================

    public static class PCRenderer
    {
        private const string LOG = "[PLAGA44/PC]";
        private const string ASSET_PATH = "Assets/Settings/PC_Renderer.asset";

        public static readonly RendererSettings INITIAL = new RendererSettings
        {
            renderingMode           = 0,        // Forward (prostszy, VR-compatible w edytorze)
            nativeRenderPass        = true,
            depthPrimingMode        = 0,
            copyDepthMode           = 0,
            shadowTransparentReceive = true,    // PC ciagnie
            intermediateTextureMode = 0,
        };

        public static readonly RendererSettings HIEND = new RendererSettings
        {
            renderingMode           = 2,        // Deferred (wiecej swiatel, GBuffer)
            nativeRenderPass        = true,
            depthPrimingMode        = 1,        // Auto
            copyDepthMode           = 0,
            shadowTransparentReceive = true,
            intermediateTextureMode = 0,
        };

        public static void Apply(RendererSettings s) => RendererCore.Apply(ASSET_PATH, s, LOG);
        public static void LogCurrent() => RendererCore.LogCurrent(ASSET_PATH, LOG);

        public static void SetRenderingMode(int v) => RendererCore.Tweak(ASSET_PATH, "m_RenderingMode", v, LOG);
        public static void SetNativeRenderPass(bool v) => RendererCore.Tweak(ASSET_PATH, "m_UseNativeRenderPass", v, LOG);
        public static void SetDepthPriming(int v) => RendererCore.Tweak(ASSET_PATH, "m_DepthPrimingMode", v, LOG);
        public static void SetShadowTransparent(bool v) => RendererCore.Tweak(ASSET_PATH, "m_ShadowTransparentReceive", v, LOG);

        [MenuItem("CYBERNOMAD/Presets/PC/Renderer INITIAL", false, 1)]
        static void MenuInitial() => Apply(INITIAL);
        [MenuItem("CYBERNOMAD/Presets/PC/Renderer HIEND", false, 2)]
        static void MenuHiEnd() => Apply(HIEND);
        [MenuItem("CYBERNOMAD/Status/PC Renderer", false, 100)]
        static void MenuShow() => LogCurrent();
    }

    // =========================================================================
    // Wspolna logika
    // =========================================================================

    internal static class RendererCore
    {
        public static void Apply(string path, RendererSettings s, string log)
        {
            var so = Load(path, log);
            if (so == null) return;

            Set(so, "m_RenderingMode",            s.renderingMode);
            Set(so, "m_UseNativeRenderPass",      s.nativeRenderPass);
            Set(so, "m_DepthPrimingMode",         s.depthPrimingMode);
            Set(so, "m_CopyDepthMode",            s.copyDepthMode);
            Set(so, "m_ShadowTransparentReceive", s.shadowTransparentReceive);
            Set(so, "m_IntermediateTextureMode",  s.intermediateTextureMode);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(so.targetObject);
            AssetDatabase.SaveAssets();

            string mode = s.renderingMode switch { 0 => "Forward", 1 => "Forward+", 2 => "Deferred", _ => "?" };
            Debug.Log($"{log} Renderer applied: {mode} NativePass={s.nativeRenderPass} " +
                      $"DepthPriming={s.depthPrimingMode} ShadowTransparent={s.shadowTransparentReceive}");
        }

        public static void LogCurrent(string path, string log)
        {
            var so = Load(path, log);
            if (so == null) return;

            int mode = GetInt(so, "m_RenderingMode");
            string modeName = mode switch { 0 => "Forward", 1 => "Forward+", 2 => "Deferred", _ => "?" };
            Debug.Log($"{log} Renderer: {modeName} NativePass={GetBool(so, "m_UseNativeRenderPass")} " +
                      $"DepthPriming={GetInt(so, "m_DepthPrimingMode")} " +
                      $"ShadowTransparent={GetBool(so, "m_ShadowTransparentReceive")}");
        }

        public static void Tweak(string path, string field, int value, string log)
        {
            var so = Load(path, log); if (so == null) return;
            Set(so, field, value); so.ApplyModifiedProperties();
            EditorUtility.SetDirty(so.targetObject);
            Debug.Log($"{log} Renderer tweak: {field}={value}");
        }

        public static void Tweak(string path, string field, bool value, string log)
        {
            var so = Load(path, log); if (so == null) return;
            Set(so, field, value); so.ApplyModifiedProperties();
            EditorUtility.SetDirty(so.targetObject);
            Debug.Log($"{log} Renderer tweak: {field}={value}");
        }

        static SerializedObject Load(string path, string log)
        {
            var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (obj == null) { Debug.LogError($"{log} Asset not found: {path}"); return null; }
            return new SerializedObject(obj);
        }

        static void Set(SerializedObject so, string f, int v)  { var p = so.FindProperty(f); if (p != null) p.intValue = v; }
        static void Set(SerializedObject so, string f, bool v)  { var p = so.FindProperty(f); if (p != null) p.boolValue = v; }
        static int GetInt(SerializedObject so, string f)         { var p = so.FindProperty(f); return p?.intValue ?? -1; }
        static bool GetBool(SerializedObject so, string f)       { var p = so.FindProperty(f); return p?.boolValue ?? false; }
    }
}
