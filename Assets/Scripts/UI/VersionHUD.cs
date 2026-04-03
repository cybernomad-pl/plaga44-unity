using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.UI
{
    /// <summary>
    /// Always-visible version stamp in the top-left corner of the VR view.
    /// Follows the camera at a fixed distance, anchored top-left.
    /// Auto-initializes -- just add to any GameObject or let SceneDefaults pick it up.
    /// </summary>
    public class VersionHUD : MonoBehaviour
    {
        [Header("Version String")]
        [Tooltip("Override version text. If empty, uses build timestamp.")]
        public string versionOverride = "";

        [Header("Position")]
        [Tooltip("Distance from camera in metres.")]
        [Range(0.5f, 3f)]
        public float displayDistance = 2f;

        [Tooltip("Offset from camera centre (left/up). Applied in view space.")]
        public Vector2 cornerOffset = new Vector2(-0.55f, 0.35f);

        [Header("Visual")]
        [Range(8, 24)]
        public int fontSize = 14;
        public Color textColor = new Color(1f, 1f, 1f, 0.5f);

        private GameObject _canvasGO;
        private Text _label;
        private Transform _camT;

        // ── Build stamp generated at compile time ────────────────────────
        // Format: PLAGA '44 TECH DEMO, v.YYYYMMDD_HH.MM
        private const string GAME_TITLE = "PLAGA '44 TECH DEMO";

        void Start()
        {
            BuildCanvas();
            _camT = Camera.main?.transform;
        }

        void LateUpdate()
        {
            if (_camT == null)
            {
                _camT = Camera.main?.transform;
                if (_camT == null) return;
            }

            // Follow camera -- top-left corner
            Vector3 forward = _camT.forward;
            Vector3 right   = _camT.right;
            Vector3 up      = _camT.up;

            Vector3 pos = _camT.position
                + forward * displayDistance
                + right   * cornerOffset.x
                + up      * cornerOffset.y;

            _canvasGO.transform.position = pos;
            _canvasGO.transform.rotation = Quaternion.LookRotation(forward, up);
        }

        void BuildCanvas()
        {
            // World-space canvas
            _canvasGO = new GameObject("VersionHUD_Canvas");
            _canvasGO.transform.SetParent(transform, false);

            var canvas = _canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 999;

            var rt = _canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(500, 40);
            rt.localScale = Vector3.one * 0.001f; // 1px = 1mm

            // Text
            var textGO = new GameObject("VersionLabel");
            textGO.transform.SetParent(_canvasGO.transform, false);

            _label = textGO.AddComponent<Text>();
            _label.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            _label.fontSize = fontSize;
            _label.color = textColor;
            _label.alignment = TextAnchor.UpperLeft;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.raycastTarget = false;

            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0, 1);
            textRT.anchorMax = new Vector2(1, 1);
            textRT.pivot = new Vector2(0, 1);
            textRT.anchoredPosition = Vector2.zero;
            textRT.sizeDelta = new Vector2(500, 40);

            // Set version string
            string version = !string.IsNullOrEmpty(versionOverride)
                ? versionOverride
                : $"{GAME_TITLE}, v.{Application.version}";

            _label.text = version;
        }
    }
}
