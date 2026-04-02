// AUTO-DISABLED: depends on PerformanceMonitor (guarded by PLAGA44_FULL_SDK)
#if PLAGA44_FULL_SDK
// QualityScaler.cs
// CYBERNOMAD -- Dynamic quality scaling in response to performance drops.
// Listens to PerformanceMonitor events and progressively reduces:
//   1. Render scale (XRSettings.eyeTextureResolutionScale)
//   2. Shadow distance (QualitySettings.shadowDistance)
//   3. LOD bias (QualitySettings.lodBias)
// Restores quality incrementally when FPS recovers.
//
// Requires PerformanceMonitor on the same or sibling GameObject.

using UnityEngine;
using UnityEngine.XR;

namespace Plaga44.Performance
{
    /// <summary>
    /// Dynamically scales render quality up/down based on PerformanceMonitor events.
    /// Attach alongside or as a child of a PerformanceMonitor.
    /// </summary>
    public class QualityScaler : MonoBehaviour
    {
        // ── Inspector config ─────────────────────────────────────────────────

        [Header("Render Scale")]
        [Tooltip("Maximum (baseline) render scale. 1.0 = native resolution.")]
        [Range(0.5f, 2f)]
        public float maxRenderScale = 1.0f;

        [Tooltip("Minimum render scale to drop to under load.")]
        [Range(0.3f, 1f)]
        public float minRenderScale = 0.7f;

        [Tooltip("How much to change render scale per step.")]
        [Range(0.05f, 0.3f)]
        public float renderScaleStep = 0.1f;

        [Header("Shadow Distance")]
        [Tooltip("Baseline shadow distance in world units.")]
        [Range(5f, 100f)]
        public float maxShadowDistance = 35f;

        [Tooltip("Minimum shadow distance under load.")]
        [Range(0f, 30f)]
        public float minShadowDistance = 8f;

        [Tooltip("Shadow distance reduction per drop step.")]
        [Range(1f, 20f)]
        public float shadowStep = 8f;

        [Header("LOD Bias")]
        [Tooltip("Baseline LOD bias (higher = higher quality models further away).")]
        [Range(0.3f, 2f)]
        public float maxLodBias = 1.0f;

        [Tooltip("Minimum LOD bias under load.")]
        [Range(0.1f, 1f)]
        public float minLodBias = 0.4f;

        [Tooltip("LOD bias reduction per drop step.")]
        [Range(0.05f, 0.4f)]
        public float lodBiasStep = 0.15f;

        [Header("Recovery")]
        [Tooltip("Seconds between each incremental quality restore step after recovery.")]
        [Range(1f, 30f)]
        public float recoveryStepInterval = 5f;

        // ── Internal state ───────────────────────────────────────────────────

        private PerformanceMonitor _monitor;

        private float _currentRenderScale;
        private float _currentShadowDistance;
        private float _currentLodBias;

        private bool  _recovering    = false;
        private float _recoveryTimer = 0f;

        // ── Properties (read-only metrics) ───────────────────────────────────

        public float CurrentRenderScale     => _currentRenderScale;
        public float CurrentShadowDistance  => _currentShadowDistance;
        public float CurrentLodBias         => _currentLodBias;

        // ── Unity lifecycle ──────────────────────────────────────────────────

        private void Start()
        {
            // Snapshot baselines from current project settings.
            _currentRenderScale    = Mathf.Clamp(XRSettings.eyeTextureResolutionScale, minRenderScale, maxRenderScale);
            _currentShadowDistance = Mathf.Clamp(QualitySettings.shadowDistance,        minShadowDistance, maxShadowDistance);
            _currentLodBias        = Mathf.Clamp((float)QualitySettings.lodBias,         minLodBias, maxLodBias);

            // Wire up to PerformanceMonitor.
            _monitor = GetComponent<PerformanceMonitor>();
            if (_monitor == null)
                _monitor = GetComponentInParent<PerformanceMonitor>();
            if (_monitor == null)
                _monitor = FindFirstObjectByType<PerformanceMonitor>();

            if (_monitor != null)
            {
                _monitor.OnPerformanceDrop    += HandlePerformanceDrop;
                _monitor.OnPerformanceRecover += HandlePerformanceRecover;
                Debug.Log("[QualityScaler] Wired to PerformanceMonitor.");
            }
            else
            {
                Debug.LogWarning("[QualityScaler] No PerformanceMonitor found -- QualityScaler is inactive.");
            }
        }

        private void Update()
        {
            if (!_recovering) return;

            _recoveryTimer += Time.unscaledDeltaTime;

            if (_recoveryTimer >= recoveryStepInterval)
            {
                _recoveryTimer = 0f;
                bool atMax = StepQualityUp();

                if (atMax)
                {
                    _recovering = false;
                    Debug.Log("[QualityScaler] Quality fully restored to baseline.");
                }
            }
        }

        private void OnDestroy()
        {
            if (_monitor != null)
            {
                _monitor.OnPerformanceDrop    -= HandlePerformanceDrop;
                _monitor.OnPerformanceRecover -= HandlePerformanceRecover;
            }
        }

        // ── Event handlers ───────────────────────────────────────────────────

        private void HandlePerformanceDrop(float avgFps)
        {
            _recovering = false; // Stop recovery if we're dropping again.
            StepQualityDown();
        }

        private void HandlePerformanceRecover(float avgFps)
        {
            // Start gradual recovery (stepped in Update).
            _recovering    = true;
            _recoveryTimer = 0f;
            Debug.Log("[QualityScaler] Starting quality recovery...");
        }

        // ── Quality stepping ─────────────────────────────────────────────────

        /// <summary>Reduce all quality axes by one step. Returns true if already at minimum.</summary>
        private bool StepQualityDown()
        {
            bool alreadyMin = true;

            // Render scale
            if (_currentRenderScale > minRenderScale + 0.001f)
            {
                _currentRenderScale = Mathf.Max(minRenderScale, _currentRenderScale - renderScaleStep);
                XRSettings.eyeTextureResolutionScale = _currentRenderScale;
                alreadyMin = false;
            }

            // Shadow distance
            if (_currentShadowDistance > minShadowDistance + 0.001f)
            {
                _currentShadowDistance = Mathf.Max(minShadowDistance, _currentShadowDistance - shadowStep);
                QualitySettings.shadowDistance = _currentShadowDistance;
                alreadyMin = false;
            }

            // LOD bias
            if (_currentLodBias > minLodBias + 0.001f)
            {
                _currentLodBias = Mathf.Max(minLodBias, _currentLodBias - lodBiasStep);
                QualitySettings.lodBias = _currentLodBias;
                alreadyMin = false;
            }

            LogCurrentSettings("DOWN");
            return alreadyMin;
        }

        /// <summary>Restore all quality axes by one step. Returns true if all at maximum.</summary>
        private bool StepQualityUp()
        {
            bool atMax = true;

            // Render scale
            if (_currentRenderScale < maxRenderScale - 0.001f)
            {
                _currentRenderScale = Mathf.Min(maxRenderScale, _currentRenderScale + renderScaleStep);
                XRSettings.eyeTextureResolutionScale = _currentRenderScale;
                atMax = false;
            }

            // Shadow distance
            if (_currentShadowDistance < maxShadowDistance - 0.001f)
            {
                _currentShadowDistance = Mathf.Min(maxShadowDistance, _currentShadowDistance + shadowStep);
                QualitySettings.shadowDistance = _currentShadowDistance;
                atMax = false;
            }

            // LOD bias
            if (_currentLodBias < maxLodBias - 0.001f)
            {
                _currentLodBias = Mathf.Min(maxLodBias, _currentLodBias + lodBiasStep);
                QualitySettings.lodBias = _currentLodBias;
                atMax = false;
            }

            LogCurrentSettings("UP");
            return atMax;
        }

        private void LogCurrentSettings(string direction)
        {
            Debug.Log($"[QualityScaler] Step {direction} -- " +
                      $"renderScale={_currentRenderScale:F2}  " +
                      $"shadowDist={_currentShadowDistance:F1}  " +
                      $"lodBias={_currentLodBias:F2}");
        }

        // ── Editor helper: force reset to baseline ───────────────────────────

        /// <summary>
        /// Immediately restore all quality settings to their configured max values.
        /// Useful from editor scripts or debug menus.
        /// </summary>
        public void ResetToBaseline()
        {
            _recovering = false;

            _currentRenderScale    = maxRenderScale;
            _currentShadowDistance = maxShadowDistance;
            _currentLodBias        = maxLodBias;

            XRSettings.eyeTextureResolutionScale = _currentRenderScale;
            QualitySettings.shadowDistance       = _currentShadowDistance;
            QualitySettings.lodBias              = _currentLodBias;

            Debug.Log("[QualityScaler] Baseline restored.");
        }
    }
}
#endif // PLAGA44_FULL_SDK
