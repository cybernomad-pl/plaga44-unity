using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plaga44.Editor
{
    [InitializeOnLoad]
    public static class Bootstrap
    {
        private const string ScenePath = "Assets/PLAGA44/TESTBED_V6.unity";
        private const string BootstrapKey = "Plaga44.Bootstrap.Done";
        private const string LOG = "[Plaga44.Bootstrap]";

        static Bootstrap()
        {
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (SessionState.GetBool(BootstrapKey, false)) return;
            SessionState.SetBool(BootstrapKey, true);

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Run;
                return;
            }

            // Otworz scene
            var active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.path.Contains("TESTBED_V6"))
            {
                if (System.IO.File.Exists(ScenePath))
                {
                    Debug.Log($"{LOG} Opening TESTBED_V6");
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
            }

            EditorApplication.delayCall += ConfigureScene;
        }

        private static void ConfigureScene()
        {
            var rig = GameObject.Find("OVRCameraRig");
            if (rig == null)
            {
                Debug.LogWarning($"{LOG} OVRCameraRig not found");
                return;
            }

            bool changed = false;

            // CharacterController
            var cc = rig.GetComponent<CharacterController>();
            if (cc == null)
            {
                cc = rig.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.radius = 0.3f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.skinWidth = 0.08f;
                cc.stepOffset = 0.5f;
                changed = true;
                Debug.Log($"{LOG} Added CharacterController");
            }

            // LocomotionController
            var loco = rig.GetComponent<Plaga44.Locomotion.LocomotionController>();
            if (loco == null)
            {
                loco = rig.AddComponent<Plaga44.Locomotion.LocomotionController>();
                loco.moveSpeed = 2.5f;
                loco.strafeFactor = 0.8f;
                changed = true;
                Debug.Log($"{LOG} Added LocomotionController");
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                Debug.Log($"{LOG} Scene configured and saved");
            }
        }
    }
}
