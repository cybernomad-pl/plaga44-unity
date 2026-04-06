// =============================================================================
// SprintModifier.cs
// CYBERNOMAD -- Sprint i skok dla PLAGA '44.
//
// SPRINT:
// Wcisniecie lewego thumbsticka (L3) aktywuje sprint -- mnozy moveSpeed
// LocomotionController przez sprintMultiplier (domyslnie 3x).
// Po puszczeniu L3, moveSpeed wraca do wartosci bazowej.
//
// SKOK:
// Przycisk B (prawy kontroler) = skok.
// Skok dziala TYLKO gdy rece sa puste (HandsAreEmpty check).
// Dlaczego? Bo B w przyszlosci moze miec inne funkcje gdy trzymamy przedmiot
// (np. rzut, uzycie). W MVP: B = skok i NIC wiecej.
//
// GRAWITACJA W SKOKU:
// Skok ustawia VerticalVelocity w LocomotionController na jumpForce.
// Grawitacja jest obslugiwana przez LocomotionController.ApplyGravity().
// SprintModifier NIE obsluguje grawitacji sam -- to by zduplikowalo logike.
//
// REFERENCJA: Zmodyfikowany z reference-branch.
// Roznice vs original:
//   - Uzywa LocomotionController zamiast OVRPlayerController
//   - Modyfikuje moveSpeed zamiast Acceleration
//   - Nie obsluguje grawitacji samodzielnie (deleguje do LocomotionController)
//   - Guard na GameState.CanMove
//   - #if HAS_META_XR z fallbackiem na klawiature
//
// INPUT MAPPING (Quest kontrolery):
//   L3 (lewy thumbstick click) = sprint
//   B  (prawy kontroler)       = skok (tylko z pustymi rekami)
// =============================================================================

using UnityEngine;

namespace Plaga44
{
    /// <summary>
    /// Modyfikator lokomocji: sprint (L3) i skok (B).
    /// Szuka LocomotionController na scenie i modyfikuje jego moveSpeed.
    /// Auto-tworzy sie jako DontDestroyOnLoad singleton.
    /// </summary>
    public class SprintModifier : MonoBehaviour
    {
        // =====================================================================
        // Pola inspektora
        // =====================================================================

        [Header("Sprint")]
        [Tooltip("Mnoznik predkosci sprintu. 3x = trzy razy szybciej niz chod.")]
        public float sprintMultiplier = 3f;

        [Header("Skok")]
        [Tooltip("Sila skoku. Wieksza wartosc = wyzszy skok.")]
        public float jumpForce = 5f;

        [Tooltip("Cooldown miedzy skokami w sekundach. Zapobiega spammowaniu skoku.")]
        public float jumpCooldown = 0.5f;

        // =====================================================================
        // Stan runtime (prywatny)
        // =====================================================================

        /// <summary>
        /// Referencja do LocomotionController -- glowny system ruchu.
        /// Szukany w Start() przez FindAnyObjectByType (Unity 6 API).
        /// </summary>
        private Locomotion.LocomotionController _loco;

        /// <summary>
        /// Bazowa predkosc ruchu PRZED sprintem.
        /// Zapamietujemy ja zeby moc przywrocic po puszczeniu L3.
        /// </summary>
        private float _baseSpeed;

        /// <summary>Czy sprint jest aktywny w tym momencie.</summary>
        private bool _sprinting;

        /// <summary>Timer cooldownu skoku. Gdy > 0, skok jest zablokowany.</summary>
        private float _jumpTimer;

        // =====================================================================
        // Auto-tworzenie
        // =====================================================================

        /// <summary>
        /// Automatycznie tworzy SprintModifier po zaladowaniu sceny.
        /// DontDestroyOnLoad sprawia ze przetrwa zmiane scen.
        /// Dzieki temu nie trzeba recznie dodawac tego komponentu na scene.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            var go = new GameObject("_SprintModifier");
            go.AddComponent<SprintModifier>();
            DontDestroyOnLoad(go);
        }

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void Start()
        {
            // Szukamy LocomotionController na scenie.
            // FindAnyObjectByType to Unity 6 API -- szybsze niz FindObjectOfType.
            _loco = FindAnyObjectByType<Locomotion.LocomotionController>();

            if (_loco != null)
            {
                // Zapamietujemy bazowa predkosc zeby moc ja przywrocic po sprincie.
                _baseSpeed = _loco.moveSpeed;
            }
            else
            {
                Debug.LogWarning("[SprintModifier] Nie znaleziono LocomotionController na scenie!");
            }
        }

        private void Update()
        {
            // Jesli LocomotionController nie zostal znaleziony w Start(),
            // probujemy jeszcze raz (mogl byc stworzony pozniej).
            if (_loco == null)
            {
                _loco = FindAnyObjectByType<Locomotion.LocomotionController>();
                if (_loco != null) _baseSpeed = _loco.moveSpeed;
                return; // Czekamy do nastepnej klatki po znalezieniu.
            }

            // --- GUARD: nie rób nic jesli nie gramy ---
            if (!GameState.CanMove) return;

            HandleSprint();
            HandleJump();
        }

        // =====================================================================
        // Sprint
        // =====================================================================

        /// <summary>
        /// Obsluguje sprint (L3 = lewy thumbstick click).
        /// Mnozy moveSpeed LocomotionController przez sprintMultiplier
        /// gdy L3 jest wcisniety, przywraca gdy puszczony.
        /// </summary>
        private void HandleSprint()
        {
            bool pressed = GetSprintInput();

            if (pressed && !_sprinting)
            {
                // Wlaczamy sprint -- mnożymy predkosc bazowa.
                // Uzywamy _baseSpeed * multiplier zamiast _loco.moveSpeed * multiplier,
                // bo wielokrotne wlaczenie sprintu mogloby narastajaco zwiekszac predkosc.
                _loco.moveSpeed = _baseSpeed * sprintMultiplier;
                _sprinting = true;
            }
            else if (!pressed && _sprinting)
            {
                // Wylaczamy sprint -- przywracamy bazowa predkosc.
                _loco.moveSpeed = _baseSpeed;
                _sprinting = false;
            }
        }

        // =====================================================================
        // Skok
        // =====================================================================

        /// <summary>
        /// Obsluguje skok (B = Button.Two na prawym kontrolerze).
        /// Skok dziala TYLKO gdy:
        /// 1. Gracz stoi na ziemi (isGrounded)
        /// 2. Cooldown minal
        /// 3. Rece sa puste (HandsAreEmpty)
        /// Ustawia VerticalVelocity w LocomotionController -- grawitacja
        /// jest obslugiwana przez LocomotionController.
        /// </summary>
        private void HandleJump()
        {
            // Odliczaj cooldown niezaleznie od tego czy skaczemy.
            _jumpTimer -= Time.deltaTime;

            bool jumpPressed = GetJumpInput();

            // Sprawdzamy wszystkie warunki skoku:
            // - jumpPressed: gracz wcisnal B
            // - _jumpTimer <= 0: cooldown minal
            // - IsGrounded: gracz stoi na ziemi (nie moze skakac w powietrzu)
            // - HandsAreEmpty: rece sa puste (B moze miec inne funkcje z przedmiotem)
            if (jumpPressed && _jumpTimer <= 0f && _loco.IsGrounded && HandsAreEmpty())
            {
                // Ustawiamy predkosc pionowa na jumpForce.
                // LocomotionController.ApplyGravity() obniza ta wartosc co klatke,
                // tworzac naturalna parabolę skoku.
                _loco.VerticalVelocity = jumpForce;
                _jumpTimer = jumpCooldown;
                Debug.Log("[PLAGA44] SKOK");
            }
        }

        // =====================================================================
        // Input z #if HAS_META_XR
        // =====================================================================

        /// <summary>Czy L3 (lewy thumbstick click) jest wcisniety.</summary>
        private bool GetSprintInput()
        {
#if HAS_META_XR
            // PrimaryThumbstick na lewym kontrolerze = L3.
            return OVRInput.Get(OVRInput.Button.PrimaryThumbstick);
#else
            // Fallback: lewy Shift na klawiaturze.
            return UnityEngine.Input.GetKey(KeyCode.LeftShift);
#endif
        }

        /// <summary>Czy B (prawy kontroler) zostal wcisniety w tej klatce.</summary>
        private bool GetJumpInput()
        {
#if HAS_META_XR
            // Button.Two = B na prawym kontrolerze Quest.
            // GetDown = tylko w klatce wcisniecia (nie trzymania).
            return OVRInput.GetDown(OVRInput.Button.Two);
#else
            // Fallback: spacja na klawiaturze.
            return UnityEngine.Input.GetKeyDown(KeyCode.Space);
#endif
        }

        // =====================================================================
        // Sprawdzanie czy rece sa puste
        // =====================================================================

        /// <summary>
        /// Sprawdza czy gracz nie trzyma zadnego przedmiotu.
        /// Szuka wszystkich OVRGrabber na scenie (Meta XR grab system)
        /// i sprawdza czy którykolwiek ma grabbedObject != null.
        ///
        /// UWAGA: W przyszlosci mozna to zoptymalizowac cache'ujac referencje
        /// zamiast uzywac FindObjectsByType co klatke. Ale w MVP z 2 grabberami
        /// (lewy + prawy kontroler) to nie jest waskie gardlo.
        /// </summary>
        private bool HandsAreEmpty()
        {
#if HAS_META_XR
            // OVRGrabber to komponent Meta XR SDK do chwytania przedmiotow.
            // FindObjectsByType to Unity 6 API (zastepcze dla FindObjectsOfType).
            var grabbers = FindObjectsByType<OVRGrabber>(FindObjectsSortMode.None);
            foreach (var g in grabbers)
            {
                if (g.grabbedObject != null) return false;
            }
#endif
            // Jesli nie ma Meta XR SDK lub nie ma grabberow -- rece sa "puste".
            return true;
        }
    }
}
