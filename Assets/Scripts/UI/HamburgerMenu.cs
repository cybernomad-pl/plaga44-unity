// =============================================================================
// HamburgerMenu.cs
// CYBERNOMAD -- Hamburger menu VR dla PLAGA '44.
//
// Bazowane na VRMenuManager z bleeding-edge, oczyszczone z zaleznosci.
// Button.Start = toggle. Thumbstick = nawigacja. Triggers = +/-.
// 6 kwadratowych ikon kategorii. Time.timeScale = 0 gdy otwarte.
//
// Canvas renderuje na stronie +Z. Zeby gracz widzial tekst poprawnie,
// canvas musi patrzec W STRONE gracza (LookRotation(-forward)).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.UI
{
    public class HamburgerMenu : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Menu]";

        // =====================================================================
        // Singleton
        // =====================================================================

        public static HamburgerMenu Instance { get; private set; }
        public static bool MenuOpen { get; private set; }

        // =====================================================================
        // Config
        // =====================================================================

        private const float MENU_DISTANCE = 1.4f;
        private const float CANVAS_SCALE = 0.001f;
        private const int CANVAS_W = 900;
        private const int CANVAS_H = 700;

        // Kolory -- dark theme
        private static readonly Color BG_COLOR = new Color(0.08f, 0.08f, 0.08f, 0.92f);
        private static readonly Color BTN_COLOR = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color BTN_HOVER = new Color(0.30f, 0.30f, 0.30f);
        private static readonly Color BTN_SELECTED = new Color(0.20f, 0.35f, 0.55f);
        private static readonly Color ACCENT = new Color(0.9f, 0.5f, 0.1f);
        private static readonly Color TEXT_WHITE = Color.white;
        private static readonly Color TEXT_GREY = new Color(0.55f, 0.55f, 0.55f);

        // =====================================================================
        // Kategorie -- dynamiczne z SettingsRegistry
        // =====================================================================

        private string[] _categories;

        // =====================================================================
        // Stan
        // =====================================================================

        private Canvas _canvas;
        private Image[] _categoryBGs;
        private Text _selectedLabel;
        private Text _valueText;
        private int _selectedIndex;

        private float _lastStickTime;
        private const float STICK_COOLDOWN = 0.2f;
        private float _lastTriggerTime;
        private const float TRIGGER_COOLDOWN = 0.25f;

        // Submenu state
        private bool _inSubmenu;
        private List<SettingDef> _currentSettings;
        private int _settingIndex;
        private GameObject _gridRoot;
        private GameObject _submenuRoot;
        private Text[] _settingTexts;
        private const int VISIBLE_ROWS = 10;

        private OVRCameraRig _rig;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Debug.Log($"{LOG} Awake");
        }

        private void Start()
        {
            _rig = FindFirstObjectByType<OVRCameraRig>();
            _categories = SettingsRegistry.GetSectionNames();
            BuildCanvas();
            BuildGrid();
            BuildFooter();
            _canvas.gameObject.SetActive(false);
            UpdateSelection();
            Debug.Log($"{LOG} Start: {_categories.Length} kategorii, rig={(_rig != null ? _rig.name : "NULL")}");
        }

        private void Update()
        {
            if (_rig == null) _rig = FindFirstObjectByType<OVRCameraRig>();

            // Button.Start = trzy kreski na lewym kontrolerze
            if (OVRInput.GetDown(OVRInput.Button.Start))
            {
                Debug.Log($"{LOG} Button.Start PRESSED, MenuOpen={MenuOpen}");
                Toggle();
            }

            if (!MenuOpen) return;

            // B = wstecz z submenu do gridu
            if (_inSubmenu && OVRInput.GetDown(OVRInput.Button.Two)) // B button
            {
                ExitSubmenu();
                return;
            }

            // A = wejdz w submenu wybranego kafelka
            if (!_inSubmenu && OVRInput.GetDown(OVRInput.Button.One)) // A button
            {
                EnterSubmenu(_categories[_selectedIndex]);
                return;
            }

            if (_inSubmenu)
            {
                HandleSubmenuInput();
            }
            else
            {
                HandleThumbstick();
                HandleTriggers();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                MenuOpen = false;
                Instance = null;
            }
        }

        // =====================================================================
        // Open / Close
        // =====================================================================

        public void Toggle()
        {
            if (MenuOpen) Close(); else Open();
        }

        public void Open()
        {
            if (MenuOpen) return;
            PlaceInFrontOfPlayer();
            _canvas.gameObject.SetActive(true);
            MenuOpen = true;
            Time.timeScale = 0f;
            Debug.Log($"{LOG} OPEN (timeScale=0)");
        }

        public void Close()
        {
            if (!MenuOpen) return;
            _canvas.gameObject.SetActive(false);
            MenuOpen = false;
            Time.timeScale = 1f;
            Debug.Log($"{LOG} CLOSE (timeScale=1)");
        }

        public bool IsOpen => MenuOpen;

        // =====================================================================
        // Input -- thumbstick nawigacja
        // =====================================================================

        private void HandleThumbstick()
        {
            // Oba thumbsticki dzialaja do nawigacji
            Vector2 stickL = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
            Vector2 stickR = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
            // Bierz ten ktory jest bardziej wychylony
            Vector2 stick = stickL.sqrMagnitude > stickR.sqrMagnitude ? stickL : stickR;

            if (Time.unscaledTime - _lastStickTime < STICK_COOLDOWN) return;

            int cols = 5;
            if (stick.x > 0.5f) { MoveSelection(1); _lastStickTime = Time.unscaledTime; }
            else if (stick.x < -0.5f) { MoveSelection(-1); _lastStickTime = Time.unscaledTime; }
            else if (stick.y > 0.5f) { MoveSelection(-cols); _lastStickTime = Time.unscaledTime; }
            else if (stick.y < -0.5f) { MoveSelection(cols); _lastStickTime = Time.unscaledTime; }
        }

        private void MoveSelection(int delta)
        {
            int newIndex = _selectedIndex + delta;
            if (newIndex < 0 || newIndex >= _categories.Length) return;
            _selectedIndex = newIndex;
            UpdateSelection();
            Debug.Log($"{LOG} Wybrano: {_categories[_selectedIndex]} [{_selectedIndex}]");
        }

        // =====================================================================
        // Input -- triggers +/-
        // =====================================================================

        private void HandleTriggers()
        {
            // W grid mode triggery tez wchodza w submenu (jak A)
            if (Time.unscaledTime - _lastTriggerTime < TRIGGER_COOLDOWN) return;

            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                _lastTriggerTime = Time.unscaledTime;
                EnterSubmenu(_categories[_selectedIndex]);
            }
        }

        // =====================================================================
        // Submenu -- lista settingow per modul
        // =====================================================================

        private void EnterSubmenu(string moduleName)
        {
            _currentSettings = SettingsRegistry.GetSettings(moduleName);
            if (_currentSettings.Count == 0)
            {
                Debug.Log($"{LOG} {moduleName}: brak ustawien runtime");
                return;
            }

            _inSubmenu = true;
            _settingIndex = 0;
            // Ukryj grid
            if (_gridRoot != null) _gridRoot.SetActive(false);

            // Buduj submenu UI
            BuildSubmenuUI(moduleName);
            UpdateSubmenuDisplay();
            Debug.Log($"{LOG} Submenu: {moduleName} ({_currentSettings.Count} settings)");
        }

        private void ExitSubmenu()
        {
            _inSubmenu = false;
            if (_submenuRoot != null) Destroy(_submenuRoot);
            if (_gridRoot != null) _gridRoot.SetActive(true);
            _selectedLabel.text = "> " + _categories[_selectedIndex] + " <";
            _valueText.text = "A = wejdz    B = wstecz";
            Debug.Log($"{LOG} Submenu: wyjscie");
        }

        private void HandleSubmenuInput()
        {
            Vector2 stickL = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
            Vector2 stickR = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
            Vector2 stick = stickL.sqrMagnitude > stickR.sqrMagnitude ? stickL : stickR;

            if (Time.unscaledTime - _lastStickTime < STICK_COOLDOWN) return;

            // Gora/dol = wybor settingu
            if (stick.y > 0.5f && _settingIndex > 0)
            {
                _settingIndex--;
                _lastStickTime = Time.unscaledTime;
                UpdateSubmenuDisplay();
            }
            else if (stick.y < -0.5f && _settingIndex < _currentSettings.Count - 1)
            {
                _settingIndex++;
                _lastStickTime = Time.unscaledTime;
                UpdateSubmenuDisplay();
            }

            // Lewo/prawo = zmiana wartosci
            if (stick.x > 0.5f)
            {
                AdjustSetting(1);
                _lastStickTime = Time.unscaledTime;
            }
            else if (stick.x < -0.5f)
            {
                AdjustSetting(-1);
                _lastStickTime = Time.unscaledTime;
            }

            // Triggery tez zmieniaja wartosc
            if (Time.unscaledTime - _lastTriggerTime > TRIGGER_COOLDOWN)
            {
                if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch))
                {
                    AdjustSetting(-1);
                    _lastTriggerTime = Time.unscaledTime;
                }
                if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
                {
                    AdjustSetting(1);
                    _lastTriggerTime = Time.unscaledTime;
                }
            }
        }

        private void AdjustSetting(int direction)
        {
            if (_settingIndex < 0 || _settingIndex >= _currentSettings.Count) return;
            var s = _currentSettings[_settingIndex];
            float val = s.get();
            val += s.step * direction;
            val = Mathf.Clamp(val, s.min, s.max);
            s.set(val);
            UpdateSubmenuDisplay();
        }

        private void BuildSubmenuUI(string title)
        {
            if (_submenuRoot != null) Destroy(_submenuRoot);

            _submenuRoot = new GameObject("SubmenuPanel");
            _submenuRoot.transform.SetParent(_canvas.transform, false);
            var rootRT = _submenuRoot.AddComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = new Vector2(20, 60);
            rootRT.offsetMax = new Vector2(-20, -60);

            // Tytul submenu
            _selectedLabel.text = "[ " + title + " ]  (B = wstecz)";

            // Wiersze settingow
            _settingTexts = new Text[VISIBLE_ROWS];
            float rowH = 30f;
            for (int i = 0; i < VISIBLE_ROWS; i++)
            {
                var rowGO = new GameObject($"Row_{i}");
                rowGO.transform.SetParent(_submenuRoot.transform, false);
                var rowRT = rowGO.AddComponent<RectTransform>();
                rowRT.anchorMin = new Vector2(0, 1);
                rowRT.anchorMax = new Vector2(1, 1);
                rowRT.pivot = new Vector2(0.5f, 1);
                rowRT.sizeDelta = new Vector2(0, rowH);
                rowRT.anchoredPosition = new Vector2(0, -i * (rowH + 2));

                var txt = rowGO.AddComponent<Text>();
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize = 18;
                txt.color = TEXT_WHITE;
                txt.alignment = TextAnchor.MiddleLeft;
                _settingTexts[i] = txt;
            }
        }

        private void UpdateSubmenuDisplay()
        {
            if (_settingTexts == null || _currentSettings == null) return;

            // Scroll zeby wybrany setting byl widoczny
            int scrollOffset = Mathf.Max(0, _settingIndex - VISIBLE_ROWS + 3);

            for (int i = 0; i < VISIBLE_ROWS; i++)
            {
                int idx = scrollOffset + i;
                if (idx >= _currentSettings.Count)
                {
                    _settingTexts[i].text = "";
                    continue;
                }

                var s = _currentSettings[idx];
                bool selected = (idx == _settingIndex);
                float val = s.get();
                string prefix = selected ? "> " : "  ";
                _settingTexts[i].text = $"{prefix}{s.name}: {val.ToString(s.format)}";
                _settingTexts[i].color = selected ? TEXT_WHITE : TEXT_GREY;
            }

            // Footer -- wartosc + opis
            {
                var s = _currentSettings[_settingIndex];
                float val = s.get();
                _selectedLabel.text = s.desc ?? "";
                _valueText.text = $"<  {val.ToString(s.format)}  >    [{s.min}..{s.max}]";
            }
        }

        // =====================================================================
        // Pozycjonowanie
        // =====================================================================

        private void PlaceInFrontOfPlayer()
        {
            if (_rig != null)
            {
                var head = _rig.centerEyeAnchor;
                Vector3 forward = head.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                forward.Normalize();

                _canvas.transform.position = head.position + forward * MENU_DISTANCE;
                _canvas.transform.position += Vector3.down * 0.1f;

                // Mirror fix: w tym renderze canvas jest widoczny od strony +Z = forward
                _canvas.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
                Debug.Log($"{LOG} Placed at {_canvas.transform.position}, facing player");
            }
            else
            {
                _canvas.transform.position = new Vector3(0f, 1.5f, MENU_DISTANCE);
                _canvas.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
                Debug.LogWarning($"{LOG} No OVRCameraRig -- fallback position");
            }
        }

        // =====================================================================
        // Budowanie UI
        // =====================================================================

        private void BuildCanvas()
        {
            var go = new GameObject("HamburgerMenu_Canvas");
            go.transform.SetParent(transform);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CANVAS_W, CANVAS_H);
            rt.localScale = Vector3.one * CANVAS_SCALE;

            go.AddComponent<GraphicRaycaster>();

            // Tlo
            var bgGO = new GameObject("BG");
            bgGO.transform.SetParent(go.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = BG_COLOR;

            // Tytul
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(go.transform, false);
            var titleRT = titleGO.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot = new Vector2(0.5f, 1);
            titleRT.sizeDelta = new Vector2(0, 50);
            var titleText = titleGO.AddComponent<Text>();
            titleText.text = "PLAGA '44";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 28;
            titleText.color = ACCENT;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.fontStyle = FontStyle.Bold;
        }

        private void BuildGrid()
        {
            _gridRoot = new GameObject("GridRoot");
            _gridRoot.transform.SetParent(_canvas.transform, false);
            var gridRootRT = _gridRoot.AddComponent<RectTransform>();
            gridRootRT.anchorMin = Vector2.zero;
            gridRootRT.anchorMax = Vector2.one;
            gridRootRT.offsetMin = gridRootRT.offsetMax = Vector2.zero;

            int cols = 5;
            float iconSize = 100f;
            float spacing = 10f;
            float gridW = cols * iconSize + (cols - 1) * spacing;
            float startX = -gridW / 2f + iconSize / 2f;
            float startY = 220f;

            _categoryBGs = new Image[_categories.Length];

            for (int i = 0; i < _categories.Length; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float x = startX + col * (iconSize + spacing);
                float y = startY - row * (iconSize + spacing);

                var cellGO = new GameObject(_categories[i]);
                cellGO.transform.SetParent(_gridRoot.transform, false);
                var cellRT = cellGO.AddComponent<RectTransform>();
                cellRT.anchorMin = cellRT.anchorMax = new Vector2(0.5f, 0.5f);
                cellRT.anchoredPosition = new Vector2(x, y);
                cellRT.sizeDelta = new Vector2(iconSize, iconSize);

                var cellImg = cellGO.AddComponent<Image>();
                cellImg.color = BTN_COLOR;
                _categoryBGs[i] = cellImg;

                // Nazwa kafelka -- centrowana
                var labelGO = new GameObject("Label");
                labelGO.transform.SetParent(cellGO.transform, false);
                var labelRT = labelGO.AddComponent<RectTransform>();
                labelRT.anchorMin = Vector2.zero;
                labelRT.anchorMax = Vector2.one;
                labelRT.offsetMin = Vector2.zero;
                labelRT.offsetMax = Vector2.zero;
                var labelText = labelGO.AddComponent<Text>();
                labelText.text = _categories[i];
                labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                labelText.fontSize = 16;
                labelText.color = TEXT_WHITE;
                labelText.alignment = TextAnchor.MiddleCenter;
            }
        }

        private void BuildFooter()
        {
            var selGO = new GameObject("SelectedLabel");
            selGO.transform.SetParent(_canvas.transform, false);
            var selRT = selGO.AddComponent<RectTransform>();
            selRT.anchorMin = new Vector2(0, 0);
            selRT.anchorMax = new Vector2(1, 0);
            selRT.pivot = new Vector2(0.5f, 0);
            selRT.sizeDelta = new Vector2(0, 40);
            selRT.anchoredPosition = new Vector2(0, 45);
            _selectedLabel = selGO.AddComponent<Text>();
            _selectedLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _selectedLabel.fontSize = 22;
            _selectedLabel.color = TEXT_WHITE;
            _selectedLabel.alignment = TextAnchor.MiddleCenter;

            var valGO = new GameObject("ValueText");
            valGO.transform.SetParent(_canvas.transform, false);
            var valRT = valGO.AddComponent<RectTransform>();
            valRT.anchorMin = new Vector2(0, 0);
            valRT.anchorMax = new Vector2(1, 0);
            valRT.pivot = new Vector2(0.5f, 0);
            valRT.sizeDelta = new Vector2(0, 30);
            valRT.anchoredPosition = new Vector2(0, 10);
            _valueText = valGO.AddComponent<Text>();
            _valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _valueText.fontSize = 18;
            _valueText.color = TEXT_GREY;
            _valueText.alignment = TextAnchor.MiddleCenter;
            _valueText.text = "A = wejdz    B = wstecz";
        }

        // =====================================================================
        // Update wizualu
        // =====================================================================

        private void UpdateSelection()
        {
            for (int i = 0; i < _categoryBGs.Length; i++)
                _categoryBGs[i].color = (i == _selectedIndex) ? BTN_SELECTED : BTN_COLOR;

            _selectedLabel.text = "> " + _categories[_selectedIndex] + " <";
            UpdateValueDisplay();
        }

        private void UpdateValueDisplay()
        {
            if (!_inSubmenu)
                _valueText.text = "A = wejdz    B = wstecz";
        }
    }
}
