// =============================================================================
// ReplaceAvatarWithLocomotion.cs
// CYBERNOMAD / PLAGA44 -- zamienia StylizedCharacterPLAGA44 (zepsuty wariant
// ISDK z modyfikacjami Locomotion) na StylizedCharacterLocomotion prefab
// z Meta Sample "Advanced Samples / ISDKLocomotion". Unity sam wygeneruje
// prawidlowe target fileID dla [SerializeReference] processorow.
//
// Po zamianie wire'uje ISDKSkeletalProcessor (source[0]):
//   _leftHand  -> GO 'LeftHand'  (ma Hand.cs = IHand implementer)
//   _rightHand -> GO 'RightHand' (ma Hand.cs = IHand implementer)
//   _cameraRig -> OVRCameraRig component w scenie
//
// To rozwiazuje BLOCKER: Assert.IsNotNull(handObject) w ISDKSkeletalProcessor.
// SetupHand -- retargeter przestanie crashowac w Start().
// =============================================================================
#if UNITY_EDITOR
using System.Linq;
using Meta.XR.Movement.Retargeting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class ReplaceAvatarWithLocomotion
    {
        private const string LOG = "[PLAGA44][ReplaceAvatar]";

        // guid'y z AssetDatabase
        private const string OLD_PLAGA44_GUID = "96cedd71aec24069a311cae72857c9bc";
        private const string LOCOMOTION_PREFAB_GUID = "286d7e2005861d341a0a94d7f615675a";

        [MenuItem("PLAGA44/Setup/Replace PLAGA44 Avatar with Locomotion Sample")]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError($"{LOG} No active scene");
                return;
            }

            // 1. Znajdz stary PLAGA44 PrefabInstance root w scenie (jesli istnieje)
            GameObject oldAvatar = FindOldAvatar(scene);
            Vector3 savedPos = Vector3.zero;
            Quaternion savedRot = Quaternion.identity;
            Transform savedParent = null;

            if (oldAvatar != null)
            {
                savedPos = oldAvatar.transform.position;
                savedRot = oldAvatar.transform.rotation;
                savedParent = oldAvatar.transform.parent;
                Debug.Log($"{LOG} Found old avatar '{oldAvatar.name}' at {savedPos} parent={savedParent?.name ?? "(scene root)"}");
                Undo.DestroyObjectImmediate(oldAvatar);
            }
            else
            {
                Debug.LogWarning($"{LOG} Old PLAGA44 avatar NOT found in scene -- spawning Locomotion at origin");
            }

            // 2. Zaladuj Locomotion prefab z AssetDatabase
            string locoPath = AssetDatabase.GUIDToAssetPath(LOCOMOTION_PREFAB_GUID);
            if (string.IsNullOrEmpty(locoPath))
            {
                Debug.LogError($"{LOG} Locomotion prefab not found by guid {LOCOMOTION_PREFAB_GUID}");
                return;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(locoPath);
            if (prefab == null)
            {
                Debug.LogError($"{LOG} Failed to load prefab at {locoPath}");
                return;
            }

            // 3. Instantuj Locomotion jako PrefabInstance w scenie
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Locomotion");
            if (savedParent != null)
                instance.transform.SetParent(savedParent, worldPositionStays: false);
            instance.transform.SetPositionAndRotation(savedPos, savedRot);
            Debug.Log($"{LOG} Instantiated '{instance.name}' at {savedPos}");

            // 4. Znajdz LeftHand, RightHand, OVRCameraRig w scenie
            GameObject leftHand = null, rightHand = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (leftHand == null && t.name == "LeftHand") leftHand = t.gameObject;
                    if (rightHand == null && t.name == "RightHand") rightHand = t.gameObject;
                    if (leftHand != null && rightHand != null) break;
                }
            }
            var camRig = Object.FindFirstObjectByType<OVRCameraRig>();

            if (leftHand == null) Debug.LogWarning($"{LOG} LeftHand GO not found in scene");
            if (rightHand == null) Debug.LogWarning($"{LOG} RightHand GO not found in scene");
            if (camRig == null) Debug.LogWarning($"{LOG} OVRCameraRig not found in scene");
            Debug.Log($"{LOG} Found: LeftHand={leftHand?.name ?? "NULL"}, RightHand={rightHand?.name ?? "NULL"}, OVRCameraRig={camRig?.name ?? "NULL"}");

            // 5. Znajdz CharacterRetargeter na nowym avatarze
            var retargeter = instance.GetComponentInChildren<CharacterRetargeter>(true);
            if (retargeter == null)
            {
                Debug.LogError($"{LOG} CharacterRetargeter not found in Locomotion instance");
                return;
            }

            // 6. Wire _leftHand / _rightHand / _cameraRig na ISDKSkeletalProcessor source[0]
            var so = new SerializedObject(retargeter);
            var srcContainers = so.FindProperty("_sourceProcessorContainers");
            if (srcContainers == null || !srcContainers.isArray || srcContainers.arraySize == 0)
            {
                Debug.LogError($"{LOG} _sourceProcessorContainers empty or missing");
                return;
            }

            var data0 = srcContainers.GetArrayElementAtIndex(0);
            var isdkProc = data0.FindPropertyRelative("_isdkProcessor");
            if (isdkProc == null)
            {
                Debug.LogError($"{LOG} _isdkProcessor property not found on source[0]");
                return;
            }

            int wired = 0;
            if (leftHand != null)
            {
                isdkProc.FindPropertyRelative("_leftHand").objectReferenceValue = leftHand;
                wired++;
            }
            if (rightHand != null)
            {
                isdkProc.FindPropertyRelative("_rightHand").objectReferenceValue = rightHand;
                wired++;
            }
            if (camRig != null)
            {
                isdkProc.FindPropertyRelative("_cameraRig").objectReferenceValue = camRig;
                wired++;
            }
            so.ApplyModifiedProperties();

            Debug.Log($"{LOG} Wired {wired}/3 ISDK source refs on {retargeter.gameObject.name}");

            // 7. Save scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"{LOG} DONE -- scene saved. Enter Play Mode to test.");
        }

        private static GameObject FindOldAvatar(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                // Sprawdz root + wszystkie children czy sa PrefabInstance root z nasza guid
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var go = t.gameObject;
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
                    var asset = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    if (asset == null) continue;
                    string path = AssetDatabase.GetAssetPath(asset);
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    if (guid == OLD_PLAGA44_GUID) return go;
                }
            }
            return null;
        }
    }
}
#endif
