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

            // Ensure ground plane exists for gravity -- reuse existing floor from TESTBED
            var ground = GameObject.Find("InfiniteFloor") ?? GameObject.Find("Ground");
            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = new Vector3(100f, 1f, 100f);
                SetUnlitMaterial(ground, new Color(0.25f, 0.25f, 0.25f));
                Undo.RegisterCreatedObjectUndo(ground, "Add Ground");
                Debug.Log($"{LOG} Ground plane added (Unlit material).");
            }
            else
            {
                // Fix existing ground material to Unlit
                SetUnlitMaterial(ground, new Color(0.25f, 0.25f, 0.25f));
                Debug.Log($"{LOG} Reusing existing floor: {ground.name} (switched to Unlit).");
            }

            // Add table with test objects at hand height
            AddTestTable(player.transform.position);

            Selection.activeGameObject = player;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} === MVP Locomotion DONE ===");
            Debug.Log($"{LOG} Controls: Left thumbstick = move, Right thumbstick = snap turn 45deg");
            Debug.Log($"{LOG} Gravity is handled by CharacterController + OVRPlayerController.");
        }

        private static void AddTestTable(Vector3 playerPos)
        {
            // Table 1m in front of player, at waist height
            var existing = GameObject.Find("TestTable");
            if (existing != null)
            {
                Debug.Log($"{LOG} TestTable already exists, skipping.");
                return;
            }

            var table = new GameObject("TestTable");
            table.transform.position = playerPos + new Vector3(0f, 0f, 1.2f);
            Undo.RegisterCreatedObjectUndo(table, "Add Test Table");

            // Table top
            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "TableTop";
            top.transform.SetParent(table.transform);
            top.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            top.transform.localScale = new Vector3(1.2f, 0.05f, 0.6f);
            SetUnlitMaterial(top, new Color(0.45f, 0.3f, 0.15f));

            // Table legs
            for (int i = 0; i < 4; i++)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = $"Leg{i}";
                leg.transform.SetParent(table.transform);
                float x = (i % 2 == 0) ? -0.5f : 0.5f;
                float z = (i < 2) ? -0.25f : 0.25f;
                leg.transform.localPosition = new Vector3(x, 0.375f, z);
                leg.transform.localScale = new Vector3(0.05f, 0.75f, 0.05f);
                SetUnlitMaterial(leg, new Color(0.35f, 0.22f, 0.1f));
            }

            // Objects ON the table (grabbable height ~0.85m)
            float tableY = 0.85f;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "GrabbableCube";
            cube.transform.SetParent(table.transform);
            cube.transform.localPosition = new Vector3(-0.3f, tableY, 0f);
            cube.transform.localScale = Vector3.one * 0.12f;
            SetUnlitMaterial(cube, new Color(0.8f, 0.2f, 0.2f));
            var rbCube = cube.AddComponent<Rigidbody>();
            rbCube.mass = 0.3f;
            rbCube.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "GrabbableSphere";
            sphere.transform.SetParent(table.transform);
            sphere.transform.localPosition = new Vector3(0f, tableY, 0f);
            sphere.transform.localScale = Vector3.one * 0.1f;
            SetUnlitMaterial(sphere, new Color(0.2f, 0.7f, 0.2f));
            var rbSphere = sphere.AddComponent<Rigidbody>();
            rbSphere.mass = 0.15f;
            rbSphere.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stone.name = "Stone";
            stone.transform.SetParent(table.transform);
            stone.transform.localPosition = new Vector3(0.3f, tableY, 0f);
            stone.transform.localScale = new Vector3(0.08f, 0.06f, 0.08f);
            SetUnlitMaterial(stone, new Color(0.5f, 0.5f, 0.5f));
            var rbStone = stone.AddComponent<Rigidbody>();
            rbStone.mass = 0.4f;
            rbStone.collisionDetectionMode = CollisionDetectionMode.Continuous;

            Debug.Log($"{LOG} TestTable with 3 objects added at hand height.");
        }

        private static void SetUnlitMaterial(GameObject obj, Color color)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Use Unlit shader to avoid VR lighting flicker
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                var mat = new Material(shader);
                mat.color = color;
                renderer.sharedMaterial = mat;
            }
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
