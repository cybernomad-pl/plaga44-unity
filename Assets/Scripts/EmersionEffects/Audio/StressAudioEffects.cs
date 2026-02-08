// PLAGA '44 - Emersion Effects System
// StressAudioEffects.cs - Tinnitus, muffled hearing, and auditory hallucinations
// Reflects deteriorating mental state through audio distortion and phantom sounds

using System.Collections;
using UnityEngine;
using Plaga44.EmersionEffects.Core;

namespace Plaga44.EmersionEffects.Audio
{
    /// <summary>
    /// Controls stress-related audio effects:
    /// - Tinnitus (high-pitched ringing) at high stress
    /// - Muffled hearing (low-pass filter) during stress or post-explosion
    /// - Auditory hallucinations at critically low mental health
    ///
    /// Requires AudioLowPassFilter on the AudioListener for muffled hearing.
    /// Attach to the player's camera/AudioListener GameObject.
    /// </summary>
    public class StressAudioEffects : MonoBehaviour
    {
        [Header("References")]
        public EmersionEffectsManager EmersionManager;
        [Tooltip("The main AudioListener (usually on the camera).")]
        public AudioListener MainAudioListener;

        [Header("Tinnitus")]
        [Tooltip("AudioSource for the tinnitus ringing sound.")]
        public AudioSource TinnitusSource;
        [Tooltip("Tinnitus tone clip (high-pitched sine wave loop).")]
        public AudioClip TinnitusClip;
        [Tooltip("Stress level (0-100) at which tinnitus starts.")]
        public float TinnitusOnsetStress = 60f;
        [Tooltip("Stress level at which tinnitus reaches max intensity.")]
        public float TinnitusMaxStress = 95f;
        [Range(0f, 1f)] public float TinnitusMaxVolume = 0.5f;
        public float TinnitusFadeDuration = 2f;
        [Tooltip("Slight pitch variation for realism.")]
        public float TinnitusPitchVariation = 0.1f;

        [Header("Muffled Hearing")]
        [Tooltip("AudioLowPassFilter on the AudioListener for muffled effect.")]
        public AudioLowPassFilter LowPassFilter;
        [Tooltip("Stress level at which muffling begins.")]
        public float MuffleOnsetStress = 50f;
        public float NormalCutoff = 22000f;
        public float MuffledCutoff = 800f;
        public float MuffleTransitionSpeed = 3f;
        [Tooltip("Duration of muffled hearing after an explosion.")]
        public float ExplosionMuffleDuration = 5f;
        [Tooltip("Cutoff frequency during explosion muffling.")]
        public float ExplosionCutoff = 400f;

        [Header("Auditory Hallucinations")]
        [Tooltip("Mental health (0-100) below which hallucinations can occur.")]
        public float HallucinationOnsetMentalHealth = 25f;
        public float HallucinationCriticalMentalHealth = 10f;
        [Tooltip("Time range between hallucination events at onset level.")]
        public float HallucinationMinInterval = 30f;
        public float HallucinationMaxInterval = 120f;
        [Tooltip("Time range between hallucination events at critical level.")]
        public float CriticalMinInterval = 10f;
        public float CriticalMaxInterval = 30f;
        [Range(0f, 1f)] public float HallucinationVolume = 0.35f;
        public int MaxSimultaneousHallucinations = 2;

        [Header("Hallucination Clips")]
        public AudioClip[] WhisperClips;
        public AudioClip[] PhantomFootstepClips;
        public AudioClip[] NameCallingClips;
        public AudioClip[] DistantScreamClips;
        public AudioClip[] RadioStaticClips;

        [Header("Hallucination Audio Sources")]
        [Tooltip("3D audio sources placed around the player for spatial hallucinations.")]
        public AudioSource[] HallucinationSources;

        // Runtime state
        private float _currentTinnitusVolume;
        private float _targetTinnitusVolume;
        private float _targetCutoff;
        private float _currentCutoff;
        private float _explosionMuffleTimer;
        private bool _isExplosionMuffled;
        private Coroutine _hallucinationCoroutine;
        private int _activeHallucinations;
        private float _nextHallucinationTime;

        private void Start()
        {
            if (EmersionManager == null)
                EmersionManager = EmersionEffectsManager.Instance;

            if (EmersionManager == null)
            {
                Debug.LogError("[StressAudioEffects] EmersionEffectsManager not found.");
                enabled = false;
                return;
            }

            // Initialize tinnitus source
            if (TinnitusSource == null)
            {
                TinnitusSource = gameObject.AddComponent<AudioSource>();
            }
            TinnitusSource.playOnAwake = false;
            TinnitusSource.spatialBlend = 0f;
            TinnitusSource.loop = true;
            TinnitusSource.volume = 0f;

            if (TinnitusClip != null)
            {
                TinnitusSource.clip = TinnitusClip;
                TinnitusSource.Play();
            }

            // Initialize low-pass filter
            if (LowPassFilter == null)
            {
                LowPassFilter = GetComponent<AudioLowPassFilter>();
                if (LowPassFilter == null && MainAudioListener != null)
                {
                    LowPassFilter = MainAudioListener.gameObject.GetComponent<AudioLowPassFilter>();
                    if (LowPassFilter == null)
                    {
                        LowPassFilter = MainAudioListener.gameObject.AddComponent<AudioLowPassFilter>();
                    }
                }
            }

            if (LowPassFilter != null)
            {
                LowPassFilter.cutoffFrequency = NormalCutoff;
            }

            _currentCutoff = NormalCutoff;
            _nextHallucinationTime = Time.time + Random.Range(HallucinationMinInterval, HallucinationMaxInterval);

            // Initialize hallucination sources if not assigned
            if (HallucinationSources == null || HallucinationSources.Length == 0)
            {
                CreateHallucinationSources();
            }

            Debug.Log("[StressAudioEffects] Stress audio effects initialized.");
        }

        private void Update()
        {
            if (EmersionManager == null || !EmersionManager.EffectsEnabled) return;

            var state = EmersionManager.PlayerState;

            UpdateTinnitus(state);
            UpdateMuffledHearing(state);
            UpdateHallucinations(state);
        }

        #region Tinnitus

        private void UpdateTinnitus(PlayerPhysiologyState state)
        {
            if (TinnitusSource == null) return;

            // Calculate target tinnitus volume based on stress
            if (state.Stress >= TinnitusOnsetStress)
            {
                float stressRange = TinnitusMaxStress - TinnitusOnsetStress;
                float stressAboveOnset = state.Stress - TinnitusOnsetStress;
                float t = Mathf.Clamp01(stressAboveOnset / stressRange);
                _targetTinnitusVolume = t * TinnitusMaxVolume;
            }
            else
            {
                _targetTinnitusVolume = 0f;
            }

            // Concussion adds tinnitus
            if (state.HasConcussion)
            {
                _targetTinnitusVolume = Mathf.Max(_targetTinnitusVolume, TinnitusMaxVolume * 0.7f);
            }

            // Smooth transition
            float speed = Time.deltaTime / TinnitusFadeDuration;
            _currentTinnitusVolume = Mathf.MoveTowards(_currentTinnitusVolume, _targetTinnitusVolume, speed);

            TinnitusSource.volume = _currentTinnitusVolume * EmersionManager.GlobalIntensityMultiplier;

            // Subtle pitch variation for organic feel
            if (_currentTinnitusVolume > 0.01f)
            {
                float pitchNoise = Mathf.PerlinNoise(Time.time * 0.5f, 0f) * 2f - 1f;
                TinnitusSource.pitch = 1f + pitchNoise * TinnitusPitchVariation;
            }
        }

        #endregion

        #region Muffled Hearing

        private void UpdateMuffledHearing(PlayerPhysiologyState state)
        {
            if (LowPassFilter == null) return;

            // Handle explosion muffling (highest priority)
            if (_isExplosionMuffled)
            {
                _explosionMuffleTimer -= Time.deltaTime;
                if (_explosionMuffleTimer <= 0f)
                {
                    _isExplosionMuffled = false;
                }
                else
                {
                    // Gradually recover from explosion muffling
                    float recoveryT = 1f - (_explosionMuffleTimer / ExplosionMuffleDuration);
                    _targetCutoff = Mathf.Lerp(ExplosionCutoff, NormalCutoff, recoveryT * recoveryT);
                }
            }
            else
            {
                // Stress-based muffling
                if (state.Stress >= MuffleOnsetStress)
                {
                    float stressRange = 100f - MuffleOnsetStress;
                    float stressAboveOnset = state.Stress - MuffleOnsetStress;
                    float t = Mathf.Clamp01(stressAboveOnset / stressRange);
                    _targetCutoff = Mathf.Lerp(NormalCutoff, MuffledCutoff, t);
                }
                else
                {
                    _targetCutoff = NormalCutoff;
                }
            }

            // Smooth transition
            _currentCutoff = Mathf.MoveTowards(_currentCutoff, _targetCutoff,
                Time.deltaTime * MuffleTransitionSpeed * 5000f);
            LowPassFilter.cutoffFrequency = _currentCutoff;
        }

        /// <summary>
        /// Trigger explosion-induced muffled hearing. Called by the EmersionEffectsManager
        /// or combat system when an explosion occurs nearby.
        /// </summary>
        public void TriggerExplosionMuffle(float intensity = 1f)
        {
            _isExplosionMuffled = true;
            _explosionMuffleTimer = ExplosionMuffleDuration * intensity;
            _currentCutoff = ExplosionCutoff;

            if (LowPassFilter != null)
            {
                LowPassFilter.cutoffFrequency = ExplosionCutoff;
            }

            Debug.Log($"[StressAudioEffects] Explosion muffling triggered. Duration: {_explosionMuffleTimer:F1}s");
        }

        #endregion

        #region Hallucinations

        private void UpdateHallucinations(PlayerPhysiologyState state)
        {
            if (EmersionManager.DisableHallucinations) return;
            if (state.MentalHealth > HallucinationOnsetMentalHealth) return;

            if (Time.time < _nextHallucinationTime) return;
            if (_activeHallucinations >= MaxSimultaneousHallucinations) return;

            // Play a hallucination
            StartCoroutine(PlayHallucination(state));

            // Schedule next hallucination
            float minInterval, maxInterval;
            if (state.MentalHealth <= HallucinationCriticalMentalHealth)
            {
                minInterval = CriticalMinInterval;
                maxInterval = CriticalMaxInterval;
            }
            else
            {
                // Interpolate between normal and critical intervals
                float t = Mathf.InverseLerp(HallucinationOnsetMentalHealth, HallucinationCriticalMentalHealth, state.MentalHealth);
                minInterval = Mathf.Lerp(HallucinationMinInterval, CriticalMinInterval, t);
                maxInterval = Mathf.Lerp(HallucinationMaxInterval, CriticalMaxInterval, t);
            }

            _nextHallucinationTime = Time.time + Random.Range(minInterval, maxInterval);
        }

        private IEnumerator PlayHallucination(PlayerPhysiologyState state)
        {
            _activeHallucinations++;

            // Pick a random hallucination type
            AudioClip clip = PickRandomHallucinationClip();
            if (clip == null)
            {
                _activeHallucinations--;
                yield break;
            }

            // Pick a random spatial source
            AudioSource source = GetAvailableHallucinationSource();
            if (source == null)
            {
                _activeHallucinations--;
                yield break;
            }

            // Randomize spatial position around the player
            RandomizeSourcePosition(source);

            float volume = HallucinationVolume * EmersionManager.GlobalIntensityMultiplier;

            // Mental severity amplifies volume slightly
            float mentalSeverity = 1f - (state.MentalHealth / HallucinationOnsetMentalHealth);
            volume *= (0.7f + mentalSeverity * 0.3f);

            source.clip = clip;
            source.volume = 0f;
            source.Play();

            // Fade in
            float fadeDuration = 0.5f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(0f, volume, elapsed / fadeDuration);
                yield return null;
            }

            // Wait for clip to play (minus fade out time)
            float waitTime = Mathf.Max(0f, clip.length - fadeDuration * 2f);
            yield return new WaitForSeconds(waitTime);

            // Fade out
            elapsed = 0f;
            float startVol = source.volume;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(startVol, 0f, elapsed / fadeDuration);
                yield return null;
            }

            source.Stop();
            _activeHallucinations--;
        }

        private AudioClip PickRandomHallucinationClip()
        {
            // Collect all available hallucination clip arrays
            AudioClip[][] allClips = new AudioClip[][]
            {
                WhisperClips,
                PhantomFootstepClips,
                NameCallingClips,
                DistantScreamClips,
                RadioStaticClips
            };

            // Filter out null/empty arrays
            int totalClips = 0;
            for (int i = 0; i < allClips.Length; i++)
            {
                if (allClips[i] != null)
                    totalClips += allClips[i].Length;
            }

            if (totalClips == 0) return null;

            int pick = Random.Range(0, totalClips);
            int running = 0;
            for (int i = 0; i < allClips.Length; i++)
            {
                if (allClips[i] == null) continue;
                if (pick < running + allClips[i].Length)
                {
                    return allClips[i][pick - running];
                }
                running += allClips[i].Length;
            }

            return null;
        }

        private AudioSource GetAvailableHallucinationSource()
        {
            if (HallucinationSources == null) return null;

            for (int i = 0; i < HallucinationSources.Length; i++)
            {
                if (HallucinationSources[i] != null && !HallucinationSources[i].isPlaying)
                    return HallucinationSources[i];
            }

            return null;
        }

        private void RandomizeSourcePosition(AudioSource source)
        {
            // Place the hallucination source at a random position around the player
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(2f, 8f);
            float height = Random.Range(-1f, 2f);

            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * distance,
                height,
                Mathf.Sin(angle) * distance
            );

            source.transform.localPosition = offset;
        }

        private void CreateHallucinationSources()
        {
            int count = MaxSimultaneousHallucinations + 1;
            HallucinationSources = new AudioSource[count];

            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject($"HallucinationSource_{i}");
                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;

                AudioSource src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f; // Full 3D for spatial hallucinations
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 1f;
                src.maxDistance = 15f;
                src.loop = false;

                HallucinationSources[i] = src;
            }
        }

        #endregion

        private void OnDisable()
        {
            // Reset audio filter to normal
            if (LowPassFilter != null)
            {
                LowPassFilter.cutoffFrequency = NormalCutoff;
            }

            if (TinnitusSource != null)
            {
                TinnitusSource.volume = 0f;
            }
        }
    }
}
