// =============================================================================
// BodyPhysicsSetup.cs
// CYBERNOMAD -- Dodaje CapsuleCollidery na kosciach Humanoid avatara gracza.
// Efekt: cialo (tors, ramie, przedramie, noga, glowa) fizycznie blokuje
// itemy + ponadto nie mozna "przebic" sciany ruchem VR.
//
// KOMPLEMENTARNE:
//   - HandPhysicsSetup: palce + dlon (OVRSkeleton capsule generator)
//   - BodyPhysicsSetup: glowa, tors, ramiona, przedramiona, uda, lydki
//   Wspolnie = pelne cialo fizyczne.
//
// DZIALANIE:
//   1. Znajdz Animator Humanoid na defaultRig
//   2. Dla kazdej pary (bone, childBone) w BoneSegments -> CapsuleCollider
//      o wysokosci = odleglosc bone->child, radius = default dla segmentu
//   3. Glowa -> SphereCollider
//   4. Rigidbody Kinematic na rigu (dla physics system awareness)
//
// Idempotentne: jesli capsule juz istnieje na kosci (po nazwie GO), skip.
// =============================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class BodyPhysicsSetup
    {
        private const string LOG               = "[PLAGA44][BodyPhysicsSetup]";
        private const string ColliderGoSuffix  = "_PhysCol";
        private const float  HeadSphereRadius  = 0.11f;  // ~22cm diameter

        // Segmenty ciala: (bone, childBone, radius) -- capsule generuje sie
        // pomiedzy bone a childBone, radius dobrany do grubosci segmentu.
        private struct BoneSegment
        {
            public HumanBodyBones from;
            public HumanBodyBones to;
            public float          radius;

            public BoneSegment(HumanBodyBones f, HumanBodyBones t, float r)
            { from = f; to = t; radius = r; }
        }

        private static readonly BoneSegment[] Segments = new[]
        {
            // Tors
            new BoneSegment(HumanBodyBones.Hips,         HumanBodyBones.Spine,        0.13f),
            new BoneSegment(HumanBodyBones.Spine,        HumanBodyBones.Chest,        0.12f),
            new BoneSegment(HumanBodyBones.Chest,        HumanBodyBones.UpperChest,   0.13f),
            new BoneSegment(HumanBodyBones.UpperChest,   HumanBodyBones.Neck,         0.11f),
            // Lewa reka
            new BoneSegment(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 0.05f),
            new BoneSegment(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,     0.04f),
            // Prawa reka
            new BoneSegment(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.05f),
            new BoneSegment(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,     0.04f),
            // Lewa noga
            new BoneSegment(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 0.07f),
            new BoneSegment(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,     0.055f),
            // Prawa noga
            new BoneSegment(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 0.07f),
            new BoneSegment(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,     0.055f),
        };

        public static void Run()
        {
            var avatar = Object.FindAnyObjectByType<Plaga44.PlayerAvatar>();
            GameObject rig = avatar != null ? avatar.defaultRig : null;
            if (rig == null)
            {
                Debug.LogWarning($"{LOG} PlayerAvatar.defaultRig null -- skip body physics");
                return;
            }
            var animator = rig.GetComponent<Animator>();
            if (animator == null) animator = rig.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning($"{LOG} No Humanoid Animator on {rig.name} -- skip");
                return;
            }

            EnsureKinematicRigidbody(rig);

            int added   = 0;
            int already = 0;
            int missing = 0;

            // Tors/konczyny -- capsule per segment
            foreach (var seg in Segments)
            {
                var from = animator.GetBoneTransform(seg.from);
                var to   = animator.GetBoneTransform(seg.to);
                if (from == null || to == null)
                {
                    missing++;
                    continue;
                }
                if (HasCapsule(from)) { already++; continue; }
                CreateCapsule(from, to, seg.radius);
                added++;
            }

            // Glowa -- sphere (jedyny sens dla trafienia kuli)
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
            {
                if (HasSphere(head)) already++;
                else { CreateSphere(head, HeadSphereRadius); added++; }
            }
            else missing++;

            Debug.Log($"{LOG} {rig.name}: added={added}, already={already}, missing={missing} (bone refs)");
        }

        private static void EnsureKinematicRigidbody(GameObject root)
        {
            var rb = root.GetComponent<Rigidbody>();
            if (rb == null) rb = root.AddComponent<Rigidbody>();
            rb.isKinematic              = true;
            rb.useGravity               = false;
            rb.interpolation            = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode   = CollisionDetectionMode.ContinuousSpeculative;
            EditorUtility.SetDirty(rb);
        }

        private static bool HasCapsule(Transform bone)
        {
            // Szukamy dziecka o nazwie "<boneName>_PhysCol"
            string want = bone.name + ColliderGoSuffix;
            for (int i = 0; i < bone.childCount; i++)
                if (bone.GetChild(i).name == want) return true;
            return false;
        }

        private static bool HasSphere(Transform bone)
        {
            string want = bone.name + ColliderGoSuffix;
            for (int i = 0; i < bone.childCount; i++)
                if (bone.GetChild(i).name == want) return true;
            return false;
        }

        private static void CreateCapsule(Transform from, Transform to, float radius)
        {
            var go = new GameObject(from.name + ColliderGoSuffix);
            Undo.RegisterCreatedObjectUndo(go, "BodyPhysicsSetup create capsule");
            go.transform.SetParent(from, worldPositionStays: false);

            float length = Vector3.Distance(from.position, to.position);
            var cap = go.AddComponent<CapsuleCollider>();
            cap.radius    = radius;
            cap.height    = length + radius * 2f;
            cap.direction = 2; // Z-axis (kapsula wzdluz Z)
            cap.center    = Vector3.zero;

            // Obroc tak zeby kapsula szla od 'from' do 'to' wzdluz Z
            Vector3 dir = to.position - from.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                go.transform.rotation   = Quaternion.LookRotation(dir.normalized);
                go.transform.position   = from.position + dir * 0.5f;
            }

            EditorUtility.SetDirty(go);
        }

        private static void CreateSphere(Transform bone, float radius)
        {
            var go = new GameObject(bone.name + ColliderGoSuffix);
            Undo.RegisterCreatedObjectUndo(go, "BodyPhysicsSetup create sphere");
            go.transform.SetParent(bone, worldPositionStays: false);

            var sph = go.AddComponent<SphereCollider>();
            sph.radius = radius;
            sph.center = new Vector3(0f, radius * 0.8f, 0f); // glowa wyzej niz bone pivot (szyja)

            EditorUtility.SetDirty(go);
        }
    }
}
#endif
