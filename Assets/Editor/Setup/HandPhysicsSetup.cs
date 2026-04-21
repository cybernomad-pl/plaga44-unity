// =============================================================================
// HandPhysicsSetup.cs
// CYBERNOMAD -- Wlacza OVRSkeleton._enablePhysicsCapsules na kazdym OVRHand
// w scenie. Meta SDK generuje wtedy automatycznie CapsuleCollidery +
// Rigidbody (kinematic) na kosciach palcow -> reka BLOKUJE item fizycznie
// zamiast przez niego przechodzic.
//
// _enablePhysicsCapsules jest private SerializeField OVRSkeleton, ustawiamy
// przez SerializedObject (nie mozna programowo z runtime). Bootstrap odpala
// ten setup przed Play -> scene ready.
//
// Idempotentne: jesli juz ustawione true, nic nie robimy.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class HandPhysicsSetup
    {
        private const string LOG      = "[PLAGA44][HandPhysicsSetup]";
        private const string PropName = "_enablePhysicsCapsules";

        public static void Run()
        {
            var skeletons = Object.FindObjectsByType<OVRSkeleton>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (skeletons == null || skeletons.Length == 0)
            {
                Debug.LogWarning($"{LOG} No OVRSkeleton in scene -- hand physics capsules cannot be enabled. "
                    + "Expected LeftHandAnchor/OVRHandPrefab + RightHandAnchor/OVRHandPrefab.");
                return;
            }

            int enabled = 0, already = 0, skipped = 0;
            foreach (var sk in skeletons)
            {
                // Tylko rece -- body skeleton tez ma OVRSkeleton, ale tam capsule
                // collidery konfliktowalyby z CharacterRetargeter kosci.
                if (sk.GetSkeletonType() != OVRSkeleton.SkeletonType.HandLeft
                    && sk.GetSkeletonType() != OVRSkeleton.SkeletonType.HandRight)
                {
                    skipped++;
                    continue;
                }

                var so   = new SerializedObject(sk);
                var prop = so.FindProperty(PropName);
                if (prop == null)
                {
                    Debug.LogWarning($"{LOG} {sk.name}: property '{PropName}' not found in OVRSkeleton (SDK version mismatch?)");
                    continue;
                }

                if (prop.boolValue)
                {
                    already++;
                    continue;
                }

                Undo.RecordObject(sk, "Enable OVR hand physics capsules");
                prop.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(sk);
                enabled++;
                Debug.Log($"{LOG} [ENABLE] {GetScenePath(sk.transform)} -- physics capsules");
            }

            Debug.Log($"{LOG} done: enabled={enabled}, already={already}, skipped-non-hand={skipped}");
        }

        private static string GetScenePath(Transform t)
        {
            if (t == null) return "<null>";
            string path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }
    }
}
#endif
