#if UNITY_EDITOR
using Plaga44.AI;
using Plaga44.Core;
using Plaga44.Interaction;
using Plaga44.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

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

        // [MenuItem("CYBERNOMAD/Scene Setup/Setup TESTBED", false, 50)]
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
            AddVRUI(player);
            AddHapticFeedback();

            Selection.activeGameObject = player;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} === TESTBED READY ===");
            Debug.Log($"{LOG} L-stick = move, R-stick = snap turn, grip = grab");
        }

        // ---- AI TESTBED ----

        /// <summary>
        /// Sets up the AI testbed: TESTBED base + 3 patrol enemies on a NavMesh-ready ground.
        ///
        /// NavMesh baking:
        ///   This setup adds a NavMeshSurface component to the ground.
        ///   You MUST manually bake the NavMesh after running this setup:
        ///   Window -> AI -> Navigation -> Bake  (or the NavMeshSurface component on Ground).
        ///   Enemies will log a warning if no NavMesh is found at their spawn position.
        /// </summary>
        // [MenuItem("CYBERNOMAD/Scene Setup/Setup AI Testbed", false, 51)]
        public static void SetupAITestbed()
        {
            Debug.Log($"{LOG} === Setup AI TESTBED ===");

            // First run the base testbed
            SetupTestbed();

            // Then add AI layer on top
            AddNavMeshSurface();
            AddEnemySpawner();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} === AI TESTBED READY ===");
            Debug.Log($"{LOG} IMPORTANT: Bake NavMesh! Window -> AI -> Navigation -> Bake");
            Debug.Log($"{LOG} Enemies: 3 capsules with patrol routes. Throw stones to damage them.");
        }

        static void AddNavMeshSurface()
        {
            // Find the Ground plane and add a NavMeshSurface so the NavMesh can be baked
            var ground = GameObject.Find("Ground");
            if (ground == null)
            {
                Debug.LogWarning($"{LOG} Ground not found -- NavMeshSurface not added. Run Setup TESTBED first.");
                return;
            }

            // Check if already has a NavMeshSurface
            if (ground.GetComponent<NavMeshSurface>() != null)
            {
                Debug.Log($"{LOG} NavMeshSurface already on Ground.");
                return;
            }

            var surface = ground.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            Undo.RegisterCreatedObjectUndo(ground, "Add NavMeshSurface");

            Debug.Log($"{LOG} NavMeshSurface added to Ground. Bake NavMesh in Window -> AI -> Navigation.");
        }

        static void AddEnemySpawner()
        {
            // Create patrol paths first
            var path1 = CreatePatrolPath("PatrolPath_1", new Vector3[]
            {
                new Vector3(-5f, 0f, 10f),
                new Vector3(-5f, 0f, 25f),
                new Vector3( 5f, 0f, 25f),
                new Vector3( 5f, 0f, 10f),
            });

            // path2 is added to the scene for manual use (assign in Inspector to a second spawner or enemy)
            CreatePatrolPath("PatrolPath_2", new Vector3[]
            {
                new Vector3(-8f, 0f, 15f),
                new Vector3(-8f, 0f, 35f),
            });

            // Spawner GO
            var spawnerGO = new GameObject("EnemySpawner");
            spawnerGO.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(spawnerGO, "Add EnemySpawner");

            var spawner = spawnerGO.AddComponent<EnemySpawner>();
            spawner.maxEnemies = 3;
            spawner.respawnDelay = 30f;
            spawner.enemyHP = 100f;
            spawner.patrolPath = path1;

            // Spawn points spread around the scene
            var sp1 = CreateSpawnPoint("SP_1", new Vector3(-4f, 0f, 12f));
            var sp2 = CreateSpawnPoint("SP_2", new Vector3( 4f, 0f, 18f));
            var sp3 = CreateSpawnPoint("SP_3", new Vector3(-7f, 0f, 22f));
            sp1.transform.SetParent(spawnerGO.transform);
            sp2.transform.SetParent(spawnerGO.transform);
            sp3.transform.SetParent(spawnerGO.transform);

            var so = new SerializedObject(spawner);
            var spawnPointsProp = so.FindProperty("spawnPoints");
            if (spawnPointsProp != null)
            {
                spawnPointsProp.arraySize = 3;
                spawnPointsProp.GetArrayElementAtIndex(0).objectReferenceValue = sp1.transform;
                spawnPointsProp.GetArrayElementAtIndex(1).objectReferenceValue = sp2.transform;
                spawnPointsProp.GetArrayElementAtIndex(2).objectReferenceValue = sp3.transform;
            }
            so.ApplyModifiedProperties();

            // Assign path2 to SP_3's enemy variation -- done at runtime by spawner
            // For simplicity, all 3 use path1 (can be changed in Inspector)

            Debug.Log($"{LOG} EnemySpawner ready: 3 spawn points, PatrolPath_1 (loop), PatrolPath_2 (pingpong).");
        }

        static PatrolPath CreatePatrolPath(string pathName, Vector3[] positions)
        {
            var pathGO = new GameObject(pathName);
            Undo.RegisterCreatedObjectUndo(pathGO, $"Create {pathName}");
            var path = pathGO.AddComponent<PatrolPath>();

            var waypoints = new Transform[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var wp = new GameObject($"WP_{i}");
                wp.transform.SetParent(pathGO.transform);
                wp.transform.position = positions[i];
                waypoints[i] = wp.transform;
                Undo.RegisterCreatedObjectUndo(wp, $"Create waypoint {i}");
            }

            // Assign via SerializedObject to ensure proper undo/serialisation
            var so = new SerializedObject(path);
            var waypointsProp = so.FindProperty("waypoints");
            if (waypointsProp != null)
            {
                waypointsProp.arraySize = waypoints.Length;
                for (int i = 0; i < waypoints.Length; i++)
                    waypointsProp.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
            }
            so.ApplyModifiedProperties();

            return path;
        }

        static GameObject CreateSpawnPoint(string spName, Vector3 pos)
        {
            var go = new GameObject(spName);
            go.transform.position = pos;
            Undo.RegisterCreatedObjectUndo(go, $"Create {spName}");
            return go;
        }

        // ---- CLEAN ----

        // [MenuItem("CYBERNOMAD/Scene Setup/Clean Scene", false, 200)]
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

        /// <summary>Public wrapper for Plaga44SceneBuilder and other callers.</summary>
        public static GameObject AddPlayerControllerPublic() => AddPlayerController();

        /// <summary>Public wrapper to add splash screen from other editor tools.</summary>
        public static void AddSplashScreenPublic() => AddSplashScreen();

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
                SetBool(so, "EnableLinearMovement", true);
                SetBool(so, "EnableRotation", true);
                SetBool(so, "SnapRotation", false);        // smooth rotation, not snap
                SetFloat(so, "RotationRatchet", 0f);       // no snap angle
                SetFloat(so, "RotationAmount", 6f);         // smooth rotation speed deg/s (slow)
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

            // RuntimeGrabbable -- subclass of OVRGrabbable with haptic feedback + null-safe Awake.
            var grabbable = stone.AddComponent<RuntimeGrabbable>();
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
                SetupGrabber(leftCtrl.gameObject, 1, player); // 1 = OVRInput.Controller.LTouch
            if (rightCtrl != null)
                SetupGrabber(rightCtrl.gameObject, 2, player); // 2 = OVRInput.Controller.RTouch

            // Ignore collision between hand colliders and CharacterController
            // Without this, hand colliders inside CC volume push the player up.
            if (player.GetComponent<HandCollisionIgnore>() == null)
                player.AddComponent<HandCollisionIgnore>();

            // Runtime performance optimization -- ASW, FFR, Dynamic Resolution
            if (player.GetComponent<PerformanceConfig>() == null)
                player.AddComponent<PerformanceConfig>();

            Debug.Log($"{LOG} OVRGrabber added to controller anchors.");
        }

        static void SetupGrabber(GameObject anchorGO, int controllerValue, GameObject player)
        {
            // Rigidbody (kinematic -- pushes dynamic objects but isn't moved by them)
            var rb = anchorGO.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Hand-shaped compound collider (child objects inherit parent Rigidbody)
            // Positioned to wrap AROUND the controller grip like ghost hands in Meta sample.
            // Controller anchor = center of grip. Hand is below and slightly behind.
            //
            // Palm -- thin box wrapping the grip area
            var palm = new GameObject("PalmCollider");
            palm.transform.SetParent(anchorGO.transform, false);
            palm.transform.localPosition = new Vector3(0f, -0.02f, -0.02f);
            var palmCol = palm.AddComponent<BoxCollider>();
            palmCol.size = new Vector3(0.06f, 0.03f, 0.06f);

            // Fingers -- curling around front of grip, slightly below
            var fingers = new GameObject("FingersCollider");
            fingers.transform.SetParent(anchorGO.transform, false);
            fingers.transform.localPosition = new Vector3(0f, -0.02f, 0.04f);
            var fingersCol = fingers.AddComponent<CapsuleCollider>();
            fingersCol.direction = 2; // Z axis (forward)
            fingersCol.radius = 0.015f;
            fingersCol.height = 0.06f;

            // Thumb -- offset to side at grip height
            var thumb = new GameObject("ThumbCollider");
            thumb.transform.SetParent(anchorGO.transform, false);
            thumb.transform.localPosition = new Vector3(0.025f, -0.005f, 0f);
            var thumbCol = thumb.AddComponent<SphereCollider>();
            thumbCol.radius = 0.015f;

            // Trigger collider -- grab detection volume centered on grip
            var grabCol = anchorGO.AddComponent<SphereCollider>();
            grabCol.isTrigger = true;
            grabCol.radius = 0.08f;
            grabCol.center = new Vector3(0f, -0.01f, 0.01f);

            // GrabToggle replaces OVRGrabber -- snap-grab toggle system
            // OVRGrabber disabled: conflicts with toggle logic (hold vs press)
            anchorGO.AddComponent<GrabToggle>();
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
            // VRInputDebug removed -- no debug HUD
        }

        // ---- VR UI ----

        static void AddVRUI(GameObject player)
        {
            // Require EventSystem for UI interaction
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                Undo.RegisterCreatedObjectUndo(esGO, "Add EventSystem");
            }

            // VRMenuManager -- world-space pause menu
            var menuGO = new GameObject("VRMenuManager");
            menuGO.AddComponent<Plaga44.UI.VRMenuManager>();
            Undo.RegisterCreatedObjectUndo(menuGO, "Add VRMenuManager");

            // VRHealthDisplay -- wrist-mounted HP/stamina
            var healthGO = new GameObject("VRHealthDisplay");
            healthGO.AddComponent<Plaga44.UI.VRHealthDisplay>();
            Undo.RegisterCreatedObjectUndo(healthGO, "Add VRHealthDisplay");

            // VRScoreboard -- floating score panel
            var scoreGO = new GameObject("VRScoreboard");
            scoreGO.AddComponent<Plaga44.UI.VRScoreboard>();
            Undo.RegisterCreatedObjectUndo(scoreGO, "Add VRScoreboard");

            // VRNotification -- popup notifications
            var notifGO = new GameObject("VRNotification");
            notifGO.AddComponent<Plaga44.UI.VRNotification>();
            Undo.RegisterCreatedObjectUndo(notifGO, "Add VRNotification");

            // UIRayPointer -- laser pointer on both controller anchors
            var rigTransform = FindChild(player.transform, "OVRCameraRig");
            if (rigTransform != null)
            {
                AddRayPointer(rigTransform, "LeftControllerAnchor",
                    OVRInput.Controller.LTouch);
                AddRayPointer(rigTransform, "RightControllerAnchor",
                    OVRInput.Controller.RTouch);
            }

            Debug.Log($"{LOG} VR UI system added (menu, health, score, notifications, ray pointers).");
        }

        static void AddRayPointer(Transform rigTransform, string anchorName,
            OVRInput.Controller ctrl)
        {
            var anchor = FindChild(rigTransform, anchorName);
            if (anchor == null)
            {
                Debug.LogWarning($"{LOG} UIRayPointer: anchor '{anchorName}' not found.");
                return;
            }

            var existing = anchor.GetComponent<Plaga44.UI.UIRayPointer>();
            if (existing != null) return;  // already set up

            var pointer = anchor.gameObject.AddComponent<Plaga44.UI.UIRayPointer>();
            pointer.controller = ctrl;
        }

        // ---- HAPTICS ----

        static void AddHapticFeedback()
        {
            // HapticFeedback is a singleton MonoBehaviour required for coroutine-based
            // timed vibration pulses. One instance in scene is sufficient.
            if (Object.FindFirstObjectByType<HapticFeedback>() != null)
            {
                Debug.Log($"{LOG} HapticFeedback already in scene.");
                return;
            }
            var go = new GameObject("HapticFeedback");
            go.AddComponent<HapticFeedback>();
            Undo.RegisterCreatedObjectUndo(go, "Add HapticFeedback");
            Debug.Log($"{LOG} HapticFeedback added.");
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
