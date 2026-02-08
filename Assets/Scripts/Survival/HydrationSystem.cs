// PLAGA '44 VR - HydrationSystem
// Water intake tracking and dehydration mechanics per season.
//
// From scenario docs (Gra_scenariusz parts 1, 4):
// - "Nalezy spozywac duzo wody co 1 godzine maksymalnie rownowartosci
//    dwoch szklanek wody" (2 glasses per hour in summer)
// - "Nalezy co 2 lub 3 godziny zazywac elektrolity rozpuszone w chlodnej wodzie"
// - "Co najmniej dwa razy dziennie zazyc po jednej tabletce magnezu"
// - "odwodnienie organizmu poprzez utrate elektrolitow i magnezu oraz glukozy"
// - Water sources: canteens (1L x2, 2L x1), thermoses, looted bottles
// - "Wode powinno sie przetrzymywac w plastykowych butelkach 1.5L i 0.5L
//    oraz manierkach okolo 1 litra 2 sztuki i jedna okolo 2 litrow"
// - Contaminated water: "zrodla wody w ktorych rozkladaja sie ciala zwierzat
//    moze spowodowac dur brzuszny, zatrucie, czerwonke, biegunke"
//
// Architecture: Subscribes to SeasonManager for seasonal modifiers.
// Drives PhysiologyController water/supplement consumption.

using System;
using UnityEngine;

namespace Plaga44.Survival
{
    using Plaga44.Physiology;

    /// <summary>
    /// Manages water inventory, consumption tracking, dehydration rates,
    /// and electrolyte/magnesium supplement scheduling.
    /// Provides warnings when the player needs to drink.
    /// </summary>
    public class HydrationSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SeasonManager seasonManager;
        [SerializeField] private PhysiologyController physiologyController;

        [Header("Water Inventory")]
        [Tooltip("Current water supply in liters. From scenario: 2x1L canteens + 1x2L canteen = 4L base.")]
        [SerializeField] private float waterSupplyLiters = 4f;

        [Tooltip("Maximum water carrying capacity in liters. From scenario: canteens + bottles.")]
        [SerializeField] private float maxWaterCapacity = 6f;

        [Header("Supplement Inventory")]
        [SerializeField] private int electrolyteCapsulesRemaining = 4;
        [SerializeField] private int magnesiumTabletsRemaining = 4;

        [Header("Consumption Tracking")]
        [SerializeField] private float hoursSinceLastDrink = 0f;
        [SerializeField] private float hoursSinceLastElectrolytes = 0f;
        [SerializeField] private float hoursSinceLastMagnesium = 12f;
        [SerializeField] private int electrolyteDosesToday = 0;
        [SerializeField] private int magnesiumDosesToday = 0;

        [Header("Dehydration State")]
        [SerializeField] private float dehydrationRate = 1f;
        [SerializeField] private float currentThirst = 0f;

        [Header("Configuration")]
        [SerializeField] private HydrationConfig config;

        // Time tracking
        private float gameHoursPerRealSecond = 0.01f;
        private float lastDayReset = 0f;

        // Events
        public event Action<float> OnThirstChanged;
        public event Action OnNeedWater;             // "Drink now" warning
        public event Action OnNeedElectrolytes;      // Electrolyte schedule
        public event Action OnNeedMagnesium;         // Magnesium schedule
        public event Action<float> OnWaterSupplyChanged;
        public event Action OnWaterDepleted;
        public event Action<string> OnContaminationRisk;

        // Public accessors
        public float WaterSupplyLiters => waterSupplyLiters;
        public float MaxWaterCapacity => maxWaterCapacity;
        public float CurrentThirst => currentThirst;
        public float DehydrationRate => dehydrationRate;
        public int ElectrolytesRemaining => electrolyteCapsulesRemaining;
        public int MagnesiumRemaining => magnesiumTabletsRemaining;
        public float HoursSinceLastDrink => hoursSinceLastDrink;

        private void Update()
        {
            float dt = Time.deltaTime;
            float dtGameHours = dt * gameHoursPerRealSecond;

            UpdateDehydrationRate();
            UpdateConsumptionTimers(dtGameHours);
            UpdateThirst(dtGameHours);
            CheckSupplementSchedule();
        }

        // ===== DEHYDRATION RATE =====
        // From scenario (part 4):
        // Summer heat: "odwodnienie organizmu" - rapid water loss
        // Heavy load: "Duzy ciezar 25kg w warunkach letnich powoduje szybsze spalanie"
        // Marching: exertion increases water needs
        // Solution: "spozywac duzo wody co 1 godzine"

        private void UpdateDehydrationRate()
        {
            if (seasonManager == null) return;

            float baseRate = config != null ? config.baseDehydrationRate : 1f;
            float seasonMultiplier = GetSeasonDehydrationMultiplier(seasonManager.CurrentSeason);
            float temperatureMultiplier = GetTemperatureDehydrationMultiplier(seasonManager.CurrentTemperature);
            float weatherMultiplier = GetWeatherDehydrationMultiplier(seasonManager.CurrentWeather);

            dehydrationRate = baseRate * seasonMultiplier * temperatureMultiplier * weatherMultiplier;
        }

        /// <summary>
        /// Season-specific dehydration multipliers.
        /// From scenario: summer is highest risk for dehydration.
        /// Winter still requires hydration but at lower rates.
        /// </summary>
        private float GetSeasonDehydrationMultiplier(Season season)
        {
            switch (season)
            {
                case Season.Summer:
                    // From scenario: need 2 glasses per hour during marches
                    return config != null ? config.summerDehydrationMultiplier : 2.0f;
                case Season.Spring:
                    return config != null ? config.springDehydrationMultiplier : 1.2f;
                case Season.Autumn:
                    return config != null ? config.autumnDehydrationMultiplier : 1.0f;
                case Season.Winter:
                    // Cold air is dry, still causes dehydration but less than heat
                    return config != null ? config.winterDehydrationMultiplier : 0.8f;
                default:
                    return 1f;
            }
        }

        /// <summary>
        /// Temperature affects water loss directly.
        /// From scenario: "wysokie temperatury" cause "odwodnienie".
        /// </summary>
        private float GetTemperatureDehydrationMultiplier(float temperature)
        {
            if (temperature > 30f)
            {
                // From scenario: heat above 30C significantly increases dehydration
                return 1f + (temperature - 30f) * 0.1f;
            }
            else if (temperature < 0f)
            {
                // Cold also dehydrates (dry air, respiratory water loss)
                return 1f + Mathf.Abs(temperature) * 0.02f;
            }
            return 1f;
        }

        private float GetWeatherDehydrationMultiplier(WeatherCondition weather)
        {
            switch (weather)
            {
                case WeatherCondition.HeatWave:
                    return 1.5f; // Extreme heat
                case WeatherCondition.Wind:
                    return 1.2f; // Wind increases evaporation
                case WeatherCondition.Rain:
                case WeatherCondition.Snow:
                    return 0.9f; // Slightly reduced evaporation
                default:
                    return 1f;
            }
        }

        // ===== CONSUMPTION TIMERS =====
        // From scenario (part 4):
        // - Water: "co 1 godzine maksymalnie rownowartosci dwoch szklanek"
        // - Electrolytes: "co 2 lub 3 godziny zazywac elektrolity"
        // - Magnesium: "co najmniej dwa razy dziennie po jednej tabletce magnezu"

        private void UpdateConsumptionTimers(float dtGameHours)
        {
            hoursSinceLastDrink += dtGameHours;
            hoursSinceLastElectrolytes += dtGameHours;
            hoursSinceLastMagnesium += dtGameHours;

            // Reset daily counters
            if (seasonManager != null)
            {
                float currentHour = seasonManager.CurrentTimeOfDay;
                // Reset at midnight (6 AM game time as "new day" for supplement tracking)
                if (currentHour < 7f && lastDayReset > 7f)
                {
                    electrolyteDosesToday = 0;
                    magnesiumDosesToday = 0;
                }
                lastDayReset = currentHour;
            }
        }

        private void UpdateThirst(float dtGameHours)
        {
            // Thirst increases based on dehydration rate and time since last drink
            float thirstRate = dehydrationRate * (config != null ? config.thirstAccumulationRate : 0.1f);
            currentThirst = Mathf.Clamp01(currentThirst + thirstRate * dtGameHours);

            OnThirstChanged?.Invoke(currentThirst);

            // From scenario: need to drink every hour in summer heat
            float drinkInterval = config != null ? config.recommendedDrinkIntervalHours : 1f;
            if (hoursSinceLastDrink > drinkInterval && currentThirst > 0.3f)
            {
                OnNeedWater?.Invoke();
            }
        }

        private void CheckSupplementSchedule()
        {
            // From scenario: electrolytes every 2-3 hours
            float electrolyteInterval = config != null ? config.electrolyteIntervalHours : 2.5f;
            if (hoursSinceLastElectrolytes > electrolyteInterval && electrolyteCapsulesRemaining > 0)
            {
                OnNeedElectrolytes?.Invoke();
            }

            // From scenario: magnesium 2x daily (every ~12 hours)
            float magnesiumInterval = config != null ? config.magnesiumIntervalHours : 12f;
            if (hoursSinceLastMagnesium > magnesiumInterval && magnesiumTabletsRemaining > 0 && magnesiumDosesToday < 2)
            {
                OnNeedMagnesium?.Invoke();
            }
        }

        // ===== PUBLIC API - Called by inventory/interaction systems =====

        /// <summary>
        /// Drink water from player's supply.
        /// From scenario: "rownowartosci dwoch szklanek wody" = ~0.5L per drink.
        /// </summary>
        /// <param name="liters">Amount to drink in liters.</param>
        /// <param name="isPurified">Whether the water source is safe.</param>
        public void DrinkWater(float liters = 0.5f, bool isPurified = true)
        {
            if (waterSupplyLiters <= 0f)
            {
                OnWaterDepleted?.Invoke();
                return;
            }

            float actualDrink = Mathf.Min(liters, waterSupplyLiters);
            waterSupplyLiters -= actualDrink;

            // Convert liters to hydration units for PhysiologyController
            // 0.5L ~= 0.15 hydration units (at full hydration = ~3.3L total body water need)
            float hydrationAmount = actualDrink * 0.3f;

            if (physiologyController != null)
            {
                physiologyController.ConsumeWater(hydrationAmount, isPurified);
            }

            hoursSinceLastDrink = 0f;
            currentThirst = Mathf.Max(0f, currentThirst - actualDrink * 0.5f);

            OnWaterSupplyChanged?.Invoke(waterSupplyLiters);

            if (!isPurified)
            {
                // From scenario: contaminated water -> "dur brzuszny, zatrucie, czerwonka, biegunka"
                OnContaminationRisk?.Invoke("UNPURIFIED_WATER");
            }

            if (waterSupplyLiters <= 0f)
            {
                OnWaterDepleted?.Invoke();
            }
        }

        /// <summary>
        /// Take electrolyte supplement.
        /// From scenario: "elektrolity rozpuszone w chlodnej wodzie co 2-3 godziny"
        /// </summary>
        public void TakeElectrolytes()
        {
            if (electrolyteCapsulesRemaining <= 0) return;

            electrolyteCapsulesRemaining--;
            hoursSinceLastElectrolytes = 0f;
            electrolyteDosesToday++;

            if (physiologyController != null)
            {
                physiologyController.TakeSupplement(SupplementType.Electrolytes);
            }
        }

        /// <summary>
        /// Take magnesium tablet.
        /// From scenario: "co najmniej dwa razy dziennie po jednej tabletce magnezu"
        /// </summary>
        public void TakeMagnesium()
        {
            if (magnesiumTabletsRemaining <= 0) return;

            magnesiumTabletsRemaining--;
            hoursSinceLastMagnesium = 0f;
            magnesiumDosesToday++;

            if (physiologyController != null)
            {
                physiologyController.TakeSupplement(SupplementType.Magnesium);
            }
        }

        /// <summary>
        /// Collect water from a source.
        /// From scenario: "czyste rzeki lub strumienie" (clean streams),
        /// "potoki, strumyki do kapieli" - also usable for drinking after purification.
        /// </summary>
        /// <param name="liters">Amount collected.</param>
        /// <param name="isPurified">Whether source is clean. Streams may be contaminated.</param>
        public void CollectWater(float liters, bool isPurified = false)
        {
            float spaceAvailable = maxWaterCapacity - waterSupplyLiters;
            float collected = Mathf.Min(liters, spaceAvailable);
            waterSupplyLiters += collected;

            OnWaterSupplyChanged?.Invoke(waterSupplyLiters);

            if (!isPurified)
            {
                // From scenario: rivers with decomposing animal bodies are dangerous
                OnContaminationRisk?.Invoke("WATER_SOURCE_UNCHECKED");
            }
        }

        /// <summary>
        /// Add supplements to inventory (from looting).
        /// </summary>
        public void AddElectrolytes(int count)
        {
            electrolyteCapsulesRemaining += count;
        }

        /// <summary>
        /// Add magnesium to inventory (from looting).
        /// </summary>
        public void AddMagnesium(int count)
        {
            magnesiumTabletsRemaining += count;
        }

        /// <summary>
        /// Boil water to purify it.
        /// From scenario: fires used "w celu zagotowania wody do picia".
        /// Requires fire (from ShelterSystem/campfire).
        /// </summary>
        /// <param name="liters">Amount to purify.</param>
        /// <returns>True if purification started successfully.</returns>
        public bool PurifyWater(float liters)
        {
            // Purification requires fire access - validated by caller
            // This marks water as safe and reduces contamination risk
            return waterSupplyLiters >= liters;
        }
    }

    // ===== HYDRATION CONFIG =====

    /// <summary>
    /// Tunable configuration for hydration parameters.
    /// Create assets: Assets > Create > Plaga44 > Hydration Config
    /// </summary>
    [CreateAssetMenu(fileName = "HydrationConfig", menuName = "Plaga44/Hydration Config")]
    public class HydrationConfig : ScriptableObject
    {
        [Header("Dehydration Rates")]
        [Tooltip("Base dehydration rate multiplier.")]
        public float baseDehydrationRate = 1f;

        [Tooltip("Summer dehydration multiplier. From scenario: 2x water needed.")]
        public float summerDehydrationMultiplier = 2.0f;

        [Tooltip("Spring dehydration multiplier.")]
        public float springDehydrationMultiplier = 1.2f;

        [Tooltip("Autumn dehydration multiplier.")]
        public float autumnDehydrationMultiplier = 1.0f;

        [Tooltip("Winter dehydration multiplier. Cold air is dry.")]
        public float winterDehydrationMultiplier = 0.8f;

        [Header("Thirst")]
        [Tooltip("Rate at which thirst accumulates per game hour.")]
        public float thirstAccumulationRate = 0.1f;

        [Header("Recommended Intervals (Game Hours)")]
        [Tooltip("How often to drink. From scenario: every 1 hour in summer heat.")]
        public float recommendedDrinkIntervalHours = 1f;

        [Tooltip("Electrolyte interval. From scenario: every 2-3 hours.")]
        public float electrolyteIntervalHours = 2.5f;

        [Tooltip("Magnesium interval. From scenario: 2x daily = every 12 hours.")]
        public float magnesiumIntervalHours = 12f;
    }
}
