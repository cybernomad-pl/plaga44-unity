// AUTO-DISABLED: depends on classes guarded by PLAGA44_FULL_SDK
#if PLAGA44_FULL_SDK
// BodyTrackingSetup.cs
// PLAGA '44 -- Editor menu: CYBERNOMAD/Scene Setup/Setup Body Tracking
//
// Configures OVRManager for Meta Movement SDK body tracking:
//   - Enables body tracking permission in OVRManager
//   - Sets tracking fidelity to High
//   - Sets joint set to FullBody
//   - Adds BodyTrackingManager, PlayerBody, BodyCalibration to OVRCameraRig
//
// IMPORTANT: This script does NOT install Movement SDK automatically.
// Movement SDK (com.meta.xr.sdk.movement) must be added manually via:
//   Package Manager > Add package from git URL:
//   https://github.com/oculus-samples/Unity-Movement.git#main
//   OR via Meta Hub (MR developer account required).
//
// After installation define HAS_META_MOVEMENT in:
//   Project Settings > Player > Scripting Define Symbols

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class BodyTrackingSetup
    {
        private const string LOG = "[PLAGA44]";

        // Menu priority 60 -- appears after existing Scene Setup items (50).
        [MenuItem("CYBERNOMAD/Scene Setup/Setup Body Tracking", false, 60)]
        public static void SetupBodyTracking()
        {
            Debug.Log($"{LOG} === Setup Body Tracking ===");

#if HAS_META_XR
            if (!ConfigureOVRManager())
            {
                Debug.LogError($"{LOG} OVRManager configuration failed. " +
                               "Run CYBERNOMAD/Meta SDK Setup/1. Setup Meta SDK first.");
                return;
            }

            AddBodyTrackingComponents();
            PrintMovementSDKInstructions();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} === Body Tracking Setup DONE ===");
#else
            Debug.LogWarning($"{LOG} HAS_META_XR is not defined. " +
                             "Run CYBERNOMAD/Meta SDK Setup/1. Setup Meta SDK first, " +
                             "then add HAS_META_XR to Scripting Define Symbols.");
            PrintMovementSDKInstructions();
#endif
        }

        [MenuItem("CYBERNOMAD/Scene Setup/Setup Body Tracking", true)]
        public static bool SetupBodyTracking_Validate()
        {
            // Only enable when we have an active scene and are in Android build target.
            return EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
        }

#if HAS_META_XR
        /// <summary>
        /// Finds OVRManager in scene and configures body tracking settings.
        /// Returns true on success.
        /// </summary>
        private static bool ConfigureOVRManager()
        {
            var ovrManager = Object.FindFirstObjectByType<OVRManager>();
            if (ovrManager == null)
            {
                Debug.LogError($"{LOG} OVRManager not found in scene. " +
                               "Run CYBERNOMAD/Scene Setup/Setup TESTBED first.");
                return false;
            }

            var so = new SerializedObject(ovrManager);

            // -- Body tracking permission --
            // OVRManager.requestBodyTrackingPermissionOnStartup
            SetBoolProperty(so, "requestBodyTrackingPermissionOnStartup", true,
                            "requestBodyTrackingPermissionOnStartup");

            // -- Body tracking fidelity --
            // OVRManager.bodyTrackingFidelity (OVRPlugin.BodyTrackingFidelity2 enum)
            // High = 1
            SetIntProperty(so, "bodyTrackingFidelity", 1, "bodyTrackingFidelity=High");

            // -- Body joint set --
            // OVRManager.bodyTrackingJointSet (OVRPlugin.BodyJointSet enum)
            // FullBody = 1 (UpperBody = 0)
            SetIntProperty(so, "bodyTrackingJointSet", 1, "bodyTrackingJointSet=FullBody");

            so.ApplyModifiedProperties();

            Debug.Log($"{LOG} OVRManager configured for body tracking on '{ovrManager.gameObject.name}'.");
            return true;
        }

        /// <summary>
        /// Adds BodyTrackingManager, PlayerBody, and BodyCalibration to OVRCameraRig
        /// (or creates a dedicated BodyTracking GameObject if rig not found).
        /// </summary>
        private static void AddBodyTrackingComponents()
        {
            // Find OVRCameraRig as the parent for body tracking components.
            var rig = Object.FindFirstObjectByType<OVRCameraRig>();
            GameObject host;

            if (rig != null)
            {
                host = rig.gameObject;
                Debug.Log($"{LOG} Adding body tracking components to OVRCameraRig.");
            }
            else
            {
                // Fallback: create dedicated GO.
                host = new GameObject("BodyTrackingRoot");
                host.transform.position = Vector3.zero;
                Undo.RegisterCreatedObjectUndo(host, "Create BodyTrackingRoot");
                Debug.LogWarning($"{LOG} OVRCameraRig not found. Created BodyTrackingRoot instead.");
            }

            // Add BodyTrackingManager.
            var mgr = host.GetComponent<Plaga44.BodyTracking.BodyTrackingManager>();
            if (mgr == null)
            {
                mgr = Undo.AddComponent<Plaga44.BodyTracking.BodyTrackingManager>(host);
                Debug.Log($"{LOG} Added BodyTrackingManager.");
            }
            else
            {
                Debug.Log($"{LOG} BodyTrackingManager already present.");
            }

            // Add PlayerBody.
            var playerBody = host.GetComponent<Plaga44.BodyTracking.PlayerBody>();
            if (playerBody == null)
            {
                playerBody = Undo.AddComponent<Plaga44.BodyTracking.PlayerBody>(host);
                playerBody.bodyTrackingManager = mgr;
                Debug.Log($"{LOG} Added PlayerBody.");
            }
            else
            {
                Debug.Log($"{LOG} PlayerBody already present.");
            }

            // Add BodyCalibration.
            var calibration = host.GetComponent<Plaga44.BodyTracking.BodyCalibration>();
            if (calibration == null)
            {
                calibration = Undo.AddComponent<Plaga44.BodyTracking.BodyCalibration>(host);
                Debug.Log($"{LOG} Added BodyCalibration (default height: 1.8m).");
            }
            else
            {
                Debug.Log($"{LOG} BodyCalibration already present.");
            }

            Selection.activeGameObject = host;
        }
#endif

        /// <summary>
        /// Prints instructions for adding Meta Movement SDK.
        /// Called regardless of HAS_META_XR state.
        /// </summary>
        private static void PrintMovementSDKInstructions()
        {
            Debug.Log($"{LOG} ==========================================");
            Debug.Log($"{LOG} META MOVEMENT SDK -- INSTALLATION GUIDE");
            Debug.Log($"{LOG} ==========================================");
            Debug.Log($"{LOG} Movement SDK is NOT included in Meta XR SDK Core.");
            Debug.Log($"{LOG} It must be installed separately.");
            Debug.Log($"{LOG} ");
            Debug.Log($"{LOG} OPTION A -- Unity Package Manager (Git URL):");
            Debug.Log($"{LOG}   Window > Package Manager > + > Add package from git URL:");
            Debug.Log($"{LOG}   https://github.com/oculus-samples/Unity-Movement.git#main");
            Debug.Log($"{LOG} ");
            Debug.Log($"{LOG} OPTION B -- Meta XR Hub:");
            Debug.Log($"{LOG}   Install from Meta XR Hub (requires Meta developer account).");
            Debug.Log($"{LOG}   Package: com.meta.xr.sdk.movement");
            Debug.Log($"{LOG} ");
            Debug.Log($"{LOG} AFTER INSTALLATION:");
            Debug.Log($"{LOG}   1. Add to manifest.json scoped registry (already done if Meta SDK installed).");
            Debug.Log($"{LOG}   2. Verify package com.meta.xr.sdk.movement appears in Package Manager.");
            Debug.Log($"{LOG}   3. Add CharacterRetargeter to your avatar GameObject.");
            Debug.Log($"{LOG}   4. PlayerBody will auto-detect it at runtime.");
            Debug.Log($"{LOG} ==========================================");
        }

        // -- helpers --

        private static void SetBoolProperty(SerializedObject so, string name, bool value, string label)
        {
            var prop = so.FindProperty(name);
            if (prop != null)
            {
                prop.boolValue = value;
                Debug.Log($"{LOG} {label} = {value}");
            }
            else
                Debug.LogWarning($"{LOG} OVRManager property not found: '{name}'. " +
                                 "SDK field name may have changed in this version.");
        }

        private static void SetIntProperty(SerializedObject so, string name, int value, string label)
        {
            var prop = so.FindProperty(name);
            if (prop != null)
            {
                prop.intValue = value;
                Debug.Log($"{LOG} {label} = {value}");
            }
            else
                Debug.LogWarning($"{LOG} OVRManager property not found: '{name}'. " +
                                 "SDK field name may have changed in this version.");
        }
    }
}
#endif
#endif // PLAGA44_FULL_SDK
