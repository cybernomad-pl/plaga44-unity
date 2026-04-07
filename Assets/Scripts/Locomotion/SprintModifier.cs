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
// Skok ustawia VerticalVelocity w LocomotionController na jumpForce.
// Grawitacja jest obslugiwana przez LocomotionController.ApplyGravity().
//
// POWIAZANIE Z LOKOMOCJA:
// Ten komponent NIE jest singletonem ani DontDestroyOnLoad.
// Powinien byc na tym samym GameObject co LocomotionController
// (lub w tej samej hierarchii). Szuka LocomotionController w Awake().
//
// REFERENCJA: Zmodyfikowany z reference-branch.
// Roznice vs original:
//   - Uzywa LocomotionController zamiast OVRPlayerController
//   - NIE jest auto-tworzony (RuntimeInitializeOnLoadMethod usuniete)
//   - NIE robi FindObjectsByType co klatke
//   - Szuka LocomotionController RAZ w Awake()
//   - Guard na GameState.CanMove
//   - #if HAS_META_XR z fallbackiem na klawiature
//
// INPUT MAPPING:
//   Quest:    L3 = sprint, B = skok, R3 = crouch
//   Edytor:   Shift = sprint, Space = skok, LCtrl = crouch
// =============================================================================

using UnityEngine;

namespace Plaga44.Locomotion
{
    /// <summary>
    /// Modyfikator lokomocji: sprint, skok, crouch.
    /// Attach do tego samego GameObject co LocomotionController.
    /// </summary>
    [DisallowMultipleComponent]
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

        [Header("Crouch")]
        [Tooltip("Wysokosc CharacterControllera podczas croucha.")]
        public float crouchHeight = 1.0f;

        [Tooltip("Predkosc przejscia miedzy staniem a crouchem.")]
        public float crouchSpeed = 8f;

        // =====================================================================
        // Stan runtime
        // =====================================================================

        /// <summary>
        /// Referencja do LocomotionController -- szukana RAZ w Awake().
        /// Jesli nie znaleziona, komponent sie wylacza.
        /// </summary>
        private LocomotionController _loco;

        /// <summary>Bazowa predkosc ruchu PRZED sprintem.</summary>
        private float _baseSpeed;

        /// <summary>Czy sprint jest aktywny w tym momencie.</summary>
        private bool _sprinting;

        /// <summary>Timer cooldownu skoku.</summary>
        private float _jumpTimer;

        /// <summary>Bazowa wysokosc CC (przed crouchem).</summary>
        private float _standHeight;

        /// <summary>Bazowy center CC (przed crouchem).</summary>
        private float _standCenterY;

        /// <summary>Czy gracz jest w crouchu.</summary>
        private bool _crouching;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void Awake()
        {
            // Szukamy LocomotionController na tym obiekcie lub w rodzicach/dzieciach.
            _loco = GetComponentInParent<LocomotionController>();
            if (_loco == null)
                _loco = GetComponentInChildren<LocomotionController>();

            if (_loco == null)
            {
                Debug.LogError("[SprintModifier] Brak LocomotionController w hierarchii! " +
                               "Dodaj SprintModifier na ten sam GameObject co LocomotionController.");
                enabled = false;
                return;
            }

            _baseSpeed = _loco.moveSpeed;
            _standHeight = _loco.CharController.height;
            _standCenterY = _loco.CharController.center.y;
        }

        private void Update()
        {
            if (!GameState.CanMove) return;

            HandleSprint();
            HandleJump();
            HandleCrouch();
        }

        private void OnDisable()
        {
            if (_loco == null) return;

            // Przywroc bazowa predkosc
            if (_sprinting)
            {
                _loco.moveSpeed = _baseSpeed;
                _sprinting = false;
            }

            // Przywroc pelna wysokosc
            if (_crouching)
            {
                var cc = _loco.CharController;
                cc.height = _standHeight;
                cc.center = new Vector3(0f, _standCenterY, 0f);
                _crouching = false;

                var camHeight = _loco.GetComponent<EditorCameraHeight>();
                if (camHeight != null)
                    camHeight.eyeHeight = 1.65f;
            }
        }

        // =====================================================================
        // Sprint
        // =====================================================================

        /// <summary>
        /// L3 (lewy thumbstick click) = sprint.
        /// Mnozy moveSpeed przez sprintMultiplier, przywraca po puszczeniu.
        /// </summary>
        private void HandleSprint()
        {
            bool pressed = GetSprintInput();

            if (pressed && !_sprinting)
            {
                // Zapamietujemy aktualna predkosc jako baze
                // (moze byc inna niz poczatkowa jesli LocomotionManager ja zmienil).
                _baseSpeed = _loco.moveSpeed;
                _loco.moveSpeed = _baseSpeed * sprintMultiplier;
                _sprinting = true;
            }
            else if (!pressed && _sprinting)
            {
                _loco.moveSpeed = _baseSpeed;
                _sprinting = false;
            }
        }

        // =====================================================================
        // Skok
        // =====================================================================

        /// <summary>
        /// B (prawy kontroler) = skok.
        /// Warunki: na ziemi + cooldown minal.
        /// Ustawia VerticalVelocity w LocomotionController.
        /// </summary>
        private void HandleJump()
        {
            _jumpTimer -= Time.deltaTime;

            if (GetJumpInput() && _jumpTimer <= 0f && _loco.IsGrounded)
            {
                _loco.VerticalVelocity = jumpForce;
                _jumpTimer = jumpCooldown;
            }
        }

        // =====================================================================
        // Crouch
        // =====================================================================

        /// <summary>
        /// LCtrl (klawiatura) / R3 (Quest) = crouch toggle.
        /// Zmniejsza wysokosc CharacterControllera i obniza kamerę proporcjonalnie.
        /// </summary>
        private void HandleCrouch()
        {
            if (GetCrouchInput())
                _crouching = !_crouching;

            float targetHeight = _crouching ? crouchHeight : _standHeight;
            float targetCenterY = targetHeight * 0.5f;

            var cc = _loco.CharController;
            cc.height = Mathf.Lerp(cc.height, targetHeight, crouchSpeed * Time.deltaTime);
            cc.center = new Vector3(0f, Mathf.Lerp(cc.center.y, targetCenterY, crouchSpeed * Time.deltaTime), 0f);

            // Obniz kamerę proporcjonalnie
            var camHeight = _loco.GetComponent<EditorCameraHeight>();
            if (camHeight != null)
            {
                float eyeRatio = 1.65f / _standHeight; // proporcja oczy/wysokosc
                camHeight.eyeHeight = Mathf.Lerp(camHeight.eyeHeight, targetHeight * eyeRatio, crouchSpeed * Time.deltaTime);
            }
        }

        // =====================================================================
        // Input
        // =====================================================================

        private bool GetSprintInput()
        {
#if HAS_META_XR
            return OVRInput.Get(OVRInput.Button.PrimaryThumbstick);
#else
            return UnityEngine.Input.GetKey(KeyCode.LeftShift);
#endif
        }

        private bool GetJumpInput()
        {
#if HAS_META_XR
            return OVRInput.GetDown(OVRInput.Button.Two);
#else
            return UnityEngine.Input.GetKeyDown(KeyCode.Space);
#endif
        }

        private bool GetCrouchInput()
        {
#if HAS_META_XR
            return OVRInput.GetDown(OVRInput.Button.SecondaryThumbstick);
#else
            return UnityEngine.Input.GetKeyDown(KeyCode.LeftControl);
#endif
        }
    }
}
