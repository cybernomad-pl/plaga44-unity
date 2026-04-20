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
    public class LocomotionController : MonoBehaviour, IPlayerMotionSource
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

        [Tooltip("Sprint ramp speed (1/speed = seconds to reach full sprint). 4.0 = 0.25s. Issue #189.")]
        public float sprintRampSpeed = 4f;

        // Current sprint amount 0..1 (lerped towards sprintActive target for momentum feel).
        private float _sprintAmount;
        public float SprintAmount => _sprintAmount;

        [Header("Stamina (issue #189)")]
        [Tooltip("Current stamina 0..1. Drains while sprinting+moving, regens otherwise.")]
        [Range(0f, 1f)] public float stamina = 1f;

        [Tooltip("Stamina drain per second when sprinting AND moving.")]
        public float staminaDrainRate = 0.2f;

        [Tooltip("Stamina regen per second when not sprinting.")]
        public float staminaRegenRate = 0.3f;

        [Tooltip("Minimum stamina required to START sprinting (auto-disables below).")]
        public float staminaMinToStart = 0.1f;

        public float Stamina => stamina;

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

        // Air momentum (issue #141): horizontal velocity persists between frames.
        // In air: input accelerates, air drag decays (ice-skating feel).
        // On ground: instant snap (direct CC.Move).
        private Vector3 _airVelocity;

        [Header("Air momentum (issue #141)")]
        [Tooltip("Air acceleration (m/s^2) when input held while flying.")]
        public float airAcceleration = 6f;
        [Tooltip("Max horizontal speed while flying (m/s).")]
        public float airMaxSpeed = 8f;
        [Tooltip("Air drag coefficient (higher = stops faster). 0.5 = gentle ice skating.")]
        public float airDrag = 0.5f;

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

        // --- IPlayerMotionSource -----------------------------------------------
        // Freefall: nie jestesmy na ziemi, nie lecimy aktywnie, spadamy w dol
        // z wieksza predkoscia niz typowy maly skok (threshold -5 m/s).
        private const float FreefallVerticalThreshold = -5f;
        private const float LocomotionSpeedThreshold  = 0.1f;

        public PlayerMotionState CurrentState
        {
            get
            {
                if (IsFlying) return PlayerMotionState.Fly;
                if (!IsGrounded && _verticalVelocity < FreefallVerticalThreshold)
                    return PlayerMotionState.Freefall;
                if (NormalisedSpeed > LocomotionSpeedThreshold)
                    return PlayerMotionState.Locomotion;
                return PlayerMotionState.Idle;
            }
        }

        /// <summary>Linear speed m/s -- dla Animator blend tree.</summary>
        public float Speed => NormalisedSpeed
            * (moveSpeed * (sprintActive ? Mathf.Lerp(1f, sprintMultiplier, _sprintAmount) : 1f));

        /// <summary>Lateral axis -1..1 dla strafe blend.</summary>
        public float StrafeX => _lastMoveInput.x;

        /// <summary>Forward axis -1..1 (+1 forward, -1 backward).</summary>
        public float ForwardZ => _lastMoveInput.y;

        private Vector2 _lastMoveInput; // filled w Update()

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
                // Block sprint start if stamina too low (Issue #189)
                if (!sprintActive && stamina < staminaMinToStart)
                {
                    Debug.Log($"{LOG} Sprint blocked (stamina={stamina:F2} < {staminaMinToStart:F2})");
                }
                else
                {
                    sprintActive = !sprintActive;
                    Debug.Log($"{LOG} Sprint: {(sprintActive ? "ON" : "OFF")} (stamina={stamina:F2})");
                }
            }

            Vector2 moveInput = GetMoveInput();
            bool isMoving = moveInput.sqrMagnitude > InputDeadZoneSqr;

            // Stamina drain/regen (Issue #189)
            if (sprintActive && isMoving)
            {
                stamina -= staminaDrainRate * _dt;
                if (stamina <= 0f)
                {
                    stamina = 0f;
                    sprintActive = false;
                    Debug.Log($"{LOG} Sprint auto-stop: stamina depleted");
                }
            }
            else
            {
                stamina = Mathf.Min(1f, stamina + staminaRegenRate * _dt);
            }

            // Sprint ramp (Issue #143) -- lerp _sprintAmount toward target for momentum feel
            float sprintTarget = sprintActive ? 1f : 0f;
            _sprintAmount = Mathf.MoveTowards(_sprintAmount, sprintTarget, sprintRampSpeed * _dt);
            // Prone blocks horizontal movement -- must stand up first
            bool movementBlocked = (_currentStance == Stance.Prone);

            float rightY = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).y;
            UpdateFly(rightY);
            UpdateStance(rightY);

            // Safety: force FLOATING if flying with wrong stance
            if (_flyState != FlyState.Grounded && _currentStance != Stance.Floating)
                SetStance(Stance.Floating);

            if (_flyState == FlyState.Grounded) ApplyGravity();

            Vector3 horizontalMove;
            if (_flyState == FlyState.Grounded)
            {
                // Ground: instant movement (no momentum)
                horizontalMove = movementBlocked ? Vector3.zero : CalculateHeadRelativeMovement(moveInput);
                _airVelocity = Vector3.zero; // reset momentum on ground
            }
            else
            {
                // Air: momentum/lodowisko (issue #141)
                horizontalMove = UpdateAirMomentum(moveInput, movementBlocked);
            }

            ApplyMove(horizontalMove);
            NormalisedSpeed = Mathf.Clamp01(moveInput.magnitude);
            _lastMoveInput = moveInput; // publish for IPlayerMotionSource
            LogGroundedChangesThrottled();
        }

        // Ice-skating horizontal movement in air (issue #141)
        // Input adds acceleration to _airVelocity. Drag decays velocity.
        // Returns displacement this frame.
        private Vector3 UpdateAirMomentum(Vector2 input, bool inputBlocked)
        {
            // Compute world-space direction vector from input (head-relative)
            Vector3 inputDir = Vector3.zero;
            if (!inputBlocked && input.sqrMagnitude >= InputDeadZoneSqr)
            {
                Vector3 fwd = _headTransform.forward; fwd.y = 0f; fwd.Normalize();
                Vector3 right = _headTransform.right; right.y = 0f; right.Normalize();
                inputDir = (fwd * input.y) + (right * input.x * strafeFactor);
            }

            // Accelerate toward input direction (sprint ramp applies, issue #143)
            float speedMul = Mathf.Lerp(1f, sprintMultiplier, _sprintAmount);
            _airVelocity += inputDir * airAcceleration * speedMul * _dt;

            // Clamp to max speed
            float max = airMaxSpeed * speedMul;
            if (_airVelocity.sqrMagnitude > max * max)
                _airVelocity = _airVelocity.normalized * max;

            // Apply air drag (exponential decay)
            _airVelocity = Vector3.Lerp(_airVelocity, Vector3.zero, airDrag * _dt);

            return _airVelocity * _dt;
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
        // Issue #188: faster direction changes = more chaotic/fun hover
        private const float HoverDriftChangeMin = 0.15f;
        private const float HoverDriftChangeMax = 0.8f;
        private const float HoverDriftLerp = 2.5f;

        private void UpdateFly(float rightY)
        {
            switch (_flyState)
            {
                case FlyState.Grounded:
                    if (rightY > StickUpThreshold && _currentStance == Stance.Stand)
                    {
                        _flyState = FlyState.Ascending;
                        // Issue #187: initial upward kick to leave ground immediately.
                        // Otherwise CC.isGrounded stays true first frame and EndFlight
                        // fires, aborting the takeoff when L stick is also active.
                        _flySpeed = 3f;
                        _verticalVelocity = _flySpeed;
                        SetStance(Stance.Floating);
                        Debug.Log($"{LOG} Fly: ASCENDING (initial kick {_flySpeed} m/s)");
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
                    // Landing check -- only when descending/low speed to avoid aborting takeoff (issue #187).
                    if (IsGrounded && _flySpeed < 0.5f) { EndFlight(); break; }
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

            // Sync stance edge flags to CURRENT stick state.
            // Otherwise stick still held DOWN after drop-landing triggers a
            // false 'DOWN edge' -> auto-crouch, and UP triggers same loop.
            // Issue: 'cały czas LATAM - nie da się zmienić stance'.
            float rightY = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).y;
            _stanceDownPressed = rightY < -StickDownThreshold;
            _stanceUpPressed   = rightY > StickUpThreshold;
            Debug.Log($"{LOG} EndFlight: synced stance flags (downHeld={_stanceDownPressed} upHeld={_stanceUpPressed})");
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
            // DISABLED: Borys wymog -- tylko chodzenie i latanie, bez crouch/prone.
            // Prawy thumbstick uzywany wylacznie do fly (UP = fly, DOWN = brak akcji).
            // Re-enable gdy bedziemy potrzebowac stance system.
            return;

            #pragma warning disable CS0162 // unreachable code
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
            #pragma warning restore CS0162
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
            // Sprint multiplier ramps 1.0 -> sprintMultiplier over sprintRampSpeed (issue #143)
            float speedMul = Mathf.Lerp(1f, sprintMultiplier, _sprintAmount);
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
