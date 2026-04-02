// AUTO-DISABLED: PLAGA44_FULL_SDK
#if PLAGA44_FULL_SDK
// CrouchDetector.cs
// PLAGA '44 -- Detects physical player crouching in real life by tracking
// the headset Y position relative to a calibrated standing height.
// Useful for cover mechanics: peek over walls, duck under obstacles.
//
// Call Calibrate() after the player stands fully upright to set standing height.
// Or let the component auto-calibrate on Start using the initial headset height.
//
// Namespace: Plaga44.IK

using UnityEngine;
using UnityEngine.Events;

namespace Plaga44.IK
{
    /// <summary>
    /// Detects physical crouch by comparing real-world headset height
    /// against a calibrated standing height. Fires OnCrouch/OnStand events.
    /// </summary>
    public class CrouchDetector : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("Headset Reference")]
        [Tooltip("The CenterEyeAnchor or Camera.main transform. Auto-found if null.")]
        public Transform headsetTransform;

        [Header("Thresholds")]
        [Tooltip("Player is considered crouching when headset drops this many metres below standing height.")]
        [Range(0.05f, 0.6f)]
        public float crouchThreshold = 0.25f;

        [Tooltip("Hysteresis: player must rise this far above crouchThreshold before OnStand fires. " +
                 "Prevents rapid toggling when head hovers at threshold.")]
        [Range(0f, 0.15f)]
        public float standHysteresis = 0.05f;

        [Header("Calibration")]
        [Tooltip("Standing height is set to this value on Start if autoCalibrate is false. " +
                 "Typical range for average adult in headset: 1.55 -- 1.85 m.")]
        [Range(1.2f, 2.2f)]
        public float defaultStandingHeight = 1.75f;

        [Tooltip("If true, standing height is read from the headset's Y position on Start. " +
                 "The player should be standing upright when the scene loads.")]
        public bool autoCalibrate = true;

        [Header("Events")]
        [Tooltip("Fired once when the player enters a crouch.")]
        public UnityEvent OnCrouch;

        [Tooltip("Fired once when the player stands up from a crouch.")]
        public UnityEvent OnStand;

        [Tooltip("Fired every frame with current crouch depth (0 = standing, 1 = fully crouched to floor).")]
        public UnityEvent<float> OnCrouchDepthChanged;

        // ── Properties ───────────────────────────────────────────────────

        /// <summary>Current calibrated standing height in world-space Y.</summary>
        public float StandingHeight { get; private set; }

        /// <summary>True if the player is currently crouching.</summary>
        public bool IsCrouching { get; private set; }

        /// <summary>Current headset Y position.</summary>
        public float CurrentHeadY => headsetTransform != null ? headsetTransform.position.y : 0f;

        /// <summary>
        /// Normalised crouch depth: 0 = standing, 1 = crouched crouchThreshold metres below standing.
        /// Values above 1 are clamped.
        /// </summary>
        public float CrouchDepth
        {
            get
            {
                float drop = StandingHeight - CurrentHeadY;
                return Mathf.Clamp01(drop / crouchThreshold);
            }
        }

        // ── Private ──────────────────────────────────────────────────────

        private float _prevDepth;

        // ── Unity lifecycle ──────────────────────────────────────────────

        private void Start()
        {
            ResolveHeadset();

            if (autoCalibrate)
                Calibrate();
            else
                StandingHeight = defaultStandingHeight;
        }

        private void Update()
        {
            if (headsetTransform == null)
            {
                ResolveHeadset();
                return;
            }

            float drop = StandingHeight - CurrentHeadY;
            bool wasCrouching = IsCrouching;

            if (!IsCrouching && drop >= crouchThreshold)
            {
                IsCrouching = true;
                OnCrouch?.Invoke();
            }
            else if (IsCrouching && drop < (crouchThreshold - standHysteresis))
            {
                IsCrouching = false;
                OnStand?.Invoke();
            }

            float depth = CrouchDepth;
            if (!Mathf.Approximately(depth, _prevDepth))
            {
                OnCrouchDepthChanged?.Invoke(depth);
                _prevDepth = depth;
            }
        }

        // ── Public API ───────────────────────────────────────────────────

        /// <summary>
        /// Sets standing height to current headset Y. Call while player is fully upright.
        /// </summary>
        public void Calibrate()
        {
            if (headsetTransform == null)
                ResolveHeadset();

            StandingHeight = headsetTransform != null ? headsetTransform.position.y : defaultStandingHeight;
            Debug.Log($"[CrouchDetector] Calibrated standing height: {StandingHeight:F3} m");
        }

        /// <summary>
        /// Manually override the standing height (e.g., restore from saved profile).
        /// </summary>
        public void SetStandingHeight(float height)
        {
            StandingHeight = height;
        }

        // ── Internal ─────────────────────────────────────────────────────

        private void ResolveHeadset()
        {
            if (headsetTransform != null) return;

#if HAS_META_XR
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig != null)
            {
                headsetTransform = rig.centerEyeAnchor;
                return;
            }
#endif
            var cam = Camera.main;
            if (cam != null)
                headsetTransform = cam.transform;
        }
    }
}
#endif // PLAGA44_FULL_SDK
