// QuickActionWheel.cs
// CYBERNOMAD -- World-space radial menu anchored to the wrist.
//
// Activation:
//   GestureCommandMap dispatches GestureCommand.OpenWheel  -> Show()
//   GestureCommandMap dispatches GestureCommand.CloseWheel -> Hide()
//   (alternatively: hold thumb on thumbstick touch to keep open, release to execute)
//
// Selection:
//   Thumb joystick position angles determine the highlighted slot.
//   Releasing the thumb while a slot is highlighted confirms the selection.
//
// Layout:
//   4-8 slots arranged in a circle in world-space around the wrist anchor.
//   Each slot is a Unity UI panel rendered on a WorldSpace Canvas.
//   Procedurally built at runtime -- no prefab required, but a prefab can override.
//
// #if HAS_META_XR guards all OVRInput/OVRCameraRig calls.
// Namespace: Plaga44.Input

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.Input
{
    [Serializable]
    public class WheelSlot
    {
        [Tooltip("Display label shown in the slot.")]
        public string label = "Action";

        [Tooltip("Command fired when this slot is confirmed.")]
        public GestureCommand command = GestureCommand.None;

        [Tooltip("Optional icon (shown if assigned).")]
        public Sprite icon = null;
    }

    /// <summary>
    /// World-space radial quick-action wheel. Attach to an empty GameObject.
    /// The wheel parents itself to the wrist anchor at runtime.
    /// </summary>
    public class QuickActionWheel : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Slots (4-8)")]
        [Tooltip("Action slots. Between 4 and 8 entries.")]
        public List<WheelSlot> slots = new List<WheelSlot>
        {
            new WheelSlot { label = "Heal",     command = GestureCommand.QuickHeal    },
            new WheelSlot { label = "Prev",     command = GestureCommand.PreviousSlot },
            new WheelSlot { label = "Next",     command = GestureCommand.NextSlot     },
            new WheelSlot { label = "Mark",     command = GestureCommand.MarkTarget   },
            new WheelSlot { label = "Reload",   command = GestureCommand.Reload       },
            new WheelSlot { label = "Map",      command = GestureCommand.ToggleMap    },
        };

        [Header("Layout")]
        [Tooltip("Radius of the wheel in world-space metres.")]
        [Range(0.05f, 0.3f)]
        public float wheelRadius = 0.12f;

        [Tooltip("Offset from wrist anchor (local space).")]
        public Vector3 wristOffset = new Vector3(0f, 0.05f, 0f);

        [Tooltip("Hand whose wrist the wheel follows.")]
        public Hand wristHand = Hand.Left;

        [Tooltip("Thumbstick deadzone for slot selection (0..1).")]
        [Range(0.1f, 0.8f)]
        public float selectionDeadzone = 0.25f;

        [Header("Visual")]
        public Color normalColor    = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        public Color highlightColor = new Color(0.0f,  0.6f,  1.0f,  0.95f);
        public Color labelColor     = Color.white;
        [Range(12, 32)]
        public int   fontSize       = 18;

        [Header("Gesture Source")]
        [Tooltip("Optional -- if assigned, subscribes to OnCommand automatically.")]
        public GestureCommandMap commandMap;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fired when the player confirms a slot selection.</summary>
        public event Action<GestureCommand> OnSlotSelected;

        // ── Private ───────────────────────────────────────────────────────────

        private bool          _isVisible;
        private int           _highlightedSlot = -1;
        private Canvas        _canvas;
        private RectTransform _canvasRect;

        // Per-slot UI references
        private readonly List<Image>  _slotImages = new List<Image>();
        private readonly List<Text>   _slotLabels = new List<Text>();
        private readonly List<Image>  _slotIcons  = new List<Image>();

        // World-space positions of slot centres (recomputed on enable)
        private readonly List<Vector3> _slotLocalPositions = new List<Vector3>();

        // Anchor transform (wrist)
        private Transform _wristAnchor;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (commandMap != null)
                commandMap.OnCommand += HandleCommand;
        }

        private void OnDisable()
        {
            if (commandMap != null)
                commandMap.OnCommand -= HandleCommand;
        }

        private void Update()
        {
            if (!_isVisible) return;

            UpdateWristAnchor();
            UpdateWheelTransform();
            UpdateSlotHighlight();
            CheckConfirm();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Show the radial wheel.</summary>
        public void Show()
        {
            _highlightedSlot = -1;
            SetVisible(true);
            UpdateSlotColors();
        }

        /// <summary>Hide the radial wheel without executing any command.</summary>
        public void Hide()
        {
            SetVisible(false);
        }

        /// <summary>Confirm the currently highlighted slot (if any).</summary>
        public void ConfirmSelection()
        {
            if (_highlightedSlot >= 0 && _highlightedSlot < slots.Count)
            {
                var cmd = slots[_highlightedSlot].command;
                Hide();
                if (cmd != GestureCommand.None)
                    OnSlotSelected?.Invoke(cmd);
            }
            else
            {
                Hide();
            }
        }

        // ── GestureCommandMap handler ─────────────────────────────────────────

        private void HandleCommand(GestureCommand command, Hand hand)
        {
            if (command == GestureCommand.OpenWheel)  Show();
            if (command == GestureCommand.CloseWheel) Hide();
        }

        // ── Update helpers ────────────────────────────────────────────────────

        private void UpdateWristAnchor()
        {
#if HAS_META_XR
            if (_wristAnchor == null)
            {
                var rig = FindFirstObjectByType<OVRCameraRig>();
                if (rig != null)
                {
                    _wristAnchor = wristHand == Hand.Left
                        ? rig.leftControllerAnchor
                        : rig.rightControllerAnchor;
                }
            }
#endif
        }

        private void UpdateWheelTransform()
        {
            if (_wristAnchor == null) return;

            transform.position = _wristAnchor.position
                + _wristAnchor.TransformDirection(wristOffset);

            // Face camera
            var cam = Camera.main;
            if (cam != null)
            {
                transform.rotation = Quaternion.LookRotation(
                    transform.position - cam.transform.position,
                    Vector3.up);
            }
        }

        private void UpdateSlotHighlight()
        {
#if HAS_META_XR
            var selectionCtrl = wristHand == Hand.Left
                ? OVRInput.Controller.RTouch   // opposite hand selects
                : OVRInput.Controller.LTouch;

            Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, selectionCtrl);
#else
            Vector2 stick = Vector2.zero;
#endif
            int prev = _highlightedSlot;

            if (stick.magnitude >= selectionDeadzone && slots.Count > 0)
            {
                float angle = Mathf.Atan2(stick.x, stick.y) * Mathf.Rad2Deg;
                if (angle < 0f) angle += 360f;

                float slotAngle = 360f / slots.Count;
                _highlightedSlot = Mathf.FloorToInt((angle + slotAngle * 0.5f) / slotAngle) % slots.Count;
            }
            else
            {
                _highlightedSlot = -1;
            }

            if (_highlightedSlot != prev)
                UpdateSlotColors();
        }

        private void CheckConfirm()
        {
#if HAS_META_XR
            var selectionCtrl = wristHand == Hand.Left
                ? OVRInput.Controller.RTouch
                : OVRInput.Controller.LTouch;

            // Confirm on thumb-up (touch released while slot highlighted)
            bool thumbTouch = OVRInput.Get(OVRInput.Touch.PrimaryThumbstick, selectionCtrl);
            if (!thumbTouch && _highlightedSlot >= 0)
            {
                ConfirmSelection();
            }
#endif
        }

        // ── UI building ───────────────────────────────────────────────────────

        private void BuildUI()
        {
            int count = Mathf.Clamp(slots.Count, 4, 8);

            // World-space canvas
            var canvasGO = new GameObject("QuickWheel_Canvas");
            canvasGO.transform.SetParent(transform, false);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            _canvasRect = _canvas.GetComponent<RectTransform>();
            _canvasRect.sizeDelta  = new Vector2(600, 600);
            _canvasRect.localScale = Vector3.one * 0.0003f;

            _slotImages.Clear();
            _slotLabels.Clear();
            _slotIcons.Clear();
            _slotLocalPositions.Clear();

            float angleStep = 360f / count;

            for (int i = 0; i < count; i++)
            {
                float angleDeg = i * angleStep;
                float angleRad = angleDeg * Mathf.Deg2Rad;

                // Position on the canvas in rect space (radius in pixels)
                float pixelRadius = 200f;
                Vector2 localPos  = new Vector2(
                    Mathf.Sin(angleRad) * pixelRadius,
                    Mathf.Cos(angleRad) * pixelRadius);

                _slotLocalPositions.Add(new Vector3(localPos.x, localPos.y, 0f));

                // Slot background
                var slotGO = new GameObject($"Slot_{i}");
                slotGO.transform.SetParent(canvasGO.transform, false);

                var slotRect = slotGO.AddComponent<RectTransform>();
                slotRect.anchoredPosition = localPos;
                slotRect.sizeDelta        = new Vector2(90, 90);

                var img = slotGO.AddComponent<Image>();
                img.color = normalColor;
                _slotImages.Add(img);

                // Label
                var labelGO = new GameObject("Label");
                labelGO.transform.SetParent(slotGO.transform, false);

                var labelRect = labelGO.AddComponent<RectTransform>();
                labelRect.sizeDelta        = new Vector2(86, 86);
                labelRect.anchoredPosition = Vector2.zero;

                var txt = labelGO.AddComponent<Text>();
                txt.text      = i < slots.Count ? slots[i].label : "";
                txt.font      = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
                txt.fontSize  = fontSize;
                txt.color     = labelColor;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.supportRichText   = true;
                txt.horizontalOverflow = HorizontalWrapMode.Wrap;
                txt.verticalOverflow   = VerticalWrapMode.Overflow;
                _slotLabels.Add(txt);

                // Optional icon placeholder
                var iconGO = new GameObject("Icon");
                iconGO.transform.SetParent(slotGO.transform, false);
                var iconRect = iconGO.AddComponent<RectTransform>();
                iconRect.sizeDelta        = new Vector2(40, 40);
                iconRect.anchoredPosition = new Vector2(0, 12);
                var iconImg = iconGO.AddComponent<Image>();
                iconImg.color = Color.clear;
                if (i < slots.Count && slots[i].icon != null)
                {
                    iconImg.sprite = slots[i].icon;
                    iconImg.color  = Color.white;
                    txt.rectTransform.anchoredPosition = new Vector2(0, -18);
                }
                _slotIcons.Add(iconImg);
            }
        }

        private void UpdateSlotColors()
        {
            for (int i = 0; i < _slotImages.Count; i++)
            {
                _slotImages[i].color = (i == _highlightedSlot) ? highlightColor : normalColor;
            }
        }

        private void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_canvas != null)
                _canvas.gameObject.SetActive(visible);
        }
    }
}
