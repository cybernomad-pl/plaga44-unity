// LocomotionOnlyMode.cs
// CYBERNOMAD -- Strip test: disable everything except locomotion.
// Enable/disable via LOCOMOTION_ONLY scripting define in Player Settings.

using UnityEngine;

#if LOCOMOTION_ONLY
public class LocomotionOnlyMode
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Strip()
    {
        // Kill all auto-created systems except locomotion + core VR
        string[] keepTypes = {
            "OVRCameraRig", "OVRManager", "OVRPlayerController",
            "CharacterController", "SmoothLocomotion", "TeleportLocomotion",
            "VRLocomotion", "SprintModifier", "VRCrouch",
            "SceneDefaults", "TerrainFix", "SkyRotator",
            "SplashScreen", "StartupLogger",
            "Camera", "AudioListener", "EventSystem",
            "LocomotionOnlyMode"
        };

        string[] destroyTypes = {
            "VRItemSpawner", "VFXSpawnerMenu", "VRQualityMenu", "VRMenuManager",
            "ModelSpawner", "ModelExhibition", "LaserInspector", "UIRayPointer",
            "BodyTrackingBootstrap", "BodyTrackingManager", "BodyCalibration",
            "PlayerAvatar", "GrabLogger", "GrabHandPose", "MakeGrabbable",
            "BoneTouchHaptics", "M249GripFix", "M249Handler", "M249Disassembly",
            "WaterScoop", "WaterEdgeSplash", "UnderwaterEffect",
            "PerformanceBenchmark", "VRPostProcessEnable",
            "FaceTrackingManager", "FaceExpressionDebug",
            "MaterialTweaks", "TerrainDeformer",
            "HapticManager", "HapticFeedback", "HapticOnGrab",
            "EnemyAI", "EnemySpawner", "EnemyHealth",
            "SpatialAudioManager",
        };

        int destroyed = 0;
        foreach (var typeName in destroyTypes)
        {
            var type = System.Type.GetType(typeName) ??
                       System.Type.GetType("Plaga44.UI." + typeName) ??
                       System.Type.GetType("Plaga44.BodyTracking." + typeName) ??
                       System.Type.GetType("Plaga44.FaceTracking." + typeName);

            if (type == null) continue;

            var objs = Object.FindObjectsByType(type, FindObjectsSortMode.None);
            foreach (var obj in objs)
            {
                if (obj is MonoBehaviour mb)
                {
                    mb.enabled = false;
                    Debug.Log($"[LOCOMOTION_ONLY] Disabled: {mb.GetType().Name} on {mb.gameObject.name}");
                    destroyed++;
                }
            }
        }

        // Also destroy DontDestroyOnLoad objects we don't need
        var allGOs = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in allGOs)
        {
            string n = go.name.ToLower();
            if (n.Contains("spawner") || n.Contains("exhibition") ||
                n.Contains("inspector") || n.Contains("benchmark") ||
                n.Contains("grablogger") || n.Contains("bodytracking") ||
                n.Contains("facetracking") || n.Contains("haptic") ||
                n.Contains("enemy") || n.Contains("avatar") ||
                n.Contains("_vrmenu") || n.Contains("qualitymenu"))
            {
                Object.Destroy(go);
                destroyed++;
            }
        }

        Debug.Log($"[LOCOMOTION_ONLY] Stripped {destroyed} systems. Only locomotion active.");
    }
}
#endif
