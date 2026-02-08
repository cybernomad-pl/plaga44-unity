// PLAGA '44 - Breathing Controller
// Breathing sounds based on fatigue, exertion, cold weather.
// Cold weather produces visible breath effect (particle system).
// Part of issue #31: Audio system and VR emersion effects

using UnityEngine;

namespace Plaga44.Audio
{
    /// <summary>
    /// Controls breathing audio and visual effects based on player's physiological state.
    ///
    /// From scenario docs:
    /// - Heavy breathing during long marches (5-15km with 25kg backpack)
    /// - Visible breath in winter cold
    /// - Labored breathing when injured or exhausted
    /// - Panic breathing during high-stress encounters
    /// - Breathing difficulty at low health (chest injury)
    /// </summary>
    public class BreathingController : MonoBehaviour
    {
        [Header("Breathing Audio Clips")]
        [SerializeField] private AudioClip breathingCalm;        // At rest
        [SerializeField] private AudioClip breathingActive;      // Walking/light exertion
        [SerializeField] private AudioClip breathingHeavy;       // Running, heavy exertion
        [SerializeField] private AudioClip breathingPanic;       // Fear/stress
        [SerializeField] private AudioClip breathingLabored;     // Injured/very low stamina
        [SerializeField] private AudioClip breathingCold;        // Cold weather - sharp inhales
        [SerializeField] private AudioClip breathingWheezing;    // Illness/smoke inhalation

        [Header("Cold Breath Effect")]
        [SerializeField] private ParticleSystem coldBreathParticles;
        [SerializeField] private float coldBreathThreshold = 5f;  // Celsius below which breath is visible

        [Header("Configuration")]
        [SerializeField] private float breathRateSmoothSpeed = 1.5f;
        [SerializeField] [Range(0f, 1f)] private float baseVolume = 0.2f;
        [SerializeField] [Range(0f, 1f)] private float maxVolume = 0.8f;
        [SerializeField] private float breathCycleMin = 3f;  // Seconds per breath cycle (calm)
        [SerializeField] private float breathCycleMax = 0.8f; // Seconds per breath cycle (panicked)

        private AudioSource breathingSource;

        // Current state
        private float exertionLevel = 0f;
        private float stressLevel = 0f;
        private float healthLevel = 1f;
        private float ambientTemperature = 20f;
        private float staminaLevel = 1f;
        private bool isInjured = false;

        private float currentBreathRate;  // 0 = calm, 1 = maximum
        private float targetBreathRate;
        private float breathTimer;

        private void Awake()
        {
            breathingSource = gameObject.AddComponent<AudioSource>();
            breathingSource.loop = true;
            breathingSource.spatialBlend = 0f; // 2D - always present
            breathingSource.volume = baseVolume;
            breathingSource.playOnAwake = false;

            if (breathingCalm != null)
            {
                breathingSource.clip = breathingCalm;
                breathingSource.Play();
            }
        }

        private void Update()
        {
            UpdateTargetBreathRate();
            SmoothBreathRate();
            UpdateBreathingAudio();
            UpdateColdBreathEffect();
        }

        /// <summary>
        /// Set physical exertion level (0-1).
        /// Scenario: carrying backpack uphill, running from threats, chopping wood.
        /// </summary>
        public void SetExertionLevel(float exertion)
        {
            exertionLevel = Mathf.Clamp01(exertion);
        }

        /// <summary>
        /// Set stress/fear level (0-1). High stress causes panic breathing.
        /// Scenario: encountering military patrols, wild animals, criminals.
        /// </summary>
        public void SetStressLevel(float stress)
        {
            stressLevel = Mathf.Clamp01(stress);
        }

        /// <summary>
        /// Set current health (0-1). Low health causes labored breathing.
        /// Scenario: injuries from falls, combat, frostbite.
        /// </summary>
        public void SetHealth(float health)
        {
            healthLevel = Mathf.Clamp01(health);
        }

        /// <summary>
        /// Set ambient temperature in Celsius.
        /// Below threshold, cold breath particles appear.
        /// Scenario: winter survival, breath visible in cold.
        /// </summary>
        public void SetTemperature(float tempCelsius)
        {
            ambientTemperature = tempCelsius;
        }

        /// <summary>
        /// Set stamina level (0-1). Low stamina increases breathing rate.
        /// Scenario: 2+ hours march without break depletes stamina.
        /// </summary>
        public void SetStamina(float stamina)
        {
            staminaLevel = Mathf.Clamp01(stamina);
        }

        /// <summary>
        /// Set whether player is injured (chest/torso).
        /// Scenario: abdominal injury from sharp objects, broken ribs.
        /// </summary>
        public void SetInjured(bool injured)
        {
            isInjured = injured;
        }

        private void UpdateTargetBreathRate()
        {
            // Combine all factors
            float exertionRate = exertionLevel * 0.4f;
            float stressRate = stressLevel * 0.3f;
            float staminaPenalty = (1f - staminaLevel) * 0.2f;
            float healthPenalty = (1f - healthLevel) * 0.15f;

            // Cold increases breathing rate slightly
            float coldRate = 0f;
            if (ambientTemperature < 0f)
            {
                coldRate = Mathf.InverseLerp(0f, -20f, ambientTemperature) * 0.15f;
            }

            targetBreathRate = exertionRate + stressRate + staminaPenalty + healthPenalty + coldRate;

            // Injury dramatically increases breathing difficulty
            if (isInjured)
            {
                targetBreathRate = Mathf.Max(targetBreathRate, 0.6f);
            }

            targetBreathRate = Mathf.Clamp01(targetBreathRate);
        }

        private void SmoothBreathRate()
        {
            currentBreathRate = Mathf.Lerp(currentBreathRate, targetBreathRate,
                                            Time.deltaTime * breathRateSmoothSpeed);
        }

        private void UpdateBreathingAudio()
        {
            AudioClip targetClip;

            // Choose breathing clip based on primary condition
            if (isInjured && healthLevel < 0.3f)
            {
                targetClip = breathingLabored;
            }
            else if (stressLevel > 0.7f)
            {
                targetClip = breathingPanic;
            }
            else if (ambientTemperature < -5f && currentBreathRate > 0.3f)
            {
                targetClip = breathingCold;
            }
            else if (currentBreathRate > 0.6f)
            {
                targetClip = breathingHeavy;
            }
            else if (currentBreathRate > 0.2f)
            {
                targetClip = breathingActive;
            }
            else
            {
                targetClip = breathingCalm;
            }

            if (targetClip != null && breathingSource.clip != targetClip)
            {
                breathingSource.clip = targetClip;
                breathingSource.Play();
            }

            // Volume and pitch scale with breath rate
            breathingSource.volume = Mathf.Lerp(baseVolume, maxVolume, currentBreathRate);
            float breathCycle = Mathf.Lerp(breathCycleMin, breathCycleMax, currentBreathRate);
            breathingSource.pitch = breathCycleMin / Mathf.Max(breathCycle, 0.1f);
        }

        private void UpdateColdBreathEffect()
        {
            if (coldBreathParticles == null) return;

            bool shouldShowBreath = ambientTemperature < coldBreathThreshold;

            if (shouldShowBreath && !coldBreathParticles.isPlaying)
            {
                coldBreathParticles.Play();
            }
            else if (!shouldShowBreath && coldBreathParticles.isPlaying)
            {
                coldBreathParticles.Stop();
            }

            if (shouldShowBreath)
            {
                // Breath cloud size scales with breathing intensity
                var emission = coldBreathParticles.emission;
                float rate = Mathf.Lerp(2f, 12f, currentBreathRate);
                emission.rateOverTime = rate;

                // Colder = more visible breath
                var main = coldBreathParticles.main;
                float coldIntensity = Mathf.InverseLerp(coldBreathThreshold, -20f, ambientTemperature);
                Color breathColor = new Color(1f, 1f, 1f, Mathf.Lerp(0.1f, 0.5f, coldIntensity));
                main.startColor = breathColor;
            }
        }
    }
}
