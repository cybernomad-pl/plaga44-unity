// AUTO-DISABLED: PLAGA44_FULL_SDK
#if PLAGA44_FULL_SDK
// InputDebugOverlay.cs
// CYBERNOMAD -- Real-time debug HUD showing detected microgestures.
//
// Shows:
//   - Last N gesture events with timestamps (swipe direction, tap, hand)
//   - Live thumbstick values for both hands
//   - Current command resolved by GestureCommandMap (if assigned)
//   - Cooldown timers
//
// Attach to any GameObject. Requires MicrogestureManager in the scene.
// Toggle visibility via menu: CYBERNOMAD > Debug > Gesture Debug Overlay
// (editor menu wired up separately in VRInputDebugMenu.cs)
//
// #if HAS_META_XR guards OVRInput reads.
// Namespace: Plaga44.Input

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.Input
{
    /// <summary>
    /// World-space HUD that displays microgesture events in real time for debugging.
    /// </summary>
    public class InputDebugOverlay : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("Source of gesture events. Auto-found via Instance if left empty.")]
        public MicrogestureManager gestureManager;

        [Tooltip("Optional command map to show resolved commands.")]
        public GestureCommandMap commandMap;

        [Header("HUD Position")]
        [Tooltip("Distance from camera in metres.")]
        [Range(0.2f, 2f)]
        public float displayDistance = 0.5f;

        [Tooltip("Offset from camera centre in view space (metres).")]
        public Vector3 viewOffset = new Vector3(0.3f, 0f, 0f);

        [Header("Event Log")]
        [Tooltip("How many recent gesture events to keep in the log.")]
        [Range(3, 20)]
        public int logCapacity = 8;

        [Tooltip("Seconds before a log entry fades out.")]
        [Range(1f, 10f)]
        public float entryLifetime = 4f;

        [Header("Visual")]
        [Range(10, 28)]
        public int fontSize = 14;

        public Color headerColor  = new Color(0f,   1f,   0f,   1f);
        public Color swipeColor   = new Color(0.2f, 0.8f, 1f,   1f);
        public Color tapColor     = new Color(1f,   0.8f, 0.0f, 1f);
        public Color commandColor = new Color(1f,   0.5f, 0.0f, 1f);
        public Color staleColor   = new Color(0.4f, 0.4f, 0.4f, 1f);

        // ── Private ───────────────────────────────────────────────────────────

        private struct LogEntry
        {
            public string text;
            public float  timestamp;
            public Color  color;
        }

        private readonly List<LogEntry> _log = new List<LogEntry>();

        private Canvas    _canvas;
        private Text      _headerText;
        private Text      _logText;
        private Text      _liveText;
        private Transform _cameraTransform;

        private bool _subscribed;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            BuildUI();
            TrySubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
            FindCamera();
            PositionHUD();
            UpdateLiveText();
            UpdateLogText();
        }

        // ── Subscription ──────────────────────────────────────────────────────

        private void TrySubscribe()
        {
            if (_subscribed) return;

            if (gestureManager == null)
                gestureManager = MicrogestureManager.Instance;

            if (gestureManager == null) return;

            gestureManager.OnSwipe += HandleSwipe;
            gestureManager.OnTap   += HandleTap;
            _subscribed = true;

            if (commandMap != null)
                commandMap.OnCommand += HandleCommand;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (gestureManager != null)
            {
                gestureManager.OnSwipe -= HandleSwipe;
                gestureManager.OnTap   -= HandleTap;
            }
            if (commandMap != null)
                commandMap.OnCommand -= HandleCommand;
            _subscribed = false;
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void HandleSwipe(SwipeDirection direction, Hand hand)
        {
            string handStr = hand == Hand.Left ? "L" : "R";
            AddEntry($"SWIPE {direction} [{handStr}]", swipeColor);
        }

        private void HandleTap(Hand hand)
        {
            string handStr = hand == Hand.Left ? "L" : "R";
            AddEntry($"TAP [{handStr}]", tapColor);
        }

        private void HandleCommand(GestureCommand command, Hand hand)
        {
            string handStr = hand == Hand.Left ? "L" : "R";
            AddEntry($"=> {command} [{handStr}]", commandColor);
        }

        // ── Log management ────────────────────────────────────────────────────

        private void AddEntry(string text, Color color)
        {
            // Try subscribe lazily (manager may not exist at Start)
            TrySubscribe();

            _log.Insert(0, new LogEntry
            {
                text      = $"[{Time.time:F1}s] {text}",
                timestamp = Time.time,
                color     = color
            });

            while (_log.Count > logCapacity)
                _log.RemoveAt(_log.Count - 1);
        }

        // ── Update helpers ────────────────────────────────────────────────────

        private void FindCamera()
        {
            if (_cameraTransform != null) return;

#if HAS_META_XR
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig != null)
            {
                _cameraTransform = rig.centerEyeAnchor;
                return;
            }
#endif
            var cam = Camera.main;
            if (cam != null) _cameraTransform = cam.transform;
        }

        private void PositionHUD()
        {
            if (_cameraTransform == null) return;

            Vector3 worldOffset = _cameraTransform.TransformDirection(viewOffset);
            Vector3 target = _cameraTransform.position
                + _cameraTransform.forward * displayDistance
                + worldOffset;

            _canvas.transform.position = target;
            _canvas.transform.rotation = Quaternion.LookRotation(
                target - _cameraTransform.position,
                Vector3.up);
        }

        private void UpdateLiveText()
        {
            // Try subscribe lazily
            TrySubscribe();

#if HAS_META_XR
            Vector2 leftStick  = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
            Vector2 rightStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
            bool    leftTouch  = OVRInput.Get(OVRInput.Touch.PrimaryThumbstick,  OVRInput.Controller.LTouch);
            bool    rightTouch = OVRInput.Get(OVRInput.Touch.PrimaryThumbstick,  OVRInput.Controller.RTouch);

            string leftTouchStr  = leftTouch  ? "<color=#0f0>[T]</color>" : "   ";
            string rightTouchStr = rightTouch ? "<color=#0f0>[T]</color>" : "   ";

            _liveText.text =
                $"<b>LIVE</b>\n" +
                $"L {leftTouchStr} {leftStick.x:+0.00;-0.00} {leftStick.y:+0.00;-0.00}\n" +
                $"R {rightTouchStr} {rightStick.x:+0.00;-0.00} {rightStick.y:+0.00;-0.00}";
#else
            _liveText.text = "<b>LIVE</b>\n<color=#888>HAS_META_XR not defined</color>";
#endif
        }

        private void UpdateLogText()
        {
            // Prune expired entries
            float now = Time.time;
            _log.RemoveAll(e => (now - e.timestamp) > entryLifetime);

            if (_log.Count == 0)
            {
                _logText.text = "<color=#444>-- no gestures yet --</color>";
                return;
            }

            var sb = new System.Text.StringBuilder();
            foreach (var entry in _log)
            {
                float age      = now - entry.timestamp;
                float fade     = 1f - Mathf.Clamp01(age / entryLifetime);
                Color c        = Color.Lerp(staleColor, entry.color, fade);
                string hexColor = ColorUtility.ToHtmlStringRGB(c);
                sb.AppendLine($"<color=#{hexColor}>{entry.text}</color>");
            }
            _logText.text = sb.ToString().TrimEnd();
        }

        // ── UI building ───────────────────────────────────────────────────────

        private void BuildUI()
        {
            // Root canvas
            var canvasGO = new GameObject("GestureDebug_Canvas");
            canvasGO.transform.SetParent(transform, false);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            var rect = _canvas.GetComponent<RectTransform>();
            rect.sizeDelta  = new Vector2(380, 300);
            rect.localScale = Vector3.one * 0.00035f;

            // Panel background
            var bgGO  = new GameObject("Background");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.sizeDelta        = new Vector2(380, 300);
            bgRect.anchoredPosition = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.6f);

            // Header
            _headerText = CreateLabel(canvasGO.transform, "Header",
                new Vector2(0, 130), new Vector2(370, 30),
                $"<color=#{ColorUtility.ToHtmlStringRGB(headerColor)}><b>GESTURE DEBUG</b></color>",
                TextAnchor.UpperCenter, fontSize + 2);

            // Live thumb state
            _liveText = CreateLabel(canvasGO.transform, "LiveState",
                new Vector2(0, 95), new Vector2(370, 55),
                "", TextAnchor.UpperCenter, fontSize);

            // Event log
            _logText = CreateLabel(canvasGO.transform, "EventLog",
                new Vector2(0, 30), new Vector2(370, 160),
                "<color=#444>-- no gestures yet --</color>", TextAnchor.UpperLeft, fontSize);
        }

        private Text CreateLabel(Transform parent, string name,
            Vector2 anchoredPos, Vector2 size,
            string initialText, TextAnchor anchor, int fs)
        {
            var go   = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta        = size;

            var text = go.AddComponent<Text>();
            text.font              = Font.CreateDynamicFontFromOSFont("Consolas", fs);
            text.fontSize          = fs;
            text.color             = Color.white;
            text.alignment         = anchor;
            text.supportRichText   = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow   = VerticalWrapMode.Overflow;
            text.text              = initialText;

            var outline = go.AddComponent<Outline>();
            outline.effectColor    = new Color(0f, 0f, 0f, 0.7f);
            outline.effectDistance = new Vector2(1, -1);

            return text;
        }
    }
}
#endif // PLAGA44_FULL_SDK
