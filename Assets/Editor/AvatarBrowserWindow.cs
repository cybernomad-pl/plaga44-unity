// =============================================================================
// AvatarBrowserWindow.cs
// CYBERNOMAD -- PLAGA44 Content Editor z 3D preview avatarow i itemow.
// Layout: [List 170px] [3D Preview expand] [Materials 220px]
// Menu: CYBERNOMAD > PLAGA44 Content Editor
// =============================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Plaga44;

namespace Plaga44.Editor
{
    public class AvatarBrowserWindow : EditorWindow
    {
        // Preview
        private PreviewRenderUtility _preview;
        private GameObject _previewInstance;
        private float _previewYaw = 180f;
        private float _previewZoom = 3f;
        private Vector3 _previewCenter;

        // State
        private enum Tab { Avatars, Items }
        private Tab _tab = Tab.Avatars;
        private int _selectedAvatar;
        private int _selectedItem;
        private Vector2 _listScroll;
        private Vector2 _matScroll;

        // Materials
        private bool[] _matFoldouts = new bool[0];
        private Material[] _selectedMaterials = new Material[0];

        // ITEM GRIP -- per-item live edit (tab Items)
        private Plaga44.Inventory.ItemGripConfig _gripCfg;
        private string _gripItemName;

        // Data
        private AvatarRegistry _registry;
        private GameObject[] _itemPrefabs;
        private string[] _itemNames;

        [MenuItem("CYBERNOMAD/PLAGA44 Content Editor", false, 20)]
        public static void Open()
        {
            var w = GetWindow<AvatarBrowserWindow>("PLAGA44 Content Editor");
            w.minSize = new Vector2(700, 450);
            w.Show();
        }

        private void OnEnable()
        {
            _preview = new PreviewRenderUtility();
            _preview.camera.fieldOfView = 30f;
            _preview.camera.nearClipPlane = 0.01f;
            _preview.camera.farClipPlane = 100f;
            _preview.camera.clearFlags = CameraClearFlags.SolidColor;
            _preview.camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            RefreshData();
        }

        private void OnDisable()
        {
            DestroyPreviewInstance();
            _preview?.Cleanup();
            _preview = null;
        }

        // Pelne odswiezenie (button "Refresh"):
        //   1. Mixamo materials extract + URP conversion + Humanoid rig reset
        //   2. Avatar rescan (builds prefabs, rebuilds AvatarRegistry)
        //   3. Item prefabs ensure (Shotgun etc.)
        //   4. Data reload
        private void FullRefresh()
        {
            Plaga44.Editor.Setup.MixamoMaterialExtractor.ExtractAll();
            AvatarAutoImport.ScanAllForce();
            ShotgunPrefabBuilder.EnsurePrefab();
            RefreshData();
        }

        private void RefreshData()
        {
            _registry = AssetDatabase.LoadAssetAtPath<AvatarRegistry>(AvatarImportConfig.RegistryPath);
            var loaded = Resources.LoadAll<GameObject>("Items");
            if (loaded != null && loaded.Length > 0)
            {
                System.Array.Sort(loaded, (a, b) => string.Compare(a.name, b.name));
                _itemPrefabs = loaded;
                _itemNames = new string[loaded.Length];
                for (int i = 0; i < loaded.Length; i++) _itemNames[i] = loaded[i].name;
            }
            else { _itemPrefabs = new GameObject[0]; _itemNames = new string[0]; }
        }

        // =====================================================================
        // Layout: toolbar + 3 columns
        // =====================================================================

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();

            // Column 1: list (fixed 170px)
            DrawListColumn();

            // Column 2: 3D preview (expanding)
            DrawPreviewColumn();

            // Column 3: materials (fixed 220px)
            DrawMaterialColumn();

            EditorGUILayout.EndHorizontal();
        }

        // =====================================================================
        // Toolbar
        // =====================================================================

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var prevTab = _tab;
            if (GUILayout.Toggle(_tab == Tab.Avatars, "Avatars", EditorStyles.toolbarButton)) _tab = Tab.Avatars;
            if (GUILayout.Toggle(_tab == Tab.Items, "Items", EditorStyles.toolbarButton)) _tab = Tab.Items;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton)) FullRefresh();
            EditorGUILayout.EndHorizontal();

            // Tab changed -- switch preview to the currently selected item on new tab
            if (prevTab != _tab)
            {
                DestroyPreviewInstance();
                _selectedMaterials = new Material[0];
                _matFoldouts = new bool[0];
                if (_tab == Tab.Avatars && _registry != null && _selectedAvatar < _registry.Count)
                {
                    var e = _registry.Get(_selectedAvatar);
                    if (e?.prefab != null) LoadPreview(e.prefab);
                }
                else if (_tab == Tab.Items && _itemPrefabs != null && _selectedItem < _itemPrefabs.Length)
                {
                    LoadPreview(_itemPrefabs[_selectedItem]);
                }
            }
        }

        // =====================================================================
        // Column 1: List
        // =====================================================================

        private void DrawListColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(170));
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            if (_tab == Tab.Avatars) DrawAvatarList(); else DrawItemList();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawAvatarList()
        {
            if (_registry == null || _registry.Count == 0)
            { EditorGUILayout.HelpBox("No avatars.\nRescan to import.", MessageType.Info); return; }

            for (int i = 0; i < _registry.Count; i++)
            {
                var e = _registry.Get(i);
                if (e == null) continue;
                bool sel = _selectedAvatar == i;
                GUI.color = e.broken ? Color.red : (sel ? Color.cyan : Color.white);
                if (GUILayout.Button(e.name, sel ? EditorStyles.boldLabel : EditorStyles.label))
                { _selectedAvatar = i; LoadPreview(e.prefab); }
                GUI.color = Color.white;
            }
        }

        private void DrawItemList()
        {
            if (_itemPrefabs.Length == 0)
            { EditorGUILayout.HelpBox("No items.", MessageType.Info); return; }

            for (int i = 0; i < _itemPrefabs.Length; i++)
            {
                bool sel = _selectedItem == i;
                GUI.color = sel ? Color.cyan : Color.white;
                if (GUILayout.Button(_itemNames[i], sel ? EditorStyles.boldLabel : EditorStyles.label))
                { _selectedItem = i; LoadPreview(_itemPrefabs[i]); }
                GUI.color = Color.white;
            }
        }

        // =====================================================================
        // Column 2: 3D Preview (full height, expanding width)
        // =====================================================================

        private void DrawPreviewColumn()
        {
            // Use all remaining space between list and materials
            var rect = GUILayoutUtility.GetRect(200, position.height - 25,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (_preview == null || _previewInstance == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
                EditorGUI.LabelField(rect, "Select to preview", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            HandlePreviewInput(rect);

            _preview.BeginPreview(rect, GUIStyle.none);
            float rad = _previewYaw * Mathf.Deg2Rad;
            Vector3 camDir = new Vector3(Mathf.Sin(rad), 0.25f, Mathf.Cos(rad)).normalized;
            _preview.camera.transform.position = _previewCenter + camDir * _previewZoom;
            _preview.camera.transform.LookAt(_previewCenter);

            if (_preview.lights.Length > 0)
            {
                _preview.lights[0].intensity = 1.4f;
                _preview.lights[0].transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            _preview.Render(true);
            GUI.DrawTexture(rect, _preview.EndPreview());

            // Info overlay
            string info = GetSelectedInfo();
            if (!string.IsNullOrEmpty(info))
                EditorGUI.DropShadowLabel(new Rect(rect.x + 8, rect.y + 8, 260, 80), info, EditorStyles.whiteLabel);
        }

        private void HandlePreviewInput(Rect rect)
        {
            var e = Event.current;
            if (!rect.Contains(e.mousePosition)) return;
            if (e.type == EventType.MouseDrag && e.button == 0)
            { _previewYaw += e.delta.x; e.Use(); Repaint(); }
            if (e.type == EventType.ScrollWheel)
            { _previewZoom = Mathf.Clamp(_previewZoom + e.delta.y * 0.1f, 0.3f, 15f); e.Use(); Repaint(); }
        }

        private string GetSelectedInfo()
        {
            if (_tab == Tab.Avatars && _registry != null)
            {
                var e = _registry.Get(_selectedAvatar);
                if (e == null) return "";
                string s = e.name;
                if (e.prefab != null)
                {
                    var anim = e.prefab.GetComponentInChildren<Animator>(true);
                    if (anim != null) s += $"\nRig: {(anim.isHuman ? "Humanoid" : "Generic")}";
                    var smr = e.prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    if (smr?.sharedMesh != null) s += $"\nVerts: {smr.sharedMesh.vertexCount}";
                    s += $"\nMaterials: {CountMaterials(e.prefab)}";
                }
                if (e.broken) s += $"\nBROKEN: {e.errorMessage}";
                return s;
            }
            if (_tab == Tab.Items && _selectedItem < _itemPrefabs.Length)
            {
                var p = _itemPrefabs[_selectedItem];
                string s = p.name;
                var mf = p.GetComponentInChildren<MeshFilter>(true);
                if (mf?.sharedMesh != null) s += $"\nVerts: {mf.sharedMesh.vertexCount}";
                var rb = p.GetComponent<Rigidbody>();
                if (rb != null) s += $"\nMass: {rb.mass}kg";
                return s;
            }
            return "";
        }

        // =====================================================================
        // Column 3: Material Editor (fixed 220px, accordion foldouts)
        // =====================================================================

        private void DrawMaterialColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(220));

            // ITEM GRIP panel for Items tab -- materials moved to expandable section below
            if (_tab == Tab.Items)
            {
                DrawItemGripPanel();
                EditorGUILayout.Space(6);
            }

            if (_selectedMaterials.Length == 0)
            {
                if (_tab == Tab.Avatars)
                    EditorGUILayout.HelpBox("No materials", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField("Materials", EditorStyles.boldLabel);
            _matScroll = EditorGUILayout.BeginScrollView(_matScroll);

            for (int i = 0; i < _selectedMaterials.Length; i++)
            {
                var mat = _selectedMaterials[i];
                if (mat == null || i >= _matFoldouts.Length) continue;

                _matFoldouts[i] = EditorGUILayout.Foldout(_matFoldouts[i], mat.name, true);
                if (!_matFoldouts[i]) continue;

                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(mat.shader.name, EditorStyles.miniLabel);
                DrawMaterialSliders(mat);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // =====================================================================
        // ITEM GRIP panel -- per-item live grip calibration (Items tab only)
        // =====================================================================

        private void DrawItemGripPanel()
        {
            if (_itemPrefabs == null || _itemPrefabs.Length == 0) return;
            if (_selectedItem < 0 || _selectedItem >= _itemPrefabs.Length) return;

            string itemName = _itemPrefabs[_selectedItem].name;

            // Reload when item changed
            if (_gripItemName != itemName)
            {
                _gripItemName = itemName;
                _gripCfg = Plaga44.Inventory.ItemGripConfig.Load(itemName);
            }

            EditorGUILayout.LabelField($"ITEM GRIP -- {itemName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Per-item offset (PlayerPrefs)", EditorStyles.miniLabel);
            EditorGUILayout.Space(2);

            // Position
            EditorGUILayout.LabelField("Offset Position (m)", EditorStyles.miniBoldLabel);
            _gripCfg.offsetPos.x = EditorGUILayout.Slider("X", _gripCfg.offsetPos.x, -0.2f, 0.2f);
            _gripCfg.offsetPos.y = EditorGUILayout.Slider("Y", _gripCfg.offsetPos.y, -0.2f, 0.2f);
            _gripCfg.offsetPos.z = EditorGUILayout.Slider("Z", _gripCfg.offsetPos.z, -0.2f, 0.2f);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Offset Rotation (deg)", EditorStyles.miniBoldLabel);
            _gripCfg.offsetRotEuler.x = EditorGUILayout.Slider("Pitch", _gripCfg.offsetRotEuler.x, -180f, 180f);
            _gripCfg.offsetRotEuler.y = EditorGUILayout.Slider("Yaw",   _gripCfg.offsetRotEuler.y, -180f, 180f);
            _gripCfg.offsetRotEuler.z = EditorGUILayout.Slider("Roll",  _gripCfg.offsetRotEuler.z, -180f, 180f);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Uniform Scale", EditorStyles.miniBoldLabel);
            _gripCfg.scale = EditorGUILayout.Slider("Scale", _gripCfg.scale, 0.1f, 3.0f);

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save"))
                Plaga44.Inventory.ItemGripConfig.Save(itemName, _gripCfg);
            if (GUILayout.Button("Reload"))
                _gripCfg = Plaga44.Inventory.ItemGripConfig.Load(itemName);
            if (GUILayout.Button("Reset"))
            {
                Plaga44.Inventory.ItemGripConfig.Clear(itemName);
                _gripCfg = Plaga44.Inventory.ItemGripConfig.Default;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMaterialSliders(Material mat)
        {
            DrawColorProp(mat, "_BaseColor", "Color");
            DrawTextureProp(mat, "_BaseMap", "Albedo");
            DrawTextureProp(mat, "_BumpMap", "Normal");
            DrawSliderProp(mat, "_Smoothness", "Smooth", 0f, 1f);
            DrawSliderProp(mat, "_Metallic", "Metal", 0f, 1f);
            DrawSliderProp(mat, "_BumpScale", "Normal Str", 0f, 2f);

            // Specular workflow
            DrawTextureProp(mat, "_SpecGlossMap", "SpecGloss");
            DrawColorProp(mat, "_SpecColor", "Spec Color");
        }

        private static void DrawColorProp(Material mat, string prop, string label)
        {
            if (!mat.HasProperty(prop)) return;
            EditorGUI.BeginChangeCheck();
            var c = EditorGUILayout.ColorField(label, mat.GetColor(prop));
            if (EditorGUI.EndChangeCheck()) { mat.SetColor(prop, c); EditorUtility.SetDirty(mat); }
        }

        private static void DrawTextureProp(Material mat, string prop, string label)
        {
            if (!mat.HasProperty(prop)) return;
            EditorGUI.BeginChangeCheck();
            var t = (Texture2D)EditorGUILayout.ObjectField(label,
                mat.GetTexture(prop), typeof(Texture2D), false, GUILayout.Height(18));
            if (EditorGUI.EndChangeCheck()) { mat.SetTexture(prop, t); EditorUtility.SetDirty(mat); }
        }

        private static void DrawSliderProp(Material mat, string prop, string label, float min, float max)
        {
            if (!mat.HasProperty(prop)) return;
            EditorGUI.BeginChangeCheck();
            float v = EditorGUILayout.Slider(label, mat.GetFloat(prop), min, max);
            if (EditorGUI.EndChangeCheck()) { mat.SetFloat(prop, v); EditorUtility.SetDirty(mat); }
        }

        // =====================================================================
        // Preview instance
        // =====================================================================

        private void LoadPreview(GameObject prefab)
        {
            DestroyPreviewInstance();
            if (prefab == null) return;

            _previewInstance = _preview.InstantiatePrefabInScene(prefab);
            _previewInstance.transform.position = Vector3.zero;
            _previewInstance.transform.rotation = Quaternion.identity;

            var bounds = CalculateBounds(_previewInstance);
            float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxDim > 0.001f)
                _previewInstance.transform.localScale = Vector3.one * (2f / maxDim);

            bounds = CalculateBounds(_previewInstance);
            _previewInstance.transform.position = -bounds.center;
            _previewCenter = Vector3.zero;

            float halfSize = bounds.extents.magnitude;
            _previewZoom = Mathf.Max(halfSize / Mathf.Tan(_preview.camera.fieldOfView * 0.5f * Mathf.Deg2Rad), 0.5f);

            CollectMaterials(prefab);
            Repaint();
        }

        private void CollectMaterials(GameObject prefab)
        {
            var mats = new List<Material>();
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null && !mats.Contains(m)) mats.Add(m);
            _selectedMaterials = mats.ToArray();
            _matFoldouts = new bool[_selectedMaterials.Length];
            if (_matFoldouts.Length > 0) _matFoldouts[0] = true;
        }

        private void DestroyPreviewInstance()
        {
            if (_previewInstance != null) { DestroyImmediate(_previewInstance); _previewInstance = null; }
        }

        private static int CountMaterials(GameObject go)
        {
            var set = new HashSet<Material>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials) if (m != null) set.Add(m);
            return set.Count;
        }

        private static Bounds CalculateBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
#endif
