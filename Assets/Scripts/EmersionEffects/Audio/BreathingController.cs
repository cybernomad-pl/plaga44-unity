// PLAGA '44 - Emersion Effects System
// BreathingController.cs - Dynamic breathing sounds based on fatigue, injury, and temperature
// Includes cold weather visible breath particle effect

using System.Collections;
using UnityEngine;
using Plaga44.EmersionEffects.Core;

namespace Plaga44.EmersionEffects.Audio
{
    /// <summary>
    /// Controls breathing sounds that dynamically respond to stamina, exertion, injury,
    /// and temperature. Includes panting at low stamina, wheezing when injured,
    /// and cold weather visible breath particles.
    ///
    /// Attach to the player's camera/head transform.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class BreathingController : MonoBehaviour
    {
        [Header("References")]
        public EmersionEffectsManager EmersionManager;

        [Header("Breathing Clips")]
        [Tooltip("Normal breathing loop (calm).")]
        public AudioClip NormalBreathing;
        [Tooltip("Heavy/labored breathing loop.")]
        public AudioClip HeavyBreathing;
        [Tooltip("Panting breathing loop.")]
        public AudioClip PantingBreathing;
        [Tooltip("Cold air breathing (sharper inhales).")]
        public AudioClip ColdBreathing;
        [Tooltip("Wheezing sound for injured state.")]
        public AudioClip WheezingSound;
        [Tooltip("Short pain gasp one-shot clips.")]
        public AudioClip[] PainGasps;

        [Header("Breathing Rate")]
        public float RestingBreathRate = 14f;
        public float MaxBreathRate = 35f;

        [Header("Volume")]
        [Range(0f, 1f)] public float VolumeAtRest = 0.05f;
        [Range(0f, 1f)] public float VolumeAtMax = 0.7f;

        [Header("Fatigue Breathing")]
        [Tooltip("Stamina below this triggers audible breathing.")]
        public float FatigueThreshold = 40f;

        [Header("Panting")]
        [Tooltip("Stamina below this triggers panting.")]
        public float PantingStaminaThreshold = 20f;
        public float PantingVolumeMultiplier = 1.3f;
        public float PantingRateMultiplier = 1.5f;

        [Header("Cold Breath")]
        [Tooltip("Ambient temperature below this shows visible breath.")]
        public float ColdBreathTemperatureThreshold = 5f;
        [Tooltip("Particle system for visible breath in cold.")]
        public ParticleSystem BreathCloudParticles;
        [Range(0f, 1f)] public float BreathCloudAlpha = 0.6f;
        public float BreathCloudDuration = 1.5f;
        public float BreathCloudScale = 0.15f;

        [Header("Injured Breathing")]
        [Tooltip("Health below this triggers injured breathing sounds.")]
        public float InjuredHealthThreshold = 40f;
        [Range(0f, 1f)] public float WheezingChance = 0.3f;
        [Range(0f, 1f)] public float PainGaspChance = 0.15f;

        // Runtime state
        private AudioSource _breathSource;
        private AudioSource _wheezingSource;
        private float _currentBreathRate;
        private float _targetBreathRate;
        private float _currentVolume;
        private float _breathCycleTimer;
        private BreathingState _currentState = BreathingState.Normal;
        private bool _isExhaling;
        private float _lastGaspTime;

        private enum BreathingState
        {
            Normal,
            Heavy,
            Panting,
            Cold,
            Injured
        }

        private void Awake()
        {
            _breathSource = GetComponent<AudioSource>();
            _breathSource.playOnAwake = false;
            _breathSource.spatialBlend = 0f;
            _breathSource.loop = true;

            // Create secondary source for overlapping wheezing/gasps
            _wheezingSource = gameObject.AddComponent<AudioSource>();
            _wheezingSource.playOnAwake = false;
            _wheezingSource.spatialBlend = 0f;
            _wheezingSource.loop = false;

            _currentBreathRate = RestingBreathRate;
        }

        private void Start()
        {
            if (EmersionManager == null)
                EmersionManager = EmersionEffectsManager.Instance;

            if (EmersionManager == null)
            {
                Debug.LogError("[BreathingController] EmersionEffectsManager not found.");
                enabled = false;
                return;
            }

            // Start with normal breathing
            if (NormalBreathing != null)
            {
                _breathSource.clip = NormalBreathing;
                _breathSource.volume = VolumeAtRest;
                _breathSource.Play();
            }

            Debug.Log("[BreathingController] Breathing audio system initialized.");
        }

        private void Update()
        {
            if (EmersionManager == null || !EmersionManager.EffectsEnabled) return;

            var state = EmersionManager.PlayerState;

            // Determine breathing state
            BreathingState newState = DetermineBreathingState(state);
            if (newState != _currentState)
            {
                TransitionToState(newState);
                _currentState = newState;
            }

            // Calculate target breath rate
            CalculateTargetBreathRate(state);

            // Smooth approach to target
            _currentBreathRate = Mathf.Lerp(_currentBreathRate, _targetBreathRate, Time.deltaTime * 2f);

            // Update volume
            float targetVolume = CalculateBreathVolume(state);
            _currentVolume = Mathf.Lerp(_currentVolume, targetVolume, Time.deltaTime * 3f);
            _breathSource.volume = _currentVolume * EmersionManager.GlobalIntensityMultiplier;

            // Update pitch to match breath rate
            float rateRatio = _currentBreathRate / RestingBreathRate;
            _breathSource.pitch = Mathf.Clamp(rateRatio * 0.9f, 0.8f, 1.8f);

            // Breath cycle tracking for particle effects
            _breathCycleTimer += Time.deltaTime * _currentBreathRate / 60f;
            if (_breathCycleTimer >= 1f)
            {
                _breathCycleTimer -= 1f;
                OnBreathCycle(state);
            }

            // Random injured sounds
            if (state.Health < InjuredHealthThreshold)
            {
                TryPlayInjuredSounds(state);
            }
        }

        private BreathingState DetermineBreathingState(PlayerPhysiologyState state)
        {
            // Priority order: Injured > Panting > Cold > Heavy > Normal
            if (state.Health < InjuredHealthThreshold && state.Health > 0f)
                return BreathingState.Injured;

            if (state.Stamina < PantingStaminaThreshold)
                return BreathingState.Panting;

            if (state.AmbientTemperature < ColdBreathTemperatureThreshold)
                return BreathingState.Cold;

            if (state.Stamina < FatigueThreshold || state.Exertion > 50f)
                return BreathingState.Heavy;

            return BreathingState.Normal;
        }

        private void TransitionToState(BreathingState newState)
        {
            AudioClip targetClip = null;

            switch (newState)
            {
                case BreathingState.Normal:
                    targetClip = NormalBreathing;
                    break;
                case BreathingState.Heavy:
                    targetClip = HeavyBreathing ?? NormalBreathing;
                    break;
                case BreathingState.Panting:
                    targetClip = PantingBreathing ?? HeavyBreathing ?? NormalBreathing;
                    break;
                case BreathingState.Cold:
                    targetClip = ColdBreathing ?? NormalBreathing;
                    break;
                case BreathingState.Injured:
                    targetClip = HeavyBreathing ?? NormalBreathing;
                    break;
            }

            if (targetClip != null && _breathSource.clip != targetClip)
            {
                StartCoroutine(CrossfadeBreathClip(targetClip));
            }
        }

        private IEnumerator CrossfadeBreathClip(AudioClip newClip)
        {
            float originalVolume = _breathSource.volume;

            // Fade out
            float elapsed = 0f;
            float fadeDuration = 0.5f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                _breathSource.volume = Mathf.Lerp(originalVolume, 0f, elapsed / fadeDuration);
                yield return null;
            }

            _breathSource.clip = newClip;
            _breathSource.Play();

            // Fade in
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                _breathSource.volume = Mathf.Lerp(0f, originalVolume, elapsed / fadeDuration);
                yield return null;
            }
        }

        private void CalculateTargetBreathRate(PlayerPhysiologyState state)
        {
            float rate = RestingBreathRate;

            // Exertion increases breath rate
            rate += (state.Exertion / 100f) * (MaxBreathRate - RestingBreathRate) * 0.5f;

            // Low stamina increases breath rate
            float staminaDeficit = 1f - state.StaminaNormalized;
            rate += staminaDeficit * (MaxBreathRate - RestingBreathRate) * 0.4f;

            // Sprinting
            if (state.IsSprinting)
                rate += 8f;

            // Fear increases breathing
            rate += (state.Fear / 100f) * 6f;

            // Pain from injuries
            float injury = 1f - state.HealthNormalized;
            rate += injury * 5f;

            // Panting multiplier
            if (_currentState == BreathingState.Panting)
                rate *= PantingRateMultiplier;

            _targetBreathRate = Mathf.Clamp(rate, RestingBreathRate, MaxBreathRate);
        }

        private float CalculateBreathVolume(PlayerPhysiologyState state)
        {
            float volume = VolumeAtRest;

            // Increase with exertion
            float exertionFactor = state.Exertion / 100f;
            volume = Mathf.Lerp(VolumeAtRest, VolumeAtMax, exertionFactor);

            // Increase with low stamina
            float staminaFactor = 1f - state.StaminaNormalized;
            volume = Mathf.Max(volume, Mathf.Lerp(VolumeAtRest, VolumeAtMax * 0.8f, staminaFactor));

            // Panting is louder
            if (_currentState == BreathingState.Panting)
                volume *= PantingVolumeMultiplier;

            // Injured breathing is louder
            if (_currentState == BreathingState.Injured)
                volume = Mathf.Max(volume, VolumeAtMax * 0.6f);

            return Mathf.Clamp(volume, VolumeAtRest, VolumeAtMax);
        }

        /// <summary>
        /// Called once per breath cycle. Triggers visible breath particles in cold weather.
        /// </summary>
        private void OnBreathCycle(PlayerPhysiologyState state)
        {
            _isExhaling = !_isExhaling;

            // Spawn visible breath cloud in cold weather on exhale
            if (_isExhaling && state.AmbientTemperature < ColdBreathTemperatureThreshold)
            {
                SpawnBreathCloud(state);
            }
        }

        private void SpawnBreathCloud(PlayerPhysiologyState state)
        {
            if (BreathCloudParticles == null) return;

            // Scale cloud intensity with how cold it is
            float coldIntensity = Mathf.InverseLerp(ColdBreathTemperatureThreshold, -15f, state.AmbientTemperature);
            coldIntensity = Mathf.Clamp01(coldIntensity);

            var main = BreathCloudParticles.main;
            main.startLifetime = BreathCloudDuration * (1f + coldIntensity * 0.5f);
            main.startSize = BreathCloudScale * (1f + coldIntensity * 0.3f);

            var colorOverLifetime = BreathCloudParticles.colorOverLifetime;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(BreathCloudAlpha * (0.5f + coldIntensity * 0.5f), 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            BreathCloudParticles.Emit(1);
        }

        private void TryPlayInjuredSounds(PlayerPhysiologyState state)
        {
            if (Time.time - _lastGaspTime < 3f) return;

            float roll = Random.value;

            // Wheezing
            if (roll < WheezingChance && WheezingSound != null && !_wheezingSource.isPlaying)
            {
                float severity = 1f - (state.Health / InjuredHealthThreshold);
                _wheezingSource.PlayOneShot(WheezingSound, severity * 0.4f * EmersionManager.GlobalIntensityMultiplier);
                _lastGaspTime = Time.time;
            }
            // Pain gasp
            else if (roll < WheezingChance + PainGaspChance && PainGasps != null && PainGasps.Length > 0)
            {
                int idx = Random.Range(0, PainGasps.Length);
                if (PainGasps[idx] != null)
                {
                    float severity = 1f - (state.Health / InjuredHealthThreshold);
                    _wheezingSource.PlayOneShot(PainGasps[idx], severity * 0.5f * EmersionManager.GlobalIntensityMultiplier);
                    _lastGaspTime = Time.time;
                }
            }
        }

        /// <summary>
        /// Get the current breath phase (0..1) for synchronizing visual effects.
        /// 0 = start of inhale, 0.5 = start of exhale, 1 = cycle complete.
        /// </summary>
        public float GetBreathPhase()
        {
            return _breathCycleTimer;
        }

        /// <summary>
        /// Returns true during the exhale portion of the breath cycle.
        /// </summary>
        public bool IsExhaling => _isExhaling;
    }
}
