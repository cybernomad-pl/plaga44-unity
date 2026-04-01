// EnvironmentProps.cs
// CYBERNOMAD -- Spawns grabbable environment objects (rocks, sticks, debris)
// around the player. All have rigidbody + OVRGrabbable + materials.

using UnityEngine;
using System.Collections.Generic;

public class EnvironmentProps : MonoBehaviour
{
    public static int spawnCount = 40;
    public static float spawnRadius = 30f;

    private static List<GameObject> _props = new List<GameObject>();
    private static Material _rockMat;
    private static Material _stickMat;
    private static Material _debrisMat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("_EnvironmentProps");
        go.AddComponent<EnvironmentProps>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        CreateMaterials();
        Invoke(nameof(SpawnProps), 2f); // wait for terrain
    }

    static void CreateMaterials()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        // Rock -- dark gray, rough
        _rockMat = new Material(shader);
        _rockMat.name = "Rock_Runtime";
        _rockMat.SetColor("_BaseColor", new Color(0.25f, 0.24f, 0.22f));
        _rockMat.SetFloat("_Metallic", 0.05f);
        _rockMat.SetFloat("_Smoothness", 0.15f);

        // Stick -- brown wood
        _stickMat = new Material(shader);
        _stickMat.name = "Stick_Runtime";
        _stickMat.SetColor("_BaseColor", new Color(0.35f, 0.25f, 0.15f));
        _stickMat.SetFloat("_Metallic", 0f);
        _stickMat.SetFloat("_Smoothness", 0.1f);

        // Debris -- concrete gray
        _debrisMat = new Material(shader);
        _debrisMat.name = "Debris_Runtime";
        _debrisMat.SetColor("_BaseColor", new Color(0.45f, 0.43f, 0.40f));
        _debrisMat.SetFloat("_Metallic", 0.02f);
        _debrisMat.SetFloat("_Smoothness", 0.08f);
    }

    void SpawnProps()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 center = cam.transform.position;

        for (int i = 0; i < spawnCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(3f, spawnRadius);
            Vector3 pos = center + new Vector3(Mathf.Cos(angle) * dist, 100f, Mathf.Sin(angle) * dist);

            // Raycast down to terrain
            if (Physics.Raycast(pos, Vector3.down, out RaycastHit hit, 200f))
            {
                pos = hit.point + Vector3.up * 0.1f;

                // Skip if underwater
                if (pos.y < 17f) continue;

                float roll = Random.value;
                GameObject prop;
                if (roll < 0.5f)
                    prop = CreateRock(pos);
                else if (roll < 0.8f)
                    prop = CreateStick(pos);
                else
                    prop = CreateDebris(pos);

                _props.Add(prop);
            }
        }
        Debug.Log($"[PLAGA44] EnvironmentProps: spawned {_props.Count} props");
    }

    static GameObject CreateRock(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Rock";
        go.transform.position = pos;

        // Deform into rock shape
        float sx = Random.Range(0.08f, 0.25f);
        float sy = Random.Range(0.06f, 0.15f);
        float sz = Random.Range(0.08f, 0.20f);
        go.transform.localScale = new Vector3(sx, sy, sz);
        go.transform.rotation = Random.rotation;

        go.GetComponent<Renderer>().sharedMaterial = _rockMat;
        SetupGrabbable(go, sx * sy * sz * 2650f); // density of rock
        return go;
    }

    static GameObject CreateStick(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Stick";
        go.transform.position = pos;

        float length = Random.Range(0.3f, 0.8f);
        float thickness = Random.Range(0.01f, 0.03f);
        go.transform.localScale = new Vector3(thickness, length * 0.5f, thickness);
        go.transform.rotation = Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(60f, 90f));

        go.GetComponent<Renderer>().sharedMaterial = _stickMat;
        SetupGrabbable(go, 0.15f);
        return go;
    }

    static GameObject CreateDebris(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Debris";
        go.transform.position = pos;

        float sx = Random.Range(0.05f, 0.20f);
        float sy = Random.Range(0.03f, 0.10f);
        float sz = Random.Range(0.05f, 0.15f);
        go.transform.localScale = new Vector3(sx, sy, sz);
        go.transform.rotation = Random.rotation;

        go.GetComponent<Renderer>().sharedMaterial = _debrisMat;
        SetupGrabbable(go, sx * sy * sz * 2400f); // density of concrete
        return go;
    }

    static void SetupGrabbable(GameObject go, float mass)
    {
        // Rigidbody
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = Mathf.Clamp(mass, 0.05f, 5f);
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // OVRGrabbable
        var grab = go.AddComponent<OVRGrabbable>();

        // Layer for interaction
        go.layer = 0; // default
    }

    // Called from VRQualityMenu or externally
    public static void RespawnProps()
    {
        foreach (var p in _props)
            if (p != null) Destroy(p);
        _props.Clear();

        var cam = Camera.main;
        if (cam == null) return;
        var ep = FindAnyObjectByType<EnvironmentProps>();
        if (ep != null) ep.SpawnProps();
    }
}
