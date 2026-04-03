// SceneDefaults.cs
// CYBERNOMAD -- Applies rendering defaults on scene load.
// SAFE mode (Quest standalone) vs HI-END mode (Editor/PCVR).
// Values from VR debug menu SaveToLog sessions.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

[DefaultExecutionOrder(-100)]
public class SceneDefaults : MonoBehaviour
{
    public static bool SafeMode
    {
        get
        {
#if UNITY_EDITOR
            return false;
#else
            return true;
#endif
        }
    }

    // Deferred preset load -- VRQualityMenu picks this up after it initializes
    public static int _pendingPresetSlot = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("_SceneDefaults");
        go.AddComponent<SceneDefaults>();
        DontDestroyOnLoad(go);
    }

    void Awake() { ApplyAll(); }

    void ApplyAll()
    {
        string profile = SafeMode ? "SAFE (Quest)" : "HI-END (Editor)";
        Debug.Log($"[PLAGA44] SceneDefaults: profile={profile}");

        // Standard gravity -- required for player to land on terrain
        Physics.gravity = new Vector3(0, -9.81f, 0);
        Debug.Log("[PLAGA44] SceneDefaults: GRAVITY ON (-9.81)");

        ApplyResolution();
        ApplyShadows();
        ApplyLighting();
        ApplyFog();
        ApplyTextures();
        ApplyColorGrading();
        ApplySkybox();
        ApplyAmbient();
        ApplyCamera();
        ApplyWater();
        ApplyTerrain();
        ApplyReflectionProbe();

        int autoSlot = SafeMode ? 3 : 1;
        string presetData = PlayerPrefs.GetString($"PLAGA44_PRESET_{autoSlot}", "");
        if (!string.IsNullOrEmpty(presetData))
        {
            Debug.Log($"[PLAGA44] SceneDefaults: auto-loading SLOT {autoSlot} ({presetData.Length} chars)");
            _pendingPresetSlot = autoSlot;
        }

        Debug.Log("[PLAGA44] SceneDefaults: defaults applied.");
    }

    // ========== RESOLUTION ==========
    void ApplyResolution()
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;
        urp.supportsCameraDepthTexture = true;

        if (SafeMode)
        { urp.renderScale = 1.2f; XRSettings.eyeTextureResolutionScale = 1.2f; urp.msaaSampleCount = 2; }
        else
        { urp.renderScale = 1.5f; XRSettings.eyeTextureResolutionScale = 1.5f; urp.msaaSampleCount = 8; }
    }

    // ========== SHADOWS ==========
    void ApplyShadows()
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;
        urp.shadowDepthBias = 10f;
        urp.shadowNormalBias = 0f;

        if (SafeMode)
        { urp.shadowDistance = 150f; urp.mainLightShadowmapResolution = 4096; }
        else
        { urp.shadowDistance = 135f; urp.mainLightShadowmapResolution = 4096; }
    }

    // ========== LIGHTING ==========
    void ApplyLighting()
    {
        var light = FindMainDirectionalLight();
        if (light == null) return;

        if (SafeMode)
        {
            light.intensity = 3.2f;
            light.color = new Color(1.00f, 0.90f, 0.96f);
            light.shadowStrength = 0.7f;
            light.bounceIntensity = 1f;
        }
        else
        {
            light.intensity = 1.4f;
            light.color = new Color(0.90f, 0.92f, 0.86f);
            light.shadowStrength = 0.935f;
            light.bounceIntensity = 4.26f;
        }
    }

    // ========== FOG ==========
    void ApplyFog()
    {
        RenderSettings.fog = true;
        if (SafeMode)
        {
            RenderSettings.fogDensity = 0.1f;
            RenderSettings.fogStartDistance = 0f;
            RenderSettings.fogEndDistance = 400f;
            RenderSettings.fogColor = new Color(0.38f, 0.44f, 0.44f);
        }
        else
        {
            RenderSettings.fogDensity = 0f;
            RenderSettings.fogStartDistance = 0f;
            RenderSettings.fogEndDistance = 400f;
            RenderSettings.fogColor = new Color(0.22f, 0.26f, 0.28f);
        }
    }

    // ========== TEXTURES ==========
    void ApplyTextures()
    {
        if (SafeMode)
        { QualitySettings.globalTextureMipmapLimit = 3; QualitySettings.lodBias = 2.0f; }
        else
        { QualitySettings.globalTextureMipmapLimit = 0; QualitySettings.lodBias = 0.3f; }
    }

    // ========== COLOR GRADING ==========
    void ApplyColorGrading()
    {
        var volume = FindAnyObjectByType<Volume>();
        if (volume == null || volume.profile == null) return;

        if (SafeMode)
        {
            volume.weight = 1f;
            if (volume.profile.TryGet<ColorAdjustments>(out var c))
            {
                c.postExposure.Override(1.2f);
                c.contrast.Override(50f);
                c.saturation.Override(10f);
                c.hueShift.Override(0f);
                c.colorFilter.Override(Color.white);
            }
        }
        else
        {
            if (volume.profile.TryGet<ColorAdjustments>(out var c))
            {
                c.postExposure.Override(1.5f);
                c.contrast.Override(80f);
                c.saturation.Override(40f);
                c.hueShift.Override(5f);
                c.colorFilter.Override(new Color(0.86f, 0.76f, 0.82f));
            }
        }
    }

    // ========== SKYBOX ==========
    void ApplySkybox()
    {
        var mat = RenderSettings.skybox;
        if (mat == null) return;

        var customShader = Shader.Find("Flooded_Grounds/Skybox_Rotating");
        if (customShader != null && mat.shader != customShader)
        {
            mat.shader = customShader;
            Debug.Log("[PLAGA44] SceneDefaults: skybox -> Skybox_Rotating");
        }

        if (SafeMode)
        {
            if (mat.HasColor("_Tint")) mat.SetColor("_Tint", new Color(1.40f, 1.55f, 1.85f));
            if (mat.HasFloat("_Exposure")) mat.SetFloat("_Exposure", 0.3f);
            if (mat.HasFloat("_Rotation")) mat.SetFloat("_Rotation", 181f);
            if (mat.HasFloat("_CloudBoost")) mat.SetFloat("_CloudBoost", 3.18f);
            if (mat.HasFloat("_CloudThreshold")) mat.SetFloat("_CloudThreshold", 0.234f);
        }
        else
        {
            if (mat.HasColor("_Tint")) mat.SetColor("_Tint", new Color(1.55f, 1.70f, 1.80f));
            if (mat.HasFloat("_Exposure")) mat.SetFloat("_Exposure", 0.2f);
            if (mat.HasFloat("_Rotation")) mat.SetFloat("_Rotation", 335f);
            if (mat.HasFloat("_CloudBoost")) mat.SetFloat("_CloudBoost", 2.77f);
            if (mat.HasFloat("_CloudThreshold")) mat.SetFloat("_CloudThreshold", 0.379f);
        }

        if (mat.HasFloat("_RotSpeed")) mat.SetFloat("_RotSpeed", 0f);
        SkyRotator.RotationSpeed = 0.31f;
    }

    // ========== AMBIENT ==========
    void ApplyAmbient()
    {
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.3f;
        RenderSettings.ambientLight = new Color(0.12f, 0.53f, 1.00f);
        RenderSettings.reflectionIntensity = 1.0f;
        RenderSettings.defaultReflectionResolution = 512;
    }

    // ========== CAMERA ==========
    void ApplyCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.15f; // avoid seeing inside avatar/models
        cam.farClipPlane = 2000f;
        Debug.Log("[PLAGA44] SceneDefaults: camera clearFlags -> Skybox");
    }

    // ========== WATER ==========
    void ApplyWater()
    {
        Material waterMat = null;
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            foreach (var m in r.sharedMaterials)
                if (m != null && m.name.Contains("Water")) { waterMat = m; break; }
            if (waterMat != null) break;
        }
        if (waterMat == null) return;

        // Same water values for both profiles (tuned in VR)
        waterMat.SetColor("_Color", new Color(0.318f, 0.381f, 0.404f));
        waterMat.SetFloat("_Metallic", 0.210f);
        waterMat.SetFloat("_Smth", 0.423f);
        waterMat.SetFloat("_ScrollSpeed", 0.007f);
        waterMat.SetFloat("_WaveHeight", 0.050f);
        waterMat.SetFloat("_WaveFreq", 0.8f);
        waterMat.SetFloat("_WaveComplexity", 0.5f);
        waterMat.SetFloat("_WaveSteepness", 0.3f);
        waterMat.SetFloat("_BumpScale", 3.0f);
        waterMat.SetFloat("_Emis", 0.312f);
        waterMat.SetFloat("_ReflStr", 1.793f);
        waterMat.SetFloat("_FresnelPow", 7.01f);
        waterMat.SetFloat("_UVScale", 3.7f);
        waterMat.SetFloat("_Alpha", 0.707f);
        waterMat.SetFloat("_FoamDepth", 0.61f);
        waterMat.SetFloat("_FoamStr", 0.27f);
        waterMat.SetColor("_FoamColor", new Color(0.772f, 0.836f, 0.764f, 0.8f));

        Debug.Log("[PLAGA44] SceneDefaults: water configured.");
    }

    // ========== TERRAIN ==========
    void ApplyTerrain()
    {
        var terrain = FindAnyObjectByType<Terrain>();
        if (terrain == null || terrain.terrainData == null) return;

        if (terrain.materialTemplate != null)
        {
            var mat = terrain.materialTemplate;
            if (mat.HasFloat("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
            if (mat.HasFloat("_BumpScale")) mat.SetFloat("_BumpScale", 1.0f);
            if (mat.HasFloat("_Metallic")) mat.SetFloat("_Metallic", 0f);
        }

        var layers = terrain.terrainData.terrainLayers;
        if (layers != null)
        {
            if (layers.Length > 0 && layers[0] != null)
            { layers[0].normalScale = 0.160f; layers[0].tileSize = new Vector2(17.8f, 17.8f); layers[0].metallic = 0.026f; layers[0].smoothness = 0.177f; }
            if (layers.Length > 1 && layers[1] != null)
            { layers[1].normalScale = 0.128f; layers[1].tileSize = new Vector2(1.5f, 1.5f); layers[1].metallic = 0f; layers[1].smoothness = 0f; }
            if (layers.Length > 2 && layers[2] != null)
            { layers[2].normalScale = 0.060f; layers[2].tileSize = new Vector2(12.3f, 12.3f); layers[2].metallic = 0f; layers[2].smoothness = 0.051f; }
        }

        Debug.Log("[PLAGA44] SceneDefaults: terrain configured.");
    }

    // ========== REFLECTION PROBE ==========
    void ApplyReflectionProbe()
    {
        if (SafeMode) return;
        if (FindAnyObjectByType<ReflectionProbe>() != null) return;

        var go = new GameObject("_WaterReflectionProbe");
        go.transform.position = new Vector3(500f, 30f, 400f);
        var probe = go.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
        probe.size = new Vector3(1000f, 200f, 1000f);
        probe.resolution = 256;
        probe.hdr = true;
        probe.nearClipPlane = 0.3f;
        probe.farClipPlane = 1000f;
        probe.RenderProbe();
    }

    static Light FindMainDirectionalLight()
    {
        var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights)
            if (l.type == LightType.Directional) return l;
        return null;
    }
}
