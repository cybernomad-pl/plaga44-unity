// SplashScreen.cs
// CYBERNOMAD -- Splash screen + main menu + pause menu.
// Lives on SplashScene (build index 0). Loads gameSceneName when player starts.
// On game start: black screen with "PLAGA '44", waits for both triggers.
// After triggers: shows main menu (CONTINUE / NEW GAME / SETTINGS).
// During gameplay: Start button opens pause menu (RESUME / SAVE / SETTINGS / QUIT).
// Stick navigation: L-stick up/down to select, trigger to confirm.
// Hides controller/hand models while splash is active.
// Follows CenterEyeAnchor at fixed distance.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Plaga44.UI
{
    public class SplashScreen : MonoBehaviour
    {
        // ---- Config ----

        public float fadeDuration = 0.6f;

        [Tooltip("Use <color=#CC3333> for red parts.")]
        public string displayName = "PLAGA <color=#CC3333>'44</color>";

        [Tooltip("Scene to load when player starts the game (NEW GAME / CONTINUE).")]
        public string gameSceneName = "PLAGA44_Demo";

        // ---- Singleton ----

        public static SplashScreen Instance { get; private set; }

        /// <summary>True when any menu (splash, main menu, or pause) is visible.</summary>
        public static bool IsMenuOpen => Instance != null && Instance._state != State.Hidden;

        // ---- State machine ----

        private enum State
        {
            Splash,      // black screen + title, waiting for both triggers
            MainMenu,    // post-splash menu: CONTINUE / NEW GAME / SETTINGS
            Paused,      // in-game pause: RESUME / SAVE / SETTINGS / QUIT
            Settings,    // settings sub-panel (delegates to VRQualityMenu)
            FadingOut,   // fading canvas to transparent, then -> Hidden
            Hidden       // not visible, gameplay active
        }

        private State _state = State.Splash;

        // ---- Visual ----

        private const float DISPLAY_DISTANCE = 1.5f;
        private const float DISPLAY_SCALE = 0.001f;
        private const int CANVAS_W = 4000;
        private const int CANVAS_H = 4000;

        // Colours
        private static readonly Color BG_COLOR = Color.black;
        private static readonly Color BTN_NORMAL = new Color(0.18f, 0.18f, 0.18f, 0.95f);
        private static readonly Color BTN_SELECTED = new Color(0.80f, 0.20f, 0.15f, 1.00f);
        private static readonly Color BTN_DISABLED = new Color(0.12f, 0.12f, 0.12f, 0.6f);
        private static readonly Color TEXT_WHITE = Color.white;
        private static readonly Color TEXT_GREY = new Color(0.4f, 0.4f, 0.4f);
        private static readonly Color TEXT_DIM = new Color(0.3f, 0.3f, 0.3f);

        // ---- References ----

        private Canvas _canvas;
        private CanvasGroup _group;
        private Transform _centerEye;
        private List<Renderer> _hiddenRenderers = new List<Renderer>();

        // UI elements
        private Text _titleText;
        private Text _subtitleText;
        private GameObject _menuContainer;
        private List<MenuEntry> _menuEntries = new List<MenuEntry>();
        private int _selectedIndex = 0;

        // Fade
        private float _fadeTimer;
        private bool _loadSceneOnFadeComplete;

        // Input
        private float _stickCooldown;
        private const float STICK_COOLDOWN = 0.25f;
        private bool _triggerWasDown; // debounce for confirm

        // ---- Menu entry ----

        private class MenuEntry
        {
            public string label;
            public System.Action action;
            public GameObject go;
            public Image bgImage;
            public Text textComponent;
            public bool interactable;
        }

        // ---- Bootstrap ----

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (Instance != null) return;
            var go = new GameObject("_SplashScreen");
            go.AddComponent<SplashScreen>();
            DontDestroyOnLoad(go);
        }

        // ---- Lifecycle ----

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
            CreateCanvas();
            CreateSplashElements();
            CreateMenuContainer();
            EnterState(State.Splash);
        }

        private void Update()
        {
            if (_centerEye == null)
            {
                FindCenterEye();
                if (_centerEye == null) return;
            }

            // Follow head (only when visible)
            if (_state != State.Hidden && _canvas != null)
            {
                _canvas.transform.position = _centerEye.position + _centerEye.forward * DISPLAY_DISTANCE;
                _canvas.transform.rotation = _centerEye.rotation;
            }

            switch (_state)
            {
                case State.Splash:
                    UpdateSplash();
                    break;
                case State.MainMenu:
                case State.Paused:
                    UpdateMenu();
                    break;
                case State.Settings:
                    UpdateSettings();
                    break;
                case State.FadingOut:
                    UpdateFade();
                    break;
                case State.Hidden:
                    UpdateHidden();
                    break;
            }
        }

        private void OnDestroy()
        {
            ShowControllers();
            if (Instance == this) Instance = null;
        }

        // ---- State transitions ----

        private void EnterState(State newState)
        {
            _state = newState;

            switch (newState)
            {
                case State.Splash:
                    _canvas.gameObject.SetActive(true);
                    _group.alpha = 1f;
                    _titleText.gameObject.SetActive(true);
                    _subtitleText.gameObject.SetActive(true);
                    _subtitleText.text = "press both triggers";
                    _menuContainer.SetActive(false);
                    HideControllers();
                    SetTimeScale(1f); // splash doesn't pause
                    break;

                case State.MainMenu:
                    _canvas.gameObject.SetActive(true);
                    _group.alpha = 1f;
                    _titleText.gameObject.SetActive(true);
                    _subtitleText.gameObject.SetActive(false);
                    _menuContainer.SetActive(true);
                    BuildMainMenuEntries();
                    _selectedIndex = 0;
                    UpdateMenuVisuals();
                    HideControllers();
                    SetTimeScale(1f); // not yet in game
                    break;

                case State.Paused:
                    _canvas.gameObject.SetActive(true);
                    _group.alpha = 1f;
                    _titleText.gameObject.SetActive(true);
                    _subtitleText.gameObject.SetActive(false);
                    _menuContainer.SetActive(true);
                    BuildPauseMenuEntries();
                    _selectedIndex = 0;
                    UpdateMenuVisuals();
                    SetTimeScale(0f);
                    DisablePlayerController(true);
                    break;

                case State.Settings:
                    // Hide our canvas, show VRQualityMenu
                    _canvas.gameObject.SetActive(false);
                    OpenVRQualityMenu();
                    break;

                case State.FadingOut:
                    _fadeTimer = 0f;
                    break;

                case State.Hidden:
                    _canvas.gameObject.SetActive(false);
                    ShowControllers();
                    SetTimeScale(1f);
                    DisablePlayerController(false);

                    // If transitioning from splash/main-menu, load the game scene
                    if (_loadSceneOnFadeComplete)
                    {
                        _loadSceneOnFadeComplete = false;
                        LoadGameScene();
                    }
                    break;
            }
        }

        // ---- Splash state ----

        private void UpdateSplash()
        {
            HideControllers();

            if (BothTriggersPressed())
            {
                EnterState(State.MainMenu);
            }
        }

        // ---- Menu state (shared for MainMenu and Paused) ----

        private void UpdateMenu()
        {
            if (_state == State.Splash) return;

            // Stick navigation
            _stickCooldown -= Time.unscaledDeltaTime;

            float stickY = 0f;
#if HAS_META_XR
            stickY = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch).y;
#endif

            if (_stickCooldown <= 0f)
            {
                if (stickY > 0.5f)
                {
                    // Up
                    NavigateUp();
                    _stickCooldown = STICK_COOLDOWN;
                }
                else if (stickY < -0.5f)
                {
                    // Down
                    NavigateDown();
                    _stickCooldown = STICK_COOLDOWN;
                }
            }

            // Reset cooldown when stick returns to center
            if (Mathf.Abs(stickY) < 0.3f)
                _stickCooldown = 0f;

            // Confirm with either trigger (debounced)
            bool triggerDown = AnyTriggerPressed();
            if (triggerDown && !_triggerWasDown)
            {
                ConfirmSelection();
            }
            _triggerWasDown = triggerDown;

            UpdateMenuVisuals();
        }

        private void NavigateUp()
        {
            if (_menuEntries.Count == 0) return;
            int attempts = _menuEntries.Count;
            do
            {
                _selectedIndex--;
                if (_selectedIndex < 0) _selectedIndex = _menuEntries.Count - 1;
                attempts--;
            } while (!_menuEntries[_selectedIndex].interactable && attempts > 0);
        }

        private void NavigateDown()
        {
            if (_menuEntries.Count == 0) return;
            int attempts = _menuEntries.Count;
            do
            {
                _selectedIndex++;
                if (_selectedIndex >= _menuEntries.Count) _selectedIndex = 0;
                attempts--;
            } while (!_menuEntries[_selectedIndex].interactable && attempts > 0);
        }

        private void ConfirmSelection()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _menuEntries.Count) return;
            var entry = _menuEntries[_selectedIndex];
            if (entry.interactable && entry.action != null)
            {
                entry.action.Invoke();
            }
        }

        // ---- Settings state ----

        private void UpdateSettings()
        {
#if HAS_META_XR
            // Start button closes VRQualityMenu and returns to our menu
            if (OVRInput.GetDown(OVRInput.Button.Start))
            {
                CloseVRQualityMenu();
                _canvas.gameObject.SetActive(true);
                EnterState(_returnToPause ? State.Paused : State.MainMenu);
                return;
            }
#endif

            // Also check if VRQualityMenu closed itself somehow
            if (!IsVRQualityMenuOpen())
            {
                _canvas.gameObject.SetActive(true);
                EnterState(_returnToPause ? State.Paused : State.MainMenu);
            }
        }

        private bool _returnToPause = false;

        // ---- Fade state ----

        private void UpdateFade()
        {
            _fadeTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_fadeTimer / fadeDuration);
            _group.alpha = 1f - t;

            if (t >= 1f)
            {
                EnterState(State.Hidden);
            }
        }

        // ---- Hidden state (gameplay) ----

        private void UpdateHidden()
        {
#if HAS_META_XR
            // Start button in gameplay = toggle VRQualityMenu (debug context menu)
            // Splash/pause menu is ONLY at game start. In-game always opens debug settings.
            if (OVRInput.GetDown(OVRInput.Button.Start))
            {
                // Toggle VRQualityMenu directly -- do NOT open splash/pause
                if (!IsVRQualityMenuOpen())
                {
                    OpenVRQualityMenu();
                }
            }
#endif
        }

        // ---- Scene loading ----

        private void LoadGameScene()
        {
            string current = SceneManager.GetActiveScene().name;
            if (current == gameSceneName)
            {
                // Already on the game scene (e.g. started directly in editor)
                Debug.Log($"[PLAGA44] SplashScreen: already on {gameSceneName}, skipping load");
                return;
            }

            Debug.Log($"[PLAGA44] SplashScreen: loading {gameSceneName}");
            SceneManager.LoadScene(gameSceneName);
        }

        // ---- Menu builders ----

        private void BuildMainMenuEntries()
        {
            ClearMenuEntries();

            bool hasSave = SaveManager.Instance != null && SaveManager.Instance.HasSave();

            AddMenuEntry("CONTINUE", hasSave, () =>
            {
                _loadSceneOnFadeComplete = true;
                if (SaveManager.Instance != null)
                {
                    SaveManager.Instance.Load();
                    EnterState(State.FadingOut);
                }
            });

            AddMenuEntry("NEW GAME", true, () =>
            {
                _loadSceneOnFadeComplete = true;
                EnterState(State.FadingOut);
            });

            AddMenuEntry("SETTINGS", true, () =>
            {
                _returnToPause = false;
                EnterState(State.Settings);
            });
        }

        private void BuildPauseMenuEntries()
        {
            ClearMenuEntries();

            AddMenuEntry("RESUME", true, () =>
            {
                EnterState(State.Hidden);
            });

            AddMenuEntry("SAVE", true, () =>
            {
                if (SaveManager.Instance != null)
                {
                    SaveManager.Instance.Save();
                    Debug.Log("[PLAGA44] SplashScreen: game saved");
                    // Brief feedback -- flash the button
                    var entry = _menuEntries[_selectedIndex];
                    if (entry.textComponent != null)
                        entry.textComponent.text = "SAVED!";
                    // Reset after a moment via coroutine
                    StartCoroutine(ResetLabelAfterDelay(entry, "SAVE", 1.0f));
                }
            });

            AddMenuEntry("SETTINGS", true, () =>
            {
                _returnToPause = true;
                EnterState(State.Settings);
            });

            AddMenuEntry("QUIT", true, () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }

        private System.Collections.IEnumerator ResetLabelAfterDelay(MenuEntry entry, string label, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (entry.textComponent != null)
                entry.textComponent.text = label;
        }

        // ---- Menu entry management ----

        private void AddMenuEntry(string label, bool interactable, System.Action action)
        {
            int index = _menuEntries.Count;
            float yOffset = -index * 70f;

            var go = new GameObject($"MenuItem_{label}");
            go.transform.SetParent(_menuContainer.transform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, yOffset);
            rt.sizeDelta = new Vector2(500, 60);

            var bg = go.AddComponent<Image>();
            bg.color = interactable ? BTN_NORMAL : BTN_DISABLED;

            // Label
            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(go.transform, false);
            var txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;

            var txt = txtGo.AddComponent<Text>();
            txt.font = Font.CreateDynamicFontFromOSFont("Consolas", 28);
            txt.fontSize = 28;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = interactable ? TEXT_WHITE : TEXT_DIM;
            txt.text = label;
            txt.raycastTarget = false;

            var entry = new MenuEntry
            {
                label = label,
                action = action,
                go = go,
                bgImage = bg,
                textComponent = txt,
                interactable = interactable
            };

            _menuEntries.Add(entry);
        }

        private void ClearMenuEntries()
        {
            foreach (var entry in _menuEntries)
            {
                if (entry.go != null) Destroy(entry.go);
            }
            _menuEntries.Clear();
            _selectedIndex = 0;
        }

        private void UpdateMenuVisuals()
        {
            for (int i = 0; i < _menuEntries.Count; i++)
            {
                var entry = _menuEntries[i];
                if (entry.bgImage == null) continue;

                if (!entry.interactable)
                {
                    entry.bgImage.color = BTN_DISABLED;
                    if (entry.textComponent != null)
                        entry.textComponent.color = TEXT_DIM;
                }
                else if (i == _selectedIndex)
                {
                    entry.bgImage.color = BTN_SELECTED;
                    if (entry.textComponent != null)
                        entry.textComponent.color = TEXT_WHITE;
                }
                else
                {
                    entry.bgImage.color = BTN_NORMAL;
                    if (entry.textComponent != null)
                        entry.textComponent.color = TEXT_WHITE;
                }
            }
        }

        // ---- Canvas creation ----

        private void CreateCanvas()
        {
            var canvasGO = new GameObject("SplashCanvas");
            canvasGO.transform.SetParent(transform);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 9999;

            var rect = _canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(CANVAS_W, CANVAS_H);
            rect.localScale = Vector3.one * DISPLAY_SCALE;

            _group = canvasGO.AddComponent<CanvasGroup>();

            // Black background -- full canvas
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bg = bgGO.AddComponent<Image>();
            bg.color = BG_COLOR;
            var bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
        }

        private void CreateSplashElements()
        {
            var parent = _canvas.transform;

            // "TESTBED:" label (top-left of title)
            var labelGO = new GameObject("TestbedLabel");
            labelGO.transform.SetParent(parent, false);
            var label = labelGO.AddComponent<Text>();
            label.text = "TESTBED:";
            label.font = Font.CreateDynamicFontFromOSFont("Consolas", 14);
            label.fontSize = 14;
            label.color = TEXT_GREY;
            label.alignment = TextAnchor.LowerLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(-124, 240);
            labelRect.sizeDelta = new Vector2(400, 30);

            // Title: "PLAGA '44"
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(parent, false);
            _titleText = titleGO.AddComponent<Text>();
            _titleText.text = string.IsNullOrEmpty(displayName) ? Application.productName : displayName;
            _titleText.font = Font.CreateDynamicFontFromOSFont("Consolas", 52);
            _titleText.fontSize = 52;
            _titleText.color = TEXT_WHITE;
            _titleText.alignment = TextAnchor.MiddleCenter;
            _titleText.supportRichText = true;
            _titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _titleText.verticalOverflow = VerticalWrapMode.Overflow;
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0, 200);
            titleRect.sizeDelta = new Vector2(2000, 200);

            // Subtitle: "press both triggers"
            var subGO = new GameObject("Subtitle");
            subGO.transform.SetParent(parent, false);
            _subtitleText = subGO.AddComponent<Text>();
            _subtitleText.text = "press both triggers";
            _subtitleText.font = Font.CreateDynamicFontFromOSFont("Consolas", 16);
            _subtitleText.fontSize = 16;
            _subtitleText.color = TEXT_GREY;
            _subtitleText.alignment = TextAnchor.MiddleCenter;
            _subtitleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _subtitleText.verticalOverflow = VerticalWrapMode.Overflow;
            var subRect = subGO.GetComponent<RectTransform>();
            subRect.anchoredPosition = new Vector2(0, 100);
            subRect.sizeDelta = new Vector2(2000, 50);
        }

        private void CreateMenuContainer()
        {
            _menuContainer = new GameObject("MenuContainer");
            _menuContainer.transform.SetParent(_canvas.transform, false);

            var rt = _menuContainer.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 1f);
            // Position below the title
            rt.anchoredPosition = new Vector2(0, 50);
            rt.sizeDelta = new Vector2(600, 400);

            _menuContainer.SetActive(false);
        }

        // ---- Input helpers ----

        private bool BothTriggersPressed()
        {
#if HAS_META_XR
            float left = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
            float right = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
            return left > 0.5f && right > 0.5f;
#else
            return Input.GetKey(KeyCode.Return);
#endif
        }

        private bool AnyTriggerPressed()
        {
#if HAS_META_XR
            float left = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
            float right = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
            return left > 0.5f || right > 0.5f;
#else
            return Input.GetKey(KeyCode.Return);
#endif
        }

        // ---- Controller visibility ----

        private void HideControllers()
        {
#if HAS_META_XR
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig == null) return;

            Transform[] anchors = new Transform[]
            {
                rig.leftControllerAnchor, rig.rightControllerAnchor,
                rig.leftHandAnchor, rig.rightHandAnchor
            };

            foreach (var anchor in anchors)
            {
                if (anchor == null) continue;
                foreach (var r in anchor.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.enabled)
                    {
                        r.enabled = false;
                        if (!_hiddenRenderers.Contains(r))
                            _hiddenRenderers.Add(r);
                    }
                }
            }
#endif
        }

        private void ShowControllers()
        {
            foreach (var r in _hiddenRenderers)
                if (r != null) r.enabled = true;
            _hiddenRenderers.Clear();

#if HAS_META_XR
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig == null) return;

            Transform[] anchors = new Transform[]
            {
                rig.leftControllerAnchor, rig.rightControllerAnchor,
                rig.leftHandAnchor, rig.rightHandAnchor
            };

            foreach (var anchor in anchors)
            {
                if (anchor == null) continue;
                foreach (var r in anchor.GetComponentsInChildren<Renderer>(true))
                    r.enabled = true;
            }
#endif
        }

        // ---- Camera ----

        private void FindCenterEye()
        {
#if HAS_META_XR
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig != null)
                _centerEye = rig.centerEyeAnchor;
#else
            var cam = Camera.main;
            if (cam != null) _centerEye = cam.transform;
#endif
        }

        // ---- VRQualityMenu integration ----

        private void OpenVRQualityMenu()
        {
            var menu = FindFirstObjectByType<VRQualityMenu>();
            if (menu != null)
            {
                // Force it open via its static field
                VRQualityMenu.MenuOpen = true;
                var canvasField = menu.GetType().GetField("_canvas",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (canvasField != null)
                {
                    var canvas = canvasField.GetValue(menu) as GameObject;
                    if (canvas != null) canvas.SetActive(true);
                }

                var visField = menu.GetType().GetField("_visible",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (visField != null) visField.SetValue(menu, true);
            }
            else
            {
                // No VRQualityMenu found -- go back to menu
                Debug.LogWarning("[PLAGA44] SplashScreen: VRQualityMenu not found, returning to menu");
                _canvas.gameObject.SetActive(true);
                EnterState(_returnToPause ? State.Paused : State.MainMenu);
            }
        }

        private void CloseVRQualityMenu()
        {
            var menu = FindFirstObjectByType<VRQualityMenu>();
            if (menu != null)
            {
                VRQualityMenu.MenuOpen = false;
                var canvasField = menu.GetType().GetField("_canvas",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (canvasField != null)
                {
                    var canvas = canvasField.GetValue(menu) as GameObject;
                    if (canvas != null) canvas.SetActive(false);
                }

                var visField = menu.GetType().GetField("_visible",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (visField != null) visField.SetValue(menu, false);
            }
        }

        private bool IsVRQualityMenuOpen()
        {
            return VRQualityMenu.MenuOpen;
        }

        // ---- Time & player controller ----

        private void SetTimeScale(float scale)
        {
            Time.timeScale = scale;
        }

        private void DisablePlayerController(bool disable)
        {
#if HAS_META_XR
            var player = FindFirstObjectByType<OVRPlayerController>();
            if (player != null) player.enabled = !disable;
#endif
        }

        // ---- Public API (for other scripts) ----

        /// <summary>Open the pause menu programmatically.</summary>
        public void OpenPauseMenu()
        {
            if (_state == State.Hidden)
                EnterState(State.Paused);
        }

        /// <summary>Close any open menu and return to gameplay.</summary>
        public void CloseMenu()
        {
            if (_state != State.Hidden)
                EnterState(State.Hidden);
        }
    }
}
