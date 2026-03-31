using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// VR Item Spawner -- runtime debug menu on LEFT controller.
/// LEFT STICK up/down = select, left/right = scale
/// LEFT TRIGGER = spawn in front of player (with Rigidbody + OVRGrabbable)
/// X = toggle menu, Y = delete last
///
/// Loads prefabs from Resources/SpawnItems/ at startup.
/// Blocks player movement when menu is open.
/// </summary>
public class VRItemSpawner : MonoBehaviour
{
    public static bool MenuOpen { get; private set; } = false;
    public static VRItemSpawner Instance { get; private set; }

    private GameObject _canvas;
    private Text _titleText;
    private Text[] _rowTexts;
    private bool _visible = false;
    private int _selectedRow = 0;
    private float _inputCooldown = 0;
    private float _spawnScale = 1f;
    private float _spawnDistance = 2f;
    private List<GameObject> _prefabs = new List<GameObject>();
    private List<GameObject> _spawnedObjects = new List<GameObject>();

    private int TotalRows => _prefabs.Count + 3;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("_VRItemSpawner");
        Instance = go.AddComponent<VRItemSpawner>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        LoadPrefabs();
        CreateWorldCanvas();
        _canvas.SetActive(false);
        Debug.Log($"[PLAGA44] VRItemSpawner: {_prefabs.Count} items loaded");
    }

    void LoadPrefabs()
    {
        _prefabs.Clear();
        var loaded = Resources.LoadAll<GameObject>("SpawnItems");
        if (loaded != null)
        {
            foreach (var p in loaded)
                _prefabs.Add(p);
        }

        // Sort: weapons by name
        _prefabs.Sort((a, b) => string.Compare(a.name, b.name));

        if (_prefabs.Count == 0)
            Debug.LogWarning("[PLAGA44] VRItemSpawner: No prefabs in Resources/SpawnItems/");
    }

    void CreateWorldCanvas()
    {
        int rowCount = TotalRows;

        _canvas = new GameObject("ItemSpawnerCanvas");
        _canvas.transform.SetParent(transform);
        var canvas = _canvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 101;
        _canvas.AddComponent<CanvasScaler>();

        var rt = _canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(450, 60 + rowCount * 28);
        rt.localScale = Vector3.one * 0.0008f;

        var bg = new GameObject("BG");
        bg.transform.SetParent(_canvas.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.02f, 0.10f, 0.93f);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        var titleGo = MakeText(bg.transform, "", 20, new Color(1f, 0.6f, 0.2f),
            new Vector2(10, -5), new Vector2(430, 28));
        _titleText = titleGo.GetComponent<Text>();

        _rowTexts = new Text[rowCount];
        for (int i = 0; i < rowCount; i++)
        {
            float y = -33 - i * 28;
            var go = MakeText(bg.transform, "", 18, Color.white, new Vector2(10, y), new Vector2(430, 26));
            _rowTexts[i] = go.GetComponent<Text>();
        }

        MakeText(bg.transform,
            "L.STICK ^v select <> scale | L.TRIG spawn | [X] menu [Y] undo",
            12, new Color(0.5f, 0.5f, 0.5f),
            new Vector2(10, -33 - rowCount * 28), new Vector2(430, 22));
    }

    GameObject MakeText(Transform parent, string txt, int size, Color col, Vector2 pos, Vector2 sz)
    {
        var go = new GameObject("T");
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = pos;
        r.sizeDelta = sz;
        var t = go.AddComponent<Text>();
        t.text = txt;
        t.fontSize = size;
        t.color = col;
        t.font = Font.CreateDynamicFontFromOSFont("Arial", size);
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return go;
    }

    void Update()
    {
        // X = toggle menu
        if (OVRInput.GetDown(OVRInput.Button.Three))
        {
            _visible = !_visible;
            _canvas.SetActive(_visible);
            MenuOpen = _visible;
            BlockPlayerMovement(_visible);
        }

        // Y = quick delete last (always available)
        if (OVRInput.GetDown(OVRInput.Button.Four))
            DeleteLastSpawned();

        if (!_visible) return;

        PositionCanvas();

        _inputCooldown -= Time.unscaledDeltaTime;
        if (_inputCooldown > 0) return;

        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

        if (stick.y > 0.5f)
        {
            _selectedRow = (_selectedRow - 1 + TotalRows) % TotalRows;
            _inputCooldown = 0.18f;
        }
        else if (stick.y < -0.5f)
        {
            _selectedRow = (_selectedRow + 1) % TotalRows;
            _inputCooldown = 0.18f;
        }

        if (stick.x > 0.5f)
        {
            _spawnScale = Mathf.Min(_spawnScale * 1.2f, 100f);
            _inputCooldown = 0.15f;
        }
        else if (stick.x < -0.5f)
        {
            _spawnScale = Mathf.Max(_spawnScale / 1.2f, 0.01f);
            _inputCooldown = 0.15f;
        }

        if (OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger) > 0.5f)
        {
            Execute(_selectedRow);
            _inputCooldown = 0.3f;
        }

        UpdateDisplay();
    }

    void BlockPlayerMovement(bool block)
    {
        var pc = FindAnyObjectByType<OVRPlayerController>();
        if (pc != null)
            pc.EnableLinearMovement = !block;
    }

    void PositionCanvas()
    {
        var rig = FindAnyObjectByType<OVRCameraRig>();
        Transform anchor = rig != null ? rig.leftHandAnchor : null;

        if (anchor != null)
        {
            Vector3 target = anchor.position + anchor.forward * 0.25f + anchor.up * 0.1f;
            _canvas.transform.position = Vector3.Lerp(_canvas.transform.position, target, Time.deltaTime * 8f);
        }
        else
        {
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 target = cam.transform.position + cam.transform.forward * 1f - cam.transform.right * 0.4f;
                _canvas.transform.position = Vector3.Lerp(_canvas.transform.position, target, Time.deltaTime * 4f);
            }
        }

        var camLook = Camera.main;
        if (camLook != null)
        {
            _canvas.transform.rotation = Quaternion.Slerp(_canvas.transform.rotation,
                Quaternion.LookRotation(_canvas.transform.position - camLook.transform.position),
                Time.deltaTime * 8f);
        }
    }

    void Execute(int row)
    {
        if (row < _prefabs.Count)
        {
            SpawnItem(_prefabs[row]);
        }
        else
        {
            int action = row - _prefabs.Count;
            if (action == 0) _spawnScale = 1f;
            else if (action == 1) DeleteLastSpawned();
            else if (action == 2) DeleteAllSpawned();
        }
    }

    void SpawnItem(GameObject source)
    {
        if (source == null) return;

        var cam = Camera.main;
        Vector3 pos = cam != null
            ? cam.transform.position + cam.transform.forward * _spawnDistance
            : Vector3.forward * _spawnDistance;
        Quaternion rot = cam != null
            ? Quaternion.LookRotation(cam.transform.forward, Vector3.up)
            : Quaternion.identity;

        var instance = Instantiate(source, pos, rot);
        instance.name = $"{source.name}_spawned_{_spawnedObjects.Count}";
        instance.transform.localScale = Vector3.one * _spawnScale;
        instance.SetActive(true);

        var rb = instance.GetComponent<Rigidbody>();
        if (rb == null)
            rb = instance.AddComponent<Rigidbody>();
        rb.mass = EstimateMass(source.name);
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (instance.GetComponent<Collider>() == null &&
            instance.GetComponentInChildren<Collider>() == null)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                var box = instance.AddComponent<BoxCollider>();
                box.center = instance.transform.InverseTransformPoint(bounds.center);
                box.size = new Vector3(
                    Mathf.Abs(instance.transform.InverseTransformVector(bounds.size).x),
                    Mathf.Abs(instance.transform.InverseTransformVector(bounds.size).y),
                    Mathf.Abs(instance.transform.InverseTransformVector(bounds.size).z));
            }
        }

        if (instance.GetComponent<OVRGrabbable>() == null)
            instance.AddComponent<OVRGrabbable>();

        // M249: full handler (two-handed grip, bipod, orientation, material)
        if (source.name.Contains("M249"))
        {
            var grab = instance.GetComponent<OVRGrabbable>();
            if (grab != null) M249GripFix.FixGrip(grab);
            if (instance.GetComponent<M249Handler>() == null)
                instance.AddComponent<M249Handler>();
            M249MaterialSetup.ApplyToWeapon(instance);
        }

        _spawnedObjects.Add(instance);
        Debug.Log($"[PLAGA44] VRItemSpawner: Spawned '{source.name}' scale:{_spawnScale:F2}");
    }

    float EstimateMass(string name)
    {
        string lower = name.ToLower();
        if (lower.Contains("m249")) return 7.5f;
        if (lower.Contains("rifle") || lower.Contains("scifi")) return 4f;
        if (lower.Contains("gun") || lower.Contains("pistol")) return 1.2f;
        if (lower.Contains("sword") || lower.Contains("katana")) return 1.5f;
        return 2f;
    }

    void DeleteLastSpawned()
    {
        while (_spawnedObjects.Count > 0)
        {
            var last = _spawnedObjects[_spawnedObjects.Count - 1];
            _spawnedObjects.RemoveAt(_spawnedObjects.Count - 1);
            if (last != null)
            {
                Destroy(last);
                return;
            }
        }
    }

    void DeleteAllSpawned()
    {
        int count = 0;
        foreach (var obj in _spawnedObjects)
            if (obj != null) { Destroy(obj); count++; }
        _spawnedObjects.Clear();
    }

    void UpdateDisplay()
    {
        _titleText.text = $"<color=#FF9933>ITEM SPAWNER</color>  x{_spawnScale:F2}  [{_spawnedObjects.Count}]";

        for (int i = 0; i < _rowTexts.Length && i < TotalRows; i++)
        {
            bool sel = (i == _selectedRow);
            string arrow = sel ? ">>  " : "    ";

            if (i < _prefabs.Count)
            {
                var p = _prefabs[i];
                string name = p != null ? p.name : "(null)";
                string c = sel ? "#ff9933" : "#cccccc";
                _rowTexts[i].text = $"<color={c}>{arrow}{name}</color>";
            }
            else
            {
                int action = i - _prefabs.Count;
                string label = action switch
                {
                    0 => "[RESET SCALE]",
                    1 => "[DELETE LAST]",
                    2 => "[DELETE ALL]",
                    _ => ""
                };
                string ac = sel ? "#ff4444" : "#666666";
                _rowTexts[i].text = $"<color={ac}>{arrow}{label}</color>";
            }
        }
    }
}
