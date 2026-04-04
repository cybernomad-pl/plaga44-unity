// WaterScoop.cs
// CYBERNOMAD -- Scoop water with hands. When hand enters water and
// grip is held, water particle effect appears in cupped hand.
// Release grip to pour/splash.

using UnityEngine;

public class WaterScoop : MonoBehaviour
{
    [Header("Config")]
    public float waterSurfaceY = 16.8f; // from scene analysis
    public float scoopRadius = 0.15f;
    public int maxDroplets = 30;
    public float pourRate = 10f;

    private OVRCameraRig _rig;
    private ParticleSystem _leftWaterPS;
    private ParticleSystem _rightWaterPS;
    private bool _leftScooped;
    private bool _rightScooped;
    private int _leftDroplets;
    private int _rightDroplets;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
#if LOCOMOTION_ONLY
        return;
#endif
        var go = new GameObject("_WaterScoop");
        go.AddComponent<WaterScoop>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        _rig = FindAnyObjectByType<OVRCameraRig>();
        if (_rig == null) return;

        _leftWaterPS = CreateWaterParticles("LeftHandWater");
        _rightWaterPS = CreateWaterParticles("RightHandWater");

        // Detect water Y from scene
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (r.sharedMaterial != null && r.sharedMaterial.name.Contains("Water"))
            {
                waterSurfaceY = r.transform.position.y;
                break;
            }
        }
        Debug.Log($"[PLAGA44] WaterScoop: water surface Y={waterSurfaceY:F1}");
    }

    ParticleSystem CreateWaterParticles(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles = 500; // enough for big splashes
        main.startLifetime = 1.5f;
        main.startSpeed = 0.02f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.005f, 0.015f);
        main.startColor = new Color(0.3f, 0.45f, 0.55f, 0.7f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.8f; // realistic gravity on splashed water
        main.loop = true;
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.rateOverTime = 0;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.03f;

        // Renderer -- small additive droplets
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.material.SetColor("_Color", new Color(0.4f, 0.55f, 0.65f, 0.6f));
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.minParticleSize = 0.002f;
        renderer.maxParticleSize = 0.02f;

        ps.Stop();
        return ps;
    }

    void Update()
    {
        if (_rig == null) return;

        UpdateHand(_rig.leftHandAnchor,
            OVRInput.Axis1D.PrimaryHandTrigger,
            ref _leftScooped, ref _leftDroplets, _leftWaterPS);

        UpdateHand(_rig.rightHandAnchor,
            OVRInput.Axis1D.SecondaryHandTrigger,
            ref _rightScooped, ref _rightDroplets, _rightWaterPS);
    }

    void UpdateHand(Transform hand, OVRInput.Axis1D gripAxis,
        ref bool scooped, ref int droplets, ParticleSystem ps)
    {
        if (hand == null || ps == null) return;

        float grip = OVRInput.Get(gripAxis);
        bool handInWater = hand.position.y < waterSurfaceY + 0.05f
                        && hand.position.y > waterSurfaceY - 0.3f;
        bool gripping = grip > 0.7f;

        // Scoop: hand in water + grip
        if (handInWater && gripping && !scooped)
        {
            scooped = true;
            droplets = maxDroplets;
            Debug.Log("[PLAGA44] Water scooped!");
        }

        // Hold water: show particles around hand while gripping
        if (scooped && gripping && droplets > 0)
        {
            ps.transform.position = hand.position + Vector3.up * 0.02f;

            if (!ps.isPlaying) ps.Play();

            // Emit a few particles to show water in hand
            var emitParams = new ParticleSystem.EmitParams();
            emitParams.position = hand.position + Random.insideUnitSphere * 0.02f;
            emitParams.velocity = Vector3.zero;
            emitParams.startLifetime = 0.5f;
            ps.Emit(emitParams, 1);

            // Slowly lose water through fingers
            if (Random.value < 0.02f) droplets--;
        }

        // Pour: release grip while holding water
        if (scooped && !gripping && droplets > 0)
        {
            // Pour particles downward
            ps.transform.position = hand.position;
            var emitParams = new ParticleSystem.EmitParams();
            emitParams.velocity = Vector3.down * 0.5f;
            emitParams.startLifetime = 1.5f;

            int pourCount = Mathf.Min(Mathf.CeilToInt(pourRate * Time.deltaTime), droplets);
            for (int i = 0; i < pourCount; i++)
            {
                emitParams.position = hand.position + Random.insideUnitSphere * 0.03f;
                ps.Emit(emitParams, 1);
                droplets--;
            }

            if (droplets <= 0)
            {
                scooped = false;
                ps.Stop();
                Debug.Log("[PLAGA44] Water poured out.");
            }
        }

        // Lost all water
        if (scooped && droplets <= 0)
        {
            scooped = false;
            ps.Stop();
        }

        // SPLASH -- hand enters water fast
        if (handInWater && !scooped)
        {
            var ctrl = gripAxis == OVRInput.Axis1D.PrimaryHandTrigger
                ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
            Vector3 handVel = OVRInput.GetLocalControllerVelocity(ctrl);
            float speed = handVel.magnitude;

            if (speed > 0.3f)
            {
                // Scale splash intensity with hand speed
                float intensity = Mathf.Clamp01((speed - 0.3f) / 2f);
                int splashCount = Mathf.CeilToInt(Mathf.Lerp(5, 60, intensity));
                float splashForce = Mathf.Lerp(0.3f, 2.5f, intensity);
                float splashRadius = Mathf.Lerp(0.03f, 0.15f, intensity);

                Vector3 surfacePoint = new Vector3(hand.position.x, waterSurfaceY, hand.position.z);

                for (int i = 0; i < splashCount; i++)
                {
                    var ep = new ParticleSystem.EmitParams();

                    // Big upward crown splash
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float outward = Random.Range(0.3f, 1f);
                    Vector3 dir = new Vector3(
                        Mathf.Cos(angle) * outward,
                        Random.Range(1.5f, 4f),  // strong upward bias
                        Mathf.Sin(angle) * outward
                    ).normalized;

                    ep.position = surfacePoint + Random.insideUnitSphere * splashRadius;
                    ep.velocity = dir * splashForce * Random.Range(0.5f, 1.2f);

                    // Varied sizes -- some big drops, mostly small mist
                    float sizeRoll = Random.value;
                    if (sizeRoll < 0.1f)
                        ep.startSize = Random.Range(0.02f, 0.04f);  // big drops
                    else if (sizeRoll < 0.4f)
                        ep.startSize = Random.Range(0.008f, 0.02f); // medium
                    else
                        ep.startSize = Random.Range(0.002f, 0.008f); // fine mist

                    ep.startLifetime = Random.Range(0.4f, 1.2f);

                    // Color variation -- white foam to blue-green water
                    float colorRoll = Random.value;
                    if (colorRoll < 0.3f)
                        ep.startColor = new Color(0.85f, 0.9f, 0.95f, 0.8f); // white foam
                    else
                        ep.startColor = new Color(
                            Random.Range(0.3f, 0.5f),
                            Random.Range(0.45f, 0.65f),
                            Random.Range(0.55f, 0.75f),
                            Random.Range(0.4f, 0.8f));

                    ps.Emit(ep, 1);
                }

                // Secondary ring of outward spray at water surface
                if (intensity > 0.3f)
                {
                    int ringCount = Mathf.CeilToInt(20 * intensity);
                    for (int i2 = 0; i2 < ringCount; i2++)
                    {
                        float a = (float)i2 / ringCount * Mathf.PI * 2f;
                        var ep = new ParticleSystem.EmitParams();
                        ep.position = surfacePoint;
                        ep.velocity = new Vector3(
                            Mathf.Cos(a) * splashForce * 0.6f,
                            Random.Range(0.1f, 0.5f),
                            Mathf.Sin(a) * splashForce * 0.6f);
                        ep.startSize = Random.Range(0.005f, 0.015f);
                        ep.startLifetime = Random.Range(0.3f, 0.8f);
                        ep.startColor = new Color(0.7f, 0.8f, 0.85f, 0.5f);
                        ps.Emit(ep, 1);
                    }
                }

                // Ripple: flat disc particles at surface (visual only)
                if (intensity > 0.2f)
                {
                    int rippleCount = 3;
                    for (int r = 0; r < rippleCount; r++)
                    {
                        var ep = new ParticleSystem.EmitParams();
                        ep.position = surfacePoint + Vector3.up * 0.01f;
                        ep.velocity = Vector3.up * 0.01f; // nearly static
                        ep.startSize = Random.Range(0.05f, 0.15f) * (1 + intensity);
                        ep.startLifetime = Random.Range(0.5f, 1.5f);
                        ep.startColor = new Color(0.6f, 0.7f, 0.75f, 0.15f); // very faint
                        ps.Emit(ep, 1);
                    }
                }
            }
        }

        // Dripping when hand leaves water
        if (!handInWater && hand.position.y > waterSurfaceY && hand.position.y < waterSurfaceY + 0.4f && !scooped)
        {
            // Occasional drip from wet hand
            if (Random.value < 0.05f)
            {
                var ep = new ParticleSystem.EmitParams();
                ep.position = hand.position + Vector3.down * 0.05f + Random.insideUnitSphere * 0.02f;
                ep.velocity = Vector3.down * Random.Range(0.1f, 0.4f);
                ep.startSize = Random.Range(0.003f, 0.008f);
                ep.startLifetime = 0.8f;
                ep.startColor = new Color(0.4f, 0.55f, 0.65f, 0.6f);
                ps.Emit(ep, 1);
            }
        }
    }
}
