using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.Terrain
{
    /// <summary>
    /// Terrain types found in the Jura Krakowsko-Czestochowska region.
    /// Based on survivalist scenario documents describing limestone hills,
    /// mixed/coniferous forests, streams, and cave systems.
    /// </summary>
    public enum TerrainType
    {
        Forest_Mixed,       // Lasy mieszane - beech, hazel, birch with spruce/fir
        Forest_Coniferous,  // Lasy iglaste - spruce (swierk) and fir (jodla)
        LimestoneRocks,     // Skaly wapienne - characteristic Jura KCz formations
        Cave,               // Jaskinie - shelter opportunities
        Stream,             // Strumienie/potoki - water source, fish
        Path_Forest,        // Sciezki lesne - movement corridors
        Path_Mountain,      // Sciezki podgorskie - elevated terrain paths
        Clearing,           // Polany - open areas, visibility risk
        Marshland,          // Tereny podmokle - berry spawns, mosquito risk
        Village_Edge,       // Obrzeza wsi - foraging from fields
        Urban_Ruins,        // Ruiny miejskie - loot but danger
        Field_Agricultural  // Pola uprawne - seasonal crops (carrots, potatoes, grain)
    }

    /// <summary>
    /// Surface material affecting movement, sound, and slip risk.
    /// </summary>
    public enum SurfaceType
    {
        ForestFloor,        // Sciolka lesna - leaves, needles, branches
        Limestone,          // Kamienie wapienne - slippery when wet
        Mud,                // Bloto - slow movement
        Snow,               // Snieg - tracks visible, deep snow exhaustion
        Ice,                // Lod - extreme slip risk on rocks
        Grass,              // Trawa - clearings and field edges
        Water_Shallow,      // Plytka woda - stream crossings
        Gravel,             // Zwir - noisy movement
        Concrete,           // Beton - urban areas
        Rubble              // Gruz - destroyed buildings
    }

    /// <summary>
    /// Properties for a terrain cell/zone describing its gameplay characteristics.
    /// </summary>
    [Serializable]
    public class TerrainProperties
    {
        [Header("Terrain Identity")]
        public TerrainType terrainType;
        public SurfaceType surfaceType;

        [Header("Movement")]
        [Range(0f, 1f)]
        [Tooltip("Base movement speed multiplier (1.0 = normal)")]
        public float baseMovementMultiplier = 1f;

        [Range(0f, 1f)]
        [Tooltip("Slip chance when surface is dry (0 = no slip)")]
        public float drySlipChance = 0f;

        [Range(0f, 1f)]
        [Tooltip("Slip chance when surface is wet from rain")]
        public float wetSlipChance = 0f;

        [Range(0f, 1f)]
        [Tooltip("Slip chance when icy (winter)")]
        public float icySlipChance = 0f;

        [Header("Stealth")]
        [Range(0f, 1f)]
        [Tooltip("How much noise movement generates (0 = silent, 1 = very loud)")]
        public float movementNoise = 0.3f;

        [Range(0f, 1f)]
        [Tooltip("Visual concealment level (0 = fully exposed, 1 = fully hidden)")]
        public float concealmentLevel = 0.5f;

        [Header("Shelter")]
        [Range(0f, 1f)]
        [Tooltip("Rain protection (0 = none, 1 = full)")]
        public float rainProtection = 0f;

        [Range(0f, 1f)]
        [Tooltip("Wind protection (0 = none, 1 = full)")]
        public float windProtection = 0f;

        [Tooltip("Can the player build a shelter here")]
        public bool canBuildShelter = false;

        [Tooltip("Can the player light a fire here")]
        public bool canLightFire = false;

        [Header("Hazards")]
        [Tooltip("Risk of ankle injury on this terrain")]
        [Range(0f, 1f)]
        public float ankleInjuryRisk = 0f;

        [Tooltip("Risk of falling on this terrain")]
        [Range(0f, 1f)]
        public float fallRisk = 0f;

        [Tooltip("Mosquito density in summer (0 = none)")]
        [Range(0f, 1f)]
        public float mosquitoDensity = 0f;
    }

    /// <summary>
    /// Manages terrain zones, surface conditions, and terrain queries for
    /// the Jura Krakowsko-Czestochowska game world.
    ///
    /// Scenario references:
    /// - "sliska sciolka lesna i kawalki kamieni wapiennych ktore sa sliskie" (cz.3)
    /// - "tereny podgorskie Jury Krakowsko-Czestochowskiej Ojcowski Park Narodowy" (cz.4)
    /// - "gruba pokrywa sniezna zakrywajaca sciolke lesna" (cz.4)
    /// - "oblodzenie skalek" in winter (cz.4)
    /// </summary>
    public class TerrainManager : MonoBehaviour
    {
        public static TerrainManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private TextAsset terrainConfigJson;

        [Header("Runtime State")]
        [SerializeField] private float currentRainIntensity = 0f;
        [SerializeField] private float currentSnowCoverage = 0f;
        [SerializeField] private float currentTemperature = 15f;
        [SerializeField] private bool isGroundFrozen = false;

        private Dictionary<TerrainType, TerrainProperties> defaultProperties;
        private TerrainConfig config;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeDefaultProperties();
            LoadConfig();
        }

        /// <summary>
        /// Load terrain configuration from JSON.
        /// </summary>
        private void LoadConfig()
        {
            if (terrainConfigJson != null)
            {
                config = JsonUtility.FromJson<TerrainConfig>(terrainConfigJson.text);
                Debug.Log("[TerrainManager] Loaded terrain config for region: " + config.regionName);
            }
            else
            {
                config = TerrainConfig.CreateDefault();
                Debug.LogWarning("[TerrainManager] No terrain config assigned, using defaults.");
            }
        }

        /// <summary>
        /// Initialize default terrain properties for each terrain type.
        /// Values based on Jura KCz survivalist scenario descriptions.
        /// </summary>
        private void InitializeDefaultProperties()
        {
            defaultProperties = new Dictionary<TerrainType, TerrainProperties>();

            // Mixed forest - beech, hazel, birch with seasonal leaf cover
            defaultProperties[TerrainType.Forest_Mixed] = new TerrainProperties
            {
                terrainType = TerrainType.Forest_Mixed,
                surfaceType = SurfaceType.ForestFloor,
                baseMovementMultiplier = 0.75f,
                drySlipChance = 0.05f,
                wetSlipChance = 0.25f,   // "sliska sciolka lesna"
                icySlipChance = 0.40f,
                movementNoise = 0.4f,     // Leaves and branches
                concealmentLevel = 0.7f,  // Good cover with underbrush
                rainProtection = 0.3f,    // Partial canopy
                windProtection = 0.4f,
                canBuildShelter = true,    // "budowac z zwalonych galezi drzew szalasy"
                canLightFire = true,
                ankleInjuryRisk = 0.15f,  // Hidden branches under leaves
                fallRisk = 0.10f,
                mosquitoDensity = 0.3f
            };

            // Coniferous forest - spruce and fir, year-round cover
            defaultProperties[TerrainType.Forest_Coniferous] = new TerrainProperties
            {
                terrainType = TerrainType.Forest_Coniferous,
                surfaceType = SurfaceType.ForestFloor,
                baseMovementMultiplier = 0.70f,
                drySlipChance = 0.05f,
                wetSlipChance = 0.20f,
                icySlipChance = 0.35f,
                movementNoise = 0.3f,     // Needles are quieter than leaves
                concealmentLevel = 0.8f,  // Dense year-round
                rainProtection = 0.5f,    // "korony drzew laczace sie tworząc naturalny dach"
                windProtection = 0.5f,
                canBuildShelter = true,    // "spac pod zwalonymi drzewami jodly lub swierki"
                canLightFire = true,
                ankleInjuryRisk = 0.10f,
                fallRisk = 0.08f,
                mosquitoDensity = 0.2f
            };

            // Limestone rocks - characteristic Jura KCz formations
            defaultProperties[TerrainType.LimestoneRocks] = new TerrainProperties
            {
                terrainType = TerrainType.LimestoneRocks,
                surfaceType = SurfaceType.Limestone,
                baseMovementMultiplier = 0.50f,
                drySlipChance = 0.10f,
                wetSlipChance = 0.45f,   // "kawalki kamieni wapiennych ktore sa sliskie"
                icySlipChance = 0.70f,   // "oblodzenie skalek"
                movementNoise = 0.2f,
                concealmentLevel = 0.3f,
                rainProtection = 0f,
                windProtection = 0.2f,
                canBuildShelter = false,
                canLightFire = false,
                ankleInjuryRisk = 0.35f, // "skrecic koste lub zlamac reke lub noge"
                fallRisk = 0.40f,        // "potluczen otarc i skaleczeń"
                mosquitoDensity = 0f
            };

            // Caves - shelter but dark and potentially dangerous
            defaultProperties[TerrainType.Cave] = new TerrainProperties
            {
                terrainType = TerrainType.Cave,
                surfaceType = SurfaceType.Limestone,
                baseMovementMultiplier = 0.40f,
                drySlipChance = 0.15f,
                wetSlipChance = 0.30f,
                icySlipChance = 0.20f,  // Caves are warmer than outside
                movementNoise = 0.5f,    // Echoes
                concealmentLevel = 0.9f,
                rainProtection = 1f,     // Full rain protection
                windProtection = 0.8f,
                canBuildShelter = false,
                canLightFire = true,     // Can light fire at entrance
                ankleInjuryRisk = 0.20f,
                fallRisk = 0.25f,
                mosquitoDensity = 0f
            };

            // Streams - water source, fish, but wet and cold
            defaultProperties[TerrainType.Stream] = new TerrainProperties
            {
                terrainType = TerrainType.Stream,
                surfaceType = SurfaceType.Water_Shallow,
                baseMovementMultiplier = 0.35f,
                drySlipChance = 0.30f,
                wetSlipChance = 0.40f,
                icySlipChance = 0.60f,
                movementNoise = 0.6f,    // Splashing
                concealmentLevel = 0.2f,
                rainProtection = 0f,
                windProtection = 0f,
                canBuildShelter = false,
                canLightFire = false,
                ankleInjuryRisk = 0.25f,
                fallRisk = 0.30f,
                mosquitoDensity = 0.5f
            };

            // Forest paths - best movement corridors but more exposed
            defaultProperties[TerrainType.Path_Forest] = new TerrainProperties
            {
                terrainType = TerrainType.Path_Forest,
                surfaceType = SurfaceType.Gravel,
                baseMovementMultiplier = 1.0f,
                drySlipChance = 0.02f,
                wetSlipChance = 0.10f,
                icySlipChance = 0.25f,
                movementNoise = 0.5f,
                concealmentLevel = 0.3f,
                rainProtection = 0.1f,
                windProtection = 0.2f,
                canBuildShelter = false,
                canLightFire = false,
                ankleInjuryRisk = 0.05f,
                fallRisk = 0.05f,
                mosquitoDensity = 0.1f
            };

            // Mountain/highland paths - Jura KCz hill trails
            defaultProperties[TerrainType.Path_Mountain] = new TerrainProperties
            {
                terrainType = TerrainType.Path_Mountain,
                surfaceType = SurfaceType.Limestone,
                baseMovementMultiplier = 0.65f,
                drySlipChance = 0.08f,
                wetSlipChance = 0.35f,
                icySlipChance = 0.55f,
                movementNoise = 0.3f,
                concealmentLevel = 0.2f,
                rainProtection = 0f,
                windProtection = 0.1f,
                canBuildShelter = false,
                canLightFire = false,
                ankleInjuryRisk = 0.25f,
                fallRisk = 0.30f,
                mosquitoDensity = 0f
            };

            // Clearings - open areas, foraging but exposed
            defaultProperties[TerrainType.Clearing] = new TerrainProperties
            {
                terrainType = TerrainType.Clearing,
                surfaceType = SurfaceType.Grass,
                baseMovementMultiplier = 0.90f,
                drySlipChance = 0.02f,
                wetSlipChance = 0.08f,
                icySlipChance = 0.15f,
                movementNoise = 0.2f,
                concealmentLevel = 0.1f,  // Very exposed
                rainProtection = 0f,
                windProtection = 0f,
                canBuildShelter = true,
                canLightFire = true,
                ankleInjuryRisk = 0.05f,
                fallRisk = 0.03f,
                mosquitoDensity = 0.4f
            };

            // Marshland - wet terrain, berries in spring/summer
            defaultProperties[TerrainType.Marshland] = new TerrainProperties
            {
                terrainType = TerrainType.Marshland,
                surfaceType = SurfaceType.Mud,
                baseMovementMultiplier = 0.40f,
                drySlipChance = 0.15f,
                wetSlipChance = 0.35f,
                icySlipChance = 0.20f,
                movementNoise = 0.5f,
                concealmentLevel = 0.4f,
                rainProtection = 0f,
                windProtection = 0f,
                canBuildShelter = false,
                canLightFire = false,
                ankleInjuryRisk = 0.20f,
                fallRisk = 0.15f,
                mosquitoDensity = 0.9f  // "unikac terenow podmoklych blota"
            };

            // Village edge - fields for foraging
            defaultProperties[TerrainType.Village_Edge] = new TerrainProperties
            {
                terrainType = TerrainType.Village_Edge,
                surfaceType = SurfaceType.Grass,
                baseMovementMultiplier = 0.85f,
                drySlipChance = 0.03f,
                wetSlipChance = 0.10f,
                icySlipChance = 0.15f,
                movementNoise = 0.3f,
                concealmentLevel = 0.2f,
                rainProtection = 0f,
                windProtection = 0.1f,
                canBuildShelter = false,
                canLightFire = false,
                ankleInjuryRisk = 0.05f,
                fallRisk = 0.03f,
                mosquitoDensity = 0.3f
            };

            // Urban ruins - loot but structural danger
            defaultProperties[TerrainType.Urban_Ruins] = new TerrainProperties
            {
                terrainType = TerrainType.Urban_Ruins,
                surfaceType = SurfaceType.Rubble,
                baseMovementMultiplier = 0.55f,
                drySlipChance = 0.10f,
                wetSlipChance = 0.20f,
                icySlipChance = 0.30f,
                movementNoise = 0.6f,    // Rubble crunching
                concealmentLevel = 0.6f,
                rainProtection = 0.4f,   // Partial walls
                windProtection = 0.5f,
                canBuildShelter = false,
                canLightFire = true,     // "opal z zburzonych mieszkan meble IKEA"
                ankleInjuryRisk = 0.25f,
                fallRisk = 0.20f,
                mosquitoDensity = 0.1f
            };

            // Agricultural fields
            defaultProperties[TerrainType.Field_Agricultural] = new TerrainProperties
            {
                terrainType = TerrainType.Field_Agricultural,
                surfaceType = SurfaceType.Mud,
                baseMovementMultiplier = 0.70f,
                drySlipChance = 0.05f,
                wetSlipChance = 0.20f,
                icySlipChance = 0.15f,
                movementNoise = 0.3f,
                concealmentLevel = 0.3f, // Crops provide some cover in summer
                rainProtection = 0f,
                windProtection = 0f,
                canBuildShelter = false,
                canLightFire = false,
                ankleInjuryRisk = 0.08f,
                fallRisk = 0.05f,
                mosquitoDensity = 0.3f
            };
        }

        /// <summary>
        /// Get the effective terrain properties at a world position,
        /// accounting for current weather and season conditions.
        /// </summary>
        public TerrainProperties GetEffectiveProperties(TerrainType terrainType)
        {
            if (!defaultProperties.ContainsKey(terrainType))
            {
                Debug.LogError($"[TerrainManager] Unknown terrain type: {terrainType}");
                return defaultProperties[TerrainType.Forest_Mixed];
            }

            TerrainProperties baseProps = defaultProperties[terrainType];
            TerrainProperties effective = CloneProperties(baseProps);

            ApplyWeatherModifiers(effective);
            ApplySnowModifiers(effective);

            return effective;
        }

        /// <summary>
        /// Get the default (no weather) properties for a terrain type.
        /// </summary>
        public TerrainProperties GetBaseProperties(TerrainType terrainType)
        {
            if (defaultProperties.ContainsKey(terrainType))
                return defaultProperties[terrainType];

            Debug.LogWarning($"[TerrainManager] No properties for {terrainType}, returning Forest_Mixed defaults.");
            return defaultProperties[TerrainType.Forest_Mixed];
        }

        /// <summary>
        /// Update weather state from EnvironmentManager.
        /// Called each frame or on weather change.
        /// </summary>
        public void UpdateWeatherState(float rainIntensity, float snowCoverage, float temperature)
        {
            currentRainIntensity = Mathf.Clamp01(rainIntensity);
            currentSnowCoverage = Mathf.Clamp01(snowCoverage);
            currentTemperature = temperature;
            isGroundFrozen = temperature < 0f;
        }

        /// <summary>
        /// Calculate the current slip chance for a terrain type,
        /// factoring in weather. Core mechanic from scenarios:
        /// "sliska sciolka lesna i kawalki kamieni wapiennych"
        /// </summary>
        public float GetSlipChance(TerrainType terrainType)
        {
            TerrainProperties props = GetEffectiveProperties(terrainType);

            if (isGroundFrozen)
                return props.icySlipChance;
            else if (currentRainIntensity > 0.1f)
                return Mathf.Lerp(props.drySlipChance, props.wetSlipChance, currentRainIntensity);
            else
                return props.drySlipChance;
        }

        /// <summary>
        /// Calculate effective movement speed multiplier.
        /// Deep snow significantly reduces speed (scenario: exhaustion from walking in deep snow).
        /// </summary>
        public float GetMovementMultiplier(TerrainType terrainType)
        {
            TerrainProperties props = GetEffectiveProperties(terrainType);
            float multiplier = props.baseMovementMultiplier;

            // Deep snow penalty: "chodzenie w glebokim sniegu"
            if (currentSnowCoverage > 0.5f)
            {
                float snowPenalty = Mathf.Lerp(0f, 0.4f, (currentSnowCoverage - 0.5f) * 2f);
                multiplier -= snowPenalty;
            }

            // Mud penalty in rain
            if (props.surfaceType == SurfaceType.Mud && currentRainIntensity > 0.3f)
            {
                multiplier *= 0.7f;
            }

            return Mathf.Max(0.15f, multiplier);
        }

        /// <summary>
        /// Check if a terrain zone is suitable for overnight shelter.
        /// Based on scenario guidance about sleeping locations.
        /// </summary>
        public bool IsSuitableForShelter(TerrainType terrainType)
        {
            TerrainProperties props = GetBaseProperties(terrainType);
            return props.canBuildShelter || props.rainProtection > 0.7f;
        }

        /// <summary>
        /// Get injury risk for traversing terrain, combining ankle and fall risks.
        /// Increased at night and in bad weather per scenario descriptions.
        /// </summary>
        public float GetInjuryRisk(TerrainType terrainType, bool isNight)
        {
            TerrainProperties props = GetEffectiveProperties(terrainType);
            float risk = (props.ankleInjuryRisk + props.fallRisk) * 0.5f;

            // Night movement is much more dangerous on rocky terrain
            // "Noce poruszanie sie utrudnione ze wzgledu na oblodzenie skalek"
            if (isNight)
            {
                risk *= 2.0f;
                if (props.surfaceType == SurfaceType.Limestone)
                    risk *= 1.5f;
            }

            // Weather increases risk
            if (currentRainIntensity > 0.3f)
                risk *= 1.3f;
            if (isGroundFrozen)
                risk *= 1.5f;

            return Mathf.Clamp01(risk);
        }

        /// <summary>
        /// Apply rain/weather modifiers to terrain properties.
        /// </summary>
        private void ApplyWeatherModifiers(TerrainProperties props)
        {
            if (currentRainIntensity > 0.1f)
            {
                // Rain increases noise from splashing but reduces visibility
                props.movementNoise *= (1f + currentRainIntensity * 0.3f);

                // Rain masks movement sounds at high intensity
                if (currentRainIntensity > 0.6f)
                    props.movementNoise *= 0.7f;

                // Wet ground is slower
                props.baseMovementMultiplier *= Mathf.Lerp(1f, 0.8f, currentRainIntensity);
            }
        }

        /// <summary>
        /// Apply snow coverage modifiers.
        /// Scenario: "gruba pokrywa sniezna zakrywajaca sciolke lesna
        /// przykryte galezie i resztki kamieni wapiennych"
        /// </summary>
        private void ApplySnowModifiers(TerrainProperties props)
        {
            if (currentSnowCoverage > 0.1f)
            {
                // Snow hides hazards underneath
                props.ankleInjuryRisk += currentSnowCoverage * 0.15f;

                // Snow muffles some sounds but leaves tracks
                props.movementNoise *= (1f - currentSnowCoverage * 0.2f);

                // Concealment reduced - tracks in snow
                props.concealmentLevel *= (1f - currentSnowCoverage * 0.3f);
            }
        }

        /// <summary>
        /// Deep clone a TerrainProperties instance.
        /// </summary>
        private TerrainProperties CloneProperties(TerrainProperties source)
        {
            return new TerrainProperties
            {
                terrainType = source.terrainType,
                surfaceType = source.surfaceType,
                baseMovementMultiplier = source.baseMovementMultiplier,
                drySlipChance = source.drySlipChance,
                wetSlipChance = source.wetSlipChance,
                icySlipChance = source.icySlipChance,
                movementNoise = source.movementNoise,
                concealmentLevel = source.concealmentLevel,
                rainProtection = source.rainProtection,
                windProtection = source.windProtection,
                canBuildShelter = source.canBuildShelter,
                canLightFire = source.canLightFire,
                ankleInjuryRisk = source.ankleInjuryRisk,
                fallRisk = source.fallRisk,
                mosquitoDensity = source.mosquitoDensity
            };
        }
    }

    /// <summary>
    /// Serializable configuration loaded from TerrainConfig.json.
    /// Holds Jura KCz specific parameters.
    /// </summary>
    [Serializable]
    public class TerrainConfig
    {
        public string regionName;
        public float mapSizeKm;
        public float minElevationM;
        public float maxElevationM;
        public float forestCoveragePercent;
        public float limestoneCoveragePercent;
        public float waterBodyPercent;
        public float averageTreeDensityPerHectare;
        public float caveFrequencyPerKm2;

        public static TerrainConfig CreateDefault()
        {
            return new TerrainConfig
            {
                regionName = "Jura Krakowsko-Czestochowska",
                mapSizeKm = 20f,
                minElevationM = 220f,
                maxElevationM = 515f,
                forestCoveragePercent = 55f,
                limestoneCoveragePercent = 15f,
                waterBodyPercent = 5f,
                averageTreeDensityPerHectare = 400f,
                caveFrequencyPerKm2 = 2.5f
            };
        }
    }
}
