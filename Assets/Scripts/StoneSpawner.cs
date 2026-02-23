// StoneSpawner.cs
// CYBERNOMAD -- Spawns a new grabbable stone on the table every N seconds.
// Stone appears at random position on the table surface with random size/color.
// Compound collider: SphereCollider (grab point) + cross-shaped BoxColliders (physics).
// OVRGrabbable.Awake() needs a Collider on root GO -- SphereCollider stays for that.

using UnityEngine;

public class StoneSpawner : MonoBehaviour
{
    [Tooltip("Seconds between spawns.")]
    public float interval = 20f;

    [Tooltip("Max stones in scene (including initial). 0 = unlimited.")]
    public int maxStones = 30;

    [Tooltip("Table surface Y position.")]
    public float tableY = 0.82f;

    [Tooltip("Spawn area half-extents on table (X, Z).")]
    public Vector2 spawnArea = new Vector2(0.4f, 0.2f);

    private float _timer;
    private PhysicsMaterial _stoneMat;

    void Start()
    {
        _timer = interval;
        _stoneMat = new PhysicsMaterial("SpawnedStoneMat")
        {
            dynamicFriction = 1.0f,
            staticFriction = 1.0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Maximum
        };
    }

    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = interval;

        if (maxStones > 0)
        {
            var existing = FindObjectsByType<OVRGrabbable>(FindObjectsSortMode.None);
            if (existing.Length >= maxStones) return;
        }

        SpawnStone();
    }

    void SpawnStone()
    {
        float x = Random.Range(-spawnArea.x, spawnArea.x);
        float z = Random.Range(-spawnArea.y, spawnArea.y);
        // Spawn slightly above table, let gravity settle
        Vector3 pos = transform.position + new Vector3(x, tableY - transform.position.y + 0.1f, z);

        float s = Random.Range(0.06f, 0.10f);
        float sy = s * Random.Range(0.8f, 1.0f);
        float sz = s * Random.Range(0.85f, 1.0f);

        var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        stone.name = "SpawnedStone";
        stone.transform.position = pos;
        stone.transform.localScale = new Vector3(s, sy, sz);

        // Material -- Lit gray
        float gray = Random.Range(0.30f, 0.55f);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
        {
            var r = stone.GetComponent<Renderer>();
            r.sharedMaterial = new Material(shader) { color = new Color(gray, gray - 0.03f, gray - 0.05f) };
        }

        // Keep SphereCollider (shrunk) -- OVRGrabbable.Awake() needs a Collider on root.
        // Radius 0.35 fits inside the compound BoxColliders so boxes dominate contact.
        var sphereCol = stone.GetComponent<SphereCollider>();
        sphereCol.radius = 0.35f;
        sphereCol.material = _stoneMat;

        // Cross-shaped compound collider: 2 boxes at 90 degrees
        var wideChild = new GameObject("Col_Wide");
        wideChild.transform.SetParent(stone.transform, false);
        var wideBox = wideChild.AddComponent<BoxCollider>();
        wideBox.size = new Vector3(0.9f, 0.8f, 0.5f);
        wideBox.material = _stoneMat;

        var longChild = new GameObject("Col_Long");
        longChild.transform.SetParent(stone.transform, false);
        var longBox = longChild.AddComponent<BoxCollider>();
        longBox.size = new Vector3(0.5f, 0.6f, 0.9f);
        longBox.material = _stoneMat;

        // Rigidbody
        var rb = stone.AddComponent<Rigidbody>();
        rb.mass = 1.0f;
        rb.linearDamping = 1.0f;
        rb.angularDamping = 2.0f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // OVRGrabbable -- Awake() finds SphereCollider on root, sets m_grabPoints. No crash.
        var grabbable = stone.AddComponent<OVRGrabbable>();

        // m_allowOffhandGrab via reflection (nice-to-have, not critical)
        var allowField = typeof(OVRGrabbable).GetField("m_allowOffhandGrab",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (allowField != null)
            allowField.SetValue(grabbable, true);

        // GazeThrow -- gaze-corrected throwing with boost (replaces ThrowBoost)
        var gt = stone.AddComponent<GazeThrow>();
        gt.boostMultiplier = 5.0f;

        // HitDetector -- registers hits on target zones
        stone.AddComponent<Plaga44.Gameplay.HitDetector>();

        Debug.Log("[PLAGA44] StoneSpawner: new stone spawned.");
    }
}
