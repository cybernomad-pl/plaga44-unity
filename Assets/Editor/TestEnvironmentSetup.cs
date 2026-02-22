#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using System.Linq;

namespace Plaga44.Editor
{
    public static class TestEnvironmentSetup
    {
        private const string LOG = "[PLAGA44]";
        private const string META_CORE_PKG = "com.meta.xr.sdk.core";

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

        [MenuItem("CYBERNOMAD/Scene Setup/Import Controller Hands Sample", false, 102)]
        public static void ImportControllerHandsSample()
        {
            var samples = Sample.FindByPackage(META_CORE_PKG, "");
            if (samples == null || !samples.Any())
            {
                // Try without version -- find any version
                var listReq = UnityEditor.PackageManager.Client.List(true);
                while (!listReq.IsCompleted) { }
                var pkg = listReq.Result?.FirstOrDefault(p => p.name == META_CORE_PKG);
                if (pkg != null)
                    samples = Sample.FindByPackage(META_CORE_PKG, pkg.version);
            }

            if (samples == null || !samples.Any())
            {
                Debug.LogError($"{LOG} No samples found for {META_CORE_PKG}. Is Meta XR SDK installed?");
                return;
            }

            foreach (var sample in samples)
            {
                Debug.Log($"{LOG} Available sample: '{sample.displayName}'");
            }

            if (!samples.Any(s => s.displayName.Contains("Controller") && s.displayName.Contains("Hand")))
            {
                Debug.LogError($"{LOG} 'Controller Driven Hand Poses' sample not found. Available samples logged above.");
                return;
            }

            var handsSample = samples.First(s =>
                s.displayName.Contains("Controller") && s.displayName.Contains("Hand"));

            if (handsSample.isImported)
            {
                Debug.Log($"{LOG} Sample '{handsSample.displayName}' already imported at: {handsSample.importPath}");
                return;
            }

            bool ok = handsSample.Import(Sample.ImportOptions.OverridePreviousImports);
            if (ok)
                Debug.Log($"{LOG} Imported '{handsSample.displayName}' to: {handsSample.importPath}");
            else
                Debug.LogError($"{LOG} Failed to import sample '{handsSample.displayName}'.");
        }

        [MenuItem("CYBERNOMAD/Scene Setup/Setup TESTBED", false, 150)]
        public static void SetupTestbed()
        {
            Debug.Log($"{LOG} === Setup TESTBED ===");
            CleanScene();
            CreateInfiniteFloor();
            MetaQuestSetup.SetupVRScene();
            AddSplashScreen();
            Debug.Log($"{LOG} === TESTBED READY ===");
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
