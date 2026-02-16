#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class TestEnvironmentSetup
    {
        private const string LOG = "[PLAGA44]";

        [MenuItem("CYBERNOMAD/Scene Setup/Add Splash Screen", false, 100)]
        public static void AddSplashScreen()
        {
            var existing = Object.FindObjectsByType<SplashScreen>(FindObjectsSortMode.None);
            if (existing.Length > 0)
            {
                Debug.Log($"{LOG} SplashScreen already in scene.");
                return;
            }

            GameObject splashGO = new GameObject("SplashScreen");
            splashGO.AddComponent<SplashScreen>();
            Undo.RegisterCreatedObjectUndo(splashGO, "Add Splash Screen");

            Debug.Log($"{LOG} SplashScreen added. Both triggers to dismiss.");
        }

        [MenuItem("CYBERNOMAD/Scene Setup/Create Infinite Floor", false, 101)]
        public static void CreateInfiniteFloor()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name == "InfiniteFloor")
                {
                    Debug.Log($"{LOG} InfiniteFloor already in scene.");
                    return;
                }
            }

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
            Debug.Log($"{LOG} InfiniteFloor created (1000x1000m plane).");
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

                // Keep lighting and volume -- remove everything else
                if (n == "Directional Light" || n == "Global Volume")
                    continue;

                Debug.Log($"{LOG} Removing: {n}");
                Undo.DestroyObjectImmediate(root);
                removed++;
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{LOG} Scene cleaned. Removed {removed} objects. Undo available.");
        }
    }
}
#endif
