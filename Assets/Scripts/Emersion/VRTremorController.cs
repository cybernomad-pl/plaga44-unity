// PLAGA '44 - VR Tremor Controller
// Hand tremor scaling with cold, hunger, injury, fear.
// Core physiology-as-controller: biological state drives VR controller behavior.
// Part of issue #31: Audio system and VR emersion effects

using UnityEngine;

namespace Plaga44.Emersion
{
    /// <summary>
    /// Controls hand tremor intensity on VR controllers based on physiological state.
    ///
    /// From scenario docs and IPK grant:
    /// - Tremor: Controller vibration and aim instability increase with hunger/dehydration/stress
    /// - Winter: frostbite causes uncontrollable shaking of hands
    /// - Hunger: low blood sugar causes fine tremor
    /// - Injury: wounded hand/arm causes gross tremor
    /// - Fear: adrenaline causes rapid fine tremor
    /// - Fatigue: 2+ hour march causes exhaustion tremor
    ///
    /// Meta Quest 3 haptic feedback is used for vibration.
    /// Controller position is offset by Perlin noise for visual tremor.
    /// </summary>
    public class VRTremorController : MonoBehaviour
    {
        [Header("Tremor Targets")]
        [SerializeField] private Transform leftHandTransform;
        [SerializeField] private Transform rightHandTransform;

        [Header("Tremor Configuration")]
        [SerializeField] private float maxTremorAmplitude = 0.02f;  // Meters of hand displacement
        [SerializeField] private float tremorFrequencyBase = 6f;     // Hz - normal tremor
        [SerializeField] private float tremorFrequencyStress = 12f;  // Hz - stress tremor
        [SerializeField] private float tremorSmoothSpeed = 2f;

        [Header("Haptic Feedback")]
        [SerializeField] private float hapticAmplitudeMax = 0.8f;
        [SerializeField] private float hapticFrequency = 0.1f;  // Duration per pulse

        [Header("Injury Lockout")]
        [SerializeField] private float lockoutTremorMultiplier = 3f;

        // Input factors (0-1)
        private float coldFactor = 0f;
        private float hungerFactor = 0f;
        private float injuryFactorLeft = 0f;
        private float injuryFactorRight = 0f;
        private float fearFactor = 0f;
        private float fatigueFactor = 0f;
        private float dehydrationFactor = 0f;

        private float currentTremorIntensity = 0f;
        private float targetTremorIntensity = 0f;
        private float tremorTime = 0f;

        // Original positions for offset calculation
        private Vector3 leftOriginalLocalPos;
        private Vector3 rightOriginalLocalPos;
        private bool positionsInitialized = false;

        private void Start()
        {
            if (leftHandTransform != null)
                leftOriginalLocalPos = leftHandTransform.localPosition;
            if (rightHandTransform != null)
                rightOriginalLocalPos = rightHandTransform.localPosition;
            positionsInitialized = true;
        }

        private void Update()
        {
            UpdateTargetIntensity();
            SmoothIntensity();
            ApplyTremor();
            ApplyHaptics();
        }

        /// <summary>
        /// Set cold/hypothermia factor (0-1).
        /// Scenario: winter frostbite, exposure to cold rain.
        /// Anti-frostbite cream helps reduce this.
        /// </summary>
        public void SetColdFactor(float cold)
        {
            coldFactor = Mathf.Clamp01(cold);
        }

        /// <summary>
        /// Set hunger/low blood sugar factor (0-1).
        /// Scenario: winter food scarcity, missed meals during march.
        /// Chocolate bars and cereal bars help reduce this.
        /// </summary>
        public void SetHungerFactor(float hunger)
        {
            hungerFactor = Mathf.Clamp01(hunger);
        }

        /// <summary>
        /// Set hand/arm injury factor per hand (0-1).
        /// Scenario: combat wounds, falls on limestone terrain,
        /// broken wrist from slipping on wet forest floor.
        /// IPK: Controller lockout simulates arm injury disabling controller.
        /// </summary>
        public void SetInjuryFactor(float leftInjury, float rightInjury)
        {
            injuryFactorLeft = Mathf.Clamp01(leftInjury);
            injuryFactorRight = Mathf.Clamp01(rightInjury);
        }

        /// <summary>
        /// Set fear/adrenaline factor (0-1).
        /// Scenario: encounter with military patrol, rabid animal,
        /// criminals in abandoned building.
        /// </summary>
        public void SetFearFactor(float fear)
        {
            fearFactor = Mathf.Clamp01(fear);
        }

        /// <summary>
        /// Set fatigue factor (0-1).
        /// Scenario: 2+ hour march without break, carrying 25kg backpack.
        /// </summary>
        public void SetFatigueFactor(float fatigue)
        {
            fatigueFactor = Mathf.Clamp01(fatigue);
        }

        /// <summary>
        /// Set dehydration factor (0-1).
        /// Scenario: summer heat without water every hour.
        /// Electrolytes and water reduce this.
        /// </summary>
        public void SetDehydrationFactor(float dehydration)
        {
            dehydrationFactor = Mathf.Clamp01(dehydration);
        }

        private void UpdateTargetIntensity()
        {
            // Weight different causes
            float coldTremor = coldFactor * 0.35f;      // Cold is major tremor source
            float hungerTremor = hungerFactor * 0.15f;   // Subtle glucose tremor
            float fearTremor = fearFactor * 0.25f;       // Adrenaline tremor
            float fatigueTremor = fatigueFactor * 0.15f;  // Exhaustion tremor
            float dehydTremor = dehydrationFactor * 0.1f; // Dehydration tremor

            targetTremorIntensity = coldTremor + hungerTremor + fearTremor + fatigueTremor + dehydTremor;
            targetTremorIntensity = Mathf.Clamp01(targetTremorIntensity);
        }

        private void SmoothIntensity()
        {
            currentTremorIntensity = Mathf.Lerp(currentTremorIntensity, targetTremorIntensity,
                                                 Time.deltaTime * tremorSmoothSpeed);
        }

        private void ApplyTremor()
        {
            if (!positionsInitialized || currentTremorIntensity < 0.01f) return;

            tremorTime += Time.deltaTime;

            // Frequency increases with stress (fast nervous tremor vs slow cold tremor)
            float freq = Mathf.Lerp(tremorFrequencyBase, tremorFrequencyStress, fearFactor);

            // Left hand tremor
            if (leftHandTransform != null)
            {
                float leftIntensity = currentTremorIntensity;
                // Injury amplifies tremor on affected hand
                leftIntensity += injuryFactorLeft * lockoutTremorMultiplier * currentTremorIntensity;
                leftIntensity = Mathf.Clamp01(leftIntensity);

                Vector3 leftOffset = CalculateTremorOffset(tremorTime, freq, leftIntensity, 0f);
                leftHandTransform.localPosition = leftOriginalLocalPos + leftOffset;
            }

            // Right hand tremor
            if (rightHandTransform != null)
            {
                float rightIntensity = currentTremorIntensity;
                rightIntensity += injuryFactorRight * lockoutTremorMultiplier * currentTremorIntensity;
                rightIntensity = Mathf.Clamp01(rightIntensity);

                Vector3 rightOffset = CalculateTremorOffset(tremorTime, freq, rightIntensity, 100f);
                rightHandTransform.localPosition = rightOriginalLocalPos + rightOffset;
            }
        }

        private Vector3 CalculateTremorOffset(float time, float frequency, float intensity, float seed)
        {
            float amplitude = maxTremorAmplitude * intensity;

            // Multi-layered Perlin noise for natural-looking tremor
            float x = (Mathf.PerlinNoise(time * frequency + seed, 0f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0f, time * frequency * 1.3f + seed) - 0.5f) * 2f;
            float z = (Mathf.PerlinNoise(time * frequency * 0.7f + seed, time * frequency * 0.5f) - 0.5f) * 2f;

            // Add high-frequency component for nervousness
            if (fearFactor > 0.5f)
            {
                float hfMultiplier = (fearFactor - 0.5f) * 2f * 0.3f;
                x += Mathf.Sin(time * frequency * 4f + seed) * hfMultiplier;
                y += Mathf.Sin(time * frequency * 5f + seed + 1f) * hfMultiplier;
            }

            return new Vector3(x, y, z) * amplitude;
        }

        private void ApplyHaptics()
        {
            if (currentTremorIntensity < 0.05f) return;

            // Haptic vibration intensity maps to tremor
            float hapticAmp = currentTremorIntensity * hapticAmplitudeMax;

            // Use OVRInput for Meta Quest 3 haptics
            // Left controller
            float leftAmp = hapticAmp * (1f + injuryFactorLeft);
            leftAmp = Mathf.Clamp01(leftAmp);
            OVRInput.SetControllerVibration(hapticFrequency, leftAmp, OVRInput.Controller.LTouch);

            // Right controller
            float rightAmp = hapticAmp * (1f + injuryFactorRight);
            rightAmp = Mathf.Clamp01(rightAmp);
            OVRInput.SetControllerVibration(hapticFrequency, rightAmp, OVRInput.Controller.RTouch);
        }

        /// <summary>
        /// Returns current tremor intensity (0-1) for use by other systems.
        /// </summary>
        public float GetTremorIntensity()
        {
            return currentTremorIntensity;
        }

        /// <summary>
        /// Returns true if either hand is effectively locked out by injury.
        /// IPK spec: "Controller lockout simulates arm injury disabling corresponding controller."
        /// </summary>
        public bool IsLeftHandLockedOut()
        {
            return injuryFactorLeft > 0.9f;
        }

        public bool IsRightHandLockedOut()
        {
            return injuryFactorRight > 0.9f;
        }

        private void OnDisable()
        {
            // Stop haptics when disabled
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
        }
    }
}
