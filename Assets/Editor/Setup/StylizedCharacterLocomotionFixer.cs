// =============================================================================
// StylizedCharacterLocomotionFixer.cs
// CYBERNOMAD -- Czysci zepsute overrides z PrefabInstance StylizedCharacter-
// Locomotion w scenie. Usuniete overrides wracaja do wartosci prefab-defaults
// z Meta Movement SDK Samples~/AdvancedSamples/ISDKLocomotion -- tam animacja
// chodzenia i IK dzialaja out-of-the-box.
//
// PROBLEM:
//   W TESTBED.unity wyzerowane (null) sa refs locomotion/IK processora:
//     _locomotionProcessor._footTransform / _headTransform -> null
//     _twoBoneIKProcessor._endJoint / _rootJoint / _midJoint / _ikTarget -> null
//     _twoBoneIKProcessor._weight -> 0, _iterations -> 0
//   + m_Controller przypisany do LocomotionController ktory samples NIE uzywa.
//
//   Skutek: processor nie wie jaka kosc animowac, IK wylaczone -> staly T-pose,
//   nogi sie nie ruszaja przy chodzeniu.
//
// FIX:
//   Usuwamy overrides ktore:
//   1) dotycza _sourceProcessorContainers / _targetProcessorContainers
//      i maja objectReference=null + property konczy sie na Transform/Joint/Target/Rig/Hand
//   2) m_Controller (samples nie przypisuja controllera, LocomotionProcessor
//      Meta ma wlasny silnik animowany bez RuntimeAnimatorController)
//   3) IK _weight=0, _iterations=0, _threshold=0 -- te wartosci wylaczaja IK
//
// Idempotentne: jesli overrides juz usuniete, nic nie robimy.
// =============================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class StylizedCharacterLocomotionFixer
    {
        private const string LOG     = "[PLAGA44][LocomotionFixer]";
        private const string RigName = "StylizedCharacterLocomotion";

        // LocomotionController.controller z ISDK Locomotion sample.
        // guid f138d28f925fa6442b115318a86915ef (sprawdzone w .meta).
        private const string LocomotionControllerPath =
            "Assets/Samples/Meta XR Movement SDK/83.0.0/Advanced Samples/ISDKLocomotion/Animations/LocomotionController.controller";

        public static void Run()
        {
            GameObject rigRoot = ResolveRig();
            if (rigRoot == null)
            {
                Debug.LogWarning($"{LOG} {RigName} not found in scene -- skipping fixer");
                return;
            }

            // KROK 0 -- wypelnij PlayerAvatar.locomotionController referencja
            // (runtime custom avatary dostaja controller -> eliminuje warning).
            WirePlayerAvatarLocomotionController();

            // KROK 1 -- wymus LocomotionController na Animatorze (nie polegaj
            // na PrefabInstance override).
            EnsureAnimatorController(rigRoot);

            var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(rigRoot);
            if (prefabRoot == null)
            {
                Debug.LogWarning($"{LOG} {rigRoot.name} is not a PrefabInstance -- skipping overrides cleanup");
                return;
            }

            var mods = PrefabUtility.GetPropertyModifications(prefabRoot);
            if (mods == null || mods.Length == 0)
            {
                Debug.Log($"{LOG} [OK] No modifications on {prefabRoot.name}");
                return;
            }

            var kept    = new List<PropertyModification>(mods.Length);
            int removed = 0;
            int total   = mods.Length;

            foreach (var m in mods)
            {
                if (IsBrokenOverride(m))
                {
                    removed++;
                    Debug.Log($"{LOG} [REVERT] {m.propertyPath} (value='{m.value}', ref={(m.objectReference == null ? "null" : m.objectReference.name)})");
                    continue;
                }
                kept.Add(m);
            }

            if (removed == 0)
            {
                Debug.Log($"{LOG} [OK] No broken overrides in {prefabRoot.name} ({total} total)");
                return;
            }

            Undo.RegisterCompleteObjectUndo(prefabRoot, "LocomotionFixer revert broken overrides");
            PrefabUtility.SetPropertyModifications(prefabRoot, kept.ToArray());
            EditorUtility.SetDirty(prefabRoot);
            EditorSceneManager.MarkSceneDirty(prefabRoot.scene);

            Debug.Log($"{LOG} Removed {removed}/{total} broken overrides from {prefabRoot.name}. "
                + "Locomotion + IK processors back to prefab defaults.");
        }

        private static bool IsBrokenOverride(PropertyModification m)
        {
            if (m == null || string.IsNullOrEmpty(m.propertyPath)) return false;
            string path = m.propertyPath;

            // m_Controller -- NIE USUWAC. Meta LocomotionSkeletalProcessor
            // wywoluje Animator.SetFloat(int, float) wewnetrznie -- WYMAGA
            // AnimatorController przypisanego. Bez niego leci warning:
            //   "Animator is not playing an AnimatorController"
            // i locomotion/animacja chodzenia nie dziala (SetFloat na pusty
            // animator = no-op). Zostawiamy override w scenie -- LocomotionController
            // z ISDKLocomotion sample jest poprawnym wyborem.

            // Tylko modifications na processor arrays (nie ruszamy innych).
            bool isProcessor = path.StartsWith("_sourceProcessorContainers")
                            || path.StartsWith("_targetProcessorContainers");
            if (!isProcessor) return false;

            // 2) Null ref na slocie ktory oczekuje Transform/GameObject/Joint/IKTarget.
            //    Objaw: objectReference=null + property konczy sie sufiksem koscianym.
            bool isNullObjectRef = m.objectReference == null;
            bool isBoneRefSlot = path.EndsWith("Transform")
                              || path.EndsWith("Joint")
                              || path.EndsWith("Target")
                              || path.EndsWith("Rig")
                              || path.EndsWith("Hand")
                              || path.EndsWith("_footTransform")
                              || path.EndsWith("_headTransform");
            if (isNullObjectRef && isBoneRefSlot) return true;

            // 3) IK disabling values -- weight=0, iterations=0, threshold=0
            //    Prefab ma sensowne defaulty (weight=1, iterations=3).
            bool isIK = path.Contains("_twoBoneIKProcessor.");
            if (isIK && (path.EndsWith("._weight") || path.EndsWith("._iterations"))
                && m.value == "0")
                return true;

            return false;
        }

        // Wypelnia SerializeField PlayerAvatar.locomotionController referencja.
        // Runtime PlayerAvatar.InstantiateAvatar uzywa tego do przypisania
        // controllera KAZDEMU spawnowanemu avatarowi (nie tylko defaultRig).
        private static void WirePlayerAvatarLocomotionController()
        {
            var avatar = Object.FindAnyObjectByType<Plaga44.PlayerAvatar>();
            if (avatar == null)
            {
                Debug.LogWarning($"{LOG} PlayerAvatar not found in scene -- cannot wire locomotionController ref");
                return;
            }
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LocomotionControllerPath);
            if (controller == null)
            {
                Debug.LogError($"{LOG} LocomotionController not found: {LocomotionControllerPath}");
                return;
            }

            if (avatar.locomotionController == controller)
            {
                Debug.Log($"{LOG} [OK] PlayerAvatar.locomotionController already = LocomotionController");
                return;
            }

            var so = new SerializedObject(avatar);
            var prop = so.FindProperty("locomotionController");
            if (prop == null)
            {
                Debug.LogError($"{LOG} SerializedProperty 'locomotionController' not found on PlayerAvatar");
                return;
            }
            prop.objectReferenceValue = controller;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(avatar);
            Debug.Log($"{LOG} [WIRED] PlayerAvatar.locomotionController = LocomotionController");
        }

        private static GameObject ResolveRig()
        {
            // Primary: via PlayerAvatar.defaultRig
            var avatar = Object.FindAnyObjectByType<Plaga44.PlayerAvatar>();
            if (avatar != null && avatar.defaultRig != null) return avatar.defaultRig;

            // Fallback: by name
            var byName = GameObject.Find(RigName);
            return byName;
        }

        // Wymus Animator.runtimeAnimatorController = LocomotionController.
        // Bez controllera LocomotionSkeletalProcessor.UpdatePose wywoluje
        // SetFloat -> "Animator is not playing an AnimatorController" warning
        // per frame + retargeter stan undefined -> avatar sie trzesie/deformuje.
        private static void EnsureAnimatorController(GameObject rig)
        {
            var animator = rig.GetComponent<Animator>();
            if (animator == null) animator = rig.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogWarning($"{LOG} No Animator on {rig.name} -- cannot assign LocomotionController");
                return;
            }

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LocomotionControllerPath);
            if (controller == null)
            {
                Debug.LogError($"{LOG} LocomotionController not found at: {LocomotionControllerPath}");
                return;
            }

            if (animator.runtimeAnimatorController == controller)
            {
                Debug.Log($"{LOG} [OK] Animator controller already = LocomotionController");
                return;
            }

            Undo.RecordObject(animator, "Assign LocomotionController");
            animator.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(animator);
            Debug.Log($"{LOG} [ASSIGNED] Animator.runtimeAnimatorController = LocomotionController");
        }
    }
}
#endif
