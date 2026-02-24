#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    /// <summary>
    /// One-click TESTBED: OVRPlayerController (camera + hands + thumbstick movement + gravity)
    /// + OVRGrabber on controllers + ground + table + grabbable stone + splash + debug HUD.
    /// OVRPlayerController has OVRCameraRig as CHILD -- no separate camera rig needed.
    /// OVRGrabber/OVRGrabbable from com.meta.xr.sdk.core -- simplest grab system.
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
            AddStoneSpawner(player.transform.position);
            TargetFactory.AddTestTargets();
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

            // Grab -- OVRGrabber on controller anchors
            AddGrabbers(rigTransform, player);

            Debug.Log($"{LOG} OVRPlayerController ready (camera + hands + movement + gravity + grab).");
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
            var topMat = new PhysicsMaterial("WoodMat")
            {
                dynamicFriction = 0.7f,
                staticFriction = 0.8f,
                bounciness = 0.02f,
                frictionCombine = PhysicsMaterialCombine.Maximum
            };
            top.GetComponent<Collider>().material = topMat;

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

            // Stones -- scattered on table, STACKABLE by player
            // High friction + zero bounce so they grip when placed on each other.
            var stoneMat = new PhysicsMaterial("StoneMat")
            {
                dynamicFriction = 1.0f,
                staticFriction = 1.0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Maximum
            };

            // Table top at Y=0.775 (0.75 + half of 0.05). Scatter on surface, no overlaps.
            float tableY = 0.82f;
            Vector3[] stonePositions =
            {
                new Vector3(-0.15f, tableY,  0.00f),
                new Vector3(-0.02f, tableY, -0.08f),
                new Vector3( 0.12f, tableY, -0.02f),
                new Vector3( 0.00f, tableY,  0.10f),
                new Vector3(-0.10f, tableY,  0.12f),
                new Vector3( 0.15f, tableY,  0.10f),
                new Vector3( 0.02f, tableY,  0.00f),
                new Vector3(-0.08f, tableY, -0.10f),
            };

            // Near-uniform scales
            float[][] stoneSizes =
            {
                new[] { 0.08f, 0.07f, 0.08f },
                new[] { 0.07f, 0.06f, 0.07f },
                new[] { 0.09f, 0.08f, 0.08f },
                new[] { 0.06f, 0.06f, 0.07f },
                new[] { 0.07f, 0.07f, 0.06f },
                new[] { 0.08f, 0.07f, 0.07f },
                new[] { 0.10f, 0.09f, 0.09f },
                new[] { 0.06f, 0.05f, 0.06f },
            };

            float[] stoneGrays = { 0.45f, 0.38f, 0.50f, 0.42f, 0.35f, 0.48f, 0.40f, 0.52f };

            for (int s = 0; s < stonePositions.Length; s++)
            {
                AddStone(table.transform, $"Stone{s}", stonePositions[s],
                    stoneSizes[s], stoneGrays[s], stoneMat);
            }
        }

        static void AddStone(Transform parent, string name, Vector3 localPos,
            float[] size, float gray, PhysicsMaterial mat)
        {
            var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stone.name = name;
            stone.transform.SetParent(parent);
            stone.transform.localPosition = localPos;
            stone.transform.localScale = new Vector3(size[0], size[1], size[2]);
            SetUnlitMaterial(stone, new Color(gray, gray - 0.03f, gray - 0.05f));

            // Cross-shaped compound collider: 2 boxes at 90 degrees.
            // Wide+thick box for width, thin+long box for depth.
            // Gives irregular stone-like contact -- flat faces for stacking,
            // but not a perfect cube so they look/feel natural.
            Object.DestroyImmediate(stone.GetComponent<SphereCollider>());

            // Box 1: wide (X) and thick (Y), shorter depth (Z)
            var wideChild = new GameObject("Col_Wide");
            wideChild.transform.SetParent(stone.transform, false);
            var wideBox = wideChild.AddComponent<BoxCollider>();
            wideBox.size = new Vector3(0.9f, 0.8f, 0.5f);
            wideBox.material = mat;

            // Box 2: narrow (X), thinner (Y), long depth (Z)
            var longChild = new GameObject("Col_Long");
            longChild.transform.SetParent(stone.transform, false);
            var longBox = longChild.AddComponent<BoxCollider>();
            longBox.size = new Vector3(0.5f, 0.6f, 0.9f);
            longBox.material = mat;

            // Physics -- heavy + high damping = stable stacking
            var rb = stone.AddComponent<Rigidbody>();
            rb.mass = 1.0f;
            rb.linearDamping = 1.0f;
            rb.angularDamping = 2.0f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // OVRGrabbable -- grab points = both colliders
            var grabbable = stone.AddComponent<OVRGrabbable>();
            var gso = new SerializedObject(grabbable);
            SetBool(gso, "m_allowOffhandGrab", true);
            var grabPointsProp = gso.FindProperty("m_grabPoints");
            if (grabPointsProp != null)
            {
                grabPointsProp.arraySize = 2;
                grabPointsProp.GetArrayElementAtIndex(0).objectReferenceValue = wideBox;
                grabPointsProp.GetArrayElementAtIndex(1).objectReferenceValue = longBox;
            }
            gso.ApplyModifiedProperties();

            // GazeThrow -- gaze-corrected throwing with velocity boost
            var gt = stone.AddComponent<GazeThrow>();
            gt.boostMultiplier = 5.0f;

            // HitDetector -- registers hits on HitTarget zones
            stone.AddComponent<Plaga44.Gameplay.HitDetector>();
        }

        // ---- SPAWNER ----

        static void AddStoneSpawner(Vector3 playerPos)
        {
            var go = new GameObject("StoneSpawner");
            // Position at table center
            go.transform.position = playerPos + new Vector3(0f, 0f, 1.2f);
            go.AddComponent<StoneSpawner>();
            Undo.RegisterCreatedObjectUndo(go, "Add StoneSpawner");
            Debug.Log($"{LOG} StoneSpawner added (new stone every 20s).");
        }

        // ---- GRAB ----

        static void AddGrabbers(Transform rigTransform, GameObject player)
        {
            if (rigTransform == null) return;

            var leftCtrl = FindChild(rigTransform, "LeftControllerAnchor");
            var rightCtrl = FindChild(rigTransform, "RightControllerAnchor");

            if (leftCtrl != null)
            {
                SetupGrabber(leftCtrl.gameObject, 1, player); // 1 = OVRInput.Controller.LTouch
                AddControllerVisual(leftCtrl, 1);
            }
            if (rightCtrl != null)
            {
                SetupGrabber(rightCtrl.gameObject, 2, player); // 2 = OVRInput.Controller.RTouch
                AddControllerVisual(rightCtrl, 2);
            }

            Debug.Log($"{LOG} OVRGrabber + controller visuals added to anchors.");
        }

        static void SetupGrabber(GameObject anchorGO, int controllerValue, GameObject player)
        {
            // Rigidbody (kinematic -- pushes dynamic objects but isn't moved by them)
            var rb = anchorGO.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Hand-shaped compound collider (child objects inherit parent Rigidbody)
            // Tight fit -- some clipping OK, no "force field"
            // Palm -- thin box at grip center
            var palm = new GameObject("PalmCollider");
            palm.transform.SetParent(anchorGO.transform, false);
            palm.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            var palmCol = palm.AddComponent<BoxCollider>();
            palmCol.size = new Vector3(0.05f, 0.02f, 0.05f);

            // Fingers -- thin capsule extending forward
            var fingers = new GameObject("FingersCollider");
            fingers.transform.SetParent(anchorGO.transform, false);
            fingers.transform.localPosition = new Vector3(0f, 0f, 0.08f);
            var fingersCol = fingers.AddComponent<CapsuleCollider>();
            fingersCol.direction = 2; // Z axis (forward)
            fingersCol.radius = 0.012f;
            fingersCol.height = 0.07f;

            // Thumb -- tiny sphere offset to side
            var thumb = new GameObject("ThumbCollider");
            thumb.transform.SetParent(anchorGO.transform, false);
            thumb.transform.localPosition = new Vector3(0.03f, 0f, 0.04f);
            var thumbCol = thumb.AddComponent<SphereCollider>();
            thumbCol.radius = 0.012f;

            // Trigger collider -- grab detection volume (on anchor, covers whole hand area)
            var grabCol = anchorGO.AddComponent<SphereCollider>();
            grabCol.isTrigger = true;
            grabCol.radius = 0.1f;
            grabCol.center = new Vector3(0f, 0f, 0.06f);

            // OVRGrabber
            var grabber = anchorGO.AddComponent<OVRGrabber>();
            var so = new SerializedObject(grabber);

            SetInt(so, "m_controller", controllerValue);
            // parentHeldObject=false -> OVRGrabber uses Rigidbody.MovePosition instead of
            // transform parenting. MovePosition preserves physics collision with other objects.
            SetBool(so, "m_parentHeldObject", false);

            // Grip transform = this anchor (where grabbed objects snap to)
            var gripProp = so.FindProperty("m_gripTransform");
            if (gripProp != null)
                gripProp.objectReferenceValue = anchorGO.transform;

            // Grab volume = the trigger SphereCollider (NOT the physical one)
            var volumesProp = so.FindProperty("m_grabVolumes");
            if (volumesProp != null)
            {
                volumesProp.arraySize = 1;
                volumesProp.GetArrayElementAtIndex(0).objectReferenceValue = grabCol;
            }

            // NOTE: m_player deliberately NOT set. OVRGrabber.GrabBegin() calls
            // SetPlayerIgnoreCollision(obj, true) but GrabEnd() never restores it.
            // With m_player=null, IgnoreCollision is skipped entirely, so hand-object
            // collision stays active after grab+release.

            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// Adds OVRControllerPrefab as child of controller anchor for visual reference.
        /// </summary>
        static void AddControllerVisual(Transform anchor, int controllerValue)
        {
            string[] guids = AssetDatabase.FindAssets("OVRControllerPrefab t:prefab");
            GameObject prefab = null;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("OVRControllerPrefab.prefab"))
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null) break;
                }
            }

            if (prefab == null)
            {
                Debug.LogWarning($"{LOG} OVRControllerPrefab.prefab not found -- skipping controller visual.");
                return;
            }

            var vis = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            vis.name = "ControllerVisual";
            vis.transform.SetParent(anchor, false);
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localRotation = Quaternion.identity;

            // Set controller type via OVRControllerHelper (if present)
            var helper = vis.GetComponent<OVRControllerHelper>();
            if (helper != null)
            {
                var so = new SerializedObject(helper);
                // m_controller: 1 = LTouch, 2 = RTouch
                SetInt(so, "m_controller", controllerValue);
                so.ApplyModifiedProperties();
            }
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
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
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
