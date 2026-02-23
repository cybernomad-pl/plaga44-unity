// EyeTrackingManager.cs
// PLAGA '44 -- Eye tracking manager for Meta Quest Pro / Quest 3.
// Initializes OVREyeGaze and exposes public gaze API.
//
// Requires: com.meta.xr.sdk.core (OVREyeGaze) -- guarded by HAS_META_XR.
// Namespace: Plaga44.EyeTracking

using UnityEngine;

namespace Plaga44.EyeTracking
{
    /// <summary>
    /// Manages eye tracking via OVREyeGaze (Meta XR SDK).
    /// Attach to a persistent GameObject (e.g. XR Rig root).
    /// </summary>
    public class EyeTrackingManager : MonoBehaviour
    {
        private const string LOG = "[EyeTracking]";

        [Header("References")]
        [Tooltip("Main camera / center eye transform used as fallback origin.")]
        [SerializeField] private Transform _cameraRoot;

        [Header("Settings")]
        [Tooltip("Minimum confidence threshold to consider gaze valid.")]
        [SerializeField][Range(0f, 1f)] private float _minConfidence = 0.5f;

        [Tooltip("Lerp speed for smoothing combined gaze direction.")]
        [SerializeField][Range(1f, 60f)] private float _smoothSpeed = 20f;

        // ── State ────────────────────────────────────────────────────────

        private bool _eyeTrackingAvailable = false;
        private Vector3 _smoothedGazeDir = Vector3.forward;

#if HAS_META_XR
        private OVREyeGaze _leftEye;
        private OVREyeGaze _rightEye;
#endif

        // ── Unity lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            if (_cameraRoot == null)
            {
#if HAS_META_XR
                var rig = FindFirstObjectByType<OVRCameraRig>();
                if (rig != null)
                    _cameraRoot = rig.centerEyeAnchor;
#endif
                if (_cameraRoot == null && Camera.main != null)
                    _cameraRoot = Camera.main.transform;
            }

#if HAS_META_XR
            InitOVREyeGaze();
#else
            Debug.LogWarning($"{LOG} HAS_META_XR not defined -- eye tracking unavailable. " +
                             "Install Meta XR SDK and ensure the scripting define is set.");
#endif
        }

#if HAS_META_XR
        private void InitOVREyeGaze()
        {
            // OVREyeGaze components are expected on child anchors of OVRCameraRig.
            // If not present, create lightweight proxies.
            var eyes = GetComponentsInChildren<OVREyeGaze>(true);
            foreach (var eye in eyes)
            {
                if (eye.Eye == OVREyeGaze.EyeId.Left)  _leftEye  = eye;
                if (eye.Eye == OVREyeGaze.EyeId.Right) _rightEye = eye;
            }

            if (_leftEye == null && _rightEye == null)
            {
                // Try finding anywhere in scene
                var allEyes = FindObjectsByType<OVREyeGaze>(FindObjectsSortMode.None);
                foreach (var eye in allEyes)
                {
                    if (eye.Eye == OVREyeGaze.EyeId.Left  && _leftEye  == null) _leftEye  = eye;
                    if (eye.Eye == OVREyeGaze.EyeId.Right && _rightEye == null) _rightEye = eye;
                }
            }

            bool permissionGranted = OVRPlugin.eyeTrackingEnabled;
            _eyeTrackingAvailable = permissionGranted && (_leftEye != null || _rightEye != null);

            if (_eyeTrackingAvailable)
                Debug.Log($"{LOG} Eye tracking initialized. L={_leftEye != null} R={_rightEye != null}");
            else
                Debug.LogWarning($"{LOG} Eye tracking not available. " +
                                 $"Permission={permissionGranted}, L={_leftEye != null}, R={_rightEye != null}. " +
                                 "Ensure EyeTracking permission is set in Meta XR Project Setup Tool.");
        }
#endif

        private void Update()
        {
            if (!_eyeTrackingAvailable) return;

            Vector3 combinedDir = GetRawCombinedDirection();
            _smoothedGazeDir = Vector3.Slerp(
                _smoothedGazeDir,
                combinedDir,
                Time.deltaTime * _smoothSpeed);
        }

        // ── Public API ───────────────────────────────────────────────────

        /// <summary>
        /// Returns a world-space Ray for the requested eye.
        /// Falls back to center eye (averaged) if the specific eye is unavailable.
        /// </summary>
        /// <param name="eye">Which eye to query (Left, Right, or Center for combined).</param>
        public Ray GetGazeDirection(GazeEye eye = GazeEye.Center)
        {
            Vector3 origin = _cameraRoot != null ? _cameraRoot.position : Vector3.zero;

#if HAS_META_XR
            if (_eyeTrackingAvailable)
            {
                OVREyeGaze source = null;
                switch (eye)
                {
                    case GazeEye.Left:   source = _leftEye  ?? _rightEye; break;
                    case GazeEye.Right:  source = _rightEye ?? _leftEye;  break;
                    case GazeEye.Center: source = null; break;
                }

                if (source != null && source.EyeTrackingEnabled && source.Confidence >= _minConfidence)
                    return new Ray(source.transform.position, source.transform.forward);

                // Center: average valid eyes
                if (eye == GazeEye.Center)
                    return new Ray(origin, _smoothedGazeDir);
            }
#endif
            // Fallback: camera forward
            Vector3 fallbackDir = _cameraRoot != null ? _cameraRoot.forward : Vector3.forward;
            return new Ray(origin, fallbackDir);
        }

        /// <summary>
        /// Returns the confidence of the combined gaze [0..1].
        /// 0 if eye tracking is unavailable or below threshold.
        /// </summary>
        public float GetGazeConfidence()
        {
#if HAS_META_XR
            if (!_eyeTrackingAvailable) return 0f;

            float total = 0f;
            int count = 0;
            if (_leftEye  != null && _leftEye.EyeTrackingEnabled)  { total += _leftEye.Confidence;  count++; }
            if (_rightEye != null && _rightEye.EyeTrackingEnabled) { total += _rightEye.Confidence; count++; }
            return count > 0 ? total / count : 0f;
#else
            return 0f;
#endif
        }

        /// <summary>
        /// Returns true if the player is looking at the target within the given angular tolerance (degrees).
        /// Uses the combined smoothed gaze ray.
        /// </summary>
        public bool IsLookingAt(Transform target, float angleTolerance = 5f)
        {
            if (target == null) return false;

            Ray gaze = GetGazeDirection(GazeEye.Center);
            Vector3 toTarget = (target.position - gaze.origin).normalized;
            float angle = Vector3.Angle(gaze.direction, toTarget);
            return angle <= angleTolerance;
        }

        /// <summary>
        /// Whether eye tracking hardware and permissions are available on this device.
        /// </summary>
        public bool IsEyeTrackingAvailable => _eyeTrackingAvailable;

        // ── Helpers ──────────────────────────────────────────────────────

        private Vector3 GetRawCombinedDirection()
        {
#if HAS_META_XR
            Vector3 sum = Vector3.zero;
            int count = 0;

            if (_leftEye  != null && _leftEye.EyeTrackingEnabled  && _leftEye.Confidence  >= _minConfidence)
            { sum += _leftEye.transform.forward;  count++; }
            if (_rightEye != null && _rightEye.EyeTrackingEnabled && _rightEye.Confidence >= _minConfidence)
            { sum += _rightEye.transform.forward; count++; }

            if (count > 0)
                return sum.normalized;
#endif
            return _cameraRoot != null ? _cameraRoot.forward : Vector3.forward;
        }
    }

    /// <summary>Which eye to use for gaze queries.</summary>
    public enum GazeEye
    {
        Left,
        Right,
        Center
    }
}
