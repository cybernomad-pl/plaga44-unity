// AUTO-DISABLED: depends on SpaceWarpManager (guarded by PLAGA44_FULL_SDK)
#if PLAGA44_FULL_SDK
// PerformanceMonitor.cs
// CYBERNOMAD -- Real-time VR performance metrics with HUD overlay.
// Tracks FPS, GPU time, CPU time (via OVRManager.GetGPUUtilLevel / GetCPULevel if available).
// Fires events OnPerformanceDrop / OnPerformanceRecover when FPS crosses dropThreshold.
// Debug HUD: world-space canvas anchored in front of headset, toggled via ToggleHud().
//
// Requires Meta XR SDK for GPU/CPU stats (graceful fallback without it).

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.Performance
{
    /// <summary>
    /// MonoBehaviour that collects real-time performance metrics and exposes them
    /// as events and an optional debug HUD.
    /// </summary>
    public class PerformanceMonitor : MonoBehaviour
    {
        // ── Inspector config ─────────────────────────────────────────────────

        [Header("Sampling")]
        [Tooltip("How often (seconds) to compute average metrics and fire events.")]
        [Range(0.2f, 5f)]
        public float sampleInterval = 1f;

        [Header("Performance Drop Detection")]
        [Tooltip("FPS below this value triggers OnPerformanceDrop.")]
        [Range(20f, 90f)]
        public float dropThreshold = 60f;

        [Tooltip("FPS must stay above dropThreshold + hysteresis to trigger OnPerformanceRecover.")]
        [Range(0f, 20f)]
        public float recoveryHysteresis = 5f;

        [Header("HUD")]
        [Tooltip("Show debug HUD on start.")]
        public bool showHudOnStart = false;

        [Tooltip("Distance from the headset camera at which the HUD floats.")]
        [Range(0.3f, 2f)]
        public float hudDistance = 0.4f;

        // ── Events ───────────────────────────────────────────────────────────

        /// <summary>Fired when average FPS drops below dropThreshold. Arg = current avg FPS.</summary>
        public event Action<float> OnPerformanceDrop;

        /// <summary>Fired when average FPS recovers above dropThreshold + recoveryHysteresis.</summary>
        public event Action<float> OnPerformanceRecover;

        // ── Public read-only metrics ─────────────────────────────────────────

        public float CurrentFps       { get; private set; }
        public float AverageFps       { get; private set; }
        public float GpuUtilLevel     { get; private set; }  // 0-4 scale (Meta) or -1 if unavailable
        public float CpuLevel         { get; private set; }  // 0-4 scale (Meta) or -1 if unavailable
        public bool  IsInDropState    { get; private set; }

        // ── Internal ─────────────────────────────────────────────────────────

        private float _sampleTimer     = 0f;
        private float _fpsAccumulator  = 0f;
        private int   _fpsFrames       = 0;

        private Transform _headTransform;

        // HUD refs
        private Canvas _hudCanvas;
        private Text   _hudText;
        private bool   _hudVisible      = false;
        private const float HudScale    = 0.0004f;

        // ── Unity lifecycle ──────────────────────────────────────────────────

        private void Start()
        {
            GpuUtilLevel = -1f;
            CpuLevel     = -1f;

            CreateHud();

            if (showHudOnStart)
                ShowHud();
            else
                HideHud();
        }

        private void Update()
        {
            if (Time.unscaledDeltaTime > 0f)
            {
                CurrentFps       = 1f / Time.unscaledDeltaTime;
                _fpsAccumulator += CurrentFps;
                _fpsFrames++;
            }

            _sampleTimer += Time.unscaledDeltaTime;

            if (_sampleTimer >= sampleInterval)
            {
                AverageFps      = (_fpsFrames > 0) ? (_fpsAccumulator / _fpsFrames) : 0f;
                _fpsAccumulator = 0f;
                _fpsFrames      = 0;
                _sampleTimer    = 0f;

                SamplePlatformStats();
                CheckDropRecover();
            }

            if (_hudVisible)
                UpdateHud();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Toggle HUD visibility.</summary>
        public void ToggleHud()
        {
            if (_hudVisible) HideHud();
            else             ShowHud();
        }

        public void ShowHud() { _hudVisible = true;  _hudCanvas?.gameObject.SetActive(true); }
        public void HideHud() { _hudVisible = false; _hudCanvas?.gameObject.SetActive(false); }

        // ── Internal helpers ─────────────────────────────────────────────────

        private void SamplePlatformStats()
        {
#if HAS_META_XR
            // OVRManager exposes CPU/GPU level as integer 0-4 (power level, not utilization %).
            // GetGPUUtilLevel / GetCPULevel are static -- available without an OVRManager instance.
            GpuUtilLevel = OVRPlugin.gpuUtilSupported ? OVRPlugin.gpuUtilLevel : -1f;
            CpuLevel     = (float)OVRPlugin.cpuLevel;
#endif
        }

        private void CheckDropRecover()
        {
            if (!IsInDropState && AverageFps < dropThreshold)
            {
                IsInDropState = true;
                Debug.Log($"[PerformanceMonitor] PERFORMANCE DROP: avgFPS={AverageFps:F1} < threshold={dropThreshold}");
                OnPerformanceDrop?.Invoke(AverageFps);
            }
            else if (IsInDropState && AverageFps >= dropThreshold + recoveryHysteresis)
            {
                IsInDropState = false;
                Debug.Log($"[PerformanceMonitor] PERFORMANCE RECOVERED: avgFPS={AverageFps:F1}");
                OnPerformanceRecover?.Invoke(AverageFps);
            }
        }

        // ── HUD ──────────────────────────────────────────────────────────────

        private void UpdateHud()
        {
            // Anchor to head each frame.
            if (_headTransform == null) FindHead();

            if (_headTransform != null)
            {
                _hudCanvas.transform.position = _headTransform.position + _headTransform.forward * hudDistance;
                _hudCanvas.transform.rotation = _headTransform.rotation;
            }

            if (_hudText == null) return;

            string fpsColor  = AverageFps >= dropThreshold + recoveryHysteresis ? "#0f0"
                             : AverageFps >= dropThreshold                       ? "#ff0"
                             :                                                     "#f00";

            string dropLabel = IsInDropState ? " <color=#f00>[DROP]</color>" : "";

            string gpuStr = GpuUtilLevel >= 0f ? $"{GpuUtilLevel:F0}/4" : "N/A";
            string cpuStr = CpuLevel     >= 0f ? $"{CpuLevel:F0}/4"     : "N/A";

#if HAS_META_XR
            bool aswActive = SpaceWarpManager.Instance != null && SpaceWarpManager.Instance.IsSpaceWarpActive;
            string aswStr  = aswActive ? "<color=#0ff>ON</color>" : "<color=#888>OFF</color>";
#else
            string aswStr = "<color=#888>N/A</color>";
#endif

            _hudText.text =
                $"<b>=== PERF MONITOR ===</b>\n" +
                $"FPS now:  <color={fpsColor}>{CurrentFps:F0}</color>{dropLabel}\n" +
                $"FPS avg:  <color={fpsColor}>{AverageFps:F1}</color>\n" +
                $"GPU lvl:  {gpuStr}\n" +
                $"CPU lvl:  {cpuStr}\n" +
                $"ASW:      {aswStr}\n" +
                $"Frame:    {Time.frameCount}";
        }

        private void FindHead()
        {
#if HAS_META_XR
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig != null)
            {
                _headTransform = rig.centerEyeAnchor;
                return;
            }
#endif
            var cam = Camera.main;
            if (cam != null) _headTransform = cam.transform;
        }

        private void CreateHud()
        {
            var go = new GameObject("PerformanceMonitor_HUD");
            go.transform.SetParent(transform);

            _hudCanvas = go.AddComponent<Canvas>();
            _hudCanvas.renderMode = RenderMode.WorldSpace;
            _hudCanvas.sortingOrder = 9998;

            var rect = _hudCanvas.GetComponent<RectTransform>();
            rect.sizeDelta  = new Vector2(340, 200);
            rect.localScale = Vector3.one * HudScale;

            // Background panel
            var bgGo  = new GameObject("BG");
            bgGo.transform.SetParent(go.transform, false);
            var bg    = bgGo.AddComponent<Image>();
            bg.color  = new Color(0f, 0f, 0f, 0.7f);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin  = Vector2.zero;
            bgRect.anchorMax  = Vector2.one;
            bgRect.sizeDelta  = Vector2.zero;
            bgRect.offsetMin  = new Vector2(-6, -6);
            bgRect.offsetMax  = new Vector2(6, 6);

            // Text
            var txtGo = new GameObject("HudText");
            txtGo.transform.SetParent(go.transform, false);
            _hudText = txtGo.AddComponent<Text>();
            _hudText.font         = Font.CreateDynamicFontFromOSFont("Consolas", 18);
            _hudText.fontSize     = 18;
            _hudText.color        = Color.white;
            _hudText.alignment    = TextAnchor.UpperLeft;
            _hudText.supportRichText      = true;
            _hudText.horizontalOverflow   = HorizontalWrapMode.Overflow;
            _hudText.verticalOverflow     = VerticalWrapMode.Overflow;

            var txtRect = txtGo.GetComponent<RectTransform>();
            txtRect.anchorMin       = new Vector2(0f, 1f);
            txtRect.anchorMax       = new Vector2(0f, 1f);
            txtRect.pivot           = new Vector2(0f, 1f);
            txtRect.anchoredPosition = new Vector2(8f, -8f);
            txtRect.sizeDelta        = new Vector2(330, 190);

            var outline         = txtGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1, -1);
        }
    }
}
#endif // PLAGA44_FULL_SDK
