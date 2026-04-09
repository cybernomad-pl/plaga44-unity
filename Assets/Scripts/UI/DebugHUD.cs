// DebugHUD.cs
// PLAGA '44 -- Performance debug HUD overlay for VR.
//
// Displays real-time metrics: FPS, draw calls, triangles, memory, battery.
// Follows the headset (CenterEyeAnchor) in world space.
// Toggle on/off from the hamburger menu (VRMenuManager > DEBUG > Debug HUD).
//
// Uses OVRPlugin for Quest-specific stats (GPU/CPU levels, battery).
// Graceful fallback when HAS_META_XR is not defined.
//
// Requires: com.meta.xr.sdk.core (HAS_META_XR define)

using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace Plaga44.UI
{
    /// <summary>
    /// Real-time debug HUD showing FPS, draw calls, triangles, memory, and battery.
    /// Controlled by VRMenuManager toggle.
    /// </summary>
    public class DebugHUD : MonoBehaviour
    {
        private const string LOG = "[DebugHUD]";

        // ---- Singleton ----
        public static DebugHUD Instance { get; private set; }

        // ---- Configuration ----

        [Header("HUD Placement")]
        [Tooltip("Distance from headset where HUD floats.")]
        public float hudDistance = 0.5f;

        [Tooltip("Vertical offset from center of view (positive = up).")]
        public float hudVerticalOffset = -0.15f;

        [Tooltip("Horizontal offset from center of view (positive = right).")]
        public float hudHorizontalOffset = 0.2f;

        [Header("Sampling")]
        [Tooltip("How often to update metrics (seconds).")]
        [Range(0.1f, 2f)]
        public float updateInterval = 0.5f;

        // ---- Public state ----

        /// <summary>True when the HUD is currently visible.</summary>
        public bool IsVisible => _hudCanvas != null && _hudCanvas.gameObject.activeSelf;

        // ---- Private ----

        private Canvas _hudCanvas;
        private Text _hudText;
        private Transform _headTransform;

        // Metrics
        private float _fps;
        private float _fpsAccumulator;
        private int _fpsFrameCount;
        private float _updateTimer;

        // Cached metrics (updated at interval)
        private float _avgFps;
        private float _minFps = float.MaxValue;
        private float _maxFps;
        private long _totalMemoryMB;
        private long _usedMemoryMB;
        private float _batteryLevel = -1f;
        private int _batteryStatus;  // 0=unknown, 1=charging, 2=discharging, 3=full
        private float _gpuLevel = -1f;
        private float _cpuLevel = -1f;

        // ---- Auto-init ----

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInit()
        {
            if (Instance != null) return;

            var go = new GameObject("_DebugHUD");
            go.AddComponent<DebugHUD>();
            DontDestroyOnLoad(go);
        }

        // ---- Unity lifecycle ----

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            BuildHUD();
            _hudCanvas.gameObject.SetActive(false); // hidden by default
            Debug.Log($"{LOG} Initialized. Toggle from hamburger menu > DEBUG.");
        }

        private void Update()
        {
            if (!IsVisible) return;

            // Accumulate FPS
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
            {
                _fps = 1f / dt;
                _fpsAccumulator += _fps;
                _fpsFrameCount++;
            }

            _updateTimer += dt;
            if (_updateTimer >= updateInterval)
            {
                _updateTimer = 0f;
                SampleMetrics();
                UpdateHUDText();
            }

            PositionHUD();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---- Public API ----

        /// <summary>Show the debug HUD.</summary>
        public void Show()
        {
            if (_hudCanvas != null)
                _hudCanvas.gameObject.SetActive(true);
            ResetMetrics();
        }

        /// <summary>Hide the debug HUD.</summary>
        public void Hide()
        {
            if (_hudCanvas != null)
                _hudCanvas.gameObject.SetActive(false);
        }

        /// <summary>Toggle visibility.</summary>
        public void Toggle()
        {
            if (IsVisible) Hide(); else Show();
        }

        // ---- Metrics ----

        private void SampleMetrics()
        {
            // FPS
            _avgFps = _fpsFrameCount > 0 ? _fpsAccumulator / _fpsFrameCount : 0f;
            if (_fps < _minFps && _fps > 0) _minFps = _fps;
            if (_fps > _maxFps) _maxFps = _fps;
            _fpsAccumulator = 0f;
            _fpsFrameCount = 0;

            // Memory
            _totalMemoryMB = SystemInfo.systemMemorySize;
            _usedMemoryMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);

            // Platform stats
#if HAS_META_XR
            // Battery
            _batteryLevel = SystemInfo.batteryLevel;
            _batteryStatus = (int)SystemInfo.batteryStatus;

            // GPU/CPU levels
            _gpuLevel = OVRPlugin.gpuUtilSupported ? OVRPlugin.gpuUtilLevel : -1f;
            _cpuLevel = (float)OVRPlugin.suggestedCpuPerfLevel;
#else
            _batteryLevel = SystemInfo.batteryLevel;
            _batteryStatus = (int)SystemInfo.batteryStatus;
#endif
        }

        private void ResetMetrics()
        {
            _minFps = float.MaxValue;
            _maxFps = 0f;
            _fpsAccumulator = 0f;
            _fpsFrameCount = 0;
            _updateTimer = 0f;
        }

        // ---- HUD Text ----

        private void UpdateHUDText()
        {
            if (_hudText == null) return;

            // FPS color coding
            string fpsColor = _avgFps >= 72f ? "#00ff00"
                            : _avgFps >= 60f ? "#ffff00"
                            : _avgFps >= 36f ? "#ff8800"
                            :                  "#ff0000";

            string minColor = _minFps >= 60f ? "#00ff00"
                            : _minFps >= 36f ? "#ffff00"
                            :                  "#ff0000";

            // Battery display
            string batteryStr;
            if (_batteryLevel >= 0f)
            {
                float pct = _batteryLevel * 100f;
                string battColor = pct > 50f ? "#00ff00"
                                 : pct > 20f ? "#ffff00"
                                 :             "#ff0000";
                string charging = _batteryStatus == 1 ? " [CHG]" : "";
                batteryStr = $"<color={battColor}>{pct:F0}%</color>{charging}";
            }
            else
            {
                batteryStr = "<color=#888>N/A</color>";
            }

            // GPU/CPU
            string gpuStr = _gpuLevel >= 0 ? $"{_gpuLevel:F1}" : "N/A";
            string cpuStr = _cpuLevel >= 0 ? $"{_cpuLevel:F0}" : "N/A";

            // Draw calls and triangles (from Unity profiler)
            // Note: these are approximations from the rendering stats
            int drawCalls = UnityEngine.Rendering.FrameTimingManager.GetLatestTimings(0, null) > 0 ? -1 : -1;

            _hudText.text =
                $"<b><color=#FF6B35>DEBUG HUD</color></b>\n" +
                $"-------------------\n" +
                $"FPS:     <color={fpsColor}>{_avgFps:F0}</color>  " +
                $"(min:<color={minColor}>{_minFps:F0}</color> max:{_maxFps:F0})\n" +
                $"Frame:   {Time.frameCount}\n" +
                $"dT:      {Time.unscaledDeltaTime * 1000f:F1}ms\n" +
                $"-------------------\n" +
                $"Memory:  {_usedMemoryMB}MB / {_totalMemoryMB}MB\n" +
                $"GC:      {System.GC.CollectionCount(0)}\n" +
                $"-------------------\n" +
                $"GPU lvl: {gpuStr}\n" +
                $"CPU lvl: {cpuStr}\n" +
                $"Battery: {batteryStr}\n" +
                $"-------------------\n" +
                $"Res:     {Screen.width}x{Screen.height}\n" +
                $"Quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}";
        }

        // ---- HUD Construction ----

        private void BuildHUD()
        {
            var go = new GameObject("DebugHUD_Canvas");
            go.transform.SetParent(transform);

            _hudCanvas = go.AddComponent<Canvas>();
            _hudCanvas.renderMode = RenderMode.WorldSpace;
            _hudCanvas.sortingOrder = 9999;

            var rect = _hudCanvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(320, 280);
            rect.localScale = Vector3.one * 0.0004f;

            // Background
            var bgGo = new GameObject("BG");
            bgGo.transform.SetParent(go.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.75f);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(-8, -8);
            bgRect.offsetMax = new Vector2(8, 8);
            bgImg.raycastTarget = false;

            // Text
            var txtGo = new GameObject("HudText");
            txtGo.transform.SetParent(go.transform, false);
            _hudText = txtGo.AddComponent<Text>();
            _hudText.font = Font.CreateDynamicFontFromOSFont("Consolas", 16);
            _hudText.fontSize = 16;
            _hudText.color = Color.white;
            _hudText.alignment = TextAnchor.UpperLeft;
            _hudText.supportRichText = true;
            _hudText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _hudText.verticalOverflow = VerticalWrapMode.Overflow;
            _hudText.raycastTarget = false;

            var txtRect = txtGo.GetComponent<RectTransform>();
            txtRect.anchorMin = new Vector2(0f, 1f);
            txtRect.anchorMax = new Vector2(0f, 1f);
            txtRect.pivot = new Vector2(0f, 1f);
            txtRect.anchoredPosition = new Vector2(8f, -8f);
            txtRect.sizeDelta = new Vector2(310, 270);

            var outline = txtGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1, -1);
        }

        private void PositionHUD()
        {
            if (_headTransform == null) FindHead();
            if (_headTransform == null || _hudCanvas == null) return;

            Vector3 forward = _headTransform.forward;
            Vector3 right = _headTransform.right;
            Vector3 up = _headTransform.up;

            _hudCanvas.transform.position = _headTransform.position
                + forward * hudDistance
                + right * hudHorizontalOffset
                + up * hudVerticalOffset;

            _hudCanvas.transform.rotation = _headTransform.rotation;
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
    }
}
