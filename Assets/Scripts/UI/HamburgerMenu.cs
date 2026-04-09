// =============================================================================
// HamburgerMenu.cs
// CYBERNOMAD -- Hamburger menu VR dla PLAGA '44.
//
// Przycisk Menu (lewy kontroler) / Escape (edytor) = toggle menu.
// World-space canvas przypiete do glowy gracza.
// Menu jest PUSTE -- zawartosc dodaje Borys.
//
// PUBLICZNE API do dodawania elementow:
//   HamburgerMenu.Instance.AddButton("Nazwa", () => { ... });
//   HamburgerMenu.Instance.AddToggle("Nazwa", wartoscStartowa, (v) => { ... });
//   HamburgerMenu.Instance.AddSeparator();
//   HamburgerMenu.Instance.AddSubmenu("Nazwa", submenuItems);
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

        // =====================================================================
        // Config
        // =====================================================================

        [Header("Pozycja menu")]
        [Tooltip("Odleglosc menu od glowy gracza w metrach.")]
        public float menuDistance = 1.5f;

        [Tooltip("Szerokosc menu w metrach.")]
        public float menuWidth = 0.6f;

        [Tooltip("Wysokosc menu w metrach.")]
        public float menuHeight = 0.8f;

        // =====================================================================
        // Stan
        // =====================================================================

        private Canvas _canvas;
        private GameObject _menuPanel;
        private RectTransform _contentParent;
        private Transform _headTransform;
        private bool _isOpen;
        private readonly List<GameObject> _menuItems = new List<GameObject>();

        // =====================================================================
        // Styl
        // =====================================================================

        private static readonly Color BG_COLOR = new Color(0.05f, 0.05f, 0.05f, 0.92f);
        private static readonly Color HEADER_COLOR = new Color(0.9f, 0.5f, 0.1f);
        private static readonly Color BTN_COLOR = new Color(0.15f, 0.15f, 0.15f);
        private static readonly Color BTN_HOVER = new Color(0.25f, 0.25f, 0.25f);
        private static readonly Color TEXT_COLOR = Color.white;
        private static readonly Color SEPARATOR_COLOR = new Color(0.3f, 0.3f, 0.3f);

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"{LOG} Duplikat -- niszcze.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Debug.Log($"{LOG} Awake");
            BuildCanvas();
            BuildMenuPanel();
            Hide();
        }

        private void Start()
        {
            _headTransform = FindHead();
            Debug.Log($"{LOG} Start: head={_headTransform?.name ?? "NULL"}, isOpen={_isOpen}");
        }

        private void Update()
        {
            if (GetMenuToggleInput())
                Toggle();

            if (_isOpen && _headTransform != null)
                FollowHead();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        // Toggle
        // =====================================================================

        public void Toggle()
        {
            if (_isOpen) Hide();
            else Show();
        }

        public void Show()
        {
            _isOpen = true;
            _menuPanel.SetActive(true);

            // Pozycjonuj przed glowa
            if (_headTransform != null)
                PositionInFrontOfHead();

            // Pauza
            GameState.Pause();
            Debug.Log($"{LOG} OPEN");
        }

        public void Hide()
        {
            _isOpen = false;
            _menuPanel.SetActive(false);

            // Wznow gre
            if (GameState.Current == GamePhase.Paused)
                GameState.Resume();

            Debug.Log($"{LOG} CLOSE");
        }

        public bool IsOpen => _isOpen;

        // =====================================================================
        // Publiczne API -- dodawanie elementow
        // =====================================================================

        /// <summary>Dodaj przycisk do menu.</summary>
        public Button AddButton(string label, Action onClick)
        {
            var btnGO = CreateMenuItemBase(label);
            var btn = btnGO.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = BTN_COLOR;
            colors.highlightedColor = BTN_HOVER;
            colors.pressedColor = HEADER_COLOR;
            btn.colors = colors;
            btn.targetGraphic = btnGO.GetComponent<Image>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            _menuItems.Add(btnGO);
            Debug.Log($"{LOG} AddButton: {label}");
            return btn;
        }

        /// <summary>Dodaj toggle do menu.</summary>
        public Toggle AddToggle(string label, bool startValue, Action<bool> onChanged)
        {
            var itemGO = CreateMenuItemBase(label);
            var btn = itemGO.AddComponent<Button>();
            btn.targetGraphic = itemGO.GetComponent<Image>();

            // Status label (ON/OFF) po prawej
            var statusGO = new GameObject("Status");
            statusGO.transform.SetParent(itemGO.transform, false);
            var statusText = statusGO.AddComponent<Text>();
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.fontSize = 18;
            statusText.alignment = TextAnchor.MiddleRight;
            var statusRect = statusGO.GetComponent<RectTransform>();
            statusRect.anchorMin = Vector2.zero;
            statusRect.anchorMax = Vector2.one;
            statusRect.offsetMin = new Vector2(0, 0);
            statusRect.offsetMax = new Vector2(-10, 0);

            bool value = startValue;
            UpdateToggleVisual(statusText, itemGO.GetComponent<Image>(), value);

            btn.onClick.AddListener(() =>
            {
                value = !value;
                UpdateToggleVisual(statusText, itemGO.GetComponent<Image>(), value);
                onChanged?.Invoke(value);
                Debug.Log($"{LOG} Toggle '{label}' -> {value}");
            });

            _menuItems.Add(itemGO);
            Debug.Log($"{LOG} AddToggle: {label} = {startValue}");
            return null; // zwracamy null bo uzywa Button wewnetrznie
        }

        /// <summary>Dodaj separator (linia).</summary>
        public void AddSeparator()
        {
            var sepGO = new GameObject("Separator");
            sepGO.transform.SetParent(_contentParent, false);
            var img = sepGO.AddComponent<Image>();
            img.color = SEPARATOR_COLOR;
            var rect = sepGO.GetComponent<RectTransform>();
            var layout = sepGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 2;
            layout.flexibleWidth = 1;
            _menuItems.Add(sepGO);
        }

        /// <summary>Wyczysc menu (usun wszystkie elementy).</summary>
        public void Clear()
        {
            foreach (var item in _menuItems)
                if (item != null) Destroy(item);
            _menuItems.Clear();
            Debug.Log($"{LOG} Clear: menu wyczyszczone");
        }

        // =====================================================================
        // Budowanie UI
        // =====================================================================

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("HamburgerMenuCanvas");
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 100;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;

            canvasGO.AddComponent<GraphicRaycaster>();

            var canvasRect = canvasGO.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(menuWidth * 1000f, menuHeight * 1000f);
            canvasRect.localScale = Vector3.one * 0.001f; // 1px = 1mm
        }

        private void BuildMenuPanel()
        {
            // Panel tla
            _menuPanel = new GameObject("MenuPanel");
            _menuPanel.transform.SetParent(_canvas.transform, false);
            var panelImg = _menuPanel.AddComponent<Image>();
            panelImg.color = BG_COLOR;
            var panelRect = _menuPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Header
            var headerGO = new GameObject("Header");
            headerGO.transform.SetParent(_menuPanel.transform, false);
            var headerImg = headerGO.AddComponent<Image>();
            headerImg.color = new Color(0.1f, 0.1f, 0.1f);
            var headerRect = headerGO.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, 60);
            var headerLayout = headerGO.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 60;

            // Hamburger ikona
            var iconGO = new GameObject("HamburgerIcon");
            iconGO.transform.SetParent(headerGO.transform, false);
            var iconText = iconGO.AddComponent<Text>();
            iconText.text = "\u2261"; // ≡
            iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            iconText.fontSize = 36;
            iconText.color = HEADER_COLOR;
            iconText.alignment = TextAnchor.MiddleLeft;
            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(15, 0);
            iconRect.offsetMax = new Vector2(-10, 0);

            // Tytul
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(headerGO.transform, false);
            var titleText = titleGO.AddComponent<Text>();
            titleText.text = "PLAGA '44";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 24;
            titleText.color = TEXT_COLOR;
            titleText.alignment = TextAnchor.MiddleCenter;
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(50, 0);
            titleRect.offsetMax = Vector2.zero;

            // Content area z vertical layout
            var contentGO = new GameObject("Content");
            contentGO.AddComponent<RectTransform>();
            contentGO.transform.SetParent(_menuPanel.transform, false);
            var contentRect = contentGO.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.offsetMin = new Vector2(10, 10);
            contentRect.offsetMax = new Vector2(-10, -70); // pod headerem

            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 5;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.padding = new RectOffset(5, 5, 5, 5);

            _contentParent = contentRect;
        }

        private GameObject CreateMenuItemBase(string label)
        {
            var itemGO = new GameObject(label);
            itemGO.transform.SetParent(_contentParent, false);
            var img = itemGO.AddComponent<Image>();
            img.color = BTN_COLOR;
            var layout = itemGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 45;

            var textGO = new GameObject("Label");
            textGO.transform.SetParent(itemGO.transform, false);
            var text = textGO.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.color = TEXT_COLOR;
            text.alignment = TextAnchor.MiddleLeft;
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(15, 0);
            textRect.offsetMax = new Vector2(-60, 0);

            return itemGO;
        }

        private void UpdateToggleVisual(Text statusText, Image bg, bool value)
        {
            statusText.text = value ? "ON" : "OFF";
            statusText.color = value ? new Color(0.3f, 1f, 0.3f) : new Color(0.5f, 0.5f, 0.5f);
            bg.color = value ? new Color(0.12f, 0.2f, 0.12f) : BTN_COLOR;
        }

        // =====================================================================
        // Pozycjonowanie
        // =====================================================================

        private void PositionInFrontOfHead()
        {
            Vector3 fwd = _headTransform.forward;
            fwd.y = 0;
            fwd.Normalize();
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;

            Vector3 pos = _headTransform.position + fwd * menuDistance;
            pos.y = _headTransform.position.y - 0.1f; // lekko ponizej oczu

            _canvas.transform.position = pos;
            _canvas.transform.rotation = Quaternion.LookRotation(fwd);
        }

        private void FollowHead()
        {
            // Menu nie podaza za glowa -- zostaje tam gdzie sie otworzylo
            // Obraca sie tylko zeby zawsze byc przodem do gracza
            Vector3 toHead = _headTransform.position - _canvas.transform.position;
            toHead.y = 0;
            if (toHead.sqrMagnitude > 0.001f)
                _canvas.transform.rotation = Quaternion.LookRotation(toHead.normalized);
        }

        // =====================================================================
        // Input
        // =====================================================================

        private bool GetMenuToggleInput()
        {
#if HAS_META_XR
            return OVRInput.GetDown(OVRInput.Button.Start);
#else
            return UnityEngine.Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        // =====================================================================
        // Szukanie glowy
        // =====================================================================

        private Transform FindHead()
        {
            // OVRCameraRig
            var tracking = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in tracking)
            {
                if (t.name == "CenterEyeAnchor") return t;
            }

            if (Camera.main != null) return Camera.main.transform;
            Debug.LogWarning($"{LOG} Nie znaleziono head transform!");
            return null;
        }
    }
}
