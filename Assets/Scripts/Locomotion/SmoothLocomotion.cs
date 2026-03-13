using UnityEngine;

#if HAS_META_XR
// OVRInput is available from Meta XR SDK Core.
#elif UNITY_XR
using UnityEngine.XR;
#endif

namespace Plaga44.Locomotion
{
    /// <summary>
    /// Thumbstick-driven smooth locomotion.
    ///
    /// Movement direction is relative to headset forward (horizontal plane only).
    /// Left thumbstick: move forward/back/strafe.
    /// Right thumbstick: turn (snap or smooth, configured by <see cref="snapTurns"/>).
    ///
    /// Requires the rig root (OVRCameraRig / XROrigin) as the transform that gets moved.
    /// The headset camera transform must be reachable via <see cref="_headTransform"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class SmoothLocomotion : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector fields
        // -------------------------------------------------------------------------

        [Header("Speed")]
        [Tooltip("Walk speed in metres per second.")]
        public float moveSpeed = 2.5f;

        [Tooltip("Strafe speed multiplier relative to forward speed.")]
        [Range(0.1f, 1f)]
        public float strafeFactor = 0.8f;

        [Header("Turn")]
        [Tooltip("True = snap turn by snapAngle. False = smooth turn at turnSpeed deg/sec.")]
        public bool snapTurns = true;

        [Tooltip("Degrees per snap turn step.")]
        public float snapAngle = 45f;

        [Tooltip("Smooth turn speed in degrees per second (used when snapTurns = false).")]
        public float turnSpeed = 90f;

        [Tooltip("Deadzone for thumbstick turn input to prevent drift.")]
        [Range(0.1f, 0.9f)]
        public float turnDeadzone = 0.5f;

        [Header("Head Reference")]
        [Tooltip("Camera / center-eye transform. Populated automatically from OVRCameraRig or Camera.main.")]
        [SerializeField] private Transform _headTransform;

        // -------------------------------------------------------------------------
        // Runtime state
        // -------------------------------------------------------------------------

        private float _snapCooldown;
        private const float SnapCooldownDuration = 0.25f;

        // Smooth-turn state
        private bool _turnReleased = true;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>Current normalised speed (0-1). Used by ComfortVignette.</summary>
        public float NormalisedSpeed { get; private set; }

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            if (_headTransform == null)
                _headTransform = ResolveHeadTransform();
        }

        private void Update()
        {
            if (_headTransform == null) return;

            Vector2 moveInput = GetMoveInput();
            Vector2 turnInput = GetTurnInput();

            HandleMovement(moveInput);
            HandleTurn(turnInput);
        }

        // -------------------------------------------------------------------------
        // Input abstraction
        // -------------------------------------------------------------------------

        private Vector2 GetMoveInput()
        {
#if HAS_META_XR
            return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
#else
            // Unity Input System / legacy fallback.
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            return new Vector2(h, v);
#endif
        }

        private Vector2 GetTurnInput()
        {
#if HAS_META_XR
            return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
#else
            float turn = Input.GetAxis("RightStickHorizontal");
            return new Vector2(turn, 0f);
#endif
        }

        // -------------------------------------------------------------------------
        // Movement
        // -------------------------------------------------------------------------

        private void HandleMovement(Vector2 input)
        {
            if (input.sqrMagnitude < 0.01f)
            {
                NormalisedSpeed = 0f;
                return;
            }

            // Flatten head forward onto horizontal plane.
            Vector3 fwd = _headTransform.forward;
            fwd.y = 0f;
            fwd.Normalize();

            Vector3 right = _headTransform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 move = (fwd * input.y) + (right * (input.x * strafeFactor));
            move *= moveSpeed * Time.deltaTime;
            move.y = 0f;

            transform.position += move;

            // Report normalised speed for vignette (clamp input magnitude to 1).
            NormalisedSpeed = Mathf.Clamp01(input.magnitude);
        }

        // -------------------------------------------------------------------------
        // Turning
        // -------------------------------------------------------------------------

        private void HandleTurn(Vector2 input)
        {
            if (snapTurns)
                HandleSnapTurn(input);
            else
                HandleSmoothTurn(input);
        }

        private void HandleSnapTurn(Vector2 input)
        {
            _snapCooldown -= Time.deltaTime;

            if (Mathf.Abs(input.x) > turnDeadzone && _snapCooldown <= 0f)
            {
                float dir = Mathf.Sign(input.x);
                transform.Rotate(0f, dir * snapAngle, 0f, Space.World);
                _snapCooldown = SnapCooldownDuration;
            }
        }

        private void HandleSmoothTurn(Vector2 input)
        {
            if (Mathf.Abs(input.x) > turnDeadzone)
            {
                float rotation = input.x * turnSpeed * Time.deltaTime;
                transform.Rotate(0f, rotation, 0f, Space.World);
                _turnReleased = false;
            }
            else
            {
                _turnReleased = true;
            }
        }

        // -------------------------------------------------------------------------
        // Head transform resolution
        // -------------------------------------------------------------------------

        private Transform ResolveHeadTransform()
        {
#if HAS_META_XR
            // OVRCameraRig stores CenterEyeAnchor under TrackingSpace.
            var tracking = transform.Find("TrackingSpace");
            if (tracking != null)
            {
                var eye = tracking.Find("CenterEyeAnchor");
                if (eye != null) return eye;
            }
#endif
            // Fallback: use the main camera.
            if (Camera.main != null)
                return Camera.main.transform;

            Debug.LogWarning("[SmoothLocomotion] Could not find head transform. Assign _headTransform manually.");
            return null;
        }
    }
}
