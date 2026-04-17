// =============================================================================
// LocomotionController.cs
// CYBERNOMAD -- Glowny kontroler lokomocji gracza w PLAGA '44.
//
// INPUT (lewy thumbstick = ruch, prawy thumbstick Y = fly/stance):
//   L thumbstick: ruch head-relative (CharacterController.Move)
//   R thumbstick UP: fly up (accelerating, gravity suspended)
//   R thumbstick DOWN short: toggle CROUCH
//   R thumbstick DOWN long hold: go to PRONE
//   From crouch/prone, flick UP: stand up
//   R thumbstick X: smooth turn (handled by SmoothTurnController, no conflict)
//
// STANCE: STAND -> CROUCH -> PRONE | FLOATING (auto-set while flying)
//   CC height + center change for collisions.
//   TrackingSpace Y offset for visual camera drop (VR camera follows headset,
//   so we must push the tracking origin DOWN to simulate crouching).
//   FLOATING is set automatically when entering flight and cleared on landing.
// =============================================================================

using UnityEngine;

namespace Plaga44.Locomotion
{
    public enum Stance { Stand, Crouch, Prone, Floating }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class LocomotionController : MonoBehaviour
    {
        // =====================================================================
        // Inspector fields
        // =====================================================================

        [Header("Movement")]
        public float moveSpeed = 2.5f;

        [Range(0.1f, 1f)]
        public float strafeFactor = 0.8f;

        [Header("Sprint (L thumbstick click)")]
        [Tooltip("Sprint speed multiplier. 2.0 = 2x walk speed when sprinting.")]
        public float sprintMultiplier = 2f;

        [Tooltip("True if sprint toggle is currently active. Public for UI.")]
        public bool sprintActive = false;

        [Header("Head Reference")]
        [SerializeField] private Transform _headTransform;

        [Header("Fly (R thumbstick UP)")]
        [Tooltip("Fly acceleration (m/s^2). Speed increases while held.")]
        public float flyAcceleration = 10f;

        [Tooltip("Max fly speed cap (m/s). Default 25 (~90 km/h).")]
        public float flyMaxSpeed = 25f;

        [Header("Stance (R thumbstick DOWN)")]
        [Tooltip("CC height when standing (captured from CC at Awake).")]
        public float standHeight = 1.8f;

        [Tooltip("CC height when crouching.")]
        public float crouchHeight = 1.0f;

        [Tooltip("CC height when prone.")]
        public float proneHeight = 0.5f;


        // =====================================================================
        // Runtime state
        // =====================================================================

        private CharacterController _cc;
        private Transform _trackingSpace; // OVRCameraRig/TrackingSpace -- offset for visual crouch
        private float _verticalVelocity;
        private float _trackingSpaceBaseY; // original TrackingSpace local Y

        // Fly state
        private enum FlyState { Grounded, Ascending, Hovering }
        private FlyState _flyState = FlyState.Grounded;
        private float _flySpeed;
        private float _hoverDrift;      // current hover vertical drift
        private float _hoverDriftTarget; // target drift (random, changes periodically)
        private float _hoverNextDriftChange;

        // Stance state
        private Stance _currentStance = Stance.Stand;

        // Thresholds
        // Issue #139: Borys reports 'R thumbstick UP nie łapie zawsze że jest wciśnięty'.
        // Lowered from 0.25 -> 0.15 (lighter push required to start fly).
        // Hysteresis: separate higher threshold for initial latch, lower for release (prevents flicker).
        private const float StickDownThreshold = 0.3f;
        private const float StickUpThreshold = 0.15f;     // was 0.25 -- easier to trigger fly
        private const float StickUpReleaseThreshold = 0.05f; // hysteresis below = stop fly
        private const float GroundedPullDown = -2f;
        private const float InputDeadZoneSqr = 0.01f;
        private const float GroundedLogThrottleSec = 0.5f;

        // =====================================================================
        // Public properties
        // =====================================================================

        public float NormalisedSpeed { get; private set; }

        public float VerticalVelocity
        {
            get => _verticalVelocity;
            set => _verticalVelocity = value;
        }

        public bool IsGrounded => _cc != null && _cc.isGrounded;
        public bool IsFlying => _flyState != FlyState.Grounded;
        public Stance CurrentStance => _currentStance;
        public CharacterController CharController => _cc;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private const string LOG = "[PLAGA44][Locomotion]";

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (_cc != null) standHeight = _cc.height;

            if (_headTransform == null)
                _headTransform = ResolveHeadTransform();

            // Cache TrackingSpace for visual crouch offset
            _trackingSpace = transform.Find("TrackingSpace");
            _trackingSpaceBaseY = _trackingSpace != null ? _trackingSpace.localPosition.y : 0f;

            Debug.Log($"{LOG} Awake: CC={_cc != null} h={_cc?.height} head={_headTransform?.name ?? "NULL"} tracking={_trackingSpace != null}");
        }

        private void Start()
        {
            // Force CC back to standHeight -- PlayerPrefs might have persisted a modified value
            // from CHAR CTRL slider. Stance system manages CC height, not the slider.
            if (_cc != null)
            {
                _cc.height = standHeight;
                _cc.center = new Vector3(_cc.center.x, standHeight * 0.5f, _cc.center.z);
            }
            _currentStance = Stance.Stand;
            _targetCCHeight = standHeight;
            _targetTrackingY = _trackingSpaceBaseY;
            Debug.Log($"{LOG} Start: CC reset to standHeight={standHeight}");
        }

        private void OnEnable()
        {
            Debug.Log($"{LOG} OnEnable: speed={moveSpeed} strafe={strafeFactor} stance={_currentStance}");
        }

        private void OnDisable()
        {
            Debug.Log($"{LOG} OnDisable");
        }

        private bool _wasGrounded = true;
        private float _lastGroundedLogTime = -1f;

        private float _dt; // cached per frame

        private void Update()
        {
            if (!GameState.CanMove) return;
            if (_headTransform == null) return;
            _dt = Time.deltaTime;

            // Sprint toggle: L thumbstick click (Issue #142)
            if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick))
            {
                sprintActive = !sprintActive;
                Debug.Log($"{LOG} Sprint: {(sprintActive ? "ON" : "OFF")}");
            }

            Vector2 moveInput = GetMoveInput();
            // Prone blocks horizontal movement -- must stand up first
            Vector3 horizontalMove = _currentStance == Stance.Prone
                ? Vector3.zero
                : CalculateHeadRelativeMovement(moveInput);

            float rightY = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).y;
            UpdateFly(rightY);
            UpdateStance(rightY);

            // Safety: force FLOATING if flying with wrong stance
            if (_flyState != FlyState.Grounded && _currentStance != Stance.Floating)
                SetStance(Stance.Floating);

            if (_flyState == FlyState.Grounded) ApplyGravity();

            ApplyMove(horizontalMove);
            NormalisedSpeed = Mathf.Clamp01(moveInput.magnitude);
            LogGroundedChangesThrottled();
        }

        // =====================================================================
        // Fly system (R thumbstick UP = ascend, release = hover, DOWN = drop)
        //
        //  GROUNDED --[stick UP]--> ASCENDING --[release]--> HOVERING
        //  HOVERING --[stick DOWN]--> GROUNDED (gravity returns)
        //  HOVERING --[stick UP]--> ASCENDING (boost again)
        //  ASCENDING/HOVERING --[land on ground]--> GROUNDED
        // =====================================================================

        // Issue #140: Borys reports 'dryftu hovera naprawdę w ogóle nie czuć'.
        // Bumped amplitude 5x (was ±0.3, now ±1.5 m/s). Faster target changes for livelier feel.
        private const float HoverDriftMin = -1.5f;  // pronounced sink
        private const float HoverDriftMax = 1.5f;   // pronounced rise
        private const float HoverDriftChangeMin = 0.5f;
        private const float HoverDriftChangeMax = 2f;
        private const float HoverDriftLerp = 2.5f;

        private void UpdateFly(float rightY)
        {
            switch (_flyState)
            {
                case FlyState.Grounded:
                    if (rightY > StickUpThreshold && _currentStance == Stance.Stand)
                    {
                        _flyState = FlyState.Ascending;
                        _flySpeed = 0f;
                        SetStance(Stance.Floating);
                        Debug.Log($"{LOG} Fly: ASCENDING");
                    }
                    break;

                case FlyState.Ascending:
                    // Hysteresis: once ascending, use LOWER threshold to keep ascending.
                    // Prevents flicker when stick is near the UP threshold (issue #139).
                    if (rightY > StickUpReleaseThreshold)
                    {
                        // Accelerating upward
                        _flySpeed += flyAcceleration * _dt;
                        _flySpeed = Mathf.Min(_flySpeed, flyMaxSpeed);
                        _verticalVelocity = _flySpeed;
                    }
                    else
                    {
                        // Released stick -- transition to hover
                        _flyState = FlyState.Hovering;
                        _flySpeed = 0f;
                        _hoverDrift = 0f;
                        _hoverDriftTarget = Random.Range(HoverDriftMin, HoverDriftMax);
                        _hoverNextDriftChange = Time.unscaledTime + Random.Range(HoverDriftChangeMin, HoverDriftChangeMax);
                        _verticalVelocity = 0f;
                        Debug.Log($"{LOG} Fly: HOVERING");
                    }
                    // Landing check
                    if (IsGrounded) { EndFlight(); break; }
                    break;

                case FlyState.Hovering:
                    if (rightY > StickUpThreshold)
                    {
                        // R stick UP -> normal fly up (NOT boost). Start from 0, accelerate normally.
                        // Issue #162: 'jak jesteś hover to góra to góra i tyle' -- no special boost treatment.
                        _flyState = FlyState.Ascending;
                        _flySpeed = 0f;
                        Debug.Log($"{LOG} Fly: ASCENDING (from hover, normal accel)");
                    }
                    else if (rightY < -StickDownThreshold)
                    {
                        // R stick DOWN -> end flight, gravity takes over (player falls)
                        EndFlight();
                        Debug.Log($"{LOG} Fly: DROPPING (R stick DOWN in hover)");
                    }
                    else
                    {
                        // Floating -- gentle random drift
                        UpdateHoverDrift();
                        _verticalVelocity = _hoverDrift;
                    }
                    // Landing check
                    if (IsGrounded) { EndFlight(); break; }
                    break;
            }
        }

        private void UpdateHoverDrift()
        {
            if (Time.unscaledTime >= _hoverNextDriftChange)
            {
                _hoverDriftTarget = Random.Range(HoverDriftMin, HoverDriftMax);
                _hoverNextDriftChange = Time.unscaledTime + Random.Range(HoverDriftChangeMin, HoverDriftChangeMax);
            }
            _hoverDrift = Mathf.Lerp(_hoverDrift, _hoverDriftTarget, HoverDriftLerp * _dt);
        }

        private void EndFlight()
        {
            _flyState = FlyState.Grounded;
            _flySpeed = 0f;
            _hoverDrift = 0f;
            _verticalVelocity = 0f;
            SetStance(Stance.Stand);
        }

        // =====================================================================
        // Stance system (R thumbstick DOWN = cycle down, UP = cycle up)
        //   STAND -> CROUCH -> PRONE (stick DOWN, one tap per step)
        //   PRONE -> CROUCH -> STAND (stick UP, one tap per step)
        //   Transitions are SMOOTH (lerp CC height + TrackingSpace)
        //   Prone BLOCKS movement (must stand to move)
        // =====================================================================

        [Tooltip("Stance transition speed (height lerp per second).")]
        public float stanceTransitionSpeed = 3f;

        private float _targetCCHeight;
        private float _targetTrackingY;
        private bool _stanceDownPressed;
        private bool _stanceUpPressed;

        private void UpdateStance(float rightY)
        {
            if (_flyState != FlyState.Grounded) return;

            // Edge detection -- trigger ONCE per stick push
            bool downNow = rightY < -StickDownThreshold;
            bool upNow = rightY > StickUpThreshold;

            if (downNow && !_stanceDownPressed)
            {
                // Cycle DOWN: Stand -> Crouch -> Prone
                if (_currentStance == Stance.Stand) SetStance(Stance.Crouch);
                else if (_currentStance == Stance.Crouch) SetStance(Stance.Prone);
            }
            if (upNow && !_stanceUpPressed)
            {
                // Cycle UP: Prone -> Crouch -> Stand
                if (_currentStance == Stance.Prone) SetStance(Stance.Crouch);
                else if (_currentStance == Stance.Crouch) SetStance(Stance.Stand);
            }
            _stanceDownPressed = downNow;
            _stanceUpPressed = upNow;

            // Smooth transition
            LerpStance();
        }

        public void SetStance(Stance stance)
        {
            if (_currentStance == stance) return;
            var prev = _currentStance;
            _currentStance = stance;

            _targetCCHeight = stance switch
            {
                Stance.Crouch => crouchHeight,
                Stance.Prone => proneHeight,
                Stance.Floating => standHeight,
                _ => standHeight
            };

            float heightDiff = standHeight - _targetCCHeight;
            _targetTrackingY = _trackingSpaceBaseY - heightDiff;

            Debug.Log($"{LOG} Stance: {prev} -> {stance}, targetH={_targetCCHeight}");
        }

        private void LerpStance()
        {
            if (_cc == null) return;

            float speed = stanceTransitionSpeed * _dt;

            // Lerp CC height
            float currentH = _cc.height;
            if (!Mathf.Approximately(currentH, _targetCCHeight))
            {
                float newH = Mathf.MoveTowards(currentH, _targetCCHeight, speed);
                _cc.height = newH;
                _cc.center = new Vector3(_cc.center.x, newH * 0.5f, _cc.center.z);
            }

            // Lerp TrackingSpace Y
            if (_trackingSpace != null)
            {
                Vector3 pos = _trackingSpace.localPosition;
                if (!Mathf.Approximately(pos.y, _targetTrackingY))
                {
                    pos.y = Mathf.MoveTowards(pos.y, _targetTrackingY, speed);
                    _trackingSpace.localPosition = pos;
                }
            }
        }

        // =====================================================================
        // Movement
        // =====================================================================

        private void ApplyMove(Vector3 horizontalMove)
        {
            Vector3 finalMove = horizontalMove + (Vector3.up * _verticalVelocity * _dt);
            _cc.Move(finalMove);
        }

        private Vector2 GetMoveInput()
        {
            return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
        }

        private Vector3 CalculateHeadRelativeMovement(Vector2 input)
        {
            if (input.sqrMagnitude < InputDeadZoneSqr) return Vector3.zero;

            Vector3 fwd = _headTransform.forward;
            fwd.y = 0f; fwd.Normalize();
            Vector3 right = _headTransform.right;
            right.y = 0f; right.Normalize();

            Vector3 move = (fwd * input.y) + (right * input.x * strafeFactor);
            // Sprint multiplier applies when sprintActive AND player actually moving (issue #142)
            float speedMul = sprintActive ? sprintMultiplier : 1f;
            move *= moveSpeed * speedMul * _dt;
            return move;
        }

        // =====================================================================
        // Gravity
        // =====================================================================

        private void ApplyGravity()
        {
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = GroundedPullDown;
            else
                _verticalVelocity += Physics.gravity.y * _dt;
        }

        // =====================================================================
        // Logging
        // =====================================================================

        private void LogGroundedChangesThrottled()
        {
            if (_cc.isGrounded == _wasGrounded) return;
            if (Time.unscaledTime - _lastGroundedLogTime > GroundedLogThrottleSec)
            {
                Debug.Log($"{LOG} Grounded: {_wasGrounded} -> {_cc.isGrounded}, vVel={_verticalVelocity:F2}, stance={_currentStance}");
                _lastGroundedLogTime = Time.unscaledTime;
            }
            _wasGrounded = _cc.isGrounded;
        }

        // =====================================================================
        // Head transform resolution
        // =====================================================================

        private Transform ResolveHeadTransform()
        {
            var tracking = transform.Find("TrackingSpace");
            if (tracking != null)
            {
                var eye = tracking.Find("CenterEyeAnchor");
                if (eye != null) return eye;
            }
            if (Camera.main != null)
            {
                Debug.Log($"{LOG} ResolveHead: fallback Camera.main ({Camera.main.name})");
                return Camera.main.transform;
            }
            Debug.LogError($"{LOG} ResolveHead: BRAK KAMERY!");
            return null;
        }
    }
}
