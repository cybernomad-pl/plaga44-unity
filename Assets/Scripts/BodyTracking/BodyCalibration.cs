// BodyCalibration.cs
// PLAGA '44 -- Player height calibration for body tracking.
//
// Reads head height from OVRCameraRig or Camera.main when the player
// presses the calibration button (both thumbsticks down by default).
// Adjusts the OVRCameraRig tracking origin Y offset so that the
// tracked skeleton aligns with the real player's height.
//
// Default assumed height: 1.8m (configurable via Inspector).
// Call CalibrateNow() from code or use the inspector button.

using UnityEngine;

namespace Plaga44.BodyTracking
{
    /// <summary>
    /// Calibrates body tracking height by measuring the actual head position
    /// and scaling the rig so the skeleton matches the player's real height.
    /// </summary>
    public class BodyCalibration : MonoBehaviour
    {
        private const string LOG = "[BodyCalibration]";

        [Header("Height Settings")]
        [Tooltip("Assumed player standing height in meters. Used as target height.")]
        [Range(1.4f, 2.2f)]
        public float targetHeight = 1.8f;

        [Tooltip("Minimum plausible head height to accept as a valid calibration sample (meters).")]
        public float minValidHeadHeight = 1.0f;

        [Tooltip("Maximum plausible head height to accept as a valid calibration sample (meters).")]
        public float maxValidHeadHeight = 2.5f;

        [Header("Input")]
        [Tooltip("Require both thumbsticks pressed simultaneously to trigger calibration.")]
        public bool calibrateOnBothThumbsticks = true;

        [Header("State")]
        [SerializeField] private bool _calibrated = false;
        [SerializeField] private float _measuredHeadHeight = 0f;
        [SerializeField] private float _appliedOffset = 0f;

        /// <summary>True after at least one successful calibration.</summary>
        public bool IsCalibrated => _calibrated;

        /// <summary>Last measured head height in meters.</summary>
        public float MeasuredHeadHeight => _measuredHeadHeight;

        // -- refs --

        private Transform _headTransform;

#if HAS_META_XR
        private OVRCameraRig _rig;
#endif

        // -- Unity lifecycle --

        private void Start()
        {
            FindHeadTransform();
        }

        private void Update()
        {
            // Don't trigger calibration while menus are open
            if (Plaga44.UI.VRMenuManager.MenuOpen || VRQualityMenu.MenuOpen) return;

            if (calibrateOnBothThumbsticks && BothThumbstickPressed())
                CalibrateNow();
        }

        // -- public API --

        /// <summary>
        /// Performs height calibration using the current head position.
        /// Adjusts OVRCameraRig tracking origin Y so the skeleton
        /// aligns with the configured targetHeight.
        /// </summary>
        public void CalibrateNow()
        {
            if (!FindHeadTransform())
            {
                Debug.LogWarning($"{LOG} CalibrateNow: head transform not found.");
                return;
            }

            float headY = _headTransform.position.y;

            if (headY < minValidHeadHeight || headY > maxValidHeadHeight)
            {
                Debug.LogWarning($"{LOG} Head height {headY:F2}m is outside valid range " +
                                 $"[{minValidHeadHeight}, {maxValidHeadHeight}]. Calibration skipped.");
                return;
            }

            _measuredHeadHeight = headY;

            // Calculate offset: how much do we need to shift the rig
            // so that measured head height equals targetHeight?
            float delta = targetHeight - _measuredHeadHeight;
            ApplyHeightOffset(delta);

            _calibrated = true;
            Debug.Log($"{LOG} Calibration complete. " +
                      $"Measured={_measuredHeadHeight:F2}m, Target={targetHeight:F2}m, " +
                      $"Offset={delta:+0.00;-0.00}m");
        }

        /// <summary>
        /// Resets the height offset to zero (removes calibration adjustment).
        /// </summary>
        public void ResetCalibration()
        {
            ApplyHeightOffset(0f);
            _calibrated = false;
            _appliedOffset = 0f;
            _measuredHeadHeight = 0f;
            Debug.Log($"{LOG} Calibration reset.");
        }

        // -- height offset --

        private void ApplyHeightOffset(float offset)
        {
#if HAS_META_XR
            if (_rig == null)
                _rig = FindFirstObjectByType<OVRCameraRig>();

            if (_rig != null)
            {
                Vector3 pos = _rig.transform.position;
                // Remove previous offset first, then apply new one.
                pos.y = pos.y - _appliedOffset + offset;
                _rig.transform.position = pos;
                _appliedOffset = offset;
                Debug.Log($"{LOG} OVRCameraRig Y offset: {offset:+0.000;-0.000}m");
                return;
            }
#endif
            // Fallback: adjust this GameObject's Y (e.g. XR Origin).
            Vector3 p = transform.position;
            p.y = p.y - _appliedOffset + offset;
            transform.position = p;
            _appliedOffset = offset;
            Debug.Log($"{LOG} Transform Y offset: {offset:+0.000;-0.000}m");
        }

        // -- helpers --

        private bool FindHeadTransform()
        {
            if (_headTransform != null) return true;

#if HAS_META_XR
            if (_rig == null)
                _rig = FindFirstObjectByType<OVRCameraRig>();

            if (_rig != null && _rig.centerEyeAnchor != null)
            {
                _headTransform = _rig.centerEyeAnchor;
                return true;
            }
#endif
            var cam = Camera.main;
            if (cam != null)
            {
                _headTransform = cam.transform;
                return true;
            }

            return false;
        }

        private bool BothThumbstickPressed()
        {
#if HAS_META_XR
            bool left  = OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick,  OVRInput.Controller.LTouch);
            bool right = OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch);
            // Trigger only when BOTH are pressed in the same frame or within a short window.
            return left && OVRInput.Get(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch)
                || right && OVRInput.Get(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch);
#else
            #error "HAS_META_XR not defined -- Quest project requires Meta XR SDK"
#endif
        }

#if UNITY_EDITOR
        // Allow triggering calibration from Inspector during play mode.
        [ContextMenu("Calibrate Now")]
        private void CalibrationContextMenu() => CalibrateNow();

        [ContextMenu("Reset Calibration")]
        private void ResetContextMenu() => ResetCalibration();
#endif
    }
}
