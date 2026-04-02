// AUTO-DISABLED: PLAGA44_FULL_SDK
#if PLAGA44_FULL_SDK
// FaceExpressionDebug.cs
// CYBERNOMAD -- In-headset HUD showing top 5 active face expressions in real time.
// World-space canvas, follows head (attached to CenterEyeAnchor).
// Toggle via menu: CYBERNOMAD > Debug > Face Expression Debug HUD
// Requires: com.meta.xr.sdk.core (auto-detected via HAS_META_XR define)

using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.FaceTracking
{
    /// <summary>
    /// Real-time HUD overlay displaying the top 5 active face expression blendshape weights.
    /// Follows the player's head in world-space. Toggle via editor menu or call Spawn/Kill.
    /// </summary>
    public class FaceExpressionDebug : MonoBehaviour
    {
        private const string LOG = "[FaceExpressionDebug]";
        internal const string ENABLED_KEY = "CYBERNOMAD_FaceExpressionDebug";

        // ── Singleton ────────────────────────────────────────────────────

        private static FaceExpressionDebug _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
#if UNITY_EDITOR
            if (!UnityEditor.EditorPrefs.GetBool(ENABLED_KEY, false)) return;
#endif
            Spawn();
        }

        public static void Spawn()
        {
            if (_instance != null) return;
            var go = new GameObject("FaceExpressionDebug_HUD");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<FaceExpressionDebug>();
        }

        public static void Kill()
        {
            if (_instance != null)
            {
                Destroy(_instance.gameObject);
                _instance = null;
            }
        }

        // ── Layout constants ─────────────────────────────────────────────

        private const float DisplayDistance = 0.5f;
        private const float DisplayScale = 0.0004f;
        private const int TopN = 5;

        // ── State ────────────────────────────────────────────────────────

        private Canvas _canvas;
        private Text _titleText;
        private Text _statusText;
        private Text _expressionsText;
        private Transform _centerEye;

#if HAS_META_XR
        // Pre-allocated array to avoid per-frame allocation
        private readonly (OVRFaceExpressions.FaceExpression expr, float weight)[] _topBuffer =
            new (OVRFaceExpressions.FaceExpression, float)[TopN];
#endif

        // ── Unity lifecycle ──────────────────────────────────────────────

        private void Start()
        {
            BuildCanvas();
        }

        private void Update()
        {
            UpdateHeadFollow();
            UpdateDisplay();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ── Head follow ──────────────────────────────────────────────────

        private void UpdateHeadFollow()
        {
            if (_centerEye == null)
                FindCenterEye();

            if (_centerEye == null) return;

            _canvas.transform.position =
                _centerEye.position + _centerEye.forward * DisplayDistance;
            _canvas.transform.rotation = _centerEye.rotation;
        }

        private void FindCenterEye()
        {
#if HAS_META_XR
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig != null)
            {
                _centerEye = rig.centerEyeAnchor;
                return;
            }
#endif
            var cam = Camera.main;
            if (cam != null) _centerEye = cam.transform;
        }

        // ── Display update ───────────────────────────────────────────────

        private void UpdateDisplay()
        {
            var manager = FaceTrackingManager.Instance;

#if HAS_META_XR
            if (!manager.IsTracking)
            {
                _statusText.text = "<color=#f80>TRACKING: OFF</color>";
                _expressionsText.text = "<color=#888>No face data</color>";
                return;
            }

            _statusText.text = "<color=#0f0>TRACKING: ON</color>";

            // Collect all expression weights
            int exprCount = (int)OVRFaceExpressions.FaceExpression.Max;

            // Insertion-sort top 5 while iterating -- avoids allocation
            for (int i = 0; i < TopN; i++)
                _topBuffer[i] = (OVRFaceExpressions.FaceExpression.BrowLowererL, -1f);

            for (int i = 0; i < exprCount; i++)
            {
                var expr = (OVRFaceExpressions.FaceExpression)i;
                float w = manager.GetExpression(expr);

                // Find insertion point
                if (w <= _topBuffer[TopN - 1].weight) continue;

                _topBuffer[TopN - 1] = (expr, w);

                // Bubble up
                for (int j = TopN - 1; j > 0; j--)
                {
                    if (_topBuffer[j].weight > _topBuffer[j - 1].weight)
                    {
                        var tmp = _topBuffer[j];
                        _topBuffer[j] = _topBuffer[j - 1];
                        _topBuffer[j - 1] = tmp;
                    }
                    else break;
                }
            }

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < TopN; i++)
            {
                var (expr, w) = _topBuffer[i];
                if (w < 0f) break;

                string color = w > 0.7f ? "#0f0" : w > 0.3f ? "#ff0" : "#888";
                string bar = BuildBar(w, 12);
                sb.AppendLine($"<color={color}>{bar} {w:F2}  {expr}</color>");
            }
            _expressionsText.text = sb.ToString();
#else
            _statusText.text = "<color=#f00>META XR SDK NOT PRESENT</color>";
            _expressionsText.text = "<color=#888>HAS_META_XR not defined</color>";
#endif
        }

        // ── Canvas construction ──────────────────────────────────────────

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("FaceDebug_Canvas");
            canvasGO.transform.SetParent(transform);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            var rect = _canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(600, 350);
            rect.localScale = Vector3.one * DisplayScale;

            _titleText = CreateText(canvasGO.transform, "Title",
                new Vector2(0, 145), new Vector2(580, 40),
                TextAnchor.UpperCenter, 22);
            _titleText.text = "<color=#0f0><b>FACE EXPRESSIONS</b></color>";

            _statusText = CreateText(canvasGO.transform, "Status",
                new Vector2(0, 100), new Vector2(580, 30),
                TextAnchor.UpperCenter, 18);
            _statusText.text = "<color=#888>Initializing...</color>";

            _expressionsText = CreateText(canvasGO.transform, "Expressions",
                new Vector2(0, 55), new Vector2(580, 250),
                TextAnchor.UpperLeft, 18);
            _expressionsText.text = "";
        }

        // ── Formatting helpers ───────────────────────────────────────────

        private static string BuildBar(float value, int width)
        {
            int filled = Mathf.RoundToInt(value * width);
            var sb = new System.Text.StringBuilder("[");
            for (int i = 0; i < width; i++)
                sb.Append(i < filled ? '#' : '.');
            sb.Append(']');
            return sb.ToString();
        }

        private static Text CreateText(Transform parent, string name,
            Vector2 pos, Vector2 size, TextAnchor anchor, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var text = go.AddComponent<Text>();
            text.font = Font.CreateDynamicFontFromOSFont("Consolas", fontSize);
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = anchor;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);

            return text;
        }
    }
}
#endif // PLAGA44_FULL_SDK
