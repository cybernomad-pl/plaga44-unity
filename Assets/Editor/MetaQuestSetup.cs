// MetaQuestSetup.cs
// Editor script for PLAGA '44 Meta Quest project setup.
// Automates Player Settings and Quality Settings configuration.
//
// Usage: Unity Editor menu -> PLAGA44 -> Setup Meta Quest Settings
//
// This script sets the project settings that CAN be set programmatically.
// Scene setup (OVRCameraRig, Building Blocks) must still be done manually
// in the Unity Editor -- see docs/META_SDK_SETUP.md for instructions.
//
// What this script does NOT do (must be manual):
// - Switch build platform to Meta Quest (File > Build Profiles)
// - Install Meta XR Core SDK (Package Manager)
// - Enable OpenXR in XR Plug-in Management
// - Add OVRCameraRig to scene (Building Blocks)
// - Configure Meta Quest Feature Group in OpenXR settings

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plaga44.Editor
{
    public static class MetaQuestSetup
    {
        [MenuItem("PLAGA44/Setup Meta Quest Settings")]
        public static void SetupMetaQuestSettings()
        {
            bool confirm = EditorUtility.DisplayDialog(
                "PLAGA '44 -- Meta Quest Setup",
                "This will configure Player Settings and Quality Settings for Meta Quest 2/3.\n\n" +
                "Changes:\n" +
                "- Color Space: Linear\n" +
                "- Graphics API: Vulkan (Android)\n" +
                "- Scripting Backend: IL2CPP\n" +
                "- Architecture: ARM64\n" +
                "- Minimum API Level: Android 10 (29)\n" +
                "- Target API Level: Android 12L (32)\n" +
                "- Quality: 4x MSAA, no VSync\n" +
                "- Product/Company names\n\n" +
                "Continue?",
                "Apply Settings",
                "Cancel"
            );

            if (!confirm) return;

            SetPlayerSettings();
            SetQualitySettings();

            Debug.Log("[PLAGA44] Meta Quest settings applied. Next steps:");
            Debug.Log("[PLAGA44] 1. File > Build Profiles > Meta Quest > Switch Platform");
            Debug.Log("[PLAGA44] 2. Window > Package Manager > Install Meta XR Core SDK");
            Debug.Log("[PLAGA44] 3. Edit > Project Settings > XR Plug-in Management > Enable OpenXR (Android)");
            Debug.Log("[PLAGA44] 4. Under OpenXR > Add Meta Quest Feature Group");
            Debug.Log("[PLAGA44] 5. Create scene with OVRCameraRig (Meta > Tools > Building Blocks)");
            Debug.Log("[PLAGA44] See docs/META_SDK_SETUP.md for full instructions.");

            EditorUtility.DisplayDialog(
                "PLAGA '44 -- Setup Complete",
                "Player Settings and Quality Settings configured.\n\n" +
                "NEXT STEPS (manual):\n" +
                "1. Switch Platform to Meta Quest\n" +
                "2. Install Meta XR Core SDK\n" +
                "3. Enable OpenXR in XR Plug-in Management\n" +
                "4. Add Meta Quest Feature Group\n" +
                "5. Setup scene with OVRCameraRig\n\n" +
                "See Console log and docs/META_SDK_SETUP.md for details.",
                "OK"
            );
        }

        private static void SetPlayerSettings()
        {
            // Company and product
            PlayerSettings.companyName = "Cybernomad";
            PlayerSettings.productName = "PLAGA 44";

            // Color space must be Linear for VR
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // Android-specific settings
            PlayerSettings.SetApplicationIdentifier(
                BuildTargetGroup.Android, "com.cybernomad.plaga44");

            // Graphics API: Vulkan only (required for Quest)
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.Android,
                new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);

            // Scripting backend: IL2CPP (required for ARM64)
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

            // Architecture: ARM64 only (Quest is ARM64)
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // API levels
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)32;

            // Orientation: Landscape Left (standard for VR)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            // Disable auto-rotation (VR handles orientation)
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            Debug.Log("[PLAGA44] Player Settings configured for Meta Quest.");
        }

        private static void SetQualitySettings()
        {
            // We set the current quality level settings.
            // In a real project you'd create a custom quality level,
            // but for now we modify the current one.

            // Anti-Aliasing: 4x MSAA (minimum for VR comfort)
            QualitySettings.antiAliasing = 4;

            // Disable VSync -- Meta Quest runtime manages frame timing
            QualitySettings.vSyncCount = 0;

            // Anisotropic filtering: Force On
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;

            // Texture quality: Full resolution
            QualitySettings.globalTextureMipmapLimit = 0;

            // Shadow distance: conservative for mobile
            QualitySettings.shadowDistance = 20f;

            // LOD bias: slightly aggressive for mobile VR
            QualitySettings.lodBias = 1.0f;

            // Pixel light count: keep low for mobile
            QualitySettings.pixelLightCount = 2;

            Debug.Log("[PLAGA44] Quality Settings configured for Meta Quest.");
        }

        [MenuItem("PLAGA44/Print Setup Status")]
        public static void PrintSetupStatus()
        {
            Debug.Log("=== PLAGA '44 Setup Status ===");
            Debug.Log($"Color Space: {PlayerSettings.colorSpace}");
            Debug.Log($"Company: {PlayerSettings.companyName}");
            Debug.Log($"Product: {PlayerSettings.productName}");
            Debug.Log($"Android Package: {PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)}");
            Debug.Log($"Scripting Backend (Android): {PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android)}");
            Debug.Log($"Target Architecture: {PlayerSettings.Android.targetArchitectures}");
            Debug.Log($"Min SDK: {PlayerSettings.Android.minSdkVersion}");
            Debug.Log($"MSAA: {QualitySettings.antiAliasing}x");
            Debug.Log($"VSync: {QualitySettings.vSyncCount}");
            Debug.Log($"Anisotropic: {QualitySettings.anisotropicFiltering}");
            Debug.Log("=== End Status ===");
        }
    }
}
#endif
