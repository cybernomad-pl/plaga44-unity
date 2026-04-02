// PlatformDebugUI.cs
// CYBERNOMAD -- World-space debug panel: user info, leaderboard top 5, achievement status.
// Attach to any GameObject in scene. Creates its own world-space Canvas.
// Visible only when CYBERNOMAD_DEBUG is defined (add to Player > Scripting Define Symbols).
// In production builds: panel stays invisible, all async calls are skipped.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.Platform
{
    /// <summary>
    /// Debug world-space panel showing Platform SDK status.
    /// Toggle visibility with the B button (right controller) at runtime.
    ///
    /// Displays:
    ///   - User name + ID + entitlement status
    ///   - Top 5 of each leaderboard (mors_cerebri_distance, streak, speed)
    ///   - Achievement unlock status (FIRST_HEADSHOT, STONE_MASTER_10, LONG_RANGE_30M, PACIFIST)
    /// </summary>
    public class PlatformDebugUI : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        // Inspector
        // ------------------------------------------------------------------ //

        [Header("Panel position (world-space, relative to camera)")]
        [Tooltip("Distance in front of the camera.")]
        public float distance = 1.2f;

        [Tooltip("Vertical offset from eye level (negative = lower).")]
        public float verticalOffset = -0.1f;

        [Tooltip("Scale of the world-space canvas (metres per unit).")]
        public float canvasScale = 0.001f;

        [Header("Refresh")]
        [Tooltip("Automatically refresh leaderboard/achievement data every N seconds. 0 = only on show.")]
        public float autoRefreshInterval = 30f;

        // ------------------------------------------------------------------ //
        // Private state
        // ------------------------------------------------------------------ //

        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private Text _userInfoText;
        private Text _leaderboardText;
        private Text _achievementText;
        private Text _statusText;

        private bool _isVisible;
        private bool _isRefreshing;
        private float _refreshTimer;
        private Transform _cameraTransform;

        // ------------------------------------------------------------------ //
        // Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Start()
        {
            CreateDebugPanel();
            SetVisible(false);

            // Subscribe to PlatformManager init event.
            PlatformManager.Instance.OnInitialized += OnPlatformInitialized;

            // Try to find camera.
            FindCamera();

            // If already initialized, refresh immediately.
            if (PlatformManager.Instance.IsInitialized)
            {
                OnPlatformInitialized(true);
            }
        }

        private void Update()
        {
            // Follow camera.
            if (_isVisible && _cameraTransform != null)
            {
                UpdatePanelPosition();
            }

            // Re-find camera if lost.
            if (_cameraTransform == null)
            {
                FindCamera();
            }

            // Toggle visibility with B button (right controller).
            if (IsBButtonPressed())
            {
                ToggleVisible();
            }

            // Auto-refresh.
            if (_isVisible && autoRefreshInterval > 0f)
            {
                _refreshTimer += Time.deltaTime;
                if (_refreshTimer >= autoRefreshInterval)
                {
                    _refreshTimer = 0f;
                    StartCoroutine(RefreshAll());
                }
            }
        }

        private void OnDestroy()
        {
            if (PlatformManager.Instance != null)
            {
                PlatformManager.Instance.OnInitialized -= OnPlatformInitialized;
            }
        }

        // ------------------------------------------------------------------ //
        // Panel setup
        // ------------------------------------------------------------------ //

        private void CreateDebugPanel()
        {
            // Root canvas GO.
            var canvasGO = new GameObject("[PlatformDebugPanel]");
            canvasGO.transform.SetParent(transform);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 100;

            // Canvas size: 600 x 800 units, displayed at canvasScale.
            var rect = _canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(600, 800);
            rect.localScale = Vector3.one * canvasScale;

            _canvasGroup = canvasGO.AddComponent<CanvasGroup>();

            // Background.
            CreateBackground(canvasGO);

            // Content text fields.
            float topY = 370f;

            // Title.
            CreateLabel(canvasGO, "[ PLATFORM DEBUG ]", 0f, topY, 24, Color.cyan, TextAnchor.UpperCenter);

            // Status row (init state).
            _statusText = CreateLabel(canvasGO, "Initializing...", 0f, topY - 35f, 14, Color.yellow, TextAnchor.UpperCenter);

            // User info section.
            CreateLabel(canvasGO, "-- USER --", -280f, topY - 65f, 14, Color.white, TextAnchor.UpperLeft);
            _userInfoText = CreateLabel(canvasGO, "...", -280f, topY - 85f, 12, new Color(0.8f, 0.8f, 0.8f), TextAnchor.UpperLeft);

            // Leaderboard section.
            CreateLabel(canvasGO, "-- LEADERBOARDS --", -280f, topY - 175f, 14, Color.white, TextAnchor.UpperLeft);
            _leaderboardText = CreateLabel(canvasGO, "...", -280f, topY - 200f, 11, new Color(0.8f, 0.8f, 0.8f), TextAnchor.UpperLeft);

            // Achievement section.
            CreateLabel(canvasGO, "-- ACHIEVEMENTS --", -280f, topY - 450f, 14, Color.white, TextAnchor.UpperLeft);
            _achievementText = CreateLabel(canvasGO, "...", -280f, topY - 475f, 12, new Color(0.8f, 0.8f, 0.8f), TextAnchor.UpperLeft);

            // Footer.
            CreateLabel(canvasGO, "[B] Toggle | CYBERNOMAD", 0f, -380f, 10, new Color(0.5f, 0.5f, 0.5f), TextAnchor.LowerCenter);
        }

        private void CreateBackground(GameObject parent)
        {
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(parent.transform, false);
            var img = bgGO.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.85f);
            var r = bgGO.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.sizeDelta = Vector2.zero;
        }

        private Text CreateLabel(GameObject parent, string text, float x, float y, int fontSize, Color color, TextAnchor anchor)
        {
            var go = new GameObject($"Label_{text.Substring(0, Mathf.Min(10, text.Length))}");
            go.transform.SetParent(parent.transform, false);

            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = Font.CreateDynamicFontFromOSFont("Consolas", fontSize);
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;

            var r = go.GetComponent<RectTransform>();
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(560f, 200f);
            r.pivot = new Vector2(0f, 1f);

            return t;
        }

        // ------------------------------------------------------------------ //
        // Visibility
        // ------------------------------------------------------------------ //

        private void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.blocksRaycasts = false;
            }

            if (visible && !_isRefreshing)
            {
                _refreshTimer = 0f;
                StartCoroutine(RefreshAll());
            }
        }

        private void ToggleVisible() => SetVisible(!_isVisible);

        // ------------------------------------------------------------------ //
        // Position update
        // ------------------------------------------------------------------ //

        private void UpdatePanelPosition()
        {
            if (_canvas == null || _cameraTransform == null) return;

            Vector3 forward = _cameraTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            _canvas.transform.position = _cameraTransform.position
                + forward * distance
                + Vector3.up * verticalOffset;

            _canvas.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        // ------------------------------------------------------------------ //
        // Data refresh
        // ------------------------------------------------------------------ //

        private void OnPlatformInitialized(bool success)
        {
            if (_statusText == null) return;

            if (success)
            {
                _statusText.text = "<color=#00FF00>INITIALIZED OK</color>";
                _statusText.color = Color.green;
            }
            else
            {
                _statusText.text = "<color=#FF3333>INIT FAILED</color>";
                _statusText.color = Color.red;
            }

            if (_isVisible)
            {
                StartCoroutine(RefreshAll());
            }
        }

        private IEnumerator RefreshAll()
        {
            if (_isRefreshing) yield break;
            _isRefreshing = true;

            // Refresh user info synchronously (already cached in PlatformManager).
            RefreshUserInfo();

            // Leaderboards -- async via coroutine adapter.
            yield return StartCoroutine(RefreshLeaderboards());

            // Achievements -- async via coroutine adapter.
            yield return StartCoroutine(RefreshAchievements());

            _isRefreshing = false;
        }

        private void RefreshUserInfo()
        {
            if (_userInfoText == null) return;

            var pm = PlatformManager.Instance;
            if (!pm.IsInitialized)
            {
                _userInfoText.text = "Not initialized";
                return;
            }

            string entitle = pm.IsEntitled ? "<color=#00FF00>ENTITLED</color>" : "<color=#FF3333>NOT ENTITLED</color>";
            _userInfoText.text =
                $"Name: {pm.LoggedInUserDisplayName}\n" +
                $"ID:   {pm.LoggedInUserId}\n" +
                $"Status: {entitle}";
        }

        private IEnumerator RefreshLeaderboards()
        {
            if (_leaderboardText == null) yield break;

            _leaderboardText.text = "Loading...";

            var lm = LeaderboardManager.Instance;
            var sb = new System.Text.StringBuilder();

            // Fetch top 5 of each leaderboard.
            string[] boards = new string[]
            {
                LeaderboardNames.MorsCerebriDistance,
                LeaderboardNames.MorsCerebriStreak,
                LeaderboardNames.MorsCerebriSpeed
            };

            string[] boardLabels = new string[]
            {
                "DISTANCE (cm)",
                "STREAK",
                "SPEED (ms)"
            };

            for (int i = 0; i < boards.Length; i++)
            {
                sb.AppendLine($"<b>{boardLabels[i]}</b>");

                var task = lm.GetScores(boards[i], 5);

                // Wait for task completion.
                while (!task.IsCompleted)
                {
                    yield return null;
                }

                var entries = task.Result;
                if (entries == null || entries.Count == 0)
                {
                    sb.AppendLine("  (empty)");
                }
                else
                {
                    foreach (var e in entries)
                    {
                        string scoreStr = boards[i] == LeaderboardNames.MorsCerebriDistance
                            ? $"{e.Score / 100f:F1}m"
                            : e.Score.ToString();
                        sb.AppendLine($"  #{e.Rank} {e.DisplayName}: {scoreStr}");
                    }
                }

                if (i < boards.Length - 1) sb.AppendLine();
            }

            _leaderboardText.text = sb.ToString();
        }

        private IEnumerator RefreshAchievements()
        {
            if (_achievementText == null) yield break;

            _achievementText.text = "Loading...";

            var am = AchievementManager.Instance;
            var sb = new System.Text.StringBuilder();

            string[] names = new string[]
            {
                AchievementNames.FirstHeadshot,
                AchievementNames.StoneMaster10,
                AchievementNames.LongRange30M,
                AchievementNames.Pacifist
            };

            foreach (var name in names)
            {
                var task = am.GetProgress(name);

                while (!task.IsCompleted)
                {
                    yield return null;
                }

                var progress = task.Result;
                if (progress == null)
                {
                    sb.AppendLine($"  {name}: <color=#888888>ERROR</color>");
                }
                else
                {
                    string status = progress.IsUnlocked
                        ? "<color=#00FF00>UNLOCKED</color>"
                        : "<color=#888888>locked</color>";

                    string extra = string.Empty;
                    if (!progress.IsUnlocked && progress.Count > 0)
                    {
                        extra = $" ({progress.Count})";
                    }

                    sb.AppendLine($"  {name}: {status}{extra}");
                }
            }

            _achievementText.text = sb.ToString();
        }

        // ------------------------------------------------------------------ //
        // Input
        // ------------------------------------------------------------------ //

        private bool _bWasDown;

        private bool IsBButtonPressed()
        {
#if HAS_META_XR
            bool down = OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.RTouch);
            if (down && !_bWasDown)
            {
                _bWasDown = true;
                return true;
            }
            if (!down) _bWasDown = false;
            return false;
#else
            // In editor: use Tab key as toggle.
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab))
                return true;
            return false;
#endif
        }

        // ------------------------------------------------------------------ //
        // Camera
        // ------------------------------------------------------------------ //

        private void FindCamera()
        {
#if HAS_META_XR
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig != null)
            {
                _cameraTransform = rig.centerEyeAnchor;
                return;
            }
#endif
            var cam = Camera.main;
            if (cam != null) _cameraTransform = cam.transform;
        }
    }
}
