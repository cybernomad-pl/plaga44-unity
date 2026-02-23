#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Plaga44.Editor
{
    /// <summary>
    /// TESTBED setup: copies Meta SDK's LocomotionExamples scene (which has
    /// OVRCameraRig + OVRInteractionComprehensive correctly wired) and opens it.
    /// That scene has working: camera, hands, controllers, locomotion (slide+teleport+turn),
    /// grab interactors, ray, poke -- all pre-configured by Meta.
    /// </summary>
    public static class TestEnvironmentSetup
    {
        private const string LOG = "[PLAGA44]";

        [MenuItem("CYBERNOMAD/Scene Setup/Setup TESTBED", false, 50)]
        public static void SetupTestbed()
        {
            Debug.Log($"{LOG} === Setup TESTBED ===");

            // Copy LocomotionExamples from SDK -- it has everything wired correctly
            string sourceScene = FindSDKScene("LocomotionExamples");
            if (sourceScene == null)
            {
                Debug.LogError($"{LOG} LocomotionExamples.unity not found in SDK Samples~!");
                Debug.LogError($"{LOG} Is com.meta.xr.sdk.interaction.ovr installed?");
                return;
            }

            // Copy to Assets/Scenes/testbed.unity
            string destDir = System.IO.Path.Combine(Application.dataPath, "Scenes");
            if (!System.IO.Directory.Exists(destDir))
                System.IO.Directory.CreateDirectory(destDir);

            string destPath = System.IO.Path.Combine(destDir, "testbed.unity");
            System.IO.File.Copy(sourceScene, destPath, true);
            AssetDatabase.Refresh();

            // Open the scene
            EditorSceneManager.OpenScene("Assets/Scenes/testbed.unity");
            Debug.Log($"{LOG} Opened testbed (based on LocomotionExamples).");

            // Add our extras to the running scene
            AddSplashScreen();
            AddDebugHUD();
            AddTestTable();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} === TESTBED READY ===");
            Debug.Log($"{LOG} Camera: OVRCameraRig (from SDK sample)");
            Debug.Log($"{LOG} Locomotion: L-stick = move, R-stick = snap turn, A = teleport");
            Debug.Log($"{LOG} Grab: grip button grabs objects");
        }

        [MenuItem("CYBERNOMAD/Scene Setup/Clean Scene", false, 200)]
        public static void CleanScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
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

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{LOG} Scene cleaned. Removed {removed} objects.");
        }

        static string FindSDKScene(string sceneName)
        {
            string packageCache = System.IO.Path.Combine(Application.dataPath, "..", "Library", "PackageCache");
            if (!System.IO.Directory.Exists(packageCache)) return null;

            var files = System.IO.Directory.GetFiles(packageCache, sceneName + ".unity", System.IO.SearchOption.AllDirectories);
            foreach (var f in files)
            {
                if (f.Contains("interaction") && f.Contains("Samples~"))
                    return f;
            }
            return null;
        }

        static void AddSplashScreen()
        {
            if (GameObject.Find("SplashScreen") != null) return;
            var go = new GameObject("SplashScreen");
            go.AddComponent<SplashScreen>();
            Undo.RegisterCreatedObjectUndo(go, "Add SplashScreen");
            Debug.Log($"{LOG} SplashScreen added.");
        }

        static void AddDebugHUD()
        {
            if (GameObject.FindFirstObjectByType<VRInputDebug>() != null) return;
            var go = new GameObject("VRInputDebug");
            go.AddComponent<VRInputDebug>();
            Undo.RegisterCreatedObjectUndo(go, "Add VRInputDebug");
            Debug.Log($"{LOG} VRInputDebug HUD added.");
        }

        static void AddTestTable()
        {
            if (GameObject.Find("TestTable") != null) return;

            var table = new GameObject("TestTable");
            table.transform.position = new Vector3(0f, 0f, 1.5f);
            Undo.RegisterCreatedObjectUndo(table, "Add TestTable");

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

            // Physics objects on the table
            float y = 0.85f;
            AddObj(table.transform, "RedCube", PrimitiveType.Cube,
                new Vector3(-0.3f, y, 0f), Vector3.one * 0.12f, new Color(0.8f, 0.2f, 0.2f), 0.3f);
            AddObj(table.transform, "GreenSphere", PrimitiveType.Sphere,
                new Vector3(0f, y, 0f), Vector3.one * 0.1f, new Color(0.2f, 0.7f, 0.2f), 0.15f);
            AddObj(table.transform, "Stone", PrimitiveType.Sphere,
                new Vector3(0.3f, y, 0f), new Vector3(0.08f, 0.06f, 0.08f), new Color(0.5f, 0.5f, 0.5f), 0.4f);

            Debug.Log($"{LOG} TestTable + 3 physics objects at hand height.");
        }

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

        static void SetMat(GameObject obj, Color color)
        {
            var r = obj.GetComponent<Renderer>();
            if (r == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return;
            var mat = new Material(shader);
            mat.color = color;
            r.sharedMaterial = mat;
        }
    }
}
#endif
