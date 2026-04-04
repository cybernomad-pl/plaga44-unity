// VFXSpawnerMenu.cs
// CYBERNOMAD -- World-space VR menu for spawning VFX prefabs.
// Toggle: A button (Button.One, right controller)
// Navigation: LEFT STICK up/down = select, left/right = category
// Spawn: LEFT TRIGGER
// Delete Last / Delete All: menu actions at bottom of list
//
// Loads prefabs from Resources/VFXPrefabs/{Category}/ at startup.
// To use: copy VFX prefabs into Assets/Resources/VFXPrefabs/Projectiles/,
//         Assets/Resources/VFXPrefabs/AoE/, Assets/Resources/VFXPrefabs/Sparks/
//
// Also supports editor-time loading from GabrielAguiarProductions paths.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.UI
{
    public class VFXSpawnerMenu : MonoBehaviour
    {
        public static bool MenuOpen { get; private set; } = false;
        public static VFXSpawnerMenu Instance { get; private set; }

        // ---- Public API for VRMenuManager ----

        /// <summary>Get formatted list of VFX entries for current category.</summary>
        public string GetVFXList()
        {
            if (_allEntries.Count == 0) return "<color=#666666>No VFX prefabs loaded.</color>";
            var sb = new System.Text.StringBuilder();
            foreach (var entry in _allEntries)
            {
                sb.AppendLine($"<color=#66aaff>[{entry.Category}]</color> {entry.Name}");
            }
            sb.AppendLine($"\n<color=#888888>{_spawnedObjects.Count} active VFX</color>");
            return sb.ToString();
        }

        /// <summary>Spawn the first VFX entry (or current selection if available).</summary>
        public void SpawnCurrent()
        {
            if (_allEntries.Count == 0) return;
            int idx = Mathf.Clamp(_selectedRow, 0, _allEntries.Count - 1);
            SpawnVFX(_allEntries[idx]);
        }

        /// <summary>Delete last spawned VFX.</summary>
        public void DeleteLast() => DeleteLastSpawned();

        /// <summary>Delete all spawned VFX.</summary>
        public void DeleteAll() => DeleteAllSpawned();

        // ---- Categories ----

        private struct VFXEntry
        {
            public string Name;
            public string Category;
            public GameObject Prefab;
        }

        private readonly string[] _categories = { "Projectiles", "AoE", "Sparks" };
        private int _currentCategory = 0;
        private List<VFXEntry> _allEntries = new List<VFXEntry>();
        private List<VFXEntry> _filteredEntries = new List<VFXEntry>();

        // ---- Spawned tracking ----

        private List<GameObject> _spawnedObjects = new List<GameObject>();

        // ---- UI ----

        private GameObject _canvasGO;
        private Text _titleText;
        private Text _categoryText;
        private Text[] _rowTexts;
        private Text _footerText;
        private bool _visible = false;
        private int _selectedRow = 0;
        private float _inputCooldown = 0f;
        private float _spawnDistance = 2.5f;

        private const int MAX_VISIBLE_ROWS = 10;

        // ---- Colours (dark theme, matching VRMenuManager) ----

        private static readonly Color BG_COLOR  = new Color(0.08f, 0.04f, 0.12f, 0.93f);
        private static readonly Color ACCENT    = new Color(0.40f, 0.70f, 1.00f, 1.00f); // blue accent for VFX
        private static readonly Color ACCENT_ALT = new Color(1.00f, 0.42f, 0.21f, 1.00f); // orange for actions
        private static readonly Color TEXT_DIM  = new Color(0.50f, 0.50f, 0.50f, 1.00f);

        // ---- Action rows ----

        private int ActionStartIndex => _filteredEntries.Count;
        private int TotalRows => _filteredEntries.Count + 2; // DELETE LAST, DELETE ALL

        // ---- Lifecycle ----

        // DISABLED: VFX Spawner removed from A button to resolve input conflicts.
        // VFX spawning can be re-enabled later via a unified menu system.
        //
        // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        // static void AutoCreate()
        // {
        //     var go = new GameObject("_VFXSpawnerMenu");
        //     Instance = go.AddComponent<VFXSpawnerMenu>();
        //     DontDestroyOnLoad(go);
        // }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            LoadAllPrefabs();
            FilterByCategory();
            CreateWorldCanvas();
            _canvasGO.SetActive(false);
            Debug.Log($"[PLAGA44] VFXSpawnerMenu: {_allEntries.Count} VFX loaded across {_categories.Length} categories");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---- Prefab Loading ----

        void LoadAllPrefabs()
        {
            _allEntries.Clear();

            // Load from Resources/VFXPrefabs/{Category}/
            foreach (var cat in _categories)
            {
                string resourcePath = $"VFXPrefabs/{cat}";
                var loaded = Resources.LoadAll<GameObject>(resourcePath);
                if (loaded != null)
                {
                    foreach (var prefab in loaded)
                    {
                        _allEntries.Add(new VFXEntry
                        {
                            Name = prefab.name,
                            Category = cat,
                            Prefab = prefab
                        });
                    }
                }
            }

#if UNITY_EDITOR
            // Fallback: load from GabrielAguiarProductions paths in editor
            if (_allEntries.Count == 0)
            {
                LoadEditorPrefabs();
            }
#endif

            // Sort alphabetically within categories
            _allEntries.Sort((a, b) =>
            {
                int catCmp = string.Compare(a.Category, b.Category);
                return catCmp != 0 ? catCmp : string.Compare(a.Name, b.Name);
            });

            if (_allEntries.Count == 0)
                Debug.LogWarning("[PLAGA44] VFXSpawnerMenu: No VFX prefabs found. " +
                    "Place prefabs in Assets/Resources/VFXPrefabs/{Projectiles,AoE,Sparks}/");
        }

#if UNITY_EDITOR
        void LoadEditorPrefabs()
        {
            // Dynamically scan known VFX asset folders for prefabs.
            // For each category, search multiple possible root paths
            // (GabrielAguiarProductions, any folder containing "VFX", etc.)
            var searchRoots = new[]
            {
                "Assets/GabrielAguiarProductions/Prefabs",
                "Assets/VFX/Prefabs",
                "Assets/Prefabs/VFX",
                "Assets/PLAGA44/VFX",
            };

            foreach (var cat in _categories)
            {
                foreach (var root in searchRoots)
                {
                    string folderPath = $"{root}/{cat}";
                    if (!UnityEditor.AssetDatabase.IsValidFolder(folderPath)) continue;

                    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
                    foreach (string guid in guids)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (prefab != null)
                        {
                            _allEntries.Add(new VFXEntry
                            {
                                Name = prefab.name,
                                Category = cat,
                                Prefab = prefab
                            });
                        }
                    }
                }
            }

            // Broader fallback: scan entire project for folders named exactly
            // "Projectiles", "AoE", or "Sparks" that contain prefabs with "vfx" in name
            if (_allEntries.Count == 0)
            {
                Debug.Log("[PLAGA44] VFXSpawnerMenu: known VFX paths not found, scanning project...");
                foreach (var cat in _categories)
                {
                    string[] folderGuids = UnityEditor.AssetDatabase.FindAssets($"t:Folder {cat}");
                    foreach (string fg in folderGuids)
                    {
                        string folderPath = UnityEditor.AssetDatabase.GUIDToAssetPath(fg);
                        // Only match folders that end with the category name
                        if (!folderPath.EndsWith($"/{cat}")) continue;
                        // Skip packages
                        if (folderPath.StartsWith("Packages/")) continue;

                        string[] prefabGuids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
                        foreach (string pg in prefabGuids)
                        {
                            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(pg);
                            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                            if (prefab != null)
                            {
                                _allEntries.Add(new VFXEntry
                                {
                                    Name = prefab.name,
                                    Category = cat,
                                    Prefab = prefab
                                });
                            }
                        }
                    }
                }
            }

            if (_allEntries.Count > 0)
                Debug.Log($"[PLAGA44] VFXSpawnerMenu: Loaded {_allEntries.Count} VFX from editor (AssetDatabase scan)");
            else
                Debug.Log("[PLAGA44] VFXSpawnerMenu: No VFX prefabs found anywhere in project. " +
                    "Import a VFX asset pack (e.g. Gabriel Aguiar Productions) with prefabs in " +
                    "Prefabs/Projectiles/, Prefabs/AoE/, Prefabs/Sparks/ subfolders.");
        }
#endif

        void FilterByCategory()
        {
            _filteredEntries.Clear();
            string cat = _categories[_currentCategory];
            foreach (var entry in _allEntries)
            {
                if (entry.Category == cat)
                    _filteredEntries.Add(entry);
            }
            _selectedRow = Mathf.Clamp(_selectedRow, 0, Mathf.Max(0, TotalRows - 1));
        }

        // ---- Update ----

        void Update()
        {
            // Input handling REMOVED -- VRMenuManager owns all menu input now.
            // VFXSpawnerMenu is controlled via public API (GetVFXList, SpawnCurrent, etc.)
            // from VRMenuManager's Spawner sub-panel.

            // Keep the canvas hidden -- VFXSpawnerMenu no longer has its own UI.
            // All display is handled by VRMenuManager.
        }

        void Execute(int row)
        {
            if (row < _filteredEntries.Count)
            {
                SpawnVFX(_filteredEntries[row]);
            }
            else
            {
                int action = row - ActionStartIndex;
                if (action == 0) DeleteLastSpawned();
                else if (action == 1) DeleteAllSpawned();
            }
        }

        // ---- Spawn / Delete ----

        void SpawnVFX(VFXEntry entry)
        {
            if (entry.Prefab == null) return;

            var cam = Camera.main;
            Vector3 pos;
            Quaternion rot;

            if (cam != null)
            {
                pos = cam.transform.position + cam.transform.forward * _spawnDistance;
                rot = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
            }
            else
            {
                pos = Vector3.forward * _spawnDistance;
                rot = Quaternion.identity;
            }

            // Raycast down to find ground for AoE effects
            if (entry.Category == "AoE")
            {
                if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 50f))
                    pos.y = hit.point.y;
            }

            var instance = Instantiate(entry.Prefab, pos, rot);
            instance.name = $"VFX_{entry.Name}_spawned_{_spawnedObjects.Count}";
            instance.SetActive(true);

            _spawnedObjects.Add(instance);

            // Haptic feedback
            OVRInput.SetControllerVibration(0.3f, 0.3f, OVRInput.Controller.LTouch);
            Invoke(nameof(StopHaptic), 0.08f);

            Debug.Log($"[PLAGA44] VFXSpawnerMenu: Spawned '{entry.Name}' ({entry.Category}) at {pos}");
            UpdateDisplay();
        }

        void StopHaptic()
        {
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
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
                    Debug.Log("[PLAGA44] VFXSpawnerMenu: Deleted last VFX");
                    UpdateDisplay();
                    return;
                }
            }
        }

        void DeleteAllSpawned()
        {
            int count = 0;
            foreach (var obj in _spawnedObjects)
            {
                if (obj != null) { Destroy(obj); count++; }
            }
            _spawnedObjects.Clear();
            Debug.Log($"[PLAGA44] VFXSpawnerMenu: Deleted {count} VFX objects");
            UpdateDisplay();
        }

        // ---- Movement Blocking ----

        void BlockPlayerMovement(bool block)
        {
            var pc = FindAnyObjectByType<OVRPlayerController>();
            if (pc != null)
                pc.EnableLinearMovement = !block;
        }

        // ---- Canvas ----

        void CreateWorldCanvas()
        {
            int rowCount = Mathf.Min(TotalRows, MAX_VISIBLE_ROWS + 2); // +2 for action rows
            float canvasHeight = 90 + rowCount * 28;

            _canvasGO = new GameObject("VFXSpawnerCanvas");
            _canvasGO.transform.SetParent(transform);
            var canvas = _canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 102;
            _canvasGO.AddComponent<CanvasScaler>();

            var rt = _canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(480, canvasHeight);
            rt.localScale = Vector3.one * 0.0008f;

            // Background
            var bg = new GameObject("BG");
            bg.transform.SetParent(_canvasGO.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = BG_COLOR;
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            // Title
            var titleGo = MakeText(bg.transform, "", 20, ACCENT,
                new Vector2(10, -5), new Vector2(460, 28));
            _titleText = titleGo.GetComponent<Text>();

            // Category selector
            var catGo = MakeText(bg.transform, "", 16, ACCENT_ALT,
                new Vector2(10, -30), new Vector2(460, 24));
            _categoryText = catGo.GetComponent<Text>();

            // Rows
            _rowTexts = new Text[rowCount];
            for (int i = 0; i < rowCount; i++)
            {
                float y = -58 - i * 28;
                var go = MakeText(bg.transform, "", 17, Color.white,
                    new Vector2(10, y), new Vector2(460, 26));
                _rowTexts[i] = go.GetComponent<Text>();
            }

            // Footer hint
            var footerY = -58 - rowCount * 28;
            var footerGo = MakeText(bg.transform,
                "L.STICK ^v select <> category | L.TRIG spawn | [A] menu",
                11, TEXT_DIM, new Vector2(10, footerY), new Vector2(460, 20));
            _footerText = footerGo.GetComponent<Text>();
        }

        void RebuildCanvas()
        {
            if (_canvasGO != null)
            {
                bool wasActive = _canvasGO.activeSelf;
                Destroy(_canvasGO);
                CreateWorldCanvas();
                _canvasGO.SetActive(wasActive);
            }
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
            return go;
        }

        // ---- Canvas Positioning ----

        void PositionCanvas()
        {
            var rig = FindAnyObjectByType<OVRCameraRig>();
            Transform anchor = rig != null ? rig.leftHandAnchor : null;

            if (anchor != null)
            {
                Vector3 target = anchor.position + anchor.forward * 0.25f + anchor.up * 0.12f;
                _canvasGO.transform.position = Vector3.Lerp(
                    _canvasGO.transform.position, target, Time.deltaTime * 8f);
            }
            else
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 target = cam.transform.position +
                        cam.transform.forward * 1f - cam.transform.right * 0.4f;
                    _canvasGO.transform.position = Vector3.Lerp(
                        _canvasGO.transform.position, target, Time.deltaTime * 4f);
                }
            }

            var camLook = Camera.main;
            if (camLook != null)
            {
                _canvasGO.transform.rotation = Quaternion.Slerp(
                    _canvasGO.transform.rotation,
                    Quaternion.LookRotation(_canvasGO.transform.position - camLook.transform.position),
                    Time.deltaTime * 8f);
            }
        }

        // ---- Display ----

        void UpdateDisplay()
        {
            if (_titleText == null) return;

            string cat = _categories[_currentCategory];
            _titleText.text = $"<color=#66AAFF>VFX SPAWNER</color>  [{_spawnedObjects.Count} active]";

            // Category bar with arrows
            string catDisplay = "";
            for (int i = 0; i < _categories.Length; i++)
            {
                if (i == _currentCategory)
                    catDisplay += $"<color=#FF6B35>[ {_categories[i]} ]</color>  ";
                else
                    catDisplay += $"<color=#555555>{_categories[i]}</color>  ";
            }
            if (_categoryText != null)
                _categoryText.text = $"< {catDisplay}>";

            int visibleCount = Mathf.Min(TotalRows, _rowTexts.Length);
            for (int i = 0; i < _rowTexts.Length; i++)
            {
                if (i >= TotalRows)
                {
                    _rowTexts[i].text = "";
                    continue;
                }

                bool sel = (i == _selectedRow);
                string arrow = sel ? ">>  " : "    ";

                if (i < _filteredEntries.Count)
                {
                    var entry = _filteredEntries[i];
                    string c = sel ? "#66aaff" : "#cccccc";
                    _rowTexts[i].text = $"<color={c}>{arrow}{entry.Name}</color>";
                }
                else
                {
                    int action = i - ActionStartIndex;
                    string label = action switch
                    {
                        0 => "[DELETE LAST]",
                        1 => "[DELETE ALL]",
                        _ => ""
                    };
                    string ac = sel ? "#ff4444" : "#666666";
                    _rowTexts[i].text = $"<color={ac}>{arrow}{label}</color>";
                }
            }
        }
    }
}
