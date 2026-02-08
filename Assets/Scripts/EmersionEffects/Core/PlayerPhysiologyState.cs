// PLAGA '44 - Emersion Effects System
// PlayerPhysiologyState.cs - Central player physiology state that drives all emersion effects
// Part of the physiology-as-controller design: biological state -> VR sensory feedback

using System;
using UnityEngine;

namespace Plaga44.EmersionEffects.Core
{
    /// <summary>
    /// Central data model for the player's physiological state.
    /// All emersion effect controllers read from this shared state.
    /// Other game systems (combat, environment, survival) write to it.
    /// </summary>
    [Serializable]
    public class PlayerPhysiologyState
    {
        [Header("Vital Signs")]
        [Range(0f, 100f)] public float Health = 100f;
        [Range(0f, 100f)] public float Stamina = 100f;
        [Range(0f, 100f)] public float MentalHealth = 100f;

        [Header("Survival Needs")]
        [Range(0f, 100f)] public float Hydration = 80f;
        [Range(0f, 100f)] public float Hunger = 80f;

        [Header("Body State")]
        [Range(30f, 42f)] public float BodyTemperature = 36.6f;
        [Range(0f, 100f)] public float Fear = 0f;
        [Range(0f, 100f)] public float Stress = 10f;
        [Range(0f, 100f)] public float Exertion = 0f;
        [Range(0f, 100f)] public float BloodLoss = 0f;

        [Header("Status Flags")]
        public bool IsSprinting;
        public bool IsAiming;
        public bool IsHoldingBreath;
        public bool IsInCombat;
        public bool HasConcussion;
        public bool IsIndoors;
        public bool IsCrouching;

        [Header("Environment")]
        public float AmbientTemperature = 15f;
        public EnvironmentType CurrentEnvironment = EnvironmentType.Urban;
        public WeatherType CurrentWeather = WeatherType.Clear;
        public float WeatherIntensity = 0f;
        public bool IsNight;

        /// <summary>
        /// Normalized health [0..1] where 0 = dead, 1 = full health.
        /// </summary>
        public float HealthNormalized => Health / 100f;

        /// <summary>
        /// Normalized stamina [0..1].
        /// </summary>
        public float StaminaNormalized => Stamina / 100f;

        /// <summary>
        /// Normalized mental health [0..1].
        /// </summary>
        public float MentalHealthNormalized => MentalHealth / 100f;

        /// <summary>
        /// Normalized hydration [0..1].
        /// </summary>
        public float HydrationNormalized => Hydration / 100f;

        /// <summary>
        /// Overall "distress" factor combining multiple negative states.
        /// Used by effects that respond to general deterioration.
        /// </summary>
        public float OverallDistress
        {
            get
            {
                float healthDistress = 1f - HealthNormalized;
                float staminaDistress = 1f - StaminaNormalized;
                float mentalDistress = 1f - MentalHealthNormalized;
                float dehydration = 1f - HydrationNormalized;
                float starvation = 1f - (Hunger / 100f);
                float fearNorm = Fear / 100f;

                return Mathf.Clamp01(
                    healthDistress * 0.25f +
                    mentalDistress * 0.2f +
                    fearNorm * 0.2f +
                    dehydration * 0.15f +
                    starvation * 0.1f +
                    staminaDistress * 0.1f
                );
            }
        }

        /// <summary>
        /// Hypothermia severity [0..1] based on body temperature.
        /// 0 = normal (36.6C+), 1 = severe hypothermia (31C or below).
        /// </summary>
        public float HypothermiaSeverity
        {
            get
            {
                const float normalTemp = 36.6f;
                const float severeTemp = 31.0f;
                if (BodyTemperature >= normalTemp) return 0f;
                return Mathf.Clamp01((normalTemp - BodyTemperature) / (normalTemp - severeTemp));
            }
        }

        /// <summary>
        /// Returns the composite tremor intensity from all contributing factors.
        /// </summary>
        public float CompositeTremorFactor
        {
            get
            {
                float cold = HypothermiaSeverity * 1.5f;
                float hunger = Mathf.Clamp01(1f - Hunger / 100f) * 1.2f;
                float injury = Mathf.Clamp01(1f - HealthNormalized) * 2.0f;
                float fear = (Fear / 100f) * 1.8f;
                float fatigue = Mathf.Clamp01(1f - StaminaNormalized) * 1.3f;

                // Take the dominant factor, not sum, to avoid over-stacking
                float dominant = Mathf.Max(cold, Mathf.Max(hunger, Mathf.Max(injury, Mathf.Max(fear, fatigue))));
                // Add a fraction of secondary factors
                float total = (cold + hunger + injury + fear + fatigue);
                float secondary = (total - dominant) * 0.2f;

                return Mathf.Clamp01(dominant + secondary);
            }
        }

        /// <summary>
        /// Reset all values to healthy defaults.
        /// </summary>
        public void ResetToDefaults()
        {
            Health = 100f;
            Stamina = 100f;
            MentalHealth = 100f;
            Hydration = 80f;
            Hunger = 80f;
            BodyTemperature = 36.6f;
            Fear = 0f;
            Stress = 10f;
            Exertion = 0f;
            BloodLoss = 0f;
            IsSprinting = false;
            IsAiming = false;
            IsHoldingBreath = false;
            IsInCombat = false;
            HasConcussion = false;
            IsIndoors = false;
            IsCrouching = false;
            AmbientTemperature = 15f;
            CurrentEnvironment = EnvironmentType.Urban;
            CurrentWeather = WeatherType.Clear;
            WeatherIntensity = 0f;
            IsNight = false;
        }
    }

    public enum EnvironmentType
    {
        Forest,
        Urban,
        Underground
    }

    public enum WeatherType
    {
        Clear,
        Rain,
        HeavyRain,
        Wind,
        Storm,
        Snow
    }
}
