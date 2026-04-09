// =============================================================================
// InventoryScreen.cs
// CYBERNOMAD -- Ekran ekwipunku gracza dla PLAGA '44.
//
// World-space canvas przed graczem:
//   - LEWA STRONA: model gracza w T-pose renderowany do RenderTexture
//     (osobna kamera, Body WIDOCZNE -- pelny podglad modelu)
//     Obracanie: prawy thumbstick / strzalki w edytorze
//   - PRAWA STRONA: sloty equipmentu (HEAD, FACE, TORSO, LEGS, FEET, HANDS)
//     Kazdy slot = toggle ON/OFF (zdejmuje/zaklada ubranie na modelu gracza)
//
// Sterowanie:
//   VR:      Menu button (Start) lub "INVENTORY" z hamburger menu
//   Edytor:  I = toggle, strzalki = obracanie modelu
//   Wyjscie: Escape / Menu button
//
// GameState: wchodzi w GamePhase.Inventory, wychodzi do Playing.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.UI
{
    /// <summary>
    /// Equipment slot definition -- maps a display name to sub-mesh names on the model.
    /// </summary>
    [Serializable]
    public struct EquipmentSlot
    {
        public string displayName;     // np. "HEAD"
        public string[] submeshNames;  // np. { "Hats" }

        public EquipmentSlot(string display, params string[] meshes)
        {
            displayName = display;
            submeshNames = meshes;
        }
    }

    public class InventoryScreen : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Inventory]";

        // =====================================================================
        // Singleton
        // =====================================================================

        public static InventoryScreen Instance { get; private set; }

        // =====================================================================
        // Config
        // =====================================================================

        [Header("Canvas placement")]
        [Tooltip("Odleglosc ekranu inventory od glowy gracza.")]
        public float screenDistance = 1.8f;

        [Tooltip("Szerokosc ekranu w metrach.")]
        public float screenWidth = 1.2f;

        [Tooltip("Wysokosc ekranu w metrach.")]
        public float screenHeight = 0.9f;

        [Header("Model preview")]
        [Tooltip("Predkosc obracania modelu (stopnie/sek).")]
        public float rotationSpeed = 60f;

        [Tooltip("RenderTexture resolution.")]
        public int renderTextureSize = 512;

        // =====================================================================
        // Equipment slot definitions
        // =====================================================================

        private static readonly EquipmentSlot[] EQUIPMENT_SLOTS = new[]
        {
            new EquipmentSlot("HEAD",  "Hats"),
            new EquipmentSlot("FACE",  "Masks", "Eyewear"),
            new EquipmentSlot("TORSO", "Tops"),
            new EquipmentSlot("LEGS",  "Bottoms"),
            new EquipmentSlot("FEET",  "Shoes"),
            new EquipmentSlot("HANDS", "Gloves"),
        };

        // =====================================================================
        // State
        // =====================================================================

        private Canvas _canvas;
        private GameObject _screenPanel;
        private Transform _headTransform;
        private bool _isOpen;

        // Model preview
        private Camera _previewCamera;
        private RenderTexture _previewRT;
        private GameObject _previewModel;
        private float _previewYaw;
        private RawImage _previewImage;

        // Slot UI elements
        private readonly List<SlotUI> _slotUIs = new List<SlotUI>();

        private struct SlotUI
        {
            public EquipmentSlot slot;
            public Text statusText;
            public Image background;
        }

        // Colors
        private static readonly Color BG_COLOR = new Color(0.04f, 0.04f, 0.06f, 0.95f);
        private static readonly Color PANEL_COLOR = new Color(0.08f, 0.08f, 0.1f, 0.9f);
        private static readonly Color SLOT_ON = new Color(0.1f, 0.22f, 0.1f);
        private static readonly Color SLOT_OFF = new Color(0.2f, 0.08f, 0.08f);
        private static readonly Color SLOT_HOVER = new Color(0.2f, 0.2f, 0.25f);
        private static readonly Color HEADER_COLOR = new Color(0.9f, 0.5f, 0.1f);
        private static readonly Color TEXT_COLOR = Color.white;
        private static readonly Color SEPARATOR_COLOR = new Color(0.3f, 0.3f, 0.3f, 0.5f);

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

            BuildUI();
            Hide();
        }

        private void Start()
        {
            _headTransform = FindHead();
            Debug.Log($"{LOG} Start: head={_headTransform?.name ?? "NULL"}");

            // Subscribe to state changes for auto-close
            GameState.OnStateChanged += OnGameStateChanged;
        }

        private void Update()
        {
            // Toggle input
            if (GetInventoryToggleInput())
                Toggle();

            // Close on escape/menu when open
            if (_isOpen && GetCloseInput())
                Hide();

            if (!_isOpen) return;

            // Rotate preview model
            HandlePreviewRotation();

            // Keep canvas facing player
            if (_headTransform != null)
                FaceHead();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            GameState.OnStateChanged -= OnGameStateChanged;

            // Cleanup
            if (_previewRT != null)
            {
                _previewRT.Release();
                Destroy(_previewRT);
            }
            if (_previewModel != null)
                Destroy(_previewModel);
            if (_previewCamera != null)
                Destroy(_previewCamera.gameObject);
        }

        // =====================================================================
        // Public API
        // =====================================================================

        public void Toggle()
        {
            if (_isOpen) Hide();
            else Show();
        }

        public void Show()
        {
            if (_isOpen) return;

            _isOpen = true;
            _screenPanel.SetActive(true);

            // Position in front of player
            if (_headTransform != null)
                PositionInFrontOfHead();

            // Setup preview model
            SetupPreviewModel();
            RefreshSlotStates();

            // Enable preview camera
            if (_previewCamera != null)
                _previewCamera.enabled = true;

            // Hamburger menu should close
            if (HamburgerMenu.Instance != null && HamburgerMenu.Instance.IsOpen)
                HamburgerMenu.Instance.Hide();

            GameState.Inventory();
            Debug.Log($"{LOG} OPEN");
        }

        public void Hide()
        {
            _isOpen = false;
            if (_screenPanel != null)
                _screenPanel.SetActive(false);

            // Disable preview camera (performance)
            if (_previewCamera != null)
                _previewCamera.enabled = false;

            // Cleanup preview model
            if (_previewModel != null)
            {
                Destroy(_previewModel);
                _previewModel = null;
            }

            // Return to playing if we were in inventory
            if (GameState.Current == GamePhase.Inventory)
                GameState.Play();

            Debug.Log($"{LOG} CLOSE");
        }

        public bool IsOpen => _isOpen;

        // =====================================================================
        // GameState listener
        // =====================================================================

        private void OnGameStateChanged(GamePhase oldState, GamePhase newState)
        {
            // If someone else changes state away from Inventory while we're open, close
            if (_isOpen && newState != GamePhase.Inventory)
                Hide();
        }

        // =====================================================================
        // Preview model
        // =====================================================================

        /// <summary>
        /// Instantiate a copy of the player model for the preview camera.
        /// All sub-meshes VISIBLE (including Body) so player sees full model.
        /// </summary>
        private void SetupPreviewModel()
        {
            if (_previewModel != null)
                Destroy(_previewModel);

            // Load model
            GameObject prefab = Resources.Load<GameObject>("PLAYER_rigged");

#if UNITY_EDITOR
            if (prefab == null)
            {
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Characters/Player/PLAYER_rigged.fbx");
            }
#endif

            if (prefab == null)
            {
                Debug.LogError($"{LOG} PLAYER_rigged nie znaleziony dla preview!");
                return;
            }

            // Spawn at preview camera position, offset back
            Vector3 previewPos = _previewCamera.transform.position + _previewCamera.transform.forward * 1.5f;
            _previewModel = Instantiate(prefab, previewPos, Quaternion.identity);
            _previewModel.name = "InventoryPreviewModel";
            _previewModel.transform.localScale = Vector3.one * 0.01f; // same scale as avatar

            // Reset yaw
            _previewYaw = 180f; // face camera

            // Place on preview layer (use layer 31 -- unlikely to conflict)
            SetLayerRecursive(_previewModel, 31);

            // Enable ALL renderers (including Body, Eyes etc)
            foreach (var r in _previewModel.GetComponentsInChildren<Renderer>(true))
                r.enabled = true;

            // But mirror current equipment state from PlayerAvatar
            if (PlayerAvatar.Instance != null)
            {
                foreach (var slot in EQUIPMENT_SLOTS)
                {
                    foreach (var meshName in slot.submeshNames)
                    {
                        bool visible = PlayerAvatar.Instance.IsSubmeshVisible(meshName);
                        SetPreviewSubmeshVisible(meshName, visible);
                    }
                }
            }

            // Disable animator (we want T-pose)
            var animator = _previewModel.GetComponent<Animator>();
            if (animator != null)
                animator.enabled = false;

            // Position model so it's centered in camera view
            // Model is in cm (scale 0.01), so height ~1.7 units at scale 0.01 = 0.017
            // But we need to adjust based on actual bounds
            _previewModel.transform.position = previewPos - Vector3.up * 0.008f;
            _previewModel.transform.rotation = Quaternion.Euler(0f, _previewYaw, 0f);

            Debug.Log($"{LOG} Preview model spawned");
        }

        private void SetPreviewSubmeshVisible(string meshName, bool visible)
        {
            if (_previewModel == null) return;
            foreach (var r in _previewModel.GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject.name == meshName || r.gameObject.name.Contains(meshName))
                {
                    // Don't hide Body/Eyes/Eyelashes in preview -- those stay visible
                    if (meshName == "Body" || meshName == "Eyes" || meshName == "Eyelashes")
                        r.enabled = true;
                    else
                        r.enabled = visible;
                }
            }
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        private void HandlePreviewRotation()
        {
            float input = 0f;

#if HAS_META_XR
            // Right thumbstick X axis for rotation
            input = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).x;
#endif
            // Keyboard fallback
            if (UnityEngine.Input.GetKey(KeyCode.LeftArrow)) input -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.RightArrow)) input += 1f;

            if (Mathf.Abs(input) > 0.1f)
            {
                _previewYaw += input * rotationSpeed * Time.unscaledDeltaTime;
                if (_previewModel != null)
                    _previewModel.transform.rotation = Quaternion.Euler(0f, _previewYaw, 0f);
            }
        }

        // =====================================================================
        // Slot management
        // =====================================================================

        private void RefreshSlotStates()
        {
            if (PlayerAvatar.Instance == null) return;

            foreach (var slotUI in _slotUIs)
            {
                bool anyVisible = false;
                foreach (var meshName in slotUI.slot.submeshNames)
                {
                    if (PlayerAvatar.Instance.IsSubmeshVisible(meshName))
                    {
                        anyVisible = true;
                        break;
                    }
                }
                UpdateSlotVisual(slotUI, anyVisible);
            }
        }

        private void ToggleSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slotUIs.Count) return;
            if (PlayerAvatar.Instance == null) return;

            var slotUI = _slotUIs[slotIndex];

            // Check current state (any visible = equipped)
            bool anyVisible = false;
            foreach (var meshName in slotUI.slot.submeshNames)
            {
                if (PlayerAvatar.Instance.IsSubmeshVisible(meshName))
                {
                    anyVisible = true;
                    break;
                }
            }

            // Toggle: if any visible -> hide all, if none visible -> show all
            bool newState = !anyVisible;
            foreach (var meshName in slotUI.slot.submeshNames)
            {
                PlayerAvatar.Instance.SetSubmeshVisible(meshName, newState);

                // Also update preview model
                SetPreviewSubmeshVisible(meshName, newState);
            }

            UpdateSlotVisual(slotUI, newState);
            Debug.Log($"{LOG} Slot '{slotUI.slot.displayName}' -> {(newState ? "EQUIPPED" : "REMOVED")}");
        }

        private void UpdateSlotVisual(SlotUI slotUI, bool equipped)
        {
            slotUI.statusText.text = equipped ? "EQUIPPED" : "---";
            slotUI.statusText.color = equipped ? new Color(0.3f, 1f, 0.3f) : new Color(0.6f, 0.3f, 0.3f);
            slotUI.background.color = equipped ? SLOT_ON : SLOT_OFF;
        }

        // =====================================================================
        // Build UI
        // =====================================================================

        private void BuildUI()
        {
            // --- Canvas ---
            var canvasGO = new GameObject("InventoryCanvas");
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 110; // above hamburger menu

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;

            canvasGO.AddComponent<GraphicRaycaster>();

            var canvasRect = canvasGO.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(screenWidth * 1000f, screenHeight * 1000f);
            canvasRect.localScale = Vector3.one * 0.001f;

            // --- Main panel ---
            _screenPanel = new GameObject("ScreenPanel");
            _screenPanel.transform.SetParent(_canvas.transform, false);
            var panelImg = _screenPanel.AddComponent<Image>();
            panelImg.color = BG_COLOR;
            var panelRect = _screenPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // --- Header ---
            BuildHeader(_screenPanel.transform);

            // --- Content area (under header) ---
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(_screenPanel.transform, false);
            var contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.offsetMin = new Vector2(10, 10);
            contentRect.offsetMax = new Vector2(-10, -70);

            var hlg = contentGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 15;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.padding = new RectOffset(10, 10, 10, 10);

            // --- LEFT: Model preview ---
            BuildPreviewPanel(contentGO.transform);

            // --- RIGHT: Equipment slots ---
            BuildEquipmentPanel(contentGO.transform);

            Debug.Log($"{LOG} UI built");
        }

        private void BuildHeader(Transform parent)
        {
            var headerGO = new GameObject("Header");
            headerGO.transform.SetParent(parent, false);
            var headerImg = headerGO.AddComponent<Image>();
            headerImg.color = new Color(0.1f, 0.1f, 0.1f);
            var headerRect = headerGO.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, 60);

            // Title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(headerGO.transform, false);
            var titleText = titleGO.AddComponent<Text>();
            titleText.text = "EQUIPMENT";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = HEADER_COLOR;
            titleText.alignment = TextAnchor.MiddleCenter;
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            // Close hint
            var hintGO = new GameObject("CloseHint");
            hintGO.transform.SetParent(headerGO.transform, false);
            var hintText = hintGO.AddComponent<Text>();
            hintText.text = "[ESC / MENU]";
            hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hintText.fontSize = 14;
            hintText.color = new Color(0.5f, 0.5f, 0.5f);
            hintText.alignment = TextAnchor.MiddleRight;
            var hintRect = hintGO.GetComponent<RectTransform>();
            hintRect.anchorMin = Vector2.zero;
            hintRect.anchorMax = Vector2.one;
            hintRect.offsetMin = new Vector2(0, 0);
            hintRect.offsetMax = new Vector2(-15, 0);
        }

        private void BuildPreviewPanel(Transform parent)
        {
            var previewGO = new GameObject("PreviewPanel");
            previewGO.transform.SetParent(parent, false);
            var previewImg = previewGO.AddComponent<Image>();
            previewImg.color = PANEL_COLOR;
            var previewLayout = previewGO.AddComponent<LayoutElement>();
            previewLayout.flexibleWidth = 1f; // 50% width

            // RenderTexture
            _previewRT = new RenderTexture(renderTextureSize, renderTextureSize, 16);
            _previewRT.name = "InventoryPreviewRT";

            // Preview camera (hidden, renders only layer 31)
            var camGO = new GameObject("InventoryPreviewCamera");
            camGO.transform.SetParent(transform, false);
            // Position far away so it doesn't see game world
            camGO.transform.localPosition = new Vector3(1000f, 1000f, 1000f);
            camGO.transform.localRotation = Quaternion.identity;

            _previewCamera = camGO.AddComponent<Camera>();
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = new Color(0.06f, 0.06f, 0.08f, 1f);
            _previewCamera.cullingMask = 1 << 31; // only layer 31
            _previewCamera.fieldOfView = 20f;
            _previewCamera.nearClipPlane = 0.001f;
            _previewCamera.farClipPlane = 10f;
            _previewCamera.targetTexture = _previewRT;
            _previewCamera.enabled = false; // only when inventory open
            _previewCamera.depth = -10; // don't interfere

            // RawImage showing RT
            var rawImgGO = new GameObject("PreviewImage");
            rawImgGO.transform.SetParent(previewGO.transform, false);
            _previewImage = rawImgGO.AddComponent<RawImage>();
            _previewImage.texture = _previewRT;
            _previewImage.color = Color.white;
            var rawRect = rawImgGO.GetComponent<RectTransform>();
            rawRect.anchorMin = new Vector2(0.05f, 0.05f);
            rawRect.anchorMax = new Vector2(0.95f, 0.95f);
            rawRect.offsetMin = Vector2.zero;
            rawRect.offsetMax = Vector2.zero;

            // "ROTATE" hint
            var rotHintGO = new GameObject("RotateHint");
            rotHintGO.transform.SetParent(previewGO.transform, false);
            var rotText = rotHintGO.AddComponent<Text>();
            rotText.text = "< ROTATE >";
            rotText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rotText.fontSize = 14;
            rotText.color = new Color(0.5f, 0.5f, 0.5f);
            rotText.alignment = TextAnchor.LowerCenter;
            var rotRect = rotHintGO.GetComponent<RectTransform>();
            rotRect.anchorMin = new Vector2(0, 0);
            rotRect.anchorMax = new Vector2(1, 0.08f);
            rotRect.offsetMin = Vector2.zero;
            rotRect.offsetMax = Vector2.zero;
        }

        private void BuildEquipmentPanel(Transform parent)
        {
            var equipGO = new GameObject("EquipmentPanel");
            equipGO.transform.SetParent(parent, false);
            var equipImg = equipGO.AddComponent<Image>();
            equipImg.color = PANEL_COLOR;
            var equipLayout = equipGO.AddComponent<LayoutElement>();
            equipLayout.flexibleWidth = 1f; // 50% width

            // Vertical list of slots
            var slotsParent = new GameObject("Slots");
            slotsParent.transform.SetParent(equipGO.transform, false);
            var slotsRect = slotsParent.AddComponent<RectTransform>();
            slotsRect.anchorMin = new Vector2(0.05f, 0.05f);
            slotsRect.anchorMax = new Vector2(0.95f, 0.95f);
            slotsRect.offsetMin = Vector2.zero;
            slotsRect.offsetMax = Vector2.zero;

            var vlg = slotsParent.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.padding = new RectOffset(5, 5, 5, 5);

            // Equipment header
            var equipHeaderGO = new GameObject("EquipHeader");
            equipHeaderGO.transform.SetParent(slotsParent.transform, false);
            var equipHeaderText = equipHeaderGO.AddComponent<Text>();
            equipHeaderText.text = "SLOTS";
            equipHeaderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            equipHeaderText.fontSize = 20;
            equipHeaderText.fontStyle = FontStyle.Bold;
            equipHeaderText.color = HEADER_COLOR;
            equipHeaderText.alignment = TextAnchor.MiddleCenter;
            var ehl = equipHeaderGO.AddComponent<LayoutElement>();
            ehl.preferredHeight = 30;

            // Separator
            var sepGO = new GameObject("Sep");
            sepGO.transform.SetParent(slotsParent.transform, false);
            var sepImg = sepGO.AddComponent<Image>();
            sepImg.color = SEPARATOR_COLOR;
            var sepLayout = sepGO.AddComponent<LayoutElement>();
            sepLayout.preferredHeight = 2;

            // Build each slot
            _slotUIs.Clear();
            for (int i = 0; i < EQUIPMENT_SLOTS.Length; i++)
            {
                BuildSlotButton(slotsParent.transform, EQUIPMENT_SLOTS[i], i);
            }
        }

        private void BuildSlotButton(Transform parent, EquipmentSlot slot, int index)
        {
            var slotGO = new GameObject($"Slot_{slot.displayName}");
            slotGO.transform.SetParent(parent, false);
            var slotImg = slotGO.AddComponent<Image>();
            slotImg.color = SLOT_ON;
            var slotLayout = slotGO.AddComponent<LayoutElement>();
            slotLayout.preferredHeight = 50;

            // Button
            var btn = slotGO.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            btn.colors = colors;
            btn.targetGraphic = slotImg;

            int capturedIndex = index;
            btn.onClick.AddListener(() => ToggleSlot(capturedIndex));

            // Slot name (left)
            var nameGO = new GameObject("Name");
            nameGO.transform.SetParent(slotGO.transform, false);
            var nameText = nameGO.AddComponent<Text>();
            nameText.text = slot.displayName;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 20;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = TEXT_COLOR;
            nameText.alignment = TextAnchor.MiddleLeft;
            var nameRect = nameGO.GetComponent<RectTransform>();
            nameRect.anchorMin = Vector2.zero;
            nameRect.anchorMax = Vector2.one;
            nameRect.offsetMin = new Vector2(15, 0);
            nameRect.offsetMax = new Vector2(-100, 0);

            // Sub-mesh names (small, under the name)
            string meshList = string.Join(", ", slot.submeshNames);
            var meshGO = new GameObject("MeshNames");
            meshGO.transform.SetParent(slotGO.transform, false);
            var meshText = meshGO.AddComponent<Text>();
            meshText.text = meshList;
            meshText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            meshText.fontSize = 12;
            meshText.color = new Color(0.5f, 0.5f, 0.5f);
            meshText.alignment = TextAnchor.LowerLeft;
            var meshRect = meshGO.GetComponent<RectTransform>();
            meshRect.anchorMin = new Vector2(0, 0);
            meshRect.anchorMax = new Vector2(0.6f, 0.4f);
            meshRect.offsetMin = new Vector2(15, 2);
            meshRect.offsetMax = Vector2.zero;

            // Status (right)
            var statusGO = new GameObject("Status");
            statusGO.transform.SetParent(slotGO.transform, false);
            var statusText = statusGO.AddComponent<Text>();
            statusText.text = "EQUIPPED";
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.fontSize = 16;
            statusText.color = new Color(0.3f, 1f, 0.3f);
            statusText.alignment = TextAnchor.MiddleRight;
            var statusRect = statusGO.GetComponent<RectTransform>();
            statusRect.anchorMin = Vector2.zero;
            statusRect.anchorMax = Vector2.one;
            statusRect.offsetMin = new Vector2(0, 0);
            statusRect.offsetMax = new Vector2(-15, 0);

            _slotUIs.Add(new SlotUI
            {
                slot = slot,
                statusText = statusText,
                background = slotImg
            });
        }

        // =====================================================================
        // Positioning
        // =====================================================================

        private void PositionInFrontOfHead()
        {
            Vector3 fwd = _headTransform.forward;
            fwd.y = 0;
            fwd.Normalize();
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;

            Vector3 pos = _headTransform.position + fwd * screenDistance;
            pos.y = _headTransform.position.y - 0.05f;

            _canvas.transform.position = pos;
            _canvas.transform.rotation = Quaternion.LookRotation(fwd);
        }

        private void FaceHead()
        {
            Vector3 toHead = _headTransform.position - _canvas.transform.position;
            toHead.y = 0;
            if (toHead.sqrMagnitude > 0.001f)
                _canvas.transform.rotation = Quaternion.LookRotation(toHead.normalized);
        }

        // =====================================================================
        // Input
        // =====================================================================

        private bool GetInventoryToggleInput()
        {
            // Only toggle when playing (open) or in inventory (close)
            if (GameState.Current != GamePhase.Playing && GameState.Current != GamePhase.Inventory)
                return false;

            // Keyboard: I
            if (UnityEngine.Input.GetKeyDown(KeyCode.I))
                return true;

            return false;
        }

        private bool GetCloseInput()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                return true;

#if HAS_META_XR
            if (OVRInput.GetDown(OVRInput.Button.Start))
                return true;
#endif

            return false;
        }

        // =====================================================================
        // Head finding
        // =====================================================================

        private Transform FindHead()
        {
            var allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                if (t.name == "CenterEyeAnchor") return t;
            }
            if (Camera.main != null) return Camera.main.transform;
            Debug.LogWarning($"{LOG} Nie znaleziono head transform!");
            return null;
        }
    }
}
