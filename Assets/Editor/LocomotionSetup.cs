#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    /// <summary>
    /// MVP scene setup using Meta's OVRInteractionComprehensive prefab.
    /// This is the SAME prefab used in ALL Meta SDK example scenes (HandGrabExamples,
    /// LocomotionExamples, etc). It includes: OVRCameraRig + hand/controller tracking +
    /// HandGrabInteractor + locomotion (slide + snap turn + teleport) + ray + poke.
    /// Everything wired and ready to go.
    /// </summary>
    public static class LocomotionSetup
    {
        private const string LOG = "[PLAGA44]";

        [MenuItem("CYBERNOMAD/Scene Setup/Setup TESTBED (Comprehensive Rig)", false, 100)]
        public static void SetupTestbed()
        {
            Debug.Log($"{LOG} === TESTBED Setup (OVRInteractionComprehensive) ===");

            // Find OVRInteractionComprehensive prefab -- the full ISDK rig
            var rigPrefab = FindPrefab("OVRInteractionComprehensive");
            if (rigPrefab == null)
            {
                Debug.LogError($"{LOG} OVRInteractionComprehensive.prefab not found! Is com.meta.xr.sdk.interaction.ovr installed?");
                return;
            }

            // Clean scene
            RemoveExisting("OVRInteractionComprehensive");
            RemoveExisting("OVRPlayerController");
            RemoveExisting("OVRCameraRig");
            RemoveExisting("Main Camera");

            // Instantiate comprehensive rig
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rig.transform.position = Vector3.zero;
            rig.transform.rotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(rig, "Add OVRInteractionComprehensive");
            Debug.Log($"{LOG} OVRInteractionComprehensive instantiated.");

            // Add ground
            AddGround();

            Selection.activeGameObject = rig;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} === TESTBED DONE ===");
            Debug.Log($"{LOG} Rig includes: locomotion (slide+teleport+snap turn), hand grab, ray, poke.");
            Debug.Log($"{LOG} Left thumbstick = move, Right thumbstick = turn.");
        }

        [MenuItem("CYBERNOMAD/Scene Setup/Copy SDK Example Scene/Locomotion Examples", false, 110)]
        public static void CopyLocomotionExample()
        {
            CopyExampleScene("LocomotionExamples");
        }

        [MenuItem("CYBERNOMAD/Scene Setup/Copy SDK Example Scene/Hand Grab Examples", false, 111)]
        public static void CopyHandGrabExample()
        {
            CopyExampleScene("HandGrabExamples");
        }

        [MenuItem("CYBERNOMAD/Scene Setup/Copy SDK Example Scene/Touch Grab Examples", false, 112)]
        public static void CopyTouchGrabExample()
        {
            CopyExampleScene("TouchGrabExamples");
        }

        [MenuItem("CYBERNOMAD/Scene Setup/Copy SDK Example Scene/Distance Grab Examples", false, 113)]
        public static void CopyDistanceGrabExample()
        {
            CopyExampleScene("DistanceGrabExamples");
        }

        private static void CopyExampleScene(string sceneName)
        {
            // Find scene in PackageCache Samples~
            string[] guids = AssetDatabase.FindAssets(sceneName + " t:scene");
            string sourcePath = null;

            // Samples~ is hidden from AssetDatabase, search manually
            string[] searchPaths = new[]
            {
                System.IO.Path.Combine(Application.dataPath, "..", "Library", "PackageCache"),
            };

            foreach (var searchRoot in searchPaths)
            {
                if (!System.IO.Directory.Exists(searchRoot)) continue;
                var files = System.IO.Directory.GetFiles(searchRoot, sceneName + ".unity", System.IO.SearchOption.AllDirectories);
                foreach (var f in files)
                {
                    if (f.Contains("Samples~") && f.Contains("interaction"))
                    {
                        sourcePath = f;
                        break;
                    }
                }
                if (sourcePath != null) break;
            }

            if (sourcePath == null)
            {
                Debug.LogError($"{LOG} {sceneName}.unity not found in SDK Samples~!");
                return;
            }

            string destDir = System.IO.Path.Combine(Application.dataPath, "Scenes");
            if (!System.IO.Directory.Exists(destDir))
                System.IO.Directory.CreateDirectory(destDir);

            string destPath = System.IO.Path.Combine(destDir, sceneName + ".unity");
            System.IO.File.Copy(sourcePath, destPath, true);
            AssetDatabase.Refresh();

            Debug.Log($"{LOG} Copied {sceneName}.unity to Assets/Scenes/");
            Debug.Log($"{LOG} Open it from Project window: Assets/Scenes/{sceneName}");

            // Open the scene
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/" + sceneName + ".unity");
            if (sceneAsset != null)
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity");
                Debug.Log($"{LOG} Scene opened: {sceneName}");
            }
        }

        private static void AddGround()
        {
            var ground = GameObject.Find("Ground");
            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = new Vector3(10f, 1f, 10f);
                Undo.RegisterCreatedObjectUndo(ground, "Add Ground");
            }
            // Always set Unlit material
            SetUnlitMaterial(ground, new Color(0.25f, 0.25f, 0.25f));
            Debug.Log($"{LOG} Ground (Unlit).");
        }

        private static GameObject FindPrefab(string name)
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

        private static void SetUnlitMaterial(GameObject obj, Color color)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                var mat = new Material(shader);
                mat.color = color;
                renderer.sharedMaterial = mat;
            }
        }

        private static void RemoveExisting(string name)
        {
            var objects = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in objects)
            {
                if (t != null && t.name == name && t.parent == null)
                {
                    Debug.Log($"{LOG} Removing existing {name}.");
                    Undo.DestroyObjectImmediate(t.gameObject);
                }
            }
        }
    }
}
#endif
