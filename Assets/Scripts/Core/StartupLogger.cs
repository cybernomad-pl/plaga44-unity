using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Diagnostic startup logger. Runs before everything else and logs each init step.
/// Attach to a GameObject in the first scene or use RuntimeInitializeOnLoadMethod.
/// </summary>
public class StartupLogger : MonoBehaviour
{
    private const string TAG = "[PLAGA44-BOOT]";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void OnSubsystemRegistration()
    {
        // CRITICAL: Set eye texture scale at earliest possible moment
        // Quest 2 default is 1832x1920/eye. At 0.8 = ~1466x1536 (saves ~1GB GPU mem)
        UnityEngine.XR.XRSettings.eyeTextureResolutionScale = 1.5f;

        Log("=== SUBSYSTEM REGISTRATION ===");
        Log($"Platform: {Application.platform}");
        Log($"Device: {SystemInfo.deviceModel}");
        Log($"OS: {SystemInfo.operatingSystem}");
        Log($"RAM: {SystemInfo.systemMemorySize} MB");
        Log($"VRAM: {SystemInfo.graphicsMemorySize} MB");
        Log($"GPU: {SystemInfo.graphicsDeviceName}");
        Log($"GraphicsAPI: {SystemInfo.graphicsDeviceType}");
        Log($"MaxTextureSize: {SystemInfo.maxTextureSize}");
        Log($"XR eyeTextureResolutionScale: {UnityEngine.XR.XRSettings.eyeTextureResolutionScale}");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    static void OnAfterAssembliesLoaded()
    {
        Log("=== ASSEMBLIES LOADED ===");
        LogMemory();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    static void OnBeforeSplashScreen()
    {
        Log("=== BEFORE SPLASH SCREEN ===");
        LogXRStatus();
        LogMemory();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void OnBeforeSceneLoad()
    {
        Log("=== BEFORE SCENE LOAD ===");
        LogXRStatus();
        LogMemory();

        // Create persistent logger GO
        var go = new GameObject("_StartupLogger");
        go.AddComponent<StartupLogger>();
        DontDestroyOnLoad(go);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnAfterSceneLoad()
    {
        Log("=== AFTER SCENE LOAD ===");
        LogXRStatus();
        LogMemory();
        LogSceneInfo();
    }

    void Awake()
    {
        Log("StartupLogger.Awake()");
        StartCoroutine(MonitorStartup());
    }

    void Start()
    {
        Log("StartupLogger.Start()");
        LogMemory();
    }

    IEnumerator MonitorStartup()
    {
        // Log every second for the first 10 seconds
        for (int i = 1; i <= 10; i++)
        {
            yield return new WaitForSeconds(1f);
            Log($"--- T+{i}s ---");
            LogMemory();
            LogXRStatus();
            Log($"  FPS: {1f / Time.deltaTime:F1}");
            Log($"  FrameCount: {Time.frameCount}");
        }
        Log("=== STARTUP MONITOR COMPLETE (10s) ===");
    }

    void OnApplicationFocus(bool hasFocus)
    {
        Log($"OnApplicationFocus({hasFocus})");
        if (hasFocus)
        {
            // Aggressive GC on regaining focus
            System.GC.Collect();
            Resources.UnloadUnusedAssets();
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        Log($"OnApplicationPause({pauseStatus})");
    }

    void OnLowMemory()
    {
        Log("!!! ON LOW MEMORY !!!");
        LogMemory();
    }

    static void LogXRStatus()
    {
        try
        {
            var xrSettings = XRGeneralSettings.Instance;
            if (xrSettings == null)
            {
                Log("  XR: XRGeneralSettings.Instance is NULL");
                return;
            }

            var mgr = xrSettings.Manager;
            if (mgr == null)
            {
                Log("  XR: Manager is NULL");
                return;
            }

            var loader = mgr.activeLoader;
            Log($"  XR: Manager exists, activeLoader={loader?.GetType().Name ?? "NULL"}");
            Log($"  XR: isInitializationComplete={mgr.isInitializationComplete}");

            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            Log($"  XR: DisplaySubsystems count={displays.Count}");
            foreach (var d in displays)
                Log($"    Display: running={d.running}");

            var inputs = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(inputs);
            Log($"  XR: InputSubsystems count={inputs.Count}");
        }
        catch (Exception e)
        {
            Log($"  XR STATUS ERROR: {e.Message}");
        }
    }

    static void LogMemory()
    {
        Log($"  Memory: Mono heap={GC.GetTotalMemory(false) / 1024 / 1024}MB, " +
            $"Unity reserved={UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1024 / 1024}MB, " +
            $"Unity allocated={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024}MB, " +
            $"System RAM={SystemInfo.systemMemorySize}MB");
    }

    static void LogSceneInfo()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Log($"  Scene: \"{scene.name}\" objects={scene.rootCount}");
        var roots = scene.GetRootGameObjects();
        foreach (var go in roots)
            Log($"    Root: {go.name} (active={go.activeSelf}, components={go.GetComponents<Component>().Length})");
    }

    static void Log(string msg)
    {
        Debug.Log($"{TAG} {msg}");
    }
}
