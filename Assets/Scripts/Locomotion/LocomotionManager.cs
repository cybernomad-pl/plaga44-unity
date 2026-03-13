using System;
using UnityEngine;

// HAS_META_XR is defined in ProjectSettings > Player > Scripting Define Symbols
// when Meta XR SDK Core is present. Sub-systems use it internally; LocomotionManager
// only orchestrates them and requires no direct OVR calls.

namespace Plaga44.Locomotion
{
    /// <summary>
    /// Central manager for all locomotion modes in PLAGA '44.
    /// Switches between SmoothLocomotion, TeleportLocomotion, and RoomScale (body tracking).
    /// Attach to the VR rig root GameObject alongside OVRCameraRig (Meta) or XROrigin (Unity XR).
    /// </summary>
    public class LocomotionManager : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Enums
        // -------------------------------------------------------------------------

        public enum LocomotionMode
        {
            SmoothLocomotion,
            Teleport,
            RoomScale
        }

        public enum TurnMode
        {
            Snap,
            Smooth
        }

        // -------------------------------------------------------------------------
        // Inspector fields
        // -------------------------------------------------------------------------

        [Header("Active Mode")]
        [Tooltip("Starting locomotion mode. Can be changed at runtime via SetMode().")]
        [SerializeField] private LocomotionMode _startMode = LocomotionMode.SmoothLocomotion;

        [Header("Movement Config")]
        [Tooltip("Walk speed in metres per second (SmoothLocomotion).")]
        [SerializeField] public float moveSpeed = 2.5f;

        [Tooltip("Snap turn angle in degrees, or smooth turn speed deg/sec.")]
        [SerializeField] public float turnSpeed = 45f;

        [SerializeField] public TurnMode turnMode = TurnMode.Snap;

        [Header("Comfort Vignette")]
        [SerializeField] private bool _enableVignette = true;

        [Header("Component References (auto-found if null)")]
        [SerializeField] private SmoothLocomotion _smoothLocomotion;
        [SerializeField] private TeleportLocomotion _teleportLocomotion;
        [SerializeField] private ComfortVignette _comfortVignette;

        // -------------------------------------------------------------------------
        // Runtime state
        // -------------------------------------------------------------------------

        private LocomotionMode _currentMode;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>Fired when the active locomotion mode changes.</summary>
        public event Action<LocomotionMode> OnModeChanged;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            GatherComponents();
            ApplyMode(_startMode, force: true);
        }

        private void Start()
        {
            // Propagate inspector config to sub-systems after all Awakes have run.
            PushConfigToSubSystems();
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>Returns the currently active locomotion mode.</summary>
        public LocomotionMode CurrentMode => _currentMode;

        /// <summary>
        /// Switches to the given locomotion mode.
        /// RoomScale is typically activated automatically by <see cref="BodyTrackingZone"/>.
        /// </summary>
        public void SetMode(LocomotionMode mode)
        {
            if (mode == _currentMode) return;
            ApplyMode(mode, force: false);
        }

        /// <summary>Re-applies current config values to sub-systems (call after changing fields at runtime).</summary>
        public void RefreshConfig()
        {
            PushConfigToSubSystems();
        }

        // -------------------------------------------------------------------------
        // Internal helpers
        // -------------------------------------------------------------------------

        private void GatherComponents()
        {
            if (_smoothLocomotion == null)
                _smoothLocomotion = GetComponentInChildren<SmoothLocomotion>(includeInactive: true);

            if (_teleportLocomotion == null)
                _teleportLocomotion = GetComponentInChildren<TeleportLocomotion>(includeInactive: true);

            if (_comfortVignette == null)
                _comfortVignette = GetComponentInChildren<ComfortVignette>(includeInactive: true);
        }

        private void ApplyMode(LocomotionMode mode, bool force)
        {
            if (!force && mode == _currentMode) return;

            _currentMode = mode;

            bool smooth    = (mode == LocomotionMode.SmoothLocomotion);
            bool teleport  = (mode == LocomotionMode.Teleport);
            bool roomScale = (mode == LocomotionMode.RoomScale);

            SetEnabled(_smoothLocomotion,   smooth);
            SetEnabled(_teleportLocomotion, teleport);

            // Vignette only makes sense during smooth locomotion.
            if (_comfortVignette != null)
            {
                _comfortVignette.enabled = _enableVignette && smooth;
                if (!smooth) _comfortVignette.SetIntensity(0f);
            }

            if (roomScale)
            {
                Debug.Log("[LocomotionManager] RoomScale active -- controller locomotion disabled.");
            }

            OnModeChanged?.Invoke(mode);
            Debug.Log($"[LocomotionManager] Mode -> {mode}");
        }

        private void PushConfigToSubSystems()
        {
            if (_smoothLocomotion != null)
            {
                _smoothLocomotion.moveSpeed = moveSpeed;
                _smoothLocomotion.turnSpeed = turnSpeed;
                _smoothLocomotion.snapTurns = (turnMode == TurnMode.Snap);
            }

            if (_comfortVignette != null)
                _comfortVignette.enabled = _enableVignette && (_currentMode == LocomotionMode.SmoothLocomotion);
        }

        private static void SetEnabled(MonoBehaviour mb, bool state)
        {
            if (mb != null) mb.enabled = state;
        }
    }
}
