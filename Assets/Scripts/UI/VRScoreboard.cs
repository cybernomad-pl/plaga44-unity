// VRScoreboard.cs
// CYBERNOMAD -- Compact floating scoreboard near the player.
// Tracks: Stones thrown, Hits, Accuracy, Current streak, Best streak.
// Updates in real-time via HitDetector/HitTarget events.
// Positioned in the player's peripheral view (right-forward at eye level).
//
// Requires: com.meta.xr.sdk.core (HAS_META_XR define)

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Plaga44.Gameplay;

namespace Plaga44.UI
{
    public class VRScoreboard : MonoBehaviour
    {
        // ---- Public API ----

        public static VRScoreboard Instance { get; private set; }

        // Call when a stone is released/thrown
        public void RecordThrow()
        {
            _totalThrows++;
            RefreshDisplay();
        }

        // Call when a stone hits a target
        public void RecordHit(bool isHeadshot = false)
        {
            _totalHits++;
            _currentStreak++;
            if (_currentStreak > _bestStreak) _bestStreak = _currentStreak;
            if (isHeadshot) _headshots++;
            RefreshDisplay();
        }

        // Call when a stone misses (hits non-target surface)
        public void RecordMiss()
        {
            _currentStreak = 0;
            RefreshDisplay();
        }

        // Reset all stats
        public void ResetStats()
        {
            _totalThrows = 0;
            _totalHits   = 0;
            _currentStreak = 0;
            _bestStreak  = 0;
            _headshots   = 0;
            RefreshDisplay();
        }

        // ---- Config ----

        private const float CANVAS_SCALE = 0.001f;
        private const int   CANVAS_W     = 240;
        private const int   CANVAS_H     = 200;

        // Offset from player position (right-forward, eye level)
        private static readonly Vector3 BOARD_OFFSET = new Vector3(0.5f, 0.0f, 0.8f);

        // Colours
        private static readonly Color BG_COLOR   = new Color(0.10f, 0.10f, 0.10f, 0.75f);
        private static readonly Color ACCENT      = new Color(1.00f, 0.42f, 0.21f, 1.00f);
        private static readonly Color TEXT_WHITE  = Color.white;
        private static readonly Color TEXT_GREY   = new Color(0.55f, 0.55f, 0.55f, 1.00f);
        private static readonly Color GOLD        = new Color(1.00f, 0.84f, 0.00f, 1.00f);

        // ---- Stats ----

        private int _totalThrows;
        private int _totalHits;
        private int _currentStreak;
        private int _bestStreak;
        private int _headshots;

        // ---- Private ----

#if HAS_META_XR
        private OVRCameraRig _rig;
#endif

        private Canvas _canvas;
        private Text _throwsVal, _hitsVal, _accVal, _streakVal, _bestVal;

        private List<HitTarget> _registeredTargets = new List<HitTarget>();

        // ---- Lifecycle ----

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            BuildCanvas();
            RefreshDisplay();
            // Auto-register any HitTargets already in scene
            RegisterAllTargets();
        }

        private void Update()
        {
#if HAS_META_XR
            if (_rig == null) _rig = FindFirstObjectByType<OVRCameraRig>();
            if (_rig == null) return;

            PositionBoard();
#endif
        }

        private void OnDestroy()
        {
            UnregisterAll();
            if (Instance == this) Instance = null;
        }

        // ---- HitTarget integration ----

        public void RegisterTarget(HitTarget target)
        {
            if (target == null || _registeredTargets.Contains(target)) return;
            target.OnHit += OnTargetHit;
            _registeredTargets.Add(target);
        }

        public void UnregisterTarget(HitTarget target)
        {
            if (target == null) return;
            target.OnHit -= OnTargetHit;
            _registeredTargets.Remove(target);
        }

        private void RegisterAllTargets()
        {
            foreach (var t in FindObjectsByType<HitTarget>(FindObjectsSortMode.None))
                RegisterTarget(t);
        }

        private void UnregisterAll()
        {
            foreach (var t in _registeredTargets)
                if (t != null) t.OnHit -= OnTargetHit;
            _registeredTargets.Clear();
        }

        private void OnTargetHit(HitZone zone, float force, Transform thrower)
        {
            bool headshot = zone.zoneType == HitZoneType.Head;
            RecordHit(headshot);

            // Fire notification
            if (VRNotification.Instance != null)
            {
                if (headshot)
                    VRNotification.Instance.Show("HEADSHOT!", VRNotification.NotifStyle.Gold);
                else
                    VRNotification.Instance.Show("TARGET HIT!", VRNotification.NotifStyle.Default);

                if (_currentStreak >= 3)
                    VRNotification.Instance.Show($"STREAK x{_currentStreak}!", VRNotification.NotifStyle.Gold);
            }
        }

        // ---- Positioning ----

#if HAS_META_XR
        private void PositionBoard()
        {
            var head = _rig.centerEyeAnchor;

            // Place board at constant offset in player's local space (right-forward)
            Vector3 right   = head.right;
            right.y = 0f;
            right.Normalize();
            Vector3 forward = head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.001f) forward.Normalize();

            Vector3 pos = head.position
                + right   * BOARD_OFFSET.x
                + Vector3.up * BOARD_OFFSET.y
                + forward * BOARD_OFFSET.z;

            _canvas.transform.position = pos;

            // Face the player, tilted slightly inward
            var toPlayer = head.position - pos;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f)
                _canvas.transform.rotation = Quaternion.LookRotation(-toPlayer.normalized, Vector3.up);
        }
#endif

        // ---- Display ----

        private void RefreshDisplay()
        {
            float acc = _totalThrows > 0
                ? (float)_totalHits / _totalThrows * 100f
                : 0f;

            if (_throwsVal)  _throwsVal.text  = _totalThrows.ToString();
            if (_hitsVal)    _hitsVal.text    = _totalHits.ToString();
            if (_accVal)     _accVal.text     = $"{acc:F0}%";
            if (_streakVal)  _streakVal.text  = _currentStreak.ToString();
            if (_bestVal)    _bestVal.text    = _bestStreak.ToString();
            // Headshots tracked internally but not displayed (no row -- compact scoreboard)

            // Colour streak -- gold when 3+
            if (_streakVal)
                _streakVal.color = _currentStreak >= 3 ? GOLD : TEXT_WHITE;
        }

        // ---- Canvas construction ----

        private void BuildCanvas()
        {
            var go = new GameObject("VRScoreboard_Canvas");
            go.transform.SetParent(transform);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta  = new Vector2(CANVAS_W, CANVAS_H);
            rt.localScale = Vector3.one * CANVAS_SCALE;

            // Background
            CreateImage(go.transform, "BG",
                Vector2.zero, new Vector2(CANVAS_W, CANVAS_H), BG_COLOR);

            // Title bar
            CreateImage(go.transform, "TitleBG",
                new Vector2(0, 82), new Vector2(CANVAS_W, 36), ACCENT);

            var title = CreateText(go.transform, "Title",
                new Vector2(0, 82), new Vector2(220, 32), TextAnchor.MiddleCenter, 20);
            title.text = "SCORE";
            title.color = TEXT_WHITE;

            // ---- Rows ----
            _throwsVal  = BuildRow(go.transform, "Throws",    new Vector2(0,  48), "THROWN");
            _hitsVal    = BuildRow(go.transform, "Hits",      new Vector2(0,  20), "HITS");
            _accVal     = BuildRow(go.transform, "Accuracy",  new Vector2(0,  -8), "ACCURACY");
            _streakVal  = BuildRow(go.transform, "Streak",    new Vector2(0, -36), "STREAK");
            _bestVal    = BuildRow(go.transform, "Best",      new Vector2(0, -64), "BEST");
        }

        /// <summary>
        /// Creates a label+value row. Returns the value Text component.
        /// </summary>
        private Text BuildRow(Transform parent, string name, Vector2 pos, string label)
        {
            // Label (left-aligned)
            var lbl = CreateText(parent, name + "Lbl",
                new Vector2(pos.x - 50, pos.y), new Vector2(100, 24),
                TextAnchor.MiddleLeft, 18);
            lbl.text  = label;
            lbl.color = TEXT_GREY;

            // Value (right-aligned)
            var val = CreateText(parent, name + "Val",
                new Vector2(pos.x + 60, pos.y), new Vector2(80, 24),
                TextAnchor.MiddleRight, 20);
            val.text  = "0";
            val.color = TEXT_WHITE;

            // Divider
            var div = CreateImage(parent, name + "Div",
                new Vector2(0, pos.y - 12), new Vector2(CANVAS_W - 20, 1),
                new Color(1, 1, 1, 0.08f));
            div.raycastTarget = false;

            return val;
        }

        private Text CreateText(Transform parent, string name,
            Vector2 pos, Vector2 size, TextAnchor anchor, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var txt = go.AddComponent<Text>();
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            txt.fontSize = fontSize;
            txt.alignment = anchor;
            txt.color = TEXT_WHITE;
            txt.raycastTarget = false;
            txt.supportRichText = true;
            return txt;
        }

        private Image CreateImage(Transform parent, string name,
            Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }
    }
}
