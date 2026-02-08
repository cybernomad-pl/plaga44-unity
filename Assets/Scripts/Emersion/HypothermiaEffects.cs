// PLAGA '44 - Hypothermia Effects
// Screen frost overlay, movement slowdown, shivering.
// Part of issue #31: Audio system and VR emersion effects

using UnityEngine;

namespace Plaga44.Emersion
{
    /// <summary>
    /// Controls hypothermia-specific VR effects: frost overlay, movement slow, shivering.
    ///
    /// From scenario docs:
    /// - Winter is the hardest season: hypothermia risk, frostbite of hands/feet
    /// - Sleeping outdoors without shelter risks freezing to death
    /// - Anti-frostbite cream, thermoactive clothing, gore-tex layers reduce risk
    /// - Chemical heat packs in boots and kidney area help
    /// - Hot tea from thermos prevents excessive cooling (11 PM - 3 AM most dangerous)
    ///
    /// Hypothermia stages:
    /// 1. Mild (35-32C): Shivering, cold hands, reduced dexterity
    /// 2. Moderate (32-28C): Violent shivering, confusion, drowsiness
    /// 3. Severe (<28C): Shivering stops, unconsciousness, death
    /// </summary>
    public class HypothermiaEffects : MonoBehaviour
    {
        [Header("Frost Overlay")]
        [SerializeField] private Material frostOverlayMaterial;
        [SerializeField] private float maxFrostOpacity = 0.7f;
        [SerializeField] private float frostGrowthSpeed = 0.5f;  // How fast frost appears

        [Header("Movement")]
        [SerializeField] private float maxMovementSlowdown = 0.5f;  // 50% slower at max hypothermia
        [SerializeField] private float movementSmoothSpeed = 1f;

        [Header("Shivering")]
        [SerializeField] private float mildShiverAmplitude = 0.002f;
        [SerializeField] private float severeShiverAmplitude = 0.008f;
        [SerializeField] private float shiverFrequency = 8f;  // Hz

        [Header("Audio")]
        [SerializeField] private AudioClip windChillLoop;
        [SerializeField] private AudioClip teethChatteringLoop;
        [SerializeField] [Range(0f, 1f)] private float maxWindChillVolume = 0.4f;

        [Header("Camera (VR Head)")]
        [SerializeField] private Transform vrCamera;

        // Shader property IDs
        private static readonly int FrostAmountProp = Shader.PropertyToID("_FrostAmount");
        private static readonly int FrostEdgeProp = Shader.PropertyToID("_FrostEdge");
        private static readonly int FrostColorProp = Shader.PropertyToID("_FrostColor");

        private float hypothermiaLevel = 0f;      // 0 = warm, 1 = severe hypothermia
        private float coreTemperature = 37f;       // Celsius, normal body temp
        private float currentFrostAmount = 0f;
        private float currentMovementMultiplier = 1f;

        private AudioSource windChillSource;
        private AudioSource teethSource;

        // Shivering
        private float shiverTime = 0f;
        private Vector3 cameraOriginalLocalPos;
        private bool cameraInitialized = false;

        private void Awake()
        {
            SetupAudio();
        }

        private void Start()
        {
            if (vrCamera != null)
            {
                cameraOriginalLocalPos = vrCamera.localPosition;
                cameraInitialized = true;
            }
        }

        private void SetupAudio()
        {
            // Wind chill ambient
            var windGo = new GameObject("HypothermiaWindChill");
            windGo.transform.SetParent(transform);
            windChillSource = windGo.AddComponent<AudioSource>();
            windChillSource.loop = true;
            windChillSource.spatialBlend = 0f;
            windChillSource.playOnAwake = false;
            windChillSource.volume = 0f;

            // Teeth chattering
            var teethGo = new GameObject("TeethChattering");
            teethGo.transform.SetParent(transform);
            teethSource = teethGo.AddComponent<AudioSource>();
            teethSource.loop = true;
            teethSource.spatialBlend = 0f;
            teethSource.playOnAwake = false;
            teethSource.volume = 0f;
        }

        private void Update()
        {
            UpdateFrostOverlay();
            UpdateMovementSlowdown();
            UpdateShivering();
            UpdateAudio();
        }

        /// <summary>
        /// Set hypothermia level (0-1).
        /// 0 = normal body temperature
        /// 0.3 = mild hypothermia (35C, shivering begins)
        /// 0.6 = moderate hypothermia (32C, violent shivering, confusion)
        /// 1.0 = severe hypothermia (28C, shivering stops, death imminent)
        /// </summary>
        public void SetHypothermiaLevel(float level)
        {
            hypothermiaLevel = Mathf.Clamp01(level);
            coreTemperature = Mathf.Lerp(37f, 26f, hypothermiaLevel);
        }

        /// <summary>
        /// Set core body temperature directly.
        /// </summary>
        public void SetCoreTemperature(float tempCelsius)
        {
            coreTemperature = Mathf.Clamp(tempCelsius, 25f, 38f);
            hypothermiaLevel = Mathf.InverseLerp(37f, 26f, coreTemperature);
        }

        private void UpdateFrostOverlay()
        {
            if (frostOverlayMaterial == null) return;

            // Frost grows from edges of screen inward as hypothermia worsens
            float targetFrost = hypothermiaLevel * maxFrostOpacity;
            currentFrostAmount = Mathf.Lerp(currentFrostAmount, targetFrost,
                                             Time.deltaTime * frostGrowthSpeed);

            frostOverlayMaterial.SetFloat(FrostAmountProp, currentFrostAmount);

            // Frost edge softness - sharper at severe hypothermia
            float edgeSoftness = Mathf.Lerp(0.3f, 0.05f, hypothermiaLevel);
            frostOverlayMaterial.SetFloat(FrostEdgeProp, edgeSoftness);

            // Ice crystal color - bluer at severe hypothermia
            Color frostColor = Color.Lerp(
                new Color(0.8f, 0.85f, 0.9f, 1f),  // Light frost
                new Color(0.6f, 0.7f, 0.9f, 1f),   // Deep cold blue
                hypothermiaLevel
            );
            frostOverlayMaterial.SetColor(FrostColorProp, frostColor);
        }

        private void UpdateMovementSlowdown()
        {
            // Movement slows progressively with hypothermia
            // Mild: 90% speed. Moderate: 70% speed. Severe: 50% speed.
            float targetMultiplier = 1f - (hypothermiaLevel * maxMovementSlowdown);
            currentMovementMultiplier = Mathf.Lerp(currentMovementMultiplier, targetMultiplier,
                                                    Time.deltaTime * movementSmoothSpeed);
        }

        private void UpdateShivering()
        {
            if (!cameraInitialized || vrCamera == null) return;

            // Shivering stages based on scenario:
            // Mild (0.2-0.5): Light shivering
            // Moderate (0.5-0.8): Violent shivering
            // Severe (0.8-1.0): Shivering STOPS (paradoxical - body gives up)

            float shiverIntensity;
            if (hypothermiaLevel < 0.2f)
            {
                shiverIntensity = 0f;
            }
            else if (hypothermiaLevel < 0.8f)
            {
                // Shivering increases from mild to moderate
                shiverIntensity = Mathf.InverseLerp(0.2f, 0.7f, hypothermiaLevel);
            }
            else
            {
                // Severe: shivering stops (body shutting down)
                shiverIntensity = Mathf.InverseLerp(1f, 0.8f, hypothermiaLevel) * 0.5f;
            }

            if (shiverIntensity > 0.01f)
            {
                shiverTime += Time.deltaTime;

                float amplitude = Mathf.Lerp(mildShiverAmplitude, severeShiverAmplitude, shiverIntensity);

                // Shivering is irregular - use multiple sine waves
                float shiverX = Mathf.Sin(shiverTime * shiverFrequency) * amplitude * 0.5f;
                float shiverY = Mathf.Sin(shiverTime * shiverFrequency * 1.7f + 0.5f) * amplitude;
                float shiverZ = Mathf.Sin(shiverTime * shiverFrequency * 0.8f + 1.2f) * amplitude * 0.3f;

                // Add random jitter for violent shivering
                if (shiverIntensity > 0.5f)
                {
                    float jitter = (shiverIntensity - 0.5f) * 2f;
                    shiverX += Random.Range(-1f, 1f) * amplitude * jitter * 0.5f;
                    shiverY += Random.Range(-1f, 1f) * amplitude * jitter * 0.5f;
                }

                vrCamera.localPosition = cameraOriginalLocalPos + new Vector3(shiverX, shiverY, shiverZ);
            }
            else
            {
                vrCamera.localPosition = cameraOriginalLocalPos;
            }
        }

        private void UpdateAudio()
        {
            // Wind chill audio
            if (windChillSource != null && windChillLoop != null)
            {
                float targetWindVolume = hypothermiaLevel * maxWindChillVolume;
                windChillSource.volume = Mathf.Lerp(windChillSource.volume, targetWindVolume, Time.deltaTime * 2f);

                if (targetWindVolume > 0.01f && !windChillSource.isPlaying)
                {
                    windChillSource.clip = windChillLoop;
                    windChillSource.Play();
                }
            }

            // Teeth chattering (mild to moderate hypothermia only)
            if (teethSource != null && teethChatteringLoop != null)
            {
                float teethVolume = 0f;
                if (hypothermiaLevel > 0.3f && hypothermiaLevel < 0.8f)
                {
                    teethVolume = Mathf.InverseLerp(0.3f, 0.6f, hypothermiaLevel) * 0.4f;
                }

                teethSource.volume = Mathf.Lerp(teethSource.volume, teethVolume, Time.deltaTime * 2f);

                if (teethVolume > 0.01f && !teethSource.isPlaying)
                {
                    teethSource.clip = teethChatteringLoop;
                    teethSource.Play();
                }
                else if (teethVolume <= 0.01f && teethSource.isPlaying)
                {
                    teethSource.Stop();
                }
            }
        }

        /// <summary>
        /// Get current movement speed multiplier for locomotion system.
        /// </summary>
        public float GetMovementMultiplier()
        {
            return currentMovementMultiplier;
        }

        /// <summary>
        /// Get core body temperature for HUD/medical display.
        /// </summary>
        public float GetCoreTemperature()
        {
            return coreTemperature;
        }

        /// <summary>
        /// Returns true if hypothermia is life-threatening.
        /// </summary>
        public bool IsLifeThreatening()
        {
            return hypothermiaLevel > 0.8f;
        }
    }
}
