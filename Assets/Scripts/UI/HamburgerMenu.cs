// =============================================================================
// HamburgerMenu.cs
// CYBERNOMAD -- 3-level VR settings menu PLAGA '44.
// Poziomy: TOP (kafelki kategorii) -> GROUP (sub-kategorie) -> SETTINGS (slider list).
// Kontrolki: Start=toggle, thumbstick=nav, A/X(dolny)=enter/zatwierdz(+haptyka), B/Y(gorny)=back/cofnij, triggery=value +/-.
// World-space canvas, faces player. Menu pauzuje GameState.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.UI
{
    public class HamburgerMenu : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Menu]";

        // ---- Canvas ---------------------------------------------------------
        private const float MENU_DISTANCE = 1.4f;
        private const float CANVAS_SCALE = 0.001f;
        private const int CANVAS_W = 900;
        private const int CANVAS_H = 700;
        private const float CANVAS_DROP = 0.1f;

        // ---- Layout: tiles --------------------------------------------------
        private const float TOP_TILE_W = 240f;
        private const float TOP_TILE_H = 120f;
        private const float TOP_TILE_SPACING = 20f;
        private const float TOP_TILE_Y = 100f;
        private const int TOP_TILE_FONT = 20;

        private const int GROUP_COLS = 4;
        private const float GROUP_TILE_W = 180f;
        private const float GROUP_TILE_H = 60f;
        private const float GROUP_TILE_SPACING = 10f;
        private const float GROUP_TILE_START_Y = 200f;
        private const int GROUP_TILE_FONT = 14;

        // ---- Layout: settings list -----------------------------------------
        private const int VISIBLE_ROWS = 10;
        private const float ROW_HEIGHT = 30f;
        private const float ROW_GAP = 2f;
        private const int ROW_FONT = 18;

        // ---- Layout: chrome (title, version, footer) -----------------------
        private const float TITLE_HEIGHT = 50f;
        private const int TITLE_FONT = 28;
        private const float VERSION_HEIGHT = 20f;
        private const int VERSION_FONT = 12;
        private const float FOOTER_LABEL_HEIGHT = 35f;
        private const float FOOTER_LABEL_Y = 40f;
        private const int FOOTER_LABEL_FONT = 20;
        private const float FOOTER_VALUE_HEIGHT = 25f;
        private const float FOOTER_VALUE_Y = 10f;
        private const int FOOTER_VALUE_FONT = 16;
        private const float CONTENT_PAD_X = 20f;
        private const float CONTENT_PAD_TOP = 80f;
        private const float CONTENT_PAD_BOTTOM = 60f;
        private const float TILE_LABEL_PAD = 6f;

        // ---- Input ----------------------------------------------------------
        private const float STICK_COOLDOWN = 0.18f;
        private const float STICK_THRESHOLD = 0.5f;
        private const float TRIGGER_REPEAT_INITIAL = 0.2f;
        private const float TRIGGER_REPEAT_MIN = 0.05f;
        private const float TRIGGER_ACCEL_TIME = 1f;

        // ---- Section names (routing) ---------------------------------------
        private const string AvatarSection = "AVATAR";
        private const string ItemsSection = "ITEMS";
        private const string NpcSection = "NPC";

        // ---- Colors (dark theme) -------------------------------------------
        private static readonly Color BG_COLOR = new Color(0f, 0f, 0f, 0f);
        private static readonly Color BTN_COLOR = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color BTN_SELECTED = new Color(0.20f, 0.35f, 0.55f);
        private static readonly Color TOP_COLOR = new Color(0.25f, 0.25f, 0.25f);
        private static readonly Color TOP_SELECTED = new Color(0.35f, 0.45f, 0.60f);
        private static readonly Color ACCENT = new Color(0.9f, 0.5f, 0.1f);
        private static readonly Color TEXT_WHITE = Color.white;
        private static readonly Color TEXT_GREY = new Color(0.9f, 0.9f, 0.9f); // was 0.55 -- too dark on transparent BG

        // ---- Groups (TOP-level) --------------------------------------------
        private static readonly (string name, string[] sections)[] GROUPS = new[]
        {
            ("GAMEPLAY", new[] { "LOCOMOTION", "SMOOTH TURN", "CHAR CTRL", "AVATAR", "ITEMS", "ITEM GRIP", "NPC", "GAME STATE", "NAVMESH" }),
            ("VISUAL",   new[] { "SHADOWS", "SUN", "FOG", "AMBIENT", "SKYBOX", "BLOOM", "COLOR", "COMFORT", "LGG", "URP" }),
            ("SYSTEM",   new[] { "PROFILE", "MISC", "AUDIO", "PHYSICS", "QUALITY", "CAMERA", "OCULUS", "TERRAIN", "EXIT" }),
        };

        // =====================================================================
        // Singleton + public state
        // =====================================================================

        public static HamburgerMenu Instance { get; private set; }
        public static bool MenuOpen { get; private set; }

        // =====================================================================
        // State
        // =====================================================================

        private enum MenuLevel { Top, Group, Settings }
        private MenuLevel _level = MenuLevel.Top;

        private string[] _allCategories;
        private Canvas _canvas;
        private OVRCameraRig _rig;

        // Navigation
        private int _topIndex;
        private int _groupIndex;
        private int _settingIndex;
        private string[] _currentGroupSections;

        // UI content (re-created per level)
        private GameObject _contentRoot;
        private Image[] _tileBGs;
        private int _tileCount;

        private Text _titleLabel;
        private Text _footerLabel;
        private Text _footerValue;

        private List<SettingDef> _currentSettings;
        private string _activeSectionName;
        private Text[] _settingTexts;

        // Input timing
        private float _lastStickTime;
        private float _lastTriggerTime;
        private float _triggerHoldStart;

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

            if (OVRInput.GetDown(OVRInput.Button.Start)) Toggle();
            if (!MenuOpen) return;

            if (PressedBack()) { GoBack(); return; }
            if (PressedEnter())
            {
                // W Settings ENTER wykonuje akcje / przelacza toggle 0<->1 biezacej pozycji.
                // Wyzej (Top/Group) ENTER wchodzi glebiej w menu.
                if (_level == MenuLevel.Settings) { ActivateCurrentSetting(); return; }
                GoForward(); return;
            }

            HandleNavigation();
        }

        private void OnDestroy()
        {
            if (Instance == this) { MenuOpen = false; Instance = null; }
        }

        private void OnApplicationQuit() => SettingsRegistry.FlushPlayerPrefs();
        private void OnApplicationPause(bool paused) { if (paused) SettingsRegistry.FlushPlayerPrefs(); }

        // DOLNY przycisk = ENTER/ZATWIERDZ, GORNY = BACK/COFNIJ (zamienione per zyczenie).
        // RawButton jawnie per fizyczny przycisk (nie virtual One/Two, ktore zaleza od hand):
        //   A = dolny prawy, X = dolny lewy  -> ENTER (zatwierdz, + haptyka)
        //   B = gorny prawy, Y = gorny lewy  -> BACK (cofnij, bez haptyki)
        private static bool PressedEnter()
            => OVRInput.GetDown(OVRInput.RawButton.A) || OVRInput.GetDown(OVRInput.RawButton.X);

        private static bool PressedBack()
            => OVRInput.GetDown(OVRInput.RawButton.B) || OVRInput.GetDown(OVRInput.RawButton.Y);

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

            // Issue #157: force gallery spawn on menu open so preview is visible even mid-flight.
            var gallery = Plaga44.AvatarGallery.Instance;
            if (gallery != null) gallery.ForceSpawnNow();

            Debug.Log($"{LOG} OPEN");
            // Event-driven world-save (#196): wejscie do menu.
            Plaga44.WorldSaveManager.Instance?.Save("menu-open");
        }

        public void Close()
        {
            if (!MenuOpen) return;
            _canvas.gameObject.SetActive(false);
            MenuOpen = false;
            if (GameState.Current == GamePhase.Paused) GameState.Resume();
            // Flush biezacych ustawien na dysk -- slidery juz zapisaly PlayerPrefs.SetFloat, brakuje Save().
            SettingsRegistry.FlushPlayerPrefs();

            // Issue #158: clean up preview objects so they don't linger in the world.
            var gallery = Plaga44.AvatarGallery.Instance;
            if (gallery != null) gallery.HideAllPreviews();
            var items = Plaga44.ItemBrowser.Instance;
            if (items != null) items.ConfirmSpawn(); // item ZOSTAJE w scenie (nie niszcz preview)

            Debug.Log($"{LOG} CLOSE");
            // Event-driven world-save (#196): wyjscie z menu (po despawnie preview).
            Plaga44.WorldSaveManager.Instance?.Save("menu-close");
        }

        // =====================================================================
        // Navigation logic
        // =====================================================================

        private void GoForward()
        {
            // Haptyka na ZATWIERDZ (dolny A/X) -- w rece ktora wcisnela.
            // A = prawy kontroler, X = lewy. GetDown jeszcze true w tej samej klatce co PressedEnter.
            var ctrl = OVRInput.GetDown(OVRInput.RawButton.A)
                ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;
            Plaga44.Feedback.HapticManager.Instance?.PlayCustom(ctrl, 0.6f, 0.7f, 0.05f, "menu-enter");

            switch (_level)
            {
                case MenuLevel.Top: EnterGroup(); break;
                case MenuLevel.Group: EnterSettings(); break;
            }
        }

        private void EnterGroup()
        {
            var group = GROUPS[_topIndex];
            _currentGroupSections = FilterExisting(group.sections);
            if (_currentGroupSections.Length == 0) return;
            _groupIndex = 0;
            _level = MenuLevel.Group;
            ShowLevel();
            Debug.Log($"{LOG} -> Group: {group.name}");
        }

        private void EnterSettings()
        {
            string section = _currentGroupSections[_groupIndex];
            _activeSectionName = section;
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

        private void GoBack()
        {
            switch (_level)
            {
                case MenuLevel.Settings:
                    OnLeaveSection(_activeSectionName);
                    SettingsRegistry.FlushPlayerPrefs();
                    _level = MenuLevel.Group; ShowLevel(); Debug.Log($"{LOG} <- back to group (auto-saved)"); break;
                case MenuLevel.Group: _level = MenuLevel.Top; ShowLevel(); Debug.Log($"{LOG} <- back to top"); break;
                default: Close(); break;
            }
        }

        private void HandleNavigation()
        {
            // Spust = nawigacja pozycji w Settings. Ma WLASNY throttle (_lastTriggerTime),
            // wiec musi byc poza stick-cooldownem -- inaczej ruch sticka blokowalby spust.
            if (_level == MenuLevel.Settings) UpdateSettingsSelectionByTriggers();

            Vector2 stick = GetStrongerThumbstick();
            if (Time.unscaledTime - _lastStickTime < STICK_COOLDOWN) return;

            if (_level == MenuLevel.Settings) HandleSettingsInput(stick);
            else HandleTileInput(stick);
        }

        private static Vector2 GetStrongerThumbstick()
        {
            var l = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
            var r = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
            return l.sqrMagnitude > r.sqrMagnitude ? l : r;
        }

        private void HandleTileInput(Vector2 stick)
        {
            int cols = (_level == MenuLevel.Top) ? GROUPS.Length : GROUP_COLS;
            int count = (_level == MenuLevel.Top) ? GROUPS.Length : _currentGroupSections.Length;
            int idx = (_level == MenuLevel.Top) ? _topIndex : _groupIndex;

            if (!TryMoveIndex(stick, idx, cols, count, out int newIdx)) return;
            _lastStickTime = Time.unscaledTime;

            if (_level == MenuLevel.Top) _topIndex = newIdx;
            else _groupIndex = newIdx;

            UpdateTileSelection();
        }

        private static bool TryMoveIndex(Vector2 stick, int current, int cols, int count, out int next)
        {
            next = current;
            if (stick.x > STICK_THRESHOLD) next = current + 1;
            else if (stick.x < -STICK_THRESHOLD) next = current - 1;
            else if (stick.y > STICK_THRESHOLD) next = current - cols;
            else if (stick.y < -STICK_THRESHOLD) next = current + cols;
            else return false;
            return next >= 0 && next < count;
        }

        private void HandleSettingsInput(Vector2 stick)
        {
            UpdateSettingsSelection(stick);     // stick Y = wybor pozycji
            UpdateSettingsValueByStick(stick);  // stick X = zmiana wartosci (nie toggle/akcja)
            // Spust (nawigacja pozycji) obslugiwany w HandleNavigation, poza stick-cooldownem.
        }

        private void UpdateSettingsSelection(Vector2 stick)
        {
            if (stick.y > STICK_THRESHOLD && _settingIndex > 0)
            {
                _settingIndex--; _lastStickTime = Time.unscaledTime; UpdateSettingsDisplay();
            }
            else if (stick.y < -STICK_THRESHOLD && _settingIndex < _currentSettings.Count - 1)
            {
                _settingIndex++; _lastStickTime = Time.unscaledTime; UpdateSettingsDisplay();
            }
        }

        private void UpdateSettingsValueByStick(Vector2 stick)
        {
            // Toggle/akcja (0..1) NIE reaguja na stick -- zatwierdzenie tylko ENTER (A/X).
            // Chroni przed przypadkowym przelaczeniem i (dla akcji) przed wyzwoleniem ze sticka.
            if (_settingIndex >= 0 && _settingIndex < _currentSettings.Count
                && IsEnterActivated(_currentSettings[_settingIndex], _activeSectionName)) return;

            if (stick.x > STICK_THRESHOLD) { AdjustSetting(1); _lastStickTime = Time.unscaledTime; }
            else if (stick.x < -STICK_THRESHOLD) { AdjustSetting(-1); _lastStickTime = Time.unscaledTime; }
        }

        // Spust L/R = poprzednia/nastepna POZYCJA w liscie ustawien (nie wartosc!).
        // Przytrzymanie = przyspieszajace przewijanie listy. NIGDY nie zmienia wartosci
        // ani nie wyzwala akcji -- to eliminuje "100 pinei" (spust trzymany na Spawn Pinea).
        private void UpdateSettingsSelectionByTriggers()
        {
            bool trigL = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
            bool trigR = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);

            if (!trigL && !trigR)
            {
                _triggerHoldStart = Time.unscaledTime;
                return;
            }

            float holdTime = Time.unscaledTime - _triggerHoldStart;
            float repeatRate = Mathf.Lerp(TRIGGER_REPEAT_INITIAL, TRIGGER_REPEAT_MIN,
                Mathf.Clamp01(holdTime / TRIGGER_ACCEL_TIME));
            if (Time.unscaledTime - _lastTriggerTime <= repeatRate) return;

            if (trigL) MoveSettingIndex(-1);
            if (trigR) MoveSettingIndex(1);
            _lastTriggerTime = Time.unscaledTime;
        }

        private void AdjustSetting(int dir)
        {
            if (_settingIndex < 0 || _settingIndex >= _currentSettings.Count) return;
            var s = _currentSettings[_settingIndex];
            float val = Mathf.Clamp(s.get() + s.step * dir, s.min, s.max);
            s.set(val);
            UpdateSettingsDisplay();
        }

        // Przesuwa wybor pozycji w liscie ustawien (spust L/R). Clamp do granic listy.
        private void MoveSettingIndex(int dir)
        {
            int n = _currentSettings.Count;
            if (n == 0) return;
            int next = Mathf.Clamp(_settingIndex + dir, 0, n - 1);
            if (next == _settingIndex) return;
            _settingIndex = next;
            UpdateSettingsDisplay();
        }

        // Pozycja typu 0..1 (step 1) = toggle LUB akcja -- zatwierdzana ENTEREM (A/X),
        // nie stickiem/spustem. Wartosci ciagle i multi-wybor (max>1) tu nie wchodza.
        // WYJATEK: multi-wybory galerii (NPC/avatar/item/animacja) ZAWSZE ida na stick,
        // nawet gdy zakres akurat 0..1 (np. 2 NPC) -- inaczej stick przestalby dzialac.
        private static bool IsEnterActivated(SettingDef s, string section)
        {
            if (section == NpcSection && (s.name == "NPC" || s.name == "Animacja")) return false;
            if (section == AvatarSection && s.name == "Mode") return false;
            if (section == ItemsSection && s.name == "Item") return false;

            return Mathf.Approximately(s.min, 0f)
                && Mathf.Approximately(s.max, 1f)
                && Mathf.Approximately(s.step, 1f);
        }

        // ENTER (A/X) w Settings: toggle przelacza 0<->1, akcja (getter=0) wyzwala set(1).
        // Wartosci ciagle / multi-wybor / read-only -> ENTER nic nie robi.
        private void ActivateCurrentSetting()
        {
            if (_settingIndex < 0 || _settingIndex >= _currentSettings.Count) return;
            var s = _currentSettings[_settingIndex];
            if (!IsEnterActivated(s, _activeSectionName)) return;

            float newVal = s.get() > 0.5f ? 0f : 1f;
            s.set(newVal);

            var ctrl = OVRInput.GetDown(OVRInput.RawButton.A)
                ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch;
            Plaga44.Feedback.HapticManager.Instance?.PlayCustom(ctrl, 0.6f, 0.7f, 0.05f, "menu-enter");
            UpdateSettingsDisplay();
        }

        // =====================================================================
        // Level rendering
        // =====================================================================

        private void ShowLevel()
        {
            RebuildContentRoot();
            switch (_level)
            {
                case MenuLevel.Top: ShowTopLevel(); break;
                case MenuLevel.Group: ShowGroupLevel(); break;
                case MenuLevel.Settings: ShowSettingsLevel(); break;
            }
        }

        private void RebuildContentRoot()
        {
            if (_contentRoot != null) Destroy(_contentRoot);
            _contentRoot = new GameObject("Content");
            _contentRoot.transform.SetParent(_canvas.transform, false);
            var rt = _contentRoot.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(CONTENT_PAD_X, CONTENT_PAD_TOP);
            rt.offsetMax = new Vector2(-CONTENT_PAD_X, -CONTENT_PAD_BOTTOM);
        }

        private void ShowTopLevel()
        {
            BuildTopTiles();
            _titleLabel.text = "SETTINGS";
            _footerLabel.text = "";
            _footerValue.text = "B/Y = enter    A/X = close";
        }

        private void ShowGroupLevel()
        {
            BuildGroupTiles();
            _titleLabel.text = GROUPS[_topIndex].name;
            _footerLabel.text = "";
            _footerValue.text = "B/Y = enter    A/X = back";
        }

        private void ShowSettingsLevel()
        {
            BuildSettingsUI();
            _titleLabel.text = _currentGroupSections[_groupIndex];
            UpdateSettingsDisplay();
        }

        // =====================================================================
        // Top tiles
        // =====================================================================

        private void BuildTopTiles()
        {
            _tileCount = GROUPS.Length;
            _tileBGs = new Image[_tileCount];

            float totalW = _tileCount * TOP_TILE_W + (_tileCount - 1) * TOP_TILE_SPACING;
            float startX = -totalW / 2f + TOP_TILE_W / 2f;

            for (int i = 0; i < _tileCount; i++)
            {
                float x = startX + i * (TOP_TILE_W + TOP_TILE_SPACING);
                _tileBGs[i] = CreateTile(_contentRoot.transform, GROUPS[i].name, x, TOP_TILE_Y, TOP_TILE_W, TOP_TILE_H, TOP_TILE_FONT);
            }
            UpdateTileSelection();
        }

        // =====================================================================
        // Group tiles
        // =====================================================================

        private void BuildGroupTiles()
        {
            _tileCount = _currentGroupSections.Length;
            _tileBGs = new Image[_tileCount];

            float gridW = GROUP_COLS * GROUP_TILE_W + (GROUP_COLS - 1) * GROUP_TILE_SPACING;
            float startX = -gridW / 2f + GROUP_TILE_W / 2f;

            for (int i = 0; i < _tileCount; i++)
            {
                int col = i % GROUP_COLS;
                int row = i / GROUP_COLS;
                float x = startX + col * (GROUP_TILE_W + GROUP_TILE_SPACING);
                float y = GROUP_TILE_START_Y - row * (GROUP_TILE_H + GROUP_TILE_SPACING);
                _tileBGs[i] = CreateTile(_contentRoot.transform, _currentGroupSections[i], x, y, GROUP_TILE_W, GROUP_TILE_H, GROUP_TILE_FONT);
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

            CreateTileLabel(go.transform, label, fontSize);
            return img;
        }

        private static void CreateTileLabel(Transform parent, string label, int fontSize)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(TILE_LABEL_PAD, 0);
            rt.offsetMax = new Vector2(-TILE_LABEL_PAD, 0);

            var txt = go.AddComponent<Text>();
            txt.text = label;
            txt.font = LegacyFont();
            txt.fontSize = fontSize;
            txt.color = TEXT_WHITE;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
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
        // Settings list
        // =====================================================================

        private void BuildSettingsUI()
        {
            _settingTexts = new Text[VISIBLE_ROWS];
            for (int i = 0; i < VISIBLE_ROWS; i++)
                _settingTexts[i] = CreateSettingRow(i);
        }

        private Text CreateSettingRow(int rowIdx)
        {
            var go = new GameObject($"Row_{rowIdx}");
            go.transform.SetParent(_contentRoot.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, ROW_HEIGHT);
            rt.anchoredPosition = new Vector2(0, -rowIdx * (ROW_HEIGHT + ROW_GAP));

            var txt = go.AddComponent<Text>();
            txt.font = LegacyFont();
            txt.fontSize = ROW_FONT;
            txt.color = TEXT_WHITE;
            txt.alignment = TextAnchor.MiddleLeft;
            return txt;
        }

        private void UpdateSettingsDisplay()
        {
            if (_settingTexts == null || _currentSettings == null) return;

            int scrollOffset = Mathf.Max(0, _settingIndex - VISIBLE_ROWS + 3);
            string section = _currentGroupSections[_groupIndex];
            var ctx = new RowContext(section);

            RenderRows(scrollOffset, ctx);
            RenderFooterForSelection(ctx);
        }

        private void RenderRows(int scrollOffset, RowContext ctx)
        {
            for (int i = 0; i < VISIBLE_ROWS; i++)
            {
                int idx = scrollOffset + i;
                if (idx >= _currentSettings.Count) { _settingTexts[i].text = ""; continue; }

                var s = _currentSettings[idx];
                bool sel = (idx == _settingIndex);
                (string line, Color color) = FormatSettingRow(s, sel, ctx);
                _settingTexts[i].text = line;
                _settingTexts[i].color = color;
            }
        }

        private void RenderFooterForSelection(RowContext ctx)
        {
            var cur = _currentSettings[_settingIndex];
            bool broken = ctx.IsBrokenAvatarMode(cur);

            _footerLabel.text = cur.desc ?? "";
            _footerLabel.color = broken ? Color.red : TEXT_WHITE;

            _footerValue.text = BuildFooterValue(cur, ctx);
            _footerValue.color = broken ? Color.red : TEXT_GREY;
        }

        private static (string line, Color color) FormatSettingRow(SettingDef s, bool selected, RowContext ctx)
        {
            string prefix = selected ? "> " : "  ";
            Color color = selected ? TEXT_WHITE : TEXT_GREY;

            if (ctx.IsAvatarMode(s))
            {
                color = ctx.Player.IsCurrentBroken ? Color.red : color;
                return ($"{prefix}{s.name}: {ctx.Player.CurrentLabel}", color);
            }
            if (ctx.IsItemMode(s))
                return ($"{prefix}{s.name}: {ctx.Browser.CurrentLabel}", color);
            if (ctx.IsNpcAnim(s))
                return ($"{prefix}{s.name}: {Plaga44.Npc.NpcMenuSection.CurrentAnimLabel}", color);
            if (ctx.IsNpcSelect(s))
                return ($"{prefix}{s.name}: {Plaga44.Npc.NpcMenuSection.SelectedNpcLabel}", color);
            return ($"{prefix}{s.name}: {s.get().ToString(s.format)}", color);
        }

        private static string BuildFooterValue(SettingDef cur, RowContext ctx)
        {
            if (ctx.IsAvatarMode(cur))
                return $"<  {ctx.Player.CurrentLabel}  >    [{cur.min}..{cur.max}]   A/X = back";
            if (ctx.IsItemMode(cur))
                return $"<  {ctx.Browser.CurrentLabel}  >    [{cur.min}..{cur.max}]   A/X = back";
            if (ctx.IsNpcAnim(cur))
                return $"<  {Plaga44.Npc.NpcMenuSection.CurrentAnimLabel}  >    [{cur.min}..{cur.max}]   A/X = back";
            if (ctx.IsNpcSelect(cur))
                return $"<  {Plaga44.Npc.NpcMenuSection.SelectedNpcLabel}  >   Spawn = ENTER na 'Spawn'";
            return $"<  {cur.get().ToString(cur.format)}  >    [{cur.min}..{cur.max}]   A/X = back";
        }

        /// <summary>Kontekst per-render -- raz wyliczony section/avatar ptr zamiast 3x field lookupy.</summary>
        private readonly struct RowContext
        {
            public readonly string Section;
            public readonly bool IsAvatarSection;
            public readonly bool IsItemsSection;
            public readonly bool IsNpcSection;
            public readonly Plaga44.PlayerAvatar Player;
            public readonly Plaga44.ItemBrowser Browser;

            public RowContext(string section)
            {
                Section = section;
                IsAvatarSection = section == AvatarSection;
                IsItemsSection = section == ItemsSection;
                IsNpcSection = section == NpcSection;
                Player = IsAvatarSection ? Plaga44.PlayerAvatar.FindCurrent() : null;
                Browser = IsItemsSection ? Plaga44.ItemBrowser.Instance : null;
            }

            public bool IsAvatarMode(SettingDef s) => IsAvatarSection && s.name == "Mode" && Player != null;
            public bool IsBrokenAvatarMode(SettingDef s) => IsAvatarMode(s) && Player.IsCurrentBroken;
            public bool IsItemMode(SettingDef s) => IsItemsSection && s.name == "Item" && Browser != null;
            public bool IsNpcAnim(SettingDef s) => IsNpcSection && s.name == "Animacja";
            public bool IsNpcSelect(SettingDef s) => IsNpcSection && s.name == "NPC";
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>Called when leaving a settings section. Confirms previews.</summary>
        private static void OnLeaveSection(string section)
        {
            if (section == AvatarSection)
            {
                var avatar = Plaga44.PlayerAvatar.FindCurrent();
                if (avatar != null) avatar.ConfirmPreview();
            }
            else if (section == ItemsSection)
            {
                // Opuszczenie sekcji ITEMS (GoBack) zatwierdza item -- zostaje w scenie
                Plaga44.ItemBrowser.Instance?.ConfirmSpawn();
            }
        }

        /// <summary>Adds Outline + Shadow to a UI text object for readability on transparent BG.</summary>
        private static void AddTextShadow(GameObject go)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(2f, -2f);
        }

        private string[] FilterExisting(string[] sections)
        {
            var result = new List<string>();
            foreach (var s in sections)
                if (System.Array.IndexOf(_allCategories, s) >= 0)
                    result.Add(s);
            return result.ToArray();
        }

        private void PlaceInFrontOfPlayer()
        {
            if (_rig != null) PlaceInFrontOfRig(_rig.centerEyeAnchor);
            else PlaceFallback();
        }

        private void PlaceInFrontOfRig(Transform head)
        {
            Vector3 fwd = head.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();
            _canvas.transform.position = head.position + fwd * MENU_DISTANCE + Vector3.down * CANVAS_DROP;
            _canvas.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        private void PlaceFallback()
        {
            _canvas.transform.position = new Vector3(0f, 1.5f, MENU_DISTANCE);
            _canvas.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
        }

        // =====================================================================
        // Canvas build (once)
        // =====================================================================

        private void BuildCanvas()
        {
            var root = CreateCanvasRoot();
            CreateBackground(root);
            _titleLabel = CreateTitleLabel(root);
            CreateVersionLabel(root);
            _footerLabel = CreateFooterLabel(root);
            _footerValue = CreateFooterValue(root);
            CreateNotifier(root);
        }

        private GameObject CreateCanvasRoot()
        {
            var go = new GameObject("HamburgerMenu_Canvas");
            go.transform.SetParent(transform);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CANVAS_W, CANVAS_H);
            rt.localScale = Vector3.one * CANVAS_SCALE;
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        private static void CreateBackground(GameObject parent)
        {
            var go = new GameObject("BG");
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = BG_COLOR;
        }

        private static Text CreateTitleLabel(GameObject parent)
        {
            var rt = CreateAnchoredTopRow(parent, "Title", TITLE_HEIGHT, 0);
            var txt = rt.gameObject.AddComponent<Text>();
            txt.text = "SETTINGS";
            txt.font = LegacyFont();
            txt.fontSize = TITLE_FONT;
            txt.color = ACCENT;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontStyle = FontStyle.Bold;
            AddTextShadow(rt.gameObject);
            return txt;
        }

        private static void CreateVersionLabel(GameObject parent)
        {
            var rt = CreateAnchoredTopRow(parent, "VersionInfo", VERSION_HEIGHT, -TITLE_HEIGHT);
            var txt = rt.gameObject.AddComponent<Text>();
            txt.text = GetVersionString();
            txt.font = LegacyFont();
            txt.fontSize = VERSION_FONT;
            txt.color = TEXT_GREY;
            txt.alignment = TextAnchor.MiddleCenter;
        }

        private static Text CreateFooterLabel(GameObject parent)
        {
            var rt = CreateAnchoredBottomRow(parent, "FooterLabel", FOOTER_LABEL_HEIGHT, FOOTER_LABEL_Y);
            var txt = rt.gameObject.AddComponent<Text>();
            txt.font = LegacyFont();
            txt.fontSize = FOOTER_LABEL_FONT;
            txt.color = TEXT_WHITE;
            txt.alignment = TextAnchor.MiddleCenter;
            AddTextShadow(rt.gameObject);
            return txt;
        }

        private static Text CreateFooterValue(GameObject parent)
        {
            var rt = CreateAnchoredBottomRow(parent, "FooterValue", FOOTER_VALUE_HEIGHT, FOOTER_VALUE_Y);
            var txt = rt.gameObject.AddComponent<Text>();
            txt.font = LegacyFont();
            txt.fontSize = FOOTER_VALUE_FONT;
            txt.color = TEXT_GREY;
            txt.alignment = TextAnchor.MiddleCenter;
            AddTextShadow(rt.gameObject);
            return txt;
        }

        private static void CreateNotifier(GameObject parent)
        {
            var go = new GameObject("Notifier");
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<MenuNotifier>();
        }

        private static RectTransform CreateAnchoredTopRow(GameObject parent, string name, float height, float offsetY)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, height);
            rt.anchoredPosition = new Vector2(0, offsetY);
            return rt;
        }

        private static RectTransform CreateAnchoredBottomRow(GameObject parent, string name, float height, float offsetY)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(0, height);
            rt.anchoredPosition = new Vector2(0, offsetY);
            return rt;
        }

        private static Font LegacyFont() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // =====================================================================
        // Version info (Resources/BuildInfo.txt: line0=branch line1=time line2=hash)
        // =====================================================================

        private static string GetVersionString()
        {
            var asset = Resources.Load<TextAsset>("BuildInfo");
            if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
            {
                string[] lines = asset.text.Split('\n');
                string branch = lines.Length > 0 ? lines[0].Trim() : "?";
                string time = lines.Length > 1 ? lines[1].Trim() : "?";
                string hash = lines.Length > 2 ? lines[2].Trim() : "?";
                return $"{branch} | {time} | {hash}";
            }
            return $"editor | {System.DateTime.Now:yyyy-MM-dd HH:mm} | local";
        }
    }
}
