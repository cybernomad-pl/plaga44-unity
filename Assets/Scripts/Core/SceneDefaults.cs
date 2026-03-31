// SceneDefaults.cs
// CYBERNOMAD -- Applies tuned rendering defaults on scene load.
// Values from VR debug menu SaveToLog (session 2026-03-30).

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

[DefaultExecutionOrder(-100)]
public class SceneDefaults : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("_SceneDefaults");
        go.AddComponent<SceneDefaults>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        ApplyAll();
    }

    void ApplyAll()
    {
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

        Debug.Log("[PLAGA44] SceneDefaults: all rendering defaults applied.");
    }

    void ApplyResolution()
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;

        urp.renderScale = 1.5f;
        XRSettings.eyeTextureResolutionScale = 1.5f;
        urp.msaaSampleCount = 8;
        urp.supportsCameraDepthTexture = true;
    }

    void ApplyShadows()
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;

        urp.shadowDistance = 135f;
        urp.shadowDepthBias = 10f;
        urp.shadowNormalBias = 0f;
        urp.mainLightShadowmapResolution = 4096;
    }

    void ApplyLighting()
    {
        var light = FindMainDirectionalLight();
        if (light == null) return;

        light.intensity = 1.4f;
        light.color = new Color(0.90f, 0.92f, 0.86f);
        light.shadowStrength = 0.935f;
        light.bounceIntensity = 4.26f;
    }

    void ApplyFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogDensity = 0f;
        RenderSettings.fogStartDistance = 0f;
        RenderSettings.fogEndDistance = 400f;
        RenderSettings.fogColor = new Color(0.22f, 0.26f, 0.28f);
    }

    void ApplyTextures()
    {
        QualitySettings.globalTextureMipmapLimit = 0;
        QualitySettings.lodBias = 0.3f;
    }

    void ApplyColorGrading()
    {
        var volume = FindAnyObjectByType<Volume>();
        if (volume == null || volume.profile == null) return;

        if (volume.profile.TryGet<ColorAdjustments>(out var color))
        {
            color.postExposure.Override(1.5f);
            color.contrast.Override(80f);
            color.saturation.Override(40f);
            color.hueShift.Override(5f);
            color.colorFilter.Override(new Color(0.86f, 0.76f, 0.82f));
        }
    }

    void ApplySkybox()
    {
        var mat = RenderSettings.skybox;
        if (mat == null) return;

        // Switch to our custom skybox shader with cloud boost
        var customShader = Shader.Find("Flooded_Grounds/Skybox_Rotating");
        if (customShader != null && mat.shader != customShader)
        {
            mat.shader = customShader;
            Debug.Log("[PLAGA44] SceneDefaults: skybox switched to Skybox_Rotating");
        }

        if (mat.HasColor("_Tint"))
            mat.SetColor("_Tint", new Color(1.55f, 1.70f, 1.80f));

        if (mat.HasFloat("_Exposure"))
            mat.SetFloat("_Exposure", 0.2f);

        if (mat.HasFloat("_Rotation"))
            mat.SetFloat("_Rotation", 335f);

        if (mat.HasFloat("_CloudBoost"))
            mat.SetFloat("_CloudBoost", 2.77f);

        if (mat.HasFloat("_CloudThreshold"))
            mat.SetFloat("_CloudThreshold", 0.379f);

        if (mat.HasFloat("_RotSpeed"))
            mat.SetFloat("_RotSpeed", 0f);

        SkyRotator.RotationSpeed = 0.31f;
    }

    void ApplyAmbient()
    {
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.3f;
        RenderSettings.ambientLight = new Color(0.12f, 0.53f, 1.00f);
        RenderSettings.reflectionIntensity = 1.0f;
        RenderSettings.defaultReflectionResolution = 512;
    }

    void ApplyCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;

        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 2000f;
    }

    void ApplyWater()
    {
        Material waterMat = null;
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m != null && m.name.Contains("Water"))
                {
                    waterMat = m;
                    break;
                }
            }
            if (waterMat != null) break;
        }
        if (waterMat == null) return;

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
            {
                layers[0].normalScale = 0.160f;
                layers[0].tileSize = new Vector2(17.8f, 17.8f);
                layers[0].metallic = 0.026f;
                layers[0].smoothness = 0.177f;
            }
            if (layers.Length > 1 && layers[1] != null)
            {
                layers[1].normalScale = 0.128f;
                layers[1].tileSize = new Vector2(1.5f, 1.5f);
                layers[1].metallic = 0f;
                layers[1].smoothness = 0f;
            }
            if (layers.Length > 2 && layers[2] != null)
            {
                layers[2].normalScale = 0.060f;
                layers[2].tileSize = new Vector2(12.3f, 12.3f);
                layers[2].metallic = 0f;
                layers[2].smoothness = 0.051f;
            }
        }

        Debug.Log("[PLAGA44] SceneDefaults: terrain configured.");
    }

    void ApplyReflectionProbe()
    {
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
