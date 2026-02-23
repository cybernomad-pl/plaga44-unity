#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    /// <summary>
    /// One-click TESTBED setup. Uses OVRInteractionComprehensive prefab from Meta SDK
    /// which is the same rig used in ALL Meta example scenes. It includes:
    /// - OVRCameraRig (camera, tracking)
    /// - Hand + controller tracking (simultaneous)
    /// - HandGrabInteractor on both hands (grab objects)
    /// - Locomotion: left thumbstick = slide, right thumbstick = snap turn
    /// - Teleport, ray, poke interactors
    /// Everything pre-wired, zero custom code.
    /// </summary>
    public static class TestEnvironmentSetup
    {
        private const string LOG = "[PLAGA44]";

        [MenuItem("CYBERNOMAD/Scene Setup/Setup TESTBED", false, 50)]
        public static void SetupTestbed()
        {
            Debug.Log($"{LOG} === Setup TESTBED ===");

            // 1. Clean scene (keep lights)
            CleanScene();

            // 2. Add comprehensive interaction rig (camera + hands + locomotion + grab)
            AddComprehensiveRig();

            // 3. Floor (Unlit, no VR flicker)
            AddFloor();

            // 4. Table with test objects
            AddTestTable();

            // 5. Splash screen
            AddSplashScreen();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} === TESTBED READY ===");
            Debug.Log($"{LOG} Controls: L-stick = move, R-stick = snap turn, grip = grab objects");
        }

        [MenuItem("CYBERNOMAD/Scene Setup/Clean Scene", false, 200)]
        public static void CleanScene()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            int removed = 0;

            foreach (var root in roots)
            {
                string n = root.name;
                if (n == "Directional Light" || n == "Global Volume")
                    continue;

                Debug.Log($"{LOG} Removing: {n}");
                Undo.DestroyObjectImmediate(root);
                removed++;
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{LOG} Scene cleaned. Removed {removed} objects.");
        }

        static void AddComprehensiveRig()
        {
            // Step 1: OVRCameraRig (the CAMERA + tracking origin)
            // This is required -- OVRInteractionComprehensive references it, doesn't contain it
            MetaQuestSetup.SetupVRSceneHands();

            // Step 2: OVRInteractionComprehensive (ISDK overlay: grab, locomotion, ray, poke)
            // It hooks into the OVRCameraRig that's already in scene
            var interactionPrefab = FindPrefab("OVRInteractionComprehensive");
            if (interactionPrefab != null)
            {
                var interaction = (GameObject)PrefabUtility.InstantiatePrefab(interactionPrefab);
                interaction.transform.position = Vector3.zero;
                interaction.transform.rotation = Quaternion.identity;
                Undo.RegisterCreatedObjectUndo(interaction, "Add ISDK Interactions");
                Debug.Log($"{LOG} OVRInteractionComprehensive added (grab + locomotion + ray + poke).");
            }
            else
            {
                Debug.LogWarning($"{LOG} OVRInteractionComprehensive not found -- no grab/locomotion.");
                Debug.LogWarning($"{LOG} Is com.meta.xr.sdk.interaction.ovr installed?");
            }

            // Step 3: VRInputDebug HUD
            AddDebugHUD();
        }

        static void AddDebugHUD()
        {
            // Find existing VRInputDebug or add new one
            var existing = GameObject.FindFirstObjectByType<VRInputDebug>();
            if (existing != null)
            {
                Debug.Log($"{LOG} VRInputDebug already in scene.");
                return;
            }

            var debugGO = new GameObject("VRInputDebug");
            debugGO.AddComponent<VRInputDebug>();
            Undo.RegisterCreatedObjectUndo(debugGO, "Add VRInputDebug");
            Debug.Log($"{LOG} VRInputDebug HUD added.");
        }

        static GameObject FindPrefab(string name)
        {
            string[] guids = AssetDatabase.FindAssets(name + " t:prefab");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains(name + ".prefab"))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        Debug.Log($"{LOG} Found {name}: {path}");
                        return prefab;
                    }
                }
            }
            return null;
        }

        static void AddFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(100f, 1f, 100f);

            // Unlit material -- no lighting artifacts in VR
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = new Color(0.22f, 0.22f, 0.25f);
                floor.GetComponent<Renderer>().sharedMaterial = mat;
            }

            Undo.RegisterCreatedObjectUndo(floor, "Add Floor");
            Debug.Log($"{LOG} Floor (Unlit).");
        }

        static void AddTestTable()
        {
            var table = new GameObject("TestTable");
            table.transform.position = new Vector3(0f, 0f, 1.2f);
            Undo.RegisterCreatedObjectUndo(table, "Add Test Table");

            // Table top
            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "TableTop";
            top.transform.SetParent(table.transform);
            top.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            top.transform.localScale = new Vector3(1.2f, 0.05f, 0.6f);
            top.isStatic = true;
            SetMat(top, new Color(0.45f, 0.3f, 0.15f));

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
                SetMat(leg, new Color(0.35f, 0.22f, 0.1f));
            }

            // Test objects on table -- these have Rigidbody + Collider (from primitives).
            // HandGrabInteractable must be added at runtime or via Building Blocks
            // because it requires ISDK assembly references.
            // For now: physics objects that the comprehensive rig's grab can interact with.
            float y = 0.85f;

            AddPhysicsObject(table.transform, "RedCube", PrimitiveType.Cube,
                new Vector3(-0.3f, y, 0f), Vector3.one * 0.12f,
                new Color(0.8f, 0.2f, 0.2f), 0.3f);

            AddPhysicsObject(table.transform, "GreenSphere", PrimitiveType.Sphere,
                new Vector3(0f, y, 0f), Vector3.one * 0.1f,
                new Color(0.2f, 0.7f, 0.2f), 0.15f);

            AddPhysicsObject(table.transform, "Stone", PrimitiveType.Sphere,
                new Vector3(0.3f, y, 0f), new Vector3(0.08f, 0.06f, 0.08f),
                new Color(0.5f, 0.5f, 0.5f), 0.4f);

            Debug.Log($"{LOG} TestTable with 3 physics objects at hand height.");
            Debug.Log($"{LOG} NOTE: To make objects grabbable, add HandGrabInteractable via");
            Debug.Log($"{LOG}   Meta > Quick Actions > Grab, or use Building Blocks.");
        }

        static void AddPhysicsObject(Transform parent, string name, PrimitiveType type,
            Vector3 localPos, Vector3 localScale, Color color, float mass)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.localPosition = localPos;
            obj.transform.localScale = localScale;
            SetMat(obj, color);

            var rb = obj.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        static void AddSplashScreen()
        {
            var splashGO = new GameObject("SplashScreen");
            splashGO.AddComponent<SplashScreen>();
            Undo.RegisterCreatedObjectUndo(splashGO, "Add Splash Screen");
            Debug.Log($"{LOG} SplashScreen.");
        }

        static void SetMat(GameObject obj, Color color)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return;
            var mat = new Material(shader);
            mat.color = color;
            renderer.sharedMaterial = mat;
        }
    }
}
#endif
