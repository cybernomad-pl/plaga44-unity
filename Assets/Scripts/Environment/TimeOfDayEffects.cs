using System;
using UnityEngine;
using Plaga44.Terrain;

namespace Plaga44.Environment
{
    /// <summary>
    /// NPC alertness states affected by time of day.
    /// </summary>
    public enum NPCAlertness
    {
        Sleeping,       // 1:00 - 4:00 - lowest alertness
        Drowsy,         // 4:00 - 6:00 and 23:00 - 1:00
        Normal,         // 6:00 - 18:00
        Alert,          // 18:00 - 23:00 - dusk vigilance
        HighAlert       // During combat or after detection
    }

    /// <summary>
    /// Manages time-of-day effects on visibility, sound propagation,
    /// NPC behavior, and movement safety.
    ///
    /// Key scenario references:
    /// - "poruszanie sie noca bez uzywanie latarek" (cz.1)
    /// - "dokonywanie wejsc w godzinach rannych miedzy 3 a 5 rano" (cz.1)
    /// - "jesienia wczesniej robi sie ciemno - od godziny 6:00 do 19-tej" (cz.3)
    /// - "noce poruszanie sie po lesie utrudnione ze wzgledu na oblodzenie
    ///    skalek i gruba pokrywe sniezna" (cz.4)
    /// - "szukanie miejsca do noclegu od 16-tej godziny maksymalnie" (cz.4)
    /// - "marsze nocne od 4 do 10 kilometrow terenami lesnymi
    ///    w poblizu glownych drog" (cz.1)
    /// </summary>
    public class TimeOfDayEffects : MonoBehaviour
    {
        public static TimeOfDayEffects Instance { get; private set; }

        [Header("Visibility Settings")]
        [Tooltip("Base visibility range in meters during full daylight")]
        [SerializeField] private float baseDaylightVisibilityM = 200f;

        [Tooltip("Minimum visibility at night without flashlight")]
        [SerializeField] private float baseNightVisibilityM = 15f;

        [Tooltip("Flashlight visibility range")]
        [SerializeField] private float flashlightRangeM = 40f;

        [Header("Sound Settings")]
        [Tooltip("Sound propagation multiplier at night (sound travels further)")]
        [SerializeField] private float nightSoundMultiplier = 1.5f;

        [Tooltip("Sound propagation multiplier during rain")]
        [SerializeField] private float rainSoundDampening = 0.6f;

        [Header("NPC Settings")]
        [Tooltip("NPC detection range multiplier at night")]
        [SerializeField] private float npcNightDetectionMultiplier = 0.3f;

        [Tooltip("NPC detection range when player uses flashlight at night")]
        [SerializeField] private float npcFlashlightDetectionMultiplier = 2.5f;

        [Header("Movement Safety")]
        [Tooltip("Injury chance multiplier for night movement")]
        [SerializeField] private float nightInjuryMultiplier = 2.0f;

        [Tooltip("Night movement speed penalty (without flashlight)")]
        [SerializeField] private float nightMovementPenalty = 0.5f;

        // Cached references
        private EnvironmentManager environment;
        private TerrainManager terrain;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            environment = EnvironmentManager.Instance;
            terrain = TerrainManager.Instance;
        }

        // =====================================================================
        // VISIBILITY
        // =====================================================================

        /// <summary>
        /// Get the current effective visibility range in meters.
        /// Depends on time of day, weather, terrain, and flashlight usage.
        /// </summary>
        public float GetVisibilityRange(bool usingFlashlight, TerrainType terrainType)
        {
            if (environment == null) return baseDaylightVisibilityM;

            float lightLevel = environment.GetAmbientLightLevel();
            float baseVisibility = Mathf.Lerp(baseNightVisibilityM, baseDaylightVisibilityM, lightLevel);

            // Flashlight in darkness
            // "poruszanie sie noca bez uzywania latarek" - using flashlight is risky
            if (usingFlashlight && !environment.IsDaytime)
            {
                baseVisibility = Mathf.Max(baseVisibility, flashlightRangeM);
            }

            // Terrain modifiers
            baseVisibility *= GetTerrainVisibilityMultiplier(terrainType);

            // Weather modifiers
            baseVisibility *= GetWeatherVisibilityMultiplier();

            // Snow on ground increases night visibility slightly (reflection)
            if (!environment.IsDaytime && environment.SnowCoverage > 0.3f)
            {
                baseVisibility *= 1.2f;
            }

            return Mathf.Max(5f, baseVisibility);
        }

        /// <summary>
        /// Get visibility multiplier based on terrain type.
        /// Forest is more restrictive than open terrain.
        /// </summary>
        private float GetTerrainVisibilityMultiplier(TerrainType terrainType)
        {
            switch (terrainType)
            {
                case TerrainType.Forest_Coniferous: return 0.4f;  // Dense, year-round
                case TerrainType.Forest_Mixed:
                    // Seasonal: leafy in summer, bare in winter
                    if (environment != null)
                    {
                        switch (environment.CurrentSeason)
                        {
                            case Season.Summer: return 0.4f;
                            case Season.Autumn: return 0.5f;  // Leaves falling
                            case Season.Winter: return 0.7f;  // Bare branches
                            case Season.Spring: return 0.5f;  // Growing back
                        }
                    }
                    return 0.5f;
                case TerrainType.Cave: return 0.1f;           // Very dark without light
                case TerrainType.Clearing: return 1.0f;
                case TerrainType.LimestoneRocks: return 0.8f;
                case TerrainType.Stream: return 0.9f;
                case TerrainType.Path_Forest: return 0.6f;
                case TerrainType.Path_Mountain: return 0.85f;
                case TerrainType.Marshland: return 0.7f;
                case TerrainType.Village_Edge: return 0.9f;
                case TerrainType.Urban_Ruins: return 0.5f;
                case TerrainType.Field_Agricultural: return 1.0f;
                default: return 0.7f;
            }
        }

        /// <summary>
        /// Get visibility multiplier based on current weather.
        /// </summary>
        private float GetWeatherVisibilityMultiplier()
        {
            if (environment == null) return 1f;

            switch (environment.CurrentWeather)
            {
                case WeatherType.Clear: return 1.0f;
                case WeatherType.Cloudy: return 0.9f;
                case WeatherType.Rain_Light: return 0.7f;
                case WeatherType.Rain_Heavy: return 0.4f;
                case WeatherType.Rain_With_Snow: return 0.45f;
                case WeatherType.Snow_Light: return 0.6f;
                case WeatherType.Snow_Heavy: return 0.3f;
                case WeatherType.Fog: return 0.2f;  // "mgla" - severe visibility reduction
                case WeatherType.Frost: return 0.85f;
                default: return 0.8f;
            }
        }

        // =====================================================================
        // SOUND PROPAGATION
        // =====================================================================

        /// <summary>
        /// Get the effective sound propagation distance multiplier.
        /// Sound travels further at night and in open areas,
        /// dampened by rain and dense forest.
        /// </summary>
        public float GetSoundPropagationMultiplier(TerrainType terrainType)
        {
            float multiplier = 1f;

            // Time of day - night sounds carry further
            if (environment != null && !environment.IsDaytime)
            {
                multiplier *= nightSoundMultiplier;
            }

            // Weather - rain dampens sound
            if (environment != null)
            {
                switch (environment.CurrentWeather)
                {
                    case WeatherType.Rain_Light:
                        multiplier *= 0.8f;
                        break;
                    case WeatherType.Rain_Heavy:
                        multiplier *= rainSoundDampening;
                        // Heavy rain actually provides sound cover
                        break;
                    case WeatherType.Snow_Heavy:
                        multiplier *= 0.7f; // Snow muffles sound
                        break;
                }

                // Snow on ground absorbs sound
                if (environment.SnowCoverage > 0.3f)
                {
                    multiplier *= 0.8f;
                }
            }

            // Terrain type
            switch (terrainType)
            {
                case TerrainType.Forest_Coniferous:
                case TerrainType.Forest_Mixed:
                    multiplier *= 0.6f; // Forest absorbs sound
                    break;
                case TerrainType.Cave:
                    multiplier *= 2.0f; // Echoes in caves
                    break;
                case TerrainType.Clearing:
                case TerrainType.Field_Agricultural:
                    multiplier *= 1.3f; // Open areas carry sound
                    break;
                case TerrainType.Urban_Ruins:
                    multiplier *= 1.1f; // Reflections off walls
                    break;
                case TerrainType.LimestoneRocks:
                    multiplier *= 1.2f; // Rock reflects sound
                    break;
            }

            return multiplier;
        }

        /// <summary>
        /// Calculate how far a specific noise will travel in meters.
        /// </summary>
        public float CalculateNoiseRadius(float baseNoiseLevel, TerrainType terrainType)
        {
            float baseRadius = baseNoiseLevel * 50f; // 0-1 noise -> 0-50m base
            return baseRadius * GetSoundPropagationMultiplier(terrainType);
        }

        // =====================================================================
        // NPC BEHAVIOR
        // =====================================================================

        /// <summary>
        /// Get the current NPC alertness state based on time of day.
        /// Scenario: "dokonywanie wejsc w godzinach rannych miedzy 3 a 5 rano"
        /// - best time to move/loot because NPCs/enemies are least alert.
        /// </summary>
        public NPCAlertness GetNPCAlertness()
        {
            if (environment == null) return NPCAlertness.Normal;

            float hour = environment.CurrentHour;

            if (hour >= 1f && hour < 4f)
                return NPCAlertness.Sleeping;      // Deep sleep hours
            else if (hour >= 4f && hour < 6f)
                return NPCAlertness.Drowsy;         // Pre-dawn, lowest guard
            else if (hour >= 6f && hour < 18f)
                return NPCAlertness.Normal;
            else if (hour >= 18f && hour < 23f)
                return NPCAlertness.Alert;          // Evening vigilance
            else
                return NPCAlertness.Drowsy;         // 23:00 - 1:00
        }

        /// <summary>
        /// Get the NPC detection range multiplier based on current conditions.
        /// Lower = harder for NPCs to detect the player.
        /// </summary>
        public float GetNPCDetectionMultiplier(bool playerUsingFlashlight)
        {
            float multiplier = 1f;

            if (environment == null) return multiplier;

            // Time of day
            NPCAlertness alertness = GetNPCAlertness();
            switch (alertness)
            {
                case NPCAlertness.Sleeping:
                    multiplier = 0.1f;
                    break;
                case NPCAlertness.Drowsy:
                    multiplier = npcNightDetectionMultiplier;
                    break;
                case NPCAlertness.Normal:
                    multiplier = 1f;
                    break;
                case NPCAlertness.Alert:
                    multiplier = 1.3f;
                    break;
                case NPCAlertness.HighAlert:
                    multiplier = 2f;
                    break;
            }

            // Flashlight at night draws massive attention
            // "poruszanie sie noca bez uzywania latarek"
            if (playerUsingFlashlight && !environment.IsDaytime)
            {
                multiplier *= npcFlashlightDetectionMultiplier;
            }

            // Weather affects detection
            switch (environment.CurrentWeather)
            {
                case WeatherType.Rain_Heavy:
                    multiplier *= 0.5f; // Rain provides cover
                    break;
                case WeatherType.Fog:
                    multiplier *= 0.3f; // Fog severely limits detection
                    break;
                case WeatherType.Snow_Heavy:
                    multiplier *= 0.4f;
                    break;
            }

            return multiplier;
        }

        /// <summary>
        /// Check if the current time window is optimal for stealth operations.
        /// "dokonywanie wejsc w godzinach rannych miedzy 3 a 5 rano" (cz.1)
        /// "rozpoznanie terenu z daleka" (cz.1)
        /// </summary>
        public bool IsOptimalStealthWindow()
        {
            if (environment == null) return false;

            float hour = environment.CurrentHour;
            return hour >= 3f && hour <= 5f;
        }

        /// <summary>
        /// Get recommended activity for current time of day based on scenarios.
        /// </summary>
        public string GetActivityRecommendation()
        {
            if (environment == null) return "";

            float hour = environment.CurrentHour;
            Season season = environment.CurrentSeason;

            // Night (most seasons): 21:00 - 4:00
            if (hour >= 21f || hour < 3f)
            {
                return "Noc - odpoczynek, spo\u017cycie ciep\u0142ej herbaty z termosu. " +
                       "Nie poruszaj si\u0119 po terenie leśnym.";
            }

            // Best stealth window: 3:00 - 5:00
            if (hour >= 3f && hour < 5f)
            {
                return "Optymalny czas na operacje: wej\u015bcia do budynk\u00f3w, " +
                       "rozpoznanie terenu, przemieszczanie si\u0119.";
            }

            // Early morning: 5:00 - 6:00
            if (hour >= 5f && hour < 6f)
            {
                return "Wczesny ranek - zako\u0144cz operacje nocne, " +
                       "znajd\u017a kryjówk\u0119 lub rozpocznij marsz.";
            }

            // Morning march: 6:00 - 10:00
            if (hour >= 6f && hour < 10f)
            {
                return "Rano - czas na marsz. Jedz śniadanie, pij ciep\u0142e napoje.";
            }

            // Midday (dangerous in summer): 10:00 - 14:00
            if (hour >= 10f && hour < 14f)
            {
                if (season == Season.Summer)
                    return "Po\u0142udnie letnie - UWAGA na udar s\u0142oneczny! " +
                           "Poruszaj si\u0119 w cieniu, pij wod\u0119!";
                return "Po\u0142udnie - kontynuuj marsz z przerwami co 2-3 godziny.";
            }

            // Afternoon: 14:00 - 16:00
            if (hour >= 14f && hour < 16f)
            {
                return "Popołudnie - planuj postój. Zr\u00f3b przerw\u0119, " +
                       "wymie\u0144 skarpety, opatrz odciski.";
            }

            // Pre-dusk: 16:00 - 19:00
            if (hour >= 16f && hour < 19f)
            {
                float sunset = environment.SunsetHour;
                if (hour >= sunset - 1f)
                    return "Szukaj miejsca na nocleg! Zbieraj opa\u0142, buduj schronienie.";
                return "P\u00f3\u017ane popołudnie - szukaj miejsca do spania " +
                       "od 16:00 maksymalnie.";
            }

            // Evening: 19:00 - 21:00
            if (hour >= 19f && hour < 21f)
            {
                return "Wiecz\u00f3r - rozpal ognisko, przygotuj posi\u0142ek, " +
                       "słuchaj wiadomo\u015bci radiowych.";
            }

            return "";
        }

        // =====================================================================
        // MOVEMENT SAFETY
        // =====================================================================

        /// <summary>
        /// Get the current movement safety rating (0 = very dangerous, 1 = safe).
        /// Combines time of day, terrain, and weather effects.
        ///
        /// "noce poruszanie sie po lesie i terenach podgorskich utrudnione
        ///  ze wzgledu na oblodzenie skalek i gruba pokrywe sniezna" (cz.4)
        /// </summary>
        public float GetMovementSafety(TerrainType terrainType)
        {
            float safety = 1f;

            if (environment == null) return safety;

            // Night penalty
            if (!environment.IsDaytime)
            {
                safety *= 0.4f;

                // Limestone terrain at night is especially dangerous
                if (terrainType == TerrainType.LimestoneRocks ||
                    terrainType == TerrainType.Path_Mountain)
                {
                    safety *= 0.5f;
                }
            }

            // Weather penalties
            if (environment.RainIntensity > 0.3f)
                safety *= 0.7f;

            if (environment.SnowCoverage > 0.5f)
                safety *= 0.6f;

            if (environment.CurrentTemperature < -5f)
                safety *= 0.7f; // Icy conditions

            // Fog
            if (environment.IsFoggy)
                safety *= 0.5f;

            return Mathf.Clamp01(safety);
        }

        /// <summary>
        /// Get the current movement speed modifier from time-of-day effects.
        /// Night movement is slower, especially without flashlight.
        /// </summary>
        public float GetTimeBasedMovementMultiplier(bool usingFlashlight)
        {
            if (environment == null) return 1f;

            if (environment.IsDaytime) return 1f;

            // Night without flashlight - very slow and cautious
            if (!usingFlashlight)
                return nightMovementPenalty;

            // Night with flashlight - faster but detectable
            return 0.75f;
        }

        /// <summary>
        /// Should the player seek shelter now? Based on scenario timing guidelines.
        /// "szukanie miejsca do noclegu od 16-tej godziny maksymalnie" (cz.4)
        /// </summary>
        public bool ShouldSeekShelter()
        {
            if (environment == null) return false;

            float hour = environment.CurrentHour;
            float sunset = environment.SunsetHour;

            // In autumn and winter, start looking earlier
            float seekHour = Mathf.Min(16f, sunset - 2f);

            return hour >= seekHour && hour < sunset;
        }

        /// <summary>
        /// Is it currently safe to light a fire without excessive detection risk?
        /// Night fires are visible from far away, but needed for warmth.
        /// </summary>
        public float GetFireDetectionRisk()
        {
            if (environment == null) return 0.5f;

            float risk = 0.3f; // Base daytime risk

            if (!environment.IsDaytime)
            {
                risk = 0.8f; // Fire visible at night

                // Weather provides some cover
                if (environment.RainIntensity > 0.5f)
                    risk *= 0.6f;
                if (environment.IsFoggy)
                    risk *= 0.4f;
            }

            return Mathf.Clamp01(risk);
        }
    }
}
