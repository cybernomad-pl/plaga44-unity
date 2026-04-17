// =============================================================================
// HandFingerFreezer.cs
// CYBERNOMAD -- Freezes finger bone rotations on the SDK rig hand that is
// currently holding an object. Prevents SDK hand tracking from moving fingers
// while grabbing -- item looks "held firm" instead of fingers flapping.
//
// Usage:
//   HandFingerFreezer.Freeze(OVRInput.Controller.RTouch, fistPose:true)
//   HandFingerFreezer.Unfreeze(OVRInput.Controller.RTouch)
//
// Works by capturing finger bone localRotations in LateUpdate AFTER SDK
// retargeter has applied its pose, and overwriting them with frozen values.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.Inventory
{
    /// <summary>Singleton component auto-attached to SDK rig root.
    /// LateUpdate overrides finger bone rotations for frozen hands.</summary>
    [DefaultExecutionOrder(10000)] // after SDK retargeter
    public class HandFingerFreezer : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][FingerFreezer]";

        private static HandFingerFreezer _instance;
        public static HandFingerFreezer Instance => _instance;

        // Fingers per hand. Each finger has 3 bones (Proximal, Intermediate, Distal).
        private static readonly HumanBodyBones[] LeftFingerBones = new[]
        {
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
        };

        private static readonly HumanBodyBones[] RightFingerBones = new[]
        {
            HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
            HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
            HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
            HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal,
        };

        private Animator _animator;
        private readonly Dictionary<Transform, Quaternion> _frozenLeftBones = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, Quaternion> _frozenRightBones = new Dictionary<Transform, Quaternion>();

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
            _animator = GetComponentInChildren<Animator>(true);
            if (_animator == null || !_animator.isHuman)
                Debug.LogWarning($"{LOG} No humanoid Animator found in children of {name} -- finger freeze disabled");
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public static void Freeze(OVRInput.Controller ctrl, bool fistPose)
        {
            var inst = _instance ?? FindAnyObjectByType<HandFingerFreezer>();
            if (inst == null)
            {
                Debug.LogWarning($"{LOG} Freeze({ctrl}) -- no HandFingerFreezer in scene");
                return;
            }
            inst.FreezeHand(ctrl, fistPose);
        }

        public static void Unfreeze(OVRInput.Controller ctrl)
        {
            if (_instance == null) return;
            _instance.UnfreezeHand(ctrl);
        }

        private void FreezeHand(OVRInput.Controller ctrl, bool fistPose)
        {
            if (_animator == null || !_animator.isHuman) return;
            var bones = ctrl == OVRInput.Controller.LTouch ? LeftFingerBones : RightFingerBones;
            var dict = ctrl == OVRInput.Controller.LTouch ? _frozenLeftBones : _frozenRightBones;
            dict.Clear();

            foreach (var hbb in bones)
            {
                var t = _animator.GetBoneTransform(hbb);
                if (t == null) continue;
                Quaternion rot = fistPose
                    ? Quaternion.Euler(GetFistRotation(hbb))
                    : t.localRotation; // capture current pose
                dict[t] = rot;
            }
            Debug.Log($"{LOG} FREEZE {ctrl}: {dict.Count} bones locked ({(fistPose ? "fist" : "current")})");
        }

        private void UnfreezeHand(OVRInput.Controller ctrl)
        {
            var dict = ctrl == OVRInput.Controller.LTouch ? _frozenLeftBones : _frozenRightBones;
            int n = dict.Count;
            dict.Clear();
            Debug.Log($"{LOG} UNFREEZE {ctrl}: {n} bones released");
        }

        // Simple fist pose: proximal 45deg curl, intermediate 60, distal 45 (local X axis)
        // Thumb different axis (local Z ~30deg opposition).
        private static Vector3 GetFistRotation(HumanBodyBones bone)
        {
            bool isThumb = bone.ToString().Contains("Thumb");
            bool isProximal = bone.ToString().Contains("Proximal");
            bool isIntermediate = bone.ToString().Contains("Intermediate");

            if (isThumb)
            {
                if (isProximal) return new Vector3(0, 0, -30);
                if (isIntermediate) return new Vector3(0, 0, -25);
                return new Vector3(0, 0, -20); // distal
            }
            // Other fingers: curl around X
            if (isProximal) return new Vector3(-45, 0, 0);
            if (isIntermediate) return new Vector3(-60, 0, 0);
            return new Vector3(-45, 0, 0); // distal
        }

        private void LateUpdate()
        {
            // Apply frozen rotations AFTER SDK retargeter has written its pose
            ApplyFrozen(_frozenLeftBones);
            ApplyFrozen(_frozenRightBones);
        }

        private static void ApplyFrozen(Dictionary<Transform, Quaternion> dict)
        {
            if (dict.Count == 0) return;
            foreach (var kv in dict)
            {
                if (kv.Key != null) kv.Key.localRotation = kv.Value;
            }
        }
    }
}
