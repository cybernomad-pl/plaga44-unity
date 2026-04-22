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
        public static void RunMenu() => Run();

        /// <summary>
        /// Idempotent pipeline:
        /// A) OVRIC juz w scenie + HandInteractors pod nim + wire kompletny -> skip (return false)
        /// B) OVRIC juz w scenie ale cos brakuje -> uzupelnij wire (return true)
        /// C) Brak OVRIC -> full pipeline (wymaga starego 'ISDK' rig + Locomotion avatar)
        /// </summary>
        public static bool Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())              return ErrorB("No active scene");

            if (!TryLoadPrefab(OVRIC_PREFAB_GUID, out var ovricPrefab))     return false;

            var locoAvatar = FindPrefabInstance(scene, LOCOMOTION_AVATAR_GUID);
            if (locoAvatar == null)            return ErrorB($"Locomotion avatar (guid {LOCOMOTION_AVATAR_GUID}) nie znaleziony -- ReplaceAvatarWithLocomotion musi odpalic wczesniej");

            var retargeter = locoAvatar.GetComponentInChildren<CharacterRetargeter>(true);
            if (retargeter == null)            return ErrorB("CharacterRetargeter nie znaleziony w Locomotion avatar");

            // -- Sciezka A/B: OVRIC juz istnieje --
            var existingOvric = FindPrefabInstance(scene, OVRIC_PREFAB_GUID);
            if (existingOvric != null)
            {
                bool wireChanged = EnsureWireOnly(existingOvric, retargeter, scene);
                if (wireChanged)
                {
                    Debug.Log($"{LOG} [REWIRED] OVRIC juz w scenie, uzupelniono wire");
                    return true;
                }
                Debug.Log($"{LOG} [OK] Hybrid rig juz kompletny -- skip");
                return false;
            }

            // -- Sciezka C: full pipeline, wymaga starego rigu --
            var oldRig = FindByName(scene, OLD_RIG_ROOT_NAME);
            if (oldRig == null)                return ErrorB($"Brak OVRIC i brak starego rigu '{OLD_RIG_ROOT_NAME}' -- nic do zrobienia");

            var leftInteractors  = FindChildTransform(oldRig, "HandInteractorsLeft");
            var rightInteractors = FindChildTransform(oldRig, "HandInteractorsRight");
            if (leftInteractors == null)       return ErrorB("HandInteractorsLeft subtree not found in old rig");
            if (rightInteractors == null)      return ErrorB("HandInteractorsRight subtree not found in old rig");

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
            if (ovric == null)                 return ErrorB("InstantiatePrefab OVRIC zwrocil null");
            Undo.RegisterCreatedObjectUndo(ovric, "Instantiate OVRInteractionComprehensive");
            ovric.transform.position = Vector3.zero;
            ovric.transform.rotation = Quaternion.identity;
            Debug.Log($"{LOG} Instantiated '{ovric.name}' (OVRInteractionComprehensive)");

            // -- 4-7. Reparent + wire (wspolna sciezka z Eklepem wire-only) --
            if (!ResolveOvricAndWire(ovric, retargeter, leftInteractors, rightInteractors))
                return false;

            // -- 8. Sprzatanie keepera --
            Undo.DestroyObjectImmediate(keeper);

            // -- 9. Mark dirty (Bootstrap kolektywnie zapisuje) --
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{LOG} DONE -- hybrid rig gotowy. Play Mode -- stickiem chodzisz, palcami lapiesz.");
            return true;
        }

        /// <summary>
        /// OVRIC juz w scenie. Sprawdz czy HandInteractors sa pod nim + wire kompletny.
        /// Uzupelnij tylko co brakuje.
        /// </summary>
        private static bool EnsureWireOnly(GameObject ovric, CharacterRetargeter retargeter, Scene scene)
        {
            var hands = ovric.GetComponentsInChildren<Hand>(true);
            var handLeft  = hands.FirstOrDefault(h => h.Handedness == Handedness.Left);
            var handRight = hands.FirstOrDefault(h => h.Handedness == Handedness.Right);
            if (handLeft == null || handRight == null) return ErrorB("OVRIC bez Hand Left/Right");
            var camRig = ovric.GetComponentInChildren<OVRCameraRig>(true);
            if (camRig == null) return ErrorB("OVRIC bez OVRCameraRig");

            bool changed = false;
            changed |= WireAllInteractors(handLeft.transform,  handLeft);
            changed |= WireAllInteractors(handRight.transform, handRight);
            changed |= WireRetargeter(retargeter, handLeft.gameObject, handRight.gameObject, camRig);
            if (changed) EditorSceneManager.MarkSceneDirty(scene);
            return changed;
        }

        /// <summary>
        /// Reparent HandInteractors pod Hand.cs, wire HandGrabInteractor._hand, wire retargeter.
        /// </summary>
        private static bool ResolveOvricAndWire(GameObject ovric, CharacterRetargeter retargeter, Transform leftInteractors, Transform rightInteractors)
        {
            var hands = ovric.GetComponentsInChildren<Hand>(true);
            var handLeft  = hands.FirstOrDefault(h => h.Handedness == Handedness.Left);
            var handRight = hands.FirstOrDefault(h => h.Handedness == Handedness.Right);
            if (handLeft == null || handRight == null) return ErrorB($"OVRIC Hand.cs niekompletne: L={handLeft!=null}, R={handRight!=null}");
            var camRig = ovric.GetComponentInChildren<OVRCameraRig>(true);
            if (camRig == null) return ErrorB("OVRIC bez OVRCameraRig");

            Debug.Log($"{LOG} OVRIC: LeftHand='{handLeft.name}' RightHand='{handRight.name}' OVRCameraRig='{camRig.name}'");

            Undo.SetTransformParent(leftInteractors,  handLeft.transform,  "Reparent LeftInteractors");
            Undo.SetTransformParent(rightInteractors, handRight.transform, "Reparent RightInteractors");
            leftInteractors.localPosition  = Vector3.zero; leftInteractors.localRotation  = Quaternion.identity;
            rightInteractors.localPosition = Vector3.zero; rightInteractors.localRotation = Quaternion.identity;

            WireAllInteractors(leftInteractors,  handLeft);
            WireAllInteractors(rightInteractors, handRight);
            return WireRetargeter(retargeter, handLeft.gameObject, handRight.gameObject, camRig);
        }

        private static bool WireAllInteractors(Transform parent, Hand hand)
        {
            bool changed = false;
            foreach (var hgi in parent.GetComponentsInChildren<Oculus.Interaction.HandGrab.HandGrabInteractor>(true))
                changed |= WireHandRef(hgi, hand);
            return changed;
        }

        // =====================================================================
        // Wire helpers
        // =====================================================================

        private static bool WireHandRef(Oculus.Interaction.HandGrab.HandGrabInteractor hgi, Hand hand)
        {
            var so = new SerializedObject(hgi);
            var prop = so.FindProperty("_hand");
            if (prop == null) { Debug.LogWarning($"{LOG} HandGrabInteractor '{hgi.name}' -- no _hand property"); return false; }
            if (prop.objectReferenceValue == hand) return false;
            prop.objectReferenceValue = hand;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(hgi);
            return true;
        }

        private static Transform FindChildTransform(GameObject root, string name)
            => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        /// <summary>Zwraca true JESLI wire zostal zmieniony (idempotentne).</summary>
        private static bool WireRetargeter(CharacterRetargeter retargeter, GameObject leftHand, GameObject rightHand, OVRCameraRig camRig)
        {
            var so  = new SerializedObject(retargeter);
            var src = so.FindProperty("_sourceProcessorContainers");
            if (src == null || !src.isArray || src.arraySize == 0)
            { Debug.LogError($"{LOG} _sourceProcessorContainers empty on '{retargeter.gameObject.name}'"); return false; }

            var isdk = src.GetArrayElementAtIndex(0).FindPropertyRelative("_isdkProcessor");
            if (isdk == null) { Debug.LogError($"{LOG} _isdkProcessor missing on source[0]"); return false; }

            bool changed = false;
            changed |= SetIfDifferent(isdk, "_leftHand",  leftHand);
            changed |= SetIfDifferent(isdk, "_rightHand", rightHand);
            changed |= SetIfDifferent(isdk, "_cameraRig", camRig);

            if (changed)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(retargeter);
                Debug.Log($"{LOG} [WIRED] retargeter '{retargeter.gameObject.name}': LH={leftHand.name} RH={rightHand.name} Cam={camRig.name}");
            }
            return changed;
        }

        private static bool SetIfDifferent(SerializedProperty parent, string path, Object value)
        {
            var p = parent.FindPropertyRelative(path);
            if (p == null) { Debug.LogError($"{LOG} Property '{path}' not found on {parent.propertyPath}"); return false; }
            if (p.objectReferenceValue == value) return false;
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

        private static bool  ErrorB(string msg)     { Debug.LogError($"{LOG} {msg}"); return false; }
    }
}
#endif
