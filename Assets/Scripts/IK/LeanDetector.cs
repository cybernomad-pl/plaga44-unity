// LeanDetector.cs
// PLAGA '44 -- Detects player leaning left/right by tracking headset X offset
// relative to a calibrated center position (the VR rig root or a body anchor).
// Intended for peek-around-corner mechanics.
//
// Namespace: Plaga44.IK

using UnityEngine;
using UnityEngine.Events;

namespace Plaga44.IK
{
    /// <summary>
    /// Detects physical lean by measuring the horizontal (X-axis) offset of the
    /// headset relative to the player's rig root. Fires directional lean events.
    /// </summary>
    public class LeanDetector : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("The CenterEyeAnchor or Camera.main transform. Auto-found if null.")]
        public Transform headsetTransform;

        [Tooltip("The rig root whose local X axis defines 'center'. " +
                 "Usually the OVRCameraRig or the GameObject this script is on.")]
        public Transform rigRoot;

        [Header("Thresholds")]
        [Tooltip("Metres the head must move laterally from center before a lean is detected.")]
        [Range(0.05f, 0.5f)]
        public float leanThreshold = 0.12f;

        [Tooltip("Hysteresis: head must return within this margin of center before OnLeanCenter fires.")]
        [Range(0f, 0.1f)]
        public float centerHysteresis = 0.04f;

        [Header("Events")]
        [Tooltip("Fired once when player leans to the left past the threshold.")]
        public UnityEvent OnLeanLeft;

        [Tooltip("Fired once when player leans to the right past the threshold.")]
        public UnityEvent OnLeanRight;

        [Tooltip("Fired once when player returns to center (within hysteresis band).")]
        public UnityEvent OnLeanCenter;

        [Tooltip("Fired every frame with signed lean offset in metres (negative = left, positive = right).")]
        public UnityEvent<float> OnLeanOffsetChanged;

        // ── Properties ───────────────────────────────────────────────────

        /// <summary>Current lean direction.</summary>
        public LeanDirection CurrentLean { get; private set; } = LeanDirection.Center;

        /// <summary>
        /// Signed lateral offset of the headset from the rig center in metres.
        /// Negative = left, positive = right, in the rig's local X axis.
        /// </summary>
        public float LeanOffset { get; private set; }

        // ── Types ────────────────────────────────────────────────────────

        public enum LeanDirection { Left, Center, Right }

        // ── Private ──────────────────────────────────────────────────────

        private float _prevOffset;

        // ── Unity lifecycle ──────────────────────────────────────────────

        private void Start()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (headsetTransform == null || rigRoot == null)
            {
                ResolveReferences();
                if (headsetTransform == null || rigRoot == null) return;
            }

            // Project headset world position into rig local space -- only X matters
            Vector3 localHead = rigRoot.InverseTransformPoint(headsetTransform.position);
            LeanOffset = localHead.x;

            // Fire offset event when changed meaningfully
            if (!Mathf.Approximately(LeanOffset, _prevOffset))
            {
                OnLeanOffsetChanged?.Invoke(LeanOffset);
                _prevOffset = LeanOffset;
            }

            // State machine: Center -> Left/Right -> Center
            switch (CurrentLean)
            {
                case LeanDirection.Center:
                    if (LeanOffset <= -leanThreshold)
                    {
                        CurrentLean = LeanDirection.Left;
                        OnLeanLeft?.Invoke();
                    }
                    else if (LeanOffset >= leanThreshold)
                    {
                        CurrentLean = LeanDirection.Right;
                        OnLeanRight?.Invoke();
                    }
                    break;

                case LeanDirection.Left:
                    // Return to center when offset is within hysteresis
                    if (LeanOffset >= -(leanThreshold - centerHysteresis))
                    {
                        // Check if crossing all the way to right
                        if (LeanOffset >= leanThreshold)
                        {
                            CurrentLean = LeanDirection.Right;
                            OnLeanCenter?.Invoke();
                            OnLeanRight?.Invoke();
                        }
                        else
                        {
                            CurrentLean = LeanDirection.Center;
                            OnLeanCenter?.Invoke();
                        }
                    }
                    break;

                case LeanDirection.Right:
                    if (LeanOffset <= (leanThreshold - centerHysteresis))
                    {
                        if (LeanOffset <= -leanThreshold)
                        {
                            CurrentLean = LeanDirection.Left;
                            OnLeanCenter?.Invoke();
                            OnLeanLeft?.Invoke();
                        }
                        else
                        {
                            CurrentLean = LeanDirection.Center;
                            OnLeanCenter?.Invoke();
                        }
                    }
                    break;
            }
        }

        // ── Public API ───────────────────────────────────────────────────

        /// <summary>
        /// Returns normalised lean amount: -1 = fully left, 0 = center, 1 = fully right.
        /// Clamped to [-1, 1].
        /// </summary>
        public float GetNormalisedLean()
        {
            return Mathf.Clamp(LeanOffset / leanThreshold, -1f, 1f);
        }

        /// <summary>
        /// Recalibrate center: sets the rig root reference frame to the player's current position.
        /// Call when the player repositions (e.g., after teleport).
        /// </summary>
        public void RecalibrateCenter()
        {
            // Re-resolve references in case rig moved
            ResolveReferences();
            Debug.Log($"[LeanDetector] Center recalibrated. Current offset: {LeanOffset:F3} m");
        }

        // ── Internal ─────────────────────────────────────────────────────

        private void ResolveReferences()
        {
            if (headsetTransform == null)
            {
#if HAS_META_XR
                var rig = FindFirstObjectByType<OVRCameraRig>();
                if (rig != null)
                {
                    headsetTransform = rig.centerEyeAnchor;
                    if (rigRoot == null)
                        rigRoot = rig.transform;
                    return;
                }
#endif
                var cam = Camera.main;
                if (cam != null)
                    headsetTransform = cam.transform;
            }

            if (rigRoot == null)
                rigRoot = transform;
        }
    }
}
