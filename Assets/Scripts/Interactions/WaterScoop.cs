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
        main.maxParticles = maxDroplets;
        main.startLifetime = 1.5f;
        main.startSpeed = 0.02f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.005f, 0.015f);
        main.startColor = new Color(0.3f, 0.45f, 0.55f, 0.7f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.3f;
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

        // Splash when hand enters water fast
        if (handInWater && !gripping && !scooped)
        {
            float handSpeed = OVRInput.GetLocalControllerVelocity(
                gripAxis == OVRInput.Axis1D.PrimaryHandTrigger
                    ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch).magnitude;

            if (handSpeed > 0.5f)
            {
                var emitParams = new ParticleSystem.EmitParams();
                emitParams.startLifetime = 0.8f;
                for (int i = 0; i < 5; i++)
                {
                    emitParams.position = hand.position + Random.insideUnitSphere * 0.05f;
                    emitParams.velocity = (Vector3.up + Random.insideUnitSphere) * handSpeed * 0.3f;
                    ps.Emit(emitParams, 1);
                }
            }
        }
    }
}
