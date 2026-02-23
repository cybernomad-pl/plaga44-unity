#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Plaga44.Editor
{
    /// <summary>
    /// One-click TESTBED. Creates:
    /// 1. OVRCameraRig (CAMERA + tracking) with hands
    /// 2. OVRInteractionComprehensive (ISDK: grab, locomotion, ray, poke)
    ///    -- wired to OVRCameraRig via OVRCameraRigRef._ovrCameraRig
    /// 3. Floor, table, debug HUD, splash screen
    /// </summary>
    public static class TestEnvironmentSetup
    {
        private const string LOG = "[PLAGA44]";

        [MenuItem("CYBERNOMAD/Scene Setup/Setup TESTBED", false, 50)]
        public static void SetupTestbed()
        {
            Debug.Log($"{LOG} === Setup TESTBED ===");

            CleanScene();

            // 1. Camera rig + hands
            var cameraRig = AddCameraRig();

            // 2. ISDK interactions (grab + locomotion) wired to camera rig
            if (cameraRig != null)
                AddInteractions(cameraRig);

            // 3. Environment
            AddFloor();
            AddTestTable();

            // 4. Extras
            AddSplashScreen();
            AddDebugHUD();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} === TESTBED READY ===");
            Debug.Log($"{LOG} L-stick = move, R-stick = snap turn, grip = grab");
        }

        [MenuItem("CYBERNOMAD/Scene Setup/Clean Scene", false, 200)]
        public static void CleanScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Directional Light" || root.name == "Global Volume")
                    continue;
                Undo.DestroyObjectImmediate(root);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{LOG} Scene cleaned.");
        }

        // ---- CAMERA RIG ----

        static OVRCameraRig AddCameraRig()
        {
            // Use MetaQuestSetup which adds OVRCameraRig + OVRHandPrefabs + configures OVRManager
            MetaQuestSetup.SetupVRSceneHands();

            // Find the OVRCameraRig that was just added
            var rig = GameObject.FindFirstObjectByType<OVRCameraRig>();
            if (rig == null)
            {
                Debug.LogError($"{LOG} OVRCameraRig not found after setup!");
                return null;
            }
            Debug.Log($"{LOG} OVRCameraRig ready (camera + hands).");
            return rig;
        }

        // ---- ISDK INTERACTIONS ----

        static void AddInteractions(OVRCameraRig cameraRig)
        {
            var prefab = FindPrefab("OVRInteractionComprehensive");
            if (prefab == null)
            {
                Debug.LogWarning($"{LOG} OVRInteractionComprehensive not found. No grab/locomotion.");
                return;
            }

            var interaction = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            interaction.transform.position = Vector3.zero;
            interaction.transform.rotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(interaction, "Add ISDK Interactions");

            // WIRE: find OVRCameraRigRef component and set _ovrCameraRig
            WireCameraRigRef(interaction, cameraRig);

            // WIRE: find PlayerLocomotor and set _playerOrigin + _playerHead
            WirePlayerOrigin(interaction, cameraRig);

            Debug.Log($"{LOG} OVRInteractionComprehensive wired to OVRCameraRig.");
        }

        static void WireCameraRigRef(GameObject interactionRoot, OVRCameraRig cameraRig)
        {
            // OVRCameraRigRef is the central config point -- has _ovrCameraRig field
            var rigRefs = interactionRoot.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in rigRefs)
            {
                if (mb == null) continue;
                if (mb.GetType().Name != "OVRCameraRigRef") continue;

                var so = new SerializedObject(mb);
                var prop = so.FindProperty("_ovrCameraRig");
                if (prop != null)
                {
                    prop.objectReferenceValue = cameraRig;
                    so.ApplyModifiedProperties();
                    Debug.Log($"{LOG} Wired OVRCameraRigRef._ovrCameraRig -> {cameraRig.name}");
                }

                // Also wire _leftHand and _rightHand if we can find them
                var leftHandProp = so.FindProperty("_leftHand");
                var rightHandProp = so.FindProperty("_rightHand");
                if (leftHandProp != null || rightHandProp != null)
                {
                    var hands = cameraRig.GetComponentsInChildren<OVRHand>(true);
                    foreach (var hand in hands)
                    {
                        var handSo = new SerializedObject(hand);
                        var handType = handSo.FindProperty("HandType");
                        if (handType != null)
                        {
                            if (handType.intValue == 0 && leftHandProp != null) // HandLeft
                            {
                                leftHandProp.objectReferenceValue = hand;
                                Debug.Log($"{LOG} Wired _leftHand -> {hand.name}");
                            }
                            else if (handType.intValue == 1 && rightHandProp != null) // HandRight
                            {
                                rightHandProp.objectReferenceValue = hand;
                                Debug.Log($"{LOG} Wired _rightHand -> {hand.name}");
                            }
                        }
                    }
                    so.ApplyModifiedProperties();
                }
                break; // Only one OVRCameraRigRef expected
            }
        }

        static void WirePlayerOrigin(GameObject interactionRoot, OVRCameraRig cameraRig)
        {
            // Find any component with _playerOrigin or _playerEyes fields
            var allMBs = interactionRoot.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in allMBs)
            {
                if (mb == null) continue;
                var so = new SerializedObject(mb);

                var originProp = so.FindProperty("_playerOrigin");
                if (originProp != null && originProp.propertyType == SerializedPropertyType.ObjectReference)
                {
                    originProp.objectReferenceValue = cameraRig.transform;
                    so.ApplyModifiedProperties();
                    Debug.Log($"{LOG} Wired _playerOrigin -> {cameraRig.name} on {mb.GetType().Name}");
                }

                var eyesProp = so.FindProperty("_playerEyes");
                if (eyesProp != null && eyesProp.propertyType == SerializedPropertyType.ObjectReference)
                {
                    var centerEye = cameraRig.centerEyeAnchor;
                    if (centerEye != null)
                    {
                        eyesProp.objectReferenceValue = centerEye;
                        so.ApplyModifiedProperties();
                        Debug.Log($"{LOG} Wired _playerEyes -> {centerEye.name} on {mb.GetType().Name}");
                    }
                }
            }
        }

        // ---- ENVIRONMENT ----

        static void AddFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(100f, 1f, 100f);
            floor.isStatic = true;
            SetMat(floor, new Color(0.22f, 0.22f, 0.25f));
            Undo.RegisterCreatedObjectUndo(floor, "Add Floor");
        }

        static void AddTestTable()
        {
            var table = new GameObject("TestTable");
            table.transform.position = new Vector3(0f, 0f, 1.5f);
            Undo.RegisterCreatedObjectUndo(table, "Add TestTable");

            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "TableTop";
            top.transform.SetParent(table.transform);
            top.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            top.transform.localScale = new Vector3(1.2f, 0.05f, 0.6f);
            top.isStatic = true;
            SetMat(top, new Color(0.45f, 0.3f, 0.15f));

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
                SetMat(leg, new Color(0.35f, 0.22f, 0.1f));
            }

            float y = 0.85f;
            AddObj(table.transform, "RedCube", PrimitiveType.Cube,
                new Vector3(-0.3f, y, 0f), Vector3.one * 0.12f, new Color(0.8f, 0.2f, 0.2f), 0.3f);
            AddObj(table.transform, "GreenSphere", PrimitiveType.Sphere,
                new Vector3(0f, y, 0f), Vector3.one * 0.1f, new Color(0.2f, 0.7f, 0.2f), 0.15f);
            AddObj(table.transform, "Stone", PrimitiveType.Sphere,
                new Vector3(0.3f, y, 0f), new Vector3(0.08f, 0.06f, 0.08f), new Color(0.5f, 0.5f, 0.5f), 0.4f);
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

        static void AddObj(Transform parent, string name, PrimitiveType type,
            Vector3 pos, Vector3 scale, Color color, float mass)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.localPosition = pos;
            obj.transform.localScale = scale;
            SetMat(obj, color);
            var rb = obj.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        static GameObject FindPrefab(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains(name + ".prefab"))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null) return prefab;
                }
            }
            return null;
        }

        static void SetMat(GameObject obj, Color color)
        {
            var r = obj.GetComponent<Renderer>();
            if (r == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color");
            if (shader == null) return;
            r.sharedMaterial = new Material(shader) { color = color };
        }
    }
}
#endif
