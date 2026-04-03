/// <summary>
/// PLAGA '44 DEMO BASELINE -- values dumped from Quest 2 session 2026-04-02 15:16.
/// THIS IS THE GROUND TRUTH. All build profiles derive from this.
/// DO NOT MODIFY without testing on device.
/// </summary>
public static class PresetBaseline
{
    // Dump source: adb logcat | grep PLAGA44_SETTINGS
    // Device: Quest 2 (1WMHHB67FS2286)
    // Scene: PLAGA44_Demo (origin/main, commit 975ce20)
    // Date: 2026-04-02T15:16:15Z

    // --- RESOLUTION ---
    public const float RenderScale = 1.5f;
    public const float EyeTextureScale = 1.5f;
    public const int MSAA = 8;

    // --- SHADOWS ---
    public const float ShadowDistance = 80f;
    public const float ShadowDepthBias = 10f;
    public const float ShadowNormalBias = 0f;
    public const int ShadowResolution = 4096;

    // --- LIGHTING ---
    public const float LightIntensity = 1f;
    public const float LightR = 0.96f;
    public const float LightG = 0.98f;
    public const float LightB = 1f;
    public const float LightShadowStrength = 1f;

    // --- FOG ---
    public const bool FogEnabled = true;
    public const float FogDensity = 0f;
    public const float FogStart = 0f;
    public const float FogEnd = 500f;
    public const float FogR = 0f;
    public const float FogG = 0f;
    public const float FogB = 0f;

    // --- TEXTURES ---
    public const int TextureQualityMip = 0; // full quality
    public const float LODBias = 0.3f;

    // --- COLOR GRADING ---
    public const float Exposure = 1.6f;
    public const float Contrast = 60f;
    public const float Saturation = 35f;
    public const float HueShift = 0f;
    public const float ColorR = 0.93f;
    public const float ColorG = 0.90f;
    public const float ColorB = 0.88f;

    // --- SKYBOX ---
    public const float SkyTintR = 1f;
    public const float SkyTintG = 1.15f;
    public const float SkyTintB = 1.30f;
    public const float SkyExposure = 1f;
    public const float SkyRotation = 0f;

    // --- AMBIENT ---
    public const float AmbientIntensity = 0f;
    public const float AmbientR = 0f;
    public const float AmbientG = 0f;
    public const float AmbientB = 0f;

    // --- SYSTEM ---
    public const bool PostProcessing = true;
    public const int FoveatedRenderLevel = 3;
    public const int DisplayRefreshRate = 72;
    public const float NearClip = 0.05f; // bumped from 0.01 -- geometry clipping
    public const float FarClip = 2000f;

    // Invariant string format for VRQualityMenu preset loader
    public const string Data =
        "Render Scale=1.5000;" +
        "Eye Texture Scale=1.5000;" +
        "MSAA=8.0000;" +
        "Shadow Distance=80.0000;" +
        "Shadow Depth Bias=10.0000;" +
        "Shadow Normal Bias=0.0000;" +
        "Shadow Resolution=4096.0000;" +
        "Directional Light Intensity=1.0000;" +
        "Directional Light R=0.9600;" +
        "Directional Light G=0.9800;" +
        "Directional Light B=1.0000;" +
        "Light Shadow Strength=1.0000;" +
        "Fog Enabled=1.0000;" +
        "Fog Density=0.0000;" +
        "Fog Start=0.0000;" +
        "Fog End=500.0000;" +
        "Fog R=0.0000;" +
        "Fog G=0.0000;" +
        "Fog B=0.0000;" +
        "Texture Quality (mip)=0.0000;" +
        "LOD Bias=0.3000;" +
        "Exposure=1.6000;" +
        "Contrast=60.0000;" +
        "Saturation=35.0000;" +
        "Hue Shift=0.0000;" +
        "Color R=0.9300;" +
        "Color G=0.9000;" +
        "Color B=0.8800;" +
        "Sky Tint R=1.0000;" +
        "Sky Tint G=1.1500;" +
        "Sky Tint B=1.3000;" +
        "Sky Exposure=1.0000;" +
        "Sky Rotation=0.0000;" +
        "Ambient Intensity=0.0000;" +
        "Ambient R=0.0000;" +
        "Ambient G=0.0000;" +
        "Ambient B=0.0000;" +
        "Post Processing=1.0000;" +
        "Foveated Render Level=3.0000;" +
        "Display Refresh Rate=72.0000;" +
        "Near Clip=0.0500;" +
        "Far Clip=2000.0000;";
}
