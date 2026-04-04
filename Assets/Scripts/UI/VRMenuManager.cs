// VRMenuManager.cs
// CYBERNOMAD -- Unified hamburger menu (Button.Start).
// Single entry point for all in-game menus.
// Structure:
//   RESUME
//   SPAWNER >  (Items | VFX | Weapons sub-tabs)
//   SETTINGS > (Volume | Comfort Vignette | Snap Turn)
//   DEBUG >    (opens VRQualityMenu panel | Inspect Skybox)
//   QUIT
//
// Opens 2m in front of player. Disables locomotion while open.
// UIRayPointer (laser) + IndexTrigger = select/confirm.
// Button.Start = toggle open/close.
//
// Requires: com.meta.xr.sdk.core (HAS_META_XR define)

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.UI
{
    public class VRMenuManager : MonoBehaviour
    {
        // ---- Public API ----

        public static VRMenuManager Instance { get; private set; }

        /// <summary>True when ANY menu panel is visible. Check this to block gameplay input.</summary>
        public static bool MenuOpen { get; private set; } = false;

        public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf;

        public static event Action<bool> OnMenuToggled;  // true = opened

        // ---- Settings state (static -- persist across scene loads) ----

        public static float Volume { get; private set; } = 1.0f;
        public static bool ComfortVignette { get; private set; } = false;
        public static bool SnapTurn { get; private set; } = true;

        // ---- Private ----

        private const float MENU_DISTANCE = 2.0f;
        private const float CANVAS_SCALE  = 0.001f;
        private const int   CANVAS_W      = 600;
        private const int   CANVAS_H      = 750;

        // Colours -- dark theme
        private static readonly Color BG_COLOR      = new Color(0.10f, 0.10f, 0.10f, 0.88f);
        private static readonly Color PANEL_COLOR    = new Color(0.14f, 0.14f, 0.14f, 0.95f);
        private static readonly Color BTN_COLOR      = new Color(0.22f, 0.22f, 0.22f, 1.00f);
        private static readonly Color BTN_HOVER      = new Color(0.35f, 0.35f, 0.35f, 1.00f);
        private static readonly Color ACCENT         = new Color(1.00f, 0.42f, 0.21f, 1.00f);  // #FF6B35
        private static readonly Color TEXT_WHITE     = Color.white;
        private static readonly Color TEXT_GREY      = new Color(0.65f, 0.65f, 0.65f, 1.00f);
        private static readonly Color TAB_ACTIVE     = new Color(0.30f, 0.55f, 1.00f, 1.00f);  // blue
        private static readonly Color TAB_INACTIVE   = new Color(0.25f, 0.25f, 0.25f, 1.00f);

#if HAS_META_XR
        private OVRCameraRig _rig;
        private OVRPlayerController _playerController;
#endif

        private Canvas _canvas;

        // Panels
        private GameObject _mainPanel;
        private GameObject _spawnerPanel;
        private GameObject _settingsPanel;
        private GameObject _debugPanel;

        // Active sub-panel tracking
        private GameObject _activePanel;

        // Settings controls
        private Slider _volumeSlider;
        private Toggle _vignetteToggle;
        private Toggle _snapTurnToggle;

        // Spawner state
        private enum SpawnerTab { Items, VFX, Weapons }
        private SpawnerTab _spawnerTab = SpawnerTab.Items;
        private Text _spawnerTabLabel;
        private Text _spawnerListText;

        // Debug: reference to VRQualityMenu for delegation
        private bool _debugQualityOpen = false;

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
            BuildSpawnerPanel();
            BuildSettingsPanel();
            BuildDebugPanel();

            // Hide all sub-panels, start closed
            _spawnerPanel.SetActive(false);
            _settingsPanel.SetActive(false);
            _debugPanel.SetActive(false);
            _mainPanel.SetActive(true);
            _activePanel = _mainPanel;
            _canvas.gameObject.SetActive(false);
        }

        private void Update()
        {
#if HAS_META_XR
            if (_rig == null) _rig = FindFirstObjectByType<OVRCameraRig>();
            if (_playerController == null) _playerController = FindFirstObjectByType<OVRPlayerController>();

            // Don't steal Start if SplashScreen is managing menus
            bool splashOwnsInput = SplashScreen.Instance != null && SplashScreen.IsMenuOpen;
            // Don't steal Start if LaserInspector is active
            bool laserActive = LaserInspector.IsOpen;

            if (!splashOwnsInput && !laserActive && OVRInput.GetDown(OVRInput.Button.Start))
            {
                // If debug quality menu is open, close it and return to debug panel
                if (_debugQualityOpen)
                {
                    CloseQualitySubMenu();
                    return;
                }

                Toggle();
            }
#endif
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                MenuOpen = false;
                Instance = null;
            }
        }

        // ---- Public methods ----

        public void Open()
        {
            if (IsOpen) return;
            PlaceInFrontOfPlayer();
            ShowPanel(_mainPanel);
            _canvas.gameObject.SetActive(true);
            _debugQualityOpen = false;

            MenuOpen = true;
            Plaga44.GameState.Pause();
            SetLocomotion(false);
            OnMenuToggled?.Invoke(true);
        }

        public void Close()
        {
            if (!IsOpen && !_debugQualityOpen) return;

            // Close quality sub-menu if open
            if (_debugQualityOpen) CloseQualitySubMenu();

            _canvas.gameObject.SetActive(false);
            MenuOpen = false;
            Plaga44.GameState.Resume();
            SetLocomotion(true);
            OnMenuToggled?.Invoke(false);
        }

        public void Toggle()
        {
            if (IsOpen || _debugQualityOpen) Close(); else Open();
        }

        // ---- Panel navigation ----

        private void ShowPanel(GameObject panel)
        {
            _mainPanel.SetActive(false);
            _spawnerPanel.SetActive(false);
            _settingsPanel.SetActive(false);
            _debugPanel.SetActive(false);
            panel.SetActive(true);
            _activePanel = panel;
        }

        // ---- Locomotion control ----

        private void SetLocomotion(bool enabled)
        {
#if HAS_META_XR
            if (_playerController != null)
                _playerController.enabled = enabled;
#endif
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
            // Canvas text renders on the +Z (forward) face.
            // To face the player, canvas forward must point TOWARD the player (= -forward).
            _canvas.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
#else
            _canvas.transform.position = new Vector3(0f, 1.5f, MENU_DISTANCE);
            _canvas.transform.rotation = Quaternion.identity;
#endif
        }

        // ---- Canvas ----

        private void BuildCanvas()
        {
            var go = new GameObject("VRMenu_Canvas");
            go.transform.SetParent(transform);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CANVAS_W, CANVAS_H);
            rt.localScale = Vector3.one * CANVAS_SCALE;

            // GraphicRaycaster so UIRayPointer can interact
            go.AddComponent<GraphicRaycaster>();

            // Semi-transparent background
            var bg = CreateImage(go.transform, "BG",
                new Vector2(0, 0), new Vector2(CANVAS_W, CANVAS_H), BG_COLOR);
            bg.raycastTarget = true;
        }

        // ================================================================
        //  MAIN PANEL -- hamburger root
        // ================================================================

        private void BuildMainPanel()
        {
            _mainPanel = new GameObject("MainPanel");
            _mainPanel.transform.SetParent(_canvas.transform, false);
            var rt = _mainPanel.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400, 700);
            rt.anchoredPosition = Vector2.zero;

            CreateImage(_mainPanel.transform, "Panel",
                Vector2.zero, new Vector2(400, 700), PANEL_COLOR);

            // Hamburger icon (3 lines)
            var icon = CreateText(_mainPanel.transform, "HamburgerIcon",
                new Vector2(0, 310), new Vector2(60, 40), TextAnchor.MiddleCenter, 28);
            icon.text = "\u2261"; // triple bar
            icon.color = ACCENT;

            // Title
            var title = CreateText(_mainPanel.transform, "Title",
                new Vector2(0, 275), new Vector2(340, 44), TextAnchor.MiddleCenter, 34);
            title.text = "MENU";
            title.color = ACCENT;

            // Divider
            var div = CreateImage(_mainPanel.transform, "Divider",
                new Vector2(0, 250), new Vector2(340, 2), new Color(1, 1, 1, 0.12f));
            div.raycastTarget = false;

            // Buttons -- top to bottom
            float y = 200f;
            float step = -70f;

            CreateButton(_mainPanel.transform, "ResumeBtn",
                new Vector2(0, y), new Vector2(300, 56),
                "RESUME", OnResumeClicked);
            y += step;

            CreateButton(_mainPanel.transform, "SpawnerBtn",
                new Vector2(0, y), new Vector2(300, 56),
                "SPAWNER  >", OnSpawnerClicked);
            y += step;

            CreateButton(_mainPanel.transform, "SettingsBtn",
                new Vector2(0, y), new Vector2(300, 56),
                "SETTINGS  >", OnSettingsClicked);
            y += step;

            CreateButton(_mainPanel.transform, "DebugBtn",
                new Vector2(0, y), new Vector2(300, 56),
                "DEBUG  >", OnDebugClicked);
            y += step;

            CreateButton(_mainPanel.transform, "QuitBtn",
                new Vector2(0, y), new Vector2(300, 56),
                "QUIT", OnQuitClicked, new Color(0.55f, 0.15f, 0.10f, 1f));

            // Version label
            var ver = CreateText(_mainPanel.transform, "VersionLabel",
                new Vector2(0, -310), new Vector2(340, 28), TextAnchor.MiddleCenter, 16);
            ver.text = "PLAGA '44  |  TECH DEMO";
            ver.color = TEXT_GREY;
        }

        // ================================================================
        //  SPAWNER PANEL -- Items / VFX / Weapons tabs
        // ================================================================

        private void BuildSpawnerPanel()
        {
            _spawnerPanel = new GameObject("SpawnerPanel");
            _spawnerPanel.transform.SetParent(_canvas.transform, false);
            var rt = _spawnerPanel.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(500, 700);
            rt.anchoredPosition = Vector2.zero;

            CreateImage(_spawnerPanel.transform, "Panel",
                Vector2.zero, new Vector2(500, 700), PANEL_COLOR);

            // Title
            var title = CreateText(_spawnerPanel.transform, "Title",
                new Vector2(0, 310), new Vector2(460, 44), TextAnchor.MiddleCenter, 30);
            title.text = "SPAWNER";
            title.color = ACCENT;

            // Divider
            var div = CreateImage(_spawnerPanel.transform, "Divider",
                new Vector2(0, 285), new Vector2(460, 2), new Color(1, 1, 1, 0.12f));
            div.raycastTarget = false;

            // Tab buttons
            float tabY = 255f;
            float tabW = 140f;

            CreateButton(_spawnerPanel.transform, "TabItems",
                new Vector2(-155, tabY), new Vector2(tabW, 44),
                "ITEMS", () => SwitchSpawnerTab(SpawnerTab.Items));

            CreateButton(_spawnerPanel.transform, "TabVFX",
                new Vector2(0, tabY), new Vector2(tabW, 44),
                "VFX", () => SwitchSpawnerTab(SpawnerTab.VFX));

            CreateButton(_spawnerPanel.transform, "TabWeapons",
                new Vector2(155, tabY), new Vector2(tabW, 44),
                "WEAPONS", () => SwitchSpawnerTab(SpawnerTab.Weapons));

            // Tab indicator label
            _spawnerTabLabel = CreateText(_spawnerPanel.transform, "TabLabel",
                new Vector2(0, 218), new Vector2(460, 30), TextAnchor.MiddleCenter, 18);
            _spawnerTabLabel.color = TAB_ACTIVE;

            // Spawner content area (text list of what is available)
            _spawnerListText = CreateText(_spawnerPanel.transform, "SpawnerList",
                new Vector2(0, 50), new Vector2(440, 300), TextAnchor.UpperCenter, 18);
            _spawnerListText.color = TEXT_WHITE;

            // Action buttons
            CreateButton(_spawnerPanel.transform, "SpawnBtn",
                new Vector2(0, -140), new Vector2(300, 50),
                "SPAWN SELECTED", OnSpawnClicked);

            CreateButton(_spawnerPanel.transform, "DeleteLastBtn",
                new Vector2(-120, -200), new Vector2(210, 44),
                "DELETE LAST", OnDeleteLastClicked);

            CreateButton(_spawnerPanel.transform, "DeleteAllBtn",
                new Vector2(120, -200), new Vector2(210, 44),
                "DELETE ALL", OnDeleteAllClicked, new Color(0.55f, 0.15f, 0.10f, 1f));

            // Hint
            var hint = CreateText(_spawnerPanel.transform, "Hint",
                new Vector2(0, -260), new Vector2(440, 24), TextAnchor.MiddleCenter, 14);
            hint.text = "Use laser pointer to select. Trigger to confirm.";
            hint.color = TEXT_GREY;

            // Back
            CreateButton(_spawnerPanel.transform, "BackBtn",
                new Vector2(0, -310), new Vector2(260, 48),
                "< BACK", OnBackToMainClicked);

            SwitchSpawnerTab(SpawnerTab.Items);
        }

        private void SwitchSpawnerTab(SpawnerTab tab)
        {
            _spawnerTab = tab;

            string label = tab switch
            {
                SpawnerTab.Items => "[ ITEMS ]   VFX   WEAPONS",
                SpawnerTab.VFX => "ITEMS   [ VFX ]   WEAPONS",
                SpawnerTab.Weapons => "ITEMS   VFX   [ WEAPONS ]",
                _ => ""
            };
            if (_spawnerTabLabel != null) _spawnerTabLabel.text = label;

            // Populate list from the appropriate spawner system
            if (_spawnerListText != null)
            {
                string content = GetSpawnerListContent(tab);
                _spawnerListText.text = content;
            }
        }

        private string GetSpawnerListContent(SpawnerTab tab)
        {
            switch (tab)
            {
                case SpawnerTab.Items:
                    if (VRItemSpawner.Instance != null)
                        return VRItemSpawner.Instance.GetItemList();
                    return "<color=#666666>No items loaded.\nPlace prefabs in Resources/SpawnItems/</color>";

                case SpawnerTab.VFX:
                    if (VFXSpawnerMenu.Instance != null)
                        return VFXSpawnerMenu.Instance.GetVFXList();
                    return "<color=#666666>No VFX loaded.\nPlace prefabs in Resources/VFXPrefabs/</color>";

                case SpawnerTab.Weapons:
                    // Weapons are a subset of items -- filter by name
                    if (VRItemSpawner.Instance != null)
                        return VRItemSpawner.Instance.GetWeaponList();
                    return "<color=#666666>No weapons loaded.</color>";
            }
            return "";
        }

        // ================================================================
        //  SETTINGS PANEL
        // ================================================================

        private void BuildSettingsPanel()
        {
            _settingsPanel = new GameObject("SettingsPanel");
            _settingsPanel.transform.SetParent(_canvas.transform, false);
            var rt = _settingsPanel.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(420, 500);
            rt.anchoredPosition = Vector2.zero;

            CreateImage(_settingsPanel.transform, "Panel",
                Vector2.zero, new Vector2(420, 500), PANEL_COLOR);

            // Title
            var title = CreateText(_settingsPanel.transform, "Title",
                new Vector2(0, 210), new Vector2(380, 44), TextAnchor.MiddleCenter, 30);
            title.text = "SETTINGS";
            title.color = ACCENT;

            var div = CreateImage(_settingsPanel.transform, "Divider",
                new Vector2(0, 185), new Vector2(380, 2), new Color(1, 1, 1, 0.12f));
            div.raycastTarget = false;

            // ---- Volume slider ----
            var volLabel = CreateText(_settingsPanel.transform, "VolLabel",
                new Vector2(-90, 130), new Vector2(140, 32), TextAnchor.MiddleLeft, 22);
            volLabel.text = "VOLUME";
            volLabel.color = TEXT_WHITE;

            var volValText = CreateText(_settingsPanel.transform, "VolValue",
                new Vector2(145, 130), new Vector2(80, 32), TextAnchor.MiddleRight, 22);
            volValText.text = "100%";
            volValText.color = ACCENT;

            _volumeSlider = CreateSlider(_settingsPanel.transform, "VolSlider",
                new Vector2(20, 95), new Vector2(300, 30), 0f, 1f, Volume);
            _volumeSlider.onValueChanged.AddListener(val =>
            {
                Volume = val;
                AudioListener.volume = val;
                volValText.text = $"{Mathf.RoundToInt(val * 100)}%";
            });

            // ---- Comfort vignette toggle ----
            var vigLabel = CreateText(_settingsPanel.transform, "VigLabel",
                new Vector2(-90, 30), new Vector2(240, 32), TextAnchor.MiddleLeft, 22);
            vigLabel.text = "COMFORT VIGNETTE";
            vigLabel.color = TEXT_WHITE;

            _vignetteToggle = CreateToggle(_settingsPanel.transform, "VigToggle",
                new Vector2(145, 30), ComfortVignette);
            _vignetteToggle.onValueChanged.AddListener(val =>
            {
                ComfortVignette = val;
                ApplyVignette(val);
            });

            // ---- Snap turn toggle ----
            var snapLabel = CreateText(_settingsPanel.transform, "SnapLabel",
                new Vector2(-90, -30), new Vector2(240, 32), TextAnchor.MiddleLeft, 22);
            snapLabel.text = "SNAP TURN";
            snapLabel.color = TEXT_WHITE;

            _snapTurnToggle = CreateToggle(_settingsPanel.transform, "SnapToggle",
                new Vector2(145, -30), SnapTurn);
            _snapTurnToggle.onValueChanged.AddListener(val =>
            {
                SnapTurn = val;
            });

            // ---- Back button ----
            CreateButton(_settingsPanel.transform, "BackBtn",
                new Vector2(0, -200), new Vector2(260, 48),
                "< BACK", OnBackToMainClicked);
        }

        // ================================================================
        //  DEBUG PANEL
        // ================================================================

        private void BuildDebugPanel()
        {
            _debugPanel = new GameObject("DebugPanel");
            _debugPanel.transform.SetParent(_canvas.transform, false);
            var rt = _debugPanel.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(420, 500);
            rt.anchoredPosition = Vector2.zero;

            CreateImage(_debugPanel.transform, "Panel",
                Vector2.zero, new Vector2(420, 500), PANEL_COLOR);

            // Title
            var title = CreateText(_debugPanel.transform, "Title",
                new Vector2(0, 210), new Vector2(380, 44), TextAnchor.MiddleCenter, 30);
            title.text = "DEBUG";
            title.color = ACCENT;

            var div = CreateImage(_debugPanel.transform, "Divider",
                new Vector2(0, 185), new Vector2(380, 2), new Color(1, 1, 1, 0.12f));
            div.raycastTarget = false;

            // Quality Menu button
            CreateButton(_debugPanel.transform, "QualityBtn",
                new Vector2(0, 120), new Vector2(300, 56),
                "QUALITY SETTINGS", OnOpenQualityClicked);

            // Inspect Skybox button
            CreateButton(_debugPanel.transform, "InspectSkyboxBtn",
                new Vector2(0, 50), new Vector2(300, 56),
                "INSPECT SKYBOX", OnInspectSkyboxClicked);

            // Hint
            var hint = CreateText(_debugPanel.transform, "DebugHint",
                new Vector2(0, -50), new Vector2(380, 60), TextAnchor.UpperCenter, 16);
            hint.text = "Quality Settings opens the full debug\npanel with thumbstick navigation.\nPress Start to return.";
            hint.color = TEXT_GREY;

            // Back
            CreateButton(_debugPanel.transform, "BackBtn",
                new Vector2(0, -200), new Vector2(260, 48),
                "< BACK", OnBackToMainClicked);
        }

        // ================================================================
        //  BUTTON CALLBACKS
        // ================================================================

        private void OnResumeClicked() => Close();

        private void OnSpawnerClicked() => ShowPanel(_spawnerPanel);

        private void OnSettingsClicked() => ShowPanel(_settingsPanel);

        private void OnDebugClicked() => ShowPanel(_debugPanel);

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnBackToMainClicked() => ShowPanel(_mainPanel);

        // ---- Spawner callbacks ----

        private void OnSpawnClicked()
        {
            switch (_spawnerTab)
            {
                case SpawnerTab.Items:
                    if (VRItemSpawner.Instance != null)
                        VRItemSpawner.Instance.SpawnCurrent();
                    break;
                case SpawnerTab.VFX:
                    if (VFXSpawnerMenu.Instance != null)
                        VFXSpawnerMenu.Instance.SpawnCurrent();
                    break;
                case SpawnerTab.Weapons:
                    if (VRItemSpawner.Instance != null)
                        VRItemSpawner.Instance.SpawnCurrentWeapon();
                    break;
            }
            // Refresh the list after spawning
            SwitchSpawnerTab(_spawnerTab);
        }

        private void OnDeleteLastClicked()
        {
            switch (_spawnerTab)
            {
                case SpawnerTab.Items:
                case SpawnerTab.Weapons:
                    if (VRItemSpawner.Instance != null)
                        VRItemSpawner.Instance.DeleteLast();
                    break;
                case SpawnerTab.VFX:
                    if (VFXSpawnerMenu.Instance != null)
                        VFXSpawnerMenu.Instance.DeleteLast();
                    break;
            }
            SwitchSpawnerTab(_spawnerTab);
        }

        private void OnDeleteAllClicked()
        {
            switch (_spawnerTab)
            {
                case SpawnerTab.Items:
                case SpawnerTab.Weapons:
                    if (VRItemSpawner.Instance != null)
                        VRItemSpawner.Instance.DeleteAll();
                    break;
                case SpawnerTab.VFX:
                    if (VFXSpawnerMenu.Instance != null)
                        VFXSpawnerMenu.Instance.DeleteAll();
                    break;
            }
            SwitchSpawnerTab(_spawnerTab);
        }

        // ---- Debug callbacks ----

        private void OnOpenQualityClicked()
        {
            // Hide our canvas and open VRQualityMenu directly
            _canvas.gameObject.SetActive(false);
            _debugQualityOpen = true;

            var menu = FindFirstObjectByType<VRQualityMenu>();
            if (menu != null)
            {
                menu.ShowPanel();
            }
            else
            {
                Debug.LogWarning("[PLAGA44] VRMenuManager: VRQualityMenu not found");
                _canvas.gameObject.SetActive(true);
                _debugQualityOpen = false;
            }
        }

        private void CloseQualitySubMenu()
        {
            var menu = FindFirstObjectByType<VRQualityMenu>();
            if (menu != null)
                menu.HidePanel();
            _debugQualityOpen = false;
            _canvas.gameObject.SetActive(true);
            ShowPanel(_debugPanel);
        }

        private void OnInspectSkyboxClicked()
        {
            // Close menu, activate LaserInspector
            Close();
            var inspector = FindFirstObjectByType<LaserInspector>();
            if (inspector != null)
            {
                // LaserInspector has its own toggle mechanism
                inspector.SendMessage("ToggleInspector", SendMessageOptions.DontRequireReceiver);
            }
        }

        // ---- Platform integration ----

        private void ApplyVignette(bool enabled)
        {
#if HAS_META_XR
            var mgr = FindFirstObjectByType<OVRManager>();
            if (mgr == null) return;
            // OVRManager v81 -- no direct vignette API. State stored for future integration.
#endif
        }

        // ================================================================
        //  UI HELPERS -- shared builder methods
        // ================================================================

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
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", 24);
            txt.fontSize = 24;
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
