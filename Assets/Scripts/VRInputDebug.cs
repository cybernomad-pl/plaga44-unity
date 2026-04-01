// VRInputDebug.cs
// CYBERNOMAD -- In-headset HUD debug overlay for controller input.
// Follows head (centerEyeAnchor), controller panels follow controller anchors.
// Pure Meta XR SDK -- no generic XR fallbacks.
//
// Requires: com.meta.xr.sdk.core (HAS_META_XR define)

using UnityEngine;
using UnityEngine.UI;

public class VRInputDebug : MonoBehaviour
{
    private const string ENABLED_KEY = "CYBERNOMAD_VRInputDebug";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        // DISABLED -- enable manually via CYBERNOMAD/Debug/VR Input Debug menu
        return;
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

    // -- Instance --

    private float displayDistance = 0.4f;
    private float displayScale = 0.0004f;

    private Canvas _canvas;
    private Text _leftText;
    private Text _rightText;
    private Text _reticleText;
    private Text _labelText;

    private Canvas _leftCtrlCanvas;
    private Text _leftCtrlText;

    private Canvas _rightCtrlCanvas;
    private Text _rightCtrlText;

#if HAS_META_XR
    private OVRCameraRig _rig;
#endif

    // Gaze throw zone indicators
    private Image _outerZone, _middleZone, _innerZone;
    private Image _leftDot, _rightDot;
    private Text _zoneText;

    void Start()
    {
        CreateDebugCanvas();
        _leftCtrlCanvas = CreateCtrlCanvas("Left", ref _leftCtrlText);
        _rightCtrlCanvas = CreateCtrlCanvas("Right", ref _rightCtrlText);
        CreateGazeZones();
    }

    void Update()
    {
#if HAS_META_XR
        if (_rig == null)
        {
            _rig = FindFirstObjectByType<OVRCameraRig>();
            if (_rig == null) return;
        }

        // Main HUD follows head
        var centerEye = _rig.centerEyeAnchor;
        if (centerEye != null)
        {
            _canvas.transform.position = centerEye.position + centerEye.forward * displayDistance;
            _canvas.transform.rotation = centerEye.rotation;
        }

        // Controller panels follow controller anchors
        PositionCtrlCanvas(_leftCtrlCanvas, _rig.leftControllerAnchor, centerEye, true);
        PositionCtrlCanvas(_rightCtrlCanvas, _rig.rightControllerAnchor, centerEye, false);

        UpdateTexts();
        UpdateGazeZones(centerEye);
#endif
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // -- Display --

#if HAS_META_XR
    private void UpdateTexts()
    {
        _leftText.text = $"FPS: {(1f / Time.unscaledDeltaTime):F0}  F:{Time.frameCount}";

        var ac = OVRInput.GetActiveController();
        string cc = ac != OVRInput.Controller.None ? "#0f0" : "#888";
        _rightText.text = $"OVR:<color={cc}>{ac}</color>";

        _leftCtrlText.text = GetControllerState("LEFT", OVRInput.Controller.LTouch);
        _rightCtrlText.text = GetControllerState("RIGHT", OVRInput.Controller.RTouch);
    }

    private string GetControllerState(string hand, OVRInput.Controller ctrl)
    {
        string s = $"<b>=== {hand} ===</b>\n";

        bool connected = OVRInput.IsControllerConnected(ctrl);
        s += $"Connected: {Colored(connected)}\n\n";

        if (!connected)
        {
            s += "<color=#888>No controller</color>\n";
            return s;
        }

        bool isLeft = ctrl == OVRInput.Controller.LTouch;

        // Buttons
        s += "<b>-- Buttons --</b>\n";
        s += $"{(isLeft ? "X" : "A")}:       {BtnStr(OVRInput.Get(OVRInput.Button.One, ctrl))}\n";
        s += $"{(isLeft ? "Y" : "B")}:       {BtnStr(OVRInput.Get(OVRInput.Button.Two, ctrl))}\n";
        if (isLeft) s += $"Menu:    {BtnStr(OVRInput.Get(OVRInput.Button.Start, ctrl))}\n";
        s += $"Stick:   {BtnStr(OVRInput.Get(OVRInput.Button.PrimaryThumbstick, ctrl))}\n\n";

        // Triggers
        s += "<b>-- Triggers --</b>\n";
        float trigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, ctrl);
        float grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, ctrl);
        s += $"Index:   {Bar(trigger)} {trigger:F2}\n";
        s += $"Grip:    {Bar(grip)} {grip:F2}\n\n";

        // Thumbstick
        s += "<b>-- Thumbstick --</b>\n";
        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, ctrl);
        s += $"X: {stick.x:+0.00;-0.00}  Y: {stick.y:+0.00;-0.00}\n";
        s += $"Dir: {StickVisual(stick)}\n\n";

        // Touch
        s += "<b>-- Touch --</b>\n";
        s += $"{(isLeft ? "X" : "A")}:     {TouchStr(OVRInput.Get(isLeft ? OVRInput.Touch.Three : OVRInput.Touch.One, ctrl))}\n";
        s += $"{(isLeft ? "Y" : "B")}:     {TouchStr(OVRInput.Get(isLeft ? OVRInput.Touch.Four : OVRInput.Touch.Two, ctrl))}\n";
        s += $"Stick: {TouchStr(OVRInput.Get(OVRInput.Touch.PrimaryThumbstick, ctrl))}\n";
        s += $"Index: {TouchStr(OVRInput.Get(OVRInput.Touch.PrimaryIndexTrigger, ctrl))}\n\n";

        // Tracking
        s += "<b>-- Tracking --</b>\n";
        Vector3 pos = OVRInput.GetLocalControllerPosition(ctrl);
        Quaternion rot = OVRInput.GetLocalControllerRotation(ctrl);
        s += $"Pos: {pos.x:F2} {pos.y:F2} {pos.z:F2}\n";
        s += $"Rot: {rot.eulerAngles.x:F0} {rot.eulerAngles.y:F0} {rot.eulerAngles.z:F0}\n";

        return s;
    }
#endif

    // -- Gaze Zone HUD --

    private void CreateGazeZones()
    {
        var parent = _canvas.transform;

        // Three concentric zone circles (behind text -- added first)
        // Outer = faint, middle = slightly brighter, inner = brightest
        _outerZone = CreateCircle(parent, "OuterZone", 400f, new Color(0.3f, 0.9f, 0.3f, 0.06f));
        _middleZone = CreateCircle(parent, "MiddleZone", 260f, new Color(0.3f, 0.9f, 0.3f, 0.08f));
        _innerZone = CreateCircle(parent, "InnerZone", 130f, new Color(0.3f, 1f, 0.3f, 0.10f));

        // Controller position dots (small, bright)
        _leftDot = CreateCircle(parent, "LDot", 20f, new Color(1f, 0.5f, 0f, 0.85f));
        _rightDot = CreateCircle(parent, "RDot", 20f, new Color(0f, 0.6f, 1f, 0.85f));

        // Zone label -- small text below reticle
        _zoneText = CreateText(parent, "ZoneLabel",
            new Vector2(0, -80), new Vector2(300, 30), TextAnchor.UpperCenter, 16);
        _zoneText.text = "";
    }

    private static Sprite _circleSprite;

    private Image CreateCircle(Transform parent, string name, float diameter, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(0, 20); // centered on reticle Y offset
        rect.sizeDelta = new Vector2(diameter, diameter);

        var img = go.AddComponent<Image>();
        img.sprite = GetCircleSprite();
        img.color = color;
        img.raycastTarget = false;

        return img;
    }

    /// <summary>
    /// Generates a circle sprite at runtime. Unity 6 removed UI/Skin/Knob.psd builtin.
    /// </summary>
    private static Sprite GetCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;

        const int res = 128;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = res / 2f;
        float radius = center - 2f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                // Smooth anti-aliased edge: 2px falloff
                float alpha = Mathf.Clamp01((radius - dist) / 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
        return _circleSprite;
    }

#if HAS_META_XR
    private void UpdateGazeZones(Transform centerEye)
    {
        if (centerEye == null || _rig == null) return;

        // Update controller dots and determine active zones
        var leftInfo = GazeThrow.GetControllerZone(_rig, OVRInput.Controller.LTouch);
        var rightInfo = GazeThrow.GetControllerZone(_rig, OVRInput.Controller.RTouch);

        UpdateDot(_leftDot, leftInfo, OVRInput.Controller.LTouch);
        UpdateDot(_rightDot, rightInfo, OVRInput.Controller.RTouch);

        // Highlight active zone (use right hand as primary, fallback to left)
        bool rightConnected = OVRInput.IsControllerConnected(OVRInput.Controller.RTouch);
        var activeInfo = rightConnected ? rightInfo : leftInfo;
        HighlightZone(activeInfo.zone);

        // Zone label
        if (_zoneText != null)
        {
            string[] names = { "INNER", "MIDDLE", "OUTER" };
            string[] colors = { "#0f0", "#ff0", "#888" };
            int z = activeInfo.zone;
            _zoneText.text = $"<color={colors[z]}>{names[z]} {activeInfo.angle:F0}°</color>";
        }
    }

    private void UpdateDot(Image dot, GazeThrow.GazeZoneInfo info, OVRInput.Controller ctrl)
    {
        if (dot == null) return;

        bool connected = OVRInput.IsControllerConnected(ctrl);
        dot.enabled = connected;
        if (!connected) return;

        // Map angle to canvas: scale so inner zone (15°) = 40px radius
        float scale = 40f / 15f; // ~2.67 px per degree
        float canvasX = info.hudX * scale;
        float canvasY = info.hudY * scale;

        var rect = dot.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(canvasX, canvasY + 20f); // +20 = reticle Y offset
    }

    private void HighlightZone(int zone)
    {
        // Pulse the active zone brighter
        float t = Mathf.PingPong(Time.time * 2f, 1f) * 0.04f;

        if (_outerZone != null)
            _outerZone.color = zone == 2
                ? new Color(0.9f, 0.9f, 0.3f, 0.12f + t)
                : new Color(0.3f, 0.9f, 0.3f, 0.06f);

        if (_middleZone != null)
            _middleZone.color = zone == 1
                ? new Color(0.9f, 0.9f, 0.3f, 0.14f + t)
                : new Color(0.3f, 0.9f, 0.3f, 0.08f);

        if (_innerZone != null)
            _innerZone.color = zone == 0
                ? new Color(0.3f, 1f, 0.3f, 0.18f + t)
                : new Color(0.3f, 1f, 0.3f, 0.10f);
    }
#else
    private void UpdateGazeZones(Transform centerEye) { }
#endif

    // -- Formatting --

    private string BtnStr(bool pressed)
    {
        return pressed ? "<color=#0f0><b>[PRESSED]</b></color>" : "<color=#888>[      ]</color>";
    }

    private string TouchStr(bool touched)
    {
        return touched ? "<color=#ff0>[TOUCH]</color>" : "<color=#888>[     ]</color>";
    }

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

    // -- Canvas --

    private void PositionCtrlCanvas(Canvas canvas, Transform anchor, Transform centerEye, bool isLeft)
    {
        if (anchor == null) { canvas.gameObject.SetActive(false); return; }

        canvas.gameObject.SetActive(true);
        // Offset relative to controller, not world space
        float sideways = isLeft ? -0.05f : 0.12f;
        canvas.transform.position = anchor.position
            + anchor.up * 0.15f + anchor.right * sideways;

        if (centerEye != null)
            canvas.transform.rotation = Quaternion.LookRotation(
                canvas.transform.position - centerEye.position, Vector3.up);
        else
            canvas.transform.rotation = anchor.rotation;
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
