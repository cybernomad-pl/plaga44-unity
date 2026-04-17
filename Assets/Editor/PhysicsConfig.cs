// PhysicsConfig.cs -- CYBERNOMAD Editor Tool
//
// Steruje: ProjectSettings/DynamicsManager.asset + ProjectSettings/TimeManager.asset
//
// Public API:
//   PhysicsConfig.Apply(PhysicsConfig.INITIAL);
//   PhysicsConfig.SetGravity(-9.81f);
//   PhysicsConfig.SetFixedTimestep(0.01111f);  // 90Hz
//   PhysicsConfig.LogCurrent();

using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public struct PhysicsSettings
    {
        public float gravityY;              // -9.81 default
        public int solverIterations;        // 4-6 typical
        public float defaultContactOffset;  // 0.01 default
        public float bounceThreshold;       // 2 default
        public float fixedTimestep;         // 0.02 = 50Hz, 0.01111 = 90Hz
        public float maxTimestep;           // 0.33333 default
    }

    public static class PhysicsConfig
    {
        private const string LOG = "[PLAGA44]";
        private const string DYNAMICS = "ProjectSettings/DynamicsManager.asset";
        private const string TIME = "ProjectSettings/TimeManager.asset";

        // ---------------------------------------------------------------------
        // Presety
        // ---------------------------------------------------------------------

        public static readonly PhysicsSettings INITIAL = new PhysicsSettings
        {
            gravityY            = -9.81f,
            solverIterations    = 4,
            defaultContactOffset = 0.01f,
            bounceThreshold     = 2f,
            fixedTimestep       = 0.01388889f,  // 72Hz (Quest 2 default)
            maxTimestep         = 0.33333334f,
        };

        public static readonly PhysicsSettings DEFAULT = new PhysicsSettings
        {
            gravityY            = -9.81f,
            solverIterations    = 6,
            defaultContactOffset = 0.01f,
            bounceThreshold     = 2f,
            fixedTimestep       = 0.02f,        // 50Hz
            maxTimestep         = 0.33333334f,
        };

        public static readonly PhysicsSettings QUEST3 = new PhysicsSettings
        {
            gravityY            = -9.81f,
            solverIterations    = 4,
            defaultContactOffset = 0.01f,
            bounceThreshold     = 2f,
            fixedTimestep       = 0.01111111f,  // 90Hz
            maxTimestep         = 0.33333334f,
        };

        // ---------------------------------------------------------------------
        // Apply all
        // ---------------------------------------------------------------------

        public static void Apply(PhysicsSettings s)
        {
            var dyn = LoadAsset(DYNAMICS);
            if (dyn != null)
            {
                SetVec3(dyn, "m_Gravity", new Vector3(0f, s.gravityY, 0f));
                Set(dyn, "m_DefaultSolverIterations", s.solverIterations);
                Set(dyn, "m_DefaultContactOffset", s.defaultContactOffset);
                Set(dyn, "m_BounceThreshold", s.bounceThreshold);
                dyn.ApplyModifiedProperties();
            }

            var time = LoadAsset(TIME);
            if (time != null)
            {
                Set(time, "Fixed Timestep", s.fixedTimestep);
                Set(time, "Maximum Allowed Timestep", s.maxTimestep);
                time.ApplyModifiedProperties();
            }

            float hz = s.fixedTimestep > 0 ? 1f / s.fixedTimestep : 0;
            Debug.Log($"{LOG} Physics applied: gravity={s.gravityY} solver={s.solverIterations} " +
                      $"timestep={s.fixedTimestep} ({hz:F0}Hz)");
        }

        // ---------------------------------------------------------------------
        // Single value
        // ---------------------------------------------------------------------

        public static void SetGravity(float y)           => TweakDyn("m_Gravity", y);
        public static void SetSolverIterations(int v)    => TweakDynI("m_DefaultSolverIterations", v);
        public static void SetContactOffset(float v)     => TweakDyn("m_DefaultContactOffset", v);
        public static void SetFixedTimestep(float v)     => TweakTime("Fixed Timestep", v);
        public static void SetMaxTimestep(float v)       => TweakTime("Maximum Allowed Timestep", v);

        // ---------------------------------------------------------------------
        // Log
        // ---------------------------------------------------------------------

        public static void LogCurrent()
        {
            var dyn = LoadAsset(DYNAMICS);
            var time = LoadAsset(TIME);
            float ts = 0;
            if (time != null) { var p = time.FindProperty("Fixed Timestep"); if (p != null) ts = p.floatValue; }
            float hz = ts > 0 ? 1f / ts : 0;
            Debug.Log($"{LOG} Physics: gravity={Physics.gravity.y} solver={Physics.defaultSolverIterations} " +
                      $"timestep={ts} ({hz:F0}Hz)");
        }

        // ---------------------------------------------------------------------
        // Menu
        // ---------------------------------------------------------------------

        [MenuItem("CYBERNOMAD/Config/Physics/Apply INITIAL (72Hz)", false, 1)]
        static void MenuInitial() => Apply(INITIAL);

        [MenuItem("CYBERNOMAD/Config/Physics/Apply QUEST3 (90Hz)", false, 2)]
        static void MenuQuest3() => Apply(QUEST3);

        [MenuItem("CYBERNOMAD/Config/Physics/Show Current", false, 100)]
        static void MenuShow() => LogCurrent();

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        static SerializedObject LoadAsset(string path)
        {
            var obj = AssetDatabase.LoadAllAssetsAtPath(path);
            if (obj == null || obj.Length == 0) { Debug.LogError($"{LOG} {path} not found"); return null; }
            return new SerializedObject(obj[0]);
        }

        static void TweakDyn(string field, float value)
        {
            var so = LoadAsset(DYNAMICS); if (so == null) return;
            if (field == "m_Gravity") SetVec3(so, field, new Vector3(0, value, 0));
            else Set(so, field, value);
            so.ApplyModifiedProperties();
            Debug.Log($"{LOG} Physics tweak: {field}={value}");
        }

        static void TweakDynI(string field, int value)
        {
            var so = LoadAsset(DYNAMICS); if (so == null) return;
            Set(so, field, value); so.ApplyModifiedProperties();
            Debug.Log($"{LOG} Physics tweak: {field}={value}");
        }

        static void TweakTime(string field, float value)
        {
            var so = LoadAsset(TIME); if (so == null) return;
            Set(so, field, value); so.ApplyModifiedProperties();
            float hz = value > 0 ? 1f / value : 0;
            Debug.Log($"{LOG} Physics tweak: {field}={value} ({hz:F0}Hz)");
        }

        static void Set(SerializedObject so, string f, int v)   { var p = so.FindProperty(f); if (p != null) p.intValue = v; }
        static void Set(SerializedObject so, string f, float v)  { var p = so.FindProperty(f); if (p != null) p.floatValue = v; }
        static void SetVec3(SerializedObject so, string f, Vector3 v) { var p = so.FindProperty(f); if (p != null) p.vector3Value = v; }
    }
}
