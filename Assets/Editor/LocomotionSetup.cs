#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    /// <summary>
    /// MVP Locomotion setup using Meta's OVRPlayerController prefab.
    /// OVRPlayerController has built-in: CharacterController + thumbstick movement + snap turn + gravity.
    /// This replaces standalone OVRCameraRig with OVRPlayerController (which contains OVRCameraRig as child).
    /// </summary>
    public static class LocomotionSetup
    {
        private const string LOG = "[PLAGA44]";

        [MenuItem("CYBERNOMAD/Scene Setup/Add Locomotion (MVP)", false, 102)]
        public static void SetupLocomotion()
        {
            Debug.Log($"{LOG} === MVP Locomotion Setup ===");

            // Find OVRPlayerController prefab in SDK
            string[] guids = AssetDatabase.FindAssets("OVRPlayerController t:prefab");
            GameObject playerPrefab = null;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("OVRPlayerController.prefab"))
                {
                    playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (playerPrefab != null)
                    {
                        Debug.Log($"{LOG} Found OVRPlayerController: {path}");
                        break;
                    }
                }
            }

            if (playerPrefab == null)
            {
                Debug.LogError($"{LOG} OVRPlayerController.prefab not found! Is com.meta.xr.sdk.core installed?");
                return;
            }

            // Remove existing OVRCameraRig / OVRPlayerController / Main Camera
            RemoveExisting("OVRPlayerController");
            RemoveExisting("OVRCameraRig");
            RemoveExisting("Main Camera");

            // Instantiate OVRPlayerController
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = new Vector3(0f, 0f, 0f);
            player.transform.rotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(player, "Add OVRPlayerController");

            // Configure OVRPlayerController
            var controller = player.GetComponent<OVRPlayerController>();
            if (controller != null)
            {
                var so = new SerializedObject(controller);

                // Movement speed
                var accel = so.FindProperty("Acceleration");
                if (accel != null) accel.floatValue = 0.1f;

                var damping = so.FindProperty("Damping");
                if (damping != null) damping.floatValue = 0.3f;

                // Snap turn
                var rotAmount = so.FindProperty("RotationAmount");
                if (rotAmount != null) rotAmount.floatValue = 45f;

                // Enable linear movement
                var enableLinear = so.FindProperty("EnableLinearMovement");
                if (enableLinear != null) enableLinear.boolValue = true;

                // Enable rotation
                var enableRot = so.FindProperty("EnableRotation");
                if (enableRot != null) enableRot.boolValue = true;

                so.ApplyModifiedProperties();
                Debug.Log($"{LOG} OVRPlayerController configured: movement + snap turn.");
            }

            // Configure OVRManager on the child OVRCameraRig
            var rig = FindChildRecursive(player.transform, "OVRCameraRig");
            if (rig != null)
            {
                var mgr = rig.GetComponent<OVRManager>();
                if (mgr != null)
                {
                    var so = new SerializedObject(mgr);

                    // FloorLevel tracking
                    var trackingOrigin = so.FindProperty("_trackingOriginType");
                    if (trackingOrigin != null) trackingOrigin.intValue = 1;

                    // Controller-driven hand poses
                    var handPoses = so.FindProperty("controllerDrivenHandPosesType");
                    if (handPoses != null) handPoses.intValue = 1;

                    // Simultaneous hands+controllers
                    var simLaunch = so.FindProperty("launchSimultaneousHandsControllersOnStartup");
                    if (simLaunch != null) simLaunch.boolValue = true;

                    var simEnabled = so.FindProperty("SimultaneousHandsAndControllersEnabled");
                    if (simEnabled != null) simEnabled.boolValue = true;

                    so.ApplyModifiedProperties();
                    Debug.Log($"{LOG} OVRManager configured on OVRCameraRig child.");
                }

                // Add hand prefabs
                MetaQuestSetup.AddOVRHandPrefabs(rig.gameObject);
            }
            else
            {
                Debug.LogWarning($"{LOG} OVRCameraRig child not found in OVRPlayerController -- hands not added.");
            }

            // Configure CharacterController for gravity
            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.height = 1.8f;
                cc.radius = 0.3f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.skinWidth = 0.02f;
                Debug.Log($"{LOG} CharacterController: h=1.8, r=0.3, gravity via OVRPlayerController.");
            }

            // Ensure ground plane exists for gravity
            var ground = GameObject.Find("Ground");
            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = new Vector3(10f, 1f, 10f);
                var renderer = ground.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.3f, 0.3f, 0.3f);
                    renderer.sharedMaterial = mat;
                }
                Undo.RegisterCreatedObjectUndo(ground, "Add Ground");
                Debug.Log($"{LOG} Ground plane added.");
            }

            Selection.activeGameObject = player;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} === MVP Locomotion DONE ===");
            Debug.Log($"{LOG} Controls: Left thumbstick = move, Right thumbstick = snap turn 45deg");
            Debug.Log($"{LOG} Gravity is handled by CharacterController + OVRPlayerController.");
        }

        private static void RemoveExisting(string name)
        {
            var objects = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in objects)
            {
                if (t != null && t.name == name && t.parent == null)
                {
                    Debug.Log($"{LOG} Removing existing {name}.");
                    Undo.DestroyObjectImmediate(t.gameObject);
                }
            }
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
