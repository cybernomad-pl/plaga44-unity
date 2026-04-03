// PeripheralThreat.cs
// PLAGA '44 -- Horror gameplay mechanic.
// The threat moves ONLY when it is in peripheral vision.
// When the player looks directly at it, it freezes completely.
//
// Peripheral zone = gaze angle > _directGazeAngle (default 15 deg).
// The object re-activates movement as soon as the player looks away.
//
// Namespace: Plaga44.EyeTracking

using System.Collections;
using UnityEngine;

namespace Plaga44.EyeTracking
{
    /// <summary>
    /// Attach to a horror entity. It stalks the player in peripheral vision
    /// but freezes instantly when looked at directly.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PeripheralThreat : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Header("Gaze Thresholds")]
        [Tooltip("Angle (degrees) from gaze center within which the threat is considered 'directly looked at'.")]
        [SerializeField][Range(1f, 45f)] private float _directGazeAngle = 15f;

        [Tooltip("When eye tracking is unavailable, use this angle for camera-forward fallback.")]
        [SerializeField][Range(1f, 45f)] private float _fallbackGazeAngle = 20f;

        [Tooltip("Hysteresis: extra degrees added before resuming movement after a direct gaze. " +
                 "Prevents flicker at the boundary.")]
        [SerializeField][Range(0f, 15f)] private float _hysteresisAngle = 5f;

        [Header("Movement")]
        [Tooltip("Target to move toward (usually the player / camera root).")]
        [SerializeField] private Transform _target;

        [Tooltip("Movement speed when in peripheral vision.")]
        [SerializeField][Range(0f, 20f)] private float _moveSpeed = 1.2f;

        [Tooltip("Acceleration applied to Rigidbody (ForceMode.Acceleration).")]
        [SerializeField] private bool _usePhysicsMovement = false;

        [Tooltip("How quickly the threat can rotate to face the target.")]
        [SerializeField][Range(0f, 720f)] private float _turnSpeed = 120f;

        [Header("Freeze Behaviour")]
        [Tooltip("Instantly snap velocity to zero when frozen.")]
        [SerializeField] private bool _snapVelocityOnFreeze = true;

        [Tooltip("Optional Animator trigger name to fire when the threat freezes.")]
        [SerializeField] private string _freezeAnimTrigger = "Freeze";

        [Tooltip("Optional Animator trigger name to fire when movement resumes.")]
        [SerializeField] private string _resumeAnimTrigger = "Resume";

        [Header("Audio")]
        [Tooltip("Sound played when the threat freezes (player looks at it).")]
        [SerializeField] private AudioClip _freezeSound;

        [Tooltip("Sound played when movement resumes (player looks away).")]
        [SerializeField] private AudioClip _resumeSound;

        [Header("Debug")]
        [SerializeField] private bool _drawDebugGizmos = true;

        // ── State ─────────────────────────────────────────────────────────

        private EyeTrackingManager _manager;
        private Rigidbody          _rigidbody;
        private Animator           _animator;
        private AudioSource        _audio;

        private bool _isFrozen = false;
        private bool _wasDirectlyGazed = false;

        // ── Events ───────────────────────────────────────────────────────

        /// <summary>Fired when the player directly gazes at the threat (it freezes).</summary>
        public System.Action OnFreeze;

        /// <summary>Fired when the player looks away and the threat resumes moving.</summary>
        public System.Action OnResume;

        // ── Lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _animator  = GetComponent<Animator>();
            _audio     = GetComponent<AudioSource>();

            _rigidbody.useGravity  = false;
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotation;

            // Auto-find target if not set
            if (_target == null)
            {
#if HAS_META_XR
                var rig = FindFirstObjectByType<OVRCameraRig>();
                if (rig != null) { _target = rig.centerEyeAnchor; }
#endif
                if (_target == null && Camera.main != null)
                    _target = Camera.main.transform;
            }
        }

        private void Start()
        {
            _manager = FindFirstObjectByType<EyeTrackingManager>();

            if (_manager == null)
                Debug.LogWarning($"[PeripheralThreat] '{name}': No EyeTrackingManager -- " +
                                 "falling back to camera forward for gaze detection.");
        }

        private void FixedUpdate()
        {
            if (_isFrozen || _target == null) return;

            if (_usePhysicsMovement)
                MovePhysics();
            else
                MoveKinematic();
        }

        private void Update()
        {
            bool directlyGazed = CheckDirectGaze();

            // Rising edge: player STARTS looking at threat
            if (directlyGazed && !_wasDirectlyGazed)
            {
                _wasDirectlyGazed = true;
                Freeze();
            }
            // Falling edge: apply hysteresis -- only unfreeze when gaze moves far enough away
            else if (_wasDirectlyGazed && !directlyGazed)
            {
                bool stilTooClose = CheckDirectGaze(_directGazeAngle + _hysteresisAngle);
                if (!stilTooClose)
                {
                    _wasDirectlyGazed = false;
                    Unfreeze();
                }
            }
        }

        // ── Freeze / Unfreeze ────────────────────────────────────────────

        private void Freeze()
        {
            if (_isFrozen) return;
            _isFrozen = true;

            if (_snapVelocityOnFreeze)
                _rigidbody.linearVelocity = Vector3.zero;

            if (_animator != null && !string.IsNullOrEmpty(_freezeAnimTrigger))
                _animator.SetTrigger(_freezeAnimTrigger);

            PlaySound(_freezeSound);
            OnFreeze?.Invoke();
        }

        private void Unfreeze()
        {
            if (!_isFrozen) return;
            _isFrozen = false;

            if (_animator != null && !string.IsNullOrEmpty(_resumeAnimTrigger))
                _animator.SetTrigger(_resumeAnimTrigger);

            PlaySound(_resumeSound);
            OnResume?.Invoke();
        }

        // ── Movement ─────────────────────────────────────────────────────

        private void MoveKinematic()
        {
            Vector3 toTarget = (_target.position - transform.position);
            Vector3 dir      = toTarget.normalized;

            // Rotate toward target
            if (toTarget.sqrMagnitude > 0.01f)
            {
                Quaternion lookRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, lookRot, _turnSpeed * Time.deltaTime);
            }

            // Move
            _rigidbody.MovePosition(transform.position + dir * _moveSpeed * Time.fixedDeltaTime);
        }

        private void MovePhysics()
        {
            Vector3 toTarget = (_target.position - transform.position).normalized;
            _rigidbody.AddForce(toTarget * _moveSpeed, ForceMode.Acceleration);

            // Rotate toward target
            if (toTarget.sqrMagnitude > 0.01f)
            {
                Quaternion lookRot = Quaternion.LookRotation(toTarget);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, lookRot, _turnSpeed * Time.deltaTime);
            }
        }

        // ── Gaze check ───────────────────────────────────────────────────

        /// <summary>
        /// Returns true if this threat is within the direct gaze cone.
        /// </summary>
        private bool CheckDirectGaze(float overrideAngle = -1f)
        {
            float threshold = overrideAngle >= 0f ? overrideAngle : _directGazeAngle;

            Ray gazeRay;

            if (_manager != null)
            {
                gazeRay = _manager.GetGazeDirection(GazeEye.Center);

                // If confidence is too low, use fallback angle (wider cone, safer for horror)
                float conf = _manager.GetGazeConfidence();
                if (conf < 0.4f)
                    threshold = overrideAngle >= 0f ? overrideAngle : _fallbackGazeAngle;
            }
            else
            {
                // Fallback: camera forward
                var cam = Camera.main;
                if (cam == null) return false;
                gazeRay   = new Ray(cam.transform.position, cam.transform.forward);
                threshold = overrideAngle >= 0f ? overrideAngle : _fallbackGazeAngle;
            }

            Vector3 toSelf = (transform.position - gazeRay.origin).normalized;
            float   angle  = Vector3.Angle(gazeRay.direction, toSelf);
            return angle <= threshold;
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private void PlaySound(AudioClip clip)
        {
            if (_audio == null || clip == null) return;
            _audio.PlayOneShot(clip);
        }

        // ── Gizmos ───────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_drawDebugGizmos) return;

            // Draw freeze cone from target's perspective
            Transform cam = _target;
            if (cam == null) cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null) return;

            // Direction from camera to this threat
            Vector3 toSelf = (transform.position - cam.position).normalized;
            float   dist   = Vector3.Distance(cam.position, transform.position);

            // Direct gaze cone (red = frozen zone)
            UnityEditor.Handles.color = _isFrozen
                ? new Color(1f, 0f, 0f, 0.25f)
                : new Color(1f, 0.5f, 0f, 0.15f);

            UnityEditor.Handles.DrawSolidArc(
                cam.position, cam.up,
                Quaternion.AngleAxis(-_directGazeAngle, cam.up) * cam.forward,
                _directGazeAngle * 2f, dist);

            // Line to threat
            Gizmos.color = _isFrozen ? Color.red : Color.yellow;
            Gizmos.DrawLine(cam.position, transform.position);
            Gizmos.DrawWireSphere(transform.position, 0.15f);

            // State label
            UnityEditor.Handles.color = _isFrozen ? Color.red : Color.green;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.3f,
                _isFrozen ? "FROZEN (direct gaze)" : "MOVING (peripheral)");
        }
#endif
    }
}
