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
    private Text _reticleText;
    private Text _labelText;
    private Transform _centerEye;

    private Canvas _leftCtrlCanvas;
    private Text _leftCtrlText;
    private Transform _leftCtrl;
    private GameObject _leftFollower;

    private Canvas _rightCtrlCanvas;
    private Text _rightCtrlText;
    private Transform _rightCtrl;
    private GameObject _rightFollower;

    private float ctrlPanelOffset = 0.315f;

    void Start()
    {
        CreateDebugCanvas();
        _leftCtrlCanvas = CreateCtrlCanvas("Left", ref _leftCtrlText);
        _rightCtrlCanvas = CreateCtrlCanvas("Right", ref _rightCtrlText);
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

        FindController(true, ref _leftCtrl, ref _leftFollower);
        FindController(false, ref _rightCtrl, ref _rightFollower);
        PositionCtrlCanvas(_leftCtrlCanvas, _leftCtrl, true);
        PositionCtrlCanvas(_rightCtrlCanvas, _rightCtrl, false);

        UpdateTexts();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ── Update display ──────────────────────────────────────────────────

    private void UpdateTexts()
    {
        _leftText.text = $"FPS: {(1f / Time.unscaledDeltaTime):F0}  F:{Time.frameCount}";

        var devices = new List<InputDevice>();
        InputDevices.GetDevices(devices);
        string status = $"XR:{devices.Count}";
#if HAS_META_XR
        var ac = OVRInput.GetActiveController();
        string cc = ac != OVRInput.Controller.None ? "#0f0" : "#888";
        status += $" OVR:<color={cc}>{ac}</color>";
#endif
        _rightText.text = status;

        _leftCtrlText.text = GetControllerState("LEFT", true);
        _rightCtrlText.text = GetControllerState("RIGHT", false);
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

    private void FindController(bool isLeft, ref Transform ctrl, ref GameObject follower)
    {
#if HAS_META_XR
        if (ctrl == null)
        {
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig != null)
            {
                ctrl = isLeft ? rig.leftControllerAnchor : rig.rightControllerAnchor;
                return;
            }
        }
#endif
        var side = isLeft ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right;
        var devs = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | side, devs);
        if (devs.Count == 0) { ctrl = null; return; }

        Vector3 pos; Quaternion rot;
        if (!devs[0].TryGetFeatureValue(CommonUsages.devicePosition, out pos) ||
            !devs[0].TryGetFeatureValue(CommonUsages.deviceRotation, out rot))
        { ctrl = null; return; }

        if (follower == null)
        {
            follower = new GameObject($"XRFollow_{(isLeft ? "L" : "R")}");
            follower.transform.SetParent(transform);
        }
        follower.transform.position = pos;
        follower.transform.rotation = rot;
        ctrl = follower.transform;
    }

    private void PositionCtrlCanvas(Canvas canvas, Transform ctrl, bool isLeft)
    {
        if (ctrl == null) { canvas.gameObject.SetActive(false); return; }

        canvas.gameObject.SetActive(true);
        float sideways = isLeft ? -0.05f : 0.12f;
        canvas.transform.position = ctrl.position
            + Vector3.up * 0.15f + Vector3.right * sideways;

        if (_centerEye != null)
            canvas.transform.rotation = Quaternion.LookRotation(
                canvas.transform.position - _centerEye.position, Vector3.up);
        else
            canvas.transform.rotation = ctrl.rotation;
    }

    private void CreateDebugCanvas()
    {
        var canvasGO = new GameObject("VRInputDebug_Canvas");
        canvasGO.transform.SetParent(transform);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;

        var rect = _canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1000, 300);
        rect.localScale = Vector3.one * displayScale;

        _reticleText = CreateText(canvasGO.transform, "Reticle",
            new Vector2(0, 20), new Vector2(100, 60), TextAnchor.MiddleCenter, 32);
        _reticleText.text = "<color=#0f0>+</color>";

        _labelText = CreateText(canvasGO.transform, "Label",
            new Vector2(0, -30), new Vector2(300, 40), TextAnchor.UpperCenter, 18);
        _labelText.text = "<color=#0f0>DEBUG ON</color>";

        _leftText = CreateText(canvasGO.transform, "HudLeft",
            new Vector2(-300, 15), new Vector2(400, 40), TextAnchor.MiddleRight, 18);

        _rightText = CreateText(canvasGO.transform, "HudRight",
            new Vector2(300, 15), new Vector2(400, 40), TextAnchor.MiddleLeft, 18);

        _headerText = CreateText(canvasGO.transform, "Header",
            Vector2.zero, Vector2.zero, TextAnchor.UpperCenter, 1);
        _headerText.gameObject.SetActive(false);
    }

    private Canvas CreateCtrlCanvas(string name, ref Text text)
    {
        var go = new GameObject($"VRDebug_{name}Ctrl");
        go.transform.SetParent(transform);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(450, 750);
        rect.localScale = Vector3.one * 0.00028f;

        text = CreateText(go.transform, $"{name}CtrlTxt",
            Vector2.zero, new Vector2(430, 730), TextAnchor.UpperLeft, 19);

        go.SetActive(false);
        return canvas;
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

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.8f);
        outline.effectDistance = new Vector2(1, -1);

        return text;
    }
}
