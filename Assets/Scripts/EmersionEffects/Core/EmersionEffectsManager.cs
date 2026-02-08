// PLAGA '44 - Emersion Effects System
// EmersionEffectsManager.cs - Central manager that coordinates all emersion effect controllers
// Singleton pattern for global access; all effect controllers register here

using UnityEngine;

namespace Plaga44.EmersionEffects.Core
{
    /// <summary>
    /// Central manager for the PLAGA '44 emersion effects system.
    /// Holds the shared PlayerPhysiologyState and coordinates all effect controllers.
    /// Attach to a persistent GameObject in the scene.
    /// </summary>
    public class EmersionEffectsManager : MonoBehaviour
    {
        public static EmersionEffectsManager Instance { get; private set; }

        [Header("Player Physiology")]
        [Tooltip("The shared physiological state that drives all emersion effects.")]
        public PlayerPhysiologyState PlayerState = new PlayerPhysiologyState();

        [Header("Global Controls")]
        [Tooltip("Master toggle for all emersion effects.")]
        public bool EffectsEnabled = true;

        [Tooltip("Global intensity multiplier (0 = off, 1 = normal, 2 = amplified).")]
        [Range(0f, 2f)]
        public float GlobalIntensityMultiplier = 1f;

        [Header("Comfort Settings")]
        [Tooltip("Enable vignette during locomotion to reduce VR sickness.")]
        public bool EnableComfortVignette = true;

        [Tooltip("Scale camera shake effects (0 = disabled, 1 = full).")]
        [Range(0f, 1f)]
        public float CameraShakeScale = 1f;

        [Tooltip("Reduce flickering effects for photosensitive players.")]
        public bool ReduceFlicker;

        [Tooltip("Use fade-to-black only (no sudden visual cuts).")]
        public bool BlackoutFadeOnly = true;

        [Tooltip("Scale tremor intensity (0 = disabled, 1 = full).")]
        [Range(0f, 1f)]
        public float TremorScale = 1f;

        [Tooltip("Disable auditory hallucinations.")]
        public bool DisableHallucinations;

        [Header("Configuration")]
        [Tooltip("Path to EmersionEffectsConfig.json in StreamingAssets or Resources.")]
        public string ConfigPath = "EmersionEffects/EmersionEffectsConfig";

        private float _timeSinceLastUpdate;
        private const float StateUpdateInterval = 0.05f; // 20Hz state broadcasts

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[EmersionEffects] Duplicate EmersionEffectsManager detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Debug.Log("[EmersionEffects] EmersionEffectsManager initialized.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!EffectsEnabled) return;

            // Clamp all state values to valid ranges each frame
            ClampPlayerState();

            _timeSinceLastUpdate += Time.deltaTime;
            if (_timeSinceLastUpdate >= StateUpdateInterval)
            {
                _timeSinceLastUpdate = 0f;
                BroadcastStateUpdate();
            }
        }

        /// <summary>
        /// Force an immediate state broadcast to all effect controllers.
        /// Use when a major event occurs (explosion, injury, etc.).
        /// </summary>
        public void ForceStateUpdate()
        {
            BroadcastStateUpdate();
        }

        /// <summary>
        /// Apply a sudden shock event (explosion, nearby gunfire, etc.).
        /// Temporarily spikes stress, fear, and triggers related audio/visual effects.
        /// </summary>
        /// <param name="intensity">Shock intensity from 0 to 1.</param>
        /// <param name="isExplosion">Whether this was an explosion (triggers muffled hearing).</param>
        public void ApplyShockEvent(float intensity, bool isExplosion = false)
        {
            intensity = Mathf.Clamp01(intensity) * GlobalIntensityMultiplier;

            PlayerState.Stress = Mathf.Min(100f, PlayerState.Stress + intensity * 40f);
            PlayerState.Fear = Mathf.Min(100f, PlayerState.Fear + intensity * 30f);

            if (isExplosion)
            {
                PlayerState.HasConcussion = intensity > 0.7f;
            }

            ForceStateUpdate();

            Debug.Log($"[EmersionEffects] Shock event applied: intensity={intensity:F2}, explosion={isExplosion}");
        }

        /// <summary>
        /// Apply damage to the player and trigger associated emersion responses.
        /// </summary>
        /// <param name="damage">Damage amount (0-100).</param>
        /// <param name="causesBleeding">Whether the damage causes ongoing blood loss.</param>
        public void ApplyDamage(float damage, bool causesBleeding = false)
        {
            PlayerState.Health = Mathf.Max(0f, PlayerState.Health - damage);
            PlayerState.Stress = Mathf.Min(100f, PlayerState.Stress + damage * 0.5f);

            if (causesBleeding)
            {
                PlayerState.BloodLoss = Mathf.Min(100f, PlayerState.BloodLoss + damage * 0.3f);
            }

            ForceStateUpdate();
        }

        /// <summary>
        /// Get the effective intensity for a specific effect, applying global multiplier
        /// and comfort settings.
        /// </summary>
        public float GetEffectiveIntensity(float rawIntensity)
        {
            return Mathf.Clamp01(rawIntensity * GlobalIntensityMultiplier);
        }

        private void ClampPlayerState()
        {
            var s = PlayerState;
            s.Health = Mathf.Clamp(s.Health, 0f, 100f);
            s.Stamina = Mathf.Clamp(s.Stamina, 0f, 100f);
            s.MentalHealth = Mathf.Clamp(s.MentalHealth, 0f, 100f);
            s.Hydration = Mathf.Clamp(s.Hydration, 0f, 100f);
            s.Hunger = Mathf.Clamp(s.Hunger, 0f, 100f);
            s.BodyTemperature = Mathf.Clamp(s.BodyTemperature, 30f, 42f);
            s.Fear = Mathf.Clamp(s.Fear, 0f, 100f);
            s.Stress = Mathf.Clamp(s.Stress, 0f, 100f);
            s.Exertion = Mathf.Clamp(s.Exertion, 0f, 100f);
            s.BloodLoss = Mathf.Clamp(s.BloodLoss, 0f, 100f);
        }

        /// <summary>
        /// Broadcasts the current state. Effect controllers check state every broadcast cycle.
        /// This is a lightweight operation - controllers poll the shared state reference.
        /// </summary>
        private void BroadcastStateUpdate()
        {
            // Effect controllers hold a reference to PlayerState and read directly.
            // This method exists as an extension point for events/analytics if needed.
        }

        /// <summary>
        /// Reset all effects and player state to defaults. Used on respawn or scene load.
        /// </summary>
        public void ResetAll()
        {
            PlayerState.ResetToDefaults();
            ForceStateUpdate();
            Debug.Log("[EmersionEffects] All effects reset to defaults.");
        }

#if UNITY_EDITOR
        [Header("Debug")]
        [Tooltip("Show emersion state debug overlay in editor.")]
        public bool ShowDebugOverlay;

        private void OnGUI()
        {
            if (!ShowDebugOverlay) return;

            var s = PlayerState;
            float x = 10f, y = 10f, lineHeight = 18f;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12 };

            GUI.Label(new Rect(x, y, 300, lineHeight), $"=== EMERSION STATE ===", style);
            y += lineHeight;
            GUI.Label(new Rect(x, y, 300, lineHeight), $"Health: {s.Health:F1}  Stamina: {s.Stamina:F1}  Mental: {s.MentalHealth:F1}", style);
            y += lineHeight;
            GUI.Label(new Rect(x, y, 300, lineHeight), $"Hydration: {s.Hydration:F1}  Hunger: {s.Hunger:F1}", style);
            y += lineHeight;
            GUI.Label(new Rect(x, y, 300, lineHeight), $"BodyTemp: {s.BodyTemperature:F1}C  Fear: {s.Fear:F1}  Stress: {s.Stress:F1}", style);
            y += lineHeight;
            GUI.Label(new Rect(x, y, 300, lineHeight), $"Exertion: {s.Exertion:F1}  BloodLoss: {s.BloodLoss:F1}", style);
            y += lineHeight;
            GUI.Label(new Rect(x, y, 300, lineHeight), $"Distress: {s.OverallDistress:F2}  Hypothermia: {s.HypothermiaSeverity:F2}  Tremor: {s.CompositeTremorFactor:F2}", style);
            y += lineHeight;
            GUI.Label(new Rect(x, y, 300, lineHeight), $"Env: {s.CurrentEnvironment}  Weather: {s.CurrentWeather} ({s.WeatherIntensity:F1})  Night: {s.IsNight}", style);
        }
#endif
    }
}
