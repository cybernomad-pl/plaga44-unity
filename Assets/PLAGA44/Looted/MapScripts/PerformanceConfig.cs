// PerformanceConfig.cs
// CYBERNOMAD -- Runtime performance optimization for Quest 3.
// Enables ASW, FFR, Dynamic Resolution via OVRManager API.
// Add to OVRPlayerController root or any persistent GameObject.

using UnityEngine;

public class PerformanceConfig : MonoBehaviour
{
    [Header("Application SpaceWarp")]
    [Tooltip("Synthesizes every other frame. ~2x GPU performance.")]
    public bool enableASW = true;

    [Header("Fixed Foveated Rendering")]
    [Tooltip("Reduces GPU load at peripheral edges.")]
    public bool enableFFR = true;
    public OVRManager.FoveatedRenderingLevel ffrLevel = OVRManager.FoveatedRenderingLevel.HighTop;
    public bool useDynamicFFR = true;

    [Header("Dynamic Resolution")]
    [Tooltip("Auto-scales render resolution when FPS drops. Configured on OVRManager instance.")]
    public bool enableDynamicResolution = false;
    [Range(0.5f, 1.0f)]
    public float minResolutionScale = 0.7f;
    [Range(0.8f, 1.6f)]
    public float maxResolutionScale = 1.0f;

    [Header("CPU/GPU Levels")]
    [Tooltip("PowerSavings=0, SustainedLow=1, SustainedHigh=2, Boost=3")]
    public OVRManager.ProcessorPerformanceLevel cpuLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;
    public OVRManager.ProcessorPerformanceLevel gpuLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;

    void Start()
    {
        ApplySettings();
    }

    public void ApplySettings()
    {
        // ASW (Application SpaceWarp)
        OVRManager.SetSpaceWarp(enableASW);
        Debug.Log($"[PLAGA44] PerformanceConfig: ASW={enableASW}");

        // FFR (Fixed Foveated Rendering)
        OVRManager.foveatedRenderingLevel = enableFFR ? ffrLevel : OVRManager.FoveatedRenderingLevel.Off;
        OVRManager.useDynamicFoveatedRendering = enableFFR && useDynamicFFR;
        Debug.Log($"[PLAGA44] PerformanceConfig: FFR={ffrLevel}, Dynamic={useDynamicFFR}");

        // Dynamic Resolution -- instance property on OVRManager
        var mgr = OVRManager.instance;
        if (mgr != null)
        {
            mgr.enableDynamicResolution = enableDynamicResolution;
            if (enableDynamicResolution)
            {
                mgr.minDynamicResolutionScale = minResolutionScale;
                mgr.maxDynamicResolutionScale = maxResolutionScale;
                Debug.Log($"[PLAGA44] PerformanceConfig: DynamicResolution min={minResolutionScale} max={maxResolutionScale}");
            }
        }
        else
        {
            Debug.LogWarning("[PLAGA44] PerformanceConfig: OVRManager.instance not found -- dynamic resolution skipped.");
        }

        // CPU/GPU Performance Levels
        OVRManager.suggestedCpuPerfLevel = cpuLevel;
        OVRManager.suggestedGpuPerfLevel = gpuLevel;
        Debug.Log($"[PLAGA44] PerformanceConfig: CPU={cpuLevel}, GPU={gpuLevel}");
    }
}
