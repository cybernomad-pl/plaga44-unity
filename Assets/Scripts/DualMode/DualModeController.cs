// PLAGA '44 - Dual Mode Controller
// Manages Mode A (Edu-Tourist) vs Mode B (Hardcore Survival) system activation.
// Part of issue #23: Unity VR project structure and dual-mode scene architecture

using UnityEngine;
using System.Collections.Generic;

namespace Plaga44.DualMode
{
    /// <summary>
    /// Controls which gameplay systems are active based on selected game mode.
    ///
    /// From IPK grant D.VI (months 07-08):
    ///
    /// Mode A (Edu-Tourist):
    /// - Free teleport locomotion
    /// - Debug camera for accessibility
    /// - Info points with heritage context (Szlak Orlich Gniazd, Jura geology)
    /// - No survival pressure (physiology disabled)
    /// - Heritage exploration focus: Olsztyn castle ruins, limestone formations
    ///
    /// Mode B (Hardcore Survival):
    /// - Full physiology-as-controller simulation
    /// - Permadeath per session (death -> BARKA orbital respawn via noEZUS)
    /// - No HUD - physical diegetic interface (watch for stats, compass, paper map)
    /// - Full environmental hazards (weather, NPCs, terrain)
    /// - 4-player co-op via Photon
    ///
    /// Shared between modes:
    /// - Terrain and environment rendering
    /// - Audio system (ambient sounds)
    /// - Basic locomotion (walking)
    /// - Asset loading and streaming
    /// </summary>
    public class DualModeController : MonoBehaviour
    {
        public static DualModeController Instance { get; private set; }

        [Header("Mode A Components (Edu-Tourist)")]
        [SerializeField] private MonoBehaviour[] eduTouristComponents;
        [SerializeField] private GameObject[] eduTouristObjects;

        [Header("Mode B Components (Hardcore Survival)")]
        [SerializeField] private MonoBehaviour[] hardcoreSurvivalComponents;
        [SerializeField] private GameObject[] hardcoreSurvivalObjects;

        [Header("Shared Components (Always Active)")]
        [SerializeField] private MonoBehaviour[] sharedComponents;

        [Header("Heritage Info System (Mode A only)")]
        [SerializeField] private bool enableInfoPoints = true;
        [SerializeField] private bool enableDebugCamera = true;
        [SerializeField] private bool enableFreeTeleport = true;

        [Header("Survival Systems (Mode B only)")]
        [SerializeField] private bool enablePhysiology = true;
        [SerializeField] private bool enablePermadeath = true;
        [SerializeField] private bool enableDiegeticUI = true;
        [SerializeField] private bool enableNPCThreats = true;

        private Core.GameMode activeMode;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (Core.GameManager.Instance != null)
            {
                Core.GameManager.Instance.OnGameModeChanged += OnGameModeChanged;
                ActivateMode(Core.GameManager.Instance.CurrentGameMode);
            }
        }

        private void OnDestroy()
        {
            if (Core.GameManager.Instance != null)
            {
                Core.GameManager.Instance.OnGameModeChanged -= OnGameModeChanged;
            }
        }

        private void OnGameModeChanged(Core.GameMode mode)
        {
            ActivateMode(mode);
        }

        /// <summary>
        /// Activate the specified game mode, enabling/disabling appropriate systems.
        /// </summary>
        public void ActivateMode(Core.GameMode mode)
        {
            activeMode = mode;

            switch (mode)
            {
                case Core.GameMode.EduTourist:
                    ActivateEduTouristMode();
                    break;
                case Core.GameMode.HardcoreSurvival:
                    ActivateHardcoreSurvivalMode();
                    break;
            }

            Debug.Log($"[PLAGA44] Game mode activated: {mode}");
        }

        private void ActivateEduTouristMode()
        {
            // Enable Mode A components
            SetComponentsActive(eduTouristComponents, true);
            SetObjectsActive(eduTouristObjects, true);

            // Disable Mode B components
            SetComponentsActive(hardcoreSurvivalComponents, false);
            SetObjectsActive(hardcoreSurvivalObjects, false);

            // Ensure shared components are active
            SetComponentsActive(sharedComponents, true);
        }

        private void ActivateHardcoreSurvivalMode()
        {
            // Disable Mode A components
            SetComponentsActive(eduTouristComponents, false);
            SetObjectsActive(eduTouristObjects, false);

            // Enable Mode B components
            SetComponentsActive(hardcoreSurvivalComponents, true);
            SetObjectsActive(hardcoreSurvivalObjects, true);

            // Ensure shared components are active
            SetComponentsActive(sharedComponents, true);
        }

        private void SetComponentsActive(MonoBehaviour[] components, bool active)
        {
            if (components == null) return;
            foreach (var component in components)
            {
                if (component != null)
                    component.enabled = active;
            }
        }

        private void SetObjectsActive(GameObject[] objects, bool active)
        {
            if (objects == null) return;
            foreach (var obj in objects)
            {
                if (obj != null)
                    obj.SetActive(active);
            }
        }

        /// <summary>
        /// Check if a specific feature is enabled in current mode.
        /// </summary>
        public bool IsFeatureEnabled(string feature)
        {
            switch (feature)
            {
                case "InfoPoints": return activeMode == Core.GameMode.EduTourist && enableInfoPoints;
                case "DebugCamera": return activeMode == Core.GameMode.EduTourist && enableDebugCamera;
                case "FreeTeleport": return activeMode == Core.GameMode.EduTourist && enableFreeTeleport;
                case "Physiology": return activeMode == Core.GameMode.HardcoreSurvival && enablePhysiology;
                case "Permadeath": return activeMode == Core.GameMode.HardcoreSurvival && enablePermadeath;
                case "DiegeticUI": return activeMode == Core.GameMode.HardcoreSurvival && enableDiegeticUI;
                case "NPCThreats": return activeMode == Core.GameMode.HardcoreSurvival && enableNPCThreats;
                default: return false;
            }
        }

        /// <summary>
        /// Get the active game mode.
        /// </summary>
        public Core.GameMode GetActiveMode()
        {
            return activeMode;
        }
    }
}
