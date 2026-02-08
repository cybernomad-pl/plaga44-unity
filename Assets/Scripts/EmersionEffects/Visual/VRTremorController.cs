// PLAGA '44 - Emersion Effects System
// VRTremorController.cs - Hand tremor that increases with cold, hunger, injury, and fear
// Core physiology-as-controller mechanic: biological state directly affects VR hand tracking

using UnityEngine;
using Plaga44.EmersionEffects.Core;

namespace Plaga44.EmersionEffects.Visual
{
    /// <summary>
    /// Controls VR hand tremor that scales with the player's physiological state.
    /// Applies Perlin noise-based micro-movements to hand transforms, simulating
    /// tremor from cold, hunger, injury, fear, and fatigue.
    ///
    /// For Meta Quest 3/3S: applies tremor offset on top of tracked hand positions.
    /// Attach to a parent object of the hand controllers.
    /// </summary>
    public class VRTremorController : MonoBehaviour
    {
        [Header("References")]
        public EmersionEffectsManager EmersionManager;

        [Tooltip("Left hand controller transform.")]
        public Transform LeftHand;
        [Tooltip("Right hand controller transform.")]
        public Transform RightHand;

        [Header("Tremor Amplitude")]
        [Tooltip("Base tremor amplitude at minimal distress (meters).")]
        public float BaseAmplitude = 0.0005f;
        [Tooltip("Maximum tremor amplitude at full distress (meters).")]
        public float MaxAmplitude = 0.008f;

        [Header("Tremor Frequency")]
        [Tooltip("Base tremor frequency at minimal distress (Hz).")]
        public float BaseFrequency = 6f;
        [Tooltip("Maximum tremor frequency at full distress (Hz).")]
        public float MaxFrequency = 12f;

        [Header("Noise")]
        [Tooltip("Number of Perlin noise octaves for natural-looking tremor.")]
        public int NoiseOctaves = 3;

        [Header("Factor Multipliers")]
        public float ColdMultiplier = 1.5f;
        public float HungerMultiplier = 1.2f;
        public float InjuryMultiplier = 2.0f;
        public float FearMultiplier = 1.8f;
        public float FatigueMultiplier = 1.3f;

        [Header("Aiming & Breath Hold")]
        [Tooltip("Multiplier when aiming (slightly reduces tremor from focus).")]
        [Range(0f, 1f)] public float AimingMultiplier = 0.7f;
        [Tooltip("Reduction when holding breath for steady aim.")]
        [Range(0f, 1f)] public float HoldBreathReduction = 0.4f;
        [Tooltip("Max duration the player can hold breath (seconds).")]
        public float HoldBreathMaxDuration = 6f;

        [Header("Hand Dominance")]
        [Tooltip("Tremor reduction for the dominant hand (right by default).")]
        [Range(0f, 0.5f)] public float DominantHandReduction = 0.15f;
        [Tooltip("Tremor reduction when using two hands on weapon.")]
        [Range(0f, 1f)] public float TwoHandedReduction = 0.4f;
        public bool LeftHandDominant;

        [Header("Weapon Sway")]
        [Tooltip("Enable additional weapon sway on top of tremor.")]
        public bool WeaponSwayEnabled = true;
        public float BaseSwayAmplitude = 0.002f;
        public float MaxSwayAmplitude = 0.015f;
        public float SwayFrequency = 0.8f;
        public float FatigueSwayMultiplier = 2.0f;

        [Header("Recovery")]
        public float RecoverySpeed = 0.5f;

        // Runtime state
        private float _currentIntensity;
        private float _targetIntensity;
        private float _holdBreathTimer;
        private float _noiseOffsetLeft;
        private float _noiseOffsetRight;
        private Vector3 _leftHandBasePos;
        private Vector3 _rightHandBasePos;
        private bool _twoHandedGrip;

        /// <summary>
        /// Current tremor intensity [0..1], readable by other systems.
        /// </summary>
        public float CurrentIntensity => _currentIntensity;

        /// <summary>
        /// Set this when the player grips a weapon with both hands.
        /// </summary>
        public bool TwoHandedGrip
        {
            get => _twoHandedGrip;
            set => _twoHandedGrip = value;
        }

        private void Start()
        {
            if (EmersionManager == null)
                EmersionManager = EmersionEffectsManager.Instance;

            if (EmersionManager == null)
            {
                Debug.LogError("[VRTremorController] EmersionEffectsManager not found.");
                enabled = false;
                return;
            }

            // Randomize noise offsets so left and right hands don't move identically
            _noiseOffsetLeft = Random.Range(0f, 1000f);
            _noiseOffsetRight = Random.Range(1000f, 2000f);

            Debug.Log("[VRTremorController] Hand tremor system initialized for Meta Quest 3.");
        }

        private void LateUpdate()
        {
            if (EmersionManager == null || !EmersionManager.EffectsEnabled) return;

            var state = EmersionManager.PlayerState;

            // Calculate target tremor intensity
            _targetIntensity = CalculateTremorIntensity(state);

            // Smooth approach
            float approachSpeed = _targetIntensity > _currentIntensity ? 5f : RecoverySpeed;
            _currentIntensity = Mathf.MoveTowards(_currentIntensity, _targetIntensity, Time.deltaTime * approachSpeed);

            // Apply tremor scale from comfort settings
            float effectiveIntensity = _currentIntensity * EmersionManager.TremorScale * EmersionManager.GlobalIntensityMultiplier;

            if (effectiveIntensity < 0.001f) return;

            // Apply tremor to hands
            if (LeftHand != null)
            {
                float handMultiplier = LeftHandDominant ? (1f - DominantHandReduction) : 1f;
                Vector3 tremor = CalculateHandTremor(_noiseOffsetLeft, effectiveIntensity * handMultiplier);
                LeftHand.localPosition += tremor;
            }

            if (RightHand != null)
            {
                float handMultiplier = LeftHandDominant ? 1f : (1f - DominantHandReduction);
                Vector3 tremor = CalculateHandTremor(_noiseOffsetRight, effectiveIntensity * handMultiplier);
                RightHand.localPosition += tremor;
            }
        }

        private float CalculateTremorIntensity(PlayerPhysiologyState state)
        {
            // Individual factor contributions
            float coldFactor = state.HypothermiaSeverity * ColdMultiplier;
            float hungerFactor = Mathf.Clamp01(1f - state.Hunger / 100f) * HungerMultiplier;
            float injuryFactor = Mathf.Clamp01(1f - state.HealthNormalized) * InjuryMultiplier;
            float fearFactor = (state.Fear / 100f) * FearMultiplier;
            float fatigueFactor = Mathf.Clamp01(1f - state.StaminaNormalized) * FatigueMultiplier;

            // Blood loss amplifies tremor
            float bloodLossFactor = (state.BloodLoss / 100f) * 1.5f;

            // Composite: dominant factor + fraction of others (prevents absurd stacking)
            float[] factors = { coldFactor, hungerFactor, injuryFactor, fearFactor, fatigueFactor, bloodLossFactor };
            float dominant = 0f;
            float total = 0f;
            for (int i = 0; i < factors.Length; i++)
            {
                if (factors[i] > dominant) dominant = factors[i];
                total += factors[i];
            }
            float secondary = (total - dominant) * 0.15f;
            float intensity = Mathf.Clamp01(dominant + secondary);

            // Aiming reduces tremor slightly (compensatory focus)
            if (state.IsAiming)
                intensity *= AimingMultiplier;

            // Holding breath for steady aim
            if (state.IsHoldingBreath)
            {
                _holdBreathTimer += Time.deltaTime;
                float breathEffectiveness = 1f - Mathf.Clamp01(_holdBreathTimer / HoldBreathMaxDuration);
                intensity *= (1f - HoldBreathReduction * breathEffectiveness);
            }
            else
            {
                _holdBreathTimer = Mathf.Max(0f, _holdBreathTimer - Time.deltaTime * 2f);
            }

            // Two-handed grip stabilization
            if (_twoHandedGrip)
                intensity *= (1f - TwoHandedReduction);

            return Mathf.Clamp01(intensity);
        }

        /// <summary>
        /// Calculate 3D tremor displacement using multi-octave Perlin noise.
        /// This creates natural, organic-looking hand movement.
        /// </summary>
        private Vector3 CalculateHandTremor(float noiseOffset, float intensity)
        {
            float amplitude = Mathf.Lerp(BaseAmplitude, MaxAmplitude, intensity);
            float frequency = Mathf.Lerp(BaseFrequency, MaxFrequency, intensity);
            float time = Time.time * frequency;

            float x = 0f, y = 0f, z = 0f;
            float octaveAmplitude = 1f;
            float totalAmplitude = 0f;

            for (int o = 0; o < NoiseOctaves; o++)
            {
                float octaveFreq = Mathf.Pow(2f, o);
                x += (Mathf.PerlinNoise(time * octaveFreq + noiseOffset, 0f) * 2f - 1f) * octaveAmplitude;
                y += (Mathf.PerlinNoise(0f, time * octaveFreq + noiseOffset + 100f) * 2f - 1f) * octaveAmplitude;
                z += (Mathf.PerlinNoise(time * octaveFreq + noiseOffset + 200f, time * octaveFreq) * 2f - 1f) * octaveAmplitude;
                totalAmplitude += octaveAmplitude;
                octaveAmplitude *= 0.5f;
            }

            // Normalize octaves and apply amplitude
            float invTotal = 1f / totalAmplitude;
            Vector3 tremor = new Vector3(x * invTotal, y * invTotal, z * invTotal) * amplitude;

            // Add weapon sway if enabled
            if (WeaponSwayEnabled)
            {
                float swayAmp = Mathf.Lerp(BaseSwayAmplitude, MaxSwayAmplitude, intensity);
                float fatigueSway = Mathf.Lerp(1f, FatigueSwayMultiplier,
                    EmersionManager != null ? (1f - EmersionManager.PlayerState.StaminaNormalized) : 0f);
                swayAmp *= fatigueSway;

                float swayX = Mathf.Sin(Time.time * SwayFrequency) * swayAmp;
                float swayY = Mathf.Sin(Time.time * SwayFrequency * 0.7f + 1.3f) * swayAmp * 0.5f;
                tremor += new Vector3(swayX, swayY, 0f);
            }

            return tremor;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (LeftHand != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(LeftHand.position, MaxAmplitude * 10f);
            }
            if (RightHand != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(RightHand.position, MaxAmplitude * 10f);
            }
        }
#endif
    }
}
