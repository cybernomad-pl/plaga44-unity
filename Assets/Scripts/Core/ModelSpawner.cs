using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ModelSpawner -- runtime model spawner z VR.
/// Thumbstick RIGHT UP/DOWN = przelaczaj modele
/// Thumbstick RIGHT LEFT/RIGHT = skaluj
/// RIGHT TRIGGER = spawn przed soba
/// Dziala z gry, nie z edytora.
///
/// Wszystkie dostepne modele ladowane z Resources/PLAGA44/
/// </summary>
public class ModelSpawner : MonoBehaviour
{
    [Header("Config")]
    public float spawnDistance = 2f;
    public float scaleStep = 0.1f;
    public float minScale = 0.1f;
    public float maxScale = 10f;

    [Header("State")]
    public int selectedIndex;
    public float currentScale = 1f;
    public string selectedName = "";

    private List<string> _modelPaths = new List<string>();
    private List<string> _modelNames = new List<string>();
    private float _scrollCooldown;
    private List<GameObject> _spawned = new List<GameObject>();

    // HUD
    private GameObject _hudCanvas;
    private UnityEngine.UI.Text _hudText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("_ModelSpawner");
        go.AddComponent<ModelSpawner>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        Debug.Log("[SPAWNER] ModelSpawner.Start() -- loading models from Resources/PLAGA44/...");

        // Load all models from Resources
        var allAssets = Resources.LoadAll<GameObject>("PLAGA44");
        Debug.Log($"[SPAWNER] Resources.LoadAll found {allAssets.Length} GameObjects total");

        foreach (var m in allAssets)
        {
            bool hasMesh = m.GetComponentInChildren<MeshFilter>() != null;
            bool hasSkinned = m.GetComponentInChildren<SkinnedMeshRenderer>() != null;
            Debug.Log($"[SPAWNER]   checking: {m.name} mesh={hasMesh} skinned={hasSkinned}");

            if (!hasMesh && !hasSkinned) continue;

            string path = GetResourcePath(m);
            _modelPaths.Add(path);
            _modelNames.Add(m.name);
            Debug.Log($"[SPAWNER]   ADDED: {m.name} -> {path}");
        }

        if (_modelPaths.Count > 0)
            selectedName = _modelNames[0];
        else
            Debug.LogWarning("[SPAWNER] NO MODELS FOUND IN RESOURCES!");

        CreateHUD();
        Debug.Log($"[SPAWNER] READY: {_modelPaths.Count} models. RIGHT stick=browse/scale, RIGHT trigger=spawn, Y=delete.");
    }

    string GetResourcePath(GameObject obj)
    {
        // Resources.Load needs path relative to Resources/ without extension
        // We loaded with LoadAll so we need to find it
        string[] guesses = {
            "PLAGA44/Characters/PINEA/" + obj.name,
            "PLAGA44/Models/" + obj.name,
            "PLAGA44/" + obj.name,
        };
        foreach (var g in guesses)
        {
            if (Resources.Load<GameObject>(g) != null) return g;
        }
        return obj.name;
    }

    void Update()
    {
        if (_modelPaths.Count == 0) return;
        if (VRQualityMenu.MenuOpen) return;

        _scrollCooldown -= Time.deltaTime;

        // RIGHT STICK UP/DOWN = browse models
        Vector2 rightStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);

        if (_scrollCooldown <= 0f)
        {
            if (rightStick.y > 0.6f)
            {
                selectedIndex = (selectedIndex + 1) % _modelPaths.Count;
                selectedName = _modelNames[selectedIndex];
                _scrollCooldown = 0.3f;
                UpdateHUD();
            }
            else if (rightStick.y < -0.6f)
            {
                selectedIndex = (selectedIndex - 1 + _modelPaths.Count) % _modelPaths.Count;
                selectedName = _modelNames[selectedIndex];
                _scrollCooldown = 0.3f;
                UpdateHUD();
            }

            // RIGHT STICK LEFT/RIGHT = scale
            if (rightStick.x > 0.6f)
            {
                currentScale = Mathf.Min(currentScale + scaleStep, maxScale);
                _scrollCooldown = 0.15f;
                UpdateHUD();
            }
            else if (rightStick.x < -0.6f)
            {
                currentScale = Mathf.Max(currentScale - scaleStep, minScale);
                _scrollCooldown = 0.15f;
                UpdateHUD();
            }
        }

        // RIGHT INDEX TRIGGER = spawn
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            Debug.Log($"[SPAWNER] RIGHT TRIGGER pressed -- spawning '{selectedName}' (idx={selectedIndex})");
            SpawnSelected();
        }

        // Y BUTTON = delete last spawned (with debounce)
        if (OVRInput.GetDown(OVRInput.Button.Four) && _scrollCooldown <= 0f) // Y
        {
            Debug.Log($"[SPAWNER] Y pressed -- deleting last ({_spawned.Count} total)");
            DeleteLast();
            _scrollCooldown = 0.5f; // debounce 500ms
        }
    }

    void SpawnSelected()
    {
        var prefab = Resources.Load<GameObject>(_modelPaths[selectedIndex]);
        if (prefab == null)
        {
            Debug.LogError($"[PLAGA44] ModelSpawner: failed to load '{_modelPaths[selectedIndex]}'");
            return;
        }

        // Spawn in front of player
        var cam = Camera.main;
        Vector3 pos = cam.transform.position + cam.transform.forward * spawnDistance;

        // Raycast down for ground
        if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 50f))
            pos.y = hit.point.y;

        var instance = Instantiate(prefab, pos, Quaternion.identity);
        instance.name = $"{selectedName}_spawned";
        instance.transform.localScale = Vector3.one * currentScale;

        // Make grabbable
        var rb = instance.GetComponent<Rigidbody>();
        if (rb == null) rb = instance.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        if (instance.GetComponent<Collider>() == null)
        {
            var box = instance.AddComponent<BoxCollider>();
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                box.center = instance.transform.InverseTransformPoint(bounds.center);
                box.size = instance.transform.InverseTransformVector(bounds.size);
            }
        }

        var grabbable = instance.GetComponent<OVRGrabbable>();
        if (grabbable == null) instance.AddComponent<OVRGrabbable>();

        _spawned.Add(instance);

        // Haptic
        OVRInput.SetControllerVibration(0.5f, 0.5f, OVRInput.Controller.RTouch);
        Invoke(nameof(StopHaptic), 0.1f);

        Debug.Log($"[PLAGA44] Spawned: {selectedName} scale={currentScale:F1} at {pos}");
        UpdateHUD();
    }

    void DeleteLast()
    {
        if (_spawned.Count == 0) return;
        var last = _spawned[_spawned.Count - 1];
        _spawned.RemoveAt(_spawned.Count - 1);
        if (last != null) Destroy(last);
        Debug.Log("[PLAGA44] Deleted last spawned model");
        UpdateHUD();
    }

    void StopHaptic()
    {
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
    }

    // =========================================================================
    // HUD -- small floating text near left wrist
    // =========================================================================

    void CreateHUD()
    {
        _hudCanvas = new GameObject("ModelSpawnerHUD");
        _hudCanvas.transform.SetParent(transform);
        var canvas = _hudCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        var rt = _hudCanvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0.2f, 0.08f);
        rt.localScale = Vector3.one * 0.001f;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(_hudCanvas.transform);
        _hudText = textGO.AddComponent<UnityEngine.UI.Text>();
        _hudText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _hudText.fontSize = 24;
        _hudText.color = Color.white;
        _hudText.alignment = TextAnchor.MiddleCenter;
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        // Background
        var bg = _hudCanvas.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0, 0, 0, 0.7f);

        UpdateHUD();
    }

    void UpdateHUD()
    {
        if (_hudText == null) return;
        _hudText.text = $"{selectedName}\nScale: {currentScale:F1}x  [{_spawned.Count} spawned]";
    }

    void LateUpdate()
    {
        // HUD follows left wrist
        if (_hudCanvas == null) return;
        var rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig != null && rig.leftHandAnchor != null)
        {
            _hudCanvas.transform.position = rig.leftHandAnchor.position + rig.leftHandAnchor.up * 0.1f;
            _hudCanvas.transform.rotation = Quaternion.LookRotation(
                _hudCanvas.transform.position - Camera.main.transform.position);
        }
    }
}
