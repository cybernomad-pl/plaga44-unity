// =============================================================================
// BuildHybridRig.cs
// CYBERNOMAD / PLAGA44 -- buduje JEDEN rig ktory ma:
//   - Locomotion (z MovementISDKLocomotion sample): kontrolery + stick chodzenie
//   - Grab (z MovementISDKIntegration sample): HandGrabInteractor do lapania
//
// PIPELINE:
//   1. Waliduje stan sceny
//   2. Znajduje stary recznie zbudowany GO 'ISDK' (rig ISDKIntegration style)
//   3. Wyciaga z niego HandInteractorsLeft + HandInteractorsRight subtree
//      (reparentuje do TEMP keepera zeby nie zginely przy destroy)
//   4. Usuwa caly stary GO 'ISDK'
//   5. Instantuje OVRInteractionComprehensive (Meta prefab -- OVR rig + Hand.cs
//      + SyntheticHand, guid 0a7d2469f24041c4284c66706f84c45e)
//   6. Znajduje Hand.cs Left / Right w nowym rigu (po Handedness)
//   7. Reparenta HandInteractors pod GO ktore ma Hand.cs
//   8. Wire HandGrabInteractor._hand na Hand.cs nowego rigu
//   9. Wire Locomotion avatar _leftHand/_rightHand/_cameraRig na nowy rig
//  10. Save scene
//
// WYMAGANIA WSTEPNE:
//   - W scenie MUSI byc StylizedCharacterLocomotion (guid 286d7e20...) --
//     wstaw recznie lub przez PLAGA44/Setup/Replace menu
//   - W scenie MUSI byc stary GO 'ISDK' z HandInteractorsLeft/Right (obecnie)
//   - OVRInteractionComprehensive prefab asset musi byc w packages
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

        private const string OVRIC_PREFAB_GUID     = "0a7d2469f24041c4284c66706f84c45e"; // OVRInteractionComprehensive
        private const string LOCOMOTION_AVATAR_GUID = "286d7e2005861d341a0a94d7f615675a"; // StylizedCharacterLocomotion
        private const string OLD_RIG_ROOT_NAME     = "ISDK";

        [MenuItem("PLAGA44/Setup/Build Hybrid Rig (Locomotion + Grab)")]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())              { Error("No active scene"); return; }

            // -- Walidacja wstepna --
            if (!TryLoadPrefab(OVRIC_PREFAB_GUID, out var ovricPrefab))     return;

            var locoAvatar = FindPrefabInstance(scene, LOCOMOTION_AVATAR_GUID);
            if (locoAvatar == null)            { Error($"Locomotion avatar (guid {LOCOMOTION_AVATAR_GUID}) nie znaleziony -- odpal 'Replace PLAGA44 Avatar' najpierw"); return; }

            var oldRig = FindByName(scene, OLD_RIG_ROOT_NAME);
            if (oldRig == null)                { Error($"Stary rig GO '{OLD_RIG_ROOT_NAME}' nie znaleziony -- nie ma czego wyrzucac"); return; }

            var leftInteractors  = oldRig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "HandInteractorsLeft");
            var rightInteractors = oldRig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "HandInteractorsRight");
            if (leftInteractors == null)       { Error("HandInteractorsLeft subtree not found in old rig"); return; }
            if (rightInteractors == null)      { Error("HandInteractorsRight subtree not found in old rig"); return; }

            var retargeter = locoAvatar.GetComponentInChildren<CharacterRetargeter>(true);
            if (retargeter == null)            { Error("CharacterRetargeter nie znaleziony w Locomotion avatar"); return; }

            // -- 1. Zachowaj HandInteractors (reparent do keepera zeby przezyly destroy starego rigu) --
            var keeper = new GameObject("__HybridRigKeeper__");
            Undo.RegisterCreatedObjectUndo(keeper, "HybridRig keeper");
            Undo.SetTransformParent(leftInteractors,  keeper.transform, "Move LeftInteractors out");
            Undo.SetTransformParent(rightInteractors, keeper.transform, "Move RightInteractors out");

            // -- 2. Usun stary rig --
            Undo.DestroyObjectImmediate(oldRig);
            Debug.Log($"{LOG} Usunieto stary rig '{OLD_RIG_ROOT_NAME}', HandInteractors zachowane w keeperze");

            // -- 3. Instantuj OVRInteractionComprehensive --
            var ovric = (GameObject)PrefabUtility.InstantiatePrefab(ovricPrefab, scene);
            if (ovric == null)                 { Error("InstantiatePrefab OVRIC zwrocil null"); return; }
            Undo.RegisterCreatedObjectUndo(ovric, "Instantiate OVRInteractionComprehensive");
            ovric.transform.position = Vector3.zero;
            ovric.transform.rotation = Quaternion.identity;
            Debug.Log($"{LOG} Instantiated '{ovric.name}' (OVRInteractionComprehensive)");

            // -- 4. Znajdz Hand Left/Right w OVRIC (po Handedness) --
            var hands = ovric.GetComponentsInChildren<Hand>(true);
            var handLeft  = hands.FirstOrDefault(h => h.Handedness == Handedness.Left);
            var handRight = hands.FirstOrDefault(h => h.Handedness == Handedness.Right);
            if (handLeft == null || handRight == null)
            {
                Error($"Hand.cs w OVRIC niekompletne: L={handLeft!=null}, R={handRight!=null}");
                return;
            }
            var camRig = ovric.GetComponentInChildren<OVRCameraRig>(true);
            if (camRig == null)                { Error("OVRCameraRig nie znaleziony w OVRIC"); return; }

            Debug.Log($"{LOG} OVRIC components: LeftHand='{handLeft.name}' RightHand='{handRight.name}' OVRCameraRig='{camRig.name}'");

            // -- 5. Reparent HandInteractors pod GO z Hand.cs --
            Undo.SetTransformParent(leftInteractors,  handLeft.transform,  "Move LeftInteractors under OVRIC LeftHand");
            Undo.SetTransformParent(rightInteractors, handRight.transform, "Move RightInteractors under OVRIC RightHand");
            leftInteractors.localPosition  = Vector3.zero;
            leftInteractors.localRotation  = Quaternion.identity;
            rightInteractors.localPosition = Vector3.zero;
            rightInteractors.localRotation = Quaternion.identity;

            // -- 6. Wire HandGrabInteractor._hand -- iteruj wszystkie HandGrabInteractor w subtree --
            int wiredInteractors = 0;
            foreach (var hgi in leftInteractors.GetComponentsInChildren<Oculus.Interaction.HandGrab.HandGrabInteractor>(true))
                wiredInteractors += WireHandRef(hgi, handLeft);
            foreach (var hgi in rightInteractors.GetComponentsInChildren<Oculus.Interaction.HandGrab.HandGrabInteractor>(true))
                wiredInteractors += WireHandRef(hgi, handRight);
            Debug.Log($"{LOG} Wired {wiredInteractors} HandGrabInteractor._hand refs");

            // -- 7. Wire Locomotion avatar (ISDKSkeletalProcessor source[0]) --
            if (!WireRetargeter(retargeter, handLeft.gameObject, handRight.gameObject, camRig))
                return; // Error juz zalogowany

            // -- 8. Sprzatanie keepera --
            Undo.DestroyObjectImmediate(keeper);

            // -- 9. Save --
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"{LOG} DONE -- hybrid rig gotowy. Play Mode -- stickiem chodzisz, palcami lapiesz.");
        }

        // =====================================================================
        // Wire helpers
        // =====================================================================

        private static int WireHandRef(Oculus.Interaction.HandGrab.HandGrabInteractor hgi, Hand hand)
        {
            var so = new SerializedObject(hgi);
            var prop = so.FindProperty("_hand");
            if (prop == null) return LogZero($"HandGrabInteractor '{hgi.name}' -- no _hand property");
            if (prop.objectReferenceValue == hand) return 0;
            prop.objectReferenceValue = hand;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(hgi);
            return 1;
        }

        private static int LogZero(string msg) { Debug.LogWarning($"{LOG} {msg}"); return 0; }

        private static bool WireRetargeter(CharacterRetargeter retargeter, GameObject leftHand, GameObject rightHand, OVRCameraRig camRig)
        {
            var so  = new SerializedObject(retargeter);
            var src = so.FindProperty("_sourceProcessorContainers");
            if (src == null || !src.isArray || src.arraySize == 0)
                return ErrorB($"_sourceProcessorContainers empty on '{retargeter.gameObject.name}'");

            var isdk = src.GetArrayElementAtIndex(0).FindPropertyRelative("_isdkProcessor");
            if (isdk == null) return ErrorB("_isdkProcessor missing on source[0]");

            if (!Set(isdk, "_leftHand",  leftHand))  return false;
            if (!Set(isdk, "_rightHand", rightHand)) return false;
            if (!Set(isdk, "_cameraRig", camRig))    return false;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(retargeter);
            Debug.Log($"{LOG} [WIRED] retargeter '{retargeter.gameObject.name}': LH={leftHand.name} RH={rightHand.name} Cam={camRig.name}");
            return true;
        }

        private static bool Set(SerializedProperty parent, string path, Object value)
        {
            var p = parent.FindPropertyRelative(path);
            if (p == null) return ErrorB($"Property '{path}' not found on {parent.propertyPath}");
            p.objectReferenceValue = value;
            return true;
        }

        // =====================================================================
        // Scene helpers
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

        private static bool TryLoadPrefab(string guid, out GameObject prefab)
        {
            prefab = null;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return ErrorB($"Prefab not found by guid {guid}");
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return ErrorB($"Failed to load prefab at {path}");
            return true;
        }

        // =====================================================================
        // Logging
        // =====================================================================

        private static void Error(string msg)       { Debug.LogError($"{LOG} {msg}"); }
        private static bool  ErrorB(string msg)     { Debug.LogError($"{LOG} {msg}"); return false; }
    }
}
#endif
