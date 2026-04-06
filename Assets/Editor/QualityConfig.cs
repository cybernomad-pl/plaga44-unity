// QualityConfig.cs -- CYBERNOMAD Editor Tool
//
// Steruje: ProjectSettings/QualitySettings.asset (Mobile quality level)
//
// UWAGA: W URP wiekszość ustawień renderingu bierze się z Pipeline Asset
// (VRPipeline/PCPipeline), nie z QualitySettings. Ten Config steruje
// ustawieniami które NIE SA w pipeline asset: texture quality, skin weights,
// terrain, async upload, streaming mipmaps, LOD.
//
// Public API:
//   QualityConfig.Apply(QualityConfig.INITIAL);
//   QualityConfig.SetSkinWeights(2);
//   QualityConfig.LogCurrent();

using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public struct QualitySettings_
    {
        public int skinWeights;             // 1=OneBone, 2=TwoBones, 4=FourBones
        public int anisotropicTextures;     // 0=Disabled, 1=PerTexture, 2=ForcedOn
        public int globalTextureMipmapLimit; // 0=Full, 1=Half, 2=Quarter
        public bool streamingMipmapsActive;
        public int streamingMipmapsBudgetMB;
        public int asyncUploadTimeSlice;    // ms per frame for async upload
        public int asyncUploadBufferSizeMB;
        public float lodBias;               // 1.0 = default, 2.0 = more detail
        public int maximumLODLevel;         // 0 = use all, 1 = skip highest, etc
        public bool enableLODCrossFade;
        public int particleRaycastBudget;
        // Terrain
        public float terrainPixelError;
        public float terrainDetailDistance;
        public float terrainBasemapDistance;
        public float terrainTreeDistance;
    }

    public static class QualityConfig
    {
        private const string LOG = "[PLAGA44]";
        private const string ASSET = "ProjectSettings/QualitySettings.asset";

        // ---------------------------------------------------------------------
        // Presety
        // ---------------------------------------------------------------------

        public static readonly QualitySettings_ INITIAL = new QualitySettings_
        {
            skinWeights             = 2,        // TwoBones (Quest oszczednosc)
            anisotropicTextures     = 1,        // PerTexture
            globalTextureMipmapLimit = 0,       // Full res
            streamingMipmapsActive  = false,    // na start off, wlaczyc przy duzych scenach
            streamingMipmapsBudgetMB = 256,
            asyncUploadTimeSlice    = 4,        // 4ms (default 2 = wolny loading)
            asyncUploadBufferSizeMB = 32,       // 32MB (default 16)
            lodBias                 = 1.0f,
            maximumLODLevel         = 0,
            enableLODCrossFade      = false,    // oszczednosc na Quest
            particleRaycastBudget   = 64,       // mniej niz default 256
            terrainPixelError       = 5,        // mniej dokladny = szybszy (default 1)
            terrainDetailDistance    = 40,       // 40m (default 80)
            terrainBasemapDistance   = 500,      // 500m (default 1000)
            terrainTreeDistance      = 2000,     // 2000m (default 5000)
        };

        public static readonly QualitySettings_ DEFAULT = new QualitySettings_
        {
            skinWeights             = 2,
            anisotropicTextures     = 1,
            globalTextureMipmapLimit = 0,
            streamingMipmapsActive  = false,
            streamingMipmapsBudgetMB = 512,
            asyncUploadTimeSlice    = 2,
            asyncUploadBufferSizeMB = 16,
            lodBias                 = 1.0f,
            maximumLODLevel         = 0,
            enableLODCrossFade      = true,
            particleRaycastBudget   = 256,
            terrainPixelError       = 1,
            terrainDetailDistance    = 80,
            terrainBasemapDistance   = 1000,
            terrainTreeDistance      = 5000,
        };

        // ---------------------------------------------------------------------
        // Apply all (do Mobile quality level)
        // ---------------------------------------------------------------------

        public static void Apply(QualitySettings_ s)
        {
            var so = LoadAsset();
            if (so == null) return;

            int idx = FindMobileIndex(so);
            if (idx < 0) { Debug.LogError($"{LOG} Mobile quality level not found"); return; }

            var arr = so.FindProperty("m_QualitySettings");
            var mobile = arr.GetArrayElementAtIndex(idx);

            SetRel(mobile, "skinWeights", s.skinWeights);
            SetRel(mobile, "anisotropicTextures", s.anisotropicTextures);
            SetRel(mobile, "globalTextureMipmapLimit", s.globalTextureMipmapLimit);
            SetRel(mobile, "streamingMipmapsActive", s.streamingMipmapsActive);
            SetRel(mobile, "streamingMipmapsMemoryBudget", s.streamingMipmapsBudgetMB);
            SetRel(mobile, "asyncUploadTimeSlice", s.asyncUploadTimeSlice);
            SetRel(mobile, "asyncUploadBufferSize", s.asyncUploadBufferSizeMB);
            SetRel(mobile, "lodBias", s.lodBias);
            SetRel(mobile, "maximumLODLevel", s.maximumLODLevel);
            SetRel(mobile, "enableLODCrossFade", s.enableLODCrossFade);
            SetRel(mobile, "particleRaycastBudget", s.particleRaycastBudget);
            SetRel(mobile, "terrainPixelError", s.terrainPixelError);
            SetRel(mobile, "terrainDetailDistance", s.terrainDetailDistance);
            SetRel(mobile, "terrainBasemapDistance", s.terrainBasemapDistance);
            SetRel(mobile, "terrainTreeDistance", s.terrainTreeDistance);

            so.ApplyModifiedProperties();
            Debug.Log($"{LOG} Quality applied: skinW={s.skinWeights} asyncBuf={s.asyncUploadBufferSizeMB}MB " +
                      $"LODcrossfade={s.enableLODCrossFade} terrainDetail={s.terrainDetailDistance}m");
        }

        // ---------------------------------------------------------------------
        // Single value
        // ---------------------------------------------------------------------

        public static void SetSkinWeights(int v)           => TweakMobile("skinWeights", v);
        public static void SetAnisotropic(int v)           => TweakMobile("anisotropicTextures", v);
        public static void SetTextureMipmapLimit(int v)    => TweakMobile("globalTextureMipmapLimit", v);
        public static void SetStreamingMipmaps(bool v)     => TweakMobileBool("streamingMipmapsActive", v);
        public static void SetAsyncUploadBuffer(int mb)    => TweakMobile("asyncUploadBufferSize", mb);
        public static void SetAsyncUploadTimeSlice(int ms) => TweakMobile("asyncUploadTimeSlice", ms);
        public static void SetLODBias(float v)             => TweakMobileFloat("lodBias", v);
        public static void SetLODCrossFade(bool v)         => TweakMobileBool("enableLODCrossFade", v);
        public static void SetTerrainPixelError(float v)   => TweakMobileFloat("terrainPixelError", v);
        public static void SetTerrainDetailDistance(float v) => TweakMobileFloat("terrainDetailDistance", v);

        // ---------------------------------------------------------------------
        // Log
        // ---------------------------------------------------------------------

        public static void LogCurrent()
        {
            var so = LoadAsset();
            if (so == null) return;
            int idx = FindMobileIndex(so);
            if (idx < 0) return;

            var mobile = so.FindProperty("m_QualitySettings").GetArrayElementAtIndex(idx);
            Debug.Log($"{LOG} Quality (Mobile): skinW={GetInt(mobile, "skinWeights")} " +
                      $"aniso={GetInt(mobile, "anisotropicTextures")} " +
                      $"streaming={GetBool(mobile, "streamingMipmapsActive")} " +
                      $"asyncBuf={GetInt(mobile, "asyncUploadBufferSize")}MB " +
                      $"LODbias={GetFloat(mobile, "lodBias")} " +
                      $"crossfade={GetBool(mobile, "enableLODCrossFade")} " +
                      $"terrainDetail={GetFloat(mobile, "terrainDetailDistance")}m");
        }

        // ---------------------------------------------------------------------
        // Menu
        // ---------------------------------------------------------------------

        [MenuItem("CYBERNOMAD/Presets/Quest/Quality INITIAL", false, 1)]
        static void MenuInitial() => Apply(INITIAL);

        [MenuItem("CYBERNOMAD/Quality/Show Current", false, 100)]
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

        static int FindMobileIndex(SerializedObject so)
        {
            var arr = so.FindProperty("m_QualitySettings");
            for (int i = 0; i < arr.arraySize; i++)
            {
                var name = arr.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                if (name != null && name.stringValue == "Mobile") return i;
            }
            return -1;
        }

        static void TweakMobile(string field, int value)
        {
            var so = LoadAsset(); if (so == null) return;
            int idx = FindMobileIndex(so); if (idx < 0) return;
            var mobile = so.FindProperty("m_QualitySettings").GetArrayElementAtIndex(idx);
            SetRel(mobile, field, value);
            so.ApplyModifiedProperties();
            Debug.Log($"{LOG} Quality tweak: {field}={value}");
        }

        static void TweakMobileBool(string field, bool value)
        {
            var so = LoadAsset(); if (so == null) return;
            int idx = FindMobileIndex(so); if (idx < 0) return;
            var mobile = so.FindProperty("m_QualitySettings").GetArrayElementAtIndex(idx);
            SetRel(mobile, field, value);
            so.ApplyModifiedProperties();
            Debug.Log($"{LOG} Quality tweak: {field}={value}");
        }

        static void TweakMobileFloat(string field, float value)
        {
            var so = LoadAsset(); if (so == null) return;
            int idx = FindMobileIndex(so); if (idx < 0) return;
            var mobile = so.FindProperty("m_QualitySettings").GetArrayElementAtIndex(idx);
            SetRel(mobile, field, value);
            so.ApplyModifiedProperties();
            Debug.Log($"{LOG} Quality tweak: {field}={value}");
        }

        static void SetRel(SerializedProperty p, string f, int v)   { var r = p.FindPropertyRelative(f); if (r != null) r.intValue = v; }
        static void SetRel(SerializedProperty p, string f, float v)  { var r = p.FindPropertyRelative(f); if (r != null) r.floatValue = v; }
        static void SetRel(SerializedProperty p, string f, bool v)   { var r = p.FindPropertyRelative(f); if (r != null) r.boolValue = v; }
        static int GetInt(SerializedProperty p, string f)             { var r = p.FindPropertyRelative(f); return r?.intValue ?? -1; }
        static float GetFloat(SerializedProperty p, string f)        { var r = p.FindPropertyRelative(f); return r?.floatValue ?? -1; }
        static bool GetBool(SerializedProperty p, string f)          { var r = p.FindPropertyRelative(f); return r?.boolValue ?? false; }
    }
}
