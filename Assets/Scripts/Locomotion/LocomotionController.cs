// =============================================================================
// LocomotionController.cs
// CYBERNOMAD -- Glowny kontroler lokomocji gracza w PLAGA '44.
//
// ARCHITEKTURA:
// Ten skrypt odpowiada za RUCH GRACZA (lewy thumbstick) i GRAWITACJE.
// NIE zajmuje sie obrotami (snap/smooth turn) -- to oddzielny system w Fazie 2.
// Uzywa CharacterController.Move() zamiast transform.position +=, dzieki czemu:
//   1. Kolizje z otoczeniem dzialaja automatycznie (sciany, podlogi, schody)
//   2. isGrounded check dziala poprawnie
//   3. Fizyka nie "teleportuje" gracza przez sciany
//
// INPUT:
// W buildzie Quest (HAS_META_XR) czyta OVRInput z lewego kontrolera.
// W edytorze bez SDK uzywa klawiatury WASD jako fallback.
//
// KIERUNEK RUCHU:
// Ruch jest RELATYWNY DO KIERUNKU GLOWY (head-relative).
// Dlaczego glowa a nie kontroler? Bo w VR gracz naturalnie patrzy tam
// gdzie chce isc -- to najbardziej intuicyjny model lokomocji.
// Wektor forward glowy jest rzutowany na plaszczyzne pozioma (y=0),
// wiec patrzenie w gore/dol nie wplywa na kierunek ruchu.
//
// GRAWITACJA:
// Czytamy Physics.gravity.y (domyslnie -9.81) zamiast hardcodowania wartosci.
// Dzieki temu zmiana grawitacji w ustawieniach projektu (PhysicsConfig) automatycznie
// wplywa na lokomocje -- np. misje na bagnach z inna fizyka.
//
// WYMAGANIA NA SCENIE:
// - Ten komponent MUSI byc na tym samym GameObject co CharacterController.
// - Standardowo: na uzytkowniku VR rig root (np. OVRCameraRig parent).
// - Pole _headTransform musi wskazywac na kamere (CenterEyeAnchor lub Camera.main).
//
// UZYWANE PRZEZ:
// - LocomotionManager.cs -- wlacza/wylacza ten komponent w zaleznosci od trybu
// - SprintModifier.cs -- modyfikuje moveSpeed podczas sprintu
// - ComfortVignette.cs -- czyta NormalisedSpeed do efektu winiety
// =============================================================================

using UnityEngine;

namespace Plaga44.Locomotion
{
    /// <summary>
    /// Kontroler lokomocji oparty na CharacterController.
    /// Lewy thumbstick = ruch relatywny do kierunku glowy.
    /// Obsluguje grawitacje i isGrounded check.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class LocomotionController : MonoBehaviour
    {
        // =====================================================================
        // Pola ustawiane w inspektorze
        // =====================================================================

        [Header("Predkosc ruchu")]
        [Tooltip("Predkosc chodzenia w metrach na sekunde. Modyfikowana runtime przez SprintModifier.")]
        public float moveSpeed = 2.5f;

        [Tooltip("Mnoznik predkosci strafe'u (ruch na boki). 0.8 = 80% predkosci do przodu. " +
                 "Nizszy strafe jest bardziej realistyczny -- biegniecie bokiem jest wolniejsze.")]
        [Range(0.1f, 1f)]
        public float strafeFactor = 0.8f;

        [Header("Referencja glowy")]
        [Tooltip("Transform kamery VR (CenterEyeAnchor). Jesli puste -- szuka automatycznie.")]
        [SerializeField] private Transform _headTransform;

        // =====================================================================
        // Stan runtime (prywatny)
        // =====================================================================

        /// <summary>
        /// Referencja do CharacterController -- ustawiana w Awake().
        /// CharacterController daje nam kolizje i isGrounded za darmo.
        /// </summary>
        private CharacterController _cc;

        /// <summary>
        /// Aktualna predkosc pionowa (oś Y). Modyfikowana przez grawitację i skoki.
        /// Dodatnia = w gore (skok), ujemna = w dol (spadanie).
        /// SprintModifier ustawia ta wartosc na jumpForce zeby zainicjowac skok.
        /// </summary>
        private float _verticalVelocity;

        /// <summary>
        /// Stala mala wartosc ciagniaca gracza w dol gdy stoi na ziemi.
        /// Dzieki temu CharacterController.isGrounded zwraca true konsekwentnie,
        /// nawet na lekko nierównym terenie. Bez tego gracz moze "drgac" miedzy
        /// isGrounded = true i false na pochylych powierzchniach.
        /// </summary>
        private const float GroundedPullDown = -2f; // mocniejszy pull = stabilniejszy isGrounded na nierównym terenie

        // =====================================================================
        // Property publiczne (read-only z zewnatrz)
        // =====================================================================

        /// <summary>
        /// Znormalizowana predkosc ruchu (0 = stoi, 1 = pelna predkosc).
        /// Uzywana przez ComfortVignette do skalowania efektu winiety --
        /// im szybciej gracz sie rusza, tym mocniejsza winieta (zapobiega chorobie lokomocyjnej).
        /// </summary>
        public float NormalisedSpeed { get; private set; }

        /// <summary>
        /// Aktualna predkosc pionowa. SprintModifier uzywa tego do sprawdzenia
        /// czy gracz jest w powietrzu i do ustawiania sily skoku.
        /// </summary>
        public float VerticalVelocity
        {
            get => _verticalVelocity;
            set => _verticalVelocity = value;
        }

        /// <summary>
        /// Czy gracz stoi na ziemi. Wrapper na CharacterController.isGrounded.
        /// Uzywany przez SprintModifier do sprawdzenia czy skok jest mozliwy.
        /// </summary>
        public bool IsGrounded => _cc != null && _cc.isGrounded;

        /// <summary>
        /// Referencja do CharacterController na tym obiekcie.
        /// SprintModifier uzywa jej do wywoływania Move() przy skoku.
        /// </summary>
        public CharacterController CharController => _cc;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private const string LOG = "[PLAGA44][Locomotion]";

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            Debug.Log($"{LOG} Awake: CC found={_cc != null}, height={_cc?.height}, radius={_cc?.radius}");

            if (_headTransform == null)
                _headTransform = ResolveHeadTransform();

            Debug.Log($"{LOG} Awake: headTransform={_headTransform?.name ?? "NULL"}, pos={transform.position}");
        }

        private void OnEnable()
        {
            Debug.Log($"{LOG} OnEnable: speed={moveSpeed}, strafe={strafeFactor}");
        }

        private void OnDisable()
        {
            Debug.Log($"{LOG} OnDisable");
        }

        private bool _wasGrounded = true;
        private float _lastGroundedLogTime = -1f;

        private void Update()
        {
            if (!GameState.CanMove) return;
            if (_headTransform == null) return;

            // 1. Odczytaj input z lewego thumbsticka (lub klawiatury)
            Vector2 moveInput = GetMoveInput();

            // 2. Przelicz input na ruch 3D relatywny do kierunku glowy
            Vector3 horizontalMove = CalculateHeadRelativeMovement(moveInput);

            // 3. Zastosuj grawitacje (lub utrzymaj gracza na ziemi)
            ApplyGravity();

            // 4. Zloz ruch poziomy i pionowy w jeden wektor i wykonaj Move()
            // CharacterController.Move() automatycznie obsluguje kolizje.
            Vector3 finalMove = horizontalMove + (Vector3.up * _verticalVelocity * Time.deltaTime);
            _cc.Move(finalMove);

            NormalisedSpeed = Mathf.Clamp01(moveInput.magnitude);

            // Log zmian grounded (throttled: max co 0.5s zeby uniknac spam przy drganiach CC)
            if (_cc.isGrounded != _wasGrounded)
            {
                if (Time.time - _lastGroundedLogTime > 0.5f)
                {
                    Debug.Log($"{LOG} Grounded: {_wasGrounded} -> {_cc.isGrounded}, pos={transform.position}, vVel={_verticalVelocity:F2}");
                    _lastGroundedLogTime = Time.time;
                }
                _wasGrounded = _cc.isGrounded;
            }
        }

        // =====================================================================
        // Odczyt inputu
        // =====================================================================

        /// <summary>
        /// Zwraca wektor 2D inputu ruchu.
        /// X = lewo/prawo (strafe), Y = przod/tyl.
        /// Na Questcie: lewy thumbstick. W edytorze: WASD.
        /// </summary>
        private Vector2 GetMoveInput()
        {
#if HAS_META_XR
            // OVRInput.Get zwraca wektor z zakresu -1..1 na obu osiach.
            // LTouch = lewy kontroler Quest.
            return OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
#else
            // Fallback na klawiature -- WASD lub strzalki.
            // Przydatne do testowania w edytorze bez podlaczonego headsetu.
            float h = UnityEngine.Input.GetAxis("Horizontal"); // A/D
            float v = UnityEngine.Input.GetAxis("Vertical");   // W/S
            return new Vector2(h, v);
#endif
        }

        // =====================================================================
        // Obliczanie ruchu relatywnego do glowy
        // =====================================================================

        /// <summary>
        /// Przeksztalca 2D input thumbsticka na 3D wektor ruchu w przestrzeni swiata,
        /// relatywny do kierunku patrzenia (head-relative).
        /// </summary>
        /// <param name="input">Raw input z thumbsticka (x = strafe, y = przod/tyl).</param>
        /// <returns>Wektor ruchu w przestrzeni swiata, juz pomnozony przez predkosc i deltaTime.</returns>
        private Vector3 CalculateHeadRelativeMovement(Vector2 input)
        {
            // Jesli thumbstick jest w martwej strefie, nie ruszamy sie.
            // sqrMagnitude jest tansza obliczeniowo niz magnitude (brak sqrt).
            if (input.sqrMagnitude < 0.01f)
                return Vector3.zero;

            // --- Rzutowanie kierunku glowy na plaszczyzne pozioma ---
            // Bierzemy forward i right kamery VR, ale zerujemy skladowa Y,
            // bo nie chcemy zeby patrzenie w gore/dol wplywalo na kierunek ruchu.
            // Bez tego: patrzenie w dol + push do przodu = gracz wchodzi w ziemie.
            Vector3 fwd = _headTransform.forward;
            fwd.y = 0f;
            fwd.Normalize();

            Vector3 right = _headTransform.right;
            right.y = 0f;
            right.Normalize();

            // --- Skladanie wektora ruchu ---
            // input.y = przod/tyl (pelna predkosc)
            // input.x = strafe (zredukowana predkosc przez strafeFactor)
            Vector3 move = (fwd * input.y) + (right * input.x * strafeFactor);

            // Mnożymy przez predkosc i deltaTime.
            // deltaTime sprawia ze ruch jest niezalezny od framerate.
            move *= moveSpeed * Time.deltaTime;

            return move;
        }

        // =====================================================================
        // Grawitacja
        // =====================================================================

        /// <summary>
        /// Aplikuje grawitacje do predkosci pionowej.
        /// Jesli gracz stoi na ziemi, ustawia mala wartosc ciagnaca w dol
        /// (zeby isGrounded dzialalo stabilnie).
        /// Jesli gracz jest w powietrzu, przyspiesza w dol zgodnie z Physics.gravity.
        /// </summary>
        private void ApplyGravity()
        {
            if (_cc.isGrounded && _verticalVelocity < 0f)
            {
                // Gracz jest na ziemi i nie skacze.
                // Ustawiamy mala ujemna wartosc zamiast 0, zeby CharacterController
                // konsekwentnie raportował isGrounded = true.
                // Bez tego: na nierównym terenie gracz "skacze" miedzy stanami.
                _verticalVelocity = GroundedPullDown;
            }
            else
            {
                // Gracz jest w powietrzu (skacze lub spada).
                // Czytamy grawitacje z ustawien fizyki projektu (PhysicsConfig.SetGravity).
                // NIGDY nie hardcodujemy -9.81 -- grawitacja moze byc inna
                // (np. misje pod woda, niska grawitacja na bagnach, itp.).
                _verticalVelocity += Physics.gravity.y * Time.deltaTime;
            }
        }

        // =====================================================================
        // Automatyczne znajdowanie kamery
        // =====================================================================

        /// <summary>
        /// Szuka transform kamery VR. Kolejnosc:
        /// 1. Meta XR: TrackingSpace/CenterEyeAnchor (standardowa hierarchia OVRCameraRig)
        /// 2. Fallback: Camera.main
        /// Jesli nic nie znajdzie, loguje warning -- trzeba ustawic recznie w inspektorze.
        /// </summary>
        private Transform ResolveHeadTransform()
        {
#if HAS_META_XR
            // OVRCameraRig tworzy hierarchie:
            //   [OVRCameraRig]
            //     TrackingSpace
            //       CenterEyeAnchor  <-- to jest kamera VR
            //       LeftHandAnchor
            //       RightHandAnchor
            var tracking = transform.Find("TrackingSpace");
            if (tracking != null)
            {
                var eye = tracking.Find("CenterEyeAnchor");
                if (eye != null) return eye;
            }
#endif
            // Fallback -- Camera.main dziala zarowno w edytorze jak i na urzadzeniu.
            if (Camera.main != null)
            {
                Debug.Log($"{LOG} ResolveHead: fallback Camera.main ({Camera.main.name})");
                return Camera.main.transform;
            }

            Debug.LogError($"{LOG} ResolveHead: BRAK KAMERY! Ustaw _headTransform w inspektorze.");
            return null;
        }
    }
}
