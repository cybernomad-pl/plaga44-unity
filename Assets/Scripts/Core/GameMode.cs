// PLAGA '44 VR - GameMode System
// Dual-mode architecture from IPK grant D.VI months 07-08:
// Mode A (edu-tourist): exploration without survival pressure
// Mode B (hardcore survival): full physiology, permadeath, noEZUS framing
//
// "Tryby odrebne, ale spojne." - Shared assets, mechanical activation layer.

using System;
using UnityEngine;

namespace Plaga44.Core
{
    /// <summary>
    /// Controls which game systems are active based on selected mode.
    /// Implements the content/mechanics layer separation required for
    /// MON/WOT licensing (IPK D.VI).
    /// </summary>
    public class GameMode : MonoBehaviour
    {
        [Header("Current Mode")]
        [SerializeField] private PlayMode currentMode = PlayMode.HardcoreSurvival;

        [Header("Mode A: Edu-Tourist Configuration")]
        [Tooltip("Free teleport enabled for accessibility.")]
        [SerializeField] private bool eduTouristTeleport = true;
        [Tooltip("Debug camera available for users with limited mobility.")]
        [SerializeField] private bool eduTouristDebugCamera = true;
        [Tooltip("Heritage info points enabled (Szlak Orlich Gniazd, Jura geology).")]
        [SerializeField] private bool eduTouristInfoPoints = true;
        [Tooltip("Survival consequences disabled.")]
        [SerializeField] private bool eduTouristNoConsequences = true;

        [Header("Mode B: Hardcore Survival Configuration")]
        [Tooltip("Full physiology simulation active.")]
        [SerializeField] private bool hardcoreFullPhysiology = true;
        [Tooltip("Permadeath per session.")]
        [SerializeField] private bool hardcorePermadeath = true;
        [Tooltip("No HUD - physical diegetic interface only (watch, compass, paper map).")]
        [SerializeField] private bool hardcoreNoHUD = true;
        [Tooltip("Orbital respawn via noEZUS narrative frame.")]
        [SerializeField] private bool hardcoreOrbitalRespawn = true;

        // Events for system activation/deactivation
        public event Action<PlayMode> OnModeChanged;
        public event Action<bool> OnPhysiologyToggled;
        public event Action<bool> OnSurvivalToggled;
        public event Action<bool> OnInfoPointsToggled;
        public event Action<bool> OnHUDToggled;

        public PlayMode CurrentMode => currentMode;
        public bool IsEduTourist => currentMode == PlayMode.EduTourist;
        public bool IsHardcoreSurvival => currentMode == PlayMode.HardcoreSurvival;

        /// <summary>
        /// Switch between game modes. All dependent systems react via events.
        /// </summary>
        public void SetMode(PlayMode mode)
        {
            if (mode == currentMode) return;

            currentMode = mode;
            ApplyModeSettings();
            OnModeChanged?.Invoke(mode);

            Debug.Log($"[GameMode] Switched to: {mode}");
        }

        private void ApplyModeSettings()
        {
            switch (currentMode)
            {
                case PlayMode.EduTourist:
                    // From IPK: "dezaktywacja mechanik survivalowych, wolny teleport,
                    // debug camera dla accessibility, infopunkty z heritage context"
                    OnPhysiologyToggled?.Invoke(false);
                    OnSurvivalToggled?.Invoke(false);
                    OnInfoPointsToggled?.Invoke(true);
                    OnHUDToggled?.Invoke(true); // HUD OK in edu mode
                    break;

                case PlayMode.HardcoreSurvival:
                    // From IPK: "pelna fizjologia, permadeath per session,
                    // orbital respawn via noEZUS narrative frame"
                    OnPhysiologyToggled?.Invoke(true);
                    OnSurvivalToggled?.Invoke(true);
                    OnInfoPointsToggled?.Invoke(false);
                    OnHUDToggled?.Invoke(false); // No HUD - diegetic only
                    break;

                case PlayMode.Training:
                    // B2B mode for MON/WOT licensing
                    // Physiology active but configurable difficulty
                    OnPhysiologyToggled?.Invoke(true);
                    OnSurvivalToggled?.Invoke(true);
                    OnInfoPointsToggled?.Invoke(false);
                    OnHUDToggled?.Invoke(true); // Instructor needs telemetry
                    break;
            }
        }

        /// <summary>
        /// Query whether a specific feature is enabled in current mode.
        /// Used by individual systems to check their activation state.
        /// </summary>
        public bool IsFeatureEnabled(GameFeature feature)
        {
            switch (feature)
            {
                case GameFeature.Physiology:
                    return currentMode != PlayMode.EduTourist;
                case GameFeature.SurvivalConsequences:
                    return currentMode != PlayMode.EduTourist;
                case GameFeature.Permadeath:
                    return currentMode == PlayMode.HardcoreSurvival;
                case GameFeature.FreeTeleport:
                    return currentMode == PlayMode.EduTourist;
                case GameFeature.DebugCamera:
                    return currentMode == PlayMode.EduTourist;
                case GameFeature.HeritageInfoPoints:
                    return currentMode == PlayMode.EduTourist || currentMode == PlayMode.Training;
                case GameFeature.DiegeticUI:
                    return currentMode == PlayMode.HardcoreSurvival;
                case GameFeature.OrbitalRespawn:
                    return currentMode != PlayMode.EduTourist;
                case GameFeature.InstructorDashboard:
                    return currentMode == PlayMode.Training;
                case GameFeature.CoopMultiplayer:
                    return true; // Available in all modes
                default:
                    return false;
            }
        }
    }

    public enum PlayMode
    {
        /// <summary>
        /// Exploration without survival pressure. Accessibility-first.
        /// Virtual tourism of Jura Krakowsko-Czestochowska.
        /// </summary>
        EduTourist,

        /// <summary>
        /// Full physiology simulation, permadeath, no HUD.
        /// The core PLAGA '44 experience.
        /// </summary>
        HardcoreSurvival,

        /// <summary>
        /// B2B training mode for MON/WOT/institutional licensing.
        /// Configurable difficulty, instructor dashboard, telemetry.
        /// </summary>
        Training
    }

    public enum GameFeature
    {
        Physiology,
        SurvivalConsequences,
        Permadeath,
        FreeTeleport,
        DebugCamera,
        HeritageInfoPoints,
        DiegeticUI,
        OrbitalRespawn,
        InstructorDashboard,
        CoopMultiplayer,
        WeaponSystem,
        CraftingSystem,
        WeatherSystem
    }
}
