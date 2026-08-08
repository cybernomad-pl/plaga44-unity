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

    private float headDistance = 0.5f;
    private float headScale = 0.0004f;
    private float ctrlScale = 0.00028f;

    private Canvas _headCanvas;
    private Canvas _leftCanvas;
    private Canvas _rightCanvas;

    private Text _topLeftText;
    private Text _topCenterText;
    private Text _topRightText;
    private Text _bottomText;
    private Text _leftText;
    private Text _rightText;

    private Transform _centerEye;
    private Transform _leftCtrl;
    private Transform _rightCtrl;
    private GameObject _leftFollower;
    private GameObject _rightFollower;

    void Start()
    {
        BuildHeadCanvas();
        _leftCanvas = BuildCtrlCanvas("Left", ref _leftText);
        _rightCanvas = BuildCtrlCanvas("Right", ref _rightText);
    }

    void Update()
    {
        FindCenterEye();
        FindControllers();

        if (_centerEye != null)
        {
            _headCanvas.transform.position = _centerEye.position + _centerEye.forward * headDistance;
            _headCanvas.transform.rotation = _centerEye.rotation;
        }

        PositionCtrlCanvas(_leftCanvas, _leftCtrl, true);
        PositionCtrlCanvas(_rightCanvas, _rightCtrl, false);

        UpdateHeadTexts();
        _leftText.text = GetControllerState("LEFT", true);
        _rightText.text = GetControllerState("RIGHT", false);
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void PositionCtrlCanvas(Canvas canvas, Transform ctrl, bool isLeft)
    {
        if (ctrl == null) { canvas.gameObject.SetActive(false); return; }

        canvas.gameObject.SetActive(true);
        float side = isLeft ? -1f : 1f;
        canvas.transform.position = ctrl.position
            + Vector3.up * 0.12f
            + Vector3.right * (side * 0.06f);

        if (_centerEye != null)
            canvas.transform.rotation = Quaternion.LookRotation(
                canvas.transform.position - _centerEye.position, Vector3.up);
        else
            canvas.transform.rotation = ctrl.rotation;
    }

    private void FindCenterEye()
    {
        if (_centerEye != null) return;
#if HAS_META_XR
        var rig = FindFirstObjectByType<OVRCameraRig>();
        if (rig != null) { _centerEye = rig.centerEyeAnchor; return; }
#endif
        var cam = Camera.main;
        if (cam != null) _centerEye = cam.transform;
    }

    private void FindControllers()
    {
#if HAS_META_XR
        if (_leftCtrl == null || _rightCtrl == null)
        {
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig != null)
            {
                _leftCtrl = rig.leftControllerAnchor;
                _rightCtrl = rig.rightControllerAnchor;
                return;
            }
        }
#endif
        UpdateXRFollower(InputDeviceCharacteristics.Left, ref _leftFollower, ref _leftCtrl);
        UpdateXRFollower(InputDeviceCharacteristics.Right, ref _rightFollower, ref _rightCtrl);
    }

    private void UpdateXRFollower(InputDeviceCharacteristics side,
        ref GameObject follower, ref Transform result)
    {
        var devs = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | side, devs);
        if (devs.Count == 0) { result = null; return; }

        Vector3 pos; Quaternion rot;
        if (!devs[0].TryGetFeatureValue(CommonUsages.devicePosition, out pos) ||
            !devs[0].TryGetFeatureValue(CommonUsages.deviceRotation, out rot))
        { result = null; return; }

        if (follower == null)
        {
            follower = new GameObject($"XRFollow_{side}");
            follower.transform.SetParent(transform);
        }
        follower.transform.position = pos;
        follower.transform.rotation = rot;
        result = follower.transform;
    }

    private void UpdateHeadTexts()
    {
        _topLeftText.text = $"FPS: {(1f / Time.unscaledDeltaTime):F0}  Frame: {Time.frameCount}";
        _topCenterText.text = "<b>VR INPUT DEBUG</b>";

        string status = "";
#if HAS_META_XR
        var ac = OVRInput.GetActiveController();
        string cc = ac != OVRInput.Controller.None ? "#0f0" : "#f00";
        status += $"OVR: <color={cc}>{ac}</color>\n";
        var mgr = FindFirstObjectByType<OVRManager>();
        status += $"Mgr: {(mgr != null ? "<color=#0f0>OK</color>" : "<color=#f00>NO</color>")}";
#else
        status += "<color=#f00>NO META XR</color>";
#endif
        _topRightText.text = status;

        var devices = new List<InputDevice>();
        InputDevices.GetDevices(devices);
        string dl = $"XR Devices: {devices.Count}";
        foreach (var dev in devices)
        {
            string r = "";
            if ((dev.characteristics & InputDeviceCharacteristics.Left) != 0) r = "L";
            else if ((dev.characteristics & InputDeviceCharacteristics.Right) != 0) r = "R";
            else if ((dev.characteristics & InputDeviceCharacteristics.HeadMounted) != 0) r = "HMD";
            dl += $"\n  <color=#0f0>[{r}]</color> {dev.name}";
        }
        _bottomText.text = dl;
    }

    private string GetControllerState(string hand, bool isLeft)
    {
        string s = $"<b>=== {hand} ===</b>\n";

        InputDevice xrDevice = default;
        var characteristics = InputDeviceCharacteristics.Controller |
            (isLeft ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right);
        var controllers = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(characteristics, controllers);

        bool xrConnected = controllers.Count > 0;
        if (xrConnected) xrDevice = controllers[0];

        s += $"XR: {Colored(xrConnected)}";
#if HAS_META_XR
        var ctrl = isLeft ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        bool ovrConn = OVRInput.IsControllerConnected(ctrl);
        s += $"  OVR: {Colored(ovrConn)}";
#endif
        s += "\n";

        if (!xrConnected)
        {
            s += "<color=#888>No controller</color>\n";
            return s;
        }

        s += "\n<b>Buttons</b>\n";
        bool primaryBtn, secondaryBtn, menuBtn, stickBtn;
        xrDevice.TryGetFeatureValue(CommonUsages.primaryButton, out primaryBtn);
        xrDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryBtn);
        xrDevice.TryGetFeatureValue(CommonUsages.menuButton, out menuBtn);
        xrDevice.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out stickBtn);

        s += $"{(isLeft ? "X" : "A")}:     {BtnStr(primaryBtn)}\n";
        s += $"{(isLeft ? "Y" : "B")}:     {BtnStr(secondaryBtn)}\n";
        if (isLeft) s += $"Menu:  {BtnStr(menuBtn)}\n";
        s += $"Stick: {BtnStr(stickBtn)}\n";

        s += "\n<b>Triggers</b>\n";
        float trigger, grip;
        xrDevice.TryGetFeatureValue(CommonUsages.trigger, out trigger);
        xrDevice.TryGetFeatureValue(CommonUsages.grip, out grip);
        s += $"Index: {Bar(trigger)} {trigger:F2}\n";
        s += $"Grip:  {Bar(grip)} {grip:F2}\n";

        s += "\n<b>Stick</b>\n";
        Vector2 stick;
        xrDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out stick);
        s += $"X:{stick.x:+0.00;-0.00} Y:{stick.y:+0.00;-0.00}\n";
        s += $"Dir: {StickVisual(stick)}\n";

#if HAS_META_XR
        s += "\n<b>Touch</b>\n";
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

        s += "\n<b>Tracking</b>\n";
        Vector3 pos; Quaternion rot;
        xrDevice.TryGetFeatureValue(CommonUsages.devicePosition, out pos);
        xrDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out rot);
        s += $"P: {pos.x:F2} {pos.y:F2} {pos.z:F2}\n";
        s += $"R: {rot.eulerAngles.x:F0} {rot.eulerAngles.y:F0} {rot.eulerAngles.z:F0}\n";

        bool tracked;
        xrDevice.TryGetFeatureValue(CommonUsages.isTracked, out tracked);
        s += $"OK: {Colored(tracked)}\n";

        return s;
    }

    private string BtnStr(bool p) =>
        p ? "<color=#0f0><b>[X]</b></color>" : "<color=#888>[ ]</color>";

#if HAS_META_XR
    private string Touch(OVRInput.Touch t, OVRInput.Controller c) =>
        OVRInput.Get(t, c) ? "<color=#ff0>[T]</color>" : "<color=#888>[ ]</color>";
#endif

    private string Bar(float v)
    {
        int f = Mathf.RoundToInt(v * 10);
        string b = "";
        for (int i = 0; i < 10; i++) b += i < f ? "#" : ".";
        string c = v > 0.8f ? "#0f0" : v > 0.1f ? "#ff0" : "#888";
        return $"<color={c}>[{b}]</color>";
    }

    private string Colored(bool v) =>
        v ? "<color=#0f0>YES</color>" : "<color=#f00>NO</color>";

    private string StickVisual(Vector2 v)
    {
        if (v.magnitude < 0.1f) return "O";
        float a = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        if (a < 0) a += 360;
        if (a >= 337.5f || a < 22.5f) return "-->";
        if (a < 67.5f) return "/^";
        if (a < 112.5f) return "^";
        if (a < 157.5f) return "^\\";
        if (a < 202.5f) return "<--";
        if (a < 247.5f) return "\\v";
        if (a < 292.5f) return "v";
        return "v/";
    }

    private void BuildHeadCanvas()
    {
        var go = new GameObject("VRDebug_Head");
        go.transform.SetParent(transform);
        _headCanvas = go.AddComponent<Canvas>();
        _headCanvas.renderMode = RenderMode.WorldSpace;

        var rect = _headCanvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1000, 500);
        rect.localScale = Vector3.one * headScale;

        _topLeftText = MakeText(go.transform, "TL",
            new Vector2(-350, 200), new Vector2(350, 60), TextAnchor.UpperLeft, 22);

        _topCenterText = MakeText(go.transform, "TC",
            new Vector2(0, 220), new Vector2(400, 50), TextAnchor.UpperCenter, 22);

        _topRightText = MakeText(go.transform, "TR",
            new Vector2(350, 200), new Vector2(350, 80), TextAnchor.UpperRight, 22);

        _bottomText = MakeText(go.transform, "BT",
            new Vector2(0, -50), new Vector2(800, 350), TextAnchor.UpperCenter, 19);
    }

    private Canvas BuildCtrlCanvas(string name, ref Text text)
    {
        var go = new GameObject($"VRDebug_{name}");
        go.transform.SetParent(transform);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(450, 750);
        rect.localScale = Vector3.one * ctrlScale;

        text = MakeText(go.transform, $"{name}Txt",
            Vector2.zero, new Vector2(430, 730), TextAnchor.UpperLeft, 19);

        go.SetActive(false);
        return canvas;
    }

    private Text MakeText(Transform parent, string name,
        Vector2 pos, Vector2 size, TextAnchor anchor, int fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var r = go.AddComponent<RectTransform>();
        r.anchoredPosition = pos;
        r.sizeDelta = size;

        var t = go.AddComponent<Text>();
        t.font = Font.CreateDynamicFontFromOSFont("Consolas", fontSize);
        t.fontSize = fontSize;
        t.color = Color.white;
        t.alignment = anchor;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.8f);
        outline.effectDistance = new Vector2(1, -1);

        return t;
    }
}
