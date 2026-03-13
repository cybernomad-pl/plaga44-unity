// VRHealthDisplay.cs
// CYBERNOMAD -- Wrist-mounted health/stamina display.
// Attaches to the left hand (LeftHandAnchor or LeftControllerAnchor).
// Only visible when the player looks at their wrist (gaze dot-product check).
//
// Requires: com.meta.xr.sdk.core (HAS_META_XR define)

using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.UI
{
    public class VRHealthDisplay : MonoBehaviour
    {
        // ---- Public state ----

        public static VRHealthDisplay Instance { get; private set; }

        [Range(0f, 1f)] public float Health  = 1.0f;
        [Range(0f, 1f)] public float Stamina = 1.0f;

        // ---- Config ----

        private const float CANVAS_SCALE    = 0.0004f;
        private const int   CANVAS_W        = 300;
        private const int   CANVAS_H        = 120;

        // How closely the player must look at the wrist (dot product, 1=direct look, -1=away)
        private const float GAZE_THRESHOLD  = 0.75f;
        private const float FADE_SPEED      = 4.0f;

        // Local offset from wrist anchor so the display floats above the back of the hand
        private static readonly Vector3 WRIST_OFFSET = new Vector3(0f, 0.04f, 0.02f);

        // Colours
        private static readonly Color BG_COLOR    = new Color(0.10f, 0.10f, 0.10f, 0.85f);
        private static readonly Color HP_COLOR     = new Color(0.88f, 0.22f, 0.22f, 1.00f);
        private static readonly Color STAM_COLOR   = new Color(0.22f, 0.65f, 0.88f, 1.00f);
        private static readonly Color BAR_BG_COLOR = new Color(0.18f, 0.18f, 0.18f, 1.00f);
        private static readonly Color ACCENT       = new Color(1.00f, 0.42f, 0.21f, 1.00f);
        private static readonly Color TEXT_WHITE   = Color.white;

#if HAS_META_XR
        private OVRCameraRig _rig;
#endif

        private Canvas   _canvas;
        private CanvasGroup _canvasGroup;
        private Transform _wristAnchor;

        private RectTransform _hpFill;
        private RectTransform _stamFill;
        private Text _hpText;
        private Text _stamText;

        // ---- Lifecycle ----

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            BuildCanvas();
        }

        private void Update()
        {
#if HAS_META_XR
            if (_rig == null) _rig = FindFirstObjectByType<OVRCameraRig>();
            if (_rig == null) return;

            ResolveWristAnchor();
            if (_wristAnchor == null) return;

            PositionOnWrist();
            UpdateGazeVisibility();
            UpdateBars();
#endif
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---- Wrist attachment ----

#if HAS_META_XR
        private void ResolveWristAnchor()
        {
            if (_wristAnchor != null) return;

            // Prefer hand tracking anchor, fall back to controller anchor
            _wristAnchor = FindDescendant(_rig.transform, "LeftHandAnchor")
                        ?? FindDescendant(_rig.transform, "LeftControllerAnchor");
        }

        private void PositionOnWrist()
        {
            _canvas.transform.position = _wristAnchor.TransformPoint(WRIST_OFFSET);

            // Face toward head camera
            var toHead = _rig.centerEyeAnchor.position - _canvas.transform.position;
            if (toHead.sqrMagnitude > 0.0001f)
                _canvas.transform.rotation = Quaternion.LookRotation(-toHead.normalized, Vector3.up);
        }

        private void UpdateGazeVisibility()
        {
            var head    = _rig.centerEyeAnchor;
            var toWrist = (_canvas.transform.position - head.position).normalized;
            float dot   = Vector3.Dot(head.forward, toWrist);

            float targetAlpha = dot >= GAZE_THRESHOLD ? 1.0f : 0.0f;
            _canvasGroup.alpha = Mathf.MoveTowards(
                _canvasGroup.alpha, targetAlpha, FADE_SPEED * Time.deltaTime);
        }
#endif

        private void UpdateBars()
        {
            // Health bar fill
            if (_hpFill != null)
            {
                var a = _hpFill.anchorMax;
                a.x = Mathf.Clamp01(Health);
                _hpFill.anchorMax = a;
            }

            // Stamina bar fill
            if (_stamFill != null)
            {
                var a = _stamFill.anchorMax;
                a.x = Mathf.Clamp01(Stamina);
                _stamFill.anchorMax = a;
            }

            // Percentage text
            if (_hpText  != null) _hpText.text   = $"{Mathf.RoundToInt(Health  * 100)}%";
            if (_stamText != null) _stamText.text = $"{Mathf.RoundToInt(Stamina * 100)}%";
        }

        // ---- Canvas construction ----

        private void BuildCanvas()
        {
            var go = new GameObject("VRHealthDisplay_Canvas");
            go.transform.SetParent(transform);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta  = new Vector2(CANVAS_W, CANVAS_H);
            rt.localScale = Vector3.one * CANVAS_SCALE;

            _canvasGroup = go.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            // Background
            CreateImage(go.transform, "BG",
                Vector2.zero, new Vector2(CANVAS_W, CANVAS_H), BG_COLOR);

            // Corner accent line
            CreateImage(go.transform, "AccentLine",
                new Vector2(-CANVAS_W * 0.5f + 3, 0), new Vector2(6, CANVAS_H), ACCENT);

            // ----- Health row -----
            CreateLabel(go.transform, "HPLabel",
                new Vector2(-80, 28), "HP", 22);

            _hpFill = CreateBar(go.transform, "HPBar",
                new Vector2(30, 28), new Vector2(160, 20), HP_COLOR, out _);

            _hpText = CreateValueText(go.transform, "HPVal",
                new Vector2(128, 28));

            // ----- Stamina row -----
            CreateLabel(go.transform, "StamLabel",
                new Vector2(-80, -8), "STM", 22);

            _stamFill = CreateBar(go.transform, "StamBar",
                new Vector2(30, -8), new Vector2(160, 20), STAM_COLOR, out _);

            _stamText = CreateValueText(go.transform, "StamVal",
                new Vector2(128, -8));

            // ----- Player label -----
            CreateLabel(go.transform, "NameLabel",
                new Vector2(0, -40), "BORYS KOWALSKI", 16);
        }

        // ---- Helpers ----

        private void CreateImage(Transform parent, string name,
            Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        /// <summary>
        /// Creates a bar with a background track and a fill rect that spans anchor 0->1.
        /// Returns the fill RectTransform so we can animate anchorMax.x.
        /// </summary>
        private RectTransform CreateBar(Transform parent, string name,
            Vector2 pos, Vector2 size, Color fillColor, out Image fillImg)
        {
            // Track
            var track = new GameObject(name + "_Track");
            track.transform.SetParent(parent, false);
            var trackRt = track.AddComponent<RectTransform>();
            trackRt.anchorMin = trackRt.anchorMax = new Vector2(0.5f, 0.5f);
            trackRt.pivot = new Vector2(0.5f, 0.5f);
            trackRt.anchoredPosition = pos;
            trackRt.sizeDelta = size;
            var trackImg = track.AddComponent<Image>();
            trackImg.color = BAR_BG_COLOR;
            trackImg.raycastTarget = false;

            // Fill
            var fill = new GameObject(name + "_Fill");
            fill.transform.SetParent(track.transform, false);
            var fillRt = fill.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;  // starts full; adjusted in UpdateBars
            fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
            fillImg = fill.AddComponent<Image>();
            fillImg.color = fillColor;
            fillImg.raycastTarget = false;

            return fillRt;
        }

        private void CreateLabel(Transform parent, string name, Vector2 pos,
            string text, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(100, 28);

            var txt = go.AddComponent<Text>();
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            txt.fontSize = fontSize;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = TEXT_WHITE;
            txt.raycastTarget = false;
            txt.text = text;
        }

        private Text CreateValueText(Transform parent, string name, Vector2 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(pos.x + 50, pos.y);
            rt.sizeDelta = new Vector2(60, 28);

            var txt = go.AddComponent<Text>();
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", 20);
            txt.fontSize = 20;
            txt.alignment = TextAnchor.MiddleRight;
            txt.color = ACCENT;
            txt.raycastTarget = false;
            txt.text = "100%";
            return txt;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (child.name == name) return child;
                var found = FindDescendant(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
