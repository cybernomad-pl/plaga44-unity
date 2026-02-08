// PLAGA '44 - Vision Effects
// FOV narrowing (tunnel vision), blur, color desaturation at low health.
// Core physiology-as-controller: biological state drives VR visual output.
// Part of issue #31: Audio system and VR emersion effects

using UnityEngine;

namespace Plaga44.Emersion
{
    /// <summary>
    /// Controls visual distortion effects driven by physiological state.
    ///
    /// From IPK grant and scenario docs:
    /// - Perception degradation: progressive blur, tunnel vision, color desaturation
    /// - FOV aberrations: field of view distortions during hypoxia
    /// - Hallucinations: shadow movement at extreme fatigue
    /// - Lag/delay: reaction time delay after mushroom poisoning
    /// - Snow blindness from winter sun without goggles (scenario part 4)
    /// </summary>
    public class VisionEffects : MonoBehaviour
    {
        [Header("Post-Processing Material")]
        [SerializeField] private Material visionEffectMaterial;

        [Header("Tunnel Vision")]
        [SerializeField] private float tunnelVisionMaxRadius = 0.3f;  // 0 = fully narrowed
        [SerializeField] private float tunnelVisionSmoothSpeed = 2f;

        [Header("Blur")]
        [SerializeField] private float maxBlurIntensity = 5f;
        [SerializeField] private float blurSmoothSpeed = 3f;

        [Header("Color Desaturation")]
        [SerializeField] private float desaturationSmoothSpeed = 2f;

        [Header("Vignette (Blood Loss)")]
        [SerializeField] private Color vignetteColor = new Color(0.3f, 0f, 0f, 1f);  // Dark red
        [SerializeField] private float maxVignetteIntensity = 0.8f;

        [Header("Snow Blindness")]
        [SerializeField] private float maxWhiteoutIntensity = 0.7f;
        [SerializeField] private float snowBlindnessBuildRate = 0.01f;  // Per second in bright snow

        [Header("Visual Hallucinations")]
        [SerializeField] private float hallucinationThreshold = 0.8f;
        [SerializeField] private float shadowFlickerInterval = 8f;

        // Shader property IDs
        private static readonly int TunnelRadiusProp = Shader.PropertyToID("_TunnelRadius");
        private static readonly int BlurIntensityProp = Shader.PropertyToID("_BlurIntensity");
        private static readonly int SaturationProp = Shader.PropertyToID("_Saturation");
        private static readonly int VignetteIntensityProp = Shader.PropertyToID("_VignetteIntensity");
        private static readonly int VignetteColorProp = Shader.PropertyToID("_VignetteColor");
        private static readonly int WhiteoutProp = Shader.PropertyToID("_WhiteoutIntensity");
        private static readonly int ChromaticAberrationProp = Shader.PropertyToID("_ChromaticAberration");

        // Input factors (0-1)
        private float healthLevel = 1f;
        private float bloodLossLevel = 0f;
        private float fatigueLevel = 0f;
        private float hypothermiaLevel = 0f;
        private float dehydrationLevel = 0f;
        private float mentalHealthLevel = 1f;
        private float poisoningLevel = 0f;   // Mushroom poisoning etc.
        private float snowBlindnessLevel = 0f;
        private bool isWearingGoggles = false;

        // Current effect values (smoothed)
        private float currentTunnelRadius = 1f;
        private float currentBlur = 0f;
        private float currentSaturation = 1f;
        private float currentVignetteIntensity = 0f;
        private float currentWhiteout = 0f;
        private float currentChromaticAberration = 0f;

        // Targets
        private float targetTunnelRadius = 1f;
        private float targetBlur = 0f;
        private float targetSaturation = 1f;
        private float targetVignetteIntensity = 0f;

        // Hallucination
        private float hallucinationTimer = 0f;

        private void Update()
        {
            UpdateTargets();
            SmoothValues();
            ApplyEffects();
            UpdateHallucinations();
            UpdateSnowBlindness();
        }

        /// <summary>
        /// Set player health (0-1). Low health causes tunnel vision, desaturation.
        /// </summary>
        public void SetHealth(float health)
        {
            healthLevel = Mathf.Clamp01(health);
        }

        /// <summary>
        /// Set blood loss level (0-1). Causes red vignette, desaturation, tunnel vision.
        /// Scenario: deep wounds from knife/bayonet, open fractures.
        /// </summary>
        public void SetBloodLoss(float bloodLoss)
        {
            bloodLossLevel = Mathf.Clamp01(bloodLoss);
        }

        /// <summary>
        /// Set fatigue level (0-1). Causes blur and slight desaturation.
        /// Scenario: 2+ hour march, 48-hour sleep deprivation.
        /// </summary>
        public void SetFatigue(float fatigue)
        {
            fatigueLevel = Mathf.Clamp01(fatigue);
        }

        /// <summary>
        /// Set hypothermia level (0-1). Causes progressive desaturation (world goes grey).
        /// See HypothermiaEffects.cs for frost overlay. This handles color loss.
        /// </summary>
        public void SetHypothermia(float hypothermia)
        {
            hypothermiaLevel = Mathf.Clamp01(hypothermia);
        }

        /// <summary>
        /// Set dehydration level (0-1). Causes visual distortion, waviness.
        /// See DehydrationEffects.cs for full dehydration effects.
        /// </summary>
        public void SetDehydration(float dehydration)
        {
            dehydrationLevel = Mathf.Clamp01(dehydration);
        }

        /// <summary>
        /// Set mental health (0-1). Very low causes shadow hallucinations.
        /// Scenario: 2+ weeks survival causes depression, nervous breakdown.
        /// </summary>
        public void SetMentalHealth(float mental)
        {
            mentalHealthLevel = Mathf.Clamp01(mental);
        }

        /// <summary>
        /// Set poisoning level (0-1). Causes blur, chromatic aberration, color shift.
        /// Scenario: eating poisonous mushrooms in autumn, contaminated water (typhoid, dysentery).
        /// </summary>
        public void SetPoisoning(float poisoning)
        {
            poisoningLevel = Mathf.Clamp01(poisoning);
        }

        /// <summary>
        /// Set whether player is wearing protective goggles.
        /// Scenario part 4: ballistic glasses or sunglasses with dark lenses
        /// prevent snow blindness.
        /// </summary>
        public void SetWearingGoggles(bool wearing)
        {
            isWearingGoggles = wearing;
        }

        private void UpdateTargets()
        {
            // Tunnel vision - from blood loss, low health, dehydration
            float tunnelFromHealth = (1f - healthLevel) * 0.4f;
            float tunnelFromBlood = bloodLossLevel * 0.5f;
            float tunnelFromDehydration = dehydrationLevel * 0.3f;
            float tunnelFactor = Mathf.Max(tunnelFromHealth, Mathf.Max(tunnelFromBlood, tunnelFromDehydration));
            targetTunnelRadius = Mathf.Lerp(1f, tunnelVisionMaxRadius, tunnelFactor);

            // Blur - from fatigue, poisoning, dehydration
            float blurFromFatigue = fatigueLevel * 0.3f;
            float blurFromPoisoning = poisoningLevel * 0.6f;
            float blurFromDehydration = dehydrationLevel * 0.2f;
            targetBlur = (blurFromFatigue + blurFromPoisoning + blurFromDehydration) * maxBlurIntensity;

            // Desaturation - from hypothermia, blood loss, low health
            float desatFromHypothermia = hypothermiaLevel * 0.6f;
            float desatFromBlood = bloodLossLevel * 0.4f;
            float desatFromHealth = (1f - healthLevel) * 0.3f;
            float desatFactor = Mathf.Max(desatFromHypothermia, Mathf.Max(desatFromBlood, desatFromHealth));
            targetSaturation = 1f - desatFactor;

            // Blood vignette
            targetVignetteIntensity = bloodLossLevel * maxVignetteIntensity;

            // Chromatic aberration from poisoning
            currentChromaticAberration = poisoningLevel * 0.02f;
        }

        private void SmoothValues()
        {
            currentTunnelRadius = Mathf.Lerp(currentTunnelRadius, targetTunnelRadius,
                                              Time.deltaTime * tunnelVisionSmoothSpeed);
            currentBlur = Mathf.Lerp(currentBlur, targetBlur,
                                      Time.deltaTime * blurSmoothSpeed);
            currentSaturation = Mathf.Lerp(currentSaturation, targetSaturation,
                                            Time.deltaTime * desaturationSmoothSpeed);
            currentVignetteIntensity = Mathf.Lerp(currentVignetteIntensity, targetVignetteIntensity,
                                                    Time.deltaTime * 3f);
        }

        private void ApplyEffects()
        {
            if (visionEffectMaterial == null) return;

            visionEffectMaterial.SetFloat(TunnelRadiusProp, currentTunnelRadius);
            visionEffectMaterial.SetFloat(BlurIntensityProp, currentBlur);
            visionEffectMaterial.SetFloat(SaturationProp, currentSaturation);
            visionEffectMaterial.SetFloat(VignetteIntensityProp, currentVignetteIntensity);
            visionEffectMaterial.SetColor(VignetteColorProp, vignetteColor);
            visionEffectMaterial.SetFloat(WhiteoutProp, currentWhiteout);
            visionEffectMaterial.SetFloat(ChromaticAberrationProp, currentChromaticAberration);
        }

        private void UpdateHallucinations()
        {
            if (mentalHealthLevel > (1f - hallucinationThreshold)) return;

            hallucinationTimer += Time.deltaTime;
            if (hallucinationTimer >= shadowFlickerInterval)
            {
                hallucinationTimer = 0f;
                // Trigger brief shadow flicker at edge of vision
                StartCoroutine(ShadowFlicker());
            }
        }

        private System.Collections.IEnumerator ShadowFlicker()
        {
            // Quick darkening at a random edge of vision
            float duration = Random.Range(0.1f, 0.3f);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float intensity = Mathf.Sin(elapsed / duration * Mathf.PI) * 0.3f;
                // Temporarily increase vignette for shadow effect
                if (visionEffectMaterial != null)
                {
                    visionEffectMaterial.SetFloat(VignetteIntensityProp,
                        currentVignetteIntensity + intensity);
                }
                yield return null;
            }
        }

        private void UpdateSnowBlindness()
        {
            // Snow blindness builds when in bright snowy conditions without goggles
            // Scenario part 4: risk of "snow blindness" without goggles
            if (!isWearingGoggles && snowBlindnessLevel > 0f)
            {
                currentWhiteout = Mathf.Lerp(currentWhiteout, snowBlindnessLevel * maxWhiteoutIntensity,
                                              Time.deltaTime * 1f);
            }
            else
            {
                currentWhiteout = Mathf.Lerp(currentWhiteout, 0f, Time.deltaTime * 0.5f);
            }
        }

        /// <summary>
        /// Increase snow blindness from exposure.
        /// Called by environment system when in snowy bright conditions.
        /// </summary>
        public void AddSnowBlindnessExposure(float deltaTime)
        {
            if (!isWearingGoggles)
            {
                snowBlindnessLevel = Mathf.Clamp01(snowBlindnessLevel + snowBlindnessBuildRate * deltaTime);
            }
        }

        /// <summary>
        /// Slowly recover from snow blindness (when indoors or wearing goggles).
        /// </summary>
        public void RecoverSnowBlindness(float deltaTime)
        {
            snowBlindnessLevel = Mathf.Clamp01(snowBlindnessLevel - snowBlindnessBuildRate * 0.3f * deltaTime);
        }
    }
}
