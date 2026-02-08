// PLAGA '44 VR - EquipmentManager
// Backpack weight effects on stamina, boot quality, clothing layers.
//
// From scenario docs (Gra_scenariusz parts 1, 4):
// Weight limits:
// - "Maksymalny ciezar plecaka 90l do 25 kg"
// - "Maksymalny ciezar plecakow 60l i 20l do 15 kg"
// - 25kg is absolute max ("maksymalny ciezar")
//
// Weight effects (part 4):
// - "Duzy ciezar 25 kg powoduje szybsze spalanie energii fizycznej i psychicznej"
// - "odwodnienie organizmu"
// - "odciski i otarcia stop"
// - "grzybica stop"
// - "przeciazenie kregoslupa i nog"
// - "przeciazenie barkow"
// - "oslabienie serca"
// - "pojawienie sie stanow depresyjnych dlugotralymi marszami powyzej 2 godzin"
//
// Backpack types (part 4):
// - 90L military (US Army Alice, Molle II, DPM British/Dutch, BW)
// - 80L civilian trekking (Campus etc.)
// - 60L medium
// - 20L daypack
//
// Wet weight increase (part 4):
// - "na wskutek deszczow ciezar plecakow moze wzrosnac na wskutek nasiaknecia woda"
// - DPM packs especially vulnerable: "maja tendencje do nasiaknania woda"
// - US Army LC2/Molle III dry faster: "szybko schna z uwagi na nylon i bawelne"
// - Solution: "worki przeprawowe" (dry bags), "pokrowce wodoporne" (rain covers)
//
// March schedule (part 4):
// - "3 do 4 postojow" during the day
// - Breaks of "30 minut do 1 godziny"
// - "poruszanie sie w godzinach rannych od 10-tej rano maksymalnie do 17-tej"
// - During breaks: change socks, spray boots/feet with antifungal
//
// Architecture: Manages equipment state that affects PhysiologyController
// activity parameters (carried weight, boot quality, clothing insulation).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.Survival
{
    using Plaga44.Physiology;

    /// <summary>
    /// Manages equipment load, backpack weight tracking, clothing system,
    /// and their effects on player physiology and stamina.
    /// </summary>
    public class EquipmentManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SeasonManager seasonManager;
        [SerializeField] private PhysiologyController physiologyController;

        [Header("Backpack")]
        [SerializeField] private BackpackType currentBackpack = BackpackType.Military90L;

        [Tooltip("Current total carried weight in kg.")]
        [SerializeField] private float currentWeight = 10f;

        [Tooltip("Dry weight of equipment (without water absorption).")]
        [SerializeField] private float dryWeight = 10f;

        [Tooltip("Water absorption weight added by rain. From scenario: DPM packs absorb water.")]
        [SerializeField] private float wetWeightBonus = 0f;

        [Tooltip("Whether backpack has waterproof cover. From scenario: 'pokrowce wodoporne'.")]
        [SerializeField] private bool hasRainCover = false;

        [Tooltip("Whether using dry bags inside. From scenario: 'worki przeprawowe'.")]
        [SerializeField] private bool hasDryBags = false;

        [Header("Clothing")]
        [SerializeField] private ClothingSetup clothing = new ClothingSetup();

        [Header("Boots")]
        [SerializeField] private BootType currentBoots = BootType.TrekingHigh;

        [Tooltip("Boot condition. Degrades with use. From scenario: needs regular maintenance.")]
        [Range(0f, 1f)]
        [SerializeField] private float bootCondition = 1f;

        [Tooltip("Whether boots are waterproofed. From scenario: 'buty impregnowane'.")]
        [SerializeField] private bool bootsWaterproofed = true;

        [Header("March State")]
        [Tooltip("Hours of continuous marching without break.")]
        [SerializeField] private float continuousMarchHours = 0f;

        [Tooltip("Whether player is currently resting (on a break).")]
        [SerializeField] private bool isOnBreak = false;

        [Tooltip("Number of breaks taken today.")]
        [SerializeField] private int breaksTakenToday = 0;

        [Header("Configuration")]
        [SerializeField] private EquipmentConfig config;

        // Stamina effect tracking
        private float weightFatigueFactor = 0f;
        private float spineStressFactor = 0f;
        private float shoulderStressFactor = 0f;

        // Time tracking
        private float gameHoursPerRealSecond = 0.01f;
        private bool isMoving = false;

        // Events
        public event Action OnOverweightWarning;
        public event Action OnNeedBreak;          // March break needed
        public event Action OnBootsDegraded;
        public event Action<float> OnWeightChanged;
        public event Action<string> OnEquipmentWarning;

        // Public accessors
        public float CurrentWeight => currentWeight;
        public float MaxWeight => GetMaxWeightForBackpack();
        public bool IsOverweight => currentWeight > GetMaxWeightForBackpack();
        public float WeightRatio => currentWeight / GetMaxWeightForBackpack();
        public float BootCondition => bootCondition;
        public float ClothingInsulation => CalculateClothingInsulation();
        public float ContinuousMarchHours => continuousMarchHours;
        public bool IsOnBreak => isOnBreak;

        private void Update()
        {
            float dt = Time.deltaTime;
            float dtGameHours = dt * gameHoursPerRealSecond;

            UpdateWetWeight(dtGameHours);
            UpdateTotalWeight();
            UpdateMarchTracking(dtGameHours);
            UpdateBootCondition(dtGameHours);
            UpdatePhysiologyEffects();
        }

        // ===== WEIGHT MANAGEMENT =====
        // From scenario (part 4): "Maksymalny ciezar plecaka 90l do 25 kg"

        /// <summary>
        /// Maximum weight capacity per backpack type.
        /// From scenario docs part 4.
        /// </summary>
        private float GetMaxWeightForBackpack()
        {
            switch (currentBackpack)
            {
                case BackpackType.Military90L:
                    // From scenario: "Maksymalny ciezar 90l do 25 kg"
                    return config != null ? config.maxWeight90L : 25f;
                case BackpackType.Civilian80L:
                    return config != null ? config.maxWeight80L : 25f;
                case BackpackType.Medium60L:
                    // From scenario: "maksymalny ciezar plecakow 60l do 15 kg"
                    return config != null ? config.maxWeight60L : 15f;
                case BackpackType.Daypack20L:
                    // From scenario: "plecakow 20l do 15 kg"
                    return config != null ? config.maxWeight20L : 15f;
                default:
                    return 25f;
            }
        }

        // ===== WET WEIGHT =====
        // From scenario (part 4):
        // - "na wskutek deszczow ciezar plecakow moze wzrosnac"
        // - DPM packs: "maja tendencje do nasiakania woda, szczegolnie klapa gorna
        //   i boczne kieszenie"
        // - US Army: "szybko schna z uwagi na nylon i bawelne"
        // - Solution: rain covers + dry bags

        private void UpdateWetWeight(float dtGameHours)
        {
            if (seasonManager == null) return;

            float precipitation = seasonManager.CurrentPrecipitation;

            if (precipitation > 0.05f && !hasRainCover)
            {
                // Backpack absorbs water
                float absorptionRate = GetWaterAbsorptionRate();
                float maxWetBonus = config != null ? config.maxWetWeightBonus : 3f;

                wetWeightBonus = Mathf.Min(maxWetBonus,
                    wetWeightBonus + absorptionRate * precipitation * dtGameHours);
            }
            else if (precipitation <= 0.05f)
            {
                // Drying out
                float dryRate = GetDryingRate();
                wetWeightBonus = Mathf.Max(0f, wetWeightBonus - dryRate * dtGameHours);
            }
        }

        /// <summary>
        /// Water absorption rate depends on backpack material.
        /// From scenario: DPM absorbs more, US Army nylon dries faster.
        /// </summary>
        private float GetWaterAbsorptionRate()
        {
            float baseRate = config != null ? config.waterAbsorptionRate : 0.5f;

            switch (currentBackpack)
            {
                case BackpackType.Military90L:
                    // DPM-style: "maja tendencje do nasiakania woda"
                    return baseRate * 1.3f;
                case BackpackType.Civilian80L:
                    // Civilian trekking packs: "odporne na wode" but need dry bags
                    return baseRate * 0.7f;
                default:
                    return baseRate;
            }

            // Dry bags reduce absorption
            // if (hasDryBags) return rate * 0.3f; -- contents protected but pack still absorbs
        }

        private float GetDryingRate()
        {
            float baseRate = config != null ? config.dryingRate : 0.2f;

            // US Army packs dry faster
            if (currentBackpack == BackpackType.Military90L)
            {
                // From scenario: "szybko schna z uwagi na nylon i bawelne"
                baseRate *= 1.5f;
            }

            // Near fire dries faster (handled externally by ShelterSystem)
            return baseRate;
        }

        private void UpdateTotalWeight()
        {
            float previousWeight = currentWeight;
            currentWeight = dryWeight + wetWeightBonus;

            if (Mathf.Abs(currentWeight - previousWeight) > 0.1f)
            {
                OnWeightChanged?.Invoke(currentWeight);
            }

            if (currentWeight > GetMaxWeightForBackpack())
            {
                OnOverweightWarning?.Invoke();
            }
        }

        // ===== MARCH TRACKING =====
        // From scenario (part 4):
        // - "w ciagu marszu trzeba wykonac 2 przerwy po godzinie"
        // - "w ciagu dnia co najmniej 3 do 4 postojow"
        // - Breaks: "30 minut do 1 godziny"
        // - During breaks: "wymiana skarpet, spryskanie butow i stop"
        // - "poruszanie sie w godzinach rannych od 10-tej rano maksymalnie do 17-tej"
        // - Marches without break: "moze spowodowac wyziebienie organizmu i wyczerpanie"

        private void UpdateMarchTracking(float dtGameHours)
        {
            if (isMoving && !isOnBreak)
            {
                continuousMarchHours += dtGameHours;

                // From scenario: need break every 2-3 hours
                float breakInterval = config != null ? config.recommendedBreakIntervalHours : 2.5f;

                if (continuousMarchHours > breakInterval)
                {
                    OnNeedBreak?.Invoke();
                }

                // Extended march effects
                // From scenario: "dlugotrawly marsz bez przerw okolo 3 przerw co najmniej
                // 40 min do 60 min moze spowodowac wyziebienie organizmu i wycienczenie"
                if (continuousMarchHours > breakInterval * 1.5f)
                {
                    OnEquipmentWarning?.Invoke("MARCH_EXHAUSTION_RISK");
                }
            }
        }

        // ===== BOOT CONDITION =====
        // From scenario (part 4):
        // - "odciski i otarcia stop" from marching
        // - "grzybica stop" from wet conditions
        // - "spryskania butow oraz stop srodkami przeciwgrzybicy stop" during breaks
        // - "buty wysokie trekingowe impregnowane 2 pary"

        private void UpdateBootCondition(float dtGameHours)
        {
            if (!isMoving) return;

            // Boot degradation from use
            float degradeRate = config != null ? config.bootDegradationRate : 0.001f;
            float weightFactor = currentWeight / 25f; // Heavier load = faster wear
            float terrainFactor = 1f; // Could be modified by terrain system

            bootCondition = Mathf.Max(0f, bootCondition - degradeRate * weightFactor * terrainFactor * dtGameHours);

            // Wet conditions accelerate degradation
            if (seasonManager != null && seasonManager.CurrentPrecipitation > 0.1f && !bootsWaterproofed)
            {
                bootCondition = Mathf.Max(0f, bootCondition - degradeRate * 0.5f * dtGameHours);
            }

            if (bootCondition < 0.3f)
            {
                OnBootsDegraded?.Invoke();
            }
        }

        // ===== CLOTHING INSULATION =====
        // From scenario (part 4):
        // - "korzystanie z odziezy termoaktywnej na cebule 2 do 3 warstw"
        // - "kurtki i spodnie z membrana goratex"
        // - Winter: thermal layers + goretex outer
        // - Summer: light clothing only

        private float CalculateClothingInsulation()
        {
            float insulation = 0f;

            // Base layer (thermoactive underwear)
            if (clothing.hasThermalBase)
            {
                insulation += config != null ? config.thermalBaseInsulation : 2f;
            }

            // Mid layer (fleece/sweater)
            insulation += clothing.midLayers * (config != null ? config.midLayerInsulation : 1.5f);

            // Outer layer (goretex jacket/pants)
            if (clothing.hasGoretexOuter)
            {
                insulation += config != null ? config.goretexInsulation : 3f;
            }

            // Rain poncho (from scenario: "ponczo przeciwdeszczowe 2 sztuki")
            if (clothing.hasRainPoncho)
            {
                insulation += config != null ? config.rainPonchoInsulation : 1f;
            }

            // Hat/balaclava (from scenario: winter face protection)
            if (clothing.hasWinterHat)
            {
                insulation += config != null ? config.winterHatInsulation : 0.5f;
            }

            // Gloves
            if (clothing.hasGloves)
            {
                insulation += config != null ? config.glovesInsulation : 0.3f;
            }

            return insulation;
        }

        // ===== PHYSIOLOGY INTEGRATION =====

        private void UpdatePhysiologyEffects()
        {
            if (physiologyController == null) return;

            // Weight effects on stamina
            // From scenario: "Duzy ciezar powoduje szybsze spalanie energii"
            float maxWeight = GetMaxWeightForBackpack();
            weightFatigueFactor = Mathf.InverseLerp(0f, maxWeight, currentWeight);

            // Spine/shoulder stress from overloading
            // From scenario: "przeciazenie kregoslupa i nog", "przeciazenie barkow"
            if (currentWeight > maxWeight * 0.8f)
            {
                spineStressFactor = Mathf.InverseLerp(maxWeight * 0.8f, maxWeight * 1.2f, currentWeight);
                shoulderStressFactor = spineStressFactor * 0.8f;
            }
            else
            {
                spineStressFactor = 0f;
                shoulderStressFactor = 0f;
            }

            // Boot quality affects foot protection in PhysiologyController
            float bootQuality = Mathf.Lerp(0.3f, 1f, bootCondition);

            // Calculate clothing insulation for thermoregulation
            float insulation = CalculateClothingInsulation();

            // Push to PhysiologyController via SetActivity
            // Note: movementSpeed and terrain are set externally by player controller
            // We augment the carriedWeight parameter
            // The physiologyController.SetActivity() expects weight in kg
        }

        // ===== PUBLIC API =====

        /// <summary>
        /// Add weight to the backpack (items picked up).
        /// </summary>
        public void AddWeight(float kg)
        {
            dryWeight += kg;
            UpdateTotalWeight();
        }

        /// <summary>
        /// Remove weight from the backpack (items dropped/consumed).
        /// </summary>
        public void RemoveWeight(float kg)
        {
            dryWeight = Mathf.Max(0f, dryWeight - kg);
            UpdateTotalWeight();
        }

        /// <summary>
        /// Set movement state (called by player movement system).
        /// </summary>
        public void SetMoving(bool moving)
        {
            isMoving = moving;
            if (!moving)
            {
                // Stopped moving - could be a rest/break
            }
        }

        /// <summary>
        /// Start a march break.
        /// From scenario: should include sock change, boot spray, meal.
        /// "w celu wymiany skarpet, spryskania butow oraz stop"
        /// </summary>
        public void StartBreak()
        {
            isOnBreak = true;
            breaksTakenToday++;
            continuousMarchHours = 0f;
        }

        /// <summary>
        /// End the current break and resume marching.
        /// </summary>
        public void EndBreak()
        {
            isOnBreak = false;
        }

        /// <summary>
        /// Maintain boots during a break.
        /// From scenario: "spryskania butow oraz stop srodkami przeciwgrzybicy stop"
        /// </summary>
        public void MaintainBoots()
        {
            bootCondition = Mathf.Min(1f, bootCondition + 0.1f);
        }

        /// <summary>
        /// Apply waterproofing to boots.
        /// From scenario: "buty impregnowane"
        /// </summary>
        public void WaterproofBoots()
        {
            bootsWaterproofed = true;
        }

        /// <summary>
        /// Change clothing layer setup.
        /// From scenario: "korzystanie z odziezy termoaktywnej na cebule 2 do 3 warstw"
        /// </summary>
        public void SetClothing(ClothingSetup newSetup)
        {
            clothing = newSetup;
        }

        /// <summary>
        /// Change backpack type.
        /// From scenario: military 90L, civilian 80L, medium 60L, daypack 20L.
        /// </summary>
        public void SetBackpack(BackpackType type)
        {
            currentBackpack = type;
            UpdateTotalWeight();
        }

        /// <summary>
        /// Returns the total weight including equipment for physiology.
        /// </summary>
        public float GetTotalCarriedWeight()
        {
            return currentWeight;
        }
    }

    // ===== ENUMS AND DATA =====

    public enum BackpackType
    {
        /// <summary>
        /// From scenario: "plecak 60 do 90 litrow", US Army Alice/Molle, DPM Bergen.
        /// Max weight: 25 kg.
        /// </summary>
        Military90L,

        /// <summary>
        /// From scenario: "plecaki cywilne 80l do 90l", Campus etc.
        /// "posiadajace stelaze plastikowe lub ze stopow aluminium"
        /// Max weight: 25 kg.
        /// </summary>
        Civilian80L,

        /// <summary>
        /// From scenario: "plecakow 60l do 15 kg".
        /// Medium pack for lighter loads.
        /// </summary>
        Medium60L,

        /// <summary>
        /// From scenario: "plecakow 20l do 15 kg".
        /// Daypack for short-range operations.
        /// </summary>
        Daypack20L
    }

    public enum BootType
    {
        /// <summary>
        /// From scenario: "buty wysokie trekingowe impregnowane 2 pary"
        /// Best foot protection and waterproofing.
        /// </summary>
        TrekingHigh,

        /// <summary>
        /// Standard military boots.
        /// From scenario: "buty wojskowe" referenced throughout.
        /// </summary>
        MilitaryStandard,

        /// <summary>
        /// Civilian boots found during scavenging.
        /// From scenario: "buty na przebranie 1 para"
        /// </summary>
        CivilianFound,

        /// <summary>
        /// Damaged/worn boots. Reduced protection.
        /// </summary>
        Damaged
    }

    [Serializable]
    public class ClothingSetup
    {
        [Tooltip("Thermal base layer. From scenario: 'bielizna termoaktywna 2 komplety'.")]
        public bool hasThermalBase = true;

        [Tooltip("Number of mid layers (fleece/sweaters). From scenario: 'swetry wojskowe 2 sztuki'.")]
        [Range(0, 3)]
        public int midLayers = 1;

        [Tooltip("Goretex outer layer. From scenario: 'kurtki i spodnie z membrana goratex'.")]
        public bool hasGoretexOuter = false;

        [Tooltip("Rain poncho. From scenario: 'ponczo przeciwdeszczowe 2 sztuki US Army'.")]
        public bool hasRainPoncho = false;

        [Tooltip("Winter hat/balaclava. From scenario: 'maski na twarz chronice przed odmrozeniami'.")]
        public bool hasWinterHat = false;

        [Tooltip("Gloves. From scenario: 'rekawice robocze cywilne 2 komplety'.")]
        public bool hasGloves = false;

        [Tooltip("Anti-frostbite cream applied. From scenario: 'kremy przeciw odmrozeniom'.")]
        public bool hasAntiFrostbiteCream = false;

        [Tooltip("Sunscreen applied. From scenario: 'kremy przeciwsloneczne (na lato)'.")]
        public bool hasSunscreen = false;

        [Tooltip("Goggles/sunglasses. From scenario: 'okulary balistyczne lub przeciwsloneczne'.")]
        public bool hasGoggles = false;

        [Tooltip("Anti-mosquito spray. From scenario: 'spray na komary i kleszcze'.")]
        public bool hasAntiMosquito = false;

        [Tooltip("Boot waterproof gaiters. From scenario: 'stuptutow nakladanych na buty'.")]
        public bool hasBootGaiters = false;
    }

    // ===== EQUIPMENT CONFIG =====

    /// <summary>
    /// Tunable configuration for equipment parameters.
    /// Create assets: Assets > Create > Plaga44 > Equipment Config
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentConfig", menuName = "Plaga44/Equipment Config")]
    public class EquipmentConfig : ScriptableObject
    {
        [Header("Weight Limits (from scenario docs)")]
        [Tooltip("Max weight for 90L packs. From scenario: 25 kg.")]
        public float maxWeight90L = 25f;

        [Tooltip("Max weight for 80L civilian packs.")]
        public float maxWeight80L = 25f;

        [Tooltip("Max weight for 60L packs. From scenario: 15 kg.")]
        public float maxWeight60L = 15f;

        [Tooltip("Max weight for 20L daypacks. From scenario: 15 kg.")]
        public float maxWeight20L = 15f;

        [Header("Wet Weight")]
        [Tooltip("Maximum extra weight from water absorption.")]
        public float maxWetWeightBonus = 3f;

        [Tooltip("Rate of water absorption per game hour at full rain.")]
        public float waterAbsorptionRate = 0.5f;

        [Tooltip("Drying rate per game hour when not raining.")]
        public float dryingRate = 0.2f;

        [Header("March Breaks (from scenario docs)")]
        [Tooltip("Recommended break interval. From scenario: 2-3 hours.")]
        public float recommendedBreakIntervalHours = 2.5f;

        [Tooltip("Recommended break duration. From scenario: 40-60 minutes.")]
        public float recommendedBreakDurationHours = 0.75f;

        [Header("Boot Degradation")]
        [Tooltip("Boot condition loss per game hour while marching.")]
        public float bootDegradationRate = 0.001f;

        [Header("Clothing Insulation Values")]
        [Tooltip("Insulation from thermal base layer (thermoactive underwear).")]
        public float thermalBaseInsulation = 2f;

        [Tooltip("Insulation per mid layer (fleece/sweater).")]
        public float midLayerInsulation = 1.5f;

        [Tooltip("Insulation from goretex outer layer.")]
        public float goretexInsulation = 3f;

        [Tooltip("Insulation from rain poncho.")]
        public float rainPonchoInsulation = 1f;

        [Tooltip("Insulation from winter hat/balaclava.")]
        public float winterHatInsulation = 0.5f;

        [Tooltip("Insulation from gloves.")]
        public float glovesInsulation = 0.3f;
    }
}
