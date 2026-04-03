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
        public float holdTime = 0.8f;

        [Tooltip("Head pitch angle to close menu (looking back up).")]
        public float closePitchThreshold = -20f;

        [Header("UI Position")]
        [Tooltip("Vertical offset from camera (negative = below).")]
        public float chestOffsetY = -0.4f;

        [Tooltip("Forward offset from camera.")]
        public float chestOffsetZ = 0.35f;

        [Header("Haptics")]
        [Tooltip("Vibration on menu open.")]
        public float hapticFrequency = 0.3f;
        public float hapticAmplitude = 0.4f;
        public float hapticDuration = 0.15f;

        // ── State ──
        private bool _isOpen;
        private float _lookDownTimer;
        private GameObject _canvasGO;
        private Text _titleText;
        private Text _statsText;
        private Transform _camT;
        private float _hapticTimer;

        void Start()
        {
            _camT = Camera.main?.transform;
            BuildCanvas();
            _canvasGO.SetActive(false);
        }

        void Update()
        {
            if (_camT == null)
            {
                _camT = Camera.main?.transform;
                if (_camT == null) return;
            }

            float pitch = _camT.eulerAngles.x;
            // Normalize: Unity gives 0-360, we want -180 to 180
            if (pitch > 180f) pitch -= 360f;

            // Haptic cooldown
            if (_hapticTimer > 0f) _hapticTimer -= Time.deltaTime;

            if (!_isOpen)
            {
                // pitch is negative when looking down (after normalization above)
                if (pitch < openPitchThreshold)
                {
                    _lookDownTimer += Time.deltaTime;
                    if (_lookDownTimer >= holdTime)
                    {
                        Open();
                        UpdatePosition();
                        UpdateStats();
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
                UpdateStats();
            }
        }

        void Open()
        {
            _isOpen = true;
            IsOpen = true;
            _canvasGO.SetActive(true);
            _lookDownTimer = 0f;

            // Haptic feedback -- both controllers
            TriggerHaptics();

            Debug.Log("[INTROSPECTION] Menu opened -- player looks inside");
        }

        void Close()
        {
            _isOpen = false;
            IsOpen = false;
            _canvasGO.SetActive(false);
            Debug.Log("[INTROSPECTION] Menu closed");
        }

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
            Vector3 lookDir = (camPos - pos);
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.001f)
                _canvasGO.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        }

        void UpdateStats()
        {
            // POC -- placeholder stats, replace with real game state later
            _statsText.text =
                "<color=#88ff88>WYTRZYMALOSC</color>  |||||||...  72%\n" +
                "<color=#ff8888>ZDROWIE</color>       |||||||||.  89%\n" +
                "<color=#88ccff>STAMINA</color>       ||||||....  61%\n" +
                "\n" +
                "<color=#cccccc>CIALO:</color> brak ran\n" +
                "<color=#cccccc>UMYSL:</color> stabilny\n" +
                "<color=#cccccc>INFEKCJE:</color> brak\n" +
                "\n" +
                "<color=#666666>[ spojrz w gore aby zamknac ]</color>";
        }

        void TriggerHaptics()
        {
#if HAS_META_XR
            OVRInput.SetControllerVibration(hapticFrequency, hapticAmplitude, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(hapticFrequency, hapticAmplitude, OVRInput.Controller.RTouch);
            _hapticTimer = hapticDuration;
            Invoke(nameof(StopHaptics), hapticDuration);
#endif
        }

        void StopHaptics()
        {
#if HAS_META_XR
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
#endif
        }

        void BuildCanvas()
        {
            _canvasGO = new GameObject("IntrospectionMenu_Canvas");
            _canvasGO.transform.SetParent(transform, false);

            var canvas = _canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 998;

            var rt = _canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400, 300);
            rt.localScale = Vector3.one * 0.001f; // 1px = 1mm

            // Semi-transparent dark background
            var bgGO = new GameObject("BG");
            bgGO.transform.SetParent(_canvasGO.transform, false);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.7f);
            bgImg.raycastTarget = false;
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            // Title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(_canvasGO.transform, false);
            _titleText = titleGO.AddComponent<Text>();
            _titleText.font = Font.CreateDynamicFontFromOSFont("Arial", 20);
            _titleText.fontSize = 20;
            _titleText.color = new Color(0.9f, 0.9f, 0.9f);
            _titleText.alignment = TextAnchor.UpperCenter;
            _titleText.text = "= INTROSPEKCJA =";
            _titleText.raycastTarget = false;

            var titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 0.85f);
            titleRT.anchorMax = new Vector2(1, 1f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            // Stats body
            var statsGO = new GameObject("Stats");
            statsGO.transform.SetParent(_canvasGO.transform, false);
            _statsText = statsGO.AddComponent<Text>();
            _statsText.font = Font.CreateDynamicFontFromOSFont("Courier New", 16);
            _statsText.fontSize = 16;
            _statsText.color = new Color(0.8f, 0.8f, 0.8f);
            _statsText.alignment = TextAnchor.UpperLeft;
            _statsText.supportRichText = true;
            _statsText.raycastTarget = false;

            var statsRT = statsGO.GetComponent<RectTransform>();
            statsRT.anchorMin = new Vector2(0.05f, 0.05f);
            statsRT.anchorMax = new Vector2(0.95f, 0.82f);
            statsRT.offsetMin = Vector2.zero;
            statsRT.offsetMax = Vector2.zero;
        }

        /// <summary>Public read for other systems to check if introspection is active.</summary>
        public static bool IsOpen { get; private set; }
    }
}
