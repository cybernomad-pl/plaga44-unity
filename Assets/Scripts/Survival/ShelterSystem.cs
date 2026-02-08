// PLAGA '44 VR - ShelterSystem
// Shelter types, protection levels per season, fire management.
//
// From scenario docs (Gra_scenariusz parts 2, 3, 4):
//
// Shelter types from scenarios:
// - "spanie pod zwalonymi drzewami ktore posiadaja liscie, najlepiej jodly lub swierki"
// - "budowac z zwalonych galezi drzew szalasy" (branch lean-to shelters)
// - "szukac miejsc w lasach mieszanych lub iglastych"
// - Spring rain shelter: "niewielki trojkat pomiedzy drzewami (40-70 cm odleglosc),
//   aby korny drzew laczyly sie za soba tworzac naturalny dach"
// - Autumn: "szukac zwalonych duzych drzew i pod nimi spac w rowach"
// - Abandoned buildings: "pustostany" - risk of criminals, lice, scabies
// - Underground garages: "garaze podziemne pod blokami lub biurowcami"
//
// Fire:
// - "palniki spirytusowe" (spirit burners) for cooking
// - "podpalka do grilla w plynie" (gel fire starter)
// - "podpalka do grilla w kostkach" (fire starter blocks)
// - "Komary odstrasza rowniez dym z ogniska" (smoke repels mosquitoes)
// - Fire used "w celu zagotowania wody do picia" and food prep
//
// Shelter selection advice:
// - "zimą szukac miejsc w lasach mieszanych lub iglastych"
// - "szukanie miejsca do noclegu w lesie od 16-tej godziny maksymalnie"
// - From scenario part 4: winter campsite search from 16:00 at latest
// - "nocne poruszanie sie po lesie utrudnione" - settle before dark
//
// Architecture: Manages shelter state, fire state, and their effects
// on PhysiologyController (shelter bonus, fire heat, protection level).

using System;
using UnityEngine;

namespace Plaga44.Survival
{
    using Plaga44.Physiology;

    /// <summary>
    /// Manages shelter construction/discovery, fire building/maintenance,
    /// and their protective effects against weather conditions.
    /// Pushes shelter/fire state to PhysiologyController.
    /// </summary>
    public class ShelterSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SeasonManager seasonManager;
        [SerializeField] private PhysiologyController physiologyController;

        [Header("Current Shelter")]
        [SerializeField] private ShelterType currentShelter = ShelterType.None;
        [SerializeField] private float shelterQuality = 0f;
        [SerializeField] private float shelterIntegrity = 0f;

        [Header("Fire State")]
        [SerializeField] private bool hasActiveFire = false;
        [SerializeField] private FireType currentFireType = FireType.None;
        [SerializeField] private float fireHeatOutput = 0f;
        [SerializeField] private float fireFuelRemaining = 0f;
        [SerializeField] private float fireBurnRate = 1f;

        [Header("Fire Supplies")]
        [SerializeField] private int spiritBurnersAvailable = 2;
        [SerializeField] private float gelFuelLiters = 1.5f;
        [SerializeField] private int fireStarterBlocks = 8;
        [SerializeField] private int matchBoxes = 3;
        [SerializeField] private int lighters = 4;

        [Header("Protection Levels (Read Only)")]
        [SerializeField] private float rainProtection = 0f;
        [SerializeField] private float windProtection = 0f;
        [SerializeField] private float coldProtection = 0f;
        [SerializeField] private float concealmentLevel = 0f;
        [SerializeField] private float sleepQualityBonus = 0f;

        [Header("Configuration")]
        [SerializeField] private ShelterConfig config;

        // Time tracking
        private float gameHoursPerRealSecond = 0.01f;
        private float timeInShelter = 0f;

        // Events
        public event Action<ShelterType> OnShelterChanged;
        public event Action<bool> OnFireStateChanged;
        public event Action OnFireFuelLow;
        public event Action OnFireExtinguished;
        public event Action<string> OnShelterWarning;

        // Public accessors
        public ShelterType CurrentShelter => currentShelter;
        public bool IsInShelter => currentShelter != ShelterType.None;
        public bool HasFire => hasActiveFire;
        public float FireHeat => fireHeatOutput;
        public float RainProtection => rainProtection;
        public float WindProtection => windProtection;
        public float ColdProtection => coldProtection;
        public float ConcealmentLevel => concealmentLevel;
        public float SleepQualityBonus => sleepQualityBonus;

        private void Update()
        {
            float dt = Time.deltaTime;
            float dtGameHours = dt * gameHoursPerRealSecond;

            UpdateFire(dtGameHours);
            UpdateShelterProtection();
            UpdateShelterDegradation(dtGameHours);
            PushToPhysiology();

            if (IsInShelter)
            {
                timeInShelter += dtGameHours;
            }
        }

        // ===== FIRE MANAGEMENT =====
        // From scenario (part 1, 4):
        // - "palniki spirytusowe 2 sztuki" (spirit burners)
        // - "podpalka do grilla w plynie 1 duza" (1.5L gel fuel)
        // - "podpalka do grilla w kostkach 2 opakowania" (starter blocks)
        // - "2 zapalniczki benzynowe, 2 zapalniczki na gaz, zapalki 3 opakowania"
        // - Spirit burner + gel + block combo: "Zalecane polaczenie palnikow
        //   spirytusowych podpalki zelowej z 1 kostka podpalki bialej - jesienia,
        //   zima, wiosna - w celu przyspieszenia gotowania wody"
        // - Campfire for warmth + water boiling + cooking
        // - "Komary odstrasza rowniez dym z ogniska"

        private void UpdateFire(float dtGameHours)
        {
            if (!hasActiveFire) return;

            // Consume fuel
            fireFuelRemaining -= fireBurnRate * dtGameHours;

            if (fireFuelRemaining <= 0f)
            {
                ExtinguishFire();
                return;
            }

            // Low fuel warning
            if (fireFuelRemaining < 0.5f)
            {
                OnFireFuelLow?.Invoke();
            }

            // Weather effects on fire
            if (seasonManager != null)
            {
                // Wind can increase burn rate
                float wind = seasonManager.CurrentWindSpeed;
                fireBurnRate = 1f + wind * 0.05f;

                // Rain can extinguish unprotected fires
                float rain = seasonManager.CurrentPrecipitation;
                if (rain > 0.5f && currentShelter == ShelterType.None)
                {
                    // Fire in rain without shelter has chance of going out
                    float extinguishChance = rain * 0.01f * dtGameHours;
                    if (UnityEngine.Random.value < extinguishChance)
                    {
                        ExtinguishFire();
                        OnShelterWarning?.Invoke("FIRE_EXTINGUISHED_RAIN");
                        return;
                    }
                }
                else if (rain > 0.3f && currentShelter == ShelterType.None)
                {
                    // Reduced heat output in light rain
                    fireHeatOutput *= 0.7f;
                }
            }

            // Calculate heat output based on fire type
            UpdateFireHeatOutput();
        }

        private void UpdateFireHeatOutput()
        {
            switch (currentFireType)
            {
                case FireType.SpiritBurner:
                    // Small, controlled flame - for cooking primarily
                    // From scenario: used with gel fuel for water boiling
                    fireHeatOutput = config != null ? config.spiritBurnerHeat : 50f;
                    break;
                case FireType.SmallCampfire:
                    // Small campfire from gathered wood
                    fireHeatOutput = config != null ? config.smallCampfireHeat : 200f;
                    break;
                case FireType.LargeCampfire:
                    // Large campfire - significant warmth but visible/detectable
                    fireHeatOutput = config != null ? config.largeCampfireHeat : 500f;
                    break;
                case FireType.FurnitureFire:
                    // From scenario: "zniszczone meble, plyty wiorowe ktore
                    // szybko podnosza temperature ogniska"
                    fireHeatOutput = config != null ? config.furnitureFireHeat : 400f;
                    break;
                default:
                    fireHeatOutput = 0f;
                    break;
            }
        }

        // ===== SHELTER PROTECTION =====
        // Protection levels depend on shelter type and season.
        // From scenario docs: different shelter strategies per season.

        private void UpdateShelterProtection()
        {
            if (currentShelter == ShelterType.None)
            {
                rainProtection = 0f;
                windProtection = 0f;
                coldProtection = 0f;
                concealmentLevel = 0f;
                sleepQualityBonus = 0f;
                return;
            }

            var protection = GetShelterProtection(currentShelter);
            rainProtection = protection.rain * shelterIntegrity;
            windProtection = protection.wind * shelterIntegrity;
            coldProtection = protection.cold * shelterIntegrity;
            concealmentLevel = protection.concealment;
            sleepQualityBonus = protection.sleepBonus * shelterIntegrity;

            // Fire adds warmth to any shelter
            if (hasActiveFire)
            {
                coldProtection = Mathf.Min(1f, coldProtection + fireHeatOutput * 0.001f);
                sleepQualityBonus = Mathf.Min(1f, sleepQualityBonus + 0.1f);
            }

            // Seasonal shelter effectiveness
            if (seasonManager != null)
            {
                ApplySeasonalShelterModifiers(seasonManager.CurrentSeason);
            }
        }

        /// <summary>
        /// Base protection values for each shelter type.
        /// From scenario docs: detailed descriptions of shelter construction.
        /// </summary>
        private ShelterProtectionValues GetShelterProtection(ShelterType type)
        {
            switch (type)
            {
                case ShelterType.FallenTree:
                    // From scenario: "spac pod zwalonymi drzewami, chronia przed
                    // deszczem i sniegiem i maskuja nasza obecnosc"
                    return new ShelterProtectionValues
                    {
                        rain = 0.5f,
                        wind = 0.4f,
                        cold = 0.2f,
                        concealment = 0.8f, // Good concealment
                        sleepBonus = 0.3f
                    };

                case ShelterType.BranchLeanTo:
                    // From scenario: "budowac z zwalonych galezi drzew szalasy"
                    // Using "toporki, pil, sznurow"
                    return new ShelterProtectionValues
                    {
                        rain = 0.6f,
                        wind = 0.5f,
                        cold = 0.3f,
                        concealment = 0.7f,
                        sleepBonus = 0.4f
                    };

                case ShelterType.TreeTriangle:
                    // From scenario (spring): "niewielki trojkat pomiedzy drzewami
                    // 40-70cm, aby korny drzew laczyly sie tworzac naturalny dach"
                    return new ShelterProtectionValues
                    {
                        rain = 0.7f,
                        wind = 0.3f,
                        cold = 0.25f,
                        concealment = 0.6f,
                        sleepBonus = 0.35f
                    };

                case ShelterType.DugoutUnderTree:
                    // From scenario (autumn): "pod duzymi drzewami w rowach
                    // znajdujacych sie pod nimi lub wykopanymi przez siebie"
                    return new ShelterProtectionValues
                    {
                        rain = 0.7f,
                        wind = 0.6f,
                        cold = 0.4f,
                        concealment = 0.85f, // Very hidden
                        sleepBonus = 0.4f
                    };

                case ShelterType.AbandonedBuilding:
                    // From scenario: "pustostany" - good protection BUT
                    // "moga przebywac w nich kryminalisti, zlodizeje, mordercy,
                    // alkoholicy i narkomani"
                    // Risk of "wszawica, swierzb, grzybica nog"
                    return new ShelterProtectionValues
                    {
                        rain = 0.95f,
                        wind = 0.9f,
                        cold = 0.6f,
                        concealment = 0.3f, // Not concealed - others can find you
                        sleepBonus = 0.5f   // Reduced by danger/stress
                    };

                case ShelterType.UndergroundGarage:
                    // From scenario: "garaze podziemne pod blokami lub biurowcami"
                    // Good for shelter but also vehicle searching
                    return new ShelterProtectionValues
                    {
                        rain = 1.0f,
                        wind = 0.95f,
                        cold = 0.5f,
                        concealment = 0.5f,
                        sleepBonus = 0.4f
                    };

                case ShelterType.Apartment:
                    // From scenario: residential apartments, upper floors preferred
                    // "bloki od 2 pietra" - lower floors already raided
                    return new ShelterProtectionValues
                    {
                        rain = 1.0f,
                        wind = 1.0f,
                        cold = 0.7f,
                        concealment = 0.4f,
                        sleepBonus = 0.6f
                    };

                default:
                    return new ShelterProtectionValues();
            }
        }

        /// <summary>
        /// Modify shelter effectiveness based on season.
        /// From scenario: different seasons have different shelter needs.
        /// </summary>
        private void ApplySeasonalShelterModifiers(Season season)
        {
            switch (season)
            {
                case Season.Winter:
                    // Winter: shelter is critical, but even good shelter may not prevent cold
                    // From scenario: "najtrudniejsza pora roku"
                    if (currentShelter != ShelterType.Apartment &&
                        currentShelter != ShelterType.AbandonedBuilding)
                    {
                        coldProtection *= 0.6f; // Outdoor shelters less effective in winter
                    }
                    break;

                case Season.Summer:
                    // Summer: shelter from sun more important than cold
                    // Mosquito protection if has fire
                    coldProtection = 1f; // No cold issue in summer
                    break;

                case Season.Autumn:
                    // From scenario: rain is primary concern
                    // "deszcze powoduja obnizenie temperatury"
                    if (rainProtection < 0.5f)
                    {
                        coldProtection *= 0.7f; // Getting wet = getting cold
                    }
                    break;

                case Season.Spring:
                    // Rain and temperature fluctuations
                    break;
            }
        }

        // ===== SHELTER DEGRADATION =====
        // Natural shelters degrade over time and in bad weather.

        private void UpdateShelterDegradation(float dtGameHours)
        {
            if (currentShelter == ShelterType.None) return;

            // Built structures degrade
            float degradeRate = config != null ? config.shelterDegradationRate : 0.005f;

            // Weather accelerates degradation
            if (seasonManager != null)
            {
                if (seasonManager.CurrentWeather == WeatherCondition.Storm)
                {
                    degradeRate *= 3f;
                }
                else if (seasonManager.CurrentPrecipitation > 0.5f)
                {
                    degradeRate *= 1.5f;
                }

                if (seasonManager.CurrentWindSpeed > 10f)
                {
                    degradeRate *= 1.5f;
                }
            }

            // Built shelters degrade; permanent structures don't
            if (currentShelter == ShelterType.BranchLeanTo ||
                currentShelter == ShelterType.TreeTriangle ||
                currentShelter == ShelterType.DugoutUnderTree)
            {
                shelterIntegrity = Mathf.Max(0f, shelterIntegrity - degradeRate * dtGameHours);

                if (shelterIntegrity < 0.3f)
                {
                    OnShelterWarning?.Invoke("SHELTER_DAMAGED");
                }

                if (shelterIntegrity <= 0f)
                {
                    LeaveShelter();
                    OnShelterWarning?.Invoke("SHELTER_COLLAPSED");
                }
            }
        }

        // ===== PHYSIOLOGY INTEGRATION =====

        private void PushToPhysiology()
        {
            if (physiologyController == null) return;

            // Override environment data with shelter effects
            bool isInShelter = currentShelter != ShelterType.None;
            bool nearFire = hasActiveFire;

            if (seasonManager != null)
            {
                // Apply shelter-modified environment to physiology
                // Temperature is modified by shelter and fire
                float shelterTempBonus = coldProtection * 5f;
                float windReduction = seasonManager.CurrentWindSpeed * (1f - windProtection);
                float rainReduction = seasonManager.CurrentPrecipitation * (1f - rainProtection);

                physiologyController.SetEnvironment(
                    seasonManager.CurrentTemperature + shelterTempBonus,
                    windReduction,
                    rainReduction,
                    isInShelter,
                    nearFire,
                    fireHeatOutput
                );
            }
        }

        // ===== PUBLIC API =====

        /// <summary>
        /// Enter/construct a shelter.
        /// From scenario: "szukanie miejsca do noclegu w lesie od 16-tej godziny"
        /// </summary>
        public void EnterShelter(ShelterType type, float quality = 1f)
        {
            currentShelter = type;
            shelterQuality = quality;
            shelterIntegrity = quality;
            timeInShelter = 0f;

            OnShelterChanged?.Invoke(type);

            // Warn about risks of abandoned buildings
            if (type == ShelterType.AbandonedBuilding)
            {
                // From scenario: "moga przebywac w nich kryminalisti"
                // "zagrożenie zachorowaniem na wszawice, swierzb, grzybice nog"
                OnShelterWarning?.Invoke("BUILDING_DANGER_RISK");
            }
        }

        /// <summary>
        /// Leave current shelter.
        /// </summary>
        public void LeaveShelter()
        {
            ShelterType previous = currentShelter;
            currentShelter = ShelterType.None;
            shelterQuality = 0f;
            shelterIntegrity = 0f;
            timeInShelter = 0f;

            if (previous != ShelterType.None)
            {
                OnShelterChanged?.Invoke(ShelterType.None);
            }
        }

        /// <summary>
        /// Repair/improve the current shelter.
        /// From scenario: using "toporki, pily, sznury" to reinforce.
        /// </summary>
        public void RepairShelter(float amount = 0.2f)
        {
            if (currentShelter == ShelterType.None) return;

            shelterIntegrity = Mathf.Min(1f, shelterIntegrity + amount);
        }

        /// <summary>
        /// Start a fire.
        /// Requires ignition source (matches/lighter) and fuel.
        /// From scenario: spirit burners + gel fuel combo recommended.
        /// </summary>
        public bool StartFire(FireType type)
        {
            // Check for ignition source
            if (matchBoxes <= 0 && lighters <= 0)
            {
                OnShelterWarning?.Invoke("NO_IGNITION_SOURCE");
                return false;
            }

            // Check fuel availability
            switch (type)
            {
                case FireType.SpiritBurner:
                    if (spiritBurnersAvailable <= 0 || gelFuelLiters <= 0f)
                    {
                        OnShelterWarning?.Invoke("NO_SPIRIT_BURNER_FUEL");
                        return false;
                    }
                    fireFuelRemaining = config != null ? config.spiritBurnerFuelDuration : 3f;
                    break;

                case FireType.SmallCampfire:
                    if (fireStarterBlocks <= 0)
                    {
                        OnShelterWarning?.Invoke("NO_FIRE_STARTER");
                        return false;
                    }
                    fireStarterBlocks--;
                    fireFuelRemaining = config != null ? config.smallCampfireDuration : 4f;
                    break;

                case FireType.LargeCampfire:
                    if (fireStarterBlocks < 2)
                    {
                        OnShelterWarning?.Invoke("INSUFFICIENT_FIRE_STARTER");
                        return false;
                    }
                    fireStarterBlocks -= 2;
                    fireFuelRemaining = config != null ? config.largeCampfireDuration : 8f;
                    break;

                case FireType.FurnitureFire:
                    // From scenario: "zniszczone meble, plyty wiorowe"
                    // Fuel is the furniture itself, just needs ignition
                    fireFuelRemaining = config != null ? config.furnitureFireDuration : 6f;
                    break;

                default:
                    return false;
            }

            // Consume ignition source
            if (matchBoxes > 0)
            {
                // Each matchbox has ~40 matches, consume 1 match (we track boxes for simplicity)
                // Simplification: matches are consumed at box level
            }

            // Weather check - harder to start fire in wind/rain
            if (seasonManager != null)
            {
                float startDifficulty = seasonManager.CurrentWindSpeed * 0.05f +
                                       seasonManager.CurrentPrecipitation * 0.3f;

                if (UnityEngine.Random.value < startDifficulty && currentShelter == ShelterType.None)
                {
                    OnShelterWarning?.Invoke("FIRE_START_FAILED_WEATHER");
                    return false;
                }
            }

            currentFireType = type;
            hasActiveFire = true;
            fireBurnRate = 1f;

            OnFireStateChanged?.Invoke(true);
            return true;
        }

        /// <summary>
        /// Extinguish the current fire.
        /// </summary>
        public void ExtinguishFire()
        {
            hasActiveFire = false;
            currentFireType = FireType.None;
            fireHeatOutput = 0f;
            fireFuelRemaining = 0f;

            OnFireStateChanged?.Invoke(false);
            OnFireExtinguished?.Invoke();
        }

        /// <summary>
        /// Add fuel to keep the fire going.
        /// From scenario: wood from "chrust, kory brzozy, suche galezi"
        /// or "zniszczone meble, plyty wiorowe" in urban areas.
        /// </summary>
        public void AddFireFuel(float hours)
        {
            if (!hasActiveFire) return;
            fireFuelRemaining += hours;
        }

        /// <summary>
        /// Check if fire can be used to boil water (for purification).
        /// From scenario: "zagotowanie wody do picia"
        /// </summary>
        public bool CanBoilWater()
        {
            return hasActiveFire && fireHeatOutput > 40f;
        }

        /// <summary>
        /// Check if fire can be used for cooking.
        /// From scenario: "przygotowanie zywnosci" on campfire.
        /// </summary>
        public bool CanCook()
        {
            return hasActiveFire && fireHeatOutput > 40f;
        }
    }

    // ===== ENUMS AND DATA =====

    public enum ShelterType
    {
        /// <summary>No shelter - exposed to elements.</summary>
        None,

        /// <summary>
        /// From scenario: "spac pod zwalonymi drzewami, chronia przed deszczem
        /// i sniegiem i maskuja nasza obecnosc w lesie"
        /// </summary>
        FallenTree,

        /// <summary>
        /// From scenario: "budowac z zwalonych galezi drzew szalasy"
        /// Requires tools (axe, rope) and construction time.
        /// </summary>
        BranchLeanTo,

        /// <summary>
        /// From scenario (spring): "niewielki trojkat pomiedzy drzewami
        /// 40-70 cm, aby korony drzew laczyly sie tworzac naturalny dach"
        /// Quick shelter for rain protection.
        /// </summary>
        TreeTriangle,

        /// <summary>
        /// From scenario (autumn/winter): "pod duzymi drzewami w rowach
        /// wykopanymi przez siebie" using "saperki skladne"
        /// </summary>
        DugoutUnderTree,

        /// <summary>
        /// From scenario: "pustostany" - abandoned buildings.
        /// Good protection but risk of criminals, disease.
        /// "moga przebywac w nich kryminalisti, zlodizeje"
        /// "zagrozenie zachorowaniem na wszawice, swierzb, grzybice nog"
        /// </summary>
        AbandonedBuilding,

        /// <summary>
        /// From scenario: "garaze podziemne pod blokami lub biurowcami"
        /// Protection from elements, may contain vehicles.
        /// </summary>
        UndergroundGarage,

        /// <summary>
        /// From scenario: residential apartment (2nd floor+).
        /// "bloki od 2 pietra - parter oraz 1 pietro z reguly sa okradane"
        /// </summary>
        Apartment
    }

    public enum FireType
    {
        /// <summary>No fire active.</summary>
        None,

        /// <summary>
        /// From scenario: "palniki spirytusowe" with "podpalka zelowa do grilla"
        /// Small, controlled, good for cooking. Low heat for warming.
        /// "Zalecane polaczenie palnikow spirytusowych podpalki zelowej
        /// z 1 kostka podpalki bialej - jesienia, zima, wiosna"
        /// </summary>
        SpiritBurner,

        /// <summary>
        /// Small campfire from gathered wood/kindling.
        /// Moderate warmth, visible from distance.
        /// </summary>
        SmallCampfire,

        /// <summary>
        /// Large campfire. Significant warmth, very visible.
        /// From scenario: fire for "zagotowanie wody do picia i mycia"
        /// </summary>
        LargeCampfire,

        /// <summary>
        /// From scenario: "zniszczone meble, plyty wiorowe ktore
        /// szybko podnosza temperature ogniska"
        /// IKEA furniture burns hot and fast.
        /// </summary>
        FurnitureFire
    }

    [Serializable]
    public struct ShelterProtectionValues
    {
        public float rain;         // 0-1 rain protection
        public float wind;         // 0-1 wind protection
        public float cold;         // 0-1 cold/insulation
        public float concealment;  // 0-1 how hidden the player is
        public float sleepBonus;   // 0-1 sleep quality improvement
    }

    // ===== SHELTER CONFIG =====

    /// <summary>
    /// Tunable configuration for shelter and fire parameters.
    /// Create assets: Assets > Create > Plaga44 > Shelter Config
    /// </summary>
    [CreateAssetMenu(fileName = "ShelterConfig", menuName = "Plaga44/Shelter Config")]
    public class ShelterConfig : ScriptableObject
    {
        [Header("Shelter Degradation")]
        [Tooltip("Base shelter degradation rate per game hour.")]
        public float shelterDegradationRate = 0.005f;

        [Header("Fire Heat Output (Watts equivalent)")]
        [Tooltip("Heat from spirit burner. Small, for cooking.")]
        public float spiritBurnerHeat = 50f;

        [Tooltip("Heat from small campfire.")]
        public float smallCampfireHeat = 200f;

        [Tooltip("Heat from large campfire.")]
        public float largeCampfireHeat = 500f;

        [Tooltip("Heat from furniture fire. From scenario: 'plyty wiorowe szybko podnosza temperature'.")]
        public float furnitureFireHeat = 400f;

        [Header("Fire Duration (Game Hours)")]
        [Tooltip("Spirit burner fuel duration per load.")]
        public float spiritBurnerFuelDuration = 3f;

        [Tooltip("Small campfire burn duration.")]
        public float smallCampfireDuration = 4f;

        [Tooltip("Large campfire burn duration.")]
        public float largeCampfireDuration = 8f;

        [Tooltip("Furniture fire burn duration. Burns hot but not long.")]
        public float furnitureFireDuration = 6f;
    }
}
