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

    void Start()
    {
        CreateDebugCanvas();
        CreateLeftCtrlCanvas();
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

        FindLeftController();
        PositionLeftCtrlCanvas();

        UpdateTexts();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ── Update display ──────────────────────────────────────────────────

    private void UpdateTexts()
    {
        _leftCtrlText.text = GetControllerState("LEFT", true);
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

    private void FindLeftController()
    {
#if HAS_META_XR
        if (_leftCtrl == null)
        {
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig != null) { _leftCtrl = rig.leftControllerAnchor; return; }
        }
#endif
        var devs = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, devs);
        if (devs.Count == 0) { _leftCtrl = null; return; }

        Vector3 pos; Quaternion rot;
        if (!devs[0].TryGetFeatureValue(CommonUsages.devicePosition, out pos) ||
            !devs[0].TryGetFeatureValue(CommonUsages.deviceRotation, out rot))
        { _leftCtrl = null; return; }

        if (_leftFollower == null)
        {
            _leftFollower = new GameObject("XRFollow_Left");
            _leftFollower.transform.SetParent(transform);
        }
        _leftFollower.transform.position = pos;
        _leftFollower.transform.rotation = rot;
        _leftCtrl = _leftFollower.transform;
    }

    private void PositionLeftCtrlCanvas()
    {
        if (_leftCtrl == null) { _leftCtrlCanvas.gameObject.SetActive(false); return; }

        _leftCtrlCanvas.gameObject.SetActive(true);
        _leftCtrlCanvas.transform.position = _leftCtrl.position
            + Vector3.up * 0.12f + Vector3.right * -0.06f;

        if (_centerEye != null)
            _leftCtrlCanvas.transform.rotation = Quaternion.LookRotation(
                _leftCtrlCanvas.transform.position - _centerEye.position, Vector3.up);
        else
            _leftCtrlCanvas.transform.rotation = _leftCtrl.rotation;
    }

    private void CreateDebugCanvas()
    {
        var canvasGO = new GameObject("VRInputDebug_Canvas");
        canvasGO.transform.SetParent(transform);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;

        var rect = _canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300, 200);
        rect.localScale = Vector3.one * displayScale;

        _reticleText = CreateText(canvasGO.transform, "Reticle",
            new Vector2(0, 20), new Vector2(100, 60), TextAnchor.MiddleCenter, 32);
        _reticleText.text = "<color=#0f0>+</color>";

        _labelText = CreateText(canvasGO.transform, "Label",
            new Vector2(0, -30), new Vector2(300, 40), TextAnchor.UpperCenter, 18);
        _labelText.text = "<color=#0f0>DEBUG ON</color>";

        _headerText = CreateText(canvasGO.transform, "Header",
            Vector2.zero, Vector2.zero, TextAnchor.UpperCenter, 1);
        _headerText.gameObject.SetActive(false);

        _leftText = CreateText(canvasGO.transform, "Left",
            Vector2.zero, Vector2.zero, TextAnchor.UpperLeft, 1);
        _leftText.gameObject.SetActive(false);

        _rightText = CreateText(canvasGO.transform, "Right",
            Vector2.zero, Vector2.zero, TextAnchor.UpperLeft, 1);
        _rightText.gameObject.SetActive(false);
    }

    private void CreateLeftCtrlCanvas()
    {
        var go = new GameObject("VRDebug_LeftCtrl");
        go.transform.SetParent(transform);
        _leftCtrlCanvas = go.AddComponent<Canvas>();
        _leftCtrlCanvas.renderMode = RenderMode.WorldSpace;

        var rect = _leftCtrlCanvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(450, 750);
        rect.localScale = Vector3.one * 0.00028f;

        _leftCtrlText = CreateText(go.transform, "LeftCtrlTxt",
            Vector2.zero, new Vector2(430, 730), TextAnchor.UpperLeft, 19);

        go.SetActive(false);
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
