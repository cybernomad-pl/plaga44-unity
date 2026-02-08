// PLAGA '44 VR - PhysiologyConfig
// ScriptableObject configuration for tuning all physiological parameters.
// Designed for rapid balancing during QA (IPK months 08-09).
// JSON-exportable for external tooling and Neo4j model sync.

using UnityEngine;

namespace Plaga44.Physiology
{
    /// <summary>
    /// Tunable configuration for the physiology simulation.
    /// Create assets: Assets > Create > Plaga44 > Physiology Config
    /// </summary>
    [CreateAssetMenu(fileName = "PhysiologyConfig", menuName = "Plaga44/Physiology Config")]
    public class PhysiologyConfig : ScriptableObject
    {
        [Header("Time Scale")]
        [Tooltip("How many game hours pass per real second. Default: 0.01 (1 game hour = 100 real seconds)")]
        public float gameHoursPerRealSecond = 0.01f;

        [Tooltip("Real-time minutes per game day (at default scale: ~40 minutes per day)")]
        public float RealMinutesPerGameDay => 24f / (gameHoursPerRealSecond * 60f);

        // ===== THERMOREGULATION PARAMETERS =====

        [Header("Thermoregulation")]
        [Tooltip("Rate at which core temperature changes. Lower = more stable.")]
        public float coreTemperatureChangeRate = 0.001f;

        [Tooltip("Celsius per second lost from wet clothing.")]
        public float wetClothingHeatLossRate = 0.005f;

        [Tooltip("Fire heat multiplier for warming effect.")]
        public float fireHeatMultiplier = 0.01f;

        [Tooltip("Clothing drying rate when near fire.")]
        public float clothingDryRate = 0.05f;

        [Tooltip("Base hypothermia onset temperature (Celsius).")]
        public float hypothermiaOnsetTemp = 35f;

        [Tooltip("Moderate hypothermia temperature.")]
        public float hypothermiaModerateTemp = 32f;

        [Tooltip("Severe hypothermia temperature.")]
        public float hypothermiaSevereTemp = 28f;

        [Tooltip("Hyperthermia onset temperature.")]
        public float hyperthermiaOnsetTemp = 38.5f;

        [Tooltip("Severe hyperthermia (heat stroke) temperature.")]
        public float heatStrokeTemp = 40f;

        // ===== HYDRATION PARAMETERS =====

        [Header("Hydration")]
        [Tooltip("Base water loss per game hour (fraction).")]
        public float baseWaterLossPerHour = 0.002f;

        [Tooltip("Additional water loss multiplier during hot weather.")]
        public float heatWaterLossMultiplier = 0.0005f;

        [Tooltip("Water loss multiplier from physical exertion.")]
        public float exertionWaterLossMultiplier = 0.0001f;

        [Tooltip("Critical dehydration threshold (fraction). Death occurs at 0.")]
        public float criticalDehydration = 0.1f;

        // ===== NUTRITION PARAMETERS =====

        [Header("Nutrition")]
        [Tooltip("Basal metabolic rate in kcal per game hour.")]
        public float basalMetabolicRate = 80f;

        [Tooltip("Calories burned per unit of movement speed * weight.")]
        public float exertionCalorieMultiplier = 5f;

        [Tooltip("Additional calories burned from cold (shivering thermogenesis).")]
        public float coldCalorieBurnRate = 20f;

        [Tooltip("Starting caloric reserve in kcal.")]
        public float startingCalories = 2000f;

        [Tooltip("Glucose depletion rate per game hour base.")]
        public float glucoseDepletionRate = 0.001f;

        // ===== FATIGUE PARAMETERS =====

        [Header("Fatigue")]
        [Tooltip("Hours awake before sleep debt starts accumulating.")]
        public float sleepDebtOnsetHours = 16f;

        [Tooltip("Sleep recovery rate multiplier (higher = faster recovery).")]
        public float sleepRecoveryRate = 0.05f;

        [Tooltip("Fatigue accumulation from carrying weight.")]
        public float weightFatigueMultiplier = 0.0001f;

        [Tooltip("Mental fatigue base drain rate per game hour.")]
        public float mentalFatigueBaseRate = 0.005f;

        // ===== TOXIN PARAMETERS =====

        [Header("Toxins")]
        [Tooltip("Toxin natural decay rate per game hour.")]
        public float toxinDecayRate = 0.01f;

        [Tooltip("Probability of waterborne illness from unpurified water.")]
        public float unpurifiedWaterIllnessChance = 0.3f;

        [Tooltip("Duration of mushroom alkaloid effects in game hours.")]
        public float mushroomAlkaloidDuration = 4f;

        // ===== WOUND PARAMETERS =====

        [Header("Wounds")]
        [Tooltip("Hours before untreated wound infection risk rises.")]
        public float infectionOnsetHours = 6f;

        [Tooltip("Infection progression rate per game hour (untreated).")]
        public float infectionProgressionRate = 0.02f;

        [Tooltip("Wound healing rate multiplier.")]
        public float healingRateMultiplier = 0.01f;

        [Tooltip("Puncture wound initial infection risk.")]
        public float punctureInfectionRisk = 0.3f;

        // ===== PSYCHOLOGICAL PARAMETERS =====

        [Header("Psychology")]
        [Tooltip("Days before depression risk begins (from scenario docs: 2 weeks).")]
        public float depressionOnsetDays = 14f;

        [Tooltip("Survival instinct growth rate per game hour.")]
        public float survivalInstinctGrowthRate = 0.001f;

        [Tooltip("Morale recovery rate in comfort conditions.")]
        public float moraleRecoveryRate = 0.001f;

        // ===== EMERSION EFFECT THRESHOLDS =====
        // These control when physiological states trigger VR effects.
        // Critical for balancing during QA (IPK months 08-09):
        // "threshold dla tremor, duration zaburzeń po grzybach, resource scarcity curve"

        [Header("Emersion Thresholds")]
        [Tooltip("Carbohydrate level below which tremor begins.")]
        public float tremorOnsetCarbohydrateLevel = 0.4f;

        [Tooltip("Maximum tremor amplitude at controller level.")]
        public float maxTremorAmplitude = 0.02f;

        [Tooltip("Toxin level at which visual distortions begin.")]
        public float visualDistortionOnsetToxin = 0.2f;

        [Tooltip("Sleep debt hours before hallucinations begin.")]
        public float hallucinationOnsetSleepDebt = 36f;

        [Tooltip("Maximum input lag in seconds from toxin effects.")]
        public float maxInputLagSeconds = 0.3f;

        [Tooltip("Oxygen saturation below which hypoxia visual effects begin.")]
        public float hypoxiaVisualOnset = 90f;

        // ===== EQUIPMENT PARAMETERS =====

        [Header("Equipment Limits (from scenario docs)")]
        [Tooltip("Maximum backpack weight in kg. From scenario: 25 kg max.")]
        public float maxBackpackWeight = 25f;

        [Tooltip("Weight at which fatigue penalty doubles.")]
        public float heavyLoadThreshold = 15f;

        [Tooltip("Foot damage rate multiplier from marching.")]
        public float marchFootDamageRate = 0.00001f;

        [Tooltip("Recommended march break interval in game hours. From scenario: 2-3 hours.")]
        public float recommendedBreakInterval = 2.5f;

        [Tooltip("Recommended break duration in game hours. From scenario: 40-60 minutes.")]
        public float recommendedBreakDuration = 0.75f;
    }
}
