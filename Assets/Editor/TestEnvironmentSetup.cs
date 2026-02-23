#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class TestEnvironmentSetup
    {
        private const string LOG = "[PLAGA44]";

        [MenuItem("CYBERNOMAD/Scene Setup/Setup TESTBED", false, 50)]
        public static void SetupTestbed()
        {
            Debug.Log($"{LOG} === Setup TESTBED ===");
            CleanScene();
            CreateInfiniteFloor();
            AddSplashScreen();
            Debug.Log($"{LOG} === TESTBED READY -- now pick a VR Rig from menu ===");
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

        static void CreateInfiniteFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "InfiniteFloor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(100f, 1f, 100f);

            Renderer renderer = floor.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.25f, 0.25f, 0.28f);
                renderer.sharedMaterial = mat;
            }

            Undo.RegisterCreatedObjectUndo(floor, "Create Infinite Floor");
            Debug.Log($"{LOG} InfiniteFloor created.");
        }

        static void AddSplashScreen()
        {
            GameObject splashGO = new GameObject("SplashScreen");
            splashGO.AddComponent<SplashScreen>();
            Undo.RegisterCreatedObjectUndo(splashGO, "Add Splash Screen");
            Debug.Log($"{LOG} SplashScreen added.");
        }
    }
}
#endif
