// VRInputDebug.cs
// CYBERNOMAD -- In-headset HUD debug overlay for controller input.
// Follows head (attached to CenterEyeAnchor), always visible.
// Toggle via menu: CYBERNOMAD > Debug > VR Input Debug HUD
// Auto-starts on Play when enabled + Meta XR SDK installed.
//
// Requires: com.meta.xr.sdk.core (auto-detected via HAS_META_XR define)

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

public class VRInputDebug : MonoBehaviour
{
    private const string ENABLED_KEY = "CYBERNOMAD_VRInputDebug";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
#if UNITY_EDITOR
        if (!UnityEditor.EditorPrefs.GetBool(ENABLED_KEY, false)) return;
#endif
        Spawn();
    }

    private static VRInputDebug _instance;

    public static void Spawn()
    {
        if (_instance != null) return;
        var go = new GameObject("VRInputDebug_HUD");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<VRInputDebug>();
    }

    public static void Kill()
    {
        if (_instance != null)
        {
            Destroy(_instance.gameObject);
            _instance = null;
        }
    }

    // ── Instance ────────────────────────────────────────────────────────

    private float displayDistance = 0.4f;
    private float displayScale = 0.0004f;

    private Canvas _canvas;
    private Text _leftText;
    private Text _rightText;
    private Text _headerText;
    private Transform _centerEye;

    void Start()
    {
        CreateDebugCanvas();
    }

    void Update()
    {
        if (_centerEye == null)
        {
            FindCenterEye();
            if (_centerEye == null)
            {
                var cam = Camera.main;
                if (cam != null) _centerEye = cam.transform;
                else return;
            }
        }

        _canvas.transform.position = _centerEye.position + _centerEye.forward * displayDistance;
        _canvas.transform.rotation = _centerEye.rotation;

        UpdateTexts();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ── Update display ──────────────────────────────────────────────────

    private void UpdateTexts()
    {
        // --- Header: diagnostics ---
        string header = "<b>VR INPUT DEBUG HUD</b>\n";
        header += $"FPS: {(1f / Time.unscaledDeltaTime):F0}  Frame: {Time.frameCount}\n";

#if HAS_META_XR
        var activeCtrl = OVRInput.GetActiveController();
        string connColor = activeCtrl != OVRInput.Controller.None ? "#0f0" : "#f00";
        header += $"OVR Active: <color={connColor}>{activeCtrl}</color>\n";

        // OVRManager check
        var ovrMgr = FindFirstObjectByType<OVRManager>();
        header += $"OVRManager: {(ovrMgr != null ? "<color=#0f0>YES</color>" : "<color=#f00>NO</color>")}\n";
#else
        header += "OVR: <color=#f00>NO HAS_META_XR</color>\n";
#endif

        // Unity XR device list
        var devices = new List<InputDevice>();
        InputDevices.GetDevices(devices);
        header += $"XR Devices: {devices.Count}\n";
        foreach (var dev in devices)
        {
            string role = "";
            if ((dev.characteristics & InputDeviceCharacteristics.Left) != 0) role = "L";
            else if ((dev.characteristics & InputDeviceCharacteristics.Right) != 0) role = "R";
            else if ((dev.characteristics & InputDeviceCharacteristics.HeadMounted) != 0) role = "HMD";
            header += $"  <color=#0f0>[{role}]</color> {dev.name}\n";
        }

        _headerText.text = header;

        // --- Controllers ---
        _leftText.text = GetControllerState("LEFT", true);
        _rightText.text = GetControllerState("RIGHT", false);
    }

    // ── Controller state (dual: OVRInput + Unity XR) ────────────────────

    private string GetControllerState(string hand, bool isLeft)
    {
        string s = $"<b>=== {hand} ===</b>\n";

        // --- Unity XR InputDevice ---
        InputDevice xrDevice = default;
        var characteristics = InputDeviceCharacteristics.Controller |
            (isLeft ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right);
        var controllers = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(characteristics, controllers);

        bool xrConnected = controllers.Count > 0;
        if (xrConnected) xrDevice = controllers[0];

        s += $"XR Connected: {Colored(xrConnected)}\n";

#if HAS_META_XR
        var ctrl = isLeft ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        bool ovrConnected = OVRInput.IsControllerConnected(ctrl);
        s += $"OVR Connected: {Colored(ovrConnected)}\n\n";
#else
        s += "\n";
#endif

        if (!xrConnected)
        {
            s += "<color=#888>No controller detected</color>\n";
            return s;
        }

        // --- Buttons via Unity XR ---
        s += "<b>-- Buttons (XR) --</b>\n";

        bool primaryBtn, secondaryBtn, menuBtn, stickBtn;
        xrDevice.TryGetFeatureValue(CommonUsages.primaryButton, out primaryBtn);
        xrDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryBtn);
        xrDevice.TryGetFeatureValue(CommonUsages.menuButton, out menuBtn);
        xrDevice.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out stickBtn);

        s += $"{(isLeft ? "X" : "A")}:       {BtnStr(primaryBtn)}\n";
        s += $"{(isLeft ? "Y" : "B")}:       {BtnStr(secondaryBtn)}\n";
        if (isLeft) s += $"Menu:    {BtnStr(menuBtn)}\n";
        s += $"Stick:   {BtnStr(stickBtn)}\n\n";

        // --- Triggers ---
        s += "<b>-- Triggers --</b>\n";
        float trigger, grip;
        xrDevice.TryGetFeatureValue(CommonUsages.trigger, out trigger);
        xrDevice.TryGetFeatureValue(CommonUsages.grip, out grip);
        s += $"Index:   {Bar(trigger)} {trigger:F2}\n";
        s += $"Grip:    {Bar(grip)} {grip:F2}\n\n";

        // --- Thumbstick ---
        s += "<b>-- Thumbstick --</b>\n";
        Vector2 stick;
        xrDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out stick);
        s += $"X: {stick.x:+0.00;-0.00}  Y: {stick.y:+0.00;-0.00}\n";
        s += $"Dir: {StickVisual(stick)}\n\n";

        // --- Touch (OVR only) ---
#if HAS_META_XR
        s += "<b>-- Touch (OVR) --</b>\n";
        if (isLeft)
        {
            s += $"X:     {Touch(OVRInput.Touch.Three, ctrl)}\n";
            s += $"Y:     {Touch(OVRInput.Touch.Four, ctrl)}\n";
        }
        else
        {
            s += $"A:     {Touch(OVRInput.Touch.One, ctrl)}\n";
            s += $"B:     {Touch(OVRInput.Touch.Two, ctrl)}\n";
        }
        s += $"Stick: {Touch(OVRInput.Touch.PrimaryThumbstick, ctrl)}\n";
        s += $"Index: {Touch(OVRInput.Touch.PrimaryIndexTrigger, ctrl)}\n";
#endif

        // --- Position/Rotation ---
        s += "\n<b>-- Tracking --</b>\n";
        Vector3 pos;
        Quaternion rot;
        xrDevice.TryGetFeatureValue(CommonUsages.devicePosition, out pos);
        xrDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out rot);
        s += $"Pos: {pos.x:F2} {pos.y:F2} {pos.z:F2}\n";
        s += $"Rot: {rot.eulerAngles.x:F0} {rot.eulerAngles.y:F0} {rot.eulerAngles.z:F0}\n";

        bool tracked;
        xrDevice.TryGetFeatureValue(CommonUsages.isTracked, out tracked);
        s += $"Tracked: {Colored(tracked)}\n";

        return s;
    }

    // ── Formatting helpers ──────────────────────────────────────────────

    private string BtnStr(bool pressed)
    {
        return pressed ? "<color=#0f0><b>[PRESSED]</b></color>" : "<color=#888>[      ]</color>";
    }

#if HAS_META_XR
    private string Touch(OVRInput.Touch touch, OVRInput.Controller ctrl)
    {
        bool touched = OVRInput.Get(touch, ctrl);
        return touched ? "<color=#ff0>[TOUCH]</color>" : "<color=#888>[     ]</color>";
    }
#endif

    private string Bar(float value)
    {
        int filled = Mathf.RoundToInt(value * 10);
        string bar = "";
        for (int i = 0; i < 10; i++)
            bar += i < filled ? "#" : ".";
        string color = value > 0.8f ? "#0f0" : value > 0.1f ? "#ff0" : "#888";
        return $"<color={color}>[{bar}]</color>";
    }

    private string Colored(bool val)
    {
        return val ? "<color=#0f0>YES</color>" : "<color=#f00>NO</color>";
    }

    private string StickVisual(Vector2 v)
    {
        if (v.magnitude < 0.1f) return "O";
        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;
        if (angle >= 337.5f || angle < 22.5f) return "-->";
        if (angle < 67.5f) return "/^";
        if (angle < 112.5f) return "^";
        if (angle < 157.5f) return "^\\";
        if (angle < 202.5f) return "<--";
        if (angle < 247.5f) return "\\v";
        if (angle < 292.5f) return "v";
        return "v/";
    }

    // ── Canvas setup ────────────────────────────────────────────────────

    private void FindCenterEye()
    {
#if HAS_META_XR
        var rig = FindFirstObjectByType<OVRCameraRig>();
        if (rig != null)
        {
            _centerEye = rig.centerEyeAnchor;
            return;
        }
#endif
        var cam = Camera.main;
        if (cam != null) _centerEye = cam.transform;
    }

    private void CreateDebugCanvas()
    {
        var canvasGO = new GameObject("VRInputDebug_Canvas");
        canvasGO.transform.SetParent(transform);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;

        var rect = _canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1000, 900);
        rect.localScale = Vector3.one * displayScale;

        var bg = canvasGO.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.3f);

        _headerText = CreateText(canvasGO.transform, "Header",
            new Vector2(0, 380), new Vector2(880, 250), TextAnchor.UpperCenter, 18);

        _leftText = CreateText(canvasGO.transform, "Left",
            new Vector2(-230, 200), new Vector2(420, 600), TextAnchor.UpperLeft, 16);

        _rightText = CreateText(canvasGO.transform, "Right",
            new Vector2(230, 200), new Vector2(420, 600), TextAnchor.UpperLeft, 16);
    }

    private Text CreateText(Transform parent, string name,
        Vector2 pos, Vector2 size, TextAnchor anchor, int fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        var text = go.AddComponent<Text>();
        text.font = Font.CreateDynamicFontFromOSFont("Consolas", fontSize);
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = anchor;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return text;
    }
}
