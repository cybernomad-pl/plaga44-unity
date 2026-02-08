// PLAGA '44 - Heartbeat Controller
// Dynamic heartbeat audio responding to exertion, stress, injury, and fear.
// Core physiology-as-controller concept: biological state drives VR audio.
// Part of issue #31: Audio system and VR emersion effects

using UnityEngine;

namespace Plaga44.Audio
{
    /// <summary>
    /// Controls dynamic heartbeat audio that responds to the player's physiological state.
    /// Heartbeat rate and intensity increase with:
    /// - Physical exertion (carrying heavy backpack, running)
    /// - Stress/fear (NPC encounters, combat)
    /// - Injury and blood loss
    /// - Dehydration and hypothermia
    ///
    /// Based on scenario docs: carrying 25kg backpack causes increased heart rate,
    /// summer marches cause dehydration -> heart strain, winter hypothermia -> irregular heartbeat.
    /// </summary>
    public class HeartbeatController : MonoBehaviour
    {
        [Header("Heartbeat Audio Clips")]
        [SerializeField] private AudioClip heartbeatNormal;     // 60-80 BPM resting
        [SerializeField] private AudioClip heartbeatElevated;   // 80-120 BPM active
        [SerializeField] private AudioClip heartbeatRacing;     // 120-160 BPM combat/fear
        [SerializeField] private AudioClip heartbeatWeak;       // Weak/irregular - low health
        [SerializeField] private AudioClip heartbeatFlatline;   // Death

        [Header("Configuration")]
        [SerializeField] private float restingBPM = 70f;
        [SerializeField] private float maxBPM = 180f;
        [SerializeField] private float bpmSmoothSpeed = 2f;
        [SerializeField] [Range(0f, 1f)] private float baseVolume = 0.3f;
        [SerializeField] [Range(0f, 1f)] private float maxVolume = 0.9f;

        [Header("Thresholds")]
        [SerializeField] private float elevatedThreshold = 90f;
        [SerializeField] private float racingThreshold = 130f;
        [SerializeField] private float weakHealthThreshold = 0.2f;

        private AudioSource heartbeatSource;
        private float currentBPM;
        private float targetBPM;
        private float currentHealth = 1f;
        private float stressLevel = 0f;
        private float exertionLevel = 0f;
        private float temperatureStress = 0f;
        private float dehydrationLevel = 0f;

        private void Awake()
        {
            heartbeatSource = gameObject.AddComponent<AudioSource>();
            heartbeatSource.loop = true;
            heartbeatSource.spatialBlend = 0f; // 2D - always close
            heartbeatSource.volume = baseVolume;
            heartbeatSource.playOnAwake = false;
            currentBPM = restingBPM;
            targetBPM = restingBPM;
        }

        private void Update()
        {
            UpdateTargetBPM();
            SmoothBPM();
            UpdateHeartbeatAudio();
            UpdatePitch();
        }

        /// <summary>
        /// Set the player's current health (0-1). Low health causes weak/irregular heartbeat.
        /// At 0, plays flatline. Scenario: injuries from combat, falls, frostbite.
        /// </summary>
        public void SetHealth(float health)
        {
            currentHealth = Mathf.Clamp01(health);
        }

        /// <summary>
        /// Set stress/fear level (0-1). Increases heartbeat.
        /// Scenario: encountering military patrols, rabid animals, criminals.
        /// </summary>
        public void SetStressLevel(float stress)
        {
            stressLevel = Mathf.Clamp01(stress);
        }

        /// <summary>
        /// Set physical exertion level (0-1).
        /// Scenario: carrying 25kg backpack uphill, 5-15km marches.
        /// </summary>
        public void SetExertionLevel(float exertion)
        {
            exertionLevel = Mathf.Clamp01(exertion);
        }

        /// <summary>
        /// Set temperature-related stress (0-1).
        /// Scenario: hypothermia in winter increases heart strain,
        /// heatstroke in summer causes rapid heartbeat.
        /// </summary>
        public void SetTemperatureStress(float tempStress)
        {
            temperatureStress = Mathf.Clamp01(tempStress);
        }

        /// <summary>
        /// Set dehydration level (0-1).
        /// Scenario: summer marches without water every hour cause cardiac stress.
        /// </summary>
        public void SetDehydrationLevel(float dehydration)
        {
            dehydrationLevel = Mathf.Clamp01(dehydration);
        }

        private void UpdateTargetBPM()
        {
            // Combine all factors into target BPM
            float stressBPM = stressLevel * 60f;        // +0-60 BPM from stress
            float exertionBPM = exertionLevel * 50f;     // +0-50 BPM from exertion
            float tempBPM = temperatureStress * 30f;     // +0-30 BPM from temperature
            float dehydBPM = dehydrationLevel * 25f;     // +0-25 BPM from dehydration

            targetBPM = restingBPM + stressBPM + exertionBPM + tempBPM + dehydBPM;

            // Low health makes heart work harder (compensatory tachycardia)
            if (currentHealth < 0.5f)
            {
                float healthPenalty = (1f - currentHealth * 2f) * 40f;
                targetBPM += healthPenalty;
            }

            targetBPM = Mathf.Clamp(targetBPM, restingBPM * 0.5f, maxBPM);
        }

        private void SmoothBPM()
        {
            currentBPM = Mathf.Lerp(currentBPM, targetBPM, Time.deltaTime * bpmSmoothSpeed);
        }

        private void UpdateHeartbeatAudio()
        {
            AudioClip targetClip;

            // Dead - flatline
            if (currentHealth <= 0f)
            {
                targetClip = heartbeatFlatline;
            }
            // Very low health - weak irregular heartbeat
            else if (currentHealth < weakHealthThreshold)
            {
                targetClip = heartbeatWeak;
            }
            // Racing heartbeat (combat, extreme stress)
            else if (currentBPM >= racingThreshold)
            {
                targetClip = heartbeatRacing;
            }
            // Elevated heartbeat (marching, mild stress)
            else if (currentBPM >= elevatedThreshold)
            {
                targetClip = heartbeatElevated;
            }
            // Normal resting heartbeat
            else
            {
                targetClip = heartbeatNormal;
            }

            if (targetClip != null && heartbeatSource.clip != targetClip)
            {
                heartbeatSource.clip = targetClip;
                heartbeatSource.Play();
            }

            // Volume scales with BPM intensity - louder when heart is racing
            float bpmRatio = Mathf.InverseLerp(restingBPM, maxBPM, currentBPM);
            heartbeatSource.volume = Mathf.Lerp(baseVolume, maxVolume, bpmRatio);
        }

        private void UpdatePitch()
        {
            // Pitch maps to BPM: faster heartbeat = higher pitch playback
            float bpmRatio = currentBPM / restingBPM;
            heartbeatSource.pitch = Mathf.Clamp(bpmRatio, 0.5f, 2.5f);
        }

        /// <summary>
        /// Get the current BPM for use by other systems (e.g., HUD display).
        /// </summary>
        public float GetCurrentBPM()
        {
            return currentBPM;
        }

        /// <summary>
        /// Returns true if heart rate is dangerously high (risk of cardiac event).
        /// Scenario: summer heatstroke can cause heart attacks.
        /// </summary>
        public bool IsHeartRateDangerous()
        {
            return currentBPM > maxBPM * 0.9f;
        }
    }
}
