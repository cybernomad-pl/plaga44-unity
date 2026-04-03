// FaceTrackingSetup.cs
// CYBERNOMAD -- Editor menu to configure face tracking in the current scene.
// Menu: CYBERNOMAD > Scene Setup > Setup Face Tracking
//
// What it does:
//   1. Enables face tracking permission on OVRManager (requestFaceTracking = true)
//   2. Ensures OVRFaceExpressions component exists on the OVRCameraRig
//   3. Adds FaceTrackingManager + EmotionDetector to a persistent manager GO
//   4. Marks scene dirty
//
// Requires: com.meta.xr.sdk.core (auto-detected via HAS_META_XR define)

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class FaceTrackingSetup
    {
        private const string LOG = "[PLAGA44]";
        private const string MENU_PATH = "CYBERNOMAD/Scene Setup/Setup Face Tracking";
        private const string MENU_DEBUG = "CYBERNOMAD/Debug/Face Expression Debug HUD";
        // Must match FaceExpressionDebug.ENABLED_KEY
        private const string DEBUG_KEY = "CYBERNOMAD_FaceExpressionDebug";

        // ── Setup menu item ──────────────────────────────────────────────

        [MenuItem(MENU_PATH, false, 110)]
        public static void SetupFaceTracking()
        {
            Debug.Log($"{LOG} === Setup Face Tracking ===");

#if HAS_META_XR
            bool anyChange = false;
            anyChange |= ConfigureOVRManager();
            anyChange |= EnsureOVRFaceExpressions();
            anyChange |= EnsureFaceTrackingManager();

            if (anyChange)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                Debug.Log($"{LOG} === Face Tracking setup complete. Save the scene. ===");
            }
            else
            {
                Debug.Log($"{LOG} === Face Tracking already configured. Nothing to do. ===");
            }
#else
            Debug.LogError(
                $"{LOG} HAS_META_XR scripting define not found. " +
                "Run CYBERNOMAD > Meta SDK Setup > 1. Setup Meta SDK first.");
#endif
        }

        // ── Debug HUD toggle ─────────────────────────────────────────────

        [MenuItem(MENU_DEBUG, false, 510)]
        private static void ToggleDebugHUD()
        {
            bool current = EditorPrefs.GetBool(DEBUG_KEY, false);
            bool next = !current;
            EditorPrefs.SetBool(DEBUG_KEY, next);

            if (Application.isPlaying)
            {
                if (next)
                    Plaga44.FaceTracking.FaceExpressionDebug.Spawn();
                else
                    Plaga44.FaceTracking.FaceExpressionDebug.Kill();
            }

            Debug.Log($"{LOG} Face Expression Debug HUD: {(next ? "ENABLED" : "DISABLED")}");
        }

        [MenuItem(MENU_DEBUG, true)]
        private static bool ToggleDebugHUD_Validate()
        {
            Menu.SetChecked(MENU_DEBUG, EditorPrefs.GetBool(DEBUG_KEY, false));
            return true;
        }

#if HAS_META_XR

        // ── OVRManager configuration ─────────────────────────────────────

        private static bool ConfigureOVRManager()
        {
            var manager = Object.FindFirstObjectByType<OVRManager>();
            if (manager == null)
            {
                Debug.LogWarning(
                    $"{LOG} OVRManager not found in scene. " +
                    "Run CYBERNOMAD > Scene Setup > Setup TESTBED first.");
                return false;
            }

            var so = new SerializedObject(manager);
            bool changed = false;

            // requestFaceTracking -- enables the face tracking permission
            changed |= SetBoolProperty(so, "requestFaceTracking", true, "requestFaceTracking");

            if (changed)
            {
                so.ApplyModifiedProperties();
                Debug.Log($"{LOG} OVRManager: face tracking permission enabled.");
            }
            else
            {
                Debug.Log($"{LOG} OVRManager: face tracking already enabled.");
            }

            return changed;
        }

        // ── OVRFaceExpressions component ─────────────────────────────────

        private static bool EnsureOVRFaceExpressions()
        {
            // Check if one already exists in the scene
            var existing = Object.FindFirstObjectByType<OVRFaceExpressions>();
            if (existing != null)
            {
                Debug.Log($"{LOG} OVRFaceExpressions already present on '{existing.gameObject.name}'.");
                return false;
            }

            // Prefer attaching to OVRCameraRig
            var rig = Object.FindFirstObjectByType<OVRCameraRig>();
            GameObject target;

            if (rig != null)
            {
                target = rig.gameObject;
                Debug.Log($"{LOG} Attaching OVRFaceExpressions to OVRCameraRig.");
            }
            else
            {
                // Fallback: create a dedicated GO
                target = new GameObject("OVRFaceExpressions");
                Undo.RegisterCreatedObjectUndo(target, "Create OVRFaceExpressions");
                Debug.LogWarning(
                    $"{LOG} OVRCameraRig not found. Created standalone OVRFaceExpressions GO.");
            }

            Undo.AddComponent<OVRFaceExpressions>(target);
            Debug.Log($"{LOG} OVRFaceExpressions added to '{target.name}'.");
            return true;
        }

        // ── Runtime manager objects ──────────────────────────────────────

        private static bool EnsureFaceTrackingManager()
        {
            bool changed = false;

            // FaceTrackingManager
            var existingManager =
                Object.FindFirstObjectByType<Plaga44.FaceTracking.FaceTrackingManager>();
            if (existingManager == null)
            {
                var go = new GameObject("FaceTrackingManager");
                Undo.RegisterCreatedObjectUndo(go, "Add FaceTrackingManager");
                Undo.AddComponent<Plaga44.FaceTracking.FaceTrackingManager>(go);
                Debug.Log($"{LOG} FaceTrackingManager added to scene.");
                changed = true;
            }
            else
            {
                Debug.Log($"{LOG} FaceTrackingManager already present.");
            }

            // EmotionDetector -- add alongside FaceTrackingManager if missing
            var existingDetector =
                Object.FindFirstObjectByType<Plaga44.FaceTracking.EmotionDetector>();
            if (existingDetector == null)
            {
                // Attach to the same GO as FaceTrackingManager
                var managerGO = existingManager != null
                    ? existingManager.gameObject
                    : Object.FindFirstObjectByType<Plaga44.FaceTracking.FaceTrackingManager>()
                             ?.gameObject;

                if (managerGO != null)
                {
                    Undo.AddComponent<Plaga44.FaceTracking.EmotionDetector>(managerGO);
                    Debug.Log($"{LOG} EmotionDetector added alongside FaceTrackingManager.");
                    changed = true;
                }
                else
                {
                    Debug.LogWarning($"{LOG} Could not find FaceTrackingManager GO to attach EmotionDetector.");
                }
            }
            else
            {
                Debug.Log($"{LOG} EmotionDetector already present.");
            }

            return changed;
        }

        // ── Helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Sets a bool property on a SerializedObject. Returns true if the value changed.
        /// Skips if property not found (logs a warning unless label is null).
        /// </summary>
        private static bool SetBoolProperty(SerializedObject so, string propName,
            bool value, string label)
        {
            var prop = so.FindProperty(propName);
            if (prop == null)
            {
                if (label != null)
                    Debug.LogWarning(
                        $"{LOG} Property '{propName}' not found on {so.targetObject.GetType().Name}. " +
                        "SDK field name may have changed.");
                return false;
            }

            if (prop.boolValue == value) return false;

            prop.boolValue = value;
            if (label != null)
                Debug.Log($"{LOG} {so.targetObject.GetType().Name}.{label} = {value}");
            return true;
        }

#endif // HAS_META_XR
    }
}
#endif // UNITY_EDITOR
