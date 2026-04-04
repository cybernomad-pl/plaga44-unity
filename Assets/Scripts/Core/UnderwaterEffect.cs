// UnderwaterEffect.cs
// CYBERNOMAD -- When camera goes below water level, applies:
//   - Dark green color tint via post-process ColorAdjustments
//   - Vignette intensified
//   - Fog color shift to murky green

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class UnderwaterEffect : MonoBehaviour
{
    [Header("Water Level")]
    public float waterY = 0f; // auto-detected from water mesh if 0

    [Header("Underwater Look")]
    public Color underwaterTint = new Color(0.3f, 0.55f, 0.35f);
    public Color underwaterFog = new Color(0.02f, 0.08f, 0.04f);
    public float underwaterVignette = 0.55f;
    public float underwaterExposure = 0.4f;

    private Volume _volume;
    private ColorAdjustments _colorAdj;
    private Vignette _vignette;
    private bool _isUnderwater = false;

    // Saved above-water values
    private float _savedExposure;
    private Color _savedColorFilter;
    private float _savedVignette;
    private Color _savedFogColor;
    private bool _savedFogState;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
#if LOCOMOTION_ONLY
        return;
#endif
        var go = new GameObject("_UnderwaterEffect");
        go.AddComponent<UnderwaterEffect>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        _volume = FindAnyObjectByType<Volume>();
        if (_volume != null && _volume.profile != null)
        {
            _volume.profile.TryGet(out _colorAdj);
            _volume.profile.TryGet(out _vignette);
        }

        // Auto-detect water level from water mesh
        if (waterY == 0f)
        {
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m != null && m.name.Contains("Water"))
                    {
                        waterY = r.bounds.center.y;
                        break;
                    }
                }
                if (waterY != 0f) break;
            }
        }

        // Save current values
        if (_colorAdj != null)
        {
            _savedExposure = _colorAdj.postExposure.value;
            _savedColorFilter = _colorAdj.colorFilter.value;
        }
        if (_vignette != null)
            _savedVignette = _vignette.intensity.value;
        _savedFogColor = RenderSettings.fogColor;
        _savedFogState = RenderSettings.fog;

        Debug.Log($"[PLAGA44] UnderwaterEffect: water level Y={waterY:F1}");
    }

    void Update()
    {
        var cam = Camera.main;
        if (cam == null) return;

        bool under = cam.transform.position.y < waterY;

        if (under && !_isUnderwater)
            EnterUnderwater();
        else if (!under && _isUnderwater)
            ExitUnderwater();

        _isUnderwater = under;
    }

    void EnterUnderwater()
    {
        // Save current state before overriding
        if (_colorAdj != null)
        {
            _savedExposure = _colorAdj.postExposure.value;
            _savedColorFilter = _colorAdj.colorFilter.value;
        }
        if (_vignette != null)
            _savedVignette = _vignette.intensity.value;
        _savedFogColor = RenderSettings.fogColor;

        // Apply underwater
        if (_colorAdj != null)
        {
            _colorAdj.postExposure.Override(underwaterExposure);
            _colorAdj.colorFilter.Override(underwaterTint);
        }
        if (_vignette != null)
        {
            _vignette.active = true;
            _vignette.intensity.Override(underwaterVignette);
        }
        RenderSettings.fog = true;
        RenderSettings.fogColor = underwaterFog;
        RenderSettings.fogDensity = 0.05f;
        RenderSettings.fogMode = FogMode.Exponential;
    }

    void ExitUnderwater()
    {
        // Restore saved values
        if (_colorAdj != null)
        {
            _colorAdj.postExposure.Override(_savedExposure);
            _colorAdj.colorFilter.Override(_savedColorFilter);
        }
        if (_vignette != null)
            _vignette.intensity.Override(_savedVignette);
        RenderSettings.fogColor = _savedFogColor;
        RenderSettings.fogDensity = 0f;
        RenderSettings.fogMode = FogMode.Linear;
    }
}
