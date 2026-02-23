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
            AddSplashScreen();

            // LocomotionSetup does EVERYTHING: OVRPlayerController (camera + hands + movement + gravity) + ground + table
            // Do NOT call SetupVRSceneHands -- LocomotionSetup handles camera rig internally
            LocomotionSetup.SetupLocomotion();

            AddDebugHUD();
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
                if (n == "Directional Light" || n == "Global Volume")
                    continue;

                Debug.Log($"{LOG} Removing: {n}");
                Undo.DestroyObjectImmediate(root);
                removed++;
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{LOG} Scene cleaned. Removed {removed} objects.");
        }

        static void AddSplashScreen()
        {
            GameObject splashGO = new GameObject("SplashScreen");
            splashGO.AddComponent<SplashScreen>();
            Undo.RegisterCreatedObjectUndo(splashGO, "Add Splash Screen");
            Debug.Log($"{LOG} SplashScreen added.");
        }

        static void AddDebugHUD()
        {
            var go = new GameObject("VRInputDebug");
            go.AddComponent<VRInputDebug>();
            Undo.RegisterCreatedObjectUndo(go, "Add VRInputDebug");
            Debug.Log($"{LOG} VRInputDebug added.");
        }
    }
}
#endif
