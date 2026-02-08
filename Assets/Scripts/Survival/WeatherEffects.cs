// PLAGA '44 VR - WeatherEffects
// Applies weather and seasonal conditions to the PhysiologyController.
// Bridge between SeasonManager environmental state and player physiology.
//
// From scenario docs:
// Winter: "zagrozenie hipotermia", "zagrozenie odmrozeniami nog i rak"
// Summer: "udar mozgu, wylewy krwi do mozgu i zawalow serca oraz omdlen"
//         "Wywolanych nieustannym przebywaniem na sloncu dlug marsz 2-12km 12:00-17:00"
// Autumn: "sliska sciolka lesna", "deszcze powoduja obnizenie temperatury"
//         "nasiakniecie woda z deszczu" on backpacks increases weight
// Spring: "deszcze", "sliska sciolka", improved food availability
//
// Architecture: Subscribes to SeasonManager.OnEnvironmentUpdated.
// Modifies PhysiologyState through PhysiologyController API calls.

using System;
using UnityEngine;

namespace Plaga44.Survival
{
    using Plaga44.Physiology;

    /// <summary>
    /// Translates environmental conditions from SeasonManager into
    /// physiological effects on the player. Handles hypothermia acceleration,
    /// heatstroke risk, wet clothing effects, frostbite, UV exposure, and
    /// mosquito-related hazards.
    /// </summary>
    public class WeatherEffects : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SeasonManager seasonManager;
        [SerializeField] private PhysiologyController physiologyController;

        [Header("Configuration")]
        [SerializeField] private WeatherEffectsConfig config;

        [Header("Current Effects (Read Only)")]
        [SerializeField] private float hypothermiaRisk = 0f;
        [SerializeField] private float heatstrokeRisk = 0f;
        [SerializeField] private float frostbiteRisk = 0f;
        [SerializeField] private float uvExposureRate = 0f;
        [SerializeField] private float mosquitoExposure = 0f;
        [SerializeField] private float slipRisk = 0f;
        [SerializeField] private float visibilityFactor = 1f;

        // Accumulated exposure tracking
        private float frostbiteAccumulation = 0f;
        private float uvAccumulation = 0f;
        private float wetExposureDuration = 0f;

        // Events
        public event Action<string> OnWeatherHazardWarning;
        public event Action<float> OnSlipRiskChanged;
        public event Action<float> OnVisibilityChanged;

        // Public accessors
        public float HypothermiaRisk => hypothermiaRisk;
        public float HeatstrokeRisk => heatstrokeRisk;
        public float FrostbiteRisk => frostbiteRisk;
        public float SlipRisk => slipRisk;
        public float VisibilityFactor => visibilityFactor;
        public float MosquitoExposure => mosquitoExposure;

        private void OnEnable()
        {
            if (seasonManager != null)
            {
                seasonManager.OnEnvironmentUpdated += HandleEnvironmentUpdate;
            }
        }

        private void OnDisable()
        {
            if (seasonManager != null)
            {
                seasonManager.OnEnvironmentUpdated -= HandleEnvironmentUpdate;
            }
        }

        private void HandleEnvironmentUpdate(EnvironmentSnapshot env)
        {
            float dt = Time.deltaTime;
            float gameHoursPerSecond = 0.01f; // sync with PhysiologyConfig
            float dtGameHours = dt * gameHoursPerSecond;

            UpdateHypothermiaRisk(env, dtGameHours);
            UpdateHeatstrokeRisk(env, dtGameHours);
            UpdateFrostbiteRisk(env, dtGameHours);
            UpdateUVExposure(env, dtGameHours);
            UpdateMosquitoExposure(env);
            UpdateSlipRisk(env);
            UpdateVisibility(env);
            ApplySeasonalWoundRisks(env, dtGameHours);
        }

        // ===== HYPOTHERMIA =====
        // From scenario (part 2): "Zima jest najtrudniejsza pora roku do przezycia"
        // - "zagrozenie hipotermia"
        // - "zagrozenie odmrozeniami nog i rak"
        // - Wet clothing dramatically increases risk
        // From scenario (part 4): "wyziebienie organizmu" from long marches in rain

        private void UpdateHypothermiaRisk(EnvironmentSnapshot env, float dtGameHours)
        {
            float tempThreshold = config != null ? config.hypothermiaTemperatureThreshold : 10f;

            if (env.temperature < tempThreshold)
            {
                float severity = Mathf.InverseLerp(tempThreshold, -20f, env.temperature);
                float windChillFactor = 1f + env.windSpeed * 0.1f;
                float precipFactor = 1f + env.precipitation * 2f; // Wet = much worse

                hypothermiaRisk = Mathf.Clamp01(severity * windChillFactor * precipFactor);

                // Track wet exposure duration for progressive effects
                if (env.precipitation > 0.1f)
                {
                    wetExposureDuration += dtGameHours;
                }
                else
                {
                    wetExposureDuration = Mathf.Max(0f, wetExposureDuration - dtGameHours * 0.5f);
                }

                // Warn at dangerous levels
                if (hypothermiaRisk > 0.7f)
                {
                    OnWeatherHazardWarning?.Invoke("HYPOTHERMIA_DANGER");
                }
            }
            else
            {
                hypothermiaRisk = 0f;
                wetExposureDuration = Mathf.Max(0f, wetExposureDuration - dtGameHours);
            }
        }

        // ===== HEATSTROKE =====
        // From scenario (part 4): "udar mozgu, wylewy krwi do mozgu i zawalow serca"
        // "Wywolanych nieustannym przebywaniem na sloncu dlug marsz od 2km do 12km
        //  w godzinach od 12:00 do 17:00"
        // "Duzy ciezar 25kg w warunkach letnich powoduje szybsze spalanie energii"

        private void UpdateHeatstrokeRisk(EnvironmentSnapshot env, float dtGameHours)
        {
            float tempThreshold = config != null ? config.heatstrokeTemperatureThreshold : 30f;

            if (env.temperature > tempThreshold && env.season == Season.Summer)
            {
                float severity = Mathf.InverseLerp(tempThreshold, 45f, env.temperature);
                float sunFactor = env.isDaytime && env.cloudCover < 0.5f ? 1.5f : 0.5f;

                // From scenario: peak risk 12:00-17:00
                float timeRisk = 0f;
                if (env.timeOfDay >= 12f && env.timeOfDay <= 17f)
                {
                    timeRisk = 0.5f;
                }

                heatstrokeRisk = Mathf.Clamp01(severity * sunFactor + timeRisk);

                if (heatstrokeRisk > 0.6f)
                {
                    OnWeatherHazardWarning?.Invoke("HEATSTROKE_DANGER");
                }
            }
            else
            {
                heatstrokeRisk = Mathf.Max(0f, heatstrokeRisk - dtGameHours * 0.5f);
            }
        }

        // ===== FROSTBITE =====
        // From scenario (part 2/4):
        // - "zagrozenie odmrozeniami nog i rak"
        // - "ryzko odmrozen nog oraz rak i ryzko zamarzniecia"
        // - "przemoczenie nog (ryzko odmorzen)"
        // - Night application of anti-frostbite cream recommended

        private void UpdateFrostbiteRisk(EnvironmentSnapshot env, float dtGameHours)
        {
            float frostbiteThreshold = config != null ? config.frostbiteTemperatureThreshold : 0f;

            if (env.temperature < frostbiteThreshold)
            {
                float severity = Mathf.InverseLerp(frostbiteThreshold, -25f, env.temperature);
                float windFactor = 1f + env.windSpeed * 0.15f;
                float wetFactor = env.precipitation > 0f ? 1.5f : 1f;

                // Frostbite accumulates over time
                float accumulationRate = config != null ? config.frostbiteAccumulationRate : 0.05f;
                frostbiteAccumulation += severity * windFactor * wetFactor * accumulationRate * dtGameHours;
                frostbiteRisk = Mathf.Clamp01(frostbiteAccumulation);

                // At high accumulation, apply actual frostbite wounds
                if (frostbiteAccumulation > 0.8f && physiologyController != null)
                {
                    // From scenario: feet and hands are most vulnerable
                    if (UnityEngine.Random.value < 0.01f * dtGameHours)
                    {
                        physiologyController.ApplyWound(
                            WoundType.Frostbite,
                            UnityEngine.Random.value > 0.5f ? WoundLocation.LeftFoot : WoundLocation.RightFoot,
                            frostbiteAccumulation * 0.5f
                        );
                        OnWeatherHazardWarning?.Invoke("FROSTBITE_INJURY");
                    }
                }
            }
            else
            {
                // Frostbite risk decreases when warm
                float recoveryRate = config != null ? config.frostbiteRecoveryRate : 0.02f;
                frostbiteAccumulation = Mathf.Max(0f, frostbiteAccumulation - recoveryRate * dtGameHours);
                frostbiteRisk = Mathf.Clamp01(frostbiteAccumulation);
            }
        }

        // ===== UV EXPOSURE =====
        // From scenario (part 1/4):
        // - "kremy przeciwsloneczne (na lato)"
        // - "Nalezy stosowac kremy z filtrami przeciw sloneczymi"
        // - "Nalezy stosowac okulary przeciw sloneczne"
        // - Winter: "ryzko wystapienie slepoty snieznej na wskutek braku gogli"

        private void UpdateUVExposure(EnvironmentSnapshot env, float dtGameHours)
        {
            if (!env.isDaytime || env.cloudCover > 0.8f)
            {
                uvExposureRate = 0f;
                uvAccumulation = Mathf.Max(0f, uvAccumulation - dtGameHours * 0.1f);
                return;
            }

            float baseUV = 0f;
            switch (env.season)
            {
                case Season.Summer:
                    baseUV = 0.8f; // High UV in summer
                    break;
                case Season.Spring:
                    baseUV = 0.5f;
                    break;
                case Season.Autumn:
                    baseUV = 0.3f;
                    break;
                case Season.Winter:
                    // From scenario: snow blindness risk
                    baseUV = 0.2f + (env.weather == WeatherCondition.Snow ? 0.4f : 0f); // Snow reflection
                    break;
            }

            float cloudReduction = 1f - env.cloudCover * 0.6f;
            uvExposureRate = baseUV * cloudReduction;

            float accRate = config != null ? config.uvAccumulationRate : 0.02f;
            uvAccumulation += uvExposureRate * accRate * dtGameHours;

            // Apply UV effects to physiology state if accessible
            if (uvAccumulation > 0.5f)
            {
                OnWeatherHazardWarning?.Invoke("UV_EXPOSURE_HIGH");
            }
        }

        // ===== MOSQUITO EXPOSURE =====
        // From scenario (part 1/3):
        // - "spray na komary (na lato)"
        // - "spanie latem w lesie moga utrudnic komary"
        // - "unikac terenow podmokych, blota, bagien"
        // - "Komary odstrasza rowniez dym z ogniska"

        private void UpdateMosquitoExposure(EnvironmentSnapshot env)
        {
            if (env.season == Season.Summer || (env.season == Season.Spring && env.temperature > 15f))
            {
                float baseExposure = 0f;

                // Mosquitoes active in evening/night during warm months
                if (env.timeOfDay > 18f || env.timeOfDay < 6f)
                {
                    baseExposure = 0.7f;
                }
                else
                {
                    baseExposure = 0.2f;
                }

                // Humidity increases mosquito activity
                baseExposure *= env.humidity;

                // Wind reduces mosquitoes
                baseExposure *= Mathf.InverseLerp(10f, 0f, env.windSpeed);

                mosquitoExposure = Mathf.Clamp01(baseExposure);
            }
            else
            {
                mosquitoExposure = 0f;
            }
        }

        // ===== SLIP RISK =====
        // From scenario (part 3/4):
        // - "sliska sciolka lesna i kawalki kamieni wapiennych"
        // - "stopa nawet zabezpieczona w butach jest podatna na poslizg"
        // - "zwieksza sie ryzyko przewrocenia na sliskiej sciolce"
        // - "oblodzenie skalek i gruba pokrywa sniezna" in winter
        // - Fall risk -> fractures, sprains (from scenario: "zlamania reki lub nogi,
        //   zwichniecie kostki")

        private void UpdateSlipRisk(EnvironmentSnapshot env)
        {
            float baseSlipRisk = 0f;

            switch (env.season)
            {
                case Season.Autumn:
                    // Wet leaves, rain-soaked forest floor
                    baseSlipRisk = 0.3f + env.precipitation * 0.4f;
                    break;
                case Season.Winter:
                    // Ice, snow cover hides obstacles
                    // From scenario: "oblodzenie skalek i gruba pokrywa sniezna"
                    baseSlipRisk = 0.5f;
                    if (env.weather == WeatherCondition.Snow || env.weather == WeatherCondition.RainWithSleet)
                    {
                        baseSlipRisk = 0.7f;
                    }
                    break;
                case Season.Spring:
                    // Wet terrain, melting snow
                    baseSlipRisk = 0.2f + env.precipitation * 0.3f;
                    break;
                case Season.Summer:
                    baseSlipRisk = 0.05f + env.precipitation * 0.2f;
                    break;
            }

            // Rain increases slip risk across all seasons
            baseSlipRisk += env.precipitation * 0.15f;

            // Night increases risk (can't see obstacles)
            // From scenario: "nocne poruszanie sie utrudnione"
            if (!env.isDaytime)
            {
                baseSlipRisk += 0.2f;
            }

            slipRisk = Mathf.Clamp01(baseSlipRisk);
            OnSlipRiskChanged?.Invoke(slipRisk);
        }

        // ===== VISIBILITY =====
        // From scenario: "jesienia wczesniej robi sie ciemno"
        // Night movement recommended between 3-5 AM for stealth
        // but risky for terrain hazards

        private void UpdateVisibility(EnvironmentSnapshot env)
        {
            float baseVisibility = env.isDaytime ? 1f : 0.1f;

            // Weather reduces visibility
            switch (env.weather)
            {
                case WeatherCondition.Fog:
                    baseVisibility *= 0.3f;
                    break;
                case WeatherCondition.Rain:
                case WeatherCondition.RainWithSleet:
                    baseVisibility *= 0.6f;
                    break;
                case WeatherCondition.Snow:
                    baseVisibility *= 0.5f;
                    break;
                case WeatherCondition.Storm:
                    baseVisibility *= 0.2f;
                    break;
            }

            // Dawn/dusk transition
            float sunrise = 12f - seasonManager.DaylightHours / 2f;
            float sunset = 12f + seasonManager.DaylightHours / 2f;
            if (env.timeOfDay > sunrise - 1f && env.timeOfDay < sunrise + 1f)
            {
                baseVisibility *= Mathf.InverseLerp(sunrise - 1f, sunrise + 1f, env.timeOfDay);
            }
            else if (env.timeOfDay > sunset - 1f && env.timeOfDay < sunset + 1f)
            {
                baseVisibility *= Mathf.InverseLerp(sunset + 1f, sunset - 1f, env.timeOfDay);
            }

            visibilityFactor = Mathf.Clamp01(baseVisibility);
            OnVisibilityChanged?.Invoke(visibilityFactor);
        }

        // ===== SEASONAL WOUND RISKS =====
        // From scenario: terrain-related injuries vary by season.
        // Autumn/winter: slippery terrain -> sprains, fractures
        // Summer: blisters from heat, burns from campfire

        private void ApplySeasonalWoundRisks(EnvironmentSnapshot env, float dtGameHours)
        {
            if (physiologyController == null) return;

            // Slip-based injury chance (very low per-tick, accumulates over marching time)
            // Actual slip events would be triggered by gameplay, this provides background risk
            float slipInjuryChance = slipRisk * 0.0001f * dtGameHours;
            if (UnityEngine.Random.value < slipInjuryChance)
            {
                // From scenario: "zlamania reki lub nogi, zwichniecie kostki"
                WoundType type = UnityEngine.Random.value > 0.6f ? WoundType.Fracture : WoundType.Sprain;
                WoundLocation loc = UnityEngine.Random.value > 0.5f ? WoundLocation.LeftFoot : WoundLocation.RightFoot;
                float severity = UnityEngine.Random.Range(0.2f, 0.6f);

                physiologyController.ApplyWound(type, loc, severity);
                OnWeatherHazardWarning?.Invoke($"TERRAIN_INJURY_{type}");
            }
        }
    }

    // ===== WEATHER EFFECTS CONFIG =====

    /// <summary>
    /// Tunable configuration for weather effect parameters.
    /// Create assets: Assets > Create > Plaga44 > Weather Effects Config
    /// </summary>
    [CreateAssetMenu(fileName = "WeatherEffectsConfig", menuName = "Plaga44/Weather Effects Config")]
    public class WeatherEffectsConfig : ScriptableObject
    {
        [Header("Hypothermia")]
        [Tooltip("Temperature below which hypothermia risk begins (Celsius).")]
        public float hypothermiaTemperatureThreshold = 10f;

        [Header("Heatstroke")]
        [Tooltip("Temperature above which heatstroke risk begins (Celsius). From scenario: high summer temps.")]
        public float heatstrokeTemperatureThreshold = 30f;

        [Header("Frostbite")]
        [Tooltip("Temperature below which frostbite risk begins. From scenario: 'odmrozenia nog i rak'.")]
        public float frostbiteTemperatureThreshold = 0f;

        [Tooltip("Rate at which frostbite accumulates per game hour at max severity.")]
        public float frostbiteAccumulationRate = 0.05f;

        [Tooltip("Rate at which frostbite risk recovers per game hour when warm.")]
        public float frostbiteRecoveryRate = 0.02f;

        [Header("UV Exposure")]
        [Tooltip("Rate at which UV exposure accumulates per game hour.")]
        public float uvAccumulationRate = 0.02f;
    }
}
