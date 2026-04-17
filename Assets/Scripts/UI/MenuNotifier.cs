// =============================================================================
// MenuNotifier.cs
// CYBERNOMAD -- dedykowany banner notifikacji dla HamburgerMenu.
//
// Dlaczego osobny komponent, nie footer menu?
// Footer pokazuje desc aktualnego settingu. Gdy user zmienia slider, desc sie
// aktualizuje i nadpisuje toast. User nie zdazy nic przeczytac.
// MenuNotifier ma wlasny element nad Canvas -- niezalezny od selection,
// widoczny dokladnie 4s, bez overwrite.
//
// Wywolywany przez event SettingsRegistry.OnAction -- automatycznie
// reaguje na SAVE/LOAD/RESET/ERROR.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.UI
{
    /// <summary>
    /// Banner notifikacji na gorze canvasu HamburgerMenu.
    /// Pokazuje sie na 4s, potem sam sie chowa. Zielony=sukces, czerwony=blad.
    /// Sam buduje swoje UI -- wystarczy AddComponent&lt;MenuNotifier&gt;() na GameObject
    /// ktory jest childem Canvasu (RectTransform parent wymagany).
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuNotifier : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Notifier]";
        private const float DISPLAY_DURATION = 4f;
        private const float FADE_DURATION = 0.5f;

        private static readonly Color SUCCESS_BG = new Color(0.10f, 0.55f, 0.18f, 0.96f);
        private static readonly Color ERROR_BG = new Color(0.70f, 0.12f, 0.12f, 0.96f);
        private static readonly Color TEXT_COLOR = Color.white;

        public static MenuNotifier Instance { get; private set; }

        private Text _text;
        private Image _bg;
        private CanvasGroup _cg;
        private float _showUntil = -1f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildUI();
            SetVisible(false);
        }

        private void OnEnable()
        {
            SettingsRegistry.OnAction += HandleAction;
        }

        private void OnDisable()
        {
            SettingsRegistry.OnAction -= HandleAction;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void HandleAction(string msg, bool success) => Show(msg, success);

        /// <summary>Manual trigger. Rowniez wywolywany przez SettingsRegistry.OnAction event.</summary>
        public void Show(string msg, bool success)
        {
            if (_text != null) _text.text = msg;
            if (_bg != null) _bg.color = success ? SUCCESS_BG : ERROR_BG;
            _showUntil = Time.unscaledTime + DISPLAY_DURATION;
            SetVisible(true);
            if (_cg != null) _cg.alpha = 1f;
        }

        private void Update()
        {
            if (_showUntil < 0f || _cg == null) return;

            float remaining = _showUntil - Time.unscaledTime;
            if (remaining <= 0f)
            {
                SetVisible(false);
                _showUntil = -1f;
                return;
            }

            // Fade out w ostatnich FADE_DURATION sekundach
            if (remaining < FADE_DURATION)
                _cg.alpha = remaining / FADE_DURATION;
        }

        private void SetVisible(bool on)
        {
            if (_cg != null) _cg.blocksRaycasts = on;
            gameObject.SetActive(on);
        }

        // =====================================================================
        // UI build -- banner na gorze canvasu, pod title
        // =====================================================================
        private void BuildUI()
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();

            // Anchor: gorny srodek, rozciagniety w poziomie z paddingiem
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -85f); // pod title + version
            rt.sizeDelta = new Vector2(-40f, 50f);

            _bg = gameObject.AddComponent<Image>();
            _bg.color = SUCCESS_BG;

            _cg = gameObject.AddComponent<CanvasGroup>();

            // Text child
            var textGO = new GameObject("Label");
            textGO.transform.SetParent(transform, false);
            var tr = textGO.AddComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(15f, 4f);
            tr.offsetMax = new Vector2(-15f, -4f);

            _text = textGO.AddComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = 22;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.color = TEXT_COLOR;
            _text.fontStyle = FontStyle.Bold;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;

            Debug.Log($"{LOG} Built notifier UI");
        }
    }
}
