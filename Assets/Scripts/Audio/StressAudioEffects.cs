// PLAGA '44 - Stress Audio Effects
// Tinnitus, muffled hearing, auditory hallucinations at critical mental health.
// Part of issue #31: Audio system and VR emersion effects

using UnityEngine;

namespace Plaga44.Audio
{
    /// <summary>
    /// Manages stress-induced audio distortions that degrade the player's hearing.
    ///
    /// From scenario docs:
    /// - After 2+ weeks in survival conditions, depression and nervous breakdown occur
    /// - Long marches (2+ hours) cause psychological deterioration
    /// - Exposure to dead bodies, violence, isolation degrades mental state
    /// - Loss of moral/ethical inhibitions for survival
    ///
    /// Audio effects include:
    /// - Tinnitus (high-pitched ringing) after explosions or extreme stress
    /// - Muffled hearing (lowpass filter) during dissociation/shock
    /// - Auditory hallucinations (phantom voices, footsteps) at critical mental health
    /// - Heartbeat becomes dominant audio at extreme fear
    /// </summary>
    public class StressAudioEffects : MonoBehaviour
    {
        [Header("Tinnitus")]
        [SerializeField] private AudioClip tinnitusLoop;
        [SerializeField] [Range(0f, 1f)] private float maxTinnitusVolume = 0.6f;
        [SerializeField] private float tinnitusOnsetThreshold = 0.6f;

        [Header("Muffled Hearing")]
        [SerializeField] private float muffledCutoffMin = 500f;    // Heavily muffled
        [SerializeField] private float muffledCutoffMax = 22000f;  // Normal hearing
        [SerializeField] private float muffleSmoothSpeed = 3f;

        [Header("Auditory Hallucinations")]
        [SerializeField] private AudioClip[] hallucinationSounds;  // Whispers, footsteps, voices
        [SerializeField] private float hallucinationThreshold = 0.8f;
        [SerializeField] private float hallucinationMinInterval = 15f;
        [SerializeField] private float hallucinationMaxInterval = 60f;
        [SerializeField] [Range(0f, 1f)] private float hallucinationVolume = 0.35f;

        [Header("Shock Audio")]
        [SerializeField] private AudioClip shockOnset;  // Dull boom when entering shock
        [SerializeField] [Range(0f, 1f)] private float shockMuffleIntensity = 0.9f;

        private AudioSource tinnitusSource;
        private AudioSource hallucinationSource;
        private AudioSource shockSource;
        private AudioLowPassFilter globalLowPass;

        private float stressLevel = 0f;
        private float mentalHealth = 1f;    // 1 = healthy, 0 = broken
        private float traumaLevel = 0f;     // Accumulated trauma
        private bool isInShock = false;

        private float currentMuffleFactor = 0f;  // 0 = normal, 1 = fully muffled
        private float targetMuffleFactor = 0f;
        private float hallucinationTimer;
        private float nextHallucinationTime;

        private void Awake()
        {
            SetupAudioSources();
            ScheduleNextHallucination();
        }

        private void SetupAudioSources()
        {
            // Tinnitus
            tinnitusSource = CreateChildAudioSource("Tinnitus");
            tinnitusSource.loop = true;
            if (tinnitusLoop != null)
            {
                tinnitusSource.clip = tinnitusLoop;
            }

            // Hallucinations - spatial audio for 3D positioning
            hallucinationSource = CreateChildAudioSource("Hallucination");
            hallucinationSource.spatialBlend = 1f; // 3D
            hallucinationSource.minDistance = 2f;
            hallucinationSource.maxDistance = 15f;

            // Shock
            shockSource = CreateChildAudioSource("Shock");

            // Global lowpass filter on AudioListener
            globalLowPass = Camera.main?.gameObject.GetComponent<AudioLowPassFilter>();
            if (globalLowPass == null && Camera.main != null)
            {
                globalLowPass = Camera.main.gameObject.AddComponent<AudioLowPassFilter>();
                globalLowPass.cutoffFrequency = muffledCutoffMax;
            }
        }

        private AudioSource CreateChildAudioSource(string name)
        {
            var go = new GameObject($"StressAudio_{name}");
            go.transform.SetParent(transform);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        private void Update()
        {
            UpdateTinnitus();
            UpdateMuffledHearing();
            UpdateHallucinations();
        }

        /// <summary>
        /// Set current stress level (0-1).
        /// High stress from combat, NPC encounters, witnessing death.
        /// </summary>
        public void SetStressLevel(float stress)
        {
            stressLevel = Mathf.Clamp01(stress);
        }

        /// <summary>
        /// Set mental health (0-1). Degrades over time in survival conditions.
        /// Scenario: 2+ weeks causes depression, loss of inhibitions.
        /// </summary>
        public void SetMentalHealth(float mental)
        {
            mentalHealth = Mathf.Clamp01(mental);
        }

        /// <summary>
        /// Set accumulated trauma level (0-1).
        /// Increases from seeing corpses, violence, near-death experiences.
        /// </summary>
        public void SetTraumaLevel(float trauma)
        {
            traumaLevel = Mathf.Clamp01(trauma);
        }

        /// <summary>
        /// Trigger shock state (e.g., from severe injury, explosion).
        /// Causes sudden muffled hearing and tinnitus spike.
        /// </summary>
        public void TriggerShock()
        {
            if (isInShock) return;
            isInShock = true;

            if (shockOnset != null)
            {
                shockSource.PlayOneShot(shockOnset, 0.8f);
            }

            // Immediate heavy muffle
            targetMuffleFactor = shockMuffleIntensity;

            // Shock fades over 10 seconds
            Invoke(nameof(EndShock), 10f);
        }

        private void EndShock()
        {
            isInShock = false;
        }

        /// <summary>
        /// Trigger temporary tinnitus (e.g., from nearby explosion).
        /// </summary>
        public void TriggerExplosionTinnitus(float duration = 5f)
        {
            if (tinnitusSource == null || tinnitusLoop == null) return;

            tinnitusSource.volume = maxTinnitusVolume;
            if (!tinnitusSource.isPlaying)
            {
                tinnitusSource.Play();
            }

            // Fade out after duration
            StartCoroutine(FadeTinnitus(duration));
        }

        private void UpdateTinnitus()
        {
            if (tinnitusSource == null || tinnitusLoop == null) return;

            // Chronic tinnitus from sustained high stress
            float stressTinnitus = 0f;
            if (stressLevel > tinnitusOnsetThreshold)
            {
                stressTinnitus = Mathf.InverseLerp(tinnitusOnsetThreshold, 1f, stressLevel) * maxTinnitusVolume * 0.5f;
            }

            // Trauma-induced tinnitus
            float traumaTinnitus = traumaLevel * maxTinnitusVolume * 0.3f;

            float targetVolume = Mathf.Max(stressTinnitus, traumaTinnitus);

            // Don't override explosion tinnitus if it's louder
            if (tinnitusSource.volume < targetVolume || !tinnitusSource.isPlaying)
            {
                tinnitusSource.volume = Mathf.Lerp(tinnitusSource.volume, targetVolume, Time.deltaTime * 2f);

                if (targetVolume > 0.01f && !tinnitusSource.isPlaying)
                {
                    tinnitusSource.Play();
                }
                else if (targetVolume <= 0.01f && tinnitusSource.isPlaying)
                {
                    tinnitusSource.Stop();
                }
            }
        }

        private void UpdateMuffledHearing()
        {
            if (globalLowPass == null) return;

            // Calculate muffle target from various sources
            if (!isInShock)
            {
                float stressMuffle = 0f;
                if (stressLevel > 0.8f)
                {
                    stressMuffle = Mathf.InverseLerp(0.8f, 1f, stressLevel) * 0.5f;
                }

                float mentalMuffle = 0f;
                if (mentalHealth < 0.3f)
                {
                    mentalMuffle = Mathf.InverseLerp(0.3f, 0f, mentalHealth) * 0.6f;
                }

                targetMuffleFactor = Mathf.Max(stressMuffle, mentalMuffle);
            }

            currentMuffleFactor = Mathf.Lerp(currentMuffleFactor, targetMuffleFactor,
                                              Time.deltaTime * muffleSmoothSpeed);

            globalLowPass.cutoffFrequency = Mathf.Lerp(muffledCutoffMax, muffledCutoffMin, currentMuffleFactor);
        }

        private void UpdateHallucinations()
        {
            // Only trigger hallucinations at critically low mental health
            if (mentalHealth > (1f - hallucinationThreshold)) return;
            if (hallucinationSounds == null || hallucinationSounds.Length == 0) return;

            hallucinationTimer += Time.deltaTime;

            if (hallucinationTimer >= nextHallucinationTime)
            {
                PlayHallucination();
                ScheduleNextHallucination();
            }
        }

        private void PlayHallucination()
        {
            if (hallucinationSource == null) return;

            // Random clip
            int idx = Random.Range(0, hallucinationSounds.Length);
            AudioClip clip = hallucinationSounds[idx];
            if (clip == null) return;

            // Position hallucination sound randomly around player
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(3f, 10f);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            hallucinationSource.transform.position = transform.position + offset;

            // Volume scales with severity of mental health degradation
            float severity = Mathf.InverseLerp(1f - hallucinationThreshold, 0f, mentalHealth);
            hallucinationSource.volume = hallucinationVolume * severity;
            hallucinationSource.PlayOneShot(clip);
        }

        private void ScheduleNextHallucination()
        {
            hallucinationTimer = 0f;
            // More frequent at worse mental health
            float severity = Mathf.InverseLerp(1f - hallucinationThreshold, 0f, mentalHealth);
            nextHallucinationTime = Mathf.Lerp(hallucinationMaxInterval, hallucinationMinInterval, severity);
        }

        private System.Collections.IEnumerator FadeTinnitus(float duration)
        {
            yield return new WaitForSeconds(duration * 0.7f);

            float startVolume = tinnitusSource.volume;
            float fadeDuration = duration * 0.3f;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                tinnitusSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeDuration);
                yield return null;
            }

            tinnitusSource.volume = 0f;
            tinnitusSource.Stop();
        }
    }
}
