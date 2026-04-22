// =============================================================================
// BuildHybridRig.cs
// CYBERNOMAD / PLAGA44 -- buduje NOWY rig hybrydowy w osobnym root GO
// "PlagaHybridRig". Nic nie usuwa ze starej sceny. Jak nowy dziala -- Borys
// usunie stary recznie (albo ten skrypt w przyszlosci).
//
// Na wzor sample Meta MovementISDKLocomotion (chodzenie kontrolerami +
// avatar) + HandGrabInteractor prefaby z com.meta.xr.sdk.interaction (grab).
//
// Struktura po Run():
//   PlagaHybridRig (nowy root)
//   +-- OVRCameraRig                   (kamera + HMD)     guid 126d619c
//   +-- OVRInteractionComprehensive    (rig rak)          guid 0a7d2469
//   |     (ma wewnatrz Hand.cs + SyntheticHand dla kazdej reki)
//   +-- HandGrabInteractor Left         (grab)             guid 885ecae5
//   +-- HandGrabInteractor Right        (grab)             guid 885ecae5
//   +-- StylizedCharacterLocomotion    (avatar chodzacy)  guid 286d7e20
//
// Wire'y (przez SerializedObject po instantiate -- Unity generuje prawidlowe
// target fileID dla [SerializeReference] processorow):
//   StylizedCharacterLocomotion.CharacterRetargeter:
//     _sourceProcessorContainers[0]._isdkProcessor._leftHand  -> Hand(Left)  GO w OVRIC
//     _sourceProcessorContainers[0]._isdkProcessor._rightHand -> Hand(Right) GO w OVRIC
//     _sourceProcessorContainers[0]._isdkProcessor._cameraRig -> OVRCameraRig
//   HandGrabInteractor._hand -> Hand(Left/Right) w OVRIC (po Handedness)
//
// Idempotent: jesli "PlagaHybridRig" juz w scenie, sprawdza wire i uzupelnia.
// =============================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Meta.XR.Movement.Retargeting;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plaga44.Editor.Setup
{
    public static class BuildHybridRig
    {
        private const string LOG = "[PLAGA44][HybridRig]";
        private const string ROOT_GO_NAME = "PlagaHybridRig";

        // Guidy prefabow z Meta SDK
        private const string OVRIC_GUID              = "0a7d2469f24041c4284c66706f84c45e"; // OVRInteractionComprehensive
        private const string OVR_CAMERA_RIG_GUID     = "126d619cf4daa52469682f85c1378b4a"; // OVRCameraRig
        private const string LOCOMOTION_AVATAR_GUID  = "286d7e2005861d341a0a94d7f615675a"; // StylizedCharacterLocomotion
        private const string HAND_GRAB_INTERACTOR_GUID = "885ecae56b16f13428a67de5ae482a72"; // HandGrabInteractor.prefab

        public static bool Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid()) return ErrorB("No active scene");

            // Jesli root juz jest -- tylko rewire (idempotent)
            var existingRoot = FindRootByName(scene, ROOT_GO_NAME);
            if (existingRoot != null)
            {
                bool changed = EnsureWire(existingRoot, scene);
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    Debug.Log($"{LOG} [REWIRED] istniejacy '{ROOT_GO_NAME}' -- uzupelniono wire");
                    return true;
                }
                Debug.Log($"{LOG} [OK] '{ROOT_GO_NAME}' juz w scenie z pelnym wire -- skip");
                return false;
            }

            // Load prefabs
            if (!TryLoadPrefab(OVRIC_GUID,              out var ovricPrefab,  "OVRInteractionComprehensive")) return false;
            if (!TryLoadPrefab(OVR_CAMERA_RIG_GUID,     out var camRigPrefab, "OVRCameraRig"))                 return false;
            if (!TryLoadPrefab(LOCOMOTION_AVATAR_GUID,  out var avatarPrefab, "StylizedCharacterLocomotion"))  return false;
            if (!TryLoadPrefab(HAND_GRAB_INTERACTOR_GUID, out var hgiPrefab,  "HandGrabInteractor"))           return false;

            // Create new root GO
            var root = new GameObject(ROOT_GO_NAME);
            Undo.RegisterCreatedObjectUndo(root, "Create PlagaHybridRig");
            SceneManager.MoveGameObjectToScene(root, scene);

            // Instantiate prefabs as children of root
            var camRigGO  = InstantiateAsChild(camRigPrefab,  root.transform, "OVRCameraRig");
            var ovricGO   = InstantiateAsChild(ovricPrefab,   root.transform, "OVRInteractionComprehensive");
            var avatarGO  = InstantiateAsChild(avatarPrefab,  root.transform, "StylizedCharacterLocomotion");
            var hgiLeftGO = InstantiateAsChild(hgiPrefab,     root.transform, "HandGrabInteractor_Left");
            var hgiRightGO= InstantiateAsChild(hgiPrefab,     root.transform, "HandGrabInteractor_Right");
            if (camRigGO == null || ovricGO == null || avatarGO == null || hgiLeftGO == null || hgiRightGO == null)
                return ErrorB("Instantiate ktoregos prefaba zwrocil null -- abort");

            // Resolve components from OVRIC
            var camRig    = camRigGO.GetComponent<OVRCameraRig>() ?? camRigGO.GetComponentInChildren<OVRCameraRig>(true);
            var ovricHands = ovricGO.GetComponentsInChildren<Hand>(true);
            var handLeft  = ovricHands.FirstOrDefault(h => h.Handedness == Handedness.Left);
            var handRight = ovricHands.FirstOrDefault(h => h.Handedness == Handedness.Right);
            if (camRig == null)    return ErrorB("OVRCameraRig component nie znaleziony w instantowanym prefabie");
            if (handLeft == null)  return ErrorB("Hand Left (IHand) nie znaleziony w OVRIC");
            if (handRight == null) return ErrorB("Hand Right (IHand) nie znaleziony w OVRIC");

            Debug.Log($"{LOG} OVRIC: LeftHand='{handLeft.name}' RightHand='{handRight.name}' Cam='{camRig.name}'");

            // Reparent HandGrabInteractors pod odpowiednie Hand GO z OVRIC
            Undo.SetTransformParent(hgiLeftGO.transform,  handLeft.transform,  "Reparent HGI_Left");
            Undo.SetTransformParent(hgiRightGO.transform, handRight.transform, "Reparent HGI_Right");
            hgiLeftGO.transform.localPosition  = Vector3.zero; hgiLeftGO.transform.localRotation  = Quaternion.identity;
            hgiRightGO.transform.localPosition = Vector3.zero; hgiRightGO.transform.localRotation = Quaternion.identity;

            // Wire retargeter
            var retargeter = avatarGO.GetComponentInChildren<CharacterRetargeter>(true);
            if (retargeter == null) return ErrorB("CharacterRetargeter nie znaleziony w Locomotion avatar");
            if (!WireRetargeter(retargeter, handLeft.gameObject, handRight.gameObject, camRig)) return false;

            // Wire HandGrabInteractor._hand
            WireAllInteractors(hgiLeftGO.transform,  handLeft);
            WireAllInteractors(hgiRightGO.transform, handRight);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{LOG} DONE -- '{ROOT_GO_NAME}' gotowy. Stary rig nietkniety. Jak dziala -- usun stary recznie.");
            return true;
        }

        // =====================================================================
        // Idempotent rewire (gdy root juz istnieje)
        // =====================================================================

        private static bool EnsureWire(GameObject root, Scene scene)
        {
            var ovricGO  = root.transform.Cast<Transform>().FirstOrDefault(t => t.GetComponentsInChildren<Hand>(true).Length > 0)?.gameObject;
            var camRig   = root.GetComponentInChildren<OVRCameraRig>(true);
            var retargeter = root.GetComponentInChildren<CharacterRetargeter>(true);
            if (ovricGO == null || camRig == null || retargeter == null)
                return ErrorB("EnsureWire: brak OVRIC/OVRCameraRig/Retargeter w istniejacym root");

            var hands = ovricGO.GetComponentsInChildren<Hand>(true);
            var handLeft  = hands.FirstOrDefault(h => h.Handedness == Handedness.Left);
            var handRight = hands.FirstOrDefault(h => h.Handedness == Handedness.Right);
            if (handLeft == null || handRight == null) return ErrorB("EnsureWire: brak Hand L/R w OVRIC");

            bool changed = false;
            changed |= WireRetargeter(retargeter, handLeft.gameObject, handRight.gameObject, camRig);
            foreach (var hgi in root.GetComponentsInChildren<Oculus.Interaction.HandGrab.HandGrabInteractor>(true))
            {
                // Handedness deduce via parent hierarchy (Hand ancestor)
                var hand = hgi.GetComponentInParent<Hand>(true);
                if (hand != null) changed |= WireHandRef(hgi, hand);
            }
            return changed;
        }

        // =====================================================================
        // Wire helpers
        // =====================================================================

        private static bool WireRetargeter(CharacterRetargeter retargeter, GameObject leftHand, GameObject rightHand, OVRCameraRig camRig)
        {
            var so  = new SerializedObject(retargeter);
            var src = so.FindProperty("_sourceProcessorContainers");
            if (src == null || !src.isArray || src.arraySize == 0)
                return ErrorB($"_sourceProcessorContainers empty on '{retargeter.gameObject.name}'");
            var isdk = src.GetArrayElementAtIndex(0).FindPropertyRelative("_isdkProcessor");
            if (isdk == null) return ErrorB("_isdkProcessor missing on source[0]");

            bool changed = false;
            changed |= SetIfDifferent(isdk, "_leftHand",  leftHand);
            changed |= SetIfDifferent(isdk, "_rightHand", rightHand);
            changed |= SetIfDifferent(isdk, "_cameraRig", camRig);
            if (changed)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(retargeter);
                Debug.Log($"{LOG} [WIRED] retargeter: LH={leftHand.name} RH={rightHand.name} Cam={camRig.name}");
            }
            return changed;
        }

        private static bool WireAllInteractors(Transform parent, Hand hand)
        {
            bool changed = false;
            foreach (var hgi in parent.GetComponentsInChildren<Oculus.Interaction.HandGrab.HandGrabInteractor>(true))
                changed |= WireHandRef(hgi, hand);
            return changed;
        }

        private static bool WireHandRef(Oculus.Interaction.HandGrab.HandGrabInteractor hgi, Hand hand)
        {
            var so = new SerializedObject(hgi);
            var prop = so.FindProperty("_hand");
            if (prop == null) { Debug.LogWarning($"{LOG} HGI '{hgi.name}' -- no _hand property"); return false; }
            if (prop.objectReferenceValue == hand) return false;
            prop.objectReferenceValue = hand;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(hgi);
            return true;
        }

        private static bool SetIfDifferent(SerializedProperty parent, string path, Object value)
        {
            var p = parent.FindPropertyRelative(path);
            if (p == null) { Debug.LogError($"{LOG} Property '{path}' not found"); return false; }
            if (p.objectReferenceValue == value) return false;
            p.objectReferenceValue = value;
            return true;
        }

        // =====================================================================
        // Scene helpers
        // =====================================================================

        private static GameObject FindRootByName(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        private static GameObject InstantiateAsChild(GameObject prefab, Transform parent, string nameSuffix = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (go == null) return null;
            Undo.RegisterCreatedObjectUndo(go, "Instantiate " + prefab.name);
            if (!string.IsNullOrEmpty(nameSuffix)) go.name = nameSuffix;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        private static bool TryLoadPrefab(string guid, out GameObject prefab, string name)
        {
            prefab = null;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return ErrorB($"Prefab '{name}' nie znaleziony (guid {guid})");
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return ErrorB($"Failed to load '{name}' at {path}");
            return true;
        }

        private static bool ErrorB(string msg) { Debug.LogError($"{LOG} {msg}"); return false; }
    }
}
#endif
