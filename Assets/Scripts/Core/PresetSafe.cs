// PresetSafe.cs
// CYBERNOMAD -- Hardcoded SAFE preset from Quest session 2026-03-31.
// Fallback when PlayerPrefs SLOT 3 is empty or corrupted.
// Values extracted from ADB logcat PLAGA44_SETTINGS dump.

public static class PresetSafe
{
    // Invariant format: "Name=Value;Name=Value;..."
    public const string Data =
        "Render Scale=1.2000;" +
        "Eye Texture Scale=1.2000;" +
        "MSAA=2.0000;" +
        "Shadow Distance=150.0000;" +
        "Shadow Depth Bias=10.0000;" +
        "Shadow Normal Bias=0.0000;" +
        "Shadow Resolution=4096.0000;" +
        "Directional Light Intensity=3.2000;" +
        "Directional Light R=1.0000;" +
        "Directional Light G=0.9000;" +
        "Directional Light B=0.9600;" +
        "Light Shadow Strength=0.7000;" +
        "Light Indirect Multiplier=1.0000;" +
        "Fog Enabled=1.0000;" +
        "Fog Density=0.1000;" +
        "Fog Start=0.0000;" +
        "Fog End=400.0000;" +
        "Fog R=0.3800;" +
        "Fog G=0.4400;" +
        "Fog B=0.4400;" +
        "Texture Quality (mip)=3.0000;" +
        "LOD Bias=2.0000;" +
        "Exposure=1.2000;" +
        "Contrast=50.0000;" +
        "Saturation=10.0000;" +
        "Hue Shift=0.0000;" +
        "Color R=1.0000;" +
        "Color G=1.0000;" +
        "Color B=1.0000;" +
        "Sky Tint R=1.4000;" +
        "Sky Tint G=1.5500;" +
        "Sky Tint B=1.8500;" +
        "Sky Exposure=0.3000;" +
        "Sky Rotation=181.0000;" +
        "Cloud Brightness=3.1800;" +
        "Cloud Threshold=0.2340;" +
        "Ambient Intensity=1.3000;" +
        "Ambient R=0.1200;" +
        "Ambient G=0.5300;" +
        "Ambient B=1.0000;" +
        "Post Processing=1.0000;" +
        "Foveated Render Level=3.0000;" +
        "Display Refresh Rate=72.0000;" +
        "Near Clip=0.0100;" +
        "Far Clip=2000.0000;" +
        "Water R=0.3180;" +
        "Water G=0.3810;" +
        "Water B=0.4040;" +
        "Water Metallic=0.2100;" +
        "Water Smoothness=0.4230;" +
        "Water Scroll Speed=0.0070;" +
        "Water Wave Height=0.0500;" +
        "Water Wave Freq=0.8000;" +
        "Water Wave Complexity=0.5000;" +
        "Water Wave Steepness=0.3000;" +
        "Water Normal Strength=3.0000;" +
        "Water Emission=0.3120;" +
        "Water Reflection Str=1.7930;" +
        "Water Fresnel Power=7.0100;" +
        "Water UV Density=3.7000;" +
        "Water Transparency=0.7070;" +
        "Water Foam Depth=0.6100;" +
        "Water Foam Strength=0.2700;" +
        "Water Foam R=0.7720;" +
        "Water Foam G=0.8360;" +
        "Water Foam B=0.7640;" +
        "Sky Rotation Speed=0.3100;";
}
