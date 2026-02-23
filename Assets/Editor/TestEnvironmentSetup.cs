#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    /// <summary>
    /// One-click TESTBED: OVRPlayerController (camera + hands + thumbstick movement + gravity)
    /// + ground + table + stone + splash + debug HUD.
    /// OVRPlayerController has OVRCameraRig as CHILD -- no separate camera rig needed.
    /// </summary>
    public static class TestEnvironmentSetup
    {
        private const string LOG = "[PLAGA44]";

        [MenuItem("CYBERNOMAD/Scene Setup/Setup TESTBED", false, 50)]
        public static void SetupTestbed()
        {
            Debug.Log($"{LOG} === Setup TESTBED ===");

            CleanScene();

            // OVRPlayerController = camera rig + hands + movement + gravity (all in one)
            var player = AddPlayerController();
            if (player == null) return;

            AddGround();
            AddTestTable(player.transform.position);
            AddSplashScreen();
            AddDebugHUD();

            Selection.activeGameObject = player;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} === TESTBED READY ===");
            Debug.Log($"{LOG} L-stick = move, R-stick = snap turn, grip = grab");
        }

        [MenuItem("CYBERNOMAD/Scene Setup/Clean Scene", false, 200)]
        public static void CleanScene()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Directional Light" || root.name == "Global Volume")
                    continue;
                Undo.DestroyObjectImmediate(root);
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{LOG} Scene cleaned.");
        }

        // ---- PLAYER (camera + hands + movement + gravity) ----

        static GameObject AddPlayerController()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError($"{LOG} Build target is not Android!");
                return null;
            }

            // Find OVRPlayerController prefab
            string[] guids = AssetDatabase.FindAssets("OVRPlayerController t:prefab");
            GameObject prefab = null;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("OVRPlayerController.prefab"))
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null) break;
                }
            }
            if (prefab == null)
            {
                Debug.LogError($"{LOG} OVRPlayerController.prefab not found!");
                return null;
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.transform.position = Vector3.zero;
            player.transform.rotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(player, "Add OVRPlayerController");

            // Configure movement
            var controller = player.GetComponent<OVRPlayerController>();
            if (controller != null)
            {
                var so = new SerializedObject(controller);
                SetFloat(so, "Acceleration", 0.1f);
                SetFloat(so, "Damping", 0.3f);
                SetFloat(so, "RotationAmount", 45f);
                SetBool(so, "EnableLinearMovement", true);
                SetBool(so, "EnableRotation", true);
                so.ApplyModifiedProperties();
            }

            // Configure OVRManager on child OVRCameraRig
            var rigTransform = FindChild(player.transform, "OVRCameraRig");
            if (rigTransform != null)
            {
                var mgr = rigTransform.GetComponent<OVRManager>();
                if (mgr != null)
                {
                    var so = new SerializedObject(mgr);
                    SetInt(so, "_trackingOriginType", 1); // FloorLevel
                    SetInt(so, "controllerDrivenHandPosesType", 1); // ConformingToController
                    SetBool(so, "launchSimultaneousHandsControllersOnStartup", true);
                    SetBool(so, "SimultaneousHandsAndControllersEnabled", true);
                    so.ApplyModifiedProperties();
                }
                MetaQuestSetup.AddOVRHandPrefabs(rigTransform.gameObject);
            }

            // CharacterController for gravity
            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.height = 1.8f;
                cc.radius = 0.3f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.skinWidth = 0.02f;
            }

            Debug.Log($"{LOG} OVRPlayerController ready (camera + hands + movement + gravity).");
            return player;
        }

        // ---- ENVIRONMENT ----

        static void AddGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(100f, 1f, 100f);
            ground.isStatic = true;
            SetUnlitMaterial(ground, new Color(0.25f, 0.25f, 0.25f));
            Undo.RegisterCreatedObjectUndo(ground, "Add Ground");
        }

        static void AddTestTable(Vector3 playerPos)
        {
            var table = new GameObject("TestTable");
            table.transform.position = playerPos + new Vector3(0f, 0f, 1.2f);
            Undo.RegisterCreatedObjectUndo(table, "Add TestTable");

            // Table top
            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "TableTop";
            top.transform.SetParent(table.transform);
            top.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            top.transform.localScale = new Vector3(1.2f, 0.05f, 0.6f);
            top.isStatic = true;
            SetUnlitMaterial(top, new Color(0.45f, 0.3f, 0.15f));

            // Legs
            float[] xs = { -0.5f, 0.5f, -0.5f, 0.5f };
            float[] zs = { -0.25f, -0.25f, 0.25f, 0.25f };
            for (int i = 0; i < 4; i++)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = $"Leg{i}";
                leg.transform.SetParent(table.transform);
                leg.transform.localPosition = new Vector3(xs[i], 0.375f, zs[i]);
                leg.transform.localScale = new Vector3(0.05f, 0.75f, 0.05f);
                leg.isStatic = true;
                SetUnlitMaterial(leg, new Color(0.35f, 0.22f, 0.1f));
            }

            // Stone
            var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stone.name = "Stone";
            stone.transform.SetParent(table.transform);
            stone.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            stone.transform.localScale = new Vector3(0.12f, 0.09f, 0.10f);
            SetUnlitMaterial(stone, new Color(0.45f, 0.42f, 0.40f));
            var rb = stone.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        // ---- EXTRAS ----

        static void AddSplashScreen()
        {
            var go = new GameObject("SplashScreen");
            go.AddComponent<SplashScreen>();
            Undo.RegisterCreatedObjectUndo(go, "Add SplashScreen");
        }

        static void AddDebugHUD()
        {
            var go = new GameObject("VRInputDebug");
            go.AddComponent<VRInputDebug>();
            Undo.RegisterCreatedObjectUndo(go, "Add VRInputDebug");
        }

        // ---- HELPERS ----

        static void SetUnlitMaterial(GameObject obj, Color color)
        {
            var r = obj.GetComponent<Renderer>();
            if (r == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return;
            r.sharedMaterial = new Material(shader) { color = color };
        }

        static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        static void SetFloat(SerializedObject so, string name, float val)
        {
            var p = so.FindProperty(name);
            if (p != null) p.floatValue = val;
        }

        static void SetInt(SerializedObject so, string name, int val)
        {
            var p = so.FindProperty(name);
            if (p != null) p.intValue = val;
        }

        static void SetBool(SerializedObject so, string name, bool val)
        {
            var p = so.FindProperty(name);
            if (p != null) p.boolValue = val;
        }
    }
}
#endif
