using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.Locomotion
{
    /// <summary>
    /// Thumbstick-triggered teleport locomotion with arc visualizer and landing indicator.
    ///
    /// Usage:
    ///   1. Attach to VR rig root.
    ///   2. Assign a LineRenderer (or let it auto-create).
    ///   3. Set <see cref="validLayers"/> to layers the player can land on.
    ///   4. Aim right thumbstick forward -- arc appears. Release to teleport.
    ///
    /// Requires <see cref="LocomotionManager"/> on same/parent GameObject to fire
    /// the mode switch callback (optional -- works standalone too).
    /// </summary>
    [DisallowMultipleComponent]
    public class TeleportLocomotion : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector fields
        // -------------------------------------------------------------------------

        [Header("Arc")]
        [Tooltip("Maximum arc travel distance in metres.")]
        public float maxDistance = 10f;

        [Tooltip("Number of segments in the arc LineRenderer.")]
        [Range(8, 64)]
        public int arcResolution = 24;

        [Tooltip("Initial launch speed of the projectile simulation (higher = flatter arc).")]
        public float arcLaunchSpeed = 8f;

        [Tooltip("Simulated gravity for the arc (downward). Usually 9.81.")]
        public float arcGravity = 9.81f;

        [Header("Layers")]
        [Tooltip("LayerMask of surfaces the player is allowed to teleport onto.")]
        public LayerMask validLayers = ~0;   // default: everything

        [Header("Input")]
        [Tooltip("Deadzone for the right thumbstick Y axis to activate the arc.")]
        [Range(0.1f, 0.9f)]
        public float inputDeadzone = 0.5f;

        [Header("References")]
        [Tooltip("LineRenderer for the arc. Auto-created if null.")]
        [SerializeField] private LineRenderer _arcLine;

        [Tooltip("Transform placed at the landing indicator. Auto-created if null.")]
        [SerializeField] private Transform _landingIndicator;

        [Tooltip("Material used on the arc when landing is valid.")]
        [SerializeField] private Material _validArcMaterial;

        [Tooltip("Material used on the arc when landing is invalid.")]
        [SerializeField] private Material _invalidArcMaterial;

        [Header("Head / Hand References")]
        [SerializeField] private Transform _headTransform;
        [SerializeField] private Transform _rightHandTransform;

        // -------------------------------------------------------------------------
        // Runtime state
        // -------------------------------------------------------------------------

        private bool _aiming;
        private bool _hasValidTarget;
        private Vector3 _targetPoint;
        private readonly List<Vector3> _arcPoints = new List<Vector3>(64);

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            EnsureLineRenderer();
            EnsureLandingIndicator();
            ResolveTransforms();

            SetArcVisible(false);
        }

        private void Update()
        {
            Vector2 thumbstick = GetRightThumbstick();
            bool wantsAim = thumbstick.y > inputDeadzone;

            if (wantsAim)
            {
                _aiming = true;
                UpdateArc();
            }
            else if (_aiming)
            {
                // Thumbstick released -- attempt teleport.
                _aiming = false;
                SetArcVisible(false);

                if (_hasValidTarget)
                    ExecuteTeleport(_targetPoint);
            }
        }

        // -------------------------------------------------------------------------
        // Input
        // -------------------------------------------------------------------------

        private Vector2 GetRightThumbstick()
        {
#if HAS_META_XR
            return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
#else
            float h = UnityEngine.Input.GetAxis("RightStickHorizontal");
            float v = UnityEngine.Input.GetAxis("RightStickVertical");
            return new Vector2(h, v);
#endif
        }

        // -------------------------------------------------------------------------
        // Arc calculation
        // -------------------------------------------------------------------------

        private void UpdateArc()
        {
            _arcPoints.Clear();

            Transform origin = _rightHandTransform != null ? _rightHandTransform
                             : _headTransform != null      ? _headTransform
                             : transform;

            Vector3 pos = origin.position;
            Vector3 vel = origin.forward * arcLaunchSpeed;

            float stepTime = maxDistance / (arcResolution * arcLaunchSpeed);
            _hasValidTarget = false;

            for (int i = 0; i < arcResolution; i++)
            {
                _arcPoints.Add(pos);

                Vector3 nextPos = pos + vel * stepTime;
                vel.y -= arcGravity * stepTime;

                // Check for collision along this segment.
                Vector3 dir = nextPos - pos;
                float dist = dir.magnitude;

                if (Physics.Raycast(pos, dir.normalized, out RaycastHit hit, dist, validLayers))
                {
                    _arcPoints.Add(hit.point);
                    _hasValidTarget = true;
                    _targetPoint = hit.point;
                    UpdateLandingIndicator(hit.point, valid: true);
                    break;
                }

                pos = nextPos;

                // Stop arc if it has gone further than maxDistance.
                if (Vector3.Distance(origin.position, pos) >= maxDistance)
                    break;
            }

            // If no hit found, hide indicator.
            if (!_hasValidTarget)
                UpdateLandingIndicator(Vector3.zero, valid: false);

            // Update LineRenderer.
            _arcLine.positionCount = _arcPoints.Count;
            _arcLine.SetPositions(_arcPoints.ToArray());

            // Swap material based on validity.
            if (_validArcMaterial != null && _invalidArcMaterial != null)
                _arcLine.material = _hasValidTarget ? _validArcMaterial : _invalidArcMaterial;

            SetArcVisible(true);
        }

        // -------------------------------------------------------------------------
        // Teleport execution
        // -------------------------------------------------------------------------

        private void ExecuteTeleport(Vector3 destination)
        {
            // Keep the player's height relative to the rig (don't snap Y unless flat surface).
            Vector3 rigPos = transform.position;
            destination.y = rigPos.y; // preserve vertical; room-scale handles height.

            transform.position = destination;
            Debug.Log($"[TeleportLocomotion] Teleported to {destination}");
        }

        // -------------------------------------------------------------------------
        // Visuals helpers
        // -------------------------------------------------------------------------

        private void SetArcVisible(bool visible)
        {
            if (_arcLine != null) _arcLine.enabled = visible;
            if (_landingIndicator != null) _landingIndicator.gameObject.SetActive(visible && _hasValidTarget);
        }

        private void UpdateLandingIndicator(Vector3 point, bool valid)
        {
            if (_landingIndicator == null) return;
            _landingIndicator.gameObject.SetActive(valid);
            if (valid) _landingIndicator.position = point;
        }

        // -------------------------------------------------------------------------
        // Auto-creation of missing components
        // -------------------------------------------------------------------------

        private void EnsureLineRenderer()
        {
            if (_arcLine != null) return;

            _arcLine = GetComponent<LineRenderer>();
            if (_arcLine != null) return;

            _arcLine = gameObject.AddComponent<LineRenderer>();
            _arcLine.widthMultiplier = 0.02f;
            _arcLine.useWorldSpace = true;
            _arcLine.receiveShadows = false;
            _arcLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Simple default material (URP unlit white if no material assigned).
            if (_arcLine.sharedMaterial == null)
                _arcLine.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")
                                                    ?? Shader.Find("Sprites/Default"));
        }

        private void EnsureLandingIndicator()
        {
            if (_landingIndicator != null) return;

            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indicator.name = "TeleportLandingIndicator";
            indicator.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);

            // Remove collider so it doesn't interfere with raycasts.
            Destroy(indicator.GetComponent<Collider>());

            _landingIndicator = indicator.transform;
        }

        private void ResolveTransforms()
        {
            if (_headTransform == null)
            {
#if HAS_META_XR
                var tracking = transform.Find("TrackingSpace");
                if (tracking != null)
                {
                    var eye = tracking.Find("CenterEyeAnchor");
                    if (eye != null) _headTransform = eye;
                }
#endif
                if (_headTransform == null && Camera.main != null)
                    _headTransform = Camera.main.transform;
            }

            if (_rightHandTransform == null)
            {
#if HAS_META_XR
                var tracking = transform.Find("TrackingSpace");
                if (tracking != null)
                {
                    var hand = tracking.Find("RightHandAnchor");
                    if (hand != null) _rightHandTransform = hand;
                }
#endif
            }
        }
    }
}
