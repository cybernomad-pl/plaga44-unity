#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Plaga44.Editor
{
    /// <summary>
    /// One-click TESTBED. Creates:
    /// 1. OVRCameraRig (CAMERA + tracking) -- bare, NO hand prefabs
    /// 2. OVRInteractionComprehensive (ISDK: grab, locomotion, ray, poke, hand visuals)
    ///    -- wired to OVRCameraRig via OVRCameraRigRef._ovrCameraRig
    /// 3. Floor, table with ONE grabbable stone, debug HUD, splash screen
    ///
    /// IMPORTANT: OVRInteractionComprehensive provides ALL hand/controller visuals.
    /// We do NOT add OVRHandPrefab -- that causes double hands!
    /// </summary>
    public static class TestEnvironmentSetup
    {
        private const string LOG = "[PLAGA44]";

        // ISDK script GUIDs (from com.meta.xr.sdk.interaction package)
        private const string GUID_GRABBABLE = "43f86b14a27b52f4f9298c33015b5c26";
        private const string GUID_HAND_GRAB_INTERACTABLE = "e9a7676b01585ce43908639a27765dfc";

        [MenuItem("CYBERNOMAD/Scene Setup/Setup TESTBED", false, 50)]
        public static void SetupTestbed()
        {
            Debug.Log($"{LOG} === Setup TESTBED ===");

            CleanScene();

            // 1. Bare camera rig (NO hand prefabs -- ISDK handles hand visuals)
            var cameraRig = AddCameraRig();

            // 2. ISDK interactions (grab + locomotion + hand visuals) wired to camera rig
            if (cameraRig != null)
                AddInteractions(cameraRig);

            // 3. Environment
            AddFloor();
            AddTableWithStone();

            // 4. Extras
            AddSplashScreen();
            AddDebugHUD(cameraRig);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} === TESTBED READY ===");
            Debug.Log($"{LOG} L-stick = move, R-stick = snap turn, grip = grab stone");
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

        // ==================================================================
        // CAMERA RIG -- bare OVRCameraRig, NO OVRHandPrefab
        // OVRInteractionComprehensive provides all hand/controller visuals!
        // ==================================================================

        static OVRCameraRig AddCameraRig()
        {
            // Check build target
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError($"{LOG} Build target is not Android! Camera rig needs Android target.");
                return null;
            }

            // Find OVRCameraRig prefab
            var prefab = FindPrefab("OVRCameraRig");
            if (prefab == null)
            {
                Debug.LogError($"{LOG} OVRCameraRig prefab not found! Is com.meta.xr.sdk.core installed?");
                return null;
            }

            // Delete existing Main Camera
            var cameras = GameObject.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam.gameObject.name == "Main Camera")
                {
                    Undo.DestroyObjectImmediate(cam.gameObject);
                    Debug.Log($"{LOG} Deleted Main Camera.");
                    break;
                }
            }

            // Instantiate bare rig -- NO hand prefabs!
            var rigGO = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rigGO.transform.position = Vector3.zero;
            rigGO.transform.rotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(rigGO, "Add OVRCameraRig");

            // Configure OVRManager
            var mgr = rigGO.GetComponent<OVRManager>();
            if (mgr != null)
            {
                var so = new SerializedObject(mgr);

                // FloorLevel tracking origin
                var trackOrigin = so.FindProperty("_trackingOriginType");
                if (trackOrigin != null) trackOrigin.intValue = 1;

                so.ApplyModifiedProperties();
                Debug.Log($"{LOG} OVRManager: FloorLevel tracking origin.");
            }

            var rig = rigGO.GetComponent<OVRCameraRig>();
            if (rig == null)
            {
                Debug.LogError($"{LOG} OVRCameraRig component not found on prefab!");
                return null;
            }

            Debug.Log($"{LOG} OVRCameraRig ready (bare rig, no hand prefabs -- ISDK handles visuals).");
            return rig;
        }

        // ==================================================================
        // ISDK INTERACTIONS
        // ==================================================================

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

            // WIRE: find locomotion components and set _playerOrigin + _playerEyes
            WirePlayerOrigin(interaction, cameraRig);

            Debug.Log($"{LOG} OVRInteractionComprehensive wired to OVRCameraRig.");
        }

        static void WireCameraRigRef(GameObject interactionRoot, OVRCameraRig cameraRig)
        {
            var allMBs = interactionRoot.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in allMBs)
            {
                if (mb == null) continue;
                if (mb.GetType().Name != "OVRCameraRigRef") continue;

                var so = new SerializedObject(mb);
                var prop = so.FindProperty("_ovrCameraRig");
                if (prop != null)
                {
                    prop.objectReferenceValue = cameraRig;
                    Debug.Log($"{LOG} Wired OVRCameraRigRef._ovrCameraRig -> {cameraRig.name}");
                }

                // NOTE: _leftHand/_rightHand on OVRCameraRigRef refer to OVRHand components.
                // Since we don't add OVRHandPrefab, these will be null.
                // OVRInteractionComprehensive handles hands internally via ISDK.

                so.ApplyModifiedProperties();
                break;
            }
        }

        static void WirePlayerOrigin(GameObject interactionRoot, OVRCameraRig cameraRig)
        {
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

                var headProp = so.FindProperty("_playerHead");
                if (headProp != null && headProp.propertyType == SerializedPropertyType.ObjectReference)
                {
                    var centerEye = cameraRig.centerEyeAnchor;
                    if (centerEye != null)
                    {
                        headProp.objectReferenceValue = centerEye;
                        so.ApplyModifiedProperties();
                        Debug.Log($"{LOG} Wired _playerHead -> {centerEye.name} on {mb.GetType().Name}");
                    }
                }
            }
        }

        // ==================================================================
        // ENVIRONMENT
        // ==================================================================

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

        static void AddTableWithStone()
        {
            // --- Table ---
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

            // --- ONE grabbable stone on the table ---
            AddGrabbableStone(table.transform);
        }

        /// <summary>
        /// Creates a stone-like grabbable sphere with ISDK components:
        /// Collider (trigger) + Rigidbody (kinematic) + Grabbable + HandGrabInteractable
        /// Matches SDK's [BB] Grabbable Cube structure.
        /// </summary>
        static void AddGrabbableStone(Transform tableParent)
        {
            // Create sphere (slightly squashed = stone shape)
            var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stone.name = "Stone";
            stone.transform.SetParent(tableParent);
            stone.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            stone.transform.localScale = new Vector3(0.12f, 0.09f, 0.10f);
            SetMat(stone, new Color(0.45f, 0.42f, 0.40f));

            // Collider must be TRIGGER for ISDK grab detection
            var col = stone.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            // Rigidbody: kinematic, no gravity (ISDK standard -- Grabbable handles physics)
            var rb = stone.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.mass = 0.4f;

            // Add ISDK Grabbable component via script GUID
            var grabbableScript = LoadScriptByGUID(GUID_GRABBABLE);
            if (grabbableScript == null)
            {
                Debug.LogError($"{LOG} Grabbable script not found (GUID: {GUID_GRABBABLE}). Stone won't be grabbable.");
                // Fallback: at least make it a normal physics object
                rb.isKinematic = false;
                rb.useGravity = true;
                return;
            }

            var grabbableType = grabbableScript.GetClass();
            var grabbable = stone.AddComponent(grabbableType);

            // Wire Grabbable fields
            var gSo = new SerializedObject(grabbable);
            SetObjRef(gSo, "_rigidbody", rb);
            SetBool(gSo, "_kinematicWhileSelected", true);
            SetBool(gSo, "_throwWhenUnselected", true);
            gSo.ApplyModifiedProperties();

            // Add ISDK HandGrabInteractable component via script GUID
            var hgiScript = LoadScriptByGUID(GUID_HAND_GRAB_INTERACTABLE);
            if (hgiScript == null)
            {
                Debug.LogError($"{LOG} HandGrabInteractable script not found (GUID: {GUID_HAND_GRAB_INTERACTABLE}).");
                return;
            }

            var hgiType = hgiScript.GetClass();
            var hgi = stone.AddComponent(hgiType);

            // Wire HandGrabInteractable fields
            var hSo = new SerializedObject(hgi);
            SetObjRef(hSo, "_pointableElement", grabbable);
            SetObjRef(hSo, "_rigidbody", rb);
            // _supportedGrabTypes: 3 = Pinch|Palm (both)
            var grabTypes = hSo.FindProperty("_supportedGrabTypes");
            if (grabTypes != null) grabTypes.intValue = 3;
            hSo.ApplyModifiedProperties();

            Debug.Log($"{LOG} Grabbable stone added: Collider(trigger) + Rigidbody + Grabbable + HandGrabInteractable");
        }

        // ==================================================================
        // EXTRAS
        // ==================================================================

        static void AddSplashScreen()
        {
            var go = new GameObject("SplashScreen");
            go.AddComponent<SplashScreen>();
            Undo.RegisterCreatedObjectUndo(go, "Add SplashScreen");
        }

        /// <summary>
        /// Adds VRInputDebug as a CHILD of OVRCameraRig so it always moves with the player.
        /// VRInputDebug.Update() repositions its canvases relative to centerEyeAnchor,
        /// but parenting ensures it's never left behind during locomotion.
        /// </summary>
        static void AddDebugHUD(OVRCameraRig cameraRig)
        {
            var go = new GameObject("VRInputDebug");
            go.AddComponent<VRInputDebug>();

            // Parent to camera rig so it moves with locomotion
            if (cameraRig != null)
            {
                go.transform.SetParent(cameraRig.transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
            }

            Undo.RegisterCreatedObjectUndo(go, "Add VRInputDebug");
        }

        // ==================================================================
        // HELPERS
        // ==================================================================

        static MonoScript LoadScriptByGUID(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"{LOG} Script GUID not found: {guid}");
                return null;
            }
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script == null)
                Debug.LogError($"{LOG} Could not load MonoScript at: {path}");
            return script;
        }

        static void SetObjRef(SerializedObject so, string propName, Object value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
                prop.objectReferenceValue = value;
        }

        static void SetBool(SerializedObject so, string propName, bool value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
                prop.boolValue = value;
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
