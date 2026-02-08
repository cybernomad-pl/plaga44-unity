using System;
using System.Collections.Generic;
using UnityEngine;
using Plaga44.Environment;
using Plaga44.Terrain;

namespace Plaga44.Survival
{
    /// <summary>
    /// Categories of forageable resources.
    /// </summary>
    public enum ForageCategory
    {
        Berry,       // Jagody, maliny, jezyny
        Mushroom,    // Grzyby (with poisoning risk)
        Fish,        // Pstragi, wegorze from clean streams
        Crop,        // Marchew, ziemniaki, zboze from fields
        Fruit,       // Jablka from orchards
        Firewood,    // Chrust, kora brzozy, suche galezie
        Herb,        // Medicinal plants
        Water        // Water sources (see WaterSourceManager)
    }

    /// <summary>
    /// Individual forageable resource definition.
    /// </summary>
    [Serializable]
    public class ForageableResource
    {
        public string resourceId;
        public string displayNamePL;       // Polish name for display
        public string displayNameEN;       // English name
        public ForageCategory category;

        [Header("Availability")]
        public Season[] availableSeasons;
        public TerrainType[] spawnTerrainTypes;

        [Header("Nutrition")]
        [Tooltip("Calories per unit gathered")]
        public float caloriesPerUnit = 50f;

        [Tooltip("Hydration restored per unit (0-1)")]
        [Range(0f, 1f)]
        public float hydrationPerUnit = 0f;

        [Tooltip("Glucose/energy boost")]
        [Range(0f, 1f)]
        public float glucoseBoost = 0f;

        [Header("Risks")]
        [Tooltip("Chance of food poisoning if consumed raw (0-1)")]
        [Range(0f, 1f)]
        public float poisoningChance = 0f;

        [Tooltip("Can this resource be fatal if wrong type gathered (mushrooms)")]
        public bool canBeLethal = false;

        [Header("Gathering")]
        [Tooltip("Time in game-minutes to gather one unit")]
        public float gatherTimeMinutes = 5f;

        [Tooltip("Noise generated while gathering (0-1)")]
        [Range(0f, 1f)]
        public float gatherNoise = 0.2f;

        [Tooltip("Requires specific tool")]
        public string requiredTool = "";

        [Header("Spawning")]
        [Tooltip("Base spawn chance per zone check")]
        [Range(0f, 1f)]
        public float baseSpawnChance = 0.5f;

        [Tooltip("Max units spawnable in one zone")]
        public int maxPerZone = 5;
    }

    /// <summary>
    /// Manages seasonal resource spawning and foraging mechanics for the
    /// Jura KCz survival setting.
    ///
    /// Based on scenario documents:
    /// - "na terenach podmoklych maliny, jezyny lub jagody" (cz.3 - spring)
    /// - "latem w niektorych lasach pojawiaja sie jagody, maliny" (cz.3)
    /// - "w czystych rzekach lub strumieniach moga pojawic sie ryby - pstragi lub wegorze" (cz.3)
    /// - "jesienia w lesie w koncu wrzesnia pojawiaja sie grzyby" (cz.3)
    /// - "bez znajomosci gatunkow grzybow grozi to smiertelnym zatruciem" (cz.3)
    /// - "zabieranie zywnosci z pol marchew, ziemniaki, zboze, jablka" (cz.1)
    /// - "latwiejsze zdobywanie opalu chrustu, kor brzozy, suchych galezi" (cz.3 - spring)
    /// - Winter: "ciezko jest o przywienie" (cz.3)
    /// </summary>
    public class ForagingSystem : MonoBehaviour
    {
        public static ForagingSystem Instance { get; private set; }

        [Header("Resource Definitions")]
        [SerializeField] private List<ForageableResource> resourceDefinitions;

        [Header("Settings")]
        [Tooltip("How often to refresh spawn zones (game hours)")]
        [SerializeField] private float spawnRefreshIntervalHours = 6f;

        [Tooltip("Knowledge level for mushroom identification (0 = none, 1 = expert)")]
        [Range(0f, 1f)]
        [SerializeField] private float mushroomKnowledge = 0f;

        [Tooltip("Fishing skill level (0 = none, 1 = expert)")]
        [Range(0f, 1f)]
        [SerializeField] private float fishingSkill = 0f;

        // Active resource spawns in the world
        private Dictionary<Vector3Int, List<SpawnedResource>> activeSpawns = new Dictionary<Vector3Int, List<SpawnedResource>>();
        private float spawnRefreshTimer = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (resourceDefinitions == null || resourceDefinitions.Count == 0)
            {
                InitializeDefaultResources();
            }
        }

        private void Update()
        {
            if (EnvironmentManager.Instance == null) return;

            float hoursElapsed = Time.deltaTime / 60f; // Approximate
            spawnRefreshTimer -= hoursElapsed;

            if (spawnRefreshTimer <= 0f)
            {
                RefreshSpawns();
                spawnRefreshTimer = spawnRefreshIntervalHours;
            }
        }

        /// <summary>
        /// Initialize the default resource definitions based on scenario documents.
        /// </summary>
        private void InitializeDefaultResources()
        {
            resourceDefinitions = new List<ForageableResource>();

            // -- BERRIES --

            // Blueberries (jagody) - spring marshland, summer forest
            resourceDefinitions.Add(new ForageableResource
            {
                resourceId = "berry_blueberry",
                displayNamePL = "Jagody",
                displayNameEN = "Blueberries",
                category = ForageCategory.Berry,
                availableSeasons = new[] { Season.Spring, Season.Summer },
                spawnTerrainTypes = new[] { TerrainType.Marshland, TerrainType.Forest_Mixed, TerrainType.Forest_Coniferous },
                caloriesPerUnit = 40f,
                hydrationPerUnit = 0.1f,
                glucoseBoost = 0.15f,
                poisoningChance = 0f,
                gatherTimeMinutes = 10f,
                gatherNoise = 0.1f,
                baseSpawnChance = 0.4f,
                maxPerZone = 8
            });

            // Raspberries (maliny) - spring/summer marshland and forest edge
            resourceDefinitions.Add(new ForageableResource
            {
                resourceId = "berry_raspberry",
                displayNamePL = "Maliny",
                displayNameEN = "Raspberries",
                category = ForageCategory.Berry,
                availableSeasons = new[] { Season.Spring, Season.Summer },
                spawnTerrainTypes = new[] { TerrainType.Marshland, TerrainType.Forest_Mixed, TerrainType.Clearing },
                caloriesPerUnit = 35f,
                hydrationPerUnit = 0.1f,
                glucoseBoost = 0.15f,
                poisoningChance = 0f,
                gatherTimeMinutes = 10f,
                gatherNoise = 0.1f,
                baseSpawnChance = 0.35f,
                maxPerZone = 6
            });

            // Blackberries (jezyny) - summer, thorny bushes
            resourceDefinitions.Add(new ForageableResource
            {
                resourceId = "berry_blackberry",
                displayNamePL = "Je\u017cyny",
                displayNameEN = "Blackberries",
                category = ForageCategory.Berry,
                availableSeasons = new[] { Season.Summer, Season.Autumn },
                spawnTerrainTypes = new[] { TerrainType.Marshland, TerrainType.Clearing, TerrainType.Forest_Mixed },
                caloriesPerUnit = 45f,
                hydrationPerUnit = 0.1f,
                glucoseBoost = 0.12f,
                poisoningChance = 0f,
                gatherTimeMinutes = 15f,  // Thorns slow gathering
                gatherNoise = 0.15f,
                baseSpawnChance = 0.30f,
                maxPerZone = 5
            });

            // -- MUSHROOMS --

            // Edible mushrooms (safe) - autumn only
            resourceDefinitions.Add(new ForageableResource
            {
                resourceId = "mushroom_edible",
                displayNamePL = "Grzyby jadalne",
                displayNameEN = "Edible Mushrooms",
                category = ForageCategory.Mushroom,
                availableSeasons = new[] { Season.Autumn },
                spawnTerrainTypes = new[] { TerrainType.Forest_Mixed, TerrainType.Forest_Coniferous },
                caloriesPerUnit = 25f,
                hydrationPerUnit = 0.05f,
                glucoseBoost = 0.05f,
                poisoningChance = 0f,  // Safe if correctly identified
                canBeLethal = false,
                gatherTimeMinutes = 5f,
                gatherNoise = 0.1f,
                baseSpawnChance = 0.45f,
                maxPerZone = 10
            });

            // Poisonous mushrooms (danger!) - "bez znajomosci gatunkow grzybow
            // grozi to smiertelnym zatruciem, zatruciem ukladu pokarmowego, biegunka, czerwonka"
            resourceDefinitions.Add(new ForageableResource
            {
                resourceId = "mushroom_poisonous",
                displayNamePL = "Grzyby truj\u0105ce",
                displayNameEN = "Poisonous Mushrooms",
                category = ForageCategory.Mushroom,
                availableSeasons = new[] { Season.Autumn },
                spawnTerrainTypes = new[] { TerrainType.Forest_Mixed, TerrainType.Forest_Coniferous },
                caloriesPerUnit = 20f,
                hydrationPerUnit = 0.03f,
                glucoseBoost = 0f,
                poisoningChance = 1f,
                canBeLethal = true,
                gatherTimeMinutes = 5f,
                gatherNoise = 0.1f,
                baseSpawnChance = 0.30f,
                maxPerZone = 8
            });

            // -- FISH --

            // Trout (pstrag) - clean streams in summer
            resourceDefinitions.Add(new ForageableResource
            {
                resourceId = "fish_trout",
                displayNamePL = "Pstr\u0105g",
                displayNameEN = "Trout",
                category = ForageCategory.Fish,
                availableSeasons = new[] { Season.Spring, Season.Summer, Season.Autumn },
                spawnTerrainTypes = new[] { TerrainType.Stream },
                caloriesPerUnit = 150f,
                hydrationPerUnit = 0.05f,
                glucoseBoost = 0.02f,
                poisoningChance = 0.1f,  // Raw fish risk
                gatherTimeMinutes = 30f,
                gatherNoise = 0.3f,
                requiredTool = "fishing_net",  // "podbieraki z malymi oczkami"
                baseSpawnChance = 0.25f,
                maxPerZone = 3
            });

            // Eel (wegorz) - clean streams
            resourceDefinitions.Add(new ForageableResource
            {
                resourceId = "fish_eel",
                displayNamePL = "W\u0119gorz",
                displayNameEN = "Eel",
                category = ForageCategory.Fish,
                availableSeasons = new[] { Season.Summer, Season.Autumn },
                spawnTerrainTypes = new[] { TerrainType.Stream },
                caloriesPerUnit = 200f,
                hydrationPerUnit = 0.03f,
                glucoseBoost = 0.02f,
                poisoningChance = 0.15f,
                gatherTimeMinutes = 45f,
                gatherNoise = 0.35f,
                requiredTool = "fishing_net",
                baseSpawnChance = 0.10f,
                maxPerZone = 1
            });

            // -- CROPS (from village fields) --

            // Carrots (marchew)
            resourceDefinitions.Add(new ForageableResource
            {
                resourceId = "crop_carrot",
                displayNamePL = "Marchew",
                displayNameEN = "Carrots",
                category = ForageCategory.Crop,
                availableSeasons = new[] { Season.Summer, Season.Autumn },
                spawnTerrainTypes = new[] { TerrainType.Field_Agricultural, TerrainType.Village_Edge },
                caloriesPerUnit = 30f,
                hydrationPerUnit = 0.1f,
                glucoseBoost = 0.05f,
                poisoningChance = 0f,
                gatherTimeMinutes = 3f,
                gatherNoise = 0.2f,
                baseSpawnChance = 0.5f,
                maxPerZone = 15
            });

            // Potatoes (ziemniaki)
            resourceDefinitions.Add(new ForageableResource
            {
                resourceId = "crop_potato",
                displayNamePL = "Ziemniaki",
                displayNameEN = "Potatoes",
                category = ForageCategory.Crop,
                availableSeasons = new[] { Season.Summer, Season.Autumn },
                spawnTerrainTypes = new[] { TerrainType.Field_Agricultural, TerrainType.Village_Edge },
                caloriesPerUnit = 80f,
                hydrationPerUnit = 0.05f,
                glucoseBoost = 0.08f,
                poisoningChance = 0f,
                gatherTimeMinutes = 5f,
                gatherNoise = 0.3f,  // Digging makes noise
                baseSpawnChance = 0.5f,
                maxPerZone = 12
            });

            // Grain (zboze) - summer harvest
            resourceDefinitions.Add(new ForageableResource
            {
                resourceId = "crop_grain",
                displayNamePL = "Zbo\u017ce",
                displayNameEN = "Grain",
                category = ForageCategory.Crop,
                availableSeasons = new[] { Season.Summer },
                spawnTerrainTypes = new[] { TerrainType.Field_Agricultural },
                caloriesPerUnit = 100f,
                hydrationPerUnit = 0f,
                glucoseBoost = 0.10f,
                poisoningChance = 0f,
                gatherTimeMinutes = 8f,
                gatherNoise = 0.2f,
                baseSpawnChance = 0.45f,
                maxPerZone = 20
            });

            // Apples (jablka)
            resourceDefinitions.Add(new ForageableResource
            {
                resourceId = "fruit_apple",
                displayNamePL = "Jab\u0142ka",
                displayNameEN = "Apples",
                category = ForageCategory.Fruit,
                availableSeasons = new[] { Season.Summer, Season.Autumn },
                spawnTerrainTypes = new[] { TerrainType.Village_Edge, TerrainType.Field_Agricultural },
                caloriesPerUnit = 50f,
                hydrationPerUnit = 0.1f,
                glucoseBoost = 0.12f,
                poisoningChance = 0f,
                gatherTimeMinutes = 2f,
                gatherNoise = 0.15f,
                baseSpawnChance = 0.40f,
                maxPerZone = 10
            });

            // -- FIREWOOD --

            // Brushwood/kindling (chrust) - "latwiejsze zdobywanie chrustu kor brzozy suchych galezi"
            resourceDefinitions.Add(new ForageableResource
            {
                resourceId = "firewood_kindling",
                displayNamePL = "Chrust i podpa\u0142ka",
                displayNameEN = "Kindling and Brushwood",
                category = ForageCategory.Firewood,
                availableSeasons = new[] { Season.Spring, Season.Summer, Season.Autumn },
                spawnTerrainTypes = new[] { TerrainType.Forest_Mixed, TerrainType.Forest_Coniferous, TerrainType.Clearing },
                caloriesPerUnit = 0f,
                hydrationPerUnit = 0f,
                glucoseBoost = 0f,
                poisoningChance = 0f,
                gatherTimeMinutes = 10f,
                gatherNoise = 0.3f,
                baseSpawnChance = 0.7f,
                maxPerZone = 15
            });

            // Birch bark (kora brzozy) - excellent fire starter
            resourceDefinitions.Add(new ForageableResource
            {
                resourceId = "firewood_birchbark",
                displayNamePL = "Kora brzozy",
                displayNameEN = "Birch Bark",
                category = ForageCategory.Firewood,
                availableSeasons = new[] { Season.Spring, Season.Summer, Season.Autumn },
                spawnTerrainTypes = new[] { TerrainType.Forest_Mixed },
                caloriesPerUnit = 0f,
                hydrationPerUnit = 0f,
                glucoseBoost = 0f,
                poisoningChance = 0f,
                gatherTimeMinutes = 5f,
                gatherNoise = 0.2f,
                baseSpawnChance = 0.35f,
                maxPerZone = 5
            });

            // Winter firewood - much harder to find
            resourceDefinitions.Add(new ForageableResource
            {
                resourceId = "firewood_winter",
                displayNamePL = "Drewno na opa\u0142 (zima)",
                displayNameEN = "Winter Firewood",
                category = ForageCategory.Firewood,
                availableSeasons = new[] { Season.Winter },
                spawnTerrainTypes = new[] { TerrainType.Forest_Mixed, TerrainType.Forest_Coniferous, TerrainType.Urban_Ruins },
                caloriesPerUnit = 0f,
                hydrationPerUnit = 0f,
                glucoseBoost = 0f,
                poisoningChance = 0f,
                gatherTimeMinutes = 20f,  // Much harder in winter
                gatherNoise = 0.5f,
                baseSpawnChance = 0.3f,
                maxPerZone = 5
            });
        }

        /// <summary>
        /// Get all resources available at a given terrain type in the current season.
        /// </summary>
        public List<ForageableResource> GetAvailableResources(TerrainType terrainType)
        {
            if (EnvironmentManager.Instance == null) return new List<ForageableResource>();

            Season season = EnvironmentManager.Instance.CurrentSeason;
            List<ForageableResource> available = new List<ForageableResource>();

            foreach (var resource in resourceDefinitions)
            {
                if (!IsResourceInSeason(resource, season)) continue;
                if (!IsResourceOnTerrain(resource, terrainType)) continue;

                available.Add(resource);
            }

            return available;
        }

        /// <summary>
        /// Attempt to gather a specific resource. Returns gathered amount.
        /// Handles mushroom identification risk based on player knowledge.
        /// </summary>
        public GatherResult AttemptGather(ForageableResource resource, bool hasTool)
        {
            var result = new GatherResult();

            // Check tool requirement
            if (!string.IsNullOrEmpty(resource.requiredTool) && !hasTool)
            {
                result.success = false;
                result.message = $"Requires: {resource.requiredTool}";
                return result;
            }

            // Mushroom identification check
            // "bez znajomosci gatunkow grzybow nie jest mozliwe ich zbieranie"
            if (resource.category == ForageCategory.Mushroom)
            {
                result = HandleMushroomGathering(resource);
                return result;
            }

            // Fish: affected by weather and time
            if (resource.category == ForageCategory.Fish)
            {
                result = HandleFishGathering(resource);
                return result;
            }

            // Standard gathering
            result.success = true;
            result.amountGathered = UnityEngine.Random.Range(1, resource.maxPerZone + 1);
            result.caloriesGained = result.amountGathered * resource.caloriesPerUnit;
            result.hydrationGained = result.amountGathered * resource.hydrationPerUnit;
            result.gatherTimeMinutes = resource.gatherTimeMinutes * result.amountGathered;
            result.noiseGenerated = resource.gatherNoise;

            // Weather affects crop gathering
            if (resource.category == ForageCategory.Crop && EnvironmentManager.Instance != null)
            {
                if (EnvironmentManager.Instance.RainIntensity > 0.5f)
                {
                    result.gatherTimeMinutes *= 1.5f; // Slower in heavy rain
                }
            }

            return result;
        }

        /// <summary>
        /// Handle mushroom gathering with identification risk.
        /// Player's mushroom knowledge affects ability to distinguish safe from toxic.
        /// </summary>
        private GatherResult HandleMushroomGathering(ForageableResource resource)
        {
            var result = new GatherResult();

            if (mushroomKnowledge < 0.3f)
            {
                // Very low knowledge - cannot reliably identify any mushrooms
                // "bez znajomosci gatunkow grzybow nie jest mozliwe ich zbieranie"
                result.success = true;
                result.amountGathered = UnityEngine.Random.Range(1, 5);

                // High risk of picking poisonous ones
                float misidentifyChance = 1f - mushroomKnowledge;
                if (UnityEngine.Random.value < misidentifyChance * 0.5f)
                {
                    result.isPoisoned = true;
                    result.isFatal = resource.canBeLethal && UnityEngine.Random.value < 0.3f;
                    result.message = result.isFatal
                        ? "Zebrano truj\u0105ce grzyby - \u015bmiertelne zatrucie!"
                        : "Zatrucie pokarmowe - biegunka, czerwonka";
                }
            }
            else if (mushroomKnowledge < 0.7f)
            {
                // Moderate knowledge - some risk remains
                result.success = true;
                result.amountGathered = UnityEngine.Random.Range(1, resource.maxPerZone);
                float misidentifyChance = (1f - mushroomKnowledge) * 0.3f;
                if (UnityEngine.Random.value < misidentifyChance)
                {
                    result.isPoisoned = true;
                    result.isFatal = false;
                    result.message = "Lekkie zatrucie pokarmowe";
                }
            }
            else
            {
                // Expert - can reliably identify safe mushrooms
                if (resource.resourceId == "mushroom_poisonous")
                {
                    result.success = false;
                    result.message = "Rozpoznano grzyby truj\u0105ce - pominięto";
                    return result;
                }
                result.success = true;
                result.amountGathered = UnityEngine.Random.Range(2, resource.maxPerZone + 1);
            }

            if (result.success && !result.isPoisoned)
            {
                result.caloriesGained = result.amountGathered * resource.caloriesPerUnit;
                result.hydrationGained = result.amountGathered * resource.hydrationPerUnit;
            }

            result.gatherTimeMinutes = resource.gatherTimeMinutes * result.amountGathered;
            result.noiseGenerated = resource.gatherNoise;
            return result;
        }

        /// <summary>
        /// Handle fishing mechanics.
        /// "W czystych rzekach lub strumieniach moga pojawic sie ryby
        /// ktore nalezy lowic przy uzyciu podbierakow z malymi oczkami"
        /// </summary>
        private GatherResult HandleFishGathering(ForageableResource resource)
        {
            var result = new GatherResult();

            float catchChance = 0.3f + fishingSkill * 0.4f;

            // Weather affects fishing
            if (EnvironmentManager.Instance != null)
            {
                if (EnvironmentManager.Instance.RainIntensity > 0.5f)
                    catchChance *= 0.6f; // Harder in rain

                if (!EnvironmentManager.Instance.IsDaytime)
                    catchChance *= 0.5f; // Harder at night
            }

            if (UnityEngine.Random.value < catchChance)
            {
                result.success = true;
                result.amountGathered = UnityEngine.Random.Range(1, 3);
                result.caloriesGained = result.amountGathered * resource.caloriesPerUnit;
                result.hydrationGained = result.amountGathered * resource.hydrationPerUnit;

                // Raw fish poisoning risk unless cooked
                result.rawPoisoningChance = resource.poisoningChance;
            }
            else
            {
                result.success = false;
                result.message = "Nie uda\u0142o si\u0119 z\u0142owi\u0107 ryby";
            }

            result.gatherTimeMinutes = resource.gatherTimeMinutes;
            result.noiseGenerated = resource.gatherNoise;
            return result;
        }

        /// <summary>
        /// Refresh resource spawns for active zones.
        /// Called periodically based on spawnRefreshIntervalHours.
        /// </summary>
        private void RefreshSpawns()
        {
            // Clear expired spawns
            var keysToRemove = new List<Vector3Int>();
            foreach (var kvp in activeSpawns)
            {
                kvp.Value.RemoveAll(s => s.remainingUnits <= 0);
                if (kvp.Value.Count == 0)
                    keysToRemove.Add(kvp.Key);
            }
            foreach (var key in keysToRemove)
                activeSpawns.Remove(key);
        }

        /// <summary>
        /// Increase mushroom knowledge through practice or finding field guide.
        /// </summary>
        public void IncreaseMushroomKnowledge(float amount)
        {
            mushroomKnowledge = Mathf.Clamp01(mushroomKnowledge + amount);
            Debug.Log($"[ForagingSystem] Mushroom knowledge: {mushroomKnowledge:F2}");
        }

        /// <summary>
        /// Increase fishing skill through practice.
        /// </summary>
        public void IncreaseFishingSkill(float amount)
        {
            fishingSkill = Mathf.Clamp01(fishingSkill + amount);
            Debug.Log($"[ForagingSystem] Fishing skill: {fishingSkill:F2}");
        }

        /// <summary>
        /// Get a seasonal summary of available food sources.
        /// Useful for UI and player guidance.
        /// </summary>
        public string GetSeasonalForagingSummary(Season season)
        {
            switch (season)
            {
                case Season.Spring:
                    return "Wiosna: Pojawiaj\u0105 si\u0119 pierwsze jagody i maliny na terenach podmok\u0142ych. " +
                           "\u0141atwiejsze zdobywanie chrustu i kory brzozy na opa\u0142. " +
                           "Mo\u017cliwe \u0142owienie ryb w strumieniach.";
                case Season.Summer:
                    return "Lato: Jagody, maliny i je\u017cyny w lasach. Ryby w czystych strumieniach. " +
                           "Marchew, ziemniaki, zbo\u017ce i jab\u0142ka na polach. " +
                           "Uwa\u017caj na odwodnienie i udar s\u0142oneczny!";
                case Season.Autumn:
                    return "Jesie\u0144: Grzyby w lasach (UWAGA: ryzyko zatrucia bez znajomo\u015bci gatunk\u00f3w!). " +
                           "Ostatnie jagody. Marchew i ziemniaki na polach. " +
                           "Wcze\u015bniej robi si\u0119 ciemno - ogranicz poruszanie.";
                case Season.Winter:
                    return "Zima: Bardzo ograniczone \u017ar\u00f3d\u0142a po\u017cywienia w lesie. " +
                           "Konieczne kradzie\u017ce z market\u00f3w, aptek, stacji benzynowych. " +
                           "Trudno\u015bci z rozpalaniem ognisk i gotowaniem.";
                default:
                    return "";
            }
        }

        private bool IsResourceInSeason(ForageableResource resource, Season season)
        {
            foreach (var s in resource.availableSeasons)
            {
                if (s == season) return true;
            }
            return false;
        }

        private bool IsResourceOnTerrain(ForageableResource resource, TerrainType terrain)
        {
            foreach (var t in resource.spawnTerrainTypes)
            {
                if (t == terrain) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Result of a gather attempt.
    /// </summary>
    [Serializable]
    public class GatherResult
    {
        public bool success;
        public int amountGathered;
        public float caloriesGained;
        public float hydrationGained;
        public float gatherTimeMinutes;
        public float noiseGenerated;
        public bool isPoisoned;
        public bool isFatal;
        public float rawPoisoningChance;
        public string message;
    }

    /// <summary>
    /// Tracks a spawned resource in the world.
    /// </summary>
    [Serializable]
    public class SpawnedResource
    {
        public string resourceId;
        public Vector3 worldPosition;
        public int remainingUnits;
        public float spawnTime;
    }
}
