// OculusConfig.cs -- CYBERNOMAD Editor Tool
//
// Steruje: Assets/Oculus/OculusProjectConfig.asset
//
// Public API:
//   OculusConfig.Apply(OculusConfig.INITIAL);
//   OculusConfig.SetHandTracking(true);
//   OculusConfig.LogCurrent();

using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public struct OculusSettings
    {
        public int handTracking;        // 0=None, 1=Controllers, 2=Hands
        public int handTrackingFreq;    // 0=Low, 1=High
        public int bodyTracking;        // 0=None, 1=Full, 2=Upper, 3=Limited
        public int faceTracking;        // 0=None, 1=Full
        public int eyeTracking;         // 0=None, 1=Full
        public int anchorSupport;       // 0=None, 1=Enabled
        public int sceneSupport;        // 0=None, 1=Enabled
        public int renderModel;         // 0=None, 1=Enabled
    }

    public static class OculusConfig
    {
        private const string LOG = "[PLAGA44]";
        private const string ASSET = "Assets/Oculus/OculusProjectConfig.asset";

        // ---------------------------------------------------------------------
        // Presety
        // ---------------------------------------------------------------------

        public static readonly OculusSettings INITIAL = new OculusSettings
        {
            handTracking     = 0,   // off -- controllers only
            handTrackingFreq = 0,
            bodyTracking     = 0,
            faceTracking     = 0,
            eyeTracking      = 0,
            anchorSupport    = 0,
            sceneSupport     = 0,
            renderModel      = 0,
        };

        public static readonly OculusSettings FULL = new OculusSettings
        {
            handTracking     = 2,   // hands
            handTrackingFreq = 1,   // high
            bodyTracking     = 1,   // full
            faceTracking     = 1,   // full
            eyeTracking      = 1,   // full
            anchorSupport    = 1,
            sceneSupport     = 1,
            renderModel      = 1,
        };

        // ---------------------------------------------------------------------
        // Apply all
        // ---------------------------------------------------------------------

        public static void Apply(OculusSettings s)
        {
            var so = LoadAsset();
            if (so == null) return;

            Set(so, "handTrackingSupport",   s.handTracking);
            Set(so, "handTrackingFrequency", s.handTrackingFreq);
            Set(so, "bodyTrackingSupport",   s.bodyTracking);
            Set(so, "faceTrackingSupport",   s.faceTracking);
            Set(so, "eyeTrackingSupport",    s.eyeTracking);
            Set(so, "anchorSupport",         s.anchorSupport);
            Set(so, "sceneSupport",          s.sceneSupport);
            Set(so, "renderModelSupport",    s.renderModel);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(so.targetObject);
            AssetDatabase.SaveAssets();
            Debug.Log($"{LOG} Oculus config applied: hand={s.handTracking} body={s.bodyTracking} " +
                      $"face={s.faceTracking} eye={s.eyeTracking} anchor={s.anchorSupport} scene={s.sceneSupport}");
        }

        // ---------------------------------------------------------------------
        // Single value
        // ---------------------------------------------------------------------

        public static void SetHandTracking(bool on)   => Tweak("handTrackingSupport", on ? 2 : 0);
        public static void SetBodyTracking(bool on)    => Tweak("bodyTrackingSupport", on ? 1 : 0);
        public static void SetFaceTracking(bool on)    => Tweak("faceTrackingSupport", on ? 1 : 0);
        public static void SetEyeTracking(bool on)     => Tweak("eyeTrackingSupport", on ? 1 : 0);
        public static void SetAnchorSupport(bool on)   => Tweak("anchorSupport", on ? 1 : 0);
        public static void SetSceneSupport(bool on)    => Tweak("sceneSupport", on ? 1 : 0);

        // ---------------------------------------------------------------------
        // Log
        // ---------------------------------------------------------------------

        public static void LogCurrent()
        {
            var so = LoadAsset();
            if (so == null) return;
            Debug.Log($"{LOG} Oculus: hand={GetInt(so, "handTrackingSupport")} " +
                      $"body={GetInt(so, "bodyTrackingSupport")} face={GetInt(so, "faceTrackingSupport")} " +
                      $"eye={GetInt(so, "eyeTrackingSupport")} anchor={GetInt(so, "anchorSupport")} " +
                      $"scene={GetInt(so, "sceneSupport")}");
        }

        // ---------------------------------------------------------------------
        // Menu
        // ---------------------------------------------------------------------

        [MenuItem("CYBERNOMAD/Oculus/Apply INITIAL (controllers only)", false, 1)]
        static void MenuInitial() => Apply(INITIAL);

        [MenuItem("CYBERNOMAD/Oculus/Apply FULL (all tracking)", false, 2)]
        static void MenuFull() => Apply(FULL);

        [MenuItem("CYBERNOMAD/Oculus/Show Current", false, 100)]
        static void MenuShow() => LogCurrent();

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        static SerializedObject LoadAsset()
        {
            var obj = AssetDatabase.LoadAssetAtPath<Object>(ASSET);
            if (obj == null) { Debug.LogError($"{LOG} {ASSET} not found"); return null; }
            return new SerializedObject(obj);
        }

        static void Tweak(string field, int value)
        {
            var so = LoadAsset(); if (so == null) return;
            Set(so, field, value);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(so.targetObject);
            AssetDatabase.SaveAssets();
            Debug.Log($"{LOG} Oculus tweak: {field}={value}");
        }

        static void Set(SerializedObject so, string f, int v)  { var p = so.FindProperty(f); if (p != null) p.intValue = v; }
        static int GetInt(SerializedObject so, string f)        { var p = so.FindProperty(f); return p != null ? p.intValue : -1; }
    }
}
