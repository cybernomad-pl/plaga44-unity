// AUTO-DISABLED: PLAGA44_FULL_SDK
#if PLAGA44_FULL_SDK
// GazeDebug.cs
// PLAGA '44 -- Debug visualizer for eye tracking.
// Draws gaze rays in Scene view (Debug.DrawRay) and optionally a world-space
// indicator showing what the player is currently looking at.
//
// Editor-only visualization is always compiled; runtime indicator
// can be toggled via Inspector.
// Namespace: Plaga44.EyeTracking

using UnityEngine;

namespace Plaga44.EyeTracking
{
    /// <summary>
    /// Attach alongside EyeTrackingManager to visualize gaze in the Scene view
    /// and (optionally) with a runtime indicator sphere.
    /// </summary>
    public class GazeDebug : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Header("Ray Visualization")]
        [SerializeField] private bool  _showLeftEye   = true;
        [SerializeField] private bool  _showRightEye  = true;
        [SerializeField] private bool  _showCombined  = true;

        [SerializeField] private Color _leftEyeColor    = new Color(0.2f, 0.6f, 1f, 1f);
        [SerializeField] private Color _rightEyeColor   = new Color(1f, 0.4f, 0.2f, 1f);
        [SerializeField] private Color _combinedColor   = Color.green;
        [SerializeField] private Color _lowConfColor    = new Color(1f, 1f, 0f, 0.5f);

        [SerializeField][Range(0.1f, 20f)] private float _rayLength = 5f;

        [Header("Hit Indicator")]
        [Tooltip("Show a world-space sphere at the gaze hit point.")]
        [SerializeField] private bool  _showHitIndicator = true;

        [Tooltip("Layer mask for gaze raycast against scene geometry.")]
        [SerializeField] private LayerMask _hitLayers = ~0;

        [Tooltip("Radius of the debug hit sphere.")]
        [SerializeField] private float _hitSphereRadius = 0.03f;

        [Header("Confidence Readout")]
        [SerializeField] private bool _logConfidenceToConsole = false;

        // ── State ─────────────────────────────────────────────────────────

        private EyeTrackingManager _manager;
        private GameObject         _hitIndicator;
        private Renderer           _hitRenderer;
        private Material           _hitMaterial;

        // ── Lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _manager = GetComponent<EyeTrackingManager>();
            if (_manager == null)
                _manager = FindFirstObjectByType<EyeTrackingManager>();

            if (_manager == null)
                Debug.LogWarning("[GazeDebug] No EyeTrackingManager found -- debug will use camera forward.");

            if (_showHitIndicator)
                BuildHitIndicator();
        }

        private void OnDestroy()
        {
            if (_hitIndicator != null) Destroy(_hitIndicator);
            if (_hitMaterial  != null) Destroy(_hitMaterial);
        }

        private void Update()
        {
            DrawRays();
            UpdateHitIndicator();

            if (_logConfidenceToConsole && _manager != null)
                Debug.Log($"[GazeDebug] Confidence: {_manager.GetGazeConfidence():F2}");
        }

        // ── Ray drawing ──────────────────────────────────────────────────

        private void DrawRays()
        {
            float confidence = _manager != null ? _manager.GetGazeConfidence() : 0f;
            bool hasConfidence = _manager != null && confidence > 0.3f;

#if HAS_META_XR
            if (_showLeftEye && _manager != null)
            {
                Ray leftRay = _manager.GetGazeDirection(GazeEye.Left);
                Color c = hasConfidence ? _leftEyeColor : _lowConfColor;
                Debug.DrawRay(leftRay.origin, leftRay.direction * _rayLength, c);
            }

            if (_showRightEye && _manager != null)
            {
                Ray rightRay = _manager.GetGazeDirection(GazeEye.Right);
                Color c = hasConfidence ? _rightEyeColor : _lowConfColor;
                Debug.DrawRay(rightRay.origin, rightRay.direction * _rayLength, c);
            }
#endif

            if (_showCombined)
            {
                Ray combinedRay = _manager != null
                    ? _manager.GetGazeDirection(GazeEye.Center)
                    : new Ray(Camera.main != null ? Camera.main.transform.position : Vector3.zero,
                              Camera.main != null ? Camera.main.transform.forward  : Vector3.forward);

                Color c = hasConfidence ? _combinedColor : _lowConfColor;
                Debug.DrawRay(combinedRay.origin, combinedRay.direction * _rayLength, c);
            }
        }

        // ── Hit indicator ────────────────────────────────────────────────

        private void UpdateHitIndicator()
        {
            if (!_showHitIndicator || _hitIndicator == null) return;

            Ray gazeRay = _manager != null
                ? _manager.GetGazeDirection(GazeEye.Center)
                : new Ray(Camera.main != null ? Camera.main.transform.position : Vector3.zero,
                          Camera.main != null ? Camera.main.transform.forward  : Vector3.forward);

            bool hit = Physics.Raycast(gazeRay, out RaycastHit hitInfo, _rayLength * 3f, _hitLayers);

            if (hit)
            {
                _hitIndicator.SetActive(true);
                _hitIndicator.transform.position = hitInfo.point;

                // Color by confidence
                float conf = _manager != null ? _manager.GetGazeConfidence() : 0f;
                Color indicatorColor = Color.Lerp(_lowConfColor, _combinedColor, conf);
                if (_hitMaterial != null) _hitMaterial.color = indicatorColor;
            }
            else
            {
                // Place at end of ray when no geometry hit
                _hitIndicator.SetActive(true);
                _hitIndicator.transform.position = gazeRay.origin + gazeRay.direction * _rayLength;
                if (_hitMaterial != null) _hitMaterial.color = _lowConfColor;
            }
        }

        private void BuildHitIndicator()
        {
            _hitIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _hitIndicator.name = "GazeHitIndicator";
            _hitIndicator.transform.localScale = Vector3.one * _hitSphereRadius * 2f;

            // Remove collider so it does not interfere with gaze raycasts
            var col = _hitIndicator.GetComponent<Collider>();
            if (col != null) Destroy(col);

            DontDestroyOnLoad(_hitIndicator);

            var meshRenderer = _hitIndicator.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                _hitMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ??
                                            Shader.Find("Standard"));
                _hitMaterial.color = _combinedColor;
                // Make unlit-ish for clarity
                _hitMaterial.SetFloat("_Smoothness", 0f);
                meshRenderer.material = _hitMaterial;
            }
        }

        // ── Gizmos ───────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_manager == null) return;

            Ray combined = _manager.GetGazeDirection(GazeEye.Center);
            float conf   = _manager.GetGazeConfidence();

            UnityEditor.Handles.color = Color.Lerp(_lowConfColor, _combinedColor, conf);
            UnityEditor.Handles.DrawLine(
                combined.origin,
                combined.origin + combined.direction * _rayLength);

            UnityEditor.Handles.color = new Color(_combinedColor.r, _combinedColor.g, _combinedColor.b, 0.2f);
            UnityEditor.Handles.SphereHandleCap(
                0,
                combined.origin + combined.direction * _rayLength,
                Quaternion.identity,
                _hitSphereRadius,
                EventType.Repaint);
        }
#endif
    }
}
#endif // PLAGA44_FULL_SDK
