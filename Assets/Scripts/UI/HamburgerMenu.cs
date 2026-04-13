// =============================================================================
// HamburgerMenu.cs
// CYBERNOMAD -- 3-level VR settings menu for PLAGA '44.
//
// Level 1: Top categories (GAMEPLAY, VISUAL, SYSTEM) -- big tiles
// Level 2: Sub-categories (LOCOMOTION, SHADOWS, etc.) -- grid of tiles
// Level 3: Settings list -- thumbstick adjust values
//
// Controls:
//   Start      = open/close menu
//   Thumbstick = navigate (both sticks work)
//   A / X      = enter / confirm
//   B / Y      = back
//   Triggers   = adjust value +/- (in settings)
//
// Canvas renders in world space, faces the player.
// GameState.Pause() when open, GameState.Resume() on close.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.UI
{
    public class HamburgerMenu : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Menu]";

        // =====================================================================
        // Singleton
        // =====================================================================

        public static HamburgerMenu Instance { get; private set; }
        public static bool MenuOpen { get; private set; }

        // =====================================================================
        // Config
        // =====================================================================

        private const float MENU_DISTANCE = 1.4f;
        private const float CANVAS_SCALE = 0.001f;
        private const int CANVAS_W = 900;
        private const int CANVAS_H = 700;

        // Colors -- dark theme
        private static readonly Color BG_COLOR = new Color(0.08f, 0.08f, 0.08f, 0.92f);
        private static readonly Color BTN_COLOR = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color BTN_SELECTED = new Color(0.20f, 0.35f, 0.55f);
        private static readonly Color TOP_COLOR = new Color(0.25f, 0.25f, 0.25f);
        private static readonly Color TOP_SELECTED = new Color(0.35f, 0.45f, 0.60f);
        private static readonly Color ACCENT = new Color(0.9f, 0.5f, 0.1f);
        private static readonly Color TEXT_WHITE = Color.white;
        private static readonly Color TEXT_GREY = new Color(0.55f, 0.55f, 0.55f);

        // =====================================================================
        // Groups definition
        // =====================================================================

        private static readonly (string name, string[] sections)[] GROUPS = new[]
        {
            ("GAMEPLAY", new[] { "LOCOMOTION", "SMOOTH TURN", "CHAR CTRL", "GAME STATE", "NAVMESH" }),
            ("VISUAL",   new[] { "SHADOWS", "SUN", "FOG", "AMBIENT", "SKYBOX", "BLOOM", "COLOR", "COMFORT", "LGG", "URP" }),
            ("SYSTEM",   new[] { "PROFILE", "MISC", "AUDIO", "PHYSICS", "QUALITY", "CAMERA", "OCULUS", "TERRAIN", "PRESETS" }),
        };

        // =====================================================================
        // State
        // =====================================================================

        private enum MenuLevel { Top, Group, Settings }
        private MenuLevel _level = MenuLevel.Top;

        private string[] _allCategories; // flat from SettingsRegistry
        private Canvas _canvas;
        private OVRCameraRig _rig;

        // Navigation
        private int _topIndex;          // selected group in level 1
        private int _groupIndex;        // selected section in level 2
        private int _settingIndex;      // selected setting in level 3
        private string[] _currentGroupSections; // sections in current group

        // UI roots (destroyed/rebuilt per level)
        private GameObject _contentRoot;
        private Image[] _tileBGs;
        private int _tileCount;

        // Footer
        private Text _titleLabel;
        private Text _footerLabel;
        private Text _footerValue;

        // Settings
        private List<SettingDef> _currentSettings;
        private Text[] _settingTexts;
        private const int VISIBLE_ROWS = 10;

        // Input cooldown
        private float _lastStickTime;
        private const float STICK_COOLDOWN = 0.18f;
        private float _lastTriggerTime;
        private const float TRIGGER_COOLDOWN = 0.2f;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            _rig = FindFirstObjectByType<OVRCameraRig>();
            _allCategories = SettingsRegistry.GetSectionNames();
            BuildCanvas();
            _canvas.gameObject.SetActive(false);
            Debug.Log($"{LOG} Start: {_allCategories.Length} categories, {GROUPS.Length} groups");
        }

        private void Update()
        {
            if (_rig == null) _rig = FindFirstObjectByType<OVRCameraRig>();

            // Start = toggle menu
            if (OVRInput.GetDown(OVRInput.Button.Start))
                Toggle();

            if (!MenuOpen) return;

            // A or X = enter / confirm
            bool enter = OVRInput.GetDown(OVRInput.Button.One) || OVRInput.GetDown(OVRInput.Button.Three);
            // B or Y = back
            bool back = OVRInput.GetDown(OVRInput.Button.Two) || OVRInput.GetDown(OVRInput.Button.Four);

            if (back)
            {
                GoBack();
                return;
            }

            if (enter && _level != MenuLevel.Settings)
            {
                GoForward();
                return;
            }

            HandleNavigation();
        }

        private void OnDestroy()
        {
            if (Instance == this) { MenuOpen = false; Instance = null; }
        }

        // =====================================================================
        // Open / Close
        // =====================================================================

        public void Toggle() { if (MenuOpen) Close(); else Open(); }

        public void Open()
        {
            if (MenuOpen) return;
            PlaceInFrontOfPlayer();
            _canvas.gameObject.SetActive(true);
            MenuOpen = true;
            GameState.Pause();
            _level = MenuLevel.Top;
            _topIndex = 0;
            ShowLevel();
            Debug.Log($"{LOG} OPEN");
        }

        public void Close()
        {
            if (!MenuOpen) return;
            _canvas.gameObject.SetActive(false);
            MenuOpen = false;
            if (GameState.Current == GamePhase.Paused)
                GameState.Resume();
            Debug.Log($"{LOG} CLOSE");
        }

        // =====================================================================
        // Navigation logic
        // =====================================================================

        private void GoForward()
        {
            if (_level == MenuLevel.Top)
            {
                // Enter group -> show sub-tiles
                var group = GROUPS[_topIndex];
                _currentGroupSections = FilterExisting(group.sections);
                if (_currentGroupSections.Length == 0) return;
                _groupIndex = 0;
                _level = MenuLevel.Group;
                ShowLevel();
                Debug.Log($"{LOG} -> Group: {group.name}");
            }
            else if (_level == MenuLevel.Group)
            {
                // Enter section -> show settings
                string section = _currentGroupSections[_groupIndex];
                _currentSettings = SettingsRegistry.GetSettings(section);
                if (_currentSettings.Count == 0)
                {
                    Debug.Log($"{LOG} {section}: no runtime settings");
                    return;
                }
                _settingIndex = 0;
                _level = MenuLevel.Settings;
                ShowLevel();
                Debug.Log($"{LOG} -> Settings: {section} ({_currentSettings.Count})");
            }
        }

        private void GoBack()
        {
            if (_level == MenuLevel.Settings)
            {
                _level = MenuLevel.Group;
                ShowLevel();
                Debug.Log($"{LOG} <- back to group");
            }
            else if (_level == MenuLevel.Group)
            {
                _level = MenuLevel.Top;
                ShowLevel();
                Debug.Log($"{LOG} <- back to top");
            }
            else
            {
                Close();
            }
        }

        private void HandleNavigation()
        {
            Vector2 stickL = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
            Vector2 stickR = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
            Vector2 stick = stickL.sqrMagnitude > stickR.sqrMagnitude ? stickL : stickR;

            if (Time.unscaledTime - _lastStickTime < STICK_COOLDOWN) return;

            if (_level == MenuLevel.Settings)
            {
                HandleSettingsInput(stick);
            }
            else
            {
                HandleTileInput(stick);
            }
        }

        private void HandleTileInput(Vector2 stick)
        {
            int cols = (_level == MenuLevel.Top) ? GROUPS.Length : 4;
            int count = (_level == MenuLevel.Top) ? GROUPS.Length : _currentGroupSections.Length;
            int idx = (_level == MenuLevel.Top) ? _topIndex : _groupIndex;

            int newIdx = idx;
            if (stick.x > 0.5f) newIdx = idx + 1;
            else if (stick.x < -0.5f) newIdx = idx - 1;
            else if (stick.y > 0.5f) newIdx = idx - cols;
            else if (stick.y < -0.5f) newIdx = idx + cols;
            else return;

            if (newIdx < 0 || newIdx >= count) return;
            _lastStickTime = Time.unscaledTime;

            if (_level == MenuLevel.Top) _topIndex = newIdx;
            else _groupIndex = newIdx;

            UpdateTileSelection();
        }

        private void HandleSettingsInput(Vector2 stick)
        {
            // Up/down = select setting
            if (stick.y > 0.5f && _settingIndex > 0)
            {
                _settingIndex--;
                _lastStickTime = Time.unscaledTime;
                UpdateSettingsDisplay();
            }
            else if (stick.y < -0.5f && _settingIndex < _currentSettings.Count - 1)
            {
                _settingIndex++;
                _lastStickTime = Time.unscaledTime;
                UpdateSettingsDisplay();
            }

            // Left/right = adjust value
            if (stick.x > 0.5f) { AdjustSetting(1); _lastStickTime = Time.unscaledTime; }
            else if (stick.x < -0.5f) { AdjustSetting(-1); _lastStickTime = Time.unscaledTime; }

            // Triggers also adjust
            if (Time.unscaledTime - _lastTriggerTime > TRIGGER_COOLDOWN)
            {
                if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch))
                { AdjustSetting(-1); _lastTriggerTime = Time.unscaledTime; }
                if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
                { AdjustSetting(1); _lastTriggerTime = Time.unscaledTime; }
            }
        }

        private void AdjustSetting(int dir)
        {
            if (_settingIndex < 0 || _settingIndex >= _currentSettings.Count) return;
            var s = _currentSettings[_settingIndex];
            float val = Mathf.Clamp(s.get() + s.step * dir, s.min, s.max);
            s.set(val);
            UpdateSettingsDisplay();
        }

        // =====================================================================
        // Show level -- rebuilds content area
        // =====================================================================

        private void ShowLevel()
        {
            if (_contentRoot != null) Destroy(_contentRoot);
            _contentRoot = new GameObject("Content");
            _contentRoot.transform.SetParent(_canvas.transform, false);
            var rt = _contentRoot.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(20, 80);
            rt.offsetMax = new Vector2(-20, -60);

            switch (_level)
            {
                case MenuLevel.Top:
                    BuildTopTiles();
                    _titleLabel.text = "SETTINGS";
                    _footerLabel.text = "";
                    _footerValue.text = "A/X = enter    B/Y = close";
                    break;
                case MenuLevel.Group:
                    BuildGroupTiles();
                    _titleLabel.text = GROUPS[_topIndex].name;
                    _footerLabel.text = "";
                    _footerValue.text = "A/X = enter    B/Y = back";
                    break;
                case MenuLevel.Settings:
                    BuildSettingsUI();
                    _titleLabel.text = _currentGroupSections[_groupIndex];
                    UpdateSettingsDisplay();
                    break;
            }
        }

        // =====================================================================
        // Level 1: Top tiles (GAMEPLAY, VISUAL, SYSTEM)
        // =====================================================================

        private void BuildTopTiles()
        {
            _tileCount = GROUPS.Length;
            _tileBGs = new Image[_tileCount];

            float tileW = 240f;
            float tileH = 120f;
            float spacing = 20f;
            float totalW = _tileCount * tileW + (_tileCount - 1) * spacing;
            float startX = -totalW / 2f + tileW / 2f;

            for (int i = 0; i < _tileCount; i++)
            {
                float x = startX + i * (tileW + spacing);
                var tile = CreateTile(_contentRoot.transform, GROUPS[i].name, x, 100f, tileW, tileH, 20);
                _tileBGs[i] = tile;
            }

            UpdateTileSelection();
        }

        // =====================================================================
        // Level 2: Group tiles (e.g. LOCOMOTION, SMOOTH TURN, etc.)
        // =====================================================================

        private void BuildGroupTiles()
        {
            _tileCount = _currentGroupSections.Length;
            _tileBGs = new Image[_tileCount];

            int cols = 4;
            float tileW = 180f;
            float tileH = 60f;
            float spacing = 10f;
            float gridW = cols * tileW + (cols - 1) * spacing;
            float startX = -gridW / 2f + tileW / 2f;
            float startY = 200f;

            for (int i = 0; i < _tileCount; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float x = startX + col * (tileW + spacing);
                float y = startY - row * (tileH + spacing);
                var tile = CreateTile(_contentRoot.transform, _currentGroupSections[i], x, y, tileW, tileH, 14);
                _tileBGs[i] = tile;
            }

            UpdateTileSelection();
        }

        private Image CreateTile(Transform parent, string label, float x, float y, float w, float h, int fontSize)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);

            var img = go.AddComponent<Image>();
            img.color = (_level == MenuLevel.Top) ? TOP_COLOR : BTN_COLOR;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = new Vector2(6, 0);
            labelRT.offsetMax = new Vector2(-6, 0);
            var txt = labelGO.AddComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.color = TEXT_WHITE;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;

            return img;
        }

        private void UpdateTileSelection()
        {
            int idx = (_level == MenuLevel.Top) ? _topIndex : _groupIndex;
            Color normal = (_level == MenuLevel.Top) ? TOP_COLOR : BTN_COLOR;
            Color selected = (_level == MenuLevel.Top) ? TOP_SELECTED : BTN_SELECTED;

            for (int i = 0; i < _tileCount; i++)
                _tileBGs[i].color = (i == idx) ? selected : normal;

            string name = (_level == MenuLevel.Top) ? GROUPS[_topIndex].name : _currentGroupSections[_groupIndex];
            _footerLabel.text = "> " + name + " <";
        }

        // =====================================================================
        // Level 3: Settings list
        // =====================================================================

        private void BuildSettingsUI()
        {
            _settingTexts = new Text[VISIBLE_ROWS];
            float rowH = 30f;
            for (int i = 0; i < VISIBLE_ROWS; i++)
            {
                var rowGO = new GameObject($"Row_{i}");
                rowGO.transform.SetParent(_contentRoot.transform, false);
                var rowRT = rowGO.AddComponent<RectTransform>();
                rowRT.anchorMin = new Vector2(0, 1);
                rowRT.anchorMax = new Vector2(1, 1);
                rowRT.pivot = new Vector2(0.5f, 1);
                rowRT.sizeDelta = new Vector2(0, rowH);
                rowRT.anchoredPosition = new Vector2(0, -i * (rowH + 2));

                var txt = rowGO.AddComponent<Text>();
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize = 18;
                txt.color = TEXT_WHITE;
                txt.alignment = TextAnchor.MiddleLeft;
                _settingTexts[i] = txt;
            }
        }

        private void UpdateSettingsDisplay()
        {
            if (_settingTexts == null || _currentSettings == null) return;

            int scrollOffset = Mathf.Max(0, _settingIndex - VISIBLE_ROWS + 3);

            for (int i = 0; i < VISIBLE_ROWS; i++)
            {
                int idx = scrollOffset + i;
                if (idx >= _currentSettings.Count) { _settingTexts[i].text = ""; continue; }

                var s = _currentSettings[idx];
                bool sel = (idx == _settingIndex);
                _settingTexts[i].text = $"{(sel ? "> " : "  ")}{s.name}: {s.get().ToString(s.format)}";
                _settingTexts[i].color = sel ? TEXT_WHITE : TEXT_GREY;
            }

            var cur = _currentSettings[_settingIndex];
            _footerLabel.text = cur.desc ?? "";
            _footerValue.text = $"<  {cur.get().ToString(cur.format)}  >    [{cur.min}..{cur.max}]   B/Y = back";
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private string[] FilterExisting(string[] sections)
        {
            var result = new List<string>();
            foreach (var s in sections)
                if (System.Array.IndexOf(_allCategories, s) >= 0)
                    result.Add(s);
            return result.ToArray();
        }

        // =====================================================================
        // Positioning
        // =====================================================================

        private void PlaceInFrontOfPlayer()
        {
            if (_rig != null)
            {
                var head = _rig.centerEyeAnchor;
                Vector3 fwd = head.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
                fwd.Normalize();
                _canvas.transform.position = head.position + fwd * MENU_DISTANCE + Vector3.down * 0.1f;
                _canvas.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
            else
            {
                _canvas.transform.position = new Vector3(0f, 1.5f, MENU_DISTANCE);
                _canvas.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            }
        }

        // =====================================================================
        // Build canvas (once)
        // =====================================================================

        private void BuildCanvas()
        {
            var go = new GameObject("HamburgerMenu_Canvas");
            go.transform.SetParent(transform);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CANVAS_W, CANVAS_H);
            rt.localScale = Vector3.one * CANVAS_SCALE;
            go.AddComponent<GraphicRaycaster>();

            // Background
            var bgGO = new GameObject("BG");
            bgGO.transform.SetParent(go.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            bgGO.AddComponent<Image>().color = BG_COLOR;

            // Title (top bar)
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(go.transform, false);
            var titleRT = titleGO.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1); titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot = new Vector2(0.5f, 1); titleRT.sizeDelta = new Vector2(0, 50);
            _titleLabel = titleGO.AddComponent<Text>();
            _titleLabel.text = "SETTINGS";
            _titleLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _titleLabel.fontSize = 28;
            _titleLabel.color = ACCENT;
            _titleLabel.alignment = TextAnchor.MiddleCenter;
            _titleLabel.fontStyle = FontStyle.Bold;

            // Version info (pod tytulem)
            var verGO = new GameObject("VersionInfo");
            verGO.transform.SetParent(go.transform, false);
            var verRT = verGO.AddComponent<RectTransform>();
            verRT.anchorMin = new Vector2(0, 1); verRT.anchorMax = new Vector2(1, 1);
            verRT.pivot = new Vector2(0.5f, 1); verRT.sizeDelta = new Vector2(0, 20);
            verRT.anchoredPosition = new Vector2(0, -50);
            var verText = verGO.AddComponent<Text>();
            verText.text = GetVersionString();
            verText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            verText.fontSize = 12;
            verText.color = TEXT_GREY;
            verText.alignment = TextAnchor.MiddleCenter;

            // Footer label
            var flGO = new GameObject("FooterLabel");
            flGO.transform.SetParent(go.transform, false);
            var flRT = flGO.AddComponent<RectTransform>();
            flRT.anchorMin = new Vector2(0, 0); flRT.anchorMax = new Vector2(1, 0);
            flRT.pivot = new Vector2(0.5f, 0); flRT.sizeDelta = new Vector2(0, 35);
            flRT.anchoredPosition = new Vector2(0, 40);
            _footerLabel = flGO.AddComponent<Text>();
            _footerLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _footerLabel.fontSize = 20;
            _footerLabel.color = TEXT_WHITE;
            _footerLabel.alignment = TextAnchor.MiddleCenter;

            // Footer value
            var fvGO = new GameObject("FooterValue");
            fvGO.transform.SetParent(go.transform, false);
            var fvRT = fvGO.AddComponent<RectTransform>();
            fvRT.anchorMin = new Vector2(0, 0); fvRT.anchorMax = new Vector2(1, 0);
            fvRT.pivot = new Vector2(0.5f, 0); fvRT.sizeDelta = new Vector2(0, 25);
            fvRT.anchoredPosition = new Vector2(0, 10);
            _footerValue = fvGO.AddComponent<Text>();
            _footerValue.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _footerValue.fontSize = 16;
            _footerValue.color = TEXT_GREY;
            _footerValue.alignment = TextAnchor.MiddleCenter;
        }

        // =====================================================================
        // Version info
        // =====================================================================

        private string GetVersionString()
        {
            var asset = Resources.Load<TextAsset>("BuildInfo");
            if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
            {
                string[] lines = asset.text.Split('\n');
                string branch = lines.Length > 0 ? lines[0].Trim() : "?";
                string time   = lines.Length > 1 ? lines[1].Trim() : "?";
                string hash   = lines.Length > 2 ? lines[2].Trim() : "?";
                return $"{branch} | {time} | {hash}";
            }
            return $"editor | {System.DateTime.Now:yyyy-MM-dd HH:mm} | local";
        }
    }
}
