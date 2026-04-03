// VRNotification.cs
// CYBERNOMAD -- World-space popup notification system.
// Notifications appear above the player's forward view, fade in, hold, then fade out.
// Supports a queue so rapid events don't overlap. Each notification can have a style.
//
// Requires: com.meta.xr.sdk.core (HAS_META_XR define)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.UI
{
    public class VRNotification : MonoBehaviour
    {
        // ---- Notification styles ----

        public enum NotifStyle
        {
            Default,   // white text, orange accent
            Gold,      // gold text -- streaks and headshots
            Warning,   // red -- danger events
            Info       // blue-grey -- neutral events
        }

        // ---- Public API ----

        public static VRNotification Instance { get; private set; }

        /// <summary>Show a notification. Queued if one is already displaying.</summary>
        public void Show(string message, NotifStyle style = NotifStyle.Default,
            float holdSeconds = 1.8f)
        {
            _queue.Enqueue(new NotifData { message = message, style = style, hold = holdSeconds });
            if (!_isShowing) StartCoroutine(DrainQueue());
        }

        // ---- Config ----

        private const float CANVAS_SCALE  = 0.001f;
        private const int   CANVAS_W      = 400;
        private const int   CANVAS_H      = 80;
        private const float FADE_IN_TIME  = 0.20f;
        private const float FADE_OUT_TIME = 0.35f;

        // Vertical offset above eye level (in metres)
        private const float HEIGHT_OFFSET = 0.35f;
        // Forward distance from head
        private const float FORWARD_DIST  = 1.8f;

        // Colours per style
        private static readonly Color C_DEFAULT = new Color(1.00f, 0.42f, 0.21f, 1.00f); // orange
        private static readonly Color C_GOLD    = new Color(1.00f, 0.84f, 0.00f, 1.00f);
        private static readonly Color C_WARN    = new Color(0.90f, 0.20f, 0.20f, 1.00f);
        private static readonly Color C_INFO    = new Color(0.45f, 0.72f, 0.90f, 1.00f);
        private static readonly Color BG_COLOR  = new Color(0.08f, 0.08f, 0.08f, 0.90f);

        // ---- Private ----

        private struct NotifData
        {
            public string message;
            public NotifStyle style;
            public float hold;
        }

        private readonly Queue<NotifData> _queue = new Queue<NotifData>();
        private bool _isShowing;

#if HAS_META_XR
        private OVRCameraRig _rig;
#endif

        private Canvas      _canvas;
        private CanvasGroup _group;
        private Text        _label;
        private Image       _bgImg;
        private Image       _accentBar;

        // ---- Lifecycle ----

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            BuildCanvas();
            _canvas.gameObject.SetActive(false);
        }

        private void Update()
        {
#if HAS_META_XR
            if (_rig == null) _rig = FindFirstObjectByType<OVRCameraRig>();
#endif
            // Keep canvas facing player if visible
            if (_canvas.gameObject.activeSelf) PositionCanvas();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            StopAllCoroutines();
        }

        // ---- Queue processing ----

        private IEnumerator DrainQueue()
        {
            _isShowing = true;
            while (_queue.Count > 0)
            {
                var data = _queue.Dequeue();
                yield return ShowOne(data);
            }
            _isShowing = false;
            _canvas.gameObject.SetActive(false);
        }

        private IEnumerator ShowOne(NotifData data)
        {
            // Apply style
            _label.text  = data.message;
            _label.color = StyleToColor(data.style);

            // Position before making visible
            PositionCanvas();
            _canvas.gameObject.SetActive(true);

            // Fade in
            yield return Fade(0f, 1f, FADE_IN_TIME);

            // Hold
            yield return new WaitForSeconds(data.hold);

            // Fade out
            yield return Fade(1f, 0f, FADE_OUT_TIME);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _group.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            _group.alpha = to;
        }

        // ---- Placement ----

        private void PositionCanvas()
        {
#if HAS_META_XR
            if (_rig == null) return;
            var head = _rig.centerEyeAnchor;
            Vector3 forward = head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            _canvas.transform.position = head.position
                + forward * FORWARD_DIST
                + Vector3.up * HEIGHT_OFFSET;

            _canvas.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
#else
            _canvas.transform.position = new Vector3(0f, 1.9f, FORWARD_DIST);
            _canvas.transform.rotation = Quaternion.identity;
#endif
        }

        // ---- Style helpers ----

        private Color StyleToColor(NotifStyle style)
        {
            switch (style)
            {
                case NotifStyle.Gold:    return C_GOLD;
                case NotifStyle.Warning: return C_WARN;
                case NotifStyle.Info:    return C_INFO;
                default:                 return C_DEFAULT;
            }
        }

        // ---- Canvas construction ----

        private void BuildCanvas()
        {
            var go = new GameObject("VRNotif_Canvas");
            go.transform.SetParent(transform);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta  = new Vector2(CANVAS_W, CANVAS_H);
            rt.localScale = Vector3.one * CANVAS_SCALE;

            _group = go.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable   = false;
            _group.blocksRaycasts = false;

            // Background
            var bgGo = new GameObject("BG");
            bgGo.transform.SetParent(go.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.sizeDelta = new Vector2(CANVAS_W, CANVAS_H);
            _bgImg = bgGo.AddComponent<Image>();
            _bgImg.color = BG_COLOR;
            _bgImg.raycastTarget = false;

            // Accent bar (top edge)
            var accGo = new GameObject("AccentBar");
            accGo.transform.SetParent(go.transform, false);
            var accRt = accGo.AddComponent<RectTransform>();
            accRt.anchorMin = accRt.anchorMax = new Vector2(0.5f, 0.5f);
            accRt.pivot = new Vector2(0.5f, 0.5f);
            accRt.anchoredPosition = new Vector2(0, CANVAS_H * 0.5f - 3);
            accRt.sizeDelta = new Vector2(CANVAS_W, 6);
            _accentBar = accGo.AddComponent<Image>();
            _accentBar.color = C_DEFAULT;
            _accentBar.raycastTarget = false;

            // Message text
            var txtGo = new GameObject("MessageText");
            txtGo.transform.SetParent(go.transform, false);
            var txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = txtRt.anchorMax = new Vector2(0.5f, 0.5f);
            txtRt.pivot = new Vector2(0.5f, 0.5f);
            txtRt.anchoredPosition = new Vector2(0, -4);
            txtRt.sizeDelta = new Vector2(CANVAS_W - 20, CANVAS_H - 16);

            _label = txtGo.AddComponent<Text>();
            _label.font = Font.CreateDynamicFontFromOSFont("Arial", 40);
            _label.fontSize = 40;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = C_DEFAULT;
            _label.fontStyle = FontStyle.Bold;
            _label.raycastTarget = false;
        }
    }
}
