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
        private const int CANVAS_W = 700;
        private const int CANVAS_H = 500;

        // Kolory -- dark theme
        private static readonly Color BG_COLOR = new Color(0.08f, 0.08f, 0.08f, 0.92f);
        private static readonly Color BTN_COLOR = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color BTN_HOVER = new Color(0.30f, 0.30f, 0.30f);
        private static readonly Color BTN_SELECTED = new Color(0.20f, 0.35f, 0.55f);
        private static readonly Color ACCENT = new Color(0.9f, 0.5f, 0.1f);
        private static readonly Color TEXT_WHITE = Color.white;
        private static readonly Color TEXT_GREY = new Color(0.55f, 0.55f, 0.55f);

        // =====================================================================
        // Kategorie menu (6 ikon)
        // =====================================================================

        private static readonly string[] CATEGORIES = new string[]
        {
            "MISC.",
            "SUBMENU 2",
            "SUBMENU 3",
            "SUBMENU 4",
            "SUBMENU 5",
            "SUBMENU 6"
        };

        private static readonly string[] CATEGORY_ICONS = new string[]
        {
            "*",
            "2",
            "3",
            "4",
            "5",
            "6",
        };

        // =====================================================================
        // Stan
        // =====================================================================

        private Canvas _canvas;
        private Image[] _categoryBGs;
        private Text _selectedLabel;
        private Text _valueText;
        private int _selectedIndex;
        private float _value;

        private float _lastStickTime;
        private const float STICK_COOLDOWN = 0.2f;
        private float _lastTriggerTime;
        private const float TRIGGER_COOLDOWN = 0.25f;

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
            BuildCanvas();
            BuildGrid();
            BuildFooter();
            _canvas.gameObject.SetActive(false);
            UpdateSelection();
            Debug.Log($"{LOG} Start: gotowe, 6 kategorii, rig={(_rig != null ? _rig.name : "NULL")}");
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

            HandleThumbstick();
            HandleTriggers();
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

            if (stick.x > 0.5f) { MoveSelection(1); _lastStickTime = Time.unscaledTime; }
            else if (stick.x < -0.5f) { MoveSelection(-1); _lastStickTime = Time.unscaledTime; }
            else if (stick.y > 0.5f) { MoveSelection(-3); _lastStickTime = Time.unscaledTime; }
            else if (stick.y < -0.5f) { MoveSelection(3); _lastStickTime = Time.unscaledTime; }
        }

        private void MoveSelection(int delta)
        {
            int newIndex = _selectedIndex + delta;
            if (newIndex < 0 || newIndex >= CATEGORIES.Length) return;
            _selectedIndex = newIndex;
            UpdateSelection();
            Debug.Log($"{LOG} Wybrano: {CATEGORIES[_selectedIndex]} [{_selectedIndex}]");
        }

        // =====================================================================
        // Input -- triggers +/-
        // =====================================================================

        private void HandleTriggers()
        {
            if (Time.unscaledTime - _lastTriggerTime < TRIGGER_COOLDOWN) return;

            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch))
            {
                _value -= 1f;
                _lastTriggerTime = Time.unscaledTime;
                UpdateValueDisplay();
                Debug.Log($"{LOG} {CATEGORIES[_selectedIndex]} MINUS -> {_value}");
            }

            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                _value += 1f;
                _lastTriggerTime = Time.unscaledTime;
                UpdateValueDisplay();
                Debug.Log($"{LOG} {CATEGORIES[_selectedIndex]} PLUS -> {_value}");
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
            float iconSize = 120f;
            float spacing = 16f;
            float gridW = 3 * iconSize + 2 * spacing;
            float startX = -gridW / 2f + iconSize / 2f;
            float startY = 80f;

            _categoryBGs = new Image[CATEGORIES.Length];

            for (int i = 0; i < CATEGORIES.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                float x = startX + col * (iconSize + spacing);
                float y = startY - row * (iconSize + spacing);

                var cellGO = new GameObject(CATEGORIES[i]);
                cellGO.transform.SetParent(_canvas.transform, false);
                var cellRT = cellGO.AddComponent<RectTransform>();
                cellRT.anchorMin = cellRT.anchorMax = new Vector2(0.5f, 0.5f);
                cellRT.anchoredPosition = new Vector2(x, y);
                cellRT.sizeDelta = new Vector2(iconSize, iconSize);

                var cellImg = cellGO.AddComponent<Image>();
                cellImg.color = BTN_COLOR;
                _categoryBGs[i] = cellImg;

                // Ikona
                var iconGO = new GameObject("Icon");
                iconGO.transform.SetParent(cellGO.transform, false);
                var iconRT = iconGO.AddComponent<RectTransform>();
                iconRT.anchorMin = Vector2.zero;
                iconRT.anchorMax = Vector2.one;
                iconRT.offsetMin = new Vector2(0, 25);
                iconRT.offsetMax = Vector2.zero;
                var iconText = iconGO.AddComponent<Text>();
                iconText.text = CATEGORY_ICONS[i];
                iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                iconText.fontSize = 40;
                iconText.color = TEXT_WHITE;
                iconText.alignment = TextAnchor.MiddleCenter;

                // Label pod ikona
                var labelGO = new GameObject("Label");
                labelGO.transform.SetParent(cellGO.transform, false);
                var labelRT = labelGO.AddComponent<RectTransform>();
                labelRT.anchorMin = new Vector2(0, 0);
                labelRT.anchorMax = new Vector2(1, 0);
                labelRT.pivot = new Vector2(0.5f, 0);
                labelRT.sizeDelta = new Vector2(0, 28);
                labelRT.anchoredPosition = new Vector2(0, 4);
                var labelText = labelGO.AddComponent<Text>();
                labelText.text = CATEGORIES[i];
                labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                labelText.fontSize = 14;
                labelText.color = TEXT_GREY;
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
            _valueText.text = "L.Trigger [-]     R.Trigger [+]";
        }

        // =====================================================================
        // Update wizualu
        // =====================================================================

        private void UpdateSelection()
        {
            for (int i = 0; i < _categoryBGs.Length; i++)
                _categoryBGs[i].color = (i == _selectedIndex) ? BTN_SELECTED : BTN_COLOR;

            _selectedLabel.text = "> " + CATEGORIES[_selectedIndex] + " <";
            _value = 0f;
            UpdateValueDisplay();
        }

        private void UpdateValueDisplay()
        {
            _valueText.text = $"L.Trigger [-]     {_value:+0;-0;0}     R.Trigger [+]";
        }
    }
}
