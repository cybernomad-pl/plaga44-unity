// =============================================================================
// PlagaMenu.cs
// CYBERNOMAD -- hamburger menu TESTBED_BASE (nowe podejscie, od zera).
// Lewy wklesly przycisk (OVRInput.Button.Start) = toggle menu.
// Na razie puste: tylko napis "PLAGA '44 / pre-alpha 0.0.3".
// CHARACTER SCREEN: MirroredObjects (mirrored postac z sampla) jest ukryta
// przez caly czas gry -- pojawia sie TYLKO gdy menu otwarte (lusterko).
// PAUZA: menu otwarte -> Time.timeScale=0 + Locomotor OFF (zero teleportu
// w pauzie). Zamkniecie -> przywrocenie poprzedniego stanu.
// ZERO zaleznosci od starego stacku bootstrapowego (GameState/SettingsRegistry).
// =============================================================================
using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.UI
{
    public class PlagaMenu : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][PlagaMenu]";

        private const string TITLE = "PLAGA '44";
        private const string VERSION = "pre-alpha 0.0.3";

        // Nazwy obiektow w TESTBED_BASE (scena = klon Meta ISDKLocomotion sample).
        private const string MIRROR_ROOT = "MirroredObjects";   // root sceny
        private const string LOCOMOTOR_NAME = "Locomotor";      // dziecko OVRInteractionComprehensive

        private const float MENU_DISTANCE = 1.4f;
        private const float CANVAS_RAISE = 0.55f;  // tytul NAD postacia, nie zaslania lusterka
        // 1.8 m -- cala postac (1.7-1.8 m) miesci sie w pionie FOV Questa z zapasem.
        private const float MIRROR_DISTANCE = 1.8f;
        private const float CANVAS_SCALE = 0.001f;
        private const int CANVAS_W = 900;
        private const int CANVAS_H = 700;
        private const int TITLE_FONT = 64;
        private const int VERSION_FONT = 28;

        private static readonly Color PANEL_COLOR = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color TITLE_COLOR = new Color(0.9f, 0.5f, 0.1f);
        private static readonly Color VERSION_COLOR = new Color(0.9f, 0.9f, 0.9f);

        public static PlagaMenu Instance { get; private set; }
        public static bool MenuOpen { get; private set; }

        private Canvas _canvas;
        private OVRCameraRig _rig;
        private GameObject _mirror;
        private GameObject _locomotor;

        private bool _locomotorWasActive;
        private float _prevTimeScale = 1f;
        private Vector3 _mirrorHomePos;
        private Quaternion _mirrorHomeRot;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            _rig = FindFirstObjectByType<OVRCameraRig>();
            if (_rig == null) Debug.LogError($"{LOG} Brak OVRCameraRig w scenie.");

            _mirror = FindSceneRoot(MIRROR_ROOT);
            if (_mirror == null)
            {
                Debug.LogError($"{LOG} Brak roota '{MIRROR_ROOT}' -- character screen nie zadziala.");
            }
            else
            {
                _mirrorHomePos = _mirror.transform.position;
                _mirrorHomeRot = _mirror.transform.rotation;
                _mirror.SetActive(false); // ukryta poza menu (character screen)
            }

            _locomotor = FindInSceneIncludingInactive(LOCOMOTOR_NAME);
            if (_locomotor == null)
                Debug.LogError($"{LOG} Brak '{LOCOMOTOR_NAME}' -- pauza nie zablokuje lokomocji.");

            BuildCanvas();
            _canvas.gameObject.SetActive(false);
            Debug.Log($"{LOG} Start OK: mirror={(_mirror != null)}, locomotor={(_locomotor != null)}");
        }

        private void Update()
        {
            if (OVRInput.GetDown(OVRInput.Button.Start)) Toggle();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            // Nie zostawiaj gry w pauzie, gdy menu ginie ze swiata.
            if (MenuOpen) Time.timeScale = _prevTimeScale;
            MenuOpen = false;
            Instance = null;
        }

        // =====================================================================
        // Open / Close
        // =====================================================================

        public void Toggle() { if (MenuOpen) Close(); else Open(); }

        public void Open()
        {
            if (MenuOpen) return;
            MenuOpen = true;

            PlaceInFrontOfPlayer();
            _canvas.gameObject.SetActive(true);

            if (_mirror != null)
            {
                PlaceMirrorInFrontOfPlayer();
                _mirror.SetActive(true);
            }

            if (_locomotor != null)
            {
                _locomotorWasActive = _locomotor.activeSelf;
                _locomotor.SetActive(false);
            }

            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            Debug.Log($"{LOG} OPEN (pauza, mirror ON)");
        }

        public void Close()
        {
            if (!MenuOpen) return;
            MenuOpen = false;

            _canvas.gameObject.SetActive(false);

            if (_mirror != null)
            {
                _mirror.SetActive(false);
                _mirror.transform.SetPositionAndRotation(_mirrorHomePos, _mirrorHomeRot);
            }
            if (_locomotor != null) _locomotor.SetActive(_locomotorWasActive);

            Time.timeScale = _prevTimeScale;

            Debug.Log($"{LOG} CLOSE (wznowienie, mirror OFF)");
        }

        // =====================================================================
        // Scene lookup (bez fallbackow: brak obiektu = LogError w Start, koniec)
        // =====================================================================

        private GameObject FindSceneRoot(string name)
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        private GameObject FindInSceneIncludingInactive(string name)
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }

        // =====================================================================
        // Placement
        // =====================================================================

        private bool TryGetFlatForward(out Transform head, out Vector3 fwd)
        {
            head = null; fwd = Vector3.forward;
            if (_rig == null || _rig.centerEyeAnchor == null) return false;
            head = _rig.centerEyeAnchor;
            fwd = head.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();
            return true;
        }

        private void PlaceInFrontOfPlayer()
        {
            if (!TryGetFlatForward(out Transform head, out Vector3 fwd))
            {
                Debug.LogError($"{LOG} Brak rigu -- menu zostaje w (0, 1.5, {MENU_DISTANCE}).");
                _canvas.transform.position = new Vector3(0f, 1.5f, MENU_DISTANCE);
                _canvas.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
                return;
            }
            // Tytul NAD lusterkiem (up), zeby nie zaslanial postaci.
            _canvas.transform.position = head.position + fwd * MENU_DISTANCE + Vector3.up * CANVAS_RAISE;
            _canvas.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        // Lusterko: postac staje MIRROR_DISTANCE przed graczem, na wysokosci
        // podlogi riga, TWARZA do gracza. Przy identity rotation postac patrzy
        // w -Z (tak stoi w samplu wzgledem spawnu), wiec LookRotation(fwd)
        // obraca ja frontem do gracza. Odbicie L/P robi MirrorTransforms
        // (parowanie kosci) -- root wolno przestawiac.
        private void PlaceMirrorInFrontOfPlayer()
        {
            if (!TryGetFlatForward(out Transform head, out Vector3 fwd))
            {
                Debug.LogError($"{LOG} Brak rigu -- lusterko zostaje na pozycji ze sceny.");
                return;
            }
            Vector3 pos = head.position + fwd * MIRROR_DISTANCE;
            pos.y = _rig.transform.position.y; // podloga = poziom roota riga
            _mirror.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, Vector3.up));
        }

        // =====================================================================
        // Canvas
        // =====================================================================

        private void BuildCanvas()
        {
            var go = new GameObject("PlagaMenu_Canvas");
            go.transform.SetParent(transform);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CANVAS_W, CANVAS_H);
            rt.localScale = Vector3.one * CANVAS_SCALE;

            CreatePanel(go);
            CreateLabel(go, "Title", TITLE, TITLE_FONT, TITLE_COLOR, 60f, FontStyle.Bold);
            CreateLabel(go, "Version", VERSION, VERSION_FONT, VERSION_COLOR, -30f, FontStyle.Normal);
        }

        private static void CreatePanel(GameObject parent)
        {
            var go = new GameObject("BG");
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = PANEL_COLOR;
        }

        private static void CreateLabel(GameObject parent, string name, string content,
            int fontSize, Color color, float offsetY, FontStyle style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(CANVAS_W, 100f);
            rt.anchoredPosition = new Vector2(0f, offsetY);

            var txt = go.AddComponent<Text>();
            txt.text = content;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }
    }
}
