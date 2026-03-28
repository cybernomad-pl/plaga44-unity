#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    /// <summary>
    /// Item Spawner -- CYBERNOMAD / Tools / Item Spawner
    ///
    /// Editor window for browsing and spawning PLAGA44 assets onto the scene.
    /// Scans Assets/PLAGA44/ for FBX models, groups by category, shows preview,
    /// and places items at scene view camera position or world origin.
    /// </summary>
    public class ItemSpawner : EditorWindow
    {
        private const string ASSETS_ROOT = "Assets/PLAGA44";
        private const string LOG = "[ItemSpawner]";

        // Category definition
        private struct AssetEntry
        {
            public string path;       // project-relative path
            public string name;       // display name
            public string category;   // grouping
            public Object asset;      // loaded reference
            public Texture2D preview; // asset preview
        }

        private List<AssetEntry> _entries = new();
        private Dictionary<string, bool> _categoryFoldouts = new();
        private Vector2 _scrollPos;
        private string _searchFilter = "";
        private float _spawnScale = 1f;
        private bool _spawnAtCamera = true;
        private bool _needsRefresh = true;

        // Preview
        private UnityEditor.Editor _previewEditor;
        private Object _previewTarget;

        [MenuItem("CYBERNOMAD/Tools/Item Spawner", false, 200)]
        public static void ShowWindow()
        {
            var wnd = GetWindow<ItemSpawner>("Item Spawner");
            wnd.minSize = new Vector2(320, 400);
            wnd.Show();
        }

        private void OnEnable()
        {
            _needsRefresh = true;
        }

        private void OnDisable()
        {
            if (_previewEditor != null)
                DestroyImmediate(_previewEditor);
        }

        private void RefreshAssetList()
        {
            _entries.Clear();
            _categoryFoldouts.Clear();

            if (!AssetDatabase.IsValidFolder(ASSETS_ROOT))
            {
                Debug.LogWarning($"{LOG} {ASSETS_ROOT} not found!");
                return;
            }

            // Find all FBX/OBJ/prefab files
            string[] guids = AssetDatabase.FindAssets("t:GameObject t:Mesh", new[] { ASSETS_ROOT });

            // Also search for FBX specifically
            string[] fbxGuids = AssetDatabase.FindAssets("", new[] { ASSETS_ROOT });

            var processedPaths = new HashSet<string>();

            foreach (string guid in fbxGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string ext = Path.GetExtension(path).ToLower();

                if (ext != ".fbx" && ext != ".obj" && ext != ".prefab")
                    continue;

                if (processedPaths.Contains(path))
                    continue;
                processedPaths.Add(path);

                string category = DeduceCategory(path);
                string name = Path.GetFileNameWithoutExtension(path);

                _entries.Add(new AssetEntry
                {
                    path = path,
                    name = name,
                    category = category,
                    asset = null,
                    preview = null
                });

                if (!_categoryFoldouts.ContainsKey(category))
                    _categoryFoldouts[category] = true;
            }

            _entries = _entries.OrderBy(e => e.category).ThenBy(e => e.name).ToList();
            _needsRefresh = false;

            Debug.Log($"{LOG} Found {_entries.Count} assets in {_categoryFoldouts.Count} categories");
        }

        private string DeduceCategory(string path)
        {
            // Derive category from folder structure
            // Assets/PLAGA44/Weapons/Models/M249/... -> "Weapons/M249"
            string relative = path.Replace(ASSETS_ROOT + "/", "");
            string[] parts = relative.Split('/');

            if (parts.Length >= 3)
            {
                // e.g. Weapons/Models/M249 or Characters/Animations/Shooter
                return $"{parts[0]}/{parts[2]}";
            }
            else if (parts.Length >= 2)
            {
                return $"{parts[0]}/{parts[1]}";
            }
            return parts[0];
        }

        private void OnGUI()
        {
            if (_needsRefresh)
                RefreshAssetList();

            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    _needsRefresh = true;

                GUILayout.Space(8);
                _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField);

                if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(20)))
                    _searchFilter = "";
            }
            EditorGUILayout.EndHorizontal();

            // Spawn settings
            EditorGUILayout.BeginHorizontal();
            {
                _spawnAtCamera = EditorGUILayout.ToggleLeft("At Camera", _spawnAtCamera, GUILayout.Width(90));
                EditorGUILayout.LabelField("Scale:", GUILayout.Width(40));
                _spawnScale = EditorGUILayout.FloatField(_spawnScale, GUILayout.Width(50));

                if (GUILayout.Button("0.01", GUILayout.Width(35))) _spawnScale = 0.01f;
                if (GUILayout.Button("0.1", GUILayout.Width(30))) _spawnScale = 0.1f;
                if (GUILayout.Button("1", GUILayout.Width(22))) _spawnScale = 1f;
                if (GUILayout.Button("100", GUILayout.Width(30))) _spawnScale = 100f;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Asset list
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            {
                string filterLower = _searchFilter.ToLower();
                string lastCategory = null;

                for (int i = 0; i < _entries.Count; i++)
                {
                    var entry = _entries[i];

                    // Filter
                    if (!string.IsNullOrEmpty(filterLower))
                    {
                        if (!entry.name.ToLower().Contains(filterLower) &&
                            !entry.category.ToLower().Contains(filterLower))
                            continue;
                    }

                    // Category header
                    if (entry.category != lastCategory)
                    {
                        lastCategory = entry.category;
                        EditorGUILayout.Space(4);
                        _categoryFoldouts[entry.category] = EditorGUILayout.Foldout(
                            _categoryFoldouts[entry.category],
                            $"--- {entry.category} ---",
                            true,
                            EditorStyles.foldoutHeader);
                    }

                    if (!_categoryFoldouts[entry.category])
                        continue;

                    // Asset row
                    EditorGUILayout.BeginHorizontal();
                    {
                        GUILayout.Space(16);

                        // Load asset lazily for preview
                        if (entry.asset == null)
                        {
                            entry.asset = AssetDatabase.LoadMainAssetAtPath(entry.path);
                            _entries[i] = entry;
                        }

                        // Thumbnail
                        Texture2D thumb = AssetPreview.GetAssetPreview(entry.asset);
                        if (thumb == null)
                            thumb = AssetPreview.GetMiniThumbnail(entry.asset);

                        if (thumb != null)
                        {
                            if (GUILayout.Button(thumb, GUILayout.Width(32), GUILayout.Height(32)))
                                SelectAndPreview(entry);
                        }
                        else
                        {
                            if (GUILayout.Button("?", GUILayout.Width(32), GUILayout.Height(32)))
                                SelectAndPreview(entry);
                        }

                        // Name + path
                        EditorGUILayout.BeginVertical();
                        {
                            EditorGUILayout.LabelField(entry.name, EditorStyles.boldLabel);
                            EditorGUILayout.LabelField(entry.path, EditorStyles.miniLabel);
                        }
                        EditorGUILayout.EndVertical();

                        // Spawn button
                        if (GUILayout.Button("SPAWN", GUILayout.Width(60), GUILayout.Height(30)))
                        {
                            SpawnAsset(entry);
                        }

                        // Ping button
                        if (GUILayout.Button(">>", GUILayout.Width(28), GUILayout.Height(30)))
                        {
                            EditorGUIUtility.PingObject(entry.asset);
                            Selection.activeObject = entry.asset;
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    // Separator
                    var rect = EditorGUILayout.GetControlRect(false, 1);
                    EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
                }
            }
            EditorGUILayout.EndScrollView();

            // Preview area at bottom
            if (_previewTarget != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Preview:", EditorStyles.boldLabel);
                if (_previewEditor != null)
                {
                    _previewEditor.OnInteractivePreviewGUI(
                        GUILayoutUtility.GetRect(200, 150),
                        EditorStyles.helpBox);
                }
            }

            // Status bar
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField($"{_entries.Count} assets | {_categoryFoldouts.Count} categories",
                EditorStyles.centeredGreyMiniLabel);
        }

        private void SelectAndPreview(AssetEntry entry)
        {
            if (entry.asset == null) return;

            _previewTarget = entry.asset;
            if (_previewEditor != null)
                DestroyImmediate(_previewEditor);
            _previewEditor = UnityEditor.Editor.CreateEditor(entry.asset);

            EditorGUIUtility.PingObject(entry.asset);
            Repaint();
        }

        private void SpawnAsset(AssetEntry entry)
        {
            if (entry.asset == null)
            {
                entry.asset = AssetDatabase.LoadMainAssetAtPath(entry.path);
                if (entry.asset == null)
                {
                    Debug.LogError($"{LOG} Failed to load: {entry.path}");
                    return;
                }
            }

            // Determine spawn position
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            if (_spawnAtCamera && SceneView.lastActiveSceneView != null)
            {
                var sv = SceneView.lastActiveSceneView;
                spawnPos = sv.camera.transform.position + sv.camera.transform.forward * 3f;
            }

            // Instantiate
            GameObject instance = PrefabUtility.InstantiatePrefab(entry.asset) as GameObject;
            if (instance == null)
            {
                // FBX -- instantiate as regular object
                instance = Object.Instantiate(entry.asset) as GameObject;
            }

            if (instance == null)
            {
                Debug.LogError($"{LOG} Could not instantiate: {entry.name}");
                return;
            }

            instance.name = entry.name;
            instance.transform.position = spawnPos;
            instance.transform.rotation = spawnRot;
            instance.transform.localScale = Vector3.one * _spawnScale;

            // Register undo
            Undo.RegisterCreatedObjectUndo(instance, $"Spawn {entry.name}");
            Selection.activeGameObject = instance;

            // Frame in scene view
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();

            Debug.Log($"{LOG} Spawned '{entry.name}' at {spawnPos} (scale: {_spawnScale})");
        }
    }
}
#endif
