// AudioConfig.cs -- CYBERNOMAD Editor Tool
//
// Steruje: ProjectSettings/AudioManager.asset
//
// Public API:
//   AudioConfig.Apply(AudioConfig.INITIAL);
//   AudioConfig.SetDSPBuffer(512);
//   AudioConfig.LogCurrent();

using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public struct AudioPreset
    {
        public int dspBufferSize;           // 256, 512, 1024 (mniej = mniej latency, wiecej CPU)
        public int speakerMode;             // 0=Raw, 1=Mono, 2=Stereo, 3=Quad, 4=Surround, 5=5.1, 6=7.1
        public string spatializer;          // "Meta XR Audio" / "" (none)
        public string ambisonicDecoder;     // "Meta XR Audio" / ""
        public int sampleRate;              // 0=system default, 24000, 44100, 48000
    }

    public static class AudioConfig
    {
        private const string LOG = "[PLAGA44]";
        private const string ASSET = "ProjectSettings/AudioManager.asset";

        // ---------------------------------------------------------------------
        // Presety
        // ---------------------------------------------------------------------

        public static readonly AudioPreset INITIAL = new AudioPreset
        {
            dspBufferSize    = 512,
            speakerMode      = 2,               // Stereo
            spatializer      = "Meta XR Audio",
            ambisonicDecoder = "Meta XR Audio",
            sampleRate       = 0,               // system default
        };

        public static readonly AudioPreset DEFAULT = new AudioPreset
        {
            dspBufferSize    = 1024,
            speakerMode      = 2,
            spatializer      = "Meta XR Audio",
            ambisonicDecoder = "Meta XR Audio",
            sampleRate       = 0,
        };

        // ---------------------------------------------------------------------
        // Apply all
        // ---------------------------------------------------------------------

        public static void Apply(AudioPreset s)
        {
            var so = LoadAsset();
            if (so == null) return;

            Set(so, "m_DSPBufferSize",           s.dspBufferSize);
            Set(so, "Default Speaker Mode",      s.speakerMode);
            Set(so, "m_SpatializerPlugin",       s.spatializer);
            Set(so, "m_AmbisonicDecoderPlugin",  s.ambisonicDecoder);
            Set(so, "m_SampleRate",              s.sampleRate);

            so.ApplyModifiedProperties();
            Debug.Log($"{LOG} Audio applied: DSP={s.dspBufferSize} Speaker={s.speakerMode} Spatializer={s.spatializer}");
        }

        // ---------------------------------------------------------------------
        // Single value
        // ---------------------------------------------------------------------

        public static void SetDSPBuffer(int value)    => Tweak("m_DSPBufferSize", value);
        public static void SetSpeakerMode(int value)   => Tweak("Default Speaker Mode", value);
        public static void SetSpatializer(string value) => TweakStr("m_SpatializerPlugin", value);
        public static void SetSampleRate(int value)    => Tweak("m_SampleRate", value);

        // ---------------------------------------------------------------------
        // Log
        // ---------------------------------------------------------------------

        public static void LogCurrent()
        {
            var so = LoadAsset();
            if (so == null) return;
            Debug.Log($"{LOG} Audio: DSP={GetInt(so, "m_DSPBufferSize")} " +
                      $"Speaker={GetInt(so, "Default Speaker Mode")} " +
                      $"Spatializer={GetStr(so, "m_SpatializerPlugin")}");
        }

        // ---------------------------------------------------------------------
        // Menu
        // ---------------------------------------------------------------------

        [MenuItem("CYBERNOMAD/Config/Audio/Apply INITIAL", false, 1)]
        static void MenuInitial() => Apply(INITIAL);

        [MenuItem("CYBERNOMAD/Config/Audio/Show Current", false, 100)]
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
            Debug.Log($"{LOG} Audio tweak: {field}={value}");
        }

        static void TweakStr(string field, string value)
        {
            var so = LoadAsset(); if (so == null) return;
            Set(so, field, value); so.ApplyModifiedProperties();
            Debug.Log($"{LOG} Audio tweak: {field}={value}");
        }

        static void Set(SerializedObject so, string f, int v)    { var p = so.FindProperty(f); if (p != null) p.intValue = v; }
        static void Set(SerializedObject so, string f, string v) { var p = so.FindProperty(f); if (p != null) p.stringValue = v; }
        static int GetInt(SerializedObject so, string f)         { var p = so.FindProperty(f); return p != null ? p.intValue : -1; }
        static string GetStr(SerializedObject so, string f)      { var p = so.FindProperty(f); return p != null ? p.stringValue : "?"; }
    }
}
