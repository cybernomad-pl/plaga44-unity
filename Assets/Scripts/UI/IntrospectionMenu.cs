// IntrospectionMenu.cs
// PLAGA '44 -- Look-down body status UI.
// Player looks down at their own body (head pitch < -45 deg for 1s)
// and the Introspection Menu opens -- literally "looking inside yourself".
//
// Shows: character stats, equipment quick view, body condition, mental state.
// World-space canvas at chest height, facing camera. Does NOT pause game.
//
// Requires: com.meta.xr.sdk.core (HAS_META_XR define)

using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.UI
{
    /// <summary>
    /// Menu Introspekcji -- player looks down at their body, menu appears.
    /// Literally "looking inside yourself".
    ///
    /// Trigger: head pitch below threshold for holdTime seconds.
    /// Close:   head pitch above closeThreshold.
    /// UI:      world-space canvas at chest height, facing camera.
    /// Does NOT pause game.
    /// </summary>
    public class IntrospectionMenu : MonoBehaviour
    {
        [Header("Trigger")]
        [Tooltip("Head pitch angle (degrees) to trigger menu. Negative = looking down.")]
        public float openPitchThreshold = -45f;

        [Tooltip("Hold time in seconds before menu opens.")]
        public float holdTime = 1.0f;

        [Tooltip("Head pitch angle to close menu (looking back up).")]
        public float closePitchThreshold = -20f;

        [Header("UI Position")]
        [Tooltip("Vertical offset from head (negative = below).")]
        public float chestOffsetY = -0.4f;

        [Tooltip("Forward offset from head.")]
        public float chestOffsetZ = 0.35f;

        [Header("Fade")]
        [Tooltip("Time in seconds for the menu to fade in/out.")]
        public float fadeDuration = 0.25f;

        // ── State ──
        private bool _isOpen;
        private float _lookDownTimer;
        private float _fadeAlpha;
        private GameObject _canvasGO;
        private CanvasGroup _canvasGroup;
        private Text _titleText;
        private Text _statsText;
        private Text _equipText;
        private Text _bodyText;
        private Text _mindText;
        private Transform _camT;

#if HAS_META_XR
        private OVRCameraRig _rig;
#endif

        void Start()
        {
            FindCameraTransform();
            BuildCanvas();
            _canvasGO.SetActive(false);
        }

        void Update()
        {
            if (_camT == null)
            {
                FindCameraTransform();
                if (_camT == null) return;
            }

            // Don't trigger introspection while any menu is open
            if (VRMenuManager.MenuOpen || VRQualityMenu.MenuOpen)
            {
                _lookDownTimer = 0f;
                return;
            }

            float pitch = _camT.eulerAngles.x;
            // Normalize: Unity gives 0-360, we want -180 to 180
            if (pitch > 180f) pitch -= 360f;

            if (!_isOpen)
            {
                // pitch is negative when looking down (after normalization)
                if (pitch < openPitchThreshold)
                {
                    _lookDownTimer += Time.deltaTime;
                    if (_lookDownTimer >= holdTime)
                    {
                        Open();
                    }
                }
                else
                {
                    _lookDownTimer = 0f;
                }
            }
            else
            {
                // Close when looking back up
                if (pitch > closePitchThreshold)
                {
                    Close();
                    return;
                }

                // Update position -- follow chest
                UpdatePosition();
            }

            // Handle fade
            UpdateFade();
        }

        // ── Camera discovery ──

        void FindCameraTransform()
        {
#if HAS_META_XR
            if (_rig == null)
                _rig = FindFirstObjectByType<OVRCameraRig>();
            if (_rig != null)
            {
                _camT = _rig.centerEyeAnchor;
                return;
            }
#endif
            // Fallback for editor testing without Meta XR
            _camT = Camera.main?.transform;
        }

        // ── Open / Close ──

        void Open()
        {
            _isOpen = true;
            IsOpen = true;
            _fadeAlpha = 0f;
            _canvasGO.SetActive(true);
            _lookDownTimer = 0f;

            UpdatePosition();
            UpdateStats();

            // Haptic feedback -- both controllers via HapticManager
            TriggerHaptics();

            Debug.Log("[INTROSPECTION] Menu opened -- player looks inside");
        }

        void Close()
        {
            _isOpen = false;
            IsOpen = false;
            _canvasGO.SetActive(false);
            _fadeAlpha = 0f;
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;

            Debug.Log("[INTROSPECTION] Menu closed");
        }

        // ── Position & Fade ──

        void UpdatePosition()
        {
            Vector3 camPos = _camT.position;
            Vector3 camFwd = _camT.forward;
            // Flatten forward to horizontal
            Vector3 flatFwd = new Vector3(camFwd.x, 0, camFwd.z).normalized;

            Vector3 pos = camPos
                + Vector3.up * chestOffsetY
                + flatFwd * chestOffsetZ;

            _canvasGO.transform.position = pos;

            // Face camera but stay upright
            Vector3 lookDir = camPos - pos;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.001f)
                _canvasGO.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        }

        void UpdateFade()
        {
            if (!_isOpen) return;

            if (_fadeAlpha < 1f)
            {
                _fadeAlpha += Time.deltaTime / Mathf.Max(fadeDuration, 0.01f);
                _fadeAlpha = Mathf.Clamp01(_fadeAlpha);
                if (_canvasGroup != null)
                    _canvasGroup.alpha = _fadeAlpha;
            }
        }

        // ── Stats display (placeholder -- wire to real game state later) ──

        void UpdateStats()
        {
            _statsText.text =
                "<color=#88ff88>WYTRZYMALOSC</color>  |||||||...  72%\n" +
                "<color=#ff8888>ZDROWIE</color>       |||||||||.  89%\n" +
                "<color=#88ccff>STAMINA</color>       ||||||....  61%";

            _equipText.text =
                "<color=#ccaa55>EKWIPUNEK:</color>\n" +
                "  L: [pusto]\n" +
                "  R: [pusto]\n" +
                "  Plecak: 2/8 slotow";

            _bodyText.text =
                "<color=#cccccc>CIALO:</color> brak ran\n" +
                "<color=#cccccc>INFEKCJE:</color> brak\n" +
                "<color=#cccccc>MUTACJE:</color> brak";

            _mindText.text =
                "<color=#bb88ff>UMYSL:</color> stabilny\n" +
                "<color=#bb88ff>STRES:</color> niski\n" +
                "<color=#bb88ff>MORALE:</color> neutralny";
        }

        // ── Haptics ──

        void TriggerHaptics()
        {
#if HAS_META_XR
            // Use HapticManager singleton if available, otherwise fall back to direct API
            var haptics = Plaga44.Feedback.HapticManager.Instance;
            if (haptics != null)
            {
                haptics.PlayGrab(OVRInput.Controller.LTouch);
                haptics.PlayGrab(OVRInput.Controller.RTouch);
            }
            else
            {
                // Fallback: gentle vibration on both controllers
                OVRInput.SetControllerVibration(0.3f, 0.4f, OVRInput.Controller.LTouch);
                OVRInput.SetControllerVibration(0.3f, 0.4f, OVRInput.Controller.RTouch);
                Invoke(nameof(StopHaptics), 0.15f);
            }
#endif
        }

#if HAS_META_XR
        void StopHaptics()
        {
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
        }
#endif

        // ── Canvas construction ──

        void BuildCanvas()
        {
            _canvasGO = new GameObject("IntrospectionMenu_Canvas");
            _canvasGO.transform.SetParent(transform, false);

            var canvas = _canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 998;

            _canvasGroup = _canvasGO.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            var rt = _canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(420, 400);
            rt.localScale = Vector3.one * 0.001f; // 1px = 1mm

            // Semi-transparent dark background
            BuildBackground(_canvasGO.transform);

            // Title
            _titleText = BuildTextElement(_canvasGO.transform, "Title",
                "= INTROSPEKCJA =", 20,
                new Color(0.9f, 0.85f, 0.7f), TextAnchor.UpperCenter,
                new Vector2(0, 0.9f), Vector2.one,
                "Arial");

            // Stats section (top -- health/stamina)
            _statsText = BuildTextElement(_canvasGO.transform, "Stats",
                "", 15,
                new Color(0.8f, 0.8f, 0.8f), TextAnchor.UpperLeft,
                new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.88f),
                "Courier New");

            // Equipment section
            _equipText = BuildTextElement(_canvasGO.transform, "Equipment",
                "", 14,
                new Color(0.8f, 0.75f, 0.6f), TextAnchor.UpperLeft,
                new Vector2(0.05f, 0.4f), new Vector2(0.95f, 0.6f),
                "Courier New");

            // Body condition section
            _bodyText = BuildTextElement(_canvasGO.transform, "Body",
                "", 14,
                new Color(0.7f, 0.7f, 0.7f), TextAnchor.UpperLeft,
                new Vector2(0.05f, 0.2f), new Vector2(0.5f, 0.38f),
                "Courier New");

            // Mental state section
            _mindText = BuildTextElement(_canvasGO.transform, "Mind",
                "", 14,
                new Color(0.75f, 0.7f, 0.85f), TextAnchor.UpperLeft,
                new Vector2(0.5f, 0.2f), new Vector2(0.95f, 0.38f),
                "Courier New");

            // Footer hint
            BuildTextElement(_canvasGO.transform, "Hint",
                "<color=#666666>[ spojrz w gore aby zamknac ]</color>", 12,
                new Color(0.4f, 0.4f, 0.4f), TextAnchor.LowerCenter,
                new Vector2(0, 0.02f), new Vector2(1, 0.12f),
                "Arial");
        }

        void BuildBackground(Transform parent)
        {
            var bgGO = new GameObject("BG");
            bgGO.transform.SetParent(parent, false);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.02f, 0.02f, 0.04f, 0.75f);
            bgImg.raycastTarget = false;
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
        }

        Text BuildTextElement(Transform parent, string name, string initialText,
            int size, Color color, TextAnchor anchor,
            Vector2 anchorMin, Vector2 anchorMax, string fontName)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = Font.CreateDynamicFontFromOSFont(fontName, size);
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.text = initialText;

            var textRT = go.GetComponent<RectTransform>();
            textRT.anchorMin = anchorMin;
            textRT.anchorMax = anchorMax;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            return text;
        }

        /// <summary>Public read for other systems to check if introspection is active.</summary>
        public static bool IsOpen { get; private set; }
    }
}
