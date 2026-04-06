// =============================================================================
// LocomotionManager.cs
// CYBERNOMAD -- Centralny zarzadca trybów lokomocji w PLAGA '44.
//
// FAZA 1 (MVP):
// Aktualnie wspiera TYLKO tryb SmoothLocomotion (thumbstick + CharacterController).
// Teleport i RoomScale (body tracking) beda dodane w Fazach 2-3.
// Enum LocomotionMode juz je przewiduje, ale logika przelaczania jest uproszczona.
//
// DLACZEGO OSOBNY MANAGER?
// LocomotionController zajmuje sie RUCHEM (input -> Move()).
// LocomotionManager zajmuje sie ORKIESTRACJA:
//   - Który tryb lokomocji jest aktywny?
//   - Jakie parametry maja byc przekazane do aktywnego systemu?
//   - Czy winieta komfortu powinna byc wlaczona?
//   - Powiadamianie innych systemow o zmianie trybu (event OnModeChanged).
//
// Dzieki tej separacji mozemy w przyszlosci dodac Teleport/RoomScale
// bez zmieniania LocomotionController.
//
// SETUP NA SCENIE:
// Attach do VR rig root (ten sam GameObject co OVRCameraRig).
// LocomotionController, ComfortVignette -- szukane automatycznie w dzieciach.
//
// REFERENCJA: Uproszczony z reference-branch (usunieto TeleportLocomotion, RoomScale).
// =============================================================================

using System;
using UnityEngine;

namespace Plaga44.Locomotion
{
    /// <summary>
    /// Centralny manager lokomocji. Orkiestruje tryby ruchu i przekazuje
    /// konfiguracje do podsystemow (LocomotionController, ComfortVignette).
    /// Faza 1: tylko SmoothLocomotion. Faza 2-3: teleport + room-scale.
    /// </summary>
    public class LocomotionManager : MonoBehaviour
    {
        // =====================================================================
        // Enumy
        // =====================================================================

        /// <summary>
        /// Tryby lokomocji dostepne w grze.
        /// Faza 1 uzywa tylko SmoothLocomotion.
        /// Teleport i RoomScale sa zarezerwowane na przyszlosc.
        /// </summary>
        public enum LocomotionMode
        {
            /// <summary>Klasyczny ruch thumbstickiem -- Faza 1 MVP.</summary>
            SmoothLocomotion,

            /// <summary>Teleportacja -- Faza 2 (do zaimplementowania).</summary>
            Teleport,

            /// <summary>Room-scale body tracking -- Faza 3 (do zaimplementowania).</summary>
            RoomScale
        }

        /// <summary>
        /// Tryby obrotu. Snap = skok o ustalony kat, Smooth = plynny obrot.
        /// Obrot NIE jest zaimplementowany w Fazie 1 -- to jest placeholder na Faze 2.
        /// </summary>
        public enum TurnMode
        {
            /// <summary>Skok o ustalony kat (np. 45 stopni) -- mniej motion sickness.</summary>
            Snap,

            /// <summary>Plynny obrot -- bardziej immersyjny, ale moze powodowac motion sickness.</summary>
            Smooth
        }

        // =====================================================================
        // Pola inspektora
        // =====================================================================

        [Header("Aktywny tryb")]
        [Tooltip("Poczatkowy tryb lokomocji. Mozna zmienic w runtime przez SetMode().")]
        [SerializeField] private LocomotionMode _startMode = LocomotionMode.SmoothLocomotion;

        [Header("Konfiguracja ruchu")]
        [Tooltip("Predkosc chodzenia w metrach na sekunde (SmoothLocomotion).")]
        [SerializeField] public float moveSpeed = 2.5f;

        [Tooltip("NIEAKTYWNE W FAZIE 1. Kat snap turna lub predkosc smooth turna (stopnie/sek).")]
        [SerializeField] public float turnSpeed = 45f;

        [Tooltip("NIEAKTYWNE W FAZIE 1. Tryb obrotu (snap vs smooth).")]
        [SerializeField] public TurnMode turnMode = TurnMode.Snap;

        [Header("Winieta komfortu")]
        [Tooltip("Czy winieta komfortu jest wlaczona. Zmniejsza motion sickness " +
                 "przyciemniajac krawedzie widoku podczas ruchu.")]
        [SerializeField] private bool _enableVignette = true;

        [Header("Referencje komponentow (auto-znajdowane jesli puste)")]
        [Tooltip("Referencja do LocomotionController. Szukany automatycznie jesli null.")]
        [SerializeField] private LocomotionController _locomotionController;

        [Tooltip("Referencja do ComfortVignette. Szukana automatycznie jesli null.")]
        [SerializeField] private ComfortVignette _comfortVignette;

        // =====================================================================
        // Stan runtime
        // =====================================================================

        /// <summary>Aktualnie aktywny tryb lokomocji.</summary>
        private LocomotionMode _currentMode;

        // =====================================================================
        // Eventy
        // =====================================================================

        /// <summary>
        /// Odpala sie gdy aktywny tryb lokomocji sie zmieni.
        /// Uzywane np. przez UI do aktualizacji ikonki trybu.
        /// </summary>
        public event Action<LocomotionMode> OnModeChanged;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void Awake()
        {
            // Szukamy komponentow w dzieciach jesli nie zostaly ustawione w inspektorze.
            GatherComponents();

            // Ustawiamy poczatkowy tryb (domyslnie SmoothLocomotion).
            // force: true -- wymuszamy ustawienie nawet jesli _currentMode juz jest taki sam
            // (bo _currentMode domyslnie = 0 = SmoothLocomotion, a chcemy wykonac logike).
            ApplyMode(_startMode, force: true);
        }

        private void Start()
        {
            // Propagujemy wartosci z inspektora do podsystemow.
            // Robimy to w Start() a nie Awake(), bo podsystemy moga
            // potrzebowac wykonac swoje Awake() najpierw.
            PushConfigToSubSystems();
        }

        // =====================================================================
        // Publiczne API
        // =====================================================================

        /// <summary>Zwraca aktualnie aktywny tryb lokomocji.</summary>
        public LocomotionMode CurrentMode => _currentMode;

        /// <summary>
        /// Przelacza na podany tryb lokomocji.
        /// W Fazie 1 jedynym funkcjonalnym trybem jest SmoothLocomotion.
        /// Teleport i RoomScale loguja warning.
        /// </summary>
        /// <param name="mode">Docelowy tryb lokomocji.</param>
        public void SetMode(LocomotionMode mode)
        {
            if (mode == _currentMode) return;
            ApplyMode(mode, force: false);
        }

        /// <summary>
        /// Wymusza ponowne przekazanie konfiguracji do podsystemow.
        /// Wywolaj po zmianie moveSpeed, turnSpeed itp. w runtime.
        /// </summary>
        public void RefreshConfig()
        {
            PushConfigToSubSystems();
        }

        // =====================================================================
        // Logika wewnetrzna
        // =====================================================================

        /// <summary>
        /// Szuka brakujacych referencji do komponentow w dzieciach.
        /// includeInactive: true -- szukamy tez wylaczonych komponentow,
        /// bo mogly byc wylaczone przez poprzednie ustawienie trybu.
        /// </summary>
        private void GatherComponents()
        {
            if (_locomotionController == null)
                _locomotionController = GetComponentInChildren<LocomotionController>(includeInactive: true);

            if (_comfortVignette == null)
                _comfortVignette = GetComponentInChildren<ComfortVignette>(includeInactive: true);
        }

        /// <summary>
        /// Ustawia aktywny tryb lokomocji i wlacza/wylacza odpowiednie komponenty.
        /// </summary>
        /// <param name="mode">Nowy tryb.</param>
        /// <param name="force">Jesli true, wykonuje logike nawet gdy tryb sie nie zmienil.</param>
        private void ApplyMode(LocomotionMode mode, bool force)
        {
            if (!force && mode == _currentMode) return;

            _currentMode = mode;

            // W Fazie 1 tylko SmoothLocomotion jest zaimplementowany.
            bool smooth = (mode == LocomotionMode.SmoothLocomotion);

            // Wlacz/wylacz LocomotionController w zaleznosci od trybu.
            SetEnabled(_locomotionController, smooth);

            // Winieta komfortu ma sens tylko podczas smooth locomotion --
            // teleportacja i room-scale nie powoduja motion sickness.
            if (_comfortVignette != null)
            {
                _comfortVignette.enabled = _enableVignette && smooth;
                if (!smooth) _comfortVignette.SetIntensity(0f);
            }

            // Logowanie trybów jeszcze niezaimplementowanych.
            if (mode == LocomotionMode.Teleport)
                Debug.LogWarning("[LocomotionManager] Teleport nie jest zaimplementowany (Faza 2).");
            if (mode == LocomotionMode.RoomScale)
                Debug.LogWarning("[LocomotionManager] RoomScale nie jest zaimplementowany (Faza 3).");

            // Powiadomienie subskrybentow o zmianie trybu.
            OnModeChanged?.Invoke(mode);
            Debug.Log($"[LocomotionManager] Tryb -> {mode}");
        }

        /// <summary>
        /// Przekazuje wartosci konfiguracji z inspektora LocomotionManagera
        /// do podsystemow (LocomotionController, ComfortVignette).
        /// Dzieki temu designer moze ustawiac wartosci w jednym miejscu.
        /// </summary>
        private void PushConfigToSubSystems()
        {
            if (_locomotionController != null)
            {
                _locomotionController.moveSpeed = moveSpeed;
                // turnSpeed nie jest jeszcze uzywany w LocomotionController (Faza 2).
            }

            if (_comfortVignette != null)
                _comfortVignette.enabled = _enableVignette && (_currentMode == LocomotionMode.SmoothLocomotion);
        }

        /// <summary>
        /// Helper do wlaczania/wylaczania MonoBehaviour z null-checkiem.
        /// </summary>
        private static void SetEnabled(MonoBehaviour mb, bool state)
        {
            if (mb != null) mb.enabled = state;
        }
    }
}
