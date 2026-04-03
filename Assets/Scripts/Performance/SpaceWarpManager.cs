// SpaceWarpManager.cs
// CYBERNOMAD -- Application SpaceWarp controller for Meta Quest 3.
// Enables ASW (render every 2nd frame, extrapolate motion) to double effective GPU budget.
// Auto-toggle: monitors FPS, enables ASW when FPS drops below fpsThreshold.
// Manual override available via SetSpaceWarp(bool).
//
// Requires: Vulkan graphics API + Meta XR SDK (HAS_META_XR define).
// Reference: https://developers.meta.com/horizon/documentation/unity/unity-asw/

using UnityEngine;

namespace Plaga44.Performance
{
    /// <summary>
    /// Singleton MonoBehaviour that controls Application SpaceWarp on Meta Quest.
    /// Attach to a persistent GameObject in your main scene (e.g. [Performance] root).
    /// </summary>
    public class SpaceWarpManager : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────

        private static SpaceWarpManager _instance;

        public static SpaceWarpManager Instance => _instance;

        // ── Inspector config ─────────────────────────────────────────────────

        [Header("Auto-Toggle Settings")]
        [Tooltip("FPS threshold below which ASW is automatically enabled.")]
        [Range(30f, 90f)]
        public float fpsThreshold = 65f;

        [Tooltip("Enable Application SpaceWarp automatically on Start.")]
        public bool enableOnStart = false;

        [Tooltip("Seconds to wait between auto-toggle evaluations (avoids flicker).")]
        [Range(0.5f, 5f)]
        public float evaluationInterval = 1.5f;

        [Header("Manual Override")]
        [Tooltip("When true, auto-toggle is disabled and the state is fully controlled via SetSpaceWarp().")]
        public bool manualOverride = false;

        // ── Internal state ───────────────────────────────────────────────────

        private bool _isSpaceWarpActive = false;
        private float _evaluationTimer = 0f;

        // Rolling FPS average over the last evaluationInterval seconds.
        private float _fpsAccumulator = 0f;
        private int _fpsFrames = 0;
        private float _averageFps = 0f;

        // ── Properties ───────────────────────────────────────────────────────

        /// <summary>Current SpaceWarp state (read-only from outside).</summary>
        public bool IsSpaceWarpActive => _isSpaceWarpActive;

        /// <summary>Last computed average FPS (over evaluationInterval).</summary>
        public float AverageFps => _averageFps;

        // ── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[SpaceWarpManager] Duplicate instance destroyed.");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            ValidateVulkan();

            if (enableOnStart)
                SetSpaceWarp(true);
        }

        private void Update()
        {
            // Accumulate FPS samples.
            if (Time.unscaledDeltaTime > 0f)
            {
                _fpsAccumulator += 1f / Time.unscaledDeltaTime;
                _fpsFrames++;
            }

            _evaluationTimer += Time.unscaledDeltaTime;

            if (_evaluationTimer >= evaluationInterval)
            {
                _averageFps = (_fpsFrames > 0) ? (_fpsAccumulator / _fpsFrames) : 0f;
                _fpsAccumulator = 0f;
                _fpsFrames = 0;
                _evaluationTimer = 0f;

                if (!manualOverride)
                    EvaluateAutoToggle();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                // Always disable ASW on cleanup so we don't leave the runtime in an unexpected state.
                ApplySpaceWarp(false);
                _instance = null;
            }
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Manually set SpaceWarp state. Activates manualOverride automatically.
        /// </summary>
        public void SetSpaceWarp(bool enabled)
        {
            manualOverride = true;
            ApplySpaceWarp(enabled);
        }

        /// <summary>
        /// Release manual override and return to auto-toggle mode.
        /// </summary>
        public void ReleaseManualOverride()
        {
            manualOverride = false;
        }

        // ── Internal ─────────────────────────────────────────────────────────

        private void EvaluateAutoToggle()
        {
            bool shouldEnable = _averageFps < fpsThreshold;

            if (shouldEnable != _isSpaceWarpActive)
            {
                Debug.Log($"[SpaceWarpManager] Auto-toggle: avgFPS={_averageFps:F1} threshold={fpsThreshold} -> ASW {(shouldEnable ? "ON" : "OFF")}");
                ApplySpaceWarp(shouldEnable);
            }
        }

        private void ApplySpaceWarp(bool enabled)
        {
            if (_isSpaceWarpActive == enabled) return;

            _isSpaceWarpActive = enabled;

#if HAS_META_XR
            OVRManager.SetSpaceWarp(enabled);
            Debug.Log($"[SpaceWarpManager] Application SpaceWarp {(enabled ? "ENABLED" : "DISABLED")}");
#else
            Debug.Log($"[SpaceWarpManager] HAS_META_XR not defined -- SpaceWarp is a no-op.");
#endif
        }

        private void ValidateVulkan()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Vulkan)
            {
                Debug.LogWarning("[SpaceWarpManager] Application SpaceWarp requires Vulkan. Current API: " + SystemInfo.graphicsDeviceType);
            }
#endif
        }
    }
}
