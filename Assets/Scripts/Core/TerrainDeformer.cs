// TerrainDeformer.cs
// CYBERNOMAD -- Runtime terrain heightmap deformation with Perlin noise.
// Adds organic irregularity to flat terrain.
// Controllable via VRQualityMenu sliders.

using UnityEngine;

public class TerrainDeformer : MonoBehaviour
{
    public static float NoiseScale = 0.02f;    // frequency of deformation
    public static float NoiseStrength = 0f;     // amplitude (0 = disabled)
    public static float NoiseSeed = 42f;

    private Terrain _terrain;
    private float[,] _originalHeights;
    private int _res;
    private bool _saved = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("_TerrainDeformer");
        go.AddComponent<TerrainDeformer>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        _terrain = FindAnyObjectByType<Terrain>();
        if (_terrain == null || _terrain.terrainData == null) return;

        _res = _terrain.terrainData.heightmapResolution;
        _originalHeights = _terrain.terrainData.GetHeights(0, 0, _res, _res);
        _saved = true;

        Debug.Log($"[PLAGA44] TerrainDeformer: heightmap {_res}x{_res} saved");
    }

    /// <summary>
    /// Apply Perlin noise deformation to terrain.
    /// Called from VRQualityMenu when sliders change.
    /// </summary>
    public static void ApplyDeformation()
    {
        var instance = FindAnyObjectByType<TerrainDeformer>();
        if (instance != null) instance.DoApply();
    }

    void DoApply()
    {
        if (!_saved || _terrain == null) return;

        var td = _terrain.terrainData;
        float[,] heights = new float[_res, _res];
        float terrainHeight = td.size.y;

        for (int y = 0; y < _res; y++)
        {
            for (int x = 0; x < _res; x++)
            {
                float orig = _originalHeights[y, x];

                if (NoiseStrength > 0)
                {
                    // Multi-octave Perlin noise
                    float nx = x * NoiseScale + NoiseSeed;
                    float ny = y * NoiseScale + NoiseSeed;

                    float noise = 0;
                    noise += Mathf.PerlinNoise(nx, ny) * 1.0f;
                    noise += Mathf.PerlinNoise(nx * 2.1f, ny * 2.1f) * 0.5f;
                    noise += Mathf.PerlinNoise(nx * 4.3f, ny * 4.3f) * 0.25f;
                    noise = (noise / 1.75f) - 0.5f; // normalize to -0.5..+0.5

                    float delta = noise * (NoiseStrength / terrainHeight);
                    heights[y, x] = Mathf.Clamp01(orig + delta);
                }
                else
                {
                    heights[y, x] = orig;
                }
            }
        }

        td.SetHeights(0, 0, heights);
    }

    void OnDestroy()
    {
        // Restore original heights on cleanup
        if (_saved && _terrain != null && _terrain.terrainData != null)
        {
            _terrain.terrainData.SetHeights(0, 0, _originalHeights);
        }
    }
}
