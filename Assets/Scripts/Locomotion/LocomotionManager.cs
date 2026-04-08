// =============================================================================
// LocomotionManager.cs
// CYBERNOMAD -- Centralny zarzadca trybów lokomocji w PLAGA '44.
//
// FAZA 1 (MVP):
// Aktualnie wspiera TYLKO tryb SmoothLocomotion (thumbstick + CharacterController).
// Teleport i RoomScale (body tracking) beda dodane w Fazach 2-3.
//
// DLACZEGO OSOBNY MANAGER?
// LocomotionController zajmuje sie RUCHEM (input -> Move()).
// LocomotionManager zajmuje sie ORKIESTRACJA:
//   - Który tryb lokomocji jest aktywny?
//   - Jakie parametry maja byc przekazane do aktywnego systemu?
//   - Powiadamianie innych systemow o zmianie trybu (event OnModeChanged).
//
// SETUP NA SCENIE:
// Attach do VR rig root (ten sam GameObject co OVRCameraRig).
// LocomotionController -- szukany automatycznie w dzieciach.
//
// REFERENCJA: Uproszczony z reference-branch (usunieto TeleportLocomotion, RoomScale, ComfortVignette).
// =============================================================================

using System;
using UnityEngine;

namespace Plaga44.Locomotion
{
    /// <summary>
    /// Centralny manager lokomocji. Orkiestruje tryby ruchu i przekazuje
    /// konfiguracje do podsystemow (LocomotionController).
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
        /// Obrot NIE jest zaimplementowany w Fazie 1 -- placeholder.
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

        [Header("Referencje komponentow (auto-znajdowane jesli puste)")]
        [Tooltip("Referencja do LocomotionController. Szukany automatycznie jesli null.")]
        [SerializeField] private LocomotionController _locomotionController;

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

        private const string LOG = "[PLAGA44][LocoManager]";

        private void Awake()
        {
            Debug.Log($"{LOG} Awake: startMode={_startMode}, moveSpeed={moveSpeed}");
            GatherComponents();
            ApplyMode(_startMode, force: true);
        }

        private void Start()
        {
            PushConfigToSubSystems();
            Debug.Log($"{LOG} Start: pushed moveSpeed={moveSpeed} to LocomotionController");
        }

        private void OnEnable() => Debug.Log($"{LOG} OnEnable");
        private void OnDisable() => Debug.Log($"{LOG} OnDisable");

        // =====================================================================
        // Publiczne API
        // =====================================================================

        /// <summary>Zwraca aktualnie aktywny tryb lokomocji.</summary>
        public LocomotionMode CurrentMode => _currentMode;

        /// <summary>
        /// Przelacza na podany tryb lokomocji.
        /// W Fazie 1 jedynym funkcjonalnym trybem jest SmoothLocomotion.
        /// </summary>
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

        private void GatherComponents()
        {
            if (_locomotionController == null)
                _locomotionController = GetComponentInChildren<LocomotionController>(includeInactive: true);
            Debug.Log($"{LOG} GatherComponents: LocoCtrl={(_locomotionController != null ? _locomotionController.gameObject.name : "NULL")}");
        }

        private void ApplyMode(LocomotionMode mode, bool force)
        {
            if (!force && mode == _currentMode) return;

            _currentMode = mode;

            bool smooth = (mode == LocomotionMode.SmoothLocomotion);
            SetEnabled(_locomotionController, smooth);

            if (mode == LocomotionMode.Teleport)
                Debug.LogWarning("[LocomotionManager] Teleport nie jest zaimplementowany (Faza 2).");
            if (mode == LocomotionMode.RoomScale)
                Debug.LogWarning("[LocomotionManager] RoomScale nie jest zaimplementowany (Faza 3).");

            OnModeChanged?.Invoke(mode);
            Debug.Log($"[LocomotionManager] Tryb -> {mode}");
        }

        private void PushConfigToSubSystems()
        {
            if (_locomotionController != null)
                _locomotionController.moveSpeed = moveSpeed;
        }

        private static void SetEnabled(MonoBehaviour mb, bool state)
        {
            if (mb != null) mb.enabled = state;
        }
    }
}
