// PLAGA '44 VR - EmersionConfig
// Tunable parameters for all emersion effects.
// Separate from PhysiologyConfig to allow independent tuning of
// "how the body works" vs "how VR represents the body's state".

using UnityEngine;

namespace Plaga44.Emersion
{
    /// <summary>
    /// Configuration for emersion effect intensities and thresholds.
    /// Create assets: Assets > Create > Plaga44 > Emersion Config
    /// </summary>
    [CreateAssetMenu(fileName = "EmersionConfig", menuName = "Plaga44/Emersion Config")]
    public class EmersionConfig : ScriptableObject
    {
        [Header("General")]
        [Tooltip("Rate at which effects transition between states. Higher = faster transitions.")]
        public float effectTransitionRate = 2f;

        // ===== TREMOR =====

        [Header("Tremor")]
        [Tooltip("Maximum controller position offset in meters.")]
        public float maxTremorAmplitude = 0.02f;

        [Tooltip("Haptic feedback multiplier for tremor (0 = no haptics, 1 = maximum).")]
        [Range(0f, 1f)]
        public float hapticTremorMultiplier = 0.5f;

        [Tooltip("Minimum tremor frequency in Hz.")]
        public float minTremorFrequency = 5f;

        [Tooltip("Maximum tremor frequency in Hz (higher intensity = higher frequency).")]
        public float maxTremorFrequency = 20f;

        [Tooltip("Tremor intensity below which effects are not applied (prevents micro-jitter).")]
        public float tremorDeadzone = 0.05f;

        // ===== VISUAL EFFECTS =====

        [Header("Visual Effects")]
        [Tooltip("Maximum FOV reduction in degrees for tunnel vision effect.")]
        public float maxFOVReduction = 30f;

        [Tooltip("Maximum chromatic aberration offset in UV space.")]
        public float maxChromaticAberration = 0.01f;

        [Tooltip("Maximum blur radius for exhaustion/dehydration blur.")]
        public float maxBlurRadius = 5f;

        [Tooltip("Maximum vignette intensity.")]
        [Range(0f, 1f)]
        public float maxVignetteIntensity = 0.8f;

        [Tooltip("Color desaturation speed (degrees per second the saturation changes).")]
        public float desaturationRate = 1f;

        // ===== INPUT LAG =====
        // From IPK: "celowy lag" -- deliberately induced delay

        [Header("Input Lag")]
        [Tooltip("Maximum input lag in seconds (at maximum toxin level).")]
        public float maxInputLagSeconds = 0.3f;

        [Tooltip("Minimum toxin level to start introducing lag.")]
        public float lagOnsetToxinLevel = 0.1f;

        // ===== HALLUCINATIONS =====
        // From SPARK: phantom sounds, voice distortion, shadow movement

        [Header("Hallucinations")]
        [Tooltip("Check interval in seconds between hallucination probability rolls.")]
        public float hallucinationCheckInterval = 5f;

        [Tooltip("Minimum hallucination duration in seconds.")]
        public float minHallucinationDuration = 1f;

        [Tooltip("Maximum hallucination duration in seconds.")]
        public float maxHallucinationDuration = 5f;

        [Tooltip("Shadow movement speed range.")]
        public float shadowMovementSpeed = 2f;

        [Tooltip("Phantom sound volume range (0-1).")]
        [Range(0f, 1f)]
        public float phantomSoundMaxVolume = 0.5f;

        // ===== CONTROLLER LOCKOUT =====

        [Header("Controller Lockout")]
        [Tooltip("Arm function below which controller is fully locked.")]
        public float lockoutThreshold = 0.1f;

        [Tooltip("Arm function below which partial degradation begins.")]
        public float degradationOnset = 0.8f;

        [Tooltip("Haptic pulse when controller lockout occurs.")]
        public float lockoutHapticPulse = 0.8f;

        // ===== COMFORT SAFETY =====
        // Even though emersion is about controlled discomfort,
        // we need safety limits to prevent actual VR sickness.

        [Header("Comfort Safety Limits")]
        [Tooltip("Maximum combined effect intensity. Prevents VR sickness.")]
        [Range(0f, 1f)]
        public float maxCombinedEffectIntensity = 0.85f;

        [Tooltip("Frame rate below which effects are reduced. Performance safety.")]
        public float performanceSafetyFPS = 72f;

        [Tooltip("Maximum duration of any single intense effect before forced cooldown.")]
        public float maxIntenseEffectDuration = 30f;

        [Tooltip("Cooldown duration after maximum effect reached.")]
        public float effectCooldownDuration = 10f;
    }
}
