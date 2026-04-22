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
// ZERO FALLBACKS: brak ktorejkolwiek zaleznosci -> LogError + abort.
// =============================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using Meta.XR.Movement.Retargeting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plaga44.Editor.Setup
{
    public static class ReplaceAvatarWithLocomotion
    {
        private const string LOG = "[PLAGA44][ReplaceAvatar]";

        private const string OLD_PLAGA44_GUID = "96cedd71aec24069a311cae72857c9bc";
        private const string LOCOMOTION_PREFAB_GUID = "286d7e2005861d341a0a94d7f615675a";

        [MenuItem("PLAGA44/Setup/Replace PLAGA44 Avatar with Locomotion Sample")]
        public static void RunMenu() => Run();

        /// <summary>
        /// Idempotent pipeline:
        /// 1) Locomotion juz w scenie -> sprawdz + uzupelnij wire.
        /// 2) Stary PLAGA44 -> waliduj zaleznosci, zamien, wire.
        /// 3) Brak obu -> LogError, return false.
        /// </summary>
        public static bool Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
                return Error("No active scene");

            // Walidacja zaleznosci PRZED jakakolwiek zmiana w scenie
            if (!TryResolveDeps(scene, out var leftHand, out var rightHand, out var camRig))
                return false;

            if (!TryLoadPrefab(LOCOMOTION_PREFAB_GUID, out var prefab))
                return false;

            // Locomotion juz jest -> tylko rewire
            var existingLoco = FindPrefabInstance(scene, LOCOMOTION_PREFAB_GUID);
            if (existingLoco != null)
                return WireAndReport(existingLoco, leftHand, rightHand, camRig, scene, "REWIRED istniejacy Locomotion");

            // Stary PLAGA44 -> zamiana
            var oldAvatar = FindPrefabInstance(scene, OLD_PLAGA44_GUID);
            if (oldAvatar == null)
                return Error("Ani Locomotion, ani stary PLAGA44 w scenie -- recznie wstaw Locomotion prefab");

            var (savedPos, savedRot, savedParent) = (oldAvatar.transform.position, oldAvatar.transform.rotation, oldAvatar.transform.parent);
            Debug.Log($"{LOG} Znaleziono stary '{oldAvatar.name}' at {savedPos} parent={savedParent?.name ?? "(root)"}");

            // Instantuj PRZED destroy -- rollback jesli walidacja failuje
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (instance == null)
                return Error("InstantiatePrefab returned null");
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Locomotion");

            if (instance.GetComponentInChildren<CharacterRetargeter>(true) == null)
            {
                Undo.DestroyObjectImmediate(instance);
                return Error("CharacterRetargeter nie znaleziony w Locomotion -- rollback");
            }

            // Wszystko OK -- zamien
            Undo.DestroyObjectImmediate(oldAvatar);
            if (savedParent != null) instance.transform.SetParent(savedParent, worldPositionStays: false);
            instance.transform.SetPositionAndRotation(savedPos, savedRot);
            Debug.Log($"{LOG} Instantiated '{instance.name}' at {savedPos}");

            return WireAndReport(instance, leftHand, rightHand, camRig, scene, "DONE -- avatar zamieniony");
        }

        // =====================================================================
        // Walidacja zaleznosci
        // =====================================================================

        private static bool TryResolveDeps(Scene scene, out GameObject leftHand, out GameObject rightHand, out OVRCameraRig camRig)
        {
            leftHand = FindByName(scene, "LeftHand");
            rightHand = FindByName(scene, "RightHand");
            camRig = Object.FindFirstObjectByType<OVRCameraRig>();

            if (leftHand == null) return Error("LeftHand GO not found");
            if (rightHand == null) return Error("RightHand GO not found");
            if (camRig == null) return Error("OVRCameraRig not found");
            return true;
        }

        private static bool TryLoadPrefab(string guid, out GameObject prefab)
        {
            prefab = null;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return Error($"Prefab not found by guid {guid}");
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab != null || Error($"Failed to load prefab at {path}");
        }

        // =====================================================================
        // Wire ISDKSkeletalProcessor source[0]
        // =====================================================================

        private static bool WireAndReport(GameObject locoInstance, GameObject leftHand, GameObject rightHand, OVRCameraRig camRig, Scene scene, string reportMsg)
        {
            var retargeter = locoInstance.GetComponentInChildren<CharacterRetargeter>(true);
            if (retargeter == null)
                return Error($"CharacterRetargeter nie znaleziony w '{locoInstance.name}'");

            if (!TryWireRetargeter(retargeter, leftHand, rightHand, camRig, out bool changed))
                return false;

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"{LOG} [{reportMsg}]");
                return true;
            }
            Debug.Log($"{LOG} [OK] wire retargetera juz poprawne -- skip");
            return false;
        }

        private static bool TryWireRetargeter(CharacterRetargeter retargeter, GameObject leftHand, GameObject rightHand, OVRCameraRig camRig, out bool changed)
        {
            changed = false;
            var so = new SerializedObject(retargeter);
            var src = so.FindProperty("_sourceProcessorContainers");
            if (src == null || !src.isArray || src.arraySize == 0)
                return Error($"_sourceProcessorContainers empty on '{retargeter.gameObject.name}'");

            var isdk = src.GetArrayElementAtIndex(0).FindPropertyRelative("_isdkProcessor");
            if (isdk == null) return Error("_isdkProcessor property missing on source[0]");

            if (!TryFindProp(isdk, "_leftHand", out var leftProp)) return false;
            if (!TryFindProp(isdk, "_rightHand", out var rightProp)) return false;
            if (!TryFindProp(isdk, "_cameraRig", out var camProp)) return false;

            changed |= AssignIfDifferent(leftProp, leftHand);
            changed |= AssignIfDifferent(rightProp, rightHand);
            changed |= AssignIfDifferent(camProp, camRig);

            if (changed)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(retargeter);
                Debug.Log($"{LOG} [WIRED] '{retargeter.gameObject.name}': LH={leftHand.name} RH={rightHand.name} Cam={camRig.name}");
            }
            return true;
        }

        private static bool TryFindProp(SerializedProperty parent, string relativePath, out SerializedProperty prop)
        {
            prop = parent.FindPropertyRelative(relativePath);
            return prop != null || Error($"Property '{relativePath}' not found on {parent.propertyPath}");
        }

        private static bool AssignIfDifferent(SerializedProperty prop, Object newValue)
        {
            if (prop.objectReferenceValue == newValue) return false;
            prop.objectReferenceValue = newValue;
            return true;
        }

        // =====================================================================
        // Scene traversal helpers
        // =====================================================================

        private static IEnumerable<Transform> AllTransforms(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    yield return t;
        }

        private static GameObject FindByName(Scene scene, string name)
        {
            foreach (var t in AllTransforms(scene))
                if (t.name == name) return t.gameObject;
            return null;
        }

        private static GameObject FindPrefabInstance(Scene scene, string targetGuid)
        {
            foreach (var t in AllTransforms(scene))
            {
                var go = t.gameObject;
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
                var asset = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (asset == null) continue;
                if (AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)) == targetGuid)
                    return go;
            }
            return null;
        }

        // =====================================================================
        // Logging
        // =====================================================================

        private static bool Error(string msg)
        {
            Debug.LogError($"{LOG} {msg}");
            return false;
        }
    }
}
#endif
