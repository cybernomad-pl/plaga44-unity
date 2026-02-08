// PLAGA '44 - Dehydration Effects
// Visual distortion, dizziness, blackout threshold.
// Part of issue #31: Audio system and VR emersion effects

using UnityEngine;

namespace Plaga44.Emersion
{
    /// <summary>
    /// Controls dehydration-specific VR effects: visual waviness, dizziness, blackout.
    ///
    /// From scenario docs:
    /// - Summer heat: must drink 2 glasses water per hour during marches
    /// - Electrolytes every 2-3 hours, magnesium twice daily
    /// - Without water: heatstroke, fainting, brain hemorrhage, heart attack
    /// - Long march (2-12km) between 12:00-17:00 is most dangerous
    /// - Contaminated water (dead animals in streams) causes typhoid, dysentery, diarrhea
    ///   which accelerates dehydration
    ///
    /// Dehydration stages:
    /// 1. Mild: Slight visual waviness, thirst indicator
    /// 2. Moderate: Heat haze visual effect, dizziness, reduced stamina
    /// 3. Severe: Blackout flashes, tunnel vision, fainting risk
    /// 4. Critical: Loss of consciousness, death
    /// </summary>
    public class DehydrationEffects : MonoBehaviour
    {
        [Header("Visual Distortion Material")]
        [SerializeField] private Material dehydrationMaterial;

        [Header("Heat Haze")]
        [SerializeField] private float maxHazeDistortion = 0.03f;
        [SerializeField] private float hazeSpeed = 1f;

        [Header("Dizziness")]
        [SerializeField] private float maxDizzinessRotation = 3f;  // Degrees of sway
        [SerializeField] private float dizzinessSpeed = 0.5f;

        [Header("Blackout")]
        [SerializeField] private float blackoutThreshold = 0.8f;  // Dehydration level to start blackouts
        [SerializeField] private float blackoutMinInterval = 10f;
        [SerializeField] private float blackoutMaxInterval = 30f;
        [SerializeField] private float blackoutDuration = 2f;
        [SerializeField] private AnimationCurve blackoutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Camera")]
        [SerializeField] private Transform vrCamera;

        [Header("Audio")]
        [SerializeField] private AudioClip ringingEarsClip;
        [SerializeField] [Range(0f, 1f)] private float maxRingingVolume = 0.4f;

        // Shader property IDs
        private static readonly int HazeAmountProp = Shader.PropertyToID("_HazeAmount");
        private static readonly int HazeSpeedProp = Shader.PropertyToID("_HazeSpeed");
        private static readonly int BlackoutProp = Shader.PropertyToID("_BlackoutAmount");
        private static readonly int WavinessProp = Shader.PropertyToID("_Waviness");

        private float dehydrationLevel = 0f;
        private float ambientTemperature = 20f;
        private bool isExerting = false;  // Is player walking/running

        // Dizziness state
        private float dizzinessTime = 0f;
        private Quaternion cameraOriginalRotation;
        private bool cameraInitialized = false;

        // Blackout state
        private bool isBlackingOut = false;
        private float blackoutTimer = 0f;
        private float nextBlackoutTime;
        private float blackoutProgress = 0f;

        // Audio
        private AudioSource ringingSource;

        private void Awake()
        {
            ScheduleNextBlackout();

            var ringingGo = new GameObject("DehydrationRinging");
            ringingGo.transform.SetParent(transform);
            ringingSource = ringingGo.AddComponent<AudioSource>();
            ringingSource.loop = true;
            ringingSource.spatialBlend = 0f;
            ringingSource.playOnAwake = false;
            ringingSource.volume = 0f;
        }

        private void Start()
        {
            if (vrCamera != null)
            {
                cameraOriginalRotation = vrCamera.localRotation;
                cameraInitialized = true;
            }
        }

        private void Update()
        {
            UpdateHeatHaze();
            UpdateDizziness();
            UpdateBlackouts();
            UpdateAudio();
        }

        /// <summary>
        /// Set dehydration level (0-1).
        /// 0 = well hydrated, 1 = critically dehydrated.
        /// </summary>
        public void SetDehydrationLevel(float level)
        {
            dehydrationLevel = Mathf.Clamp01(level);
        }

        /// <summary>
        /// Set ambient temperature. Higher temps accelerate dehydration effects.
        /// Scenario: summer 12:00-17:00 most dangerous period.
        /// </summary>
        public void SetAmbientTemperature(float tempCelsius)
        {
            ambientTemperature = tempCelsius;
        }

        /// <summary>
        /// Set whether player is physically exerting (walking, running, carrying load).
        /// Exertion amplifies dehydration effects.
        /// </summary>
        public void SetExerting(bool exerting)
        {
            isExerting = exerting;
        }

        private void UpdateHeatHaze()
        {
            if (dehydrationMaterial == null) return;

            // Heat haze starts at moderate dehydration, worse in high temperatures
            float temperatureMultiplier = Mathf.InverseLerp(25f, 40f, ambientTemperature);
            float hazeAmount = 0f;

            if (dehydrationLevel > 0.3f)
            {
                hazeAmount = Mathf.InverseLerp(0.3f, 0.8f, dehydrationLevel) * maxHazeDistortion;
                hazeAmount *= (1f + temperatureMultiplier); // Double in extreme heat
            }

            dehydrationMaterial.SetFloat(HazeAmountProp, hazeAmount);
            dehydrationMaterial.SetFloat(HazeSpeedProp, hazeSpeed);

            // Waviness increases with dehydration
            float waviness = dehydrationLevel > 0.4f ?
                Mathf.InverseLerp(0.4f, 1f, dehydrationLevel) * 0.02f : 0f;
            dehydrationMaterial.SetFloat(WavinessProp, waviness);
        }

        private void UpdateDizziness()
        {
            if (!cameraInitialized || vrCamera == null) return;

            if (dehydrationLevel < 0.4f)
            {
                vrCamera.localRotation = cameraOriginalRotation;
                return;
            }

            dizzinessTime += Time.deltaTime;

            float dizzinessIntensity = Mathf.InverseLerp(0.4f, 0.9f, dehydrationLevel);
            if (isExerting)
            {
                dizzinessIntensity *= 1.5f; // Worse when moving
            }
            dizzinessIntensity = Mathf.Clamp01(dizzinessIntensity);

            float maxRot = maxDizzinessRotation * dizzinessIntensity;

            // Slow, irregular swaying
            float swayX = Mathf.Sin(dizzinessTime * dizzinessSpeed) * maxRot * 0.5f;
            float swayZ = Mathf.Sin(dizzinessTime * dizzinessSpeed * 0.7f + 1.5f) * maxRot;

            // Occasional stumble: sudden larger sway
            if (Random.value < 0.001f * dizzinessIntensity)
            {
                swayZ += Random.Range(-maxRot, maxRot) * 2f;
            }

            Quaternion sway = Quaternion.Euler(swayX, 0f, swayZ);
            vrCamera.localRotation = cameraOriginalRotation * sway;
        }

        private void UpdateBlackouts()
        {
            if (dehydrationLevel < blackoutThreshold)
            {
                // Reset blackout visual
                if (dehydrationMaterial != null)
                {
                    dehydrationMaterial.SetFloat(BlackoutProp, 0f);
                }
                return;
            }

            if (isBlackingOut)
            {
                blackoutProgress += Time.deltaTime / blackoutDuration;
                if (blackoutProgress >= 1f)
                {
                    blackoutProgress = 0f;
                    isBlackingOut = false;
                    ScheduleNextBlackout();
                }

                float blackoutValue = blackoutCurve.Evaluate(blackoutProgress);
                if (dehydrationMaterial != null)
                {
                    dehydrationMaterial.SetFloat(BlackoutProp, blackoutValue);
                }
            }
            else
            {
                blackoutTimer += Time.deltaTime;
                if (blackoutTimer >= nextBlackoutTime)
                {
                    isBlackingOut = true;
                    blackoutProgress = 0f;
                }

                if (dehydrationMaterial != null)
                {
                    dehydrationMaterial.SetFloat(BlackoutProp, 0f);
                }
            }
        }

        private void UpdateAudio()
        {
            if (ringingSource == null || ringingEarsClip == null) return;

            // Ringing ears at high dehydration
            float targetVolume = 0f;
            if (dehydrationLevel > 0.5f)
            {
                targetVolume = Mathf.InverseLerp(0.5f, 1f, dehydrationLevel) * maxRingingVolume;
            }

            ringingSource.volume = Mathf.Lerp(ringingSource.volume, targetVolume, Time.deltaTime * 2f);

            if (targetVolume > 0.01f && !ringingSource.isPlaying)
            {
                ringingSource.clip = ringingEarsClip;
                ringingSource.Play();
            }
            else if (targetVolume <= 0.01f && ringingSource.isPlaying)
            {
                ringingSource.Stop();
            }
        }

        private void ScheduleNextBlackout()
        {
            blackoutTimer = 0f;
            // More frequent at higher dehydration
            float severity = Mathf.InverseLerp(blackoutThreshold, 1f, dehydrationLevel);
            nextBlackoutTime = Mathf.Lerp(blackoutMaxInterval, blackoutMinInterval, severity);
        }

        /// <summary>
        /// Returns true if player just had a blackout (for fainting/stumble check).
        /// </summary>
        public bool IsBlackingOut()
        {
            return isBlackingOut;
        }

        /// <summary>
        /// Returns true if dehydration is life-threatening.
        /// Scenario: without water in summer heat, death from heatstroke/cardiac arrest.
        /// </summary>
        public bool IsLifeThreatening()
        {
            return dehydrationLevel > 0.9f;
        }
    }
}
