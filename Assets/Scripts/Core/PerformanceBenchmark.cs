// PerformanceBenchmark.cs
// CYBERNOMAD -- Runtime FPS/frame time benchmark for Quest builds.
// Logs min/max/avg FPS, frame time, draw calls, triangles.
// Auto-starts on scene load. Results dumped to log after benchmark period.

using UnityEngine;
using UnityEngine.Profiling;

public class PerformanceBenchmark : MonoBehaviour
{
    [Header("Config")]
    public float warmupSeconds = 5f;
    public float benchmarkSeconds = 30f;
    public bool showHUD = true;

    private float _timer;
    private bool _warmingUp = true;
    private bool _done;
    private int _frameCount;
    private float _totalFrameTime;
    private float _minFPS = float.MaxValue;
    private float _maxFPS = 0;
    private float _worstFrameTime;
    private long _totalTris;
    private long _totalDrawCalls;
    private int _sampleCount;
    private GUIStyle _style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
#if LOCOMOTION_ONLY
        return;
#endif
        var go = new GameObject("_Benchmark");
        go.AddComponent<PerformanceBenchmark>();
        DontDestroyOnLoad(go);
    }

    void Update()
    {
        if (_done) return;

        float dt = Time.unscaledDeltaTime;
        _timer += dt;

        if (_warmingUp)
        {
            if (_timer >= warmupSeconds)
            {
                _warmingUp = false;
                _timer = 0;
                Debug.Log("[BENCHMARK] Warmup complete. Starting measurement...");
            }
            return;
        }

        // Measure
        float fps = 1f / Mathf.Max(dt, 0.0001f);
        _frameCount++;
        _totalFrameTime += dt;
        if (fps < _minFPS) _minFPS = fps;
        if (fps > _maxFPS) _maxFPS = fps;
        if (dt > _worstFrameTime) _worstFrameTime = dt;

        if (_timer >= benchmarkSeconds)
        {
            _done = true;
            DumpResults();
        }
    }

    void DumpResults()
    {
        float avgFPS = _frameCount / Mathf.Max(_totalFrameTime, 0.001f);
        float avgFrameTime = _totalFrameTime / Mathf.Max(_frameCount, 1) * 1000f;

        string report = $@"
### PLAGA44_BENCHMARK ###
Duration: {benchmarkSeconds}s ({_frameCount} frames)
FPS avg: {avgFPS:F1}
FPS min: {_minFPS:F1}
FPS max: {_maxFPS:F1}
Frame time avg: {avgFrameTime:F2}ms
Frame time worst: {_worstFrameTime * 1000f:F2}ms
System memory: {SystemInfo.systemMemorySize}MB
GPU: {SystemInfo.graphicsDeviceName}
GPU memory: {SystemInfo.graphicsMemorySize}MB
Allocated memory: {Profiler.GetTotalAllocatedMemoryLong() / 1048576}MB
Reserved memory: {Profiler.GetTotalReservedMemoryLong() / 1048576}MB
Screen: {Screen.width}x{Screen.height}
Quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}
### PLAGA44_BENCHMARK_END ###";

        Debug.Log(report);
    }

    void OnGUI()
    {
        if (!showHUD || _done) return;

        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label);
            _style.fontSize = 18;
            _style.normal.textColor = Color.yellow;
        }

        float dt = Time.unscaledDeltaTime;
        float fps = 1f / Mathf.Max(dt, 0.0001f);
        string status = _warmingUp
            ? $"WARMUP {warmupSeconds - _timer:F0}s"
            : $"BENCH {benchmarkSeconds - _timer:F0}s | {fps:F0} FPS | {dt * 1000f:F1}ms";

        GUI.Label(new Rect(10, 10, 500, 30), status, _style);
    }
}
