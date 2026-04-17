#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    /// <summary>
    /// Adds smooth turn to existing LocomotionController.
    /// Menu: CYBERNOMAD > Locomotion > Add Smooth Turn
    /// </summary>
    public static class ISDKLocomotionSetup
    {
        private const string LOG = "[PLAGA44][Loco]";

        [MenuItem("CYBERNOMAD/Config/Locomotion/Add Smooth Turn to Rig", false, 60)]
        public static void Setup()
        {
            var rig = Object.FindFirstObjectByType<OVRCameraRig>();
            if (rig == null)
            {
                Debug.LogError($"{LOG} OVRCameraRig not found!");
                return;
            }

            var rigGO = rig.gameObject;

            // Add SmoothTurnController if not present
            var turn = rigGO.GetComponent<Plaga44.Locomotion.SmoothTurnController>();
            if (turn == null)
                turn = Undo.AddComponent<Plaga44.Locomotion.SmoothTurnController>(rigGO);

            Debug.Log($"{LOG} SmoothTurnController added to {rigGO.name}");

            EditorUtility.SetDirty(rigGO);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} Right thumbstick = smooth turn (120 deg/s)");
        }
    }
}
#endif
