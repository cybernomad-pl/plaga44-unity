// VRMenuManager.cs
// CYBERNOMAD -- World-space VR pause menu.
// Opens 2m in front of player when Menu button is pressed.
// Stays static (does not follow gaze) while open.
// Buttons: Resume, Settings, Quit.
// Settings panel: Volume slider, Comfort vignette toggle, Snap turn toggle.
//
// Requires: com.meta.xr.sdk.core (HAS_META_XR define)

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.UI
{
    public class VRMenuManager : MonoBehaviour
    {
        // ---- Public API ----

        public static VRMenuManager Instance { get; private set; }

        public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf;

        public static event Action<bool> OnMenuToggled;  // true = opened

        // ---- Settings state (static -- persist across scene loads) ----

        public static float Volume { get; private set; } = 1.0f;
        public static bool ComfortVignette { get; private set; } = false;
        public static bool SnapTurn { get; private set; } = true;

        // ---- Private ----

        private const float MENU_DISTANCE = 2.0f;   // metres in front of player
        private const float CANVAS_SCALE  = 0.001f; // 1px = 1mm in world space
        private const int   CANVAS_W      = 600;
        private const int   CANVAS_H      = 660;

        // Colours -- dark theme
        private static readonly Color BG_COLOR      = new Color(0.10f, 0.10f, 0.10f, 0.88f);
        private static readonly Color PANEL_COLOR    = new Color(0.14f, 0.14f, 0.14f, 0.95f);
        private static readonly Color BTN_COLOR      = new Color(0.22f, 0.22f, 0.22f, 1.00f);
        private static readonly Color BTN_HOVER      = new Color(0.35f, 0.35f, 0.35f, 1.00f);
        private static readonly Color ACCENT         = new Color(1.00f, 0.42f, 0.21f, 1.00f);  // #FF6B35
        private static readonly Color TEXT_WHITE     = Color.white;
        private static readonly Color TEXT_GREY      = new Color(0.65f, 0.65f, 0.65f, 1.00f);

#if HAS_META_XR
        private OVRCameraRig _rig;
#endif

        private Canvas _canvas;
        private GameObject _mainPanel;
        private GameObject _settingsPanel;

        private Button _continueBtn;
        private Slider _volumeSlider;
        private Toggle _vignetteToggle;
        private Toggle _snapTurnToggle;

        // ---- Lifecycle ----

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            BuildCanvas();
            BuildMainPanel();
            BuildSettingsPanel();
            _settingsPanel.SetActive(false);
            _canvas.gameObject.SetActive(false);  // closed at start
        }

        private void Update()
        {
#if HAS_META_XR
            if (_rig == null) _rig = FindFirstObjectByType<OVRCameraRig>();

            // DISABLED: Start button now exclusively handled by VRQualityMenu.
            // VRMenuManager can still be opened/closed via Open()/Close()/Toggle()
            // from other scripts (e.g. VRQualityMenu could call it).
            // if (OVRInput.GetDown(OVRInput.Button.Start))
            //     Toggle();
#endif
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---- Public methods ----

        public void Open()
        {
            if (IsOpen) return;
            PlaceInFrontOfPlayer();
            _mainPanel.SetActive(true);
            _settingsPanel.SetActive(false);
            _canvas.gameObject.SetActive(true);
            UpdateContinueButton();
            OnMenuToggled?.Invoke(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            _canvas.gameObject.SetActive(false);
            OnMenuToggled?.Invoke(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        // ---- Placement ----

        private void PlaceInFrontOfPlayer()
        {
#if HAS_META_XR
            if (_rig == null) return;
            var head = _rig.centerEyeAnchor;
            Vector3 forward = head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            _canvas.transform.position = head.position + forward * MENU_DISTANCE;
            _canvas.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
#else
            _canvas.transform.position = new Vector3(0f, 1.5f, MENU_DISTANCE);
            _canvas.transform.rotation = Quaternion.identity;
#endif
        }

        // ---- Canvas & panels ----

        private void BuildCanvas()
        {
            var go = new GameObject("VRMenu_Canvas");
            go.transform.SetParent(transform);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CANVAS_W, CANVAS_H);
            rt.localScale  = Vector3.one * CANVAS_SCALE;

            // GraphicRaycaster so UIRayPointer can interact with buttons
            go.AddComponent<GraphicRaycaster>();

            // Semi-transparent background
            var bg = CreateImage(go.transform, "BG",
                new Vector2(0, 0), new Vector2(CANVAS_W, CANVAS_H), BG_COLOR);
            bg.raycastTarget = true;
        }

        private void BuildMainPanel()
        {
            _mainPanel = new GameObject("MainPanel");
            _mainPanel.transform.SetParent(_canvas.transform, false);
            var rt = _mainPanel.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(340, 600);
            rt.anchoredPosition = Vector2.zero;

            // Dark panel
            CreateImage(_mainPanel.transform, "Panel",
                Vector2.zero, new Vector2(340, 600), PANEL_COLOR);

            // Title
            var title = CreateText(_mainPanel.transform, "Title",
                new Vector2(0, 255), new Vector2(300, 50), TextAnchor.MiddleCenter, 36);
            title.text = "MENU";
            title.color = ACCENT;

            // Divider
            var div = CreateImage(_mainPanel.transform, "Divider",
                new Vector2(0, 228), new Vector2(280, 2), new Color(1, 1, 1, 0.12f));
            div.raycastTarget = false;

            // Buttons -- top to bottom: Continue, Resume, Save, Load, Settings, Quit
            _continueBtn = CreateButton(_mainPanel.transform, "ContinueBtn",
                new Vector2(0, 180), new Vector2(260, 56),
                "CONTINUE", OnContinueClicked);

            CreateButton(_mainPanel.transform, "ResumeBtn",
                new Vector2(0, 115), new Vector2(260, 56),
                "RESUME", OnResumeClicked);

            CreateButton(_mainPanel.transform, "SaveBtn",
                new Vector2(0, 50), new Vector2(260, 56),
                "SAVE", OnSaveClicked);

            CreateButton(_mainPanel.transform, "LoadBtn",
                new Vector2(0, -15), new Vector2(260, 56),
                "LOAD", OnLoadClicked);

            CreateButton(_mainPanel.transform, "SettingsBtn",
                new Vector2(0, -80), new Vector2(260, 56),
                "SETTINGS", OnSettingsClicked);

            CreateButton(_mainPanel.transform, "QuitBtn",
                new Vector2(0, -145), new Vector2(260, 56),
                "QUIT", OnQuitClicked, new Color(0.55f, 0.15f, 0.10f, 1f));

            // Grey out CONTINUE if no save exists
            UpdateContinueButton();

            // Version label
            var ver = CreateText(_mainPanel.transform, "VersionLabel",
                new Vector2(0, -265), new Vector2(300, 28), TextAnchor.MiddleCenter, 18);
            ver.text = "PLAGA '44  |  v0.1";
            ver.color = TEXT_GREY;
        }

        private void BuildSettingsPanel()
        {
            _settingsPanel = new GameObject("SettingsPanel");
            _settingsPanel.transform.SetParent(_canvas.transform, false);
            var rt = _settingsPanel.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(380, 440);
            rt.anchoredPosition = Vector2.zero;

            CreateImage(_settingsPanel.transform, "Panel",
                Vector2.zero, new Vector2(380, 440), PANEL_COLOR);

            // Title
            var title = CreateText(_settingsPanel.transform, "Title",
                new Vector2(0, 175), new Vector2(340, 50), TextAnchor.MiddleCenter, 36);
            title.text = "SETTINGS";
            title.color = ACCENT;

            var div = CreateImage(_settingsPanel.transform, "Divider",
                new Vector2(0, 148), new Vector2(340, 2), new Color(1, 1, 1, 0.12f));
            div.raycastTarget = false;

            // ---- Volume slider ----
            var volLabel = CreateText(_settingsPanel.transform, "VolLabel",
                new Vector2(-80, 90), new Vector2(120, 32), TextAnchor.MiddleLeft, 24);
            volLabel.text = "VOLUME";
            volLabel.color = TEXT_WHITE;

            var volValText = CreateText(_settingsPanel.transform, "VolValue",
                new Vector2(140, 90), new Vector2(70, 32), TextAnchor.MiddleRight, 24);
            volValText.text = "100%";
            volValText.color = ACCENT;

            _volumeSlider = CreateSlider(_settingsPanel.transform, "VolSlider",
                new Vector2(20, 55), new Vector2(300, 30), 0f, 1f, Volume);
            _volumeSlider.onValueChanged.AddListener(val =>
            {
                Volume = val;
                AudioListener.volume = val;
                volValText.text = $"{Mathf.RoundToInt(val * 100)}%";
            });

            // ---- Comfort vignette toggle ----
            var vigLabel = CreateText(_settingsPanel.transform, "VigLabel",
                new Vector2(-80, -10), new Vector2(220, 32), TextAnchor.MiddleLeft, 24);
            vigLabel.text = "COMFORT VIGNETTE";
            vigLabel.color = TEXT_WHITE;

            _vignetteToggle = CreateToggle(_settingsPanel.transform, "VigToggle",
                new Vector2(140, -10), ComfortVignette);
            _vignetteToggle.onValueChanged.AddListener(val =>
            {
                ComfortVignette = val;
                ApplyVignette(val);
            });

            // ---- Snap turn toggle ----
            var snapLabel = CreateText(_settingsPanel.transform, "SnapLabel",
                new Vector2(-80, -60), new Vector2(220, 32), TextAnchor.MiddleLeft, 24);
            snapLabel.text = "SNAP TURN";
            snapLabel.color = TEXT_WHITE;

            _snapTurnToggle = CreateToggle(_settingsPanel.transform, "SnapToggle",
                new Vector2(140, -60), SnapTurn);
            _snapTurnToggle.onValueChanged.AddListener(val =>
            {
                SnapTurn = val;
            });

            // ---- Back button ----
            CreateButton(_settingsPanel.transform, "BackBtn",
                new Vector2(0, -160), new Vector2(260, 52),
                "< BACK", OnBackClicked);
        }

        // ---- Button callbacks ----

        private void OnResumeClicked() => Close();

        private void OnContinueClicked()
        {
            if (SaveManager.Instance != null && SaveManager.Instance.HasSave())
            {
                SaveManager.Instance.Load();
                Close();
            }
        }

        private void OnSaveClicked()
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Save();
                UpdateContinueButton();
            }
        }

        private void OnLoadClicked()
        {
            if (SaveManager.Instance != null && SaveManager.Instance.HasSave())
            {
                SaveManager.Instance.Load();
                Close();
            }
        }

        private void OnSettingsClicked()
        {
            _mainPanel.SetActive(false);
            _settingsPanel.SetActive(true);
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnBackClicked()
        {
            _settingsPanel.SetActive(false);
            _mainPanel.SetActive(true);
        }

        // ---- Save/Load helpers ----

        private void UpdateContinueButton()
        {
            if (_continueBtn == null) return;
            bool hasSave = SaveManager.Instance != null && SaveManager.Instance.HasSave();
            _continueBtn.interactable = hasSave;

            // Dim the label when no save exists
            var label = _continueBtn.GetComponentInChildren<Text>();
            if (label != null)
                label.color = hasSave ? TEXT_WHITE : TEXT_GREY;
        }

        // ---- Platform integration ----

        private void ApplyVignette(bool enabled)
        {
#if HAS_META_XR
            // OVRManager exposes comfort vignette in newer SDK versions.
            // Keep a soft fallback -- no-op if property absent.
            var mgr = FindFirstObjectByType<OVRManager>();
            if (mgr == null) return;
            // OVRManager.instance.isInsightPassthroughEnabled has no direct vignette API
            // in v81 -- leave as settings state for future integration.
#endif
        }

        // ---- UI helpers ----

        private Image CreateImage(Transform parent, string name,
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
            return img;
        }

        private Text CreateText(Transform parent, string name,
            Vector2 pos, Vector2 size, TextAnchor anchor, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var txt = go.AddComponent<Text>();
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            txt.fontSize = fontSize;
            txt.alignment = anchor;
            txt.color = TEXT_WHITE;
            txt.supportRichText = true;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow   = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        private Button CreateButton(Transform parent, string name,
            Vector2 pos, Vector2 size, string label,
            Action onClick, Color? bgOverride = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = bgOverride ?? BTN_COLOR;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = bgOverride ?? BTN_COLOR;
            colors.highlightedColor = BTN_HOVER;
            colors.pressedColor     = ACCENT;
            colors.selectedColor    = BTN_HOVER;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            // Label
            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(go.transform, false);
            var txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;

            var txt = txtGo.AddComponent<Text>();
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", 26);
            txt.fontSize = 26;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = TEXT_WHITE;
            txt.raycastTarget = false;
            txt.text = label;

            return btn;
        }

        private Slider CreateSlider(Transform parent, string name,
            Vector2 pos, Vector2 size, float min, float max, float value)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var slider = go.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.wholeNumbers = false;

            // Track background
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(go.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.25f);
            bgRt.anchorMax = new Vector2(1f, 0.75f);
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            // Fill area
            var fillAreaGo = new GameObject("FillArea");
            fillAreaGo.transform.SetParent(go.transform, false);
            var fillAreaRt = fillAreaGo.AddComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRt.offsetMin = new Vector2(5, 0);
            fillAreaRt.offsetMax = new Vector2(-5, 0);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(value, 1f);
            fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = ACCENT;

            slider.fillRect = fillRt;

            // Handle
            var handleAreaGo = new GameObject("HandleArea");
            handleAreaGo.transform.SetParent(go.transform, false);
            var handleAreaRt = handleAreaGo.AddComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = new Vector2(10, 0);
            handleAreaRt.offsetMax = new Vector2(-10, 0);

            var handleGo = new GameObject("Handle");
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            var handleRt = handleGo.AddComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(24, 24);
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = Color.white;

            slider.handleRect = handleRt;
            slider.targetGraphic = handleImg;

            return slider;
        }

        private Toggle CreateToggle(Transform parent, string name, Vector2 pos, bool initialValue)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(50, 30);

            var bgImg = go.AddComponent<Image>();
            bgImg.color = BTN_COLOR;

            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = bgImg;

            // Checkmark
            var checkGo = new GameObject("Checkmark");
            checkGo.transform.SetParent(go.transform, false);
            var checkRt = checkGo.AddComponent<RectTransform>();
            checkRt.anchorMin = new Vector2(0.1f, 0.1f);
            checkRt.anchorMax = new Vector2(0.9f, 0.9f);
            checkRt.offsetMin = checkRt.offsetMax = Vector2.zero;
            var checkImg = checkGo.AddComponent<Image>();
            checkImg.color = ACCENT;

            toggle.graphic = checkImg;
            toggle.isOn = initialValue;

            var colors = toggle.colors;
            colors.normalColor      = BTN_COLOR;
            colors.highlightedColor = BTN_HOVER;
            colors.pressedColor     = ACCENT;
            colors.selectedColor    = BTN_HOVER;
            toggle.colors = colors;

            return toggle;
        }
    }
}
