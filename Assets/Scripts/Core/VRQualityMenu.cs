using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;
using System;
using System.Collections.Generic;

/// <summary>
/// VR Quality Menu -- thumbstick navigation. No clicking needed.
/// LEFT STICK up/down = select setting, left/right = adjust value
/// B/Y = toggle menu visibility
/// </summary>
public class VRQualityMenu : MonoBehaviour
{
    /// <summary>True when menu is open -- locomotion scripts should check this and skip input.</summary>
    public static bool MenuOpen { get; private set; } = true;

    private GameObject _canvas;
    private Text _titleText;
    private Text[] _rowTexts;
    private Text _fpsText;
    private bool _visible = true;
    private int _selectedRow = 0;
    private float _inputCooldown = 0;

    private UniversalRenderPipelineAsset _urpAsset;
    private Volume _postProcessVolume;
    private OVRPlayerController _ovrPlayer;
    private Material _skyboxMat;
    private ColorAdjustments _colorAdj;
    private Tonemapping _tonemapping;
    private Vignette _vignette;
    private LiftGammaGain _lgg;
    private WhiteBalance _whiteBalance;

    private float _fps;
    private int _frameCount;
    private float _fpsTimer;

    private List<Setting> _settings = new List<Setting>();

    class Setting
    {
        public string name;
        public Func<float> get;
        public Action<float> set;
        public float min, max, step;
        public string format;
        public Setting(string n, Func<float> g, Action<float> s, float mn, float mx, float st, string fmt = "F1")
        { name = n; get = g; set = s; min = mn; max = mx; step = st; format = fmt; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("_VRQualityMenu");
        go.AddComponent<VRQualityMenu>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        _urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        _postProcessVolume = FindAnyObjectByType<Volume>();
        _ovrPlayer = FindAnyObjectByType<OVRPlayerController>();
        if (_ovrPlayer != null) _ovrPlayer.enabled = false; // blocked until menu closed
        _skyboxMat = RenderSettings.skybox;

        // Get post-process components
        if (_postProcessVolume != null && _postProcessVolume.profile != null)
        {
            _postProcessVolume.profile.TryGet(out _colorAdj);
            _postProcessVolume.profile.TryGet(out _tonemapping);
            _postProcessVolume.profile.TryGet(out _vignette);
            _postProcessVolume.profile.TryGet(out _lgg);
            _postProcessVolume.profile.TryGet(out _whiteBalance);
        }

        BuildSettings();
        CreateWorldCanvas();
        Debug.Log($"[PLAGA44] VRQualityMenu: {_settings.Count} settings loaded");
    }

    void BuildSettings()
    {
        // --- RESOLUTION ---
        _settings.Add(new Setting("--- RESOLUTION ---", () => 0, v => {}, 0, 0, 0));

        _settings.Add(new Setting("Render Scale",
            () => _urpAsset?.renderScale ?? 1f,
            v => { if (_urpAsset) _urpAsset.renderScale = v; },
            0.3f, 2.0f, 0.1f));

        _settings.Add(new Setting("Eye Texture Scale",
            () => XRSettings.eyeTextureResolutionScale,
            v => XRSettings.eyeTextureResolutionScale = v,
            0.3f, 2.0f, 0.1f));

        _settings.Add(new Setting("MSAA",
            () => _urpAsset?.msaaSampleCount ?? 1,
            v => { if (_urpAsset) _urpAsset.msaaSampleCount = (int)v; },
            1, 8, 1, "F0"));

        // --- SHADOWS ---
        _settings.Add(new Setting("--- SHADOWS ---", () => 0, v => {}, 0, 0, 0));

        _settings.Add(new Setting("Shadow Distance",
            () => _urpAsset?.shadowDistance ?? 0,
            v => { if (_urpAsset) _urpAsset.shadowDistance = v; },
            0, 150, 5, "F0"));

        _settings.Add(new Setting("Shadow Depth Bias",
            () => _urpAsset != null ? _urpAsset.shadowDepthBias : 1,
            v => { if (_urpAsset) _urpAsset.shadowDepthBias = v; },
            0, 10, 0.5f));

        _settings.Add(new Setting("Shadow Normal Bias",
            () => _urpAsset != null ? _urpAsset.shadowNormalBias : 1,
            v => { if (_urpAsset) _urpAsset.shadowNormalBias = v; },
            0, 10, 0.5f));

        _settings.Add(new Setting("Shadow Resolution",
            () => _urpAsset != null ? _urpAsset.mainLightShadowmapResolution : 2048,
            v => { if (_urpAsset) _urpAsset.mainLightShadowmapResolution = (int)v; },
            256, 4096, 256, "F0"));

        // --- LIGHTING ---
        _settings.Add(new Setting("--- LIGHTING ---", () => 0, v => {}, 0, 0, 0));

        _settings.Add(new Setting("Directional Light Intensity",
            () => { var l = FindMainLight(); return l != null ? l.intensity : 1; },
            v => { var l = FindMainLight(); if (l) l.intensity = v; },
            0, 5, 0.1f));

        _settings.Add(new Setting("Directional Light R",
            () => { var l = FindMainLight(); return l != null ? l.color.r : 1; },
            v => { var l = FindMainLight(); if (l) { var c = l.color; c.r = v; l.color = c; } },
            0, 1, 0.02f, "F2"));

        _settings.Add(new Setting("Directional Light G",
            () => { var l = FindMainLight(); return l != null ? l.color.g : 1; },
            v => { var l = FindMainLight(); if (l) { var c = l.color; c.g = v; l.color = c; } },
            0, 1, 0.02f, "F2"));

        _settings.Add(new Setting("Directional Light B",
            () => { var l = FindMainLight(); return l != null ? l.color.b : 1; },
            v => { var l = FindMainLight(); if (l) { var c = l.color; c.b = v; l.color = c; } },
            0, 1, 0.02f, "F2"));

        _settings.Add(new Setting("Light Shadow Strength",
            () => { var l = FindMainLight(); return l != null ? l.shadowStrength : 1; },
            v => { var l = FindMainLight(); if (l) l.shadowStrength = v; },
            0, 1, 0.05f, "F2"));

        _settings.Add(new Setting("Fog Enabled",
            () => RenderSettings.fog ? 1 : 0,
            v => RenderSettings.fog = v > 0.5f,
            0, 1, 1, "F0"));

        _settings.Add(new Setting("Fog Density",
            () => RenderSettings.fogDensity,
            v => RenderSettings.fogDensity = v,
            0, 0.1f, 0.002f, "F3"));

        _settings.Add(new Setting("Fog Start",
            () => RenderSettings.fogStartDistance,
            v => RenderSettings.fogStartDistance = v,
            0, 200, 5, "F0"));

        _settings.Add(new Setting("Fog End",
            () => RenderSettings.fogEndDistance,
            v => RenderSettings.fogEndDistance = v,
            10, 500, 10, "F0"));

        _settings.Add(new Setting("Fog R",
            () => RenderSettings.fogColor.r,
            v => { var c = RenderSettings.fogColor; c.r = v; RenderSettings.fogColor = c; },
            0, 1, 0.02f, "F2"));

        _settings.Add(new Setting("Fog G",
            () => RenderSettings.fogColor.g,
            v => { var c = RenderSettings.fogColor; c.g = v; RenderSettings.fogColor = c; },
            0, 1, 0.02f, "F2"));

        _settings.Add(new Setting("Fog B",
            () => RenderSettings.fogColor.b,
            v => { var c = RenderSettings.fogColor; c.b = v; RenderSettings.fogColor = c; },
            0, 1, 0.02f, "F2"));

        // --- TEXTURES ---
        _settings.Add(new Setting("--- TEXTURES ---", () => 0, v => {}, 0, 0, 0));

        _settings.Add(new Setting("Texture Quality (mip)",
            () => QualitySettings.globalTextureMipmapLimit,
            v => QualitySettings.globalTextureMipmapLimit = (int)v,
            0, 3, 1, "F0"));

        _settings.Add(new Setting("LOD Bias",
            () => QualitySettings.lodBias,
            v => QualitySettings.lodBias = v,
            0.3f, 2.0f, 0.1f));

        // --- COLOR ---
        _settings.Add(new Setting("--- COLOR GRADING ---", () => 0, v => {}, 0, 0, 0));

        if (_colorAdj != null)
        {
            _settings.Add(new Setting("Exposure",
                () => _colorAdj.postExposure.value,
                v => { _colorAdj.postExposure.Override(v); },
                -3f, 3f, 0.1f));

            _settings.Add(new Setting("Contrast",
                () => _colorAdj.contrast.value,
                v => { _colorAdj.contrast.Override(v); },
                -100, 100, 5, "F0"));

            _settings.Add(new Setting("Saturation",
                () => _colorAdj.saturation.value,
                v => { _colorAdj.saturation.Override(v); },
                -100, 100, 5, "F0"));

            _settings.Add(new Setting("Hue Shift",
                () => _colorAdj.hueShift.value,
                v => { _colorAdj.hueShift.Override(v); },
                -180, 180, 5, "F0"));

            _settings.Add(new Setting("Color R",
                () => _colorAdj.colorFilter.value.r,
                v => { var c = _colorAdj.colorFilter.value; c.r = v; _colorAdj.colorFilter.Override(c); },
                0, 1, 0.02f, "F2"));

            _settings.Add(new Setting("Color G",
                () => _colorAdj.colorFilter.value.g,
                v => { var c = _colorAdj.colorFilter.value; c.g = v; _colorAdj.colorFilter.Override(c); },
                0, 1, 0.02f, "F2"));

            _settings.Add(new Setting("Color B",
                () => _colorAdj.colorFilter.value.b,
                v => { var c = _colorAdj.colorFilter.value; c.b = v; _colorAdj.colorFilter.Override(c); },
                0, 1, 0.02f, "F2"));
        }

        // --- WHITE BALANCE ---
        if (_whiteBalance != null)
        {
            _settings.Add(new Setting("Temperature",
                () => _whiteBalance.temperature.value,
                v => { _whiteBalance.temperature.Override(v); },
                -100, 100, 5, "F0"));

            _settings.Add(new Setting("Tint",
                () => _whiteBalance.tint.value,
                v => { _whiteBalance.tint.Override(v); },
                -100, 100, 5, "F0"));
        }

        // --- VIGNETTE ---
        _settings.Add(new Setting("--- VIGNETTE ---", () => 0, v => {}, 0, 0, 0));

        if (_vignette != null)
        {
            _settings.Add(new Setting("Vignette Intensity",
                () => _vignette.intensity.value,
                v => { _vignette.intensity.Override(v); },
                0, 1, 0.05f, "F2"));

            _settings.Add(new Setting("Vignette Smoothness",
                () => _vignette.smoothness.value,
                v => { _vignette.smoothness.Override(v); },
                0, 1, 0.05f, "F2"));
        }

        // --- LIFT GAMMA GAIN ---
        if (_lgg != null)
        {
            _settings.Add(new Setting("--- LIFT (shadows) ---", () => 0, v => {}, 0, 0, 0));
            _settings.Add(new Setting("Lift R", () => _lgg.lift.value.x, v => { var x = _lgg.lift.value; x.x = v; _lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
            _settings.Add(new Setting("Lift G", () => _lgg.lift.value.y, v => { var x = _lgg.lift.value; x.y = v; _lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
            _settings.Add(new Setting("Lift B", () => _lgg.lift.value.z, v => { var x = _lgg.lift.value; x.z = v; _lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));
            _settings.Add(new Setting("Lift W", () => _lgg.lift.value.w, v => { var x = _lgg.lift.value; x.w = v; _lgg.lift.Override(x); }, -1, 1, 0.02f, "F2"));

            _settings.Add(new Setting("--- GAMMA (mids) ---", () => 0, v => {}, 0, 0, 0));
            _settings.Add(new Setting("Gamma R", () => _lgg.gamma.value.x, v => { var x = _lgg.gamma.value; x.x = v; _lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
            _settings.Add(new Setting("Gamma G", () => _lgg.gamma.value.y, v => { var x = _lgg.gamma.value; x.y = v; _lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
            _settings.Add(new Setting("Gamma B", () => _lgg.gamma.value.z, v => { var x = _lgg.gamma.value; x.z = v; _lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));
            _settings.Add(new Setting("Gamma W", () => _lgg.gamma.value.w, v => { var x = _lgg.gamma.value; x.w = v; _lgg.gamma.Override(x); }, -1, 1, 0.02f, "F2"));

            _settings.Add(new Setting("--- GAIN (highlights) ---", () => 0, v => {}, 0, 0, 0));
            _settings.Add(new Setting("Gain R", () => _lgg.gain.value.x, v => { var x = _lgg.gain.value; x.x = v; _lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
            _settings.Add(new Setting("Gain G", () => _lgg.gain.value.y, v => { var x = _lgg.gain.value; x.y = v; _lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
            _settings.Add(new Setting("Gain B", () => _lgg.gain.value.z, v => { var x = _lgg.gain.value; x.z = v; _lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
            _settings.Add(new Setting("Gain W", () => _lgg.gain.value.w, v => { var x = _lgg.gain.value; x.w = v; _lgg.gain.Override(x); }, -1, 1, 0.02f, "F2"));
        }

        // --- SKYBOX ---
        _settings.Add(new Setting("--- SKYBOX ---", () => 0, v => {}, 0, 0, 0));

        if (_skyboxMat != null)
        {
            _settings.Add(new Setting("Sky Tint R",
                () => _skyboxMat.HasColor("_Tint") ? _skyboxMat.GetColor("_Tint").r
                    : _skyboxMat.HasColor("_Color") ? _skyboxMat.GetColor("_Color").r : 1f,
                v => { SetSkyColor(0, v); },
                0, 2, 0.05f, "F2"));

            _settings.Add(new Setting("Sky Tint G",
                () => _skyboxMat.HasColor("_Tint") ? _skyboxMat.GetColor("_Tint").g
                    : _skyboxMat.HasColor("_Color") ? _skyboxMat.GetColor("_Color").g : 1f,
                v => { SetSkyColor(1, v); },
                0, 2, 0.05f, "F2"));

            _settings.Add(new Setting("Sky Tint B",
                () => _skyboxMat.HasColor("_Tint") ? _skyboxMat.GetColor("_Tint").b
                    : _skyboxMat.HasColor("_Color") ? _skyboxMat.GetColor("_Color").b : 1f,
                v => { SetSkyColor(2, v); },
                0, 2, 0.05f, "F2"));

            _settings.Add(new Setting("Sky Exposure",
                () => _skyboxMat.HasFloat("_Exposure") ? _skyboxMat.GetFloat("_Exposure") : 1f,
                v => { if (_skyboxMat.HasFloat("_Exposure")) _skyboxMat.SetFloat("_Exposure", v); },
                0, 5, 0.1f));

            _settings.Add(new Setting("Sky Rotation",
                () => _skyboxMat.HasFloat("_Rotation") ? _skyboxMat.GetFloat("_Rotation") : 0f,
                v => { if (_skyboxMat.HasFloat("_Rotation")) _skyboxMat.SetFloat("_Rotation", v); },
                0, 360, 10, "F0"));
        }

        // --- AMBIENT ---
        _settings.Add(new Setting("--- AMBIENT ---", () => 0, v => {}, 0, 0, 0));

        _settings.Add(new Setting("Ambient Intensity",
            () => RenderSettings.ambientIntensity,
            v => RenderSettings.ambientIntensity = v,
            0, 3, 0.1f));

        _settings.Add(new Setting("Ambient R",
            () => RenderSettings.ambientLight.r,
            v => { var c = RenderSettings.ambientLight; c.r = v; RenderSettings.ambientLight = c; },
            0, 1, 0.05f, "F2"));

        _settings.Add(new Setting("Ambient G",
            () => RenderSettings.ambientLight.g,
            v => { var c = RenderSettings.ambientLight; c.g = v; RenderSettings.ambientLight = c; },
            0, 1, 0.05f, "F2"));

        _settings.Add(new Setting("Ambient B",
            () => RenderSettings.ambientLight.b,
            v => { var c = RenderSettings.ambientLight; c.b = v; RenderSettings.ambientLight = c; },
            0, 1, 0.05f, "F2"));

        // --- POST PROCESS ---
        _settings.Add(new Setting("--- ON/OFF ---", () => 0, v => {}, 0, 0, 0));

        _settings.Add(new Setting("Post Processing",
            () => (_postProcessVolume != null && _postProcessVolume.enabled) ? 1 : 0,
            v => { if (_postProcessVolume) _postProcessVolume.enabled = v > 0.5f; },
            0, 1, 1, "F0"));

        // --- RENDERING MODES ---
        _settings.Add(new Setting("--- RENDER MODES ---", () => 0, v => {}, 0, 0, 0));

        _settings.Add(new Setting("Foveated Render Level",
            () => (float)OVRManager.foveatedRenderingLevel,
            v => OVRManager.foveatedRenderingLevel = (OVRManager.FoveatedRenderingLevel)(int)v,
            0, 4, 1, "F0"));

        _settings.Add(new Setting("Display Refresh Rate",
            () => OVRManager.display != null ? OVRManager.display.displayFrequency : 72,
            v => { if (OVRManager.display != null) OVRManager.display.displayFrequency = v; },
            60, 120, 6, "F0"));

        // --- CAMERA ---
        _settings.Add(new Setting("--- CAMERA ---", () => 0, v => {}, 0, 0, 0));

        _settings.Add(new Setting("Near Clip",
            () => Camera.main != null ? Camera.main.nearClipPlane : 0.01f,
            v => { if (Camera.main) Camera.main.nearClipPlane = v; },
            0.01f, 1f, 0.01f, "F2"));

        _settings.Add(new Setting("Far Clip",
            () => Camera.main != null ? Camera.main.farClipPlane : 1000,
            v => { if (Camera.main) Camera.main.farClipPlane = v; },
            50, 2000, 50, "F0"));

        // --- SAVE ---
        _settings.Add(new Setting("--- ACTIONS ---", () => 0, v => {}, 0, 0, 0));
        _settings.Add(new Setting("[SAVE TO LOG]",
            () => 0,
            v => SaveToLog(),
            0, 1, 1, "F0"));
    }

    static Light FindMainLight()
    {
        var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights)
            if (l.type == LightType.Directional) return l;
        return null;
    }

    void CreateWorldCanvas()
    {
        _canvas = new GameObject("QualityMenuCanvas");
        _canvas.transform.SetParent(transform);
        var canvas = _canvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;
        _canvas.AddComponent<CanvasScaler>();

        var rt = _canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(700, 40 + _settings.Count * 28);
        rt.localScale = Vector3.one * 0.0008f;

        // Background
        var bg = new GameObject("BG");
        bg.transform.SetParent(_canvas.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.02f, 0.02f, 0.06f, 0.93f);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        // FPS line
        var fpsGo = MakeText(bg.transform, "", 20, Color.green, new Vector2(10, -5), new Vector2(680, 28));
        _fpsText = fpsGo.GetComponent<Text>();

        // Rows
        _rowTexts = new Text[_settings.Count];
        for (int i = 0; i < _settings.Count; i++)
        {
            float y = -33 - i * 28;
            var go = MakeText(bg.transform, "", 18, Color.white, new Vector2(10, y), new Vector2(680, 26));
            _rowTexts[i] = go.GetComponent<Text>();
        }

        // Footer
        MakeText(bg.transform, "R.STICK ^v select  |  L.TRIGGER -  R.TRIGGER +  |  [B]/[Y] hide",
            14, new Color(0.5f, 0.5f, 0.5f), new Vector2(10, -33 - _settings.Count * 28), new Vector2(680, 22));
    }

    GameObject MakeText(Transform parent, string txt, int size, Color col, Vector2 pos, Vector2 sz)
    {
        var go = new GameObject("T");
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = pos;
        r.sizeDelta = sz;
        var t = go.AddComponent<Text>();
        t.text = txt;
        t.fontSize = size;
        t.color = col;
        t.font = Font.CreateDynamicFontFromOSFont("Arial", size);
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return go;
    }

    void Update()
    {
        _frameCount++;
        _fpsTimer += Time.unscaledDeltaTime;
        if (_fpsTimer >= 0.5f)
        {
            _fps = _frameCount / _fpsTimer;
            _frameCount = 0;
            _fpsTimer = 0;
        }

        // Toggle
        if (OVRInput.GetDown(OVRInput.Button.Two) || OVRInput.GetDown(OVRInput.Button.Start))
        {
            _visible = !_visible;
            _canvas.SetActive(_visible);
            MenuOpen = _visible;
            // Disable/enable OVRPlayerController to block its built-in turn
            if (_ovrPlayer != null) _ovrPlayer.enabled = !_visible;
        }

        if (!_visible) return;

        // Position
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 target = cam.transform.position + cam.transform.forward * 1.2f;
            _canvas.transform.position = Vector3.Lerp(_canvas.transform.position, target, Time.deltaTime * 2f);
            _canvas.transform.rotation = Quaternion.Slerp(_canvas.transform.rotation,
                Quaternion.LookRotation(_canvas.transform.position - cam.transform.position), Time.deltaTime * 2f);
        }

        // Input cooldown
        _inputCooldown -= Time.unscaledDeltaTime;
        if (_inputCooldown > 0) return;

        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
        float leftTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger);   // left hand
        float rightTrigger = OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger); // right hand

        // Navigate up/down with right stick
        if (stick.y > 0.5f)
        {
            do { _selectedRow = (_selectedRow - 1 + _settings.Count) % _settings.Count; }
            while (_settings[_selectedRow].step == 0); // skip headers
            _inputCooldown = 0.2f;
        }
        else if (stick.y < -0.5f)
        {
            do { _selectedRow = (_selectedRow + 1) % _settings.Count; }
            while (_settings[_selectedRow].step == 0);
            _inputCooldown = 0.2f;
        }

        // Adjust with triggers: RIGHT = increase, LEFT = decrease
        if (rightTrigger > 0.5f)
        {
            var s = _settings[_selectedRow];
            s.set(Mathf.Clamp(s.get() + s.step, s.min, s.max));
            _inputCooldown = 0.10f;
        }
        else if (leftTrigger > 0.5f)
        {
            var s = _settings[_selectedRow];
            s.set(Mathf.Clamp(s.get() - s.step, s.min, s.max));
            _inputCooldown = 0.10f;
        }

        // Update display
        string col = _fps >= 60 ? "#00ff00" : _fps >= 36 ? "#ffff00" : "#ff3333";
        long mem = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024;
        _fpsText.text = $"<color={col}>FPS: {_fps:F0}</color>  Mem: {mem}MB  Frame: {Time.frameCount}";

        for (int i = 0; i < _settings.Count; i++)
        {
            var s = _settings[i];
            bool selected = (i == _selectedRow);
            bool isHeader = (s.step == 0);

            if (isHeader)
            {
                _rowTexts[i].text = $"<color=#888888>{s.name}</color>";
            }
            else
            {
                string val = s.get().ToString(s.format);
                string arrow = selected ? ">>  " : "    ";
                string c = selected ? "#00ffff" : "#cccccc";
                string bar = "";
                if (s.max > s.min)
                {
                    float pct = Mathf.Clamp01((s.get() - s.min) / (s.max - s.min));
                    int filled = Mathf.Clamp((int)(pct * 12), 0, 12);
                    bar = " [" + new string('|', filled) + new string('.', 12 - filled) + "]";
                }
                _rowTexts[i].text = $"<color={c}>{arrow}{s.name}: {val}{bar}</color>";
            }
        }
    }

    void SetSkyColor(int channel, float value)
    {
        if (_skyboxMat == null) return;
        string prop = _skyboxMat.HasColor("_Tint") ? "_Tint" : "_Color";
        if (!_skyboxMat.HasColor(prop)) return;
        var c = _skyboxMat.GetColor(prop);
        if (channel == 0) c.r = value;
        else if (channel == 1) c.g = value;
        else c.b = value;
        _skyboxMat.SetColor(prop, c);
    }

    void SaveToLog()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("### PLAGA44_SETTINGS_BEGIN ###");
        foreach (var s in _settings)
        {
            if (s.step == 0) continue; // skip headers
            if (s.name == "[SAVE TO LOG]" || s.name == "[PRESET]") continue;
            sb.AppendLine($"{s.name} = {s.get().ToString(s.format)}");
        }
        sb.AppendLine("### PLAGA44_SETTINGS_END ###");
        Debug.Log(sb.ToString());
    }
}
