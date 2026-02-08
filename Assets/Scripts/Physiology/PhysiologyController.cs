// PLAGA '44 VR - PhysiologyController
// Central MonoBehaviour that updates the player's biological state each frame.
// Consumes environmental inputs (temperature, altitude, exertion) and
// outputs the PhysiologyState that drives all emersion effects.
//
// Architecture: This component runs on each player's local client.
// Key state variables are synchronized via Photon for co-op awareness.
// The EmersionEffectsManager reads from this to drive VR output.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.Physiology
{
    /// <summary>
    /// Updates player physiology state based on environmental conditions,
    /// player actions, consumed items, and time progression.
    /// Attach to the player VR rig root.
    /// </summary>
    public class PhysiologyController : MonoBehaviour
    {
        [Header("Current State")]
        [SerializeField] private PhysiologyState state = new PhysiologyState();

        [Header("Configuration")]
        [SerializeField] private PhysiologyConfig config;

        [Header("Environmental Inputs")]
        [SerializeField] private float ambientTemperature = 15f;  // Celsius
        [SerializeField] private float windSpeed = 0f;            // m/s
        [SerializeField] private float altitude = 300f;           // meters ASL (Jura avg)
        [SerializeField] private float precipitation = 0f;        // 0-1
        [SerializeField] private bool isInShelter = false;
        [SerializeField] private bool isNearFire = false;
        [SerializeField] private float fireHeatOutput = 0f;       // Watts equivalent

        [Header("Activity")]
        [SerializeField] private float movementSpeed = 0f;        // m/s
        [SerializeField] private float carriedWeight = 0f;        // kg
        [SerializeField] private float terrainDifficulty = 1f;    // 1=flat, 2=forest, 3=karst rocks
        [SerializeField] private bool isSleeping = false;
        [SerializeField] private bool isSwimming = false;

        [Header("Equipment")]
        [SerializeField] private float clothingInsulation = 1f;   // Thermal resistance factor
        [SerializeField] private bool hasGoggles = false;         // Snow blindness protection
        [SerializeField] private bool hasSunscreen = false;       // UV protection
        [SerializeField] private bool hasAntiMosquito = false;    // Insect protection
        [SerializeField] private float bootQuality = 1f;          // Foot protection factor

        [Header("Wound Tracking")]
        [SerializeField] private List<WoundState> activeWounds = new List<WoundState>();

        // Events for other systems to react to state changes
        public event Action<PhysiologyState> OnStateChanged;
        public event Action<string> OnPlayerDeath;
        public event Action<float> OnTremorChanged;
        public event Action<WoundState> OnNewWound;

        // Time tracking
        private float gameTimeElapsed = 0f;
        private float lastMealTime = 0f;
        private float lastDrinkTime = 0f;
        private float lastSleepTime = 0f;

        /// <summary>
        /// Read-only access to current physiological state.
        /// Used by EmersionEffectsManager and UI systems.
        /// </summary>
        public PhysiologyState State => state;

        /// <summary>
        /// Whether the dual-mode system has survival mechanics enabled.
        /// Mode A (edu-tourist) = false, Mode B (hardcore) = true.
        /// </summary>
        public bool SurvivalModeEnabled { get; set; } = true;

        private void Update()
        {
            if (!SurvivalModeEnabled) return;
            if (!state.IsAlive) return;

            float dt = Time.deltaTime;
            float gameHoursPerRealSecond = config != null ? config.gameHoursPerRealSecond : 0.01f;
            float dtGameHours = dt * gameHoursPerRealSecond;

            gameTimeElapsed += dtGameHours;
            state.daysSurvived = gameTimeElapsed / 24f;

            UpdateThermoregulation(dt, dtGameHours);
            UpdateHydrationNutrition(dt, dtGameHours);
            UpdateFatigueSleep(dt, dtGameHours);
            UpdateWounds(dt, dtGameHours);
            UpdatePsychologicalState(dt, dtGameHours);
            UpdateRespiration(dt);
            UpdateFootCondition(dt, dtGameHours);
            UpdateCircadianRhythm(dtGameHours);

            state.activeWounds = activeWounds.Count;

            OnStateChanged?.Invoke(state);

            // Check death conditions
            if (!state.IsAlive)
            {
                string cause = state.GetDeathCause();
                OnPlayerDeath?.Invoke(cause);
                Debug.Log($"[noEZUS] SPECIMEN TERMINATED. Cause: {cause}. " +
                         $"Survival time: {state.daysSurvived:F1} days. " +
                         $"Data points collected: {Mathf.RoundToInt(gameTimeElapsed * 60)}");
            }
        }

        // ===== THERMOREGULATION =====
        // Wind chill, wet clothing, shelter, fire, clothing insulation.
        // From scenario docs: winter is the hardest season, hypothermia/frostbite risk.

        private void UpdateThermoregulation(float dt, float dtGameHours)
        {
            // Wind chill factor
            float windChill = windSpeed > 0 ? windSpeed * 0.5f : 0f;
            float effectiveAmbient = ambientTemperature - windChill;

            // Wet clothing dramatically accelerates heat loss
            // From scenario: "nasiakniecie woda z deszczu" on backpacks/clothing
            if (precipitation > 0 && !isInShelter)
            {
                state.clothingWetness = Mathf.Min(1f, state.clothingWetness + precipitation * dt * 0.1f);
            }
            else if (isNearFire)
            {
                state.clothingWetness = Mathf.Max(0f, state.clothingWetness - dt * 0.05f);
            }

            float wetnessPenalty = state.clothingWetness * 5f; // Wet clothing = massive heat loss
            float shelterBonus = isInShelter ? 5f : 0f;
            float fireBonus = isNearFire ? Mathf.Min(fireHeatOutput * 0.01f, 10f) : 0f;
            float exertionHeat = movementSpeed * carriedWeight * 0.01f; // Exercise generates heat

            float targetTemp = effectiveAmbient + shelterBonus + fireBonus + exertionHeat
                             - wetnessPenalty + clothingInsulation * 2f;

            // Core temperature moves slowly toward equilibrium
            float coreRate = config != null ? config.coreTemperatureChangeRate : 0.001f;
            state.coreTemperature = Mathf.Lerp(state.coreTemperature,
                Mathf.Clamp(targetTemp + 20f, 25f, 42f), // Body tries to maintain 36.6
                coreRate * dt);

            // Peripheral temperature responds faster
            state.peripheralTemperature = Mathf.Lerp(state.peripheralTemperature,
                targetTemp + 15f,
                coreRate * 5f * dt);

            // Hypothermia staging
            if (state.coreTemperature < 35f) state.hypothermiaStage = 1;
            else if (state.coreTemperature < 32f) state.hypothermiaStage = 2;
            else if (state.coreTemperature < 28f) state.hypothermiaStage = 3;
            else state.hypothermiaStage = 0;

            // Hyperthermia staging
            if (state.coreTemperature > 40f) state.hyperthermiaStage = 2;
            else if (state.coreTemperature > 38.5f) state.hyperthermiaStage = 1;
            else state.hyperthermiaStage = 0;
        }

        // ===== HYDRATION & NUTRITION =====
        // From scenario: electrolytes every 2-3 hours, magnesium 2x daily,
        // 2 glasses of water every hour during summer marches.

        private void UpdateHydrationNutrition(float dt, float dtGameHours)
        {
            // Base metabolic water loss
            float baseWaterLoss = 0.002f; // per game hour
            float exertionWaterLoss = movementSpeed * carriedWeight * 0.0001f;
            float temperatureWaterLoss = Mathf.Max(0f, (ambientTemperature - 20f) * 0.0005f);
            float totalWaterLoss = (baseWaterLoss + exertionWaterLoss + temperatureWaterLoss) * dtGameHours;

            state.hydration = Mathf.Max(0f, state.hydration - totalWaterLoss);

            // Caloric expenditure
            float basalMetabolicRate = 80f; // kcal per game hour
            float exertionCalories = movementSpeed * carriedWeight * terrainDifficulty * 5f;
            float thermoCalories = Mathf.Max(0f, (36.6f - state.coreTemperature)) * 20f; // Shivering burns calories
            float totalCalorieBurn = (basalMetabolicRate + exertionCalories + thermoCalories) * dtGameHours;

            state.caloricReserve = Mathf.Max(0f, state.caloricReserve - totalCalorieBurn);

            // Carbohydrate level derived from caloric reserve
            state.carbohydrateLevel = Mathf.Clamp01(state.caloricReserve / 2000f);

            // Glucose level - drops faster during exertion
            float glucoseDrain = (0.001f + exertionCalories * 0.0001f) * dtGameHours;
            state.glucoseLevel = Mathf.Max(0f, state.glucoseLevel - glucoseDrain);

            // Electrolyte depletion - from sweating and exertion
            float electrolyteLoss = (exertionWaterLoss + temperatureWaterLoss) * 0.5f;
            state.electrolyteBalance = Mathf.Max(0f, state.electrolyteBalance - electrolyteLoss);

            // Magnesium depletion
            state.magnesiumLevel = Mathf.Max(0f, state.magnesiumLevel - 0.0005f * dtGameHours);

            // Toxin decay over time
            if (state.toxinLevel > 0f)
            {
                float toxinDecayRate = config != null ? config.toxinDecayRate : 0.01f;
                state.toxinLevel = Mathf.Max(0f, state.toxinLevel - toxinDecayRate * dtGameHours);
                if (state.toxinLevel <= 0.01f)
                {
                    state.toxinLevel = 0f;
                    state.activeToxin = ToxinType.None;
                }
            }
        }

        // ===== FATIGUE & SLEEP =====
        // From scenario: "dlugotrawle przebywanie w lasach powoduje po 2 tygodniach
        // oslabienia i przemeczenie organizmu"

        private void UpdateFatigueSleep(float dt, float dtGameHours)
        {
            if (isSleeping)
            {
                // Recovery during sleep
                float sleepQuality = isInShelter ? 1f : 0.5f; // Shelter improves sleep
                sleepQuality *= (state.coreTemperature > 34f && state.coreTemperature < 38f) ? 1f : 0.3f;
                sleepQuality *= (1f - state.painLevel); // Pain disrupts sleep

                state.physicalFatigue = Mathf.Max(0f, state.physicalFatigue - 0.05f * sleepQuality * dtGameHours);
                state.mentalFatigue = Mathf.Max(0f, state.mentalFatigue - 0.03f * sleepQuality * dtGameHours);
                state.sleepDebtHours = Mathf.Max(0f, state.sleepDebtHours - sleepQuality * dtGameHours);
                state.hoursSinceLastSleep = 0f;
            }
            else
            {
                // Fatigue accumulation
                float exertionFatigue = movementSpeed * carriedWeight * terrainDifficulty * 0.0001f;
                state.physicalFatigue = Mathf.Min(1f, state.physicalFatigue + exertionFatigue * dtGameHours);

                // Mental fatigue increases with duration, monotony, stress
                float mentalDrain = 0.005f + state.stressLevel * 0.01f;
                state.mentalFatigue = Mathf.Min(1f, state.mentalFatigue + mentalDrain * dtGameHours);

                state.hoursSinceLastSleep += dtGameHours;

                // Sleep debt accumulates after 16 hours awake
                if (state.hoursSinceLastSleep > 16f)
                {
                    state.sleepDebtHours += dtGameHours;
                }
            }
        }

        // ===== WOUND MANAGEMENT =====

        private void UpdateWounds(float dt, float dtGameHours)
        {
            float totalBleedRate = 0f;

            for (int i = activeWounds.Count - 1; i >= 0; i--)
            {
                var wound = activeWounds[i];
                wound.timeSinceInjury += dtGameHours;

                // Infection progression for untreated wounds
                if (!wound.isTreated && wound.timeSinceInjury > 6f)
                {
                    wound.infectionRisk = Mathf.Min(1f, wound.infectionRisk + 0.02f * dtGameHours);
                }

                // Bleeding
                if (wound.bleedRate > 0f)
                {
                    totalBleedRate += wound.bleedRate;
                    // Treated wounds bleed less
                    if (wound.isTreated) wound.bleedRate *= 0.9f;
                }

                // Healing
                if (wound.isTreated)
                {
                    float healRate = 0.01f * dtGameHours * Mathf.Lerp(0.3f, 1f, state.caloricReserve / 2000f);
                    wound.healingProgress = Mathf.Min(1f, wound.healingProgress + healRate);

                    if (wound.healingProgress >= 1f)
                    {
                        activeWounds.RemoveAt(i);
                        continue;
                    }
                }

                // Pain contribution
                state.painLevel = Mathf.Min(1f, state.painLevel + wound.severity * 0.1f * (1f - wound.healingProgress));

                // Update limb function based on wound location
                UpdateLimbFunction(wound);
            }

            // Blood loss from active bleeding
            state.bloodVolume = Mathf.Max(0f, state.bloodVolume - totalBleedRate * dt);

            // Infection contributes to overall health
            float totalInfection = 0f;
            foreach (var wound in activeWounds)
            {
                totalInfection += wound.infectionRisk * wound.severity;
            }
            state.infectionLevel = Mathf.Clamp01(totalInfection);

            // Pain naturally decays slightly
            state.painLevel = Mathf.Max(0f, state.painLevel - 0.001f * dt);
        }

        private void UpdateLimbFunction(WoundState wound)
        {
            float functionReduction = wound.severity * (1f - wound.healingProgress);
            switch (wound.location)
            {
                case WoundLocation.LeftArm:
                case WoundLocation.LeftHand:
                    state.leftArmFunction = Mathf.Max(0f, 1f - functionReduction);
                    break;
                case WoundLocation.RightArm:
                case WoundLocation.RightHand:
                    state.rightArmFunction = Mathf.Max(0f, 1f - functionReduction);
                    break;
                case WoundLocation.LeftLeg:
                case WoundLocation.LeftFoot:
                    state.leftLegFunction = Mathf.Max(0f, 1f - functionReduction);
                    break;
                case WoundLocation.RightLeg:
                case WoundLocation.RightFoot:
                    state.rightLegFunction = Mathf.Max(0f, 1f - functionReduction);
                    break;
            }
        }

        // ===== PSYCHOLOGICAL STATE =====
        // From scenario: "spadek popedu plciowego", "calkowity brak hamulcow moralnych"

        private void UpdatePsychologicalState(float dt, float dtGameHours)
        {
            // Morale affected by: comfort, food, shelter, success
            float comfortFactor = isInShelter ? 0.3f : 0f;
            comfortFactor += isNearFire ? 0.2f : 0f;
            comfortFactor += state.caloricReserve > 1000f ? 0.2f : 0f;
            comfortFactor += state.hydration > 0.5f ? 0.2f : 0f;

            float discomfortFactor = state.painLevel * 0.3f +
                                    state.physicalFatigue * 0.2f +
                                    state.mentalFatigue * 0.3f +
                                    (1f - state.hydration) * 0.2f;

            state.morale = Mathf.Clamp01(state.morale + (comfortFactor - discomfortFactor) * 0.001f * dtGameHours);

            // Stress increases with danger, decreases with safety
            state.stressLevel = Mathf.Clamp01(
                state.stressLevel +
                (discomfortFactor - comfortFactor) * 0.005f * dtGameHours
            );

            // Survival instinct grows with experience
            state.survivalInstinct = Mathf.Min(1f, state.survivalInstinct + 0.001f * dtGameHours);
        }

        // ===== RESPIRATION =====

        private void UpdateRespiration(float dt)
        {
            // Altitude effect on oxygen (Jura is 300-500m, minimal effect, but caves might be different)
            float altitudeOxygenFactor = altitude < 2000f ? 1f : Mathf.Lerp(1f, 0.7f, (altitude - 2000f) / 3000f);

            // Exertion increases demand
            float exertionDemand = movementSpeed * carriedWeight * 0.001f;

            // Blood loss reduces oxygen carrying capacity
            float oxygenCapacity = state.bloodVolume * altitudeOxygenFactor;

            float targetSaturation = Mathf.Clamp(oxygenCapacity * 100f - exertionDemand, 60f, 100f);
            state.oxygenSaturation = Mathf.Lerp(state.oxygenSaturation, targetSaturation, dt * 0.5f);

            // Respiration rate adjusts
            state.respirationRate = Mathf.Lerp(12f, 35f, exertionDemand + (1f - oxygenCapacity) * 0.5f);
        }

        // ===== FOOT CONDITION =====
        // From scenario: "odciski i otarcia stop", "grzybica stop" - critical survival detail

        private void UpdateFootCondition(float dt, float dtGameHours)
        {
            if (movementSpeed > 0.5f)
            {
                float marchDamage = movementSpeed * carriedWeight * 0.00001f * (1f / bootQuality);
                float terrainDamage = terrainDifficulty * 0.0001f;
                state.footCondition = Mathf.Max(0f, state.footCondition - (marchDamage + terrainDamage) * dtGameHours);
            }

            // Fungal risk from wet feet
            if (state.clothingWetness > 0.5f && movementSpeed > 0)
            {
                state.footCondition = Mathf.Max(0f, state.footCondition - 0.001f * dtGameHours);
            }
        }

        // ===== CIRCADIAN RHYTHM =====

        private void UpdateCircadianRhythm(float dtGameHours)
        {
            state.circadianPhase = (state.circadianPhase + dtGameHours) % 24f;
        }

        // ===== PUBLIC API - Called by inventory/interaction systems =====

        /// <summary>
        /// Consume food item. Updates caloric reserve, glucose, potentially toxins.
        /// </summary>
        public void ConsumeFood(float calories, float glucoseBoost, ToxinType toxin = ToxinType.None, float toxinAmount = 0f)
        {
            state.caloricReserve += calories;
            state.glucoseLevel = Mathf.Min(1f, state.glucoseLevel + glucoseBoost);
            lastMealTime = gameTimeElapsed;

            if (toxin != ToxinType.None)
            {
                state.activeToxin = toxin;
                state.toxinLevel = Mathf.Min(1f, state.toxinLevel + toxinAmount);
            }
        }

        /// <summary>
        /// Consume water. From scenario: 2 glasses per hour in summer heat.
        /// </summary>
        public void ConsumeWater(float amount, bool isPurified = true)
        {
            state.hydration = Mathf.Min(1f, state.hydration + amount);
            lastDrinkTime = gameTimeElapsed;

            if (!isPurified)
            {
                // Risk of waterborne illness
                // From scenario: contaminated water causes "dur brzuszny, zatrucie, czerwonka, biegunka"
                float contaminationRisk = 0.3f;
                if (UnityEngine.Random.value < contaminationRisk)
                {
                    state.activeToxin = ToxinType.WaterContaminated;
                    state.toxinLevel = Mathf.Min(1f, state.toxinLevel + 0.3f);
                }
            }
        }

        /// <summary>
        /// Take supplement (electrolytes, magnesium, etc.)
        /// From scenario: "elektrolity rozpuszone w chlodnej wodzie", "magnez w tabletkach"
        /// </summary>
        public void TakeSupplement(SupplementType type)
        {
            switch (type)
            {
                case SupplementType.Electrolytes:
                    state.electrolyteBalance = Mathf.Min(1f, state.electrolyteBalance + 0.3f);
                    break;
                case SupplementType.Magnesium:
                    state.magnesiumLevel = Mathf.Min(1f, state.magnesiumLevel + 0.25f);
                    break;
                case SupplementType.Painkiller:
                    state.painLevel = Mathf.Max(0f, state.painLevel - 0.4f);
                    break;
                case SupplementType.Glucose:
                    state.glucoseLevel = Mathf.Min(1f, state.glucoseLevel + 0.4f);
                    break;
            }
        }

        /// <summary>
        /// Apply wound to player. Called by damage/hazard systems.
        /// </summary>
        public void ApplyWound(WoundType type, WoundLocation location, float severity)
        {
            var wound = new WoundState
            {
                type = type,
                location = location,
                severity = Mathf.Clamp01(severity),
                infectionRisk = type == WoundType.Puncture ? 0.3f : 0.1f, // Puncture wounds infect faster
                bleedRate = type == WoundType.Gunshot ? 0.01f : (type == WoundType.Laceration ? 0.005f : 0f),
                healingProgress = 0f,
                isTreated = false,
                timeSinceInjury = 0f
            };

            activeWounds.Add(wound);
            OnNewWound?.Invoke(wound);
        }

        /// <summary>
        /// Treat a wound with medical supplies.
        /// From scenario: "plastry, bandaze, woda utleniona"
        /// </summary>
        public void TreatWound(int woundIndex)
        {
            if (woundIndex >= 0 && woundIndex < activeWounds.Count)
            {
                activeWounds[woundIndex].isTreated = true;
                activeWounds[woundIndex].bleedRate *= 0.1f;
                activeWounds[woundIndex].infectionRisk *= 0.5f;
            }
        }

        /// <summary>
        /// Set environmental conditions. Called by WeatherSystem/EnvironmentManager.
        /// </summary>
        public void SetEnvironment(float temperature, float wind, float rain, bool shelter, bool nearFire, float fireHeat)
        {
            ambientTemperature = temperature;
            windSpeed = wind;
            precipitation = rain;
            isInShelter = shelter;
            isNearFire = nearFire;
            fireHeatOutput = fireHeat;
        }

        /// <summary>
        /// Set activity state. Called by PlayerMovement system.
        /// </summary>
        public void SetActivity(float speed, float weight, float terrain, bool sleeping, bool swimming)
        {
            movementSpeed = speed;
            carriedWeight = weight;
            terrainDifficulty = terrain;
            isSleeping = sleeping;
            isSwimming = swimming;

            if (swimming)
            {
                state.clothingWetness = 1f;
            }
        }
    }

    public enum SupplementType
    {
        Electrolytes,
        Magnesium,
        Painkiller,
        Glucose,
        AntiFungal,
        Antibiotic
    }
}
