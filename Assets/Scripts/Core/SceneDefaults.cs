// SceneDefaults.cs
// CYBERNOMAD -- Applies tuned rendering defaults on scene load.
// Values captured from VR debug menu (VRQualityMenu SaveToLog).
// Runs before VRQualityMenu so the menu reads correct initial values.

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

        Debug.Log("[PLAGA44] SceneDefaults: all rendering defaults applied.");
    }

    void ApplyResolution()
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;

        urp.renderScale = 1.5f;
        XRSettings.eyeTextureResolutionScale = 1.5f;
        urp.msaaSampleCount = 8;
    }

    void ApplyShadows()
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;

        urp.shadowDistance = 80f;
        urp.shadowDepthBias = 10f;
        urp.shadowNormalBias = 0f;
        urp.mainLightShadowmapResolution = 4096;
    }

    void ApplyLighting()
    {
        var light = FindMainDirectionalLight();
        if (light == null) return;

        light.intensity = 1.0f;
        light.color = new Color(0.96f, 0.98f, 1.00f);
        light.shadowStrength = 1.0f;
    }

    void ApplyFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogDensity = 0f;
        RenderSettings.fogStartDistance = 0f;
        RenderSettings.fogEndDistance = 500f;
        RenderSettings.fogColor = Color.black;
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
            color.postExposure.Override(1.6f);
            color.contrast.Override(60f);
            color.saturation.Override(35f);
            color.hueShift.Override(0f);
            color.colorFilter.Override(new Color(0.93f, 0.90f, 0.88f));
        }
    }

    void ApplySkybox()
    {
        var mat = RenderSettings.skybox;
        if (mat == null) return;

        string tintProp = mat.HasColor("_Tint") ? "_Tint" : "_Color";
        if (mat.HasColor(tintProp))
            mat.SetColor(tintProp, new Color(1.00f, 1.15f, 1.30f));

        if (mat.HasFloat("_Exposure"))
            mat.SetFloat("_Exposure", 0.3f);

        if (mat.HasFloat("_Rotation"))
            mat.SetFloat("_Rotation", 100f);
    }

    void ApplyAmbient()
    {
        RenderSettings.ambientIntensity = 0f;
        RenderSettings.ambientLight = Color.black;
    }

    void ApplyCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;

        cam.nearClipPlane = 0.05f; // was 0.01 -- too close, clipping through geometry
        cam.farClipPlane = 2000f;
    }

    static Light FindMainDirectionalLight()
    {
        var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights)
            if (l.type == LightType.Directional) return l;
        return null;
    }
}
