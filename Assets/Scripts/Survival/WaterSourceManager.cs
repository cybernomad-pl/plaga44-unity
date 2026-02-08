using System;
using System.Collections.Generic;
using UnityEngine;
using Plaga44.Environment;
using Plaga44.Terrain;

namespace Plaga44.Survival
{
    /// <summary>
    /// Water contamination levels.
    /// </summary>
    public enum WaterQuality
    {
        Clean,          // Safe to drink directly
        Slightly_Dirty, // May cause mild stomach issues
        Contaminated,   // Will cause illness - needs purification
        Toxic           // Cannot be purified - dead animals, chemicals
    }

    /// <summary>
    /// Types of water sources in the game world.
    /// </summary>
    public enum WaterSourceType
    {
        Stream_Clean,        // Czysty strumien - pstragi, wegorze
        Stream_Contaminated, // Strumien z martwymi zwierzetami
        River,               // Rzeka - variable quality
        Lake,                // Jezioro - check for contamination
        Puddle,              // Kaluza - usually contaminated
        Well,                // Studnia - village wells, usually clean
        Tap_Urban,           // Kran miejski - may or may not work
        Rainwater,           // Woda deszczowa - collected, clean
        Snow_Melted          // Topiony snieg - needs boiling
    }

    /// <summary>
    /// Represents a water source in the game world.
    /// </summary>
    [Serializable]
    public class WaterSource
    {
        public string sourceId;
        public WaterSourceType sourceType;
        public WaterQuality quality;
        public Vector3 worldPosition;
        public float flowRate;           // Liters per game-hour available
        public float currentVolume;      // Available volume in liters
        public float maxVolume;          // Maximum volume
        public bool hasDeadAnimals;      // Contamination from corpses
        public bool isFlowing;           // Flowing water is generally safer
        public float temperature;        // Water temperature

        [Header("Seasonal Availability")]
        public bool driesUpInSummer;
        public bool freezesInWinter;
    }

    /// <summary>
    /// Manages water sources, contamination, purification, and dehydration
    /// mechanics for the Jura KCz survival setting.
    ///
    /// Scenario references:
    /// - "korzystanie z potokow strumykow do kapieli nocą" (cz.1)
    /// - "unikac zrodel wody rzeczki, rzeki, jeziora, kaluze w ktorych rozkladaja
    ///    sie ciala zwierzat - co moze spowodowac dur brzuszny, zatrucie ukladu
    ///    pokarmowego, czerwonke, biegunke. W skrajnych przypadkach moze
    ///    doprowadzic do smierci." (cz.3)
    /// - "wode powinno sie przetrzymywac w plastykowych butelkach 1.5 litra" (cz.4)
    /// - "zagotowanie wody do picia" (cz.2/3)
    /// - "nalazy spozywac duzo wody co 1 godzine maksymalnie rownowartossc
    ///    dwoch szklanek wody" (cz.4)
    /// - "o wode mozna i trzeba prosic ludzi z terenow wiejskich" (cz.4)
    /// </summary>
    public class WaterSourceManager : MonoBehaviour
    {
        public static WaterSourceManager Instance { get; private set; }

        [Header("Player Hydration")]
        [Range(0f, 1f)]
        [SerializeField] private float playerHydration = 0.8f;

        [Tooltip("Hydration loss per game-hour at rest")]
        [SerializeField] private float baseDehydrationRate = 0.02f;

        [Header("Water Sources")]
        [SerializeField] private List<WaterSource> registeredSources = new List<WaterSource>();

        [Header("Purification")]
        [Tooltip("Game-minutes to boil water for purification")]
        [SerializeField] private float boilTimeMinutes = 15f;

        [Tooltip("Fuel needed per liter of water boiled")]
        [SerializeField] private float fuelPerLiterBoil = 0.3f;

        // Dehydration multipliers per season (summer = faster dehydration)
        // "odwodnienie organizmu poprzez utrate elektrolitow i magnezu" (cz.4)
        private readonly float[] seasonDehydrationMultiplier = { 1.2f, 2.0f, 1.0f, 0.8f };

        // Illness chances from contaminated water
        private readonly Dictionary<WaterQuality, float> illnessChance = new Dictionary<WaterQuality, float>
        {
            { WaterQuality.Clean, 0f },
            { WaterQuality.Slightly_Dirty, 0.15f },
            { WaterQuality.Contaminated, 0.6f },
            { WaterQuality.Toxic, 0.95f }
        };

        // Properties
        public float PlayerHydration => playerHydration;
        public bool IsDehydrated => playerHydration < 0.3f;
        public bool IsSeverelyDehydrated => playerHydration < 0.15f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            if (EnvironmentManager.Instance == null) return;

            UpdateDehydration(Time.deltaTime);
            UpdateWaterSources(Time.deltaTime);
        }

        /// <summary>
        /// Register a water source in the world.
        /// </summary>
        public void RegisterWaterSource(WaterSource source)
        {
            registeredSources.Add(source);
            Debug.Log($"[WaterSourceManager] Registered water source: {source.sourceId} ({source.sourceType}, quality: {source.quality})");
        }

        /// <summary>
        /// Find the nearest water source to a position.
        /// </summary>
        public WaterSource FindNearestSource(Vector3 position, float maxRange = 100f)
        {
            WaterSource nearest = null;
            float nearestDist = maxRange;

            foreach (var source in registeredSources)
            {
                float dist = Vector3.Distance(position, source.worldPosition);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = source;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Find all water sources within range, sorted by distance.
        /// </summary>
        public List<WaterSource> FindSourcesInRange(Vector3 position, float range)
        {
            List<WaterSource> result = new List<WaterSource>();

            foreach (var source in registeredSources)
            {
                float dist = Vector3.Distance(position, source.worldPosition);
                if (dist <= range)
                {
                    result.Add(source);
                }
            }

            result.Sort((a, b) =>
                Vector3.Distance(position, a.worldPosition)
                    .CompareTo(Vector3.Distance(position, b.worldPosition)));

            return result;
        }

        /// <summary>
        /// Attempt to drink from a water source.
        /// Returns the health consequences.
        /// </summary>
        public DrinkResult DrinkFrom(WaterSource source, float liters, bool isPurified)
        {
            var result = new DrinkResult();

            if (source.currentVolume < liters)
            {
                result.success = false;
                result.message = "Niewystarczaj\u0105ca ilo\u015b\u0107 wody";
                return result;
            }

            // Check if frozen
            if (source.freezesInWinter && EnvironmentManager.Instance != null &&
                EnvironmentManager.Instance.CurrentSeason == Season.Winter &&
                EnvironmentManager.Instance.CurrentTemperature < -2f)
            {
                result.success = false;
                result.message = "\u0179r\u00f3d\u0142o wody jest zamarznięte";
                return result;
            }

            result.success = true;
            source.currentVolume -= liters;

            // Hydration restored (about 0.15 per glass, 2 glasses per hour recommended)
            float hydrationGain = liters * 0.2f;
            playerHydration = Mathf.Clamp01(playerHydration + hydrationGain);
            result.hydrationRestored = hydrationGain;

            // Check for illness
            WaterQuality effectiveQuality = isPurified ? WaterQuality.Clean : source.quality;

            // Dead animals make water toxic regardless
            // "zrodla wody w ktorych rozkladaja sie ciala zwierzat"
            if (source.hasDeadAnimals && !isPurified)
            {
                effectiveQuality = WaterQuality.Toxic;
            }

            if (illnessChance.ContainsKey(effectiveQuality))
            {
                float chance = illnessChance[effectiveQuality] * liters; // More water = more risk
                if (UnityEngine.Random.value < chance)
                {
                    result.causedIllness = true;
                    result.illnessType = DetermineWaterIllness(effectiveQuality);
                    result.message = GetIllnessMessage(result.illnessType);

                    // "w skrajnych przypadkach moze doprowadzic do smierci"
                    if (effectiveQuality == WaterQuality.Toxic)
                    {
                        result.isFatal = UnityEngine.Random.value < 0.2f;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Purify water by boiling.
        /// "zagotowanie wody do picia" (cz.2/3)
        /// Requires fire and fuel (spirit burner or campfire).
        /// </summary>
        public PurifyResult PurifyWater(float liters, bool hasFire, float availableFuel)
        {
            var result = new PurifyResult();

            if (!hasFire)
            {
                result.success = false;
                result.message = "Potrzebne ognisko lub palnik spirytusowy";
                return result;
            }

            float fuelNeeded = liters * fuelPerLiterBoil;
            if (availableFuel < fuelNeeded)
            {
                result.success = false;
                result.message = $"Za ma\u0142o paliwa. Potrzeba: {fuelNeeded:F1}, dost\u0119pne: {availableFuel:F1}";
                return result;
            }

            result.success = true;
            result.fuelUsed = fuelNeeded;
            result.timeMinutes = boilTimeMinutes * liters;
            result.litersPurified = liters;
            result.message = $"Oczyszczono {liters:F1}L wody przez gotowanie ({result.timeMinutes:F0} min)";

            return result;
        }

        /// <summary>
        /// Collect rainwater (free, clean water during rain).
        /// </summary>
        public float CollectRainwater(float collectionAreaM2, float durationHours)
        {
            if (EnvironmentManager.Instance == null) return 0f;

            float rainIntensity = EnvironmentManager.Instance.RainIntensity;
            if (rainIntensity <= 0f) return 0f;

            // Approximate liters collected: area * intensity * time
            float litersCollected = collectionAreaM2 * rainIntensity * durationHours * 0.5f;
            return litersCollected;
        }

        /// <summary>
        /// Melt snow for water. Requires fire.
        /// </summary>
        public float MeltSnowForWater(float snowVolumeLiters, bool hasFire)
        {
            if (!hasFire) return 0f;
            if (EnvironmentManager.Instance == null) return 0f;
            if (EnvironmentManager.Instance.SnowCoverage < 0.1f) return 0f;

            // Snow produces roughly 1/10 its volume in water
            return snowVolumeLiters * 0.1f;
        }

        /// <summary>
        /// Update player dehydration based on activity and conditions.
        /// "nalazy spozywac duzo wody co 1 godzine" (cz.4)
        /// </summary>
        private void UpdateDehydration(float deltaTime)
        {
            if (EnvironmentManager.Instance == null) return;

            float hoursElapsed = deltaTime / 60f; // Approximate
            int s = (int)EnvironmentManager.Instance.CurrentSeason;
            float seasonMultiplier = seasonDehydrationMultiplier[s];

            // Temperature affects dehydration
            float temp = EnvironmentManager.Instance.CurrentTemperature;
            float tempMultiplier = 1f;
            if (temp > 25f)
            {
                // "wysokie temperatury" causing faster dehydration
                tempMultiplier = 1f + (temp - 25f) * 0.1f;
            }

            float dehydrationThisFrame = baseDehydrationRate * seasonMultiplier * tempMultiplier * hoursElapsed;
            playerHydration = Mathf.Clamp01(playerHydration - dehydrationThisFrame);
        }

        /// <summary>
        /// Apply additional dehydration from physical activity (marching, carrying heavy loads).
        /// "duzy ciezar 25 kg powoduje szybsze spalanie energii - odwodnienie organizmu" (cz.4)
        /// </summary>
        public void ApplyActivityDehydration(float intensity, float durationHours)
        {
            float loss = intensity * durationHours * 0.05f;

            if (EnvironmentManager.Instance != null)
            {
                int s = (int)EnvironmentManager.Instance.CurrentSeason;
                loss *= seasonDehydrationMultiplier[s];
            }

            playerHydration = Mathf.Clamp01(playerHydration - loss);
        }

        /// <summary>
        /// Update water source volumes (flowing water replenishes, standing water may stagnate).
        /// </summary>
        private void UpdateWaterSources(float deltaTime)
        {
            float hoursElapsed = deltaTime / 60f;

            foreach (var source in registeredSources)
            {
                if (source.isFlowing && source.currentVolume < source.maxVolume)
                {
                    source.currentVolume = Mathf.Min(
                        source.maxVolume,
                        source.currentVolume + source.flowRate * hoursElapsed
                    );
                }

                // Stagnant water quality degrades over time
                if (!source.isFlowing && source.quality == WaterQuality.Slightly_Dirty)
                {
                    // Small chance to degrade to contaminated
                    if (UnityEngine.Random.value < 0.001f * hoursElapsed)
                    {
                        source.quality = WaterQuality.Contaminated;
                    }
                }
            }
        }

        /// <summary>
        /// Determine specific illness from contaminated water.
        /// "dur brzuszny, zatrucie ukladu pokarmowego, czerwonka, biegunka"
        /// </summary>
        private WaterIllness DetermineWaterIllness(WaterQuality quality)
        {
            if (quality == WaterQuality.Toxic)
            {
                float roll = UnityEngine.Random.value;
                if (roll < 0.3f) return WaterIllness.Typhoid;        // dur brzuszny
                if (roll < 0.6f) return WaterIllness.Dysentery;      // czerwonka
                return WaterIllness.SeverePoisoning;                   // zatrucie
            }
            else if (quality == WaterQuality.Contaminated)
            {
                float roll = UnityEngine.Random.value;
                if (roll < 0.4f) return WaterIllness.Diarrhea;       // biegunka
                if (roll < 0.7f) return WaterIllness.StomachPoisoning;
                return WaterIllness.Dysentery;
            }
            else
            {
                return WaterIllness.Diarrhea;
            }
        }

        private string GetIllnessMessage(WaterIllness illness)
        {
            switch (illness)
            {
                case WaterIllness.Diarrhea:
                    return "Biegunka od ska\u017conej wody";
                case WaterIllness.Dysentery:
                    return "Czerwonka - powa\u017cne objawy!";
                case WaterIllness.Typhoid:
                    return "Dur brzuszny - zagro\u017cenie \u017cycia!";
                case WaterIllness.StomachPoisoning:
                    return "Zatrucie uk\u0142adu pokarmowego";
                case WaterIllness.SeverePoisoning:
                    return "Ci\u0119\u017ckie zatrucie - potrzebne leki!";
                default:
                    return "Z\u0142e samopoczucie";
            }
        }

        /// <summary>
        /// Restore hydration directly (e.g., from found bottled water, tea from thermos).
        /// </summary>
        public void RestoreHydration(float amount)
        {
            playerHydration = Mathf.Clamp01(playerHydration + amount);
        }

        /// <summary>
        /// Check if player should consume electrolytes.
        /// "co 2 lub 3 godziny zazywac elektrolity rozpuszczone w chlodnej wodzie" (cz.4)
        /// </summary>
        public bool ShouldConsumeElectrolytes()
        {
            if (EnvironmentManager.Instance == null) return false;

            return EnvironmentManager.Instance.CurrentSeason == Season.Summer &&
                   EnvironmentManager.Instance.CurrentTemperature > 25f &&
                   playerHydration < 0.6f;
        }
    }

    /// <summary>
    /// Water-borne illnesses from scenario descriptions.
    /// </summary>
    public enum WaterIllness
    {
        Diarrhea,           // biegunka
        Dysentery,          // czerwonka
        Typhoid,            // dur brzuszny
        StomachPoisoning,   // zatrucie ukladu pokarmowego
        SeverePoisoning     // ciezkie zatrucie
    }

    /// <summary>
    /// Result of drinking water.
    /// </summary>
    [Serializable]
    public class DrinkResult
    {
        public bool success;
        public float hydrationRestored;
        public bool causedIllness;
        public WaterIllness illnessType;
        public bool isFatal;
        public string message;
    }

    /// <summary>
    /// Result of water purification.
    /// </summary>
    [Serializable]
    public class PurifyResult
    {
        public bool success;
        public float fuelUsed;
        public float timeMinutes;
        public float litersPurified;
        public string message;
    }
}
