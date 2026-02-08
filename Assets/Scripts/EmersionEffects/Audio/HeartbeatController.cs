// PLAGA '44 - Emersion Effects System
// HeartbeatController.cs - Dynamic heartbeat audio that responds to exertion, stress, and injury
// The heartbeat becomes audible when BPM exceeds a threshold, creating visceral feedback

using UnityEngine;
using Plaga44.EmersionEffects.Core;

namespace Plaga44.EmersionEffects.Audio
{
    /// <summary>
    /// Controls heartbeat audio that dynamically responds to the player's physiological state.
    /// The heartbeat sound is inaudible at rest and becomes progressively louder and faster
    /// as stress, exertion, injury, and fear increase.
    ///
    /// Attach to the player's head/camera for stereo effect.
    /// Requires an AudioSource with a heartbeat AudioClip assigned.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class HeartbeatController : MonoBehaviour
    {
        [Header("References")]
        public EmersionEffectsManager EmersionManager;

        [Header("Heartbeat Audio")]
        [Tooltip("The heartbeat sound effect clip (single beat).")]
        public AudioClip HeartbeatClip;

        [Tooltip("Optional: heavier heartbeat for high BPM.")]
        public AudioClip HeartbeatHeavyClip;

        [Header("BPM Settings")]
        public int RestingBPM = 70;
        public int MaxBPM = 180;

        [Tooltip("BPM above which the heartbeat becomes audible.")]
        public int HearingThresholdBPM = 100;

        [Tooltip("BPM increase per unit of stress (0-100).")]
        public float BPMPerStressUnit = 1.2f;

        [Tooltip("BPM increase per unit of exertion (0-100).")]
        public float BPMPerExertionUnit = 0.8f;

        [Tooltip("Flat BPM increase when injured.")]
        public float BPMOnInjury = 30f;

        [Tooltip("BPM recovery rate per second when causes diminish.")]
        public float RecoveryRate = 2f;

        [Tooltip("Additional BPM when health is critically low.")]
        public float LowHealthBPMBoost = 20f;

        [Tooltip("Health percentage threshold for low health BPM boost.")]
        public float LowHealthThreshold = 30f;

        [Header("Volume")]
        [Range(0f, 1f)] public float VolumeAtRest = 0f;
        [Range(0f, 1f)] public float VolumeAtMax = 0.8f;

        [Header("Audio Processing")]
        [Range(0f, 1f)] public float StereoWidening = 0.3f;
        public bool BassBoostAtHighBPM = true;

        // Runtime state
        private AudioSource _audioSource;
        private float _currentBPM;
        private float _targetBPM;
        private float _timeSinceLastBeat;
        private float _currentBeatInterval;
        private bool _isHeavyBeat;

        /// <summary>
        /// Current beats per minute, readable by other systems.
        /// </summary>
        public float CurrentBPM => _currentBPM;

        /// <summary>
        /// Current beat interval in seconds.
        /// </summary>
        public float BeatInterval => _currentBeatInterval;

        /// <summary>
        /// Whether the heartbeat is currently audible to the player.
        /// </summary>
        public bool IsAudible => _currentBPM >= HearingThresholdBPM;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D - plays in the player's "chest"
            _audioSource.loop = false;

            _currentBPM = RestingBPM;
            _targetBPM = RestingBPM;
            _currentBeatInterval = 60f / RestingBPM;
        }

        private void Start()
        {
            if (EmersionManager == null)
                EmersionManager = EmersionEffectsManager.Instance;

            if (EmersionManager == null)
            {
                Debug.LogError("[HeartbeatController] EmersionEffectsManager not found.");
                enabled = false;
                return;
            }

            Debug.Log("[HeartbeatController] Heartbeat audio system initialized.");
        }

        private void Update()
        {
            if (EmersionManager == null || !EmersionManager.EffectsEnabled) return;

            var state = EmersionManager.PlayerState;

            // Calculate target BPM from physiological state
            CalculateTargetBPM(state);

            // Smoothly approach target BPM
            float delta = _targetBPM - _currentBPM;
            if (delta > 0)
            {
                // BPM increases quickly (sympathetic response)
                _currentBPM += Mathf.Min(delta, Time.deltaTime * 60f);
            }
            else
            {
                // BPM decreases slowly (parasympathetic recovery)
                _currentBPM += Mathf.Max(delta, -Time.deltaTime * RecoveryRate);
            }

            _currentBPM = Mathf.Clamp(_currentBPM, RestingBPM, MaxBPM);
            _currentBeatInterval = 60f / _currentBPM;

            // Schedule heartbeats
            _timeSinceLastBeat += Time.deltaTime;
            if (_timeSinceLastBeat >= _currentBeatInterval)
            {
                _timeSinceLastBeat -= _currentBeatInterval;
                PlayHeartbeat();
            }
        }

        private void CalculateTargetBPM(PlayerPhysiologyState state)
        {
            float bpm = RestingBPM;

            // Stress contribution
            bpm += state.Stress * BPMPerStressUnit;

            // Exertion contribution
            bpm += state.Exertion * BPMPerExertionUnit;

            // Fear spike
            bpm += state.Fear * 0.8f;

            // Injury response - elevated heart rate from pain/shock
            float injurySeverity = 1f - state.HealthNormalized;
            bpm += injurySeverity * BPMOnInjury;

            // Low health danger boost (tachycardia)
            if (state.Health < LowHealthThreshold)
            {
                float criticalFactor = 1f - (state.Health / LowHealthThreshold);
                bpm += LowHealthBPMBoost * criticalFactor;
            }

            // Blood loss increases heart rate (compensatory tachycardia)
            bpm += state.BloodLoss * 0.5f;

            // Combat awareness
            if (state.IsInCombat)
                bpm += 15f;

            // Sprinting
            if (state.IsSprinting)
                bpm += 20f;

            // Dehydration slightly elevates heart rate
            float dehydration = 1f - state.HydrationNormalized;
            bpm += dehydration * 10f;

            _targetBPM = Mathf.Clamp(bpm, RestingBPM, MaxBPM);
        }

        private void PlayHeartbeat()
        {
            if (HeartbeatClip == null) return;

            // Only play if above hearing threshold
            if (_currentBPM < HearingThresholdBPM) return;

            // Calculate volume based on BPM range above threshold
            float bpmRange = MaxBPM - HearingThresholdBPM;
            float bpmAboveThreshold = _currentBPM - HearingThresholdBPM;
            float volumeT = Mathf.Clamp01(bpmAboveThreshold / bpmRange);
            float volume = Mathf.Lerp(VolumeAtRest, VolumeAtMax, volumeT);

            volume *= EmersionManager.GlobalIntensityMultiplier;

            // Switch to heavy heartbeat clip at high BPM
            _isHeavyBeat = _currentBPM > (MaxBPM * 0.7f);
            AudioClip clip = (_isHeavyBeat && HeartbeatHeavyClip != null) ? HeartbeatHeavyClip : HeartbeatClip;

            // Pitch varies slightly with BPM for realism
            float pitchVariation = Mathf.Lerp(0.95f, 1.1f, volumeT);
            _audioSource.pitch = pitchVariation;

            // Pan slightly for stereo widening effect
            _audioSource.panStereo = StereoWidening * Mathf.Sin(Time.time * 0.5f);

            _audioSource.PlayOneShot(clip, volume);
        }

        /// <summary>
        /// Get the current normalized heartbeat phase (0..1) for synchronizing
        /// visual effects (e.g., blood overlay pulse) with the heartbeat rhythm.
        /// </summary>
        public float GetBeatPhase()
        {
            if (_currentBeatInterval <= 0f) return 0f;
            return _timeSinceLastBeat / _currentBeatInterval;
        }

        /// <summary>
        /// Returns a 0..1 pulse value that peaks at each heartbeat.
        /// Useful for visual effects that should pulse with the heart.
        /// </summary>
        public float GetBeatPulse()
        {
            float phase = GetBeatPhase();
            // Sharp peak at beat, quick falloff
            return Mathf.Exp(-phase * 8f);
        }
    }
}
