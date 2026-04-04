// LaserInspector.cs
// PLAGA '44 -- Debug laser pointer that inspects and edits material properties
// of any object in the scene. Point right controller at object, pull trigger
// to open a world-space settings panel near the object.
//
// Object type detection:
//   Terrain  -- terrain layers (tile size, normal scale, metallic, smoothness)
//   Water    -- water shader properties (color, metallic, waves, foam, etc.)
//   Skybox   -- triggered when laser hits nothing (sky); skybox material props
//   NPC      -- character material sliders
//   Generic  -- standard material: color, metallic, smoothness, emission
//
// Navigation: Left stick up/down = select row, left/right = adjust value
// Close: B button (Button.Two) or trigger on empty space while panel is open
//
// Requires: com.meta.xr.sdk.core (HAS_META_XR define)

using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace Plaga44.UI
{
    public class LaserInspector : MonoBehaviour
    {
        // ── Public state for other systems ──
        public static bool IsOpen { get; private set; }

        // ── Config ──
        private const float MAX_DISTANCE = 50f;
        private const int VISIBLE_ROWS = 14;
        private const float INPUT_COOLDOWN_NAV = 0.18f;
        private const float INPUT_COOLDOWN_ADJ = 0.08f;

        // ── Internal ──
        private LineRenderer _laser;
        private Transform _controllerAnchor;
        private GameObject _panelGO;
        private Text[] _rowTexts;
        private Text _titleText;
        private Text _fpsText;
        private List<Setting> _settings = new List<Setting>();
        private int _selectedRow;
        private int _scrollOffset;
        private float _inputCooldown;
        private GameObject _highlightedGO;
        private Material _outlineMat; // cached for highlight effect
        private Color _originalOutlineColor;
        private Vector3 _panelWorldPos;
        private string _inspectedName;

        // Raycast hit cache
        private RaycastHit _lastHit;
        private bool _lastHitValid;

#if HAS_META_XR
        private OVRCameraRig _rig;
#endif

        // ── Setting class (same pattern as VRQualityMenu) ──

        class Setting
        {
            public string name;
            public Func<float> get;
            public Action<float> set;
            public float min, max, step;
            public string format;
            public bool isHeader => step == 0 && name.StartsWith("---");

            public Setting(string n, Func<float> g, Action<float> s,
                float mn, float mx, float st, string fmt = "F2")
            {
                name = n; get = g; set = s; min = mn; max = mx; step = st; format = fmt;
            }
        }

        // ── Auto-create ──

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
#if LOCOMOTION_ONLY
        return;
#endif
            var go = new GameObject("_LaserInspector");
            go.AddComponent<LaserInspector>();
            DontDestroyOnLoad(go);
        }

        // ── Lifecycle ──

        void Start()
        {
            FindController();
            SetupLaser();
        }

        void Update()
        {
            if (_controllerAnchor == null)
            {
                FindController();
                if (_controllerAnchor == null) return;
            }

            // Don't run while any menu is open
            if (VRMenuManager.MenuOpen || VRQualityMenu.MenuOpen)
            {
                HideLaser();
                return;
            }

            // Close panel with B button
#if HAS_META_XR
            if (IsOpen && OVRInput.GetDown(OVRInput.Button.Two))
            {
                ClosePanel();
                return;
            }
#endif

            if (IsOpen)
            {
                UpdatePanelInput();
                UpdatePanelDisplay();
                PositionPanel();
                // Keep laser pointing but shorter
                DrawLaser(_controllerAnchor.position,
                    _controllerAnchor.position + _controllerAnchor.forward * 0.3f);
                return;
            }

            // Raycast
            Ray ray = new Ray(_controllerAnchor.position, _controllerAnchor.forward);
            _lastHitValid = Physics.Raycast(ray, out _lastHit, MAX_DISTANCE);

            if (_lastHitValid)
            {
                DrawLaser(_controllerAnchor.position, _lastHit.point);
            }
            else
            {
                DrawLaser(_controllerAnchor.position,
                    _controllerAnchor.position + _controllerAnchor.forward * MAX_DISTANCE);
            }

            // Trigger to inspect
            bool triggerDown = false;
#if HAS_META_XR
            triggerDown = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
#else
            triggerDown = UnityEngine.Input.GetMouseButtonDown(0); // Editor fallback: left click
#endif
            if (triggerDown)
            {
                if (_lastHitValid)
                {
                    OpenPanel(_lastHit);
                }
                else
                {
                    // Hit nothing = inspect skybox
                    OpenSkyboxPanel();
                }
            }
        }

        void OnDisable()
        {
            HideLaser();
            if (IsOpen) ClosePanel();
        }

        // ── Controller discovery ──

        void FindController()
        {
#if HAS_META_XR
            if (_rig == null)
                _rig = FindFirstObjectByType<OVRCameraRig>();
            if (_rig != null)
            {
                _controllerAnchor = _rig.rightControllerAnchor;
                if (_laser != null)
                    _laser.transform.SetParent(_controllerAnchor, false);
            }
#else
            // Editor fallback
            var cam = Camera.main;
            if (cam != null) _controllerAnchor = cam.transform;
#endif
        }

        // ── Laser ──

        void SetupLaser()
        {
            var laserGO = new GameObject("LaserLine");
            laserGO.transform.SetParent(transform, false);
            _laser = laserGO.AddComponent<LineRenderer>();
            _laser.positionCount = 2;
            _laser.startWidth = 0.005f;
            _laser.endWidth = 0.005f;
            _laser.material = new Material(Shader.Find("Sprites/Default"));
            _laser.startColor = new Color(1f, 0.2f, 0.2f, 1f);
            _laser.endColor = new Color(1f, 0.4f, 0.4f, 0.8f);
            _laser.useWorldSpace = true;
            _laser.enabled = false;
        }

        void DrawLaser(Vector3 from, Vector3 to)
        {
            _laser.enabled = true;
            _laser.SetPosition(0, from);
            _laser.SetPosition(1, to);
        }

        void HideLaser()
        {
            if (_laser != null) _laser.enabled = false;
        }

        // ── Detect object type and open panel ──

        enum InspectedType { Generic, Terrain, Water, NPC }

        InspectedType ClassifyHit(RaycastHit hit)
        {
            var go = hit.collider.gameObject;

            // Terrain
            if (hit.collider is TerrainCollider || go.GetComponent<Terrain>() != null)
                return InspectedType.Terrain;

            // Water -- check material/shader name
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                foreach (var m in renderer.sharedMaterials)
                {
                    if (m != null && (m.name.ToLower().Contains("water") ||
                        (m.shader != null && m.shader.name.ToLower().Contains("water"))))
                        return InspectedType.Water;
                }
            }

            // NPC -- tag or hierarchy name
            try { if (go.CompareTag("NPC")) return InspectedType.NPC; } catch { }
            Transform t = go.transform;
            while (t != null)
            {
                string n = t.name.ToLower();
                if (n.Contains("npc") || n.Contains("soldier") || n.Contains("character")
                    || n.Contains("enemy") || n.Contains("klaszczur") || n.Contains("pinea"))
                    return InspectedType.NPC;
                t = t.parent;
            }

            return InspectedType.Generic;
        }

        void OpenPanel(RaycastHit hit)
        {
            _settings.Clear();
            _selectedRow = 0;
            _scrollOffset = 0;
            _highlightedGO = hit.collider.gameObject;
            _panelWorldPos = hit.point + hit.normal * 0.15f;

            var type = ClassifyHit(hit);
            _inspectedName = $"{_highlightedGO.name} [{type}]";

            switch (type)
            {
                case InspectedType.Terrain:
                    BuildTerrainSettings(hit);
                    break;
                case InspectedType.Water:
                    BuildWaterSettings(_highlightedGO);
                    break;
                case InspectedType.NPC:
                    BuildNPCSettings(_highlightedGO);
                    break;
                default:
                    BuildGenericSettings(_highlightedGO);
                    break;
            }

            if (_settings.Count == 0)
            {
                _settings.Add(new Setting("--- NO EDITABLE PROPERTIES ---", () => 0, v => { }, 0, 0, 0));
            }

            // Skip to first editable row
            _selectedRow = 0;
            while (_selectedRow < _settings.Count && _settings[_selectedRow].isHeader)
                _selectedRow++;
            if (_selectedRow >= _settings.Count) _selectedRow = 0;

            BuildPanelCanvas();
            _panelGO.SetActive(true);
            IsOpen = true;

            Debug.Log($"[LASER INSPECTOR] Opened: {_inspectedName} ({_settings.Count} settings)");
        }

        void OpenSkyboxPanel()
        {
            _settings.Clear();
            _selectedRow = 0;
            _scrollOffset = 0;
            _highlightedGO = null;
            _inspectedName = "SKYBOX";

            var cam = Camera.main;
            if (cam != null)
                _panelWorldPos = cam.transform.position + cam.transform.forward * 1.5f;

            BuildSkyboxSettings();

            if (_settings.Count == 0)
            {
                _settings.Add(new Setting("--- NO SKYBOX MATERIAL ---", () => 0, v => { }, 0, 0, 0));
            }

            _selectedRow = 0;
            while (_selectedRow < _settings.Count && _settings[_selectedRow].isHeader)
                _selectedRow++;
            if (_selectedRow >= _settings.Count) _selectedRow = 0;

            BuildPanelCanvas();
            _panelGO.SetActive(true);
            IsOpen = true;

            Debug.Log("[LASER INSPECTOR] Opened: SKYBOX");
        }

        void ClosePanel()
        {
            IsOpen = false;
            if (_panelGO != null)
            {
                Destroy(_panelGO);
                _panelGO = null;
            }
            _settings.Clear();
            _highlightedGO = null;
            Debug.Log("[LASER INSPECTOR] Panel closed");
        }

        // ── Settings builders ──

        void BuildTerrainSettings(RaycastHit hit)
        {
            var terrain = hit.collider.GetComponent<Terrain>();
            if (terrain == null) terrain = hit.collider.GetComponentInParent<Terrain>();
            if (terrain == null) terrain = FindAnyObjectByType<Terrain>();
            if (terrain == null) return;

            // Terrain material
            var mat = terrain.materialTemplate;
            if (mat != null)
            {
                _settings.Add(new Setting("--- TERRAIN MATERIAL ---", () => 0, v => { }, 0, 0, 0));

                if (mat.HasFloat("_BumpScale"))
                    _settings.Add(new Setting("Normal Scale",
                        () => mat.GetFloat("_BumpScale"),
                        v => mat.SetFloat("_BumpScale", v),
                        0, 3, 0.01f, "F3"));

                if (mat.HasFloat("_Smoothness"))
                    _settings.Add(new Setting("Smoothness",
                        () => mat.GetFloat("_Smoothness"),
                        v => mat.SetFloat("_Smoothness", v),
                        0, 1, 0.01f, "F3"));

                if (mat.HasFloat("_Metallic"))
                    _settings.Add(new Setting("Metallic",
                        () => mat.GetFloat("_Metallic"),
                        v => mat.SetFloat("_Metallic", v),
                        0, 1, 0.01f, "F3"));
            }

            // Terrain layers
            if (terrain.terrainData != null && terrain.terrainData.terrainLayers != null)
            {
                var layers = terrain.terrainData.terrainLayers;
                for (int li = 0; li < Mathf.Min(layers.Length, 6); li++)
                {
                    var layer = layers[li];
                    if (layer == null) continue;
                    int idx = li;
                    string layerName = layer.diffuseTexture != null ? layer.diffuseTexture.name : $"Layer{li}";

                    _settings.Add(new Setting($"--- {layerName} ---", () => 0, v => { }, 0, 0, 0));

                    _settings.Add(new Setting($"  NormalScale",
                        () => layers[idx].normalScale,
                        v => layers[idx].normalScale = v,
                        0, 3, 0.01f, "F3"));

                    _settings.Add(new Setting($"  TileSize",
                        () => layers[idx].tileSize.x,
                        v => layers[idx].tileSize = new Vector2(v, v),
                        1, 100, 0.5f, "F1"));

                    _settings.Add(new Setting($"  Metallic",
                        () => layers[idx].metallic,
                        v => layers[idx].metallic = v,
                        0, 1, 0.01f, "F3"));

                    _settings.Add(new Setting($"  Smoothness",
                        () => layers[idx].smoothness,
                        v => layers[idx].smoothness = v,
                        0, 1, 0.01f, "F3"));
                }
            }
        }

        void BuildWaterSettings(GameObject go)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            Material waterMat = null;
            foreach (var m in renderer.sharedMaterials)
            {
                if (m != null && (m.name.ToLower().Contains("water") ||
                    (m.shader != null && m.shader.name.ToLower().Contains("water"))))
                {
                    waterMat = m;
                    break;
                }
            }
            if (waterMat == null) return;

            _settings.Add(new Setting("--- WATER COLOR ---", () => 0, v => { }, 0, 0, 0));

            AddColorSettings(waterMat, "_Color", "Water");

            _settings.Add(new Setting("--- WATER SURFACE ---", () => 0, v => { }, 0, 0, 0));

            AddFloatIfExists(waterMat, "_Metallic", "Metallic", 0, 1, 0.01f, "F3");
            AddFloatIfExists(waterMat, "_Smth", "Smoothness", 0, 1, 0.01f, "F3");
            AddFloatIfExists(waterMat, "_Smoothness", "Smoothness", 0, 1, 0.01f, "F3");
            AddFloatIfExists(waterMat, "_BumpScale", "Normal Strength", 0, 3, 0.01f, "F3");
            AddFloatIfExists(waterMat, "_Alpha", "Transparency", 0, 1, 0.01f, "F3");

            _settings.Add(new Setting("--- WATER WAVES ---", () => 0, v => { }, 0, 0, 0));

            AddFloatIfExists(waterMat, "_WaveHeight", "Wave Height", 0, 3, 0.01f, "F3");
            AddFloatIfExists(waterMat, "_WaveFreq", "Wave Freq", 0, 100, 0.5f, "F1");
            AddFloatIfExists(waterMat, "_WaveComplexity", "Wave Complexity", 0, 1, 0.01f, "F3");
            AddFloatIfExists(waterMat, "_WaveSteepness", "Wave Steepness", 0, 1, 0.01f, "F3");
            AddFloatIfExists(waterMat, "_ScrollSpeed", "Scroll Speed", 0, 2, 0.01f, "F3");

            _settings.Add(new Setting("--- WATER FOAM ---", () => 0, v => { }, 0, 0, 0));

            AddFloatIfExists(waterMat, "_FoamDepth", "Foam Depth", 0.01f, 5, 0.05f, "F2");
            AddFloatIfExists(waterMat, "_FoamStr", "Foam Strength", 0, 3, 0.05f, "F2");
            AddColorIfExists(waterMat, "_FoamColor", "Foam");

            _settings.Add(new Setting("--- WATER EXTRA ---", () => 0, v => { }, 0, 0, 0));

            AddFloatIfExists(waterMat, "_Emis", "Emission", 0, 0.5f, 0.01f, "F3");
            AddFloatIfExists(waterMat, "_ReflStr", "Reflection Str", 0, 3, 0.01f, "F3");
            AddFloatIfExists(waterMat, "_FresnelPow", "Fresnel Power", 0.1f, 10, 0.1f, "F2");
            AddFloatIfExists(waterMat, "_UVScale", "UV Density", 0.1f, 200, 1f, "F1");
        }

        void BuildSkyboxSettings()
        {
            var skyMat = RenderSettings.skybox;
            if (skyMat == null) return;

            _settings.Add(new Setting("--- SKYBOX ---", () => 0, v => { }, 0, 0, 0));

            // Tint or Color
            string colorProp = skyMat.HasColor("_Tint") ? "_Tint" :
                               skyMat.HasColor("_Color") ? "_Color" : null;

            if (colorProp != null)
            {
                _settings.Add(new Setting("Tint R",
                    () => skyMat.GetColor(colorProp).r,
                    v => { var c = skyMat.GetColor(colorProp); c.r = v; skyMat.SetColor(colorProp, c); },
                    0, 2, 0.02f, "F2"));
                _settings.Add(new Setting("Tint G",
                    () => skyMat.GetColor(colorProp).g,
                    v => { var c = skyMat.GetColor(colorProp); c.g = v; skyMat.SetColor(colorProp, c); },
                    0, 2, 0.02f, "F2"));
                _settings.Add(new Setting("Tint B",
                    () => skyMat.GetColor(colorProp).b,
                    v => { var c = skyMat.GetColor(colorProp); c.b = v; skyMat.SetColor(colorProp, c); },
                    0, 2, 0.02f, "F2"));
            }

            AddFloatIfExists(skyMat, "_Exposure", "Exposure", 0, 5, 0.1f, "F1");
            AddFloatIfExists(skyMat, "_Rotation", "Rotation", 0, 360, 5, "F0");
            AddFloatIfExists(skyMat, "_CloudBoost", "Cloud Brightness", 0, 5, 0.05f, "F2");
            AddFloatIfExists(skyMat, "_CloudThreshold", "Cloud Threshold", 0, 1, 0.01f, "F3");
        }

        void BuildNPCSettings(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                // Walk up to find root NPC
                Transform root = go.transform;
                while (root.parent != null)
                {
                    string n = root.parent.name.ToLower();
                    if (n.Contains("npc") || n.Contains("soldier") || n.Contains("character")
                        || n.Contains("enemy") || n.Contains("klaszczur") || n.Contains("pinea"))
                        root = root.parent;
                    else
                        break;
                }
                renderers = root.GetComponentsInChildren<Renderer>(true);
            }

            // Collect unique materials
            var mats = new List<Material>();
            var seen = new HashSet<int>();
            foreach (var r in renderers)
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m != null && seen.Add(m.GetInstanceID()))
                        mats.Add(m);
                }
            }

            if (mats.Count == 0) return;

            foreach (var mat in mats)
            {
                string matName = mat.name.Length > 20 ? mat.name.Substring(0, 20) : mat.name;
                _settings.Add(new Setting($"--- {matName} ---", () => 0, v => { }, 0, 0, 0));

                string colorProp = mat.HasColor("_BaseColor") ? "_BaseColor" :
                                   mat.HasColor("_Color") ? "_Color" : null;

                if (colorProp != null)
                    AddColorSettings(mat, colorProp, "Color");

                AddFloatIfExists(mat, "_Metallic", "Metallic", 0, 1, 0.01f, "F3");
                AddFloatIfExists(mat, "_Smoothness", "Smoothness", 0, 1, 0.01f, "F3");
                AddFloatIfExists(mat, "_Glossiness", "Glossiness", 0, 1, 0.01f, "F3");

                // Emission
                if (mat.HasColor("_EmissionColor"))
                {
                    Material capturedMat = mat;
                    _settings.Add(new Setting("Emission Intensity",
                        () =>
                        {
                            var ec = capturedMat.GetColor("_EmissionColor");
                            return Mathf.Max(ec.r, Mathf.Max(ec.g, ec.b));
                        },
                        v =>
                        {
                            capturedMat.SetColor("_EmissionColor", Color.white * v);
                            if (v > 0) capturedMat.EnableKeyword("_EMISSION");
                            else capturedMat.DisableKeyword("_EMISSION");
                        },
                        0, 5, 0.1f, "F2"));
                }

                AddFloatIfExists(mat, "_BumpScale", "Normal Scale", 0, 3, 0.01f, "F3");
            }
        }

        void BuildGenericSettings(GameObject go)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) renderer = go.GetComponentInChildren<Renderer>();
            if (renderer == null) return;

            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat == null) continue;

                string matName = mat.name.Length > 25 ? mat.name.Substring(0, 25) : mat.name;
                _settings.Add(new Setting($"--- {matName} ---", () => 0, v => { }, 0, 0, 0));

                // Color
                string colorProp = mat.HasColor("_BaseColor") ? "_BaseColor" :
                                   mat.HasColor("_Color") ? "_Color" : null;

                if (colorProp != null)
                    AddColorSettings(mat, colorProp, "Color");

                // Standard PBR
                AddFloatIfExists(mat, "_Metallic", "Metallic", 0, 1, 0.01f, "F3");
                AddFloatIfExists(mat, "_Smoothness", "Smoothness", 0, 1, 0.01f, "F3");
                AddFloatIfExists(mat, "_Glossiness", "Glossiness", 0, 1, 0.01f, "F3");
                AddFloatIfExists(mat, "_BumpScale", "Normal Scale", 0, 3, 0.01f, "F3");
                AddFloatIfExists(mat, "_OcclusionStrength", "AO Strength", 0, 1, 0.01f, "F3");

                // Emission
                if (mat.HasColor("_EmissionColor"))
                {
                    Material capturedMat = mat;
                    _settings.Add(new Setting("Emission Intensity",
                        () =>
                        {
                            var ec = capturedMat.GetColor("_EmissionColor");
                            return Mathf.Max(ec.r, Mathf.Max(ec.g, ec.b));
                        },
                        v =>
                        {
                            capturedMat.SetColor("_EmissionColor", Color.white * v);
                            if (v > 0) capturedMat.EnableKeyword("_EMISSION");
                            else capturedMat.DisableKeyword("_EMISSION");
                        },
                        0, 5, 0.1f, "F2"));
                }

                // Alpha
                AddFloatIfExists(mat, "_Alpha", "Alpha", 0, 1, 0.01f, "F3");

                // Specular
                AddColorIfExists(mat, "_SpecColor", "Specular");
            }
        }

        // ── Helper: add settings for common patterns ──

        void AddFloatIfExists(Material mat, string prop, string displayName,
            float min, float max, float step, string fmt)
        {
            if (!mat.HasFloat(prop)) return;
            Material capturedMat = mat;
            string capturedProp = prop;
            _settings.Add(new Setting(displayName,
                () => capturedMat.GetFloat(capturedProp),
                v => capturedMat.SetFloat(capturedProp, v),
                min, max, step, fmt));
        }

        void AddColorSettings(Material mat, string prop, string prefix)
        {
            Material capturedMat = mat;
            string capturedProp = prop;
            _settings.Add(new Setting($"{prefix} R",
                () => capturedMat.GetColor(capturedProp).r,
                v => { var c = capturedMat.GetColor(capturedProp); c.r = v; capturedMat.SetColor(capturedProp, c); },
                0, 2, 0.01f, "F3"));
            _settings.Add(new Setting($"{prefix} G",
                () => capturedMat.GetColor(capturedProp).g,
                v => { var c = capturedMat.GetColor(capturedProp); c.g = v; capturedMat.SetColor(capturedProp, c); },
                0, 2, 0.01f, "F3"));
            _settings.Add(new Setting($"{prefix} B",
                () => capturedMat.GetColor(capturedProp).b,
                v => { var c = capturedMat.GetColor(capturedProp); c.b = v; capturedMat.SetColor(capturedProp, c); },
                0, 2, 0.01f, "F3"));
        }

        void AddColorIfExists(Material mat, string prop, string prefix)
        {
            if (!mat.HasColor(prop)) return;
            AddColorSettings(mat, prop, prefix);
        }

        // ── Panel Canvas ──

        void BuildPanelCanvas()
        {
            if (_panelGO != null) Destroy(_panelGO);

            _panelGO = new GameObject("LaserInspector_Panel");
            _panelGO.transform.SetParent(transform, false);

            var canvas = _panelGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 999;

            var cg = _panelGO.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            var rt = _panelGO.GetComponent<RectTransform>();
            float panelHeight = 40 + VISIBLE_ROWS * 26 + 30;
            rt.sizeDelta = new Vector2(520, panelHeight);
            rt.localScale = Vector3.one * 0.0008f;

            // Background
            var bgGO = new GameObject("BG");
            bgGO.transform.SetParent(_panelGO.transform, false);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.01f, 0.01f, 0.03f, 0.92f);
            bgImg.raycastTarget = false;
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            // Title
            var titleGO = MakeText(bgGO.transform, $"INSPECT: {_inspectedName}",
                17, new Color(0.3f, 0.9f, 1f), new Vector2(10, -5), new Vector2(500, 26));
            _titleText = titleGO.GetComponent<Text>();

            // Rows
            _rowTexts = new Text[VISIBLE_ROWS];
            for (int i = 0; i < VISIBLE_ROWS; i++)
            {
                float y = -33 - i * 26;
                var go = MakeText(bgGO.transform, "", 16, Color.white,
                    new Vector2(10, y), new Vector2(500, 24));
                _rowTexts[i] = go.GetComponent<Text>();
            }

            // Footer
            MakeText(bgGO.transform,
                "L.STICK ^v select <> adjust | [B] close",
                13, new Color(0.4f, 0.4f, 0.4f),
                new Vector2(10, -33 - VISIBLE_ROWS * 26), new Vector2(500, 22));
        }

        GameObject MakeText(Transform parent, string txt, int size, Color col,
            Vector2 pos, Vector2 sz)
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
            t.supportRichText = true;
            t.raycastTarget = false;
            return go;
        }

        // ── Panel position ──

        void PositionPanel()
        {
            if (_panelGO == null) return;

            var cam = Camera.main;
            if (cam == null) return;

            // Smooth follow near the inspected point, facing camera
            _panelGO.transform.position = Vector3.Lerp(
                _panelGO.transform.position, _panelWorldPos, Time.deltaTime * 5f);

            Vector3 lookDir = cam.transform.position - _panelGO.transform.position;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.001f)
                _panelGO.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        }

        // ── Panel input ──

        void UpdatePanelInput()
        {
#if HAS_META_XR
            _inputCooldown -= Time.unscaledDeltaTime;
            if (_inputCooldown > 0) return;

            Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick); // left stick

            // Navigate up/down
            if (stick.y > 0.5f)
            {
                do { _selectedRow = (_selectedRow - 1 + _settings.Count) % _settings.Count; }
                while (_settings[_selectedRow].isHeader && _settings.Count > 1);
                _inputCooldown = INPUT_COOLDOWN_NAV;
            }
            else if (stick.y < -0.5f)
            {
                do { _selectedRow = (_selectedRow + 1) % _settings.Count; }
                while (_settings[_selectedRow].isHeader && _settings.Count > 1);
                _inputCooldown = INPUT_COOLDOWN_NAV;
            }

            // Adjust left/right
            if (stick.x > 0.3f && !_settings[_selectedRow].isHeader)
            {
                var s = _settings[_selectedRow];
                float multiplier = stick.x > 0.8f ? 3f : 1f; // faster at full tilt
                s.set(Mathf.Clamp(s.get() + s.step * multiplier, s.min, s.max));
                _inputCooldown = INPUT_COOLDOWN_ADJ;
            }
            else if (stick.x < -0.3f && !_settings[_selectedRow].isHeader)
            {
                var s = _settings[_selectedRow];
                float multiplier = stick.x < -0.8f ? 3f : 1f;
                s.set(Mathf.Clamp(s.get() - s.step * multiplier, s.min, s.max));
                _inputCooldown = INPUT_COOLDOWN_ADJ;
            }

            // Keep selected row in visible scroll window
            if (_selectedRow < _scrollOffset) _scrollOffset = _selectedRow;
            if (_selectedRow >= _scrollOffset + VISIBLE_ROWS) _scrollOffset = _selectedRow - VISIBLE_ROWS + 1;
            _scrollOffset = Mathf.Clamp(_scrollOffset, 0, Mathf.Max(0, _settings.Count - VISIBLE_ROWS));
#endif
        }

        // ── Panel display ──

        void UpdatePanelDisplay()
        {
            if (_rowTexts == null) return;

            for (int vi = 0; vi < VISIBLE_ROWS; vi++)
            {
                int si = vi + _scrollOffset;
                if (si >= _settings.Count) { _rowTexts[vi].text = ""; continue; }

                var s = _settings[si];
                bool selected = (si == _selectedRow);

                if (s.isHeader)
                {
                    _rowTexts[vi].text = $"<color=#666666>{s.name}</color>";
                }
                else
                {
                    string val;
                    try { val = s.get().ToString(s.format); }
                    catch { val = "???"; }

                    string arrow = selected ? ">>  " : "    ";
                    string c = selected ? "#00ffcc" : "#cccccc";
                    string bar = "";
                    if (s.max > s.min)
                    {
                        float pct = Mathf.Clamp01((s.get() - s.min) / (s.max - s.min));
                        int filled = Mathf.Clamp((int)(pct * 10), 0, 10);
                        bar = " [" + new string('|', filled) + new string('.', 10 - filled) + "]";
                    }
                    _rowTexts[vi].text = $"<color={c}>{arrow}{s.name}: {val}{bar}</color>";
                }
            }
        }
    }
}
