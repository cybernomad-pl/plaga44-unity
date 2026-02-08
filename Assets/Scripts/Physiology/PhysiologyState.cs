// PLAGA '44 VR - Physiology-as-Controller System
// Core data structure representing the player's complete biological state.
// This is the foundation of the "physiology-as-controller" mechanic described
// in IPK grant 4041/25 and SPARK 3.0 pitch.
//
// Design principle: The player's organism IS the controller.
// Carbohydrate deficit -> controller tremor. Mushroom toxins -> lag + FOV aberrations.
// The player doesn't read a health bar - they FEEL the symptoms.

using System;
using UnityEngine;

namespace Plaga44.Physiology
{
    /// <summary>
    /// Complete physiological state of the player avatar.
    /// Updated every frame by PhysiologyController.
    /// Read by EmersionEffectsManager to drive VR output effects.
    /// Synchronizable via Photon for co-op sessions.
    /// </summary>
    [Serializable]
    public class PhysiologyState
    {
        // ===== THERMOREGULATION =====
        // Core vs peripheral temperature model.
        // Based on consultations with Dr. Anna Radlicka-Borysewska (PAN).

        [Header("Thermoregulation")]
        [Tooltip("Core body temperature in Celsius. Normal: 36.1-37.2. Below 35 = hypothermia. Above 38 = hyperthermia.")]
        [Range(30f, 42f)]
        public float coreTemperature = 36.6f;

        [Tooltip("Peripheral (skin) temperature in Celsius. Responds faster to environment than core.")]
        [Range(15f, 42f)]
        public float peripheralTemperature = 33.0f;

        [Tooltip("Current hypothermia stage. 0=none, 1=mild(35-32C), 2=moderate(32-28C), 3=severe(<28C)")]
        [Range(0, 3)]
        public int hypothermiaStage = 0;

        [Tooltip("Current hyperthermia stage. 0=none, 1=heat exhaustion, 2=heat stroke")]
        [Range(0, 2)]
        public int hyperthermiaStage = 0;

        [Tooltip("Wet clothing factor. 0=dry, 1=soaked. Accelerates heat loss dramatically.")]
        [Range(0f, 1f)]
        public float clothingWetness = 0f;

        // ===== HYDRATION & NUTRITION =====
        // Macro/micronutrient model. Caloric deficit drives tremor.
        // From IPK: "Niedobor weglowodanow awatara powoduje tremor miesni"

        [Header("Hydration & Nutrition")]
        [Tooltip("Hydration level. 1.0 = fully hydrated, 0.0 = critical dehydration.")]
        [Range(0f, 1f)]
        public float hydration = 1.0f;

        [Tooltip("Caloric reserve in kcal. Average adult needs ~2000/day. Heavy exertion: 3000-4000.")]
        public float caloricReserve = 2000f;

        [Tooltip("Carbohydrate level (fuel for muscles). Depletes with exertion, causes tremor when low.")]
        [Range(0f, 1f)]
        public float carbohydrateLevel = 1.0f;

        [Tooltip("Electrolyte balance. Depletion causes cramps, confusion, cardiac risk.")]
        [Range(0f, 1f)]
        public float electrolyteBalance = 1.0f;

        [Tooltip("Magnesium level. From scenario docs: critical supplement for survival.")]
        [Range(0f, 1f)]
        public float magnesiumLevel = 1.0f;

        [Tooltip("Glucose level. Drops cause weakness, confusion, loss of consciousness.")]
        [Range(0f, 1f)]
        public float glucoseLevel = 1.0f;

        [Tooltip("Toxin level from contaminated food/water/mushrooms. Drives lag and FOV aberrations.")]
        [Range(0f, 1f)]
        public float toxinLevel = 0f;

        [Tooltip("Type of active toxin. Determines specific emersion effects.")]
        public ToxinType activeToxin = ToxinType.None;

        // ===== FATIGUE & SLEEP =====
        // Circadian rhythm model. Sleep debt accumulates cognitive/motor degradation.
        // From SPARK: "Sleep debt accumulation. Cognitive/motor degradation."

        [Header("Fatigue & Sleep")]
        [Tooltip("Physical fatigue. 0=rested, 1=exhausted. Affected by exertion, weight carried, terrain.")]
        [Range(0f, 1f)]
        public float physicalFatigue = 0f;

        [Tooltip("Mental fatigue. 0=alert, 1=delirious. Affected by sleep debt, stress, monotony.")]
        [Range(0f, 1f)]
        public float mentalFatigue = 0f;

        [Tooltip("Sleep debt in hours. Accumulates when awake, reduces hallucination threshold.")]
        public float sleepDebtHours = 0f;

        [Tooltip("Hours since last sleep. Scenario docs: after 2 weeks, depression/nervous breakdown risk.")]
        public float hoursSinceLastSleep = 0f;

        [Tooltip("Circadian phase. 0-24 representing internal body clock hour.")]
        [Range(0f, 24f)]
        public float circadianPhase = 8f;

        // ===== INJURY & TRAUMA =====
        // Wound system. Blood loss, infection risk, treatment procedures.
        // From IPK: "Krwotoki, zakazenia, wstrzas mozgu, zatrucia pokarmowe, hipoksja"

        [Header("Injury & Trauma")]
        [Tooltip("Blood volume as fraction of normal. Below 0.7 = hypovolemic shock risk.")]
        [Range(0f, 1f)]
        public float bloodVolume = 1.0f;

        [Tooltip("Infection level. Untreated wounds become infected over time.")]
        [Range(0f, 1f)]
        public float infectionLevel = 0f;

        [Tooltip("Pain level. Affects all motor control and cognitive function.")]
        [Range(0f, 1f)]
        public float painLevel = 0f;

        [Tooltip("Concussion severity. Causes visual distortions and disorientation.")]
        [Range(0f, 1f)]
        public float concussionSeverity = 0f;

        [Tooltip("Left arm functionality. 0=disabled, 1=full function. Maps to left controller.")]
        [Range(0f, 1f)]
        public float leftArmFunction = 1.0f;

        [Tooltip("Right arm functionality. 0=disabled, 1=full function. Maps to right controller.")]
        [Range(0f, 1f)]
        public float rightArmFunction = 1.0f;

        [Tooltip("Left leg functionality. Affects movement speed and terrain traversal.")]
        [Range(0f, 1f)]
        public float leftLegFunction = 1.0f;

        [Tooltip("Right leg functionality. Affects movement speed and terrain traversal.")]
        [Range(0f, 1f)]
        public float rightLegFunction = 1.0f;

        [Tooltip("Active wound count. Each wound is a potential infection vector.")]
        public int activeWounds = 0;

        // ===== PSYCHOLOGICAL STATE =====
        // From scenario docs: "Po co najmniej 2 tygodniach moze wywolac depresje"
        // "Calkowity brak hamulcow moralnych i etycznych w celu przezycia"

        [Header("Psychological State")]
        [Tooltip("Morale level. 0=broken, 1=determined. Affected by sleep, food, shelter, success.")]
        [Range(0f, 1f)]
        public float morale = 1.0f;

        [Tooltip("Stress level. High stress + low morale = psychological breakdown risk.")]
        [Range(0f, 1f)]
        public float stressLevel = 0f;

        [Tooltip("Survival instinct. Increases with time and challenges overcome. From scenario: 'Gracz zdolnosci przetrwania powinien zwieksza z czasem'.")]
        [Range(0f, 1f)]
        public float survivalInstinct = 0.1f;

        [Tooltip("Days survived. Drives long-term psychological changes.")]
        public float daysSurvived = 0f;

        // ===== ENVIRONMENTAL EXPOSURE =====

        [Header("Environmental Exposure")]
        [Tooltip("UV exposure level. Sunburn, heat stroke risk in summer without protection.")]
        [Range(0f, 1f)]
        public float uvExposure = 0f;

        [Tooltip("Foot condition. Blisters, fungal infection risk from wet boots/long marches.")]
        [Range(0f, 1f)]
        public float footCondition = 1.0f;

        [Tooltip("Skin condition. Scabies/lice risk from sleeping in abandoned buildings.")]
        [Range(0f, 1f)]
        public float skinCondition = 1.0f;

        // ===== OXYGEN =====

        [Header("Respiration")]
        [Tooltip("Blood oxygen saturation. Normal: 95-100%. Below 90% = hypoxia effects.")]
        [Range(0f, 100f)]
        public float oxygenSaturation = 98f;

        [Tooltip("Respiration rate per minute. Normal: 12-20. Increases with exertion, altitude.")]
        [Range(4f, 40f)]
        public float respirationRate = 16f;

        // ===== COMPUTED PROPERTIES =====

        /// <summary>
        /// Overall physical capability. Drives movement speed, interaction speed, carry capacity.
        /// </summary>
        public float PhysicalCapability =>
            Mathf.Clamp01(
                (1f - physicalFatigue) *
                ((leftLegFunction + rightLegFunction) / 2f) *
                Mathf.Lerp(0.3f, 1f, hydration) *
                Mathf.Lerp(0.5f, 1f, carbohydrateLevel) *
                Mathf.Lerp(0.6f, 1f, bloodVolume)
            );

        /// <summary>
        /// Overall cognitive capability. Drives puzzle solving, map reading, decision quality.
        /// </summary>
        public float CognitiveCapability =>
            Mathf.Clamp01(
                (1f - mentalFatigue) *
                Mathf.Lerp(0.2f, 1f, glucoseLevel) *
                (1f - concussionSeverity * 0.8f) *
                (1f - toxinLevel * 0.6f) *
                Mathf.Lerp(0.5f, 1f, oxygenSaturation / 100f)
            );

        /// <summary>
        /// Tremor intensity. Drives controller vibration/instability.
        /// Primary emersion mechanic from IPK grant.
        /// </summary>
        public float TremorIntensity =>
            Mathf.Clamp01(
                (1f - carbohydrateLevel) * 0.5f +    // Hunger tremor
                (1f - hydration) * 0.3f +              // Dehydration tremor
                stressLevel * 0.2f +                    // Stress tremor
                painLevel * 0.3f +                      // Pain tremor
                hypothermiaStage * 0.25f                // Cold tremor
            );

        /// <summary>
        /// Visual distortion intensity. Drives FOV aberrations, blur, tunnel vision.
        /// </summary>
        public float VisualDistortionIntensity =>
            Mathf.Clamp01(
                toxinLevel * 0.7f +                     // Mushroom/toxin visual effects
                concussionSeverity * 0.5f +             // Head trauma
                (1f - oxygenSaturation / 100f) * 2f +  // Hypoxia
                Mathf.Max(0f, mentalFatigue - 0.7f) * 2f // Extreme fatigue hallucinations
            );

        /// <summary>
        /// Input lag factor. Drives reaction time delay.
        /// From IPK: "celowy lag" after mushroom ingestion.
        /// </summary>
        public float InputLagFactor =>
            Mathf.Clamp01(
                toxinLevel * 0.5f +                     // Toxin-induced lag
                (1f - glucoseLevel) * 0.2f +            // Low glucose sluggishness
                Mathf.Max(0f, physicalFatigue - 0.8f) * 1.5f // Extreme exhaustion
            );

        /// <summary>
        /// Hallucination probability per frame check.
        /// From SPARK: "Phantom sounds, voice distortion, shadow movement"
        /// </summary>
        public float HallucinationProbability =>
            Mathf.Clamp01(
                Mathf.Max(0f, sleepDebtHours - 36f) / 36f +  // Sleep deprivation hallucinations
                toxinLevel * 0.3f +                            // Hallucinogenic toxins
                Mathf.Max(0f, mentalFatigue - 0.85f) * 5f     // Extreme mental fatigue
            );

        /// <summary>
        /// Is the player alive? Death occurs when critical systems fail.
        /// From IPK: "Smierc konczy probe, nie fabule. Gracz umiera jak zwierze
        /// laboratoryjne - cicho, udokumentowane, bez znaczenia."
        /// </summary>
        public bool IsAlive =>
            coreTemperature > 25f &&
            coreTemperature < 42f &&
            bloodVolume > 0.2f &&
            oxygenSaturation > 40f &&
            hydration > 0.02f;

        /// <summary>
        /// Generate noEZUS death report data.
        /// From IPK: "Smierc generuje raport: przyczyna zgonu, czas przezycia, zebrane dane."
        /// </summary>
        public string GetDeathCause()
        {
            if (coreTemperature <= 25f) return "HYPOTHERMIA_TERMINAL";
            if (coreTemperature >= 42f) return "HYPERTHERMIA_TERMINAL";
            if (bloodVolume <= 0.2f) return "HYPOVOLEMIC_SHOCK";
            if (oxygenSaturation <= 40f) return "HYPOXIC_ARREST";
            if (hydration <= 0.02f) return "DEHYDRATION_TERMINAL";
            return "UNKNOWN";
        }
    }

    /// <summary>
    /// Types of toxins from contaminated food/water/mushrooms.
    /// Each type produces different emersion effects.
    /// </summary>
    public enum ToxinType
    {
        None,
        MushroomHallucinogenic,    // FOV distortions, color shifts, time perception changes
        MushroomGastrointestinal,  // Nausea effects, reduced movement, vomiting
        WaterContaminated,         // Dysentery, typhoid - progressive weakening
        FoodPoisoning,             // Vomiting, dehydration, weakness
        Alkaloid                   // From IPK: "alkaloidami grzybow" - lag + perception changes
    }

    /// <summary>
    /// Wound descriptor for the injury tracking system.
    /// </summary>
    [Serializable]
    public class WoundState
    {
        public WoundType type;
        public WoundLocation location;
        public float severity;        // 0-1
        public float infectionRisk;   // 0-1, increases over time if untreated
        public float bleedRate;       // Blood volume loss per second
        public float healingProgress; // 0-1, increases with treatment
        public bool isTreated;
        public float timeSinceInjury; // In game hours
    }

    public enum WoundType
    {
        Laceration,        // Cuts from glass, metal, falls
        Puncture,          // Knife, rebar, fence impalement (from scenario: "przebicie ostrymi metalowymi koncami ogrodzen")
        Fracture,          // Broken bones from falls on slippery terrain
        Sprain,            // Ankle/wrist from wet forest floor (scenario: karst limestone hazards)
        Burn,              // From campfire, cooking accidents
        Blister,           // From long marches with heavy pack
        Frostbite,         // Extremities in winter
        Gunshot,           // From weapon encounters
        Contusion          // Bruises from falls, impacts
    }

    public enum WoundLocation
    {
        Head,
        Torso,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg,
        LeftFoot,
        RightFoot,
        LeftHand,
        RightHand
    }
}
