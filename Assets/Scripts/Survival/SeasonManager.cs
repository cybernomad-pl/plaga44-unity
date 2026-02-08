// PLAGA '44 VR - SeasonManager
// Tracks current season, weather conditions, temperature, and day/night cycle.
// Drives all other survival systems with environmental context.
//
// From scenario docs (Gra_scenariusz parts 1-7):
// - "Zima jest najtrudniejsza pora roku do przezycia w warunkach wojny"
// - "Latem w lesie jest cieplo dziki czemu mozna spac bez ryzyka zamarznicia"
// - "Jesienia w lesie wczesniej robi sie ciemno"
// - "Wiosna zwieksza szanse przezycia z uwagi na wzrost temperatur"
//
// Architecture: Central MonoBehaviour that updates once per frame.
// Other survival systems subscribe to season/weather change events.
// Integrates with PhysiologyController via SetEnvironment().

using System;
using UnityEngine;

namespace Plaga44.Survival
{
    using Plaga44.Physiology;

    /// <summary>
    /// Manages seasonal progression, weather generation, and environmental
    /// state for the survival simulation. Attach to a persistent game object.
    /// </summary>
    public class SeasonManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private SeasonConfig config;

        [Header("Time")]
        [Tooltip("Current game time in hours (0-24). Drives day/night cycle.")]
        [Range(0f, 24f)]
        [SerializeField] private float currentTimeOfDay = 8f;

        [Tooltip("Current day number since game start.")]
        [SerializeField] private int currentDay = 1;

        [Tooltip("Game hours per real second. Synced with PhysiologyConfig.")]
        [SerializeField] private float gameHoursPerRealSecond = 0.01f;

        [Header("Current Season State")]
        [SerializeField] private Season currentSeason = Season.Summer;

        [Header("Current Weather State")]
        [SerializeField] private WeatherCondition currentWeather = WeatherCondition.Clear;
        [SerializeField] private float currentTemperature = 20f;
        [SerializeField] private float currentWindSpeed = 0f;
        [SerializeField] private float currentPrecipitation = 0f;
        [SerializeField] private float currentHumidity = 0.5f;
        [SerializeField] private float currentCloudCover = 0f;
        [SerializeField] private bool isDaytime = true;

        [Header("References")]
        [SerializeField] private PhysiologyController physiologyController;

        // Weather transition tracking
        private float weatherChangeTimer = 0f;
        private float nextWeatherChangeInterval = 2f; // game hours
        private WeatherCondition targetWeather;
        private float targetTemperature;
        private float weatherTransitionProgress = 1f;

        // Events
        public event Action<Season> OnSeasonChanged;
        public event Action<WeatherCondition> OnWeatherChanged;
        public event Action<bool> OnDayNightChanged;
        public event Action<EnvironmentSnapshot> OnEnvironmentUpdated;

        // Public accessors
        public Season CurrentSeason => currentSeason;
        public WeatherCondition CurrentWeather => currentWeather;
        public float CurrentTemperature => currentTemperature;
        public float CurrentWindSpeed => currentWindSpeed;
        public float CurrentPrecipitation => currentPrecipitation;
        public float CurrentHumidity => currentHumidity;
        public float CurrentTimeOfDay => currentTimeOfDay;
        public int CurrentDay => currentDay;
        public bool IsDaytime => isDaytime;
        public float DaylightHours => GetDaylightHours();

        /// <summary>
        /// Returns the season for a given day number.
        /// Game calendar: Day 1 = start of the selected starting season.
        /// Each season lasts a configurable number of days (default: 30 game-days).
        /// </summary>
        public Season GetSeasonForDay(int day)
        {
            int daysPerSeason = config != null ? config.daysPerSeason : 30;
            int seasonIndex = ((int)currentSeason + (day - 1) / daysPerSeason) % 4;
            return (Season)seasonIndex;
        }

        private void Start()
        {
            targetWeather = currentWeather;
            targetTemperature = currentTemperature;
            GenerateInitialWeather();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float dtGameHours = dt * gameHoursPerRealSecond;

            UpdateTimeProgression(dtGameHours);
            UpdateWeatherProgression(dtGameHours);
            UpdateTemperature(dtGameHours);
            PushEnvironmentToPhysiology();
            BroadcastEnvironment();
        }

        // ===== TIME PROGRESSION =====

        private void UpdateTimeProgression(float dtGameHours)
        {
            float previousTime = currentTimeOfDay;
            currentTimeOfDay += dtGameHours;

            if (currentTimeOfDay >= 24f)
            {
                currentTimeOfDay -= 24f;
                currentDay++;

                // Check season transition
                Season newSeason = GetSeasonForDay(currentDay);
                if (newSeason != currentSeason)
                {
                    Season oldSeason = currentSeason;
                    currentSeason = newSeason;
                    OnSeasonChanged?.Invoke(currentSeason);
                    Debug.Log($"[SeasonManager] Season changed: {oldSeason} -> {currentSeason} on day {currentDay}");
                }
            }

            // Day/night transition
            float sunrise = GetSunriseHour();
            float sunset = GetSunsetHour();
            bool wasDay = isDaytime;
            isDaytime = currentTimeOfDay >= sunrise && currentTimeOfDay < sunset;

            if (wasDay != isDaytime)
            {
                OnDayNightChanged?.Invoke(isDaytime);
            }
        }

        // ===== WEATHER SYSTEM =====
        // From scenario docs:
        // Winter: snow, sub-zero temps, short days
        // Autumn: "deszcze ktore powoduja obnizenie temperatury powietrza"
        // Spring: rain, rising temps, "deszcze" noted as obstacle
        // Summer: high temps, risk of heatstroke during 12:00-17:00 marches

        private void UpdateWeatherProgression(float dtGameHours)
        {
            weatherChangeTimer += dtGameHours;

            if (weatherChangeTimer >= nextWeatherChangeInterval)
            {
                weatherChangeTimer = 0f;
                GenerateNewWeather();
            }

            // Smooth transition between weather states
            if (weatherTransitionProgress < 1f)
            {
                float transitionRate = config != null ? config.weatherTransitionRate : 0.5f;
                weatherTransitionProgress = Mathf.Min(1f, weatherTransitionProgress + dtGameHours * transitionRate);

                currentPrecipitation = Mathf.Lerp(currentPrecipitation,
                    GetPrecipitationForWeather(targetWeather), weatherTransitionProgress);
                currentWindSpeed = Mathf.Lerp(currentWindSpeed,
                    GetWindForWeather(targetWeather), weatherTransitionProgress);
                currentCloudCover = Mathf.Lerp(currentCloudCover,
                    GetCloudCoverForWeather(targetWeather), weatherTransitionProgress);
                currentHumidity = Mathf.Lerp(currentHumidity,
                    GetHumidityForSeason(), weatherTransitionProgress);

                if (weatherTransitionProgress >= 1f && currentWeather != targetWeather)
                {
                    currentWeather = targetWeather;
                    OnWeatherChanged?.Invoke(currentWeather);
                }
            }
        }

        private void GenerateInitialWeather()
        {
            currentTemperature = GetBaseTemperatureForSeason();
            currentWeather = GetRandomWeatherForSeason();
            targetWeather = currentWeather;
            currentPrecipitation = GetPrecipitationForWeather(currentWeather);
            currentWindSpeed = GetWindForWeather(currentWeather);
            currentCloudCover = GetCloudCoverForWeather(currentWeather);
            currentHumidity = GetHumidityForSeason();
            nextWeatherChangeInterval = UnityEngine.Random.Range(1f, 4f);
        }

        private void GenerateNewWeather()
        {
            targetWeather = GetRandomWeatherForSeason();
            targetTemperature = GetBaseTemperatureForSeason() + GetTemperatureVariation();
            weatherTransitionProgress = 0f;

            // Weather changes more frequently in autumn/spring (from scenarios: rain is common)
            float minInterval = config != null ? config.minWeatherChangeHours : 1f;
            float maxInterval = config != null ? config.maxWeatherChangeHours : 6f;

            if (currentSeason == Season.Autumn || currentSeason == Season.Spring)
            {
                minInterval *= 0.7f;
                maxInterval *= 0.7f;
            }

            nextWeatherChangeInterval = UnityEngine.Random.Range(minInterval, maxInterval);
        }

        // ===== TEMPERATURE MODEL =====
        // From scenario docs part 4:
        // Summer: "wysokie temperatury ktore maja negatywny wplyw na stan fizyczny"
        // - Heat risk during marches 12:00-17:00
        // Winter: "niska temperatura", frostbite risk, hypothermia
        // Day/night temperature differential

        private void UpdateTemperature(float dtGameHours)
        {
            float baseTemp = GetBaseTemperatureForSeason();
            float timeOfDayModifier = GetTimeOfDayTemperatureModifier();
            float weatherModifier = GetWeatherTemperatureModifier();
            float target = baseTemp + timeOfDayModifier + weatherModifier;

            // Temperature changes slowly (thermal inertia)
            float changeRate = config != null ? config.temperatureChangeRate : 0.3f;
            currentTemperature = Mathf.Lerp(currentTemperature, target, changeRate * dtGameHours);
        }

        /// <summary>
        /// Base temperature ranges per season.
        /// Set for central Poland (Jura Krakowsko-Czestochowska) climate.
        /// From scenario docs: explicit seasonal temperature references.
        /// </summary>
        private float GetBaseTemperatureForSeason()
        {
            switch (currentSeason)
            {
                case Season.Spring:
                    // From scenario: "wzrost temperatur", rain still common
                    return config != null ? config.springBaseTemp : 12f;
                case Season.Summer:
                    // From scenario: "wysokie temperatury", heatstroke risk 12:00-17:00
                    return config != null ? config.summerBaseTemp : 25f;
                case Season.Autumn:
                    // From scenario: "deszcze powoduja obnizenie temperatury"
                    return config != null ? config.autumnBaseTemp : 8f;
                case Season.Winter:
                    // From scenario: "najtrudniejsza pora roku", hypothermia/frostbite
                    return config != null ? config.winterBaseTemp : -5f;
                default:
                    return 15f;
            }
        }

        /// <summary>
        /// Temperature varies with time of day.
        /// Coldest at 4-5 AM, warmest at 14-15 PM.
        /// From scenario: summer heat risk is "godziny od 12:00 do 17:00".
        /// </summary>
        private float GetTimeOfDayTemperatureModifier()
        {
            // Sinusoidal day/night temperature curve
            // Peak at ~14:00, trough at ~04:00
            float phase = (currentTimeOfDay - 4f) / 24f * Mathf.PI * 2f;
            float amplitude = config != null ? config.diurnalTemperatureRange : 8f;
            return Mathf.Sin(phase) * amplitude * 0.5f;
        }

        private float GetWeatherTemperatureModifier()
        {
            switch (currentWeather)
            {
                case WeatherCondition.Rain:
                case WeatherCondition.RainWithSleet:
                    return -3f; // Rain cools the air
                case WeatherCondition.Snow:
                    return -5f;
                case WeatherCondition.Fog:
                    return -2f;
                case WeatherCondition.Storm:
                    return -4f;
                case WeatherCondition.HeatWave:
                    return 8f;  // From scenario: extreme summer heat
                case WeatherCondition.Overcast:
                    return -1f;
                default:
                    return 0f;
            }
        }

        private float GetTemperatureVariation()
        {
            return UnityEngine.Random.Range(-3f, 3f);
        }

        // ===== WEATHER GENERATION PER SEASON =====

        private WeatherCondition GetRandomWeatherForSeason()
        {
            float roll = UnityEngine.Random.value;

            switch (currentSeason)
            {
                case Season.Winter:
                    // From scenario: snow, cold, limited options
                    if (roll < 0.25f) return WeatherCondition.Snow;
                    if (roll < 0.40f) return WeatherCondition.RainWithSleet;
                    if (roll < 0.55f) return WeatherCondition.Overcast;
                    if (roll < 0.65f) return WeatherCondition.Fog;
                    if (roll < 0.75f) return WeatherCondition.Clear;
                    if (roll < 0.85f) return WeatherCondition.Wind;
                    return WeatherCondition.Storm;

                case Season.Spring:
                    // From scenario: "deszcze", rising temperatures
                    if (roll < 0.25f) return WeatherCondition.Rain;
                    if (roll < 0.40f) return WeatherCondition.Overcast;
                    if (roll < 0.60f) return WeatherCondition.Clear;
                    if (roll < 0.70f) return WeatherCondition.Fog;
                    if (roll < 0.80f) return WeatherCondition.Wind;
                    if (roll < 0.90f) return WeatherCondition.RainWithSleet;
                    return WeatherCondition.Storm;

                case Season.Summer:
                    // From scenario: high temps, heat risk, mosquitoes near water
                    if (roll < 0.40f) return WeatherCondition.Clear;
                    if (roll < 0.55f) return WeatherCondition.HeatWave;
                    if (roll < 0.65f) return WeatherCondition.Overcast;
                    if (roll < 0.75f) return WeatherCondition.Rain;
                    if (roll < 0.85f) return WeatherCondition.Wind;
                    return WeatherCondition.Storm;

                case Season.Autumn:
                    // From scenario: "deszcze, deszcz ze sniegiem, mgla, przymrozki"
                    if (roll < 0.30f) return WeatherCondition.Rain;
                    if (roll < 0.45f) return WeatherCondition.Overcast;
                    if (roll < 0.55f) return WeatherCondition.Fog;
                    if (roll < 0.65f) return WeatherCondition.RainWithSleet;
                    if (roll < 0.75f) return WeatherCondition.Wind;
                    if (roll < 0.85f) return WeatherCondition.Clear;
                    return WeatherCondition.Storm;

                default:
                    return WeatherCondition.Clear;
            }
        }

        private float GetPrecipitationForWeather(WeatherCondition weather)
        {
            switch (weather)
            {
                case WeatherCondition.Rain: return UnityEngine.Random.Range(0.3f, 0.7f);
                case WeatherCondition.RainWithSleet: return UnityEngine.Random.Range(0.4f, 0.8f);
                case WeatherCondition.Snow: return UnityEngine.Random.Range(0.2f, 0.6f);
                case WeatherCondition.Storm: return UnityEngine.Random.Range(0.7f, 1.0f);
                case WeatherCondition.Fog: return UnityEngine.Random.Range(0.05f, 0.15f);
                default: return 0f;
            }
        }

        private float GetWindForWeather(WeatherCondition weather)
        {
            switch (weather)
            {
                case WeatherCondition.Wind: return UnityEngine.Random.Range(5f, 15f);
                case WeatherCondition.Storm: return UnityEngine.Random.Range(10f, 25f);
                case WeatherCondition.Clear: return UnityEngine.Random.Range(0f, 3f);
                case WeatherCondition.Rain: return UnityEngine.Random.Range(2f, 8f);
                case WeatherCondition.Snow: return UnityEngine.Random.Range(1f, 10f);
                default: return UnityEngine.Random.Range(0f, 5f);
            }
        }

        private float GetCloudCoverForWeather(WeatherCondition weather)
        {
            switch (weather)
            {
                case WeatherCondition.Clear: return UnityEngine.Random.Range(0f, 0.2f);
                case WeatherCondition.HeatWave: return UnityEngine.Random.Range(0f, 0.1f);
                case WeatherCondition.Overcast: return UnityEngine.Random.Range(0.7f, 1.0f);
                case WeatherCondition.Rain:
                case WeatherCondition.Snow:
                case WeatherCondition.Storm:
                    return UnityEngine.Random.Range(0.8f, 1.0f);
                case WeatherCondition.Fog: return UnityEngine.Random.Range(0.5f, 0.9f);
                default: return 0.5f;
            }
        }

        private float GetHumidityForSeason()
        {
            switch (currentSeason)
            {
                case Season.Summer: return UnityEngine.Random.Range(0.3f, 0.6f);
                case Season.Winter: return UnityEngine.Random.Range(0.6f, 0.9f);
                case Season.Autumn: return UnityEngine.Random.Range(0.6f, 0.95f);
                case Season.Spring: return UnityEngine.Random.Range(0.5f, 0.8f);
                default: return 0.5f;
            }
        }

        // ===== DAY LENGTH =====
        // From scenario: "jesienia wczesniej robi sie ciemno"
        // Jura Krakowsko-Czestochowska latitude ~50.5N
        // Summer days: ~16h, Winter days: ~8h

        private float GetDaylightHours()
        {
            switch (currentSeason)
            {
                case Season.Summer: return config != null ? config.summerDaylightHours : 16f;
                case Season.Winter: return config != null ? config.winterDaylightHours : 8f;
                case Season.Spring: return config != null ? config.springDaylightHours : 12.5f;
                case Season.Autumn: return config != null ? config.autumnDaylightHours : 11f;
                default: return 12f;
            }
        }

        private float GetSunriseHour()
        {
            float daylight = GetDaylightHours();
            return 12f - daylight / 2f;
        }

        private float GetSunsetHour()
        {
            float daylight = GetDaylightHours();
            return 12f + daylight / 2f;
        }

        // ===== PHYSIOLOGY INTEGRATION =====

        private void PushEnvironmentToPhysiology()
        {
            if (physiologyController == null) return;

            physiologyController.SetEnvironment(
                currentTemperature,
                currentWindSpeed,
                currentPrecipitation,
                false, // isInShelter - managed by ShelterSystem
                false, // isNearFire - managed by ShelterSystem
                0f     // fireHeat - managed by ShelterSystem
            );
        }

        private void BroadcastEnvironment()
        {
            OnEnvironmentUpdated?.Invoke(new EnvironmentSnapshot
            {
                season = currentSeason,
                weather = currentWeather,
                temperature = currentTemperature,
                windSpeed = currentWindSpeed,
                precipitation = currentPrecipitation,
                humidity = currentHumidity,
                cloudCover = currentCloudCover,
                timeOfDay = currentTimeOfDay,
                isDaytime = isDaytime,
                dayNumber = currentDay
            });
        }

        // ===== PUBLIC API =====

        /// <summary>
        /// Force a specific season (for testing/debugging).
        /// </summary>
        public void SetSeason(Season season)
        {
            if (season != currentSeason)
            {
                currentSeason = season;
                GenerateInitialWeather();
                OnSeasonChanged?.Invoke(currentSeason);
            }
        }

        /// <summary>
        /// Force specific weather (for testing/debugging).
        /// </summary>
        public void SetWeather(WeatherCondition weather)
        {
            targetWeather = weather;
            weatherTransitionProgress = 0f;
        }

        /// <summary>
        /// Advance time by specified game hours (for testing).
        /// </summary>
        public void AdvanceTime(float gameHours)
        {
            UpdateTimeProgression(gameHours);
        }
    }

    // ===== ENUMS =====

    public enum Season
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3
    }

    public enum WeatherCondition
    {
        Clear,
        Overcast,
        Rain,
        RainWithSleet,  // From scenario: "deszcz ze sniegiem" (autumn/spring)
        Snow,
        Fog,            // From scenario: "mgla" (autumn)
        Wind,
        Storm,
        HeatWave        // From scenario: summer "wysokie temperatury"
    }

    // ===== DATA STRUCTS =====

    [Serializable]
    public struct EnvironmentSnapshot
    {
        public Season season;
        public WeatherCondition weather;
        public float temperature;
        public float windSpeed;
        public float precipitation;
        public float humidity;
        public float cloudCover;
        public float timeOfDay;
        public bool isDaytime;
        public int dayNumber;
    }

    // ===== SEASON CONFIG SCRIPTABLE OBJECT =====

    /// <summary>
    /// Tunable configuration for seasonal parameters.
    /// Create assets: Assets > Create > Plaga44 > Season Config
    /// </summary>
    [CreateAssetMenu(fileName = "SeasonConfig", menuName = "Plaga44/Season Config")]
    public class SeasonConfig : ScriptableObject
    {
        [Header("Calendar")]
        [Tooltip("Number of game-days per season.")]
        public int daysPerSeason = 30;

        [Header("Temperature Ranges (Celsius)")]
        [Tooltip("Base temp for spring. Jura region ~12C average.")]
        public float springBaseTemp = 12f;

        [Tooltip("Base temp for summer. From scenario: high temps, heatstroke risk.")]
        public float summerBaseTemp = 25f;

        [Tooltip("Base temp for autumn. From scenario: rain lowers temp.")]
        public float autumnBaseTemp = 8f;

        [Tooltip("Base temp for winter. From scenario: hardest season, hypothermia.")]
        public float winterBaseTemp = -5f;

        [Tooltip("Day-night temperature swing amplitude in Celsius.")]
        public float diurnalTemperatureRange = 8f;

        [Tooltip("Rate at which temperature transitions. Lower = more stable.")]
        public float temperatureChangeRate = 0.3f;

        [Header("Daylight Hours (Latitude ~50.5N)")]
        [Tooltip("From scenario: autumn 'wczesniej robi sie ciemno'. Hours of daylight per season.")]
        public float springDaylightHours = 12.5f;
        public float summerDaylightHours = 16f;
        public float autumnDaylightHours = 11f;
        public float winterDaylightHours = 8f;

        [Header("Weather")]
        [Tooltip("Minimum hours between weather changes.")]
        public float minWeatherChangeHours = 1f;

        [Tooltip("Maximum hours between weather changes.")]
        public float maxWeatherChangeHours = 6f;

        [Tooltip("Rate of weather transitions (higher = faster).")]
        public float weatherTransitionRate = 0.5f;
    }
}
