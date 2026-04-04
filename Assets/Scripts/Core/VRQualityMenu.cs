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
/// START (Menu/3-lines button, left controller) = toggle menu visibility
/// </summary>
public class VRQualityMenu : MonoBehaviour
{
    /// <summary>True when menu is open -- locomotion scripts should check this and skip input.</summary>
    public static bool MenuOpen { get; set; } = false;

    private GameObject _canvas;
    private Text _titleText;
    private Text[] _rowTexts;
    private Text _fpsText;
    private bool _visible = false;
    private int _selectedRow = 0;
    private float _inputCooldown = 0;
    private int _scrollOffset = 0;
    private const int VISIBLE_ROWS = 20;
    private List<int> _sectionStarts = new List<int>();
    private int _currentSection = 0;

    private UniversalRenderPipelineAsset _urpAsset;
    private Volume _postProcessVolume;
    private OVRPlayerController _ovrPlayer;
    private Material _skyboxMat;
    private Material _waterMat;
    private Material _treeBarkMat;
    private Material _terrainMat;
    private List<Material> _npcMats = new List<Material>();
    private List<Material> _weaponMats = new List<Material>();
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
#if LOCOMOTION_ONLY
        return;
#endif
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
        // NOTE: locomotion blocking is now handled by VRMenuManager.MenuOpen
        _skyboxMat = RenderSettings.skybox;

        // Find water material by name or shader name
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m != null && (m.name.Contains("Water") || m.name.Contains("water") ||
                    (m.shader != null && m.shader.name.Contains("Water"))))
                {
                    _waterMat = m;
                    break;
                }
            }
            if (_waterMat != null) break;
        }
        if (_waterMat != null) Debug.Log($"[PLAGA44] VRQualityMenu: water material: {_waterMat.name}");

        // Find tree bark material (first instance material containing "trunk")
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            foreach (var m in r.materials) // .materials gives instances we can edit
            {
                if (m != null && (m.name.ToLower().Contains("trunk") || m.name.ToLower().Contains("bark")))
                {
                    _treeBarkMat = m;
                    break;
                }
            }
            if (_treeBarkMat != null) break;
        }
        if (_treeBarkMat != null) Debug.Log($"[PLAGA44] VRQualityMenu: tree bark material: {_treeBarkMat.name}");

        // Find terrain material
        var terrain = FindAnyObjectByType<Terrain>();
        if (terrain != null && terrain.materialTemplate != null)
            _terrainMat = terrain.materialTemplate;

        // NPC/Weapon material scanning disabled -- UI sections are disabled

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
        BuildSectionIndex();
        CreateWorldCanvas();
        _canvas.SetActive(false);
        Debug.Log($"[PLAGA44] VRQualityMenu: {_settings.Count} settings, {_sectionStarts.Count} sections");

        // Auto-load preset from SceneDefaults (SLOT 3 on Quest, SLOT 1 in editor)
        if (SceneDefaults._pendingPresetSlot > 0)
        {
            LoadPreset(SceneDefaults._pendingPresetSlot);
            SceneDefaults._pendingPresetSlot = 0;
        }
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
            0, 1, 0.001f, "F3"));

        _settings.Add(new Setting("Light Indirect Multiplier",
            () => { var l = FindMainLight(); return l != null ? l.bounceIntensity : 1; },
            v => { var l = FindMainLight(); if (l) l.bounceIntensity = v; },
            0, 5, 0.01f, "F2"));

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

            _settings.Add(new Setting("Cloud Brightness",
                () => _skyboxMat.HasFloat("_CloudBoost") ? _skyboxMat.GetFloat("_CloudBoost") : 1f,
                v => { if (_skyboxMat.HasFloat("_CloudBoost")) _skyboxMat.SetFloat("_CloudBoost", v); },
                0, 5, 0.01f, "F2"));

            _settings.Add(new Setting("Cloud Threshold",
                () => _skyboxMat.HasFloat("_CloudThreshold") ? _skyboxMat.GetFloat("_CloudThreshold") : 0.3f,
                v => { if (_skyboxMat.HasFloat("_CloudThreshold")) _skyboxMat.SetFloat("_CloudThreshold", v); },
                0, 1, 0.001f, "F3"));
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

        // --- WATER ---
        _settings.Add(new Setting("--- WATER ---", () => 0, v => {}, 0, 0, 0));

        if (_waterMat != null)
        {
            _settings.Add(new Setting("Water R",
                () => _waterMat.GetColor("_Color").r,
                v => { var c = _waterMat.GetColor("_Color"); c.r = v; _waterMat.SetColor("_Color", c); },
                0, 1, 0.001f, "F3"));

            _settings.Add(new Setting("Water G",
                () => _waterMat.GetColor("_Color").g,
                v => { var c = _waterMat.GetColor("_Color"); c.g = v; _waterMat.SetColor("_Color", c); },
                0, 1, 0.001f, "F3"));

            _settings.Add(new Setting("Water B",
                () => _waterMat.GetColor("_Color").b,
                v => { var c = _waterMat.GetColor("_Color"); c.b = v; _waterMat.SetColor("_Color", c); },
                0, 1, 0.001f, "F3"));

            _settings.Add(new Setting("Water Metallic",
                () => _waterMat.GetFloat("_Metallic"),
                v => _waterMat.SetFloat("_Metallic", v),
                0, 1, 0.001f, "F3"));

            _settings.Add(new Setting("Water Smoothness",
                () => _waterMat.GetFloat("_Smth"),
                v => _waterMat.SetFloat("_Smth", v),
                0, 1, 0.001f, "F3"));

            _settings.Add(new Setting("Water Scroll Speed",
                () => _waterMat.GetFloat("_ScrollSpeed"),
                v => _waterMat.SetFloat("_ScrollSpeed", v),
                0, 2, 0.001f, "F3"));

            _settings.Add(new Setting("Water Wave Height",
                () => _waterMat.GetFloat("_WaveHeight"),
                v => _waterMat.SetFloat("_WaveHeight", v),
                0, 3, 0.001f, "F3"));

            _settings.Add(new Setting("Water Wave Freq",
                () => _waterMat.GetFloat("_WaveFreq"),
                v => _waterMat.SetFloat("_WaveFreq", v),
                0, 100, 0.1f, "F1"));

            _settings.Add(new Setting("Water Wave Complexity",
                () => _waterMat.GetFloat("_WaveComplexity"),
                v => _waterMat.SetFloat("_WaveComplexity", v),
                0, 1, 0.001f, "F3"));

            _settings.Add(new Setting("Water Wave Steepness",
                () => _waterMat.GetFloat("_WaveSteepness"),
                v => _waterMat.SetFloat("_WaveSteepness", v),
                0, 1, 0.001f, "F3"));

            _settings.Add(new Setting("Water Normal Strength",
                () => _waterMat.GetFloat("_BumpScale"),
                v => _waterMat.SetFloat("_BumpScale", v),
                0, 3, 0.001f, "F3"));

            _settings.Add(new Setting("Water Emission",
                () => _waterMat.GetFloat("_Emis"),
                v => _waterMat.SetFloat("_Emis", v),
                0, 0.5f, 0.001f, "F3"));

            _settings.Add(new Setting("Water Reflection Str",
                () => _waterMat.GetFloat("_ReflStr"),
                v => _waterMat.SetFloat("_ReflStr", v),
                0, 3, 0.001f, "F3"));

            _settings.Add(new Setting("Water Fresnel Power",
                () => _waterMat.GetFloat("_FresnelPow"),
                v => _waterMat.SetFloat("_FresnelPow", v),
                0.1f, 10, 0.01f, "F2"));

            _settings.Add(new Setting("Water UV Density",
                () => _waterMat.GetFloat("_UVScale"),
                v => _waterMat.SetFloat("_UVScale", v),
                0.1f, 200, 0.1f, "F1"));

            _settings.Add(new Setting("Water Transparency",
                () => _waterMat.GetFloat("_Alpha"),
                v => _waterMat.SetFloat("_Alpha", v),
                0, 1, 0.001f, "F3"));

            _settings.Add(new Setting("Water Foam Depth",
                () => _waterMat.GetFloat("_FoamDepth"),
                v => _waterMat.SetFloat("_FoamDepth", v),
                0.01f, 5, 0.01f, "F2"));

            _settings.Add(new Setting("Water Foam Strength",
                () => _waterMat.GetFloat("_FoamStr"),
                v => _waterMat.SetFloat("_FoamStr", v),
                0, 3, 0.01f, "F2"));

            _settings.Add(new Setting("Water Foam R",
                () => _waterMat.GetColor("_FoamColor").r,
                v => { var c = _waterMat.GetColor("_FoamColor"); c.r = v; _waterMat.SetColor("_FoamColor", c); },
                0, 1, 0.001f, "F3"));

            _settings.Add(new Setting("Water Foam G",
                () => _waterMat.GetColor("_FoamColor").g,
                v => { var c = _waterMat.GetColor("_FoamColor"); c.g = v; _waterMat.SetColor("_FoamColor", c); },
                0, 1, 0.001f, "F3"));

            _settings.Add(new Setting("Water Foam B",
                () => _waterMat.GetColor("_FoamColor").b,
                v => { var c = _waterMat.GetColor("_FoamColor"); c.b = v; _waterMat.SetColor("_FoamColor", c); },
                0, 1, 0.001f, "F3"));
        }

        // --- TREES ---
        _settings.Add(new Setting("--- TREES ---", () => 0, v => {}, 0, 0, 0));

        {
            // Collect ALL bark + leaf instance materials
            var allBarkMats = new System.Collections.Generic.List<Material>();
            var allLeafMats = new System.Collections.Generic.List<Material>();
            var seenBark = new System.Collections.Generic.HashSet<int>();
            var seenLeaf = new System.Collections.Generic.HashSet<int>();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                // Use sharedMaterials to affect all instances sharing the same material
                var mats = r.sharedMaterials;
                foreach (var m in mats)
                {
                    if (m == null) continue;
                    string mn = m.name.ToLower();
                    string sn = m.shader != null ? m.shader.name.ToLower() : "";
                    int id = m.GetInstanceID();
                    if ((mn.Contains("bark") || mn.Contains("trunk") || sn.Contains("bark")) && seenBark.Add(id))
                        allBarkMats.Add(m);
                    if ((mn.Contains("leaf") || mn.Contains("leaves") || sn.Contains("leaf")) && seenLeaf.Add(id))
                        allLeafMats.Add(m);
                }
            }

            if (allBarkMats.Count > 0)
            {
                var refMat = allBarkMats[0];
                // URP Lit uses _BaseColor; TreeCreator uses _Color
                string colorProp = refMat.HasColor("_BaseColor") ? "_BaseColor" : "_Color";

                _settings.Add(new Setting("Bark R",
                    () => refMat.GetColor(colorProp).r,
                    v => { foreach (var m in allBarkMats) {
                        string p = m.HasColor("_BaseColor") ? "_BaseColor" : "_Color";
                        var c = m.GetColor(p); c.r = v; m.SetColor(p, c);
                        if (m.HasColor("_Color") && p != "_Color") { var c2 = m.GetColor("_Color"); c2.r = v; m.SetColor("_Color", c2); }
                    }}, 0, 2, 0.001f, "F3"));

                _settings.Add(new Setting("Bark G",
                    () => refMat.GetColor(colorProp).g,
                    v => { foreach (var m in allBarkMats) {
                        string p = m.HasColor("_BaseColor") ? "_BaseColor" : "_Color";
                        var c = m.GetColor(p); c.g = v; m.SetColor(p, c);
                        if (m.HasColor("_Color") && p != "_Color") { var c2 = m.GetColor("_Color"); c2.g = v; m.SetColor("_Color", c2); }
                    }}, 0, 2, 0.001f, "F3"));

                _settings.Add(new Setting("Bark B",
                    () => refMat.GetColor(colorProp).b,
                    v => { foreach (var m in allBarkMats) {
                        string p = m.HasColor("_BaseColor") ? "_BaseColor" : "_Color";
                        var c = m.GetColor(p); c.b = v; m.SetColor(p, c);
                        if (m.HasColor("_Color") && p != "_Color") { var c2 = m.GetColor("_Color"); c2.b = v; m.SetColor("_Color", c2); }
                    }}, 0, 2, 0.001f, "F3"));

                _settings.Add(new Setting("Bark Smoothness",
                    () => refMat.HasFloat("_Smoothness") ? refMat.GetFloat("_Smoothness") : (refMat.HasFloat("_Glossiness") ? refMat.GetFloat("_Glossiness") : 0),
                    v => { foreach (var m in allBarkMats) { if (m.HasFloat("_Smoothness")) m.SetFloat("_Smoothness", v); if (m.HasFloat("_Glossiness")) m.SetFloat("_Glossiness", v); } },
                    0, 1, 0.001f, "F3"));

                _settings.Add(new Setting("Bark Specular R",
                    () => refMat.HasColor("_SpecColor") ? refMat.GetColor("_SpecColor").r : 0.5f,
                    v => { foreach (var m in allBarkMats) { if (m.HasColor("_SpecColor")) { var c = m.GetColor("_SpecColor"); c.r = v; m.SetColor("_SpecColor", c); } } },
                    0, 1, 0.001f, "F3"));

                _settings.Add(new Setting("Bark Specular G",
                    () => refMat.HasColor("_SpecColor") ? refMat.GetColor("_SpecColor").g : 0.5f,
                    v => { foreach (var m in allBarkMats) { if (m.HasColor("_SpecColor")) { var c = m.GetColor("_SpecColor"); c.g = v; m.SetColor("_SpecColor", c); } } },
                    0, 1, 0.001f, "F3"));

                _settings.Add(new Setting("Bark Specular B",
                    () => refMat.HasColor("_SpecColor") ? refMat.GetColor("_SpecColor").b : 0.5f,
                    v => { foreach (var m in allBarkMats) { if (m.HasColor("_SpecColor")) { var c = m.GetColor("_SpecColor"); c.b = v; m.SetColor("_SpecColor", c); } } },
                    0, 1, 0.001f, "F3"));

                Debug.Log($"[PLAGA44] VRQualityMenu: {allBarkMats.Count} bark materials (prop: {colorProp})");
            }

            if (allLeafMats.Count > 0)
            {
                var refLeaf = allLeafMats[0];

                _settings.Add(new Setting("Leaf R",
                    () => refLeaf.GetColor("_Color").r,
                    v => { foreach (var m in allLeafMats) { var c = m.GetColor("_Color"); c.r = v; m.SetColor("_Color", c); } },
                    0, 2, 0.001f, "F3"));

                _settings.Add(new Setting("Leaf G",
                    () => refLeaf.GetColor("_Color").g,
                    v => { foreach (var m in allLeafMats) { var c = m.GetColor("_Color"); c.g = v; m.SetColor("_Color", c); } },
                    0, 2, 0.001f, "F3"));

                _settings.Add(new Setting("Leaf B",
                    () => refLeaf.GetColor("_Color").b,
                    v => { foreach (var m in allLeafMats) { var c = m.GetColor("_Color"); c.b = v; m.SetColor("_Color", c); } },
                    0, 2, 0.001f, "F3"));

                Debug.Log($"[PLAGA44] VRQualityMenu: {allLeafMats.Count} leaf materials");
            }
        }

        // --- TERRAIN ---
        _settings.Add(new Setting("--- TERRAIN ---", () => 0, v => {}, 0, 0, 0));

        if (_terrainMat != null)
        {
            _settings.Add(new Setting("Terrain Normal",
                () => _terrainMat.HasFloat("_BumpScale") ? _terrainMat.GetFloat("_BumpScale") : 1,
                v => { if (_terrainMat.HasFloat("_BumpScale")) _terrainMat.SetFloat("_BumpScale", v); },
                0, 3, 0.001f, "F3"));

            _settings.Add(new Setting("Terrain Smoothness",
                () => _terrainMat.HasFloat("_Smoothness") ? _terrainMat.GetFloat("_Smoothness") : 0,
                v => { if (_terrainMat.HasFloat("_Smoothness")) _terrainMat.SetFloat("_Smoothness", v); },
                0, 1, 0.001f, "F3"));

            _settings.Add(new Setting("Terrain Metallic",
                () => _terrainMat.HasFloat("_Metallic") ? _terrainMat.GetFloat("_Metallic") : 0,
                v => { if (_terrainMat.HasFloat("_Metallic")) _terrainMat.SetFloat("_Metallic", v); },
                0, 1, 0.001f, "F3"));
        }

        // Terrain layers
        var terrain = FindAnyObjectByType<Terrain>();
        if (terrain != null && terrain.terrainData != null && terrain.terrainData.terrainLayers != null)
        {
            var layers = terrain.terrainData.terrainLayers;
            for (int li = 0; li < Mathf.Min(layers.Length, 4); li++)
            {
                var layer = layers[li];
                if (layer == null) continue;
                int idx = li;
                _settings.Add(new Setting($"Layer{li} NormalScale",
                    () => layers[idx].normalScale,
                    v => layers[idx].normalScale = v,
                    0, 3, 0.001f, "F3"));
                _settings.Add(new Setting($"Layer{li} TileSize",
                    () => layers[idx].tileSize.x,
                    v => layers[idx].tileSize = new Vector2(v, v),
                    1, 100, 0.1f, "F1"));
                _settings.Add(new Setting($"Layer{li} Metallic",
                    () => layers[idx].metallic,
                    v => layers[idx].metallic = v,
                    0, 1, 0.001f, "F3"));
                _settings.Add(new Setting($"Layer{li} Smoothness",
                    () => layers[idx].smoothness,
                    v => layers[idx].smoothness = v,
                    0, 1, 0.001f, "F3"));
            }
        }

        // --- M249 MATERIAL ---
        _settings.Add(new Setting("--- M249 ---", () => 0, v => {}, 0, 0, 0));
        _settings.Add(new Setting("Gun Color R",
            () => M249MaterialSetup.gunColor.r,
            v => { M249MaterialSetup.gunColor.r = v; M249MaterialSetup.GetGunMaterial().SetColor("_BaseColor", M249MaterialSetup.gunColor); },
            0, 1, 0.001f, "F3"));
        _settings.Add(new Setting("Gun Color G",
            () => M249MaterialSetup.gunColor.g,
            v => { M249MaterialSetup.gunColor.g = v; M249MaterialSetup.GetGunMaterial().SetColor("_BaseColor", M249MaterialSetup.gunColor); },
            0, 1, 0.001f, "F3"));
        _settings.Add(new Setting("Gun Color B",
            () => M249MaterialSetup.gunColor.b,
            v => { M249MaterialSetup.gunColor.b = v; M249MaterialSetup.GetGunMaterial().SetColor("_BaseColor", M249MaterialSetup.gunColor); },
            0, 1, 0.001f, "F3"));
        _settings.Add(new Setting("Gun Metallic",
            () => M249MaterialSetup.gunMetallic,
            v => { M249MaterialSetup.gunMetallic = v; M249MaterialSetup.GetGunMaterial().SetFloat("_Metallic", v); },
            0, 1, 0.001f, "F3"));
        _settings.Add(new Setting("Gun Smoothness",
            () => M249MaterialSetup.gunSmoothness,
            v => { M249MaterialSetup.gunSmoothness = v; M249MaterialSetup.GetGunMaterial().SetFloat("_Smoothness", v); },
            0, 1, 0.001f, "F3"));

        // --- TERRAIN DEFORMATION ---
        _settings.Add(new Setting("--- DEFORMATION ---", () => 0, v => {}, 0, 0, 0));

        _settings.Add(new Setting("Terrain Noise Strength",
            () => TerrainDeformer.NoiseStrength,
            v => { TerrainDeformer.NoiseStrength = v; TerrainDeformer.ApplyDeformation(); },
            0, 20, 0.1f, "F1"));

        _settings.Add(new Setting("Terrain Noise Scale",
            () => TerrainDeformer.NoiseScale,
            v => { TerrainDeformer.NoiseScale = v; TerrainDeformer.ApplyDeformation(); },
            0.001f, 0.2f, 0.001f, "F3"));

        _settings.Add(new Setting("Terrain Noise Seed",
            () => TerrainDeformer.NoiseSeed,
            v => { TerrainDeformer.NoiseSeed = v; TerrainDeformer.ApplyDeformation(); },
            0, 1000, 1, "F0"));

        // NPC/Weapon material sliders removed -- replaced by laser pointer debug system
        /* DISABLED -- separate feature
        _settings.Add(new Setting("--- NPC MATERIALS ---", () => 0, v => {}, 0, 0, 0));

        if (_npcMats.Count > 0)
        {
            var npcRef = _npcMats[0];
            string npcColorProp = npcRef.HasColor("_BaseColor") ? "_BaseColor" : "_Color";

            _settings.Add(new Setting("NPC Color R",
                () => npcRef.GetColor(npcColorProp).r,
                v => { foreach (var m in _npcMats) {
                    string p = m.HasColor("_BaseColor") ? "_BaseColor" : "_Color";
                    var c = m.GetColor(p); c.r = v; m.SetColor(p, c);
                }}, 0, 2, 0.01f, "F2"));

            _settings.Add(new Setting("NPC Color G",
                () => npcRef.GetColor(npcColorProp).g,
                v => { foreach (var m in _npcMats) {
                    string p = m.HasColor("_BaseColor") ? "_BaseColor" : "_Color";
                    var c = m.GetColor(p); c.g = v; m.SetColor(p, c);
                }}, 0, 2, 0.01f, "F2"));

            _settings.Add(new Setting("NPC Color B",
                () => npcRef.GetColor(npcColorProp).b,
                v => { foreach (var m in _npcMats) {
                    string p = m.HasColor("_BaseColor") ? "_BaseColor" : "_Color";
                    var c = m.GetColor(p); c.b = v; m.SetColor(p, c);
                }}, 0, 2, 0.01f, "F2"));

            _settings.Add(new Setting("NPC Metallic",
                () => npcRef.HasFloat("_Metallic") ? npcRef.GetFloat("_Metallic") : 0,
                v => { foreach (var m in _npcMats) { if (m.HasFloat("_Metallic")) m.SetFloat("_Metallic", v); } },
                0, 1, 0.01f, "F2"));

            _settings.Add(new Setting("NPC Smoothness",
                () => npcRef.HasFloat("_Smoothness") ? npcRef.GetFloat("_Smoothness") : (npcRef.HasFloat("_Glossiness") ? npcRef.GetFloat("_Glossiness") : 0),
                v => { foreach (var m in _npcMats) { if (m.HasFloat("_Smoothness")) m.SetFloat("_Smoothness", v); if (m.HasFloat("_Glossiness")) m.SetFloat("_Glossiness", v); } },
                0, 1, 0.01f, "F2"));

            _settings.Add(new Setting("NPC Emission",
                () => npcRef.HasFloat("_EmissionStrength") ? npcRef.GetFloat("_EmissionStrength") : 0,
                v => { foreach (var m in _npcMats) {
                    if (m.HasFloat("_EmissionStrength")) m.SetFloat("_EmissionStrength", v);
                    // Enable emission keyword if setting > 0
                    if (v > 0) m.EnableKeyword("_EMISSION"); else m.DisableKeyword("_EMISSION");
                    if (m.HasColor("_EmissionColor"))
                    {
                        var ec = m.GetColor("_EmissionColor");
                        float maxC = Mathf.Max(ec.r, Mathf.Max(ec.g, ec.b));
                        if (maxC < 0.01f) m.SetColor("_EmissionColor", Color.white * v);
                    }
                }}, 0, 5, 0.1f));

            Debug.Log($"[PLAGA44] VRQualityMenu: NPC material settings added (colorProp: {npcColorProp})");
        }

        // --- WEAPON MATERIALS ---
        _settings.Add(new Setting("--- WEAPON MATERIALS ---", () => 0, v => {}, 0, 0, 0));

        if (_weaponMats.Count > 0)
        {
            var wpnRef = _weaponMats[0];
            string wpnColorProp = wpnRef.HasColor("_BaseColor") ? "_BaseColor" : "_Color";

            _settings.Add(new Setting("Weapon Color R",
                () => wpnRef.GetColor(wpnColorProp).r,
                v => { foreach (var m in _weaponMats) {
                    string p = m.HasColor("_BaseColor") ? "_BaseColor" : "_Color";
                    var c = m.GetColor(p); c.r = v; m.SetColor(p, c);
                }}, 0, 2, 0.01f, "F2"));

            _settings.Add(new Setting("Weapon Color G",
                () => wpnRef.GetColor(wpnColorProp).g,
                v => { foreach (var m in _weaponMats) {
                    string p = m.HasColor("_BaseColor") ? "_BaseColor" : "_Color";
                    var c = m.GetColor(p); c.g = v; m.SetColor(p, c);
                }}, 0, 2, 0.01f, "F2"));

            _settings.Add(new Setting("Weapon Color B",
                () => wpnRef.GetColor(wpnColorProp).b,
                v => { foreach (var m in _weaponMats) {
                    string p = m.HasColor("_BaseColor") ? "_BaseColor" : "_Color";
                    var c = m.GetColor(p); c.b = v; m.SetColor(p, c);
                }}, 0, 2, 0.01f, "F2"));

            _settings.Add(new Setting("Weapon Metallic",
                () => wpnRef.HasFloat("_Metallic") ? wpnRef.GetFloat("_Metallic") : 0,
                v => { foreach (var m in _weaponMats) { if (m.HasFloat("_Metallic")) m.SetFloat("_Metallic", v); } },
                0, 1, 0.01f, "F2"));

            _settings.Add(new Setting("Weapon Smoothness",
                () => wpnRef.HasFloat("_Smoothness") ? wpnRef.GetFloat("_Smoothness") : (wpnRef.HasFloat("_Glossiness") ? wpnRef.GetFloat("_Glossiness") : 0),
                v => { foreach (var m in _weaponMats) { if (m.HasFloat("_Smoothness")) m.SetFloat("_Smoothness", v); if (m.HasFloat("_Glossiness")) m.SetFloat("_Glossiness", v); } },
                0, 1, 0.01f, "F2"));

            Debug.Log($"[PLAGA44] VRQualityMenu: Weapon material settings added (colorProp: {wpnColorProp})");
        }
        */ // END DISABLED NPC/Weapon

        // --- SAVE/LOAD ---
        _settings.Add(new Setting("--- PRESETS ---", () => 0, v => {}, 0, 0, 0));
        _settings.Add(new Setting("[SAVE 1:HI-END]", () => 0, v => SavePreset(1), 0, 1, 1, "F0"));
        _settings.Add(new Setting("[SAVE 2:CUSTOM]", () => 0, v => SavePreset(2), 0, 1, 1, "F0"));
        _settings.Add(new Setting("[SAVE 3:SAFE]", () => 0, v => SavePreset(3), 0, 1, 1, "F0"));
        _settings.Add(new Setting("[LOAD 1:HI-END]", () => 0, v => LoadPreset(1), 0, 1, 1, "F0"));
        _settings.Add(new Setting("[LOAD 2:CUSTOM]", () => 0, v => LoadPreset(2), 0, 1, 1, "F0"));
        _settings.Add(new Setting("[LOAD 3:SAFE]", () => 0, v => LoadPreset(3), 0, 1, 1, "F0"));
        _settings.Add(new Setting("[SAVE TO LOG]", () => 0, v => SaveToLog(), 0, 1, 1, "F0"));

        // --- EXTRA ---
        _settings.Add(new Setting("--- EXTRA ---", () => 0, v => {}, 0, 0, 0));
        _settings.Add(new Setting("Sky Rotation Speed",
            () => SkyRotator.RotationSpeed,
            v => SkyRotator.RotationSpeed = v,
            -5, 5, 0.01f, "F2"));
    }

    void BuildSectionIndex()
    {
        _sectionStarts.Clear();
        for (int i = 0; i < _settings.Count; i++)
        {
            if (_settings[i].step == 0 && _settings[i].name.StartsWith("---"))
                _sectionStarts.Add(i);
        }
    }

    int GetCurrentSectionIndex()
    {
        for (int i = _sectionStarts.Count - 1; i >= 0; i--)
        {
            if (_selectedRow >= _sectionStarts[i]) return i;
        }
        return 0;
    }

    void JumpToSection(int sectionIdx)
    {
        sectionIdx = Mathf.Clamp(sectionIdx, 0, _sectionStarts.Count - 1);
        _currentSection = sectionIdx;
        _selectedRow = _sectionStarts[sectionIdx];
        // Jump to first editable row in section
        while (_selectedRow < _settings.Count && _settings[_selectedRow].step == 0)
            _selectedRow++;
        if (_selectedRow >= _settings.Count)
            _selectedRow = _sectionStarts[sectionIdx];
    }

    static Light FindMainLight()
    {
        var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights)
            if (l.type == LightType.Directional) return l;
        return null;
    }

    // FindNPCAndWeaponMaterials removed -- NPC/Weapon UI sections are disabled

    void CreateWorldCanvas()
    {
        _canvas = new GameObject("QualityMenuCanvas");
        _canvas.transform.SetParent(transform);
        var canvas = _canvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;
        _canvas.AddComponent<CanvasScaler>();

        var rt = _canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(700, 40 + VISIBLE_ROWS * 28 + 30);
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

        // Visible rows (scrollable window)
        _rowTexts = new Text[VISIBLE_ROWS];
        for (int i = 0; i < VISIBLE_ROWS; i++)
        {
            float y = -33 - i * 28;
            var go = MakeText(bg.transform, "", 18, Color.white, new Vector2(10, y), new Vector2(680, 26));
            _rowTexts[i] = go.GetComponent<Text>();
        }

        // Footer
        MakeText(bg.transform, "R.STICK ^v select <> section | L.TRIG - R.TRIG + | [B]/[Y] hide",
            14, new Color(0.5f, 0.5f, 0.5f), new Vector2(10, -33 - VISIBLE_ROWS * 28), new Vector2(680, 22));
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

    // ---- Public API for VRMenuManager ----

    /// <summary>Show the quality settings panel. Called by VRMenuManager (Debug > Quality).</summary>
    public void ShowPanel()
    {
        _visible = true;
        _canvas.SetActive(true);
        MenuOpen = true;
    }

    /// <summary>Hide the quality settings panel. Called by VRMenuManager on back/close.</summary>
    public void HidePanel()
    {
        _visible = false;
        _canvas.SetActive(false);
        MenuOpen = false;
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

        // Input toggle REMOVED -- VRMenuManager owns Button.Start now.
        // VRQualityMenu is opened/closed via ShowPanel()/HidePanel() from VRMenuManager.

        if (!_visible) return;

        // Position
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 target = cam.transform.position + cam.transform.forward * 1.2f;
            _canvas.transform.position = Vector3.Lerp(_canvas.transform.position, target, Time.deltaTime * 2f);
            // Canvas text renders on +Z face. To face the player, canvas forward must
            // point TOWARD the player (= cam.position - canvas.position = negative of old direction).
            _canvas.transform.rotation = Quaternion.Slerp(_canvas.transform.rotation,
                Quaternion.LookRotation(cam.transform.position - _canvas.transform.position), Time.deltaTime * 2f);
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

        // Left/right = jump between sections
        if (stick.x > 0.5f)
        {
            int sec = GetCurrentSectionIndex();
            JumpToSection(sec + 1);
            _inputCooldown = 0.25f;
        }
        else if (stick.x < -0.5f)
        {
            int sec = GetCurrentSectionIndex();
            JumpToSection(sec - 1);
            _inputCooldown = 0.25f;
        }

        // Keep selected row in visible scroll window
        if (_selectedRow < _scrollOffset) _scrollOffset = _selectedRow;
        if (_selectedRow >= _scrollOffset + VISIBLE_ROWS) _scrollOffset = _selectedRow - VISIBLE_ROWS + 1;
        _scrollOffset = Mathf.Clamp(_scrollOffset, 0, Mathf.Max(0, _settings.Count - VISIBLE_ROWS));

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
        int secIdx = GetCurrentSectionIndex();
        string secName = secIdx < _sectionStarts.Count ? _settings[_sectionStarts[secIdx]].name.Replace("---", "").Trim() : "";
        _fpsText.text = $"<color={col}>FPS: {_fps:F0}</color>  Mem: {mem}MB  <color=#00ffff>{secName}</color> [{secIdx+1}/{_sectionStarts.Count}]";

        for (int vi = 0; vi < VISIBLE_ROWS; vi++)
        {
            int si = vi + _scrollOffset;
            if (si >= _settings.Count) { _rowTexts[vi].text = ""; continue; }

            var s = _settings[si];
            bool selected = (si == _selectedRow);
            bool isHeader = (s.step == 0);

            if (isHeader)
            {
                _rowTexts[vi].text = $"<color=#888888>{s.name}</color>";
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
                _rowTexts[vi].text = $"<color={c}>{arrow}{s.name}: {val}{bar}</color>";
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
            if (s.step == 0) continue;
            if (s.name.StartsWith("[")) continue;
            sb.AppendLine($"{s.name} = {s.get().ToString(s.format)}");
        }
        sb.AppendLine("### PLAGA44_SETTINGS_END ###");
        Debug.Log(sb.ToString());
    }

    void SavePreset(int slot)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var s in _settings)
        {
            if (s.step == 0) continue;
            if (s.name.StartsWith("[")) continue;
            sb.Append($"{s.name}={s.get().ToString("F4", System.Globalization.CultureInfo.InvariantCulture)};");
        }
        string key = $"PLAGA44_PRESET_{slot}";
        PlayerPrefs.SetString(key, sb.ToString());
        PlayerPrefs.Save();
        Debug.Log($"[PLAGA44] Preset {slot} SAVED ({_settings.Count} values)");
    }

    void LoadPreset(int slot)
    {
        string key = $"PLAGA44_PRESET_{slot}";
        string data = PlayerPrefs.GetString(key, "");

        // Hardcoded presets -- ALWAYS use these for slot 1 and 3
        if (slot == 1) data = PresetHiEnd.Data;
        else if (slot == 3) data = PresetSafe.Data;
        if (string.IsNullOrEmpty(data))
        {
            Debug.LogWarning($"[PLAGA44] Preset {slot} is EMPTY");
            return;
        }

        // Parse "Name=Value;Name=Value;..."
        var pairs = data.Split(';');
        var lookup = new System.Collections.Generic.Dictionary<string, float>();
        foreach (var pair in pairs)
        {
            if (string.IsNullOrEmpty(pair)) continue;
            var kv = pair.Split('=');
            if (kv.Length == 2)
            {
                // Try invariant (dot) first, then comma-locale fallback
                string valStr = kv[1].Trim();
                if (float.TryParse(valStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float val))
                {
                    lookup[kv[0]] = val;
                }
                else if (float.TryParse(valStr.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float val2))
                {
                    lookup[kv[0]] = val2;
                }
            }
        }

        int applied = 0;
        foreach (var s in _settings)
        {
            if (s.step == 0 || s.name.StartsWith("[")) continue;
            if (lookup.TryGetValue(s.name, out float val))
            {
                s.set(val);
                applied++;
            }
        }

        Debug.Log($"[PLAGA44] Preset {slot} LOADED ({applied} values applied)");
    }
}
