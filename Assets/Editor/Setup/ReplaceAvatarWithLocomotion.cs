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
//
// ZERO FALLBACKS: jesli LeftHand/RightHand/OVRCameraRig nie znaleziono
// w scenie -> LogError + return false. NIE spawnuje avatara bez wire.
// =============================================================================
#if UNITY_EDITOR
using Meta.XR.Movement.Retargeting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class ReplaceAvatarWithLocomotion
    {
        private const string LOG = "[PLAGA44][ReplaceAvatar]";

        private const string OLD_PLAGA44_GUID = "96cedd71aec24069a311cae72857c9bc";
        private const string LOCOMOTION_PREFAB_GUID = "286d7e2005861d341a0a94d7f615675a";

        [MenuItem("PLAGA44/Setup/Replace PLAGA44 Avatar with Locomotion Sample")]
        public static void RunMenu()
        {
            Run();
        }

        /// <summary>
        /// Idempotent pipeline:
        /// 1) Jesli Locomotion juz w scenie -> SPRAWDZ wire (może brakować); uzupelnij i return.
        /// 2) Jesli stary PLAGA44 w scenie -> waliduj zaleznosci, zamien, wire, return.
        /// 3) Brak obu -> LogError, return false (nie spawnujemy znikad).
        /// </summary>
        /// <returns>true jesli scena modyfikowana</returns>
        public static bool Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError($"{LOG} No active scene");
                return false;
            }

            // 1) Walidacja zaleznosci PRZED zmianami w scenie
            GameObject leftHand = FindByName(scene, "LeftHand");
            GameObject rightHand = FindByName(scene, "RightHand");
            OVRCameraRig camRig = Object.FindFirstObjectByType<OVRCameraRig>();

            if (leftHand == null)
            {
                Debug.LogError($"{LOG} LeftHand GO not found in scene -- abort");
                return false;
            }
            if (rightHand == null)
            {
                Debug.LogError($"{LOG} RightHand GO not found in scene -- abort");
                return false;
            }
            if (camRig == null)
            {
                Debug.LogError($"{LOG} OVRCameraRig not found in scene -- abort");
                return false;
            }

            // 2) Sciezka prefab
            string locoPath = AssetDatabase.GUIDToAssetPath(LOCOMOTION_PREFAB_GUID);
            if (string.IsNullOrEmpty(locoPath))
            {
                Debug.LogError($"{LOG} Locomotion prefab not found by guid {LOCOMOTION_PREFAB_GUID}");
                return false;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(locoPath);
            if (prefab == null)
            {
                Debug.LogError($"{LOG} Failed to load prefab at {locoPath}");
                return false;
            }

            // 3) Jesli Locomotion juz jest -- sprawdz wire, uzupelnij brakujace
            GameObject existingLoco = FindPrefabInstanceByGuid(scene, LOCOMOTION_PREFAB_GUID);
            if (existingLoco != null)
            {
                bool wireChanged = EnsureWire(existingLoco, leftHand, rightHand, camRig);
                if (wireChanged)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    Debug.Log($"{LOG} [REWIRED] Locomotion juz w scenie, uzupelniono wire");
                    return true;
                }
                Debug.Log($"{LOG} [OK] Locomotion juz w scenie z pelnym wire -- skip");
                return false;
            }

            // 4) Znajdz stary PLAGA44 -- wymagany zeby zamienic (nie spawnujemy znikad)
            GameObject oldAvatar = FindPrefabInstanceByGuid(scene, OLD_PLAGA44_GUID);
            if (oldAvatar == null)
            {
                Debug.LogError($"{LOG} Ani StylizedCharacterLocomotion, ani stary PLAGA44 nie w scenie -- abort. Recznie wstaw Locomotion prefab do sceny.");
                return false;
            }

            Vector3 savedPos = oldAvatar.transform.position;
            Quaternion savedRot = oldAvatar.transform.rotation;
            Transform savedParent = oldAvatar.transform.parent;
            Debug.Log($"{LOG} Znaleziono stary avatar '{oldAvatar.name}' at {savedPos} parent={savedParent?.name ?? "(scene root)"}");

            // 5) Instantuj nowy PRZED destroy -- zeby w razie bledu mozna cofnac bez utraty starego
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (instance == null)
            {
                Debug.LogError($"{LOG} InstantiatePrefab zwrocil null -- abort");
                return false;
            }
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Locomotion");

            // Walidacja CharacterRetargeter w nowym instance PRZED destroy starego
            var retargeter = instance.GetComponentInChildren<CharacterRetargeter>(true);
            if (retargeter == null)
            {
                Debug.LogError($"{LOG} CharacterRetargeter nie znaleziony w Locomotion instance -- abort, rollback");
                Undo.DestroyObjectImmediate(instance);
                return false;
            }

            // 6) Teraz usun stary i ustaw pozycje/parent nowego
            Undo.DestroyObjectImmediate(oldAvatar);
            if (savedParent != null)
                instance.transform.SetParent(savedParent, worldPositionStays: false);
            instance.transform.SetPositionAndRotation(savedPos, savedRot);
            Debug.Log($"{LOG} Instantiated '{instance.name}' at {savedPos}");

            // 7) Wire ISDKSkeletalProcessor source[0]
            if (!WireRetargeter(retargeter, leftHand, rightHand, camRig))
            {
                Debug.LogError($"{LOG} Wire retargeter FAILED -- scena moze byc w niepelnym stanie");
                return false;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{LOG} DONE -- avatar zamieniony, ISDK source wire kompletny.");
            return true;
        }

        /// <summary>
        /// Sprawdza aktualny wire na Locomotion avatar i uzupelnia brakujace refs.
        /// </summary>
        /// <returns>true jesli ktorykolwiek wire zostal zmieniony</returns>
        private static bool EnsureWire(GameObject locoInstance, GameObject leftHand, GameObject rightHand, OVRCameraRig camRig)
        {
            var retargeter = locoInstance.GetComponentInChildren<CharacterRetargeter>(true);
            if (retargeter == null)
            {
                Debug.LogError($"{LOG} CharacterRetargeter nie znaleziony w istniejacym Locomotion instance");
                return false;
            }
            return WireRetargeter(retargeter, leftHand, rightHand, camRig);
        }

        /// <summary>
        /// Ustawia _leftHand/_rightHand/_cameraRig na ISDKSkeletalProcessor source[0]
        /// tylko jesli sa aktualnie rozne -- zwraca true jesli ktorys zmieniony.
        /// Zwraca false + LogError jesli struktura retargetera nieoczekiwana.
        /// </summary>
        private static bool WireRetargeter(CharacterRetargeter retargeter, GameObject leftHand, GameObject rightHand, OVRCameraRig camRig)
        {
            var so = new SerializedObject(retargeter);
            var src = so.FindProperty("_sourceProcessorContainers");
            if (src == null || !src.isArray || src.arraySize == 0)
            {
                Debug.LogError($"{LOG} _sourceProcessorContainers empty or missing on retargeter '{retargeter.gameObject.name}'");
                return false;
            }

            var isdk = src.GetArrayElementAtIndex(0).FindPropertyRelative("_isdkProcessor");
            if (isdk == null)
            {
                Debug.LogError($"{LOG} _isdkProcessor property not found on source[0]");
                return false;
            }

            var leftProp = isdk.FindPropertyRelative("_leftHand");
            var rightProp = isdk.FindPropertyRelative("_rightHand");
            var camProp = isdk.FindPropertyRelative("_cameraRig");
            if (leftProp == null || rightProp == null || camProp == null)
            {
                Debug.LogError($"{LOG} ISDK processor props missing (leftHand={leftProp!=null}, rightHand={rightProp!=null}, cameraRig={camProp!=null})");
                return false;
            }

            bool changed = false;
            if (leftProp.objectReferenceValue != leftHand) { leftProp.objectReferenceValue = leftHand; changed = true; }
            if (rightProp.objectReferenceValue != rightHand) { rightProp.objectReferenceValue = rightHand; changed = true; }
            if ((OVRCameraRig)camProp.objectReferenceValue != camRig) { camProp.objectReferenceValue = camRig; changed = true; }

            if (changed)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(retargeter);
                Debug.Log($"{LOG} [WIRED] retargeter '{retargeter.gameObject.name}': leftHand={leftHand.name}, rightHand={rightHand.name}, cameraRig={camRig.name}");
            }
            else
            {
                Debug.Log($"{LOG} [OK] retargeter '{retargeter.gameObject.name}' wire already correct");
            }
            return changed;
        }

        private static GameObject FindByName(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name) return t.gameObject;
                }
            }
            return null;
        }

        private static GameObject FindPrefabInstanceByGuid(UnityEngine.SceneManagement.Scene scene, string targetGuid)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var go = t.gameObject;
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
                    var asset = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    if (asset == null) continue;
                    string path = AssetDatabase.GetAssetPath(asset);
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    if (guid == targetGuid) return go;
                }
            }
            return null;
        }
    }
}
#endif
