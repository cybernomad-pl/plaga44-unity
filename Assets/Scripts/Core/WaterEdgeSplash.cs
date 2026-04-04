// WaterEdgeSplash.cs
// CYBERNOMAD -- Dense fountain-like splash particles at water/terrain shoreline.
// Two particle systems: spray (upward fountain) + mist (soft blur at water level).

using UnityEngine;

public class WaterEdgeSplash : MonoBehaviour
{
    public float waterY = 0f;
    public float scanRadius = 50f;
    public float scanInterval = 0.08f;

    private ParticleSystem _spray;
    private ParticleSystem _mist;
    private float _scanTimer;
    private Terrain _terrain;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
#if LOCOMOTION_ONLY
        return;
#endif
    static void AutoCreate()
    {
        var go = new GameObject("_WaterEdgeSplash");
        go.AddComponent<WaterEdgeSplash>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        _terrain = FindAnyObjectByType<Terrain>();

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

        CreateSpraySystem();
        CreateMistSystem();
        Debug.Log($"[PLAGA44] WaterEdgeSplash: waterY={waterY:F1}");
    }

    void CreateSpraySystem()
    {
        var go = new GameObject("_SprayParticles");
        go.transform.SetParent(transform);
        _spray = go.AddComponent<ParticleSystem>();

        var main = _spray.main;
        main.maxParticles = 2000;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
        main.startColor = new Color(0.9f, 0.93f, 0.96f, 0.35f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.6f;
        main.playOnAwake = false;
        main.loop = false;

        var emission = _spray.emission;
        emission.enabled = false;

        var shape = _spray.shape;
        shape.enabled = false;

        // Size: burst then vanish
        var sol = _spray.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.2f),
            new Keyframe(0.1f, 1f),
            new Keyframe(0.5f, 0.6f),
            new Keyframe(1f, 0f)));

        // Color: white -> transparent
        var col = _spray.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new(new Color(0.95f, 0.96f, 0.98f), 0), new(new Color(0.85f, 0.9f, 0.95f), 1) },
            new GradientAlphaKey[] { new(0.4f, 0), new(0.25f, 0.2f), new(0f, 1) }
        );
        col.color = grad;

        // Noise for turbulence
        var noise = _spray.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 3f;
        noise.damping = true;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        var mat = new Material(Shader.Find("Particles/Standard Unlit"));
        mat.SetColor("_Color", new Color(0.95f, 0.96f, 0.98f, 0.3f));
        mat.SetFloat("_SoftParticlesEnabled", 1f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
        renderer.material = mat;
        renderer.minParticleSize = 0.0005f;
        renderer.maxParticleSize = 0.03f;
    }

    void CreateMistSystem()
    {
        var go = new GameObject("_MistParticles");
        go.transform.SetParent(transform);
        _mist = go.AddComponent<ParticleSystem>();

        var main = _mist.main;
        main.maxParticles = 500;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
        main.startColor = new Color(0.8f, 0.85f, 0.9f, 0.08f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.02f; // slight upward drift
        main.playOnAwake = false;
        main.loop = false;

        var emission = _mist.emission;
        emission.enabled = false;

        var shape = _mist.shape;
        shape.enabled = false;

        // Size: grow slowly
        var sol = _mist.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 1.5f));

        // Color: very subtle fade
        var col = _mist.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new(Color.white, 0), new(Color.white, 1) },
            new GradientAlphaKey[] { new(0.06f, 0), new(0.04f, 0.5f), new(0f, 1) }
        );
        col.color = grad;

        // Strong noise for blur/organic look
        var noise = _mist.noise;
        noise.enabled = true;
        noise.strength = 0.15f;
        noise.frequency = 0.8f;
        noise.damping = true;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        var mat = new Material(Shader.Find("Particles/Standard Unlit"));
        mat.SetColor("_Color", new Color(0.9f, 0.92f, 0.95f, 0.08f));
        mat.SetFloat("_SoftParticlesEnabled", 1f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
        renderer.material = mat;
        renderer.minParticleSize = 0.005f;
        renderer.maxParticleSize = 0.08f;
    }

    void Update()
    {
        if (_terrain == null || _spray == null || _mist == null) return;

        _scanTimer -= Time.deltaTime;
        if (_scanTimer > 0) return;
        _scanTimer = scanInterval;

        var cam = Camera.main;
        if (cam == null) return;

        Vector3 center = cam.transform.position;
        int sprayEmitted = 0;
        int mistEmitted = 0;

        for (int attempt = 0; attempt < 300 && (sprayEmitted < 60 || mistEmitted < 8); attempt++)
        {
            float x = center.x + Random.Range(-scanRadius, scanRadius);
            float z = center.z + Random.Range(-scanRadius, scanRadius);
            Vector3 worldPos = new Vector3(x, waterY, z);

            float terrainY = _terrain.SampleHeight(worldPos) + _terrain.transform.position.y;
            float diff = terrainY - waterY;

            // Shoreline zone
            if (diff > -0.8f && diff < 1.2f)
            {
                // SPRAY -- upward fountain droplets
                if (sprayEmitted < 60)
                {
                    var ep = new ParticleSystem.EmitParams();
                    ep.position = new Vector3(x, waterY + 0.02f, z);

                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float upSpeed = Random.Range(0.8f, 2.2f);
                    float sideSpeed = Random.Range(0.1f, 0.5f);
                    ep.velocity = new Vector3(
                        Mathf.Cos(angle) * sideSpeed,
                        upSpeed,
                        Mathf.Sin(angle) * sideSpeed);

                    ep.startSize = Random.Range(0.008f, 0.035f);
                    ep.startLifetime = Random.Range(0.2f, 0.7f);
                    ep.startColor = new Color32(
                        (byte)Random.Range(220, 245),
                        (byte)Random.Range(230, 248),
                        (byte)Random.Range(240, 255),
                        (byte)Random.Range(50, 100));

                    _spray.Emit(ep, 1);
                    sprayEmitted++;
                }

                // MIST -- soft blur clouds at waterline (less frequent)
                if (mistEmitted < 8 && Random.value < 0.15f)
                {
                    var ep = new ParticleSystem.EmitParams();
                    ep.position = new Vector3(
                        x + Random.Range(-1f, 1f),
                        waterY + Random.Range(0f, 0.3f),
                        z + Random.Range(-1f, 1f));
                    ep.velocity = new Vector3(
                        Random.Range(-0.1f, 0.1f),
                        Random.Range(0.02f, 0.08f),
                        Random.Range(-0.1f, 0.1f));
                    ep.startSize = Random.Range(0.2f, 0.5f);
                    ep.startLifetime = Random.Range(1.5f, 2.5f);
                    ep.startColor = new Color32(200, 210, 220, 15);

                    _mist.Emit(ep, 1);
                    mistEmitted++;
                }
            }
        }
    }
}
