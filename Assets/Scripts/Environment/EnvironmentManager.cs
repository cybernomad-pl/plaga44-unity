using System;
using UnityEngine;

namespace Plaga44.Environment
{
    /// <summary>
    /// Seasons affecting all gameplay systems.
    /// Each season has distinct survival characteristics per scenario docs.
    /// </summary>
    public enum Season
    {
        Spring,  // Wiosna - warming, rain, early berries
        Summer,  // Lato - warm, berries, fish, mosquitoes, heat stroke risk
        Autumn,  // Jesien - mushrooms, rain, early darkness, slippery ground
        Winter   // Zima - "najtrudniejsza pora roku do przezycia"
    }

    /// <summary>
    /// Weather conditions from scenario documents.
    /// </summary>
    public enum WeatherType
    {
        Clear,          // Pogodnie
        Cloudy,         // Pochmurnie
        Rain_Light,     // Lekki deszcz
        Rain_Heavy,     // Silny deszcz
        Rain_With_Snow, // Deszcz ze sniegiem (autumn/spring)
        Snow_Light,     // Lekki snieg
        Snow_Heavy,     // Silny snieg
        Fog,            // Mgla
        Frost           // Przymrozki (morning frost in autumn)
    }

    /// <summary>
    /// Central environment manager handling day/night cycle, weather, temperature,
    /// and seasonal progression for the Jura KCz setting.
    ///
    /// Key scenario references:
    /// - "zima jest najtrudniejsza pora roku do przezycia" (cz.2/3)
    /// - "jesienia wczesniej robi sie ciemno" (cz.3)
    /// - "opady deszczu, deszczu ze sniegiem" (cz.1)
    /// - "wysokie temperatury ktore maja negatywny wplyw" - summer (cz.4)
    /// - "przymrozki noce i ranne" - autumn (cz.4)
    /// </summary>
    public class EnvironmentManager : MonoBehaviour
    {
        public static EnvironmentManager Instance { get; private set; }

        // -- Events --
        public event Action<Season> OnSeasonChanged;
        public event Action<WeatherType> OnWeatherChanged;
        public event Action<float> OnTimeOfDayChanged;  // 0-24 hours

        [Header("Time Settings")]
        [Tooltip("Real seconds per in-game hour")]
        [SerializeField] private float realSecondsPerGameHour = 60f;

        [Tooltip("Current in-game hour (0-24)")]
        [SerializeField] private float currentHour = 6f;

        [Tooltip("Current day number")]
        [SerializeField] private int currentDay = 1;

        [Header("Season Settings")]
        [SerializeField] private Season currentSeason = Season.Summer;
        [Tooltip("Days per in-game season")]
        [SerializeField] private int daysPerSeason = 30;

        [Header("Weather State")]
        [SerializeField] private WeatherType currentWeather = WeatherType.Clear;
        [SerializeField] private float weatherDurationHours = 4f;
        private float weatherTimer = 0f;

        [Header("Temperature")]
        [SerializeField] private float currentTemperature = 20f;

        // Seasonal temperature ranges for Jura KCz (Celsius)
        // Based on real Malopolska climate data
        private readonly float[] seasonMinTemp = { 2f, 15f, 3f, -15f };   // Spring, Summer, Autumn, Winter
        private readonly float[] seasonMaxTemp = { 18f, 35f, 18f, 2f };
        private readonly float[] seasonAvgTemp = { 10f, 22f, 9f, -3f };

        // Sunrise/sunset hours per season for southern Poland latitude (~50N)
        private readonly float[] seasonSunrise = { 5.5f, 4.5f, 6.5f, 7.5f };
        private readonly float[] seasonSunset  = { 19.5f, 21f, 17.5f, 16f };

        // Weather probability weights per season [Clear, Cloudy, RainLight, RainHeavy, RainSnow, SnowLight, SnowHeavy, Fog, Frost]
        private readonly float[,] weatherWeights = {
            { 25f, 20f, 20f, 10f, 5f, 2f, 0f, 10f, 8f },   // Spring
            { 40f, 20f, 15f, 10f, 0f, 0f, 0f, 8f, 2f },     // Summer
            { 15f, 20f, 20f, 15f, 8f, 2f, 0f, 12f, 8f },    // Autumn
            { 10f, 15f, 5f, 2f, 10f, 20f, 15f, 8f, 15f }    // Winter
        };

        // -- Properties --
        public float CurrentHour => currentHour;
        public int CurrentDay => currentDay;
        public Season CurrentSeason => currentSeason;
        public WeatherType CurrentWeather => currentWeather;
        public float CurrentTemperature => currentTemperature;

        /// <summary>
        /// Whether it is currently daytime.
        /// </summary>
        public bool IsDaytime
        {
            get
            {
                int s = (int)currentSeason;
                return currentHour >= seasonSunrise[s] && currentHour <= seasonSunset[s];
            }
        }

        /// <summary>
        /// Get the sunrise hour for the current season.
        /// </summary>
        public float SunriseHour => seasonSunrise[(int)currentSeason];

        /// <summary>
        /// Get the sunset hour for the current season.
        /// "jesienia wczesniej robi sie ciemno po terenach lesnych nalezy sie
        /// poruszac od godziny 6:00 do 19-tej wieczorem"
        /// </summary>
        public float SunsetHour => seasonSunset[(int)currentSeason];

        /// <summary>
        /// Current rain intensity (0 = none, 1 = heavy downpour).
        /// </summary>
        public float RainIntensity
        {
            get
            {
                switch (currentWeather)
                {
                    case WeatherType.Rain_Light: return 0.3f;
                    case WeatherType.Rain_Heavy: return 0.8f;
                    case WeatherType.Rain_With_Snow: return 0.5f;
                    default: return 0f;
                }
            }
        }

        /// <summary>
        /// Current snow coverage accumulation (0-1).
        /// Builds up during snow weather, melts during warm weather.
        /// </summary>
        public float SnowCoverage { get; private set; } = 0f;

        /// <summary>
        /// Whether there is fog reducing visibility.
        /// </summary>
        public bool IsFoggy => currentWeather == WeatherType.Fog;

        /// <summary>
        /// Wind chill factor (multiplier for cold damage).
        /// </summary>
        public float WindChillFactor { get; private set; } = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            UpdateTemperature();
            SelectNewWeather();
        }

        private void Update()
        {
            AdvanceTime(Time.deltaTime);
            UpdateWeather(Time.deltaTime);
            UpdateSnowCoverage(Time.deltaTime);
            UpdateWindChill();

            // Sync terrain manager with current weather
            if (Terrain.TerrainManager.Instance != null)
            {
                Terrain.TerrainManager.Instance.UpdateWeatherState(
                    RainIntensity, SnowCoverage, currentTemperature
                );
            }
        }

        /// <summary>
        /// Advance the in-game clock.
        /// </summary>
        private void AdvanceTime(float deltaTime)
        {
            float previousHour = currentHour;
            float hoursElapsed = deltaTime / realSecondsPerGameHour;
            currentHour += hoursElapsed;

            if (currentHour >= 24f)
            {
                currentHour -= 24f;
                currentDay++;

                // Season change check
                if (currentDay % daysPerSeason == 0)
                {
                    AdvanceSeason();
                }
            }

            // Update temperature based on time of day
            UpdateTemperature();

            OnTimeOfDayChanged?.Invoke(currentHour);
        }

        /// <summary>
        /// Progress to the next season.
        /// </summary>
        private void AdvanceSeason()
        {
            Season previousSeason = currentSeason;
            currentSeason = (Season)(((int)currentSeason + 1) % 4);

            Debug.Log($"[EnvironmentManager] Season changed: {previousSeason} -> {currentSeason}");
            OnSeasonChanged?.Invoke(currentSeason);

            // Force weather re-evaluation on season change
            SelectNewWeather();
        }

        /// <summary>
        /// Update current temperature based on season, time of day, and weather.
        /// Implements diurnal temperature cycle.
        /// </summary>
        private void UpdateTemperature()
        {
            int s = (int)currentSeason;
            float minTemp = seasonMinTemp[s];
            float maxTemp = seasonMaxTemp[s];

            // Diurnal cycle: coldest at ~5AM, warmest at ~2PM
            float dayProgress;
            if (currentHour < 5f)
                dayProgress = Mathf.InverseLerp(14f, 29f, currentHour + 24f); // Previous day's cooling
            else if (currentHour < 14f)
                dayProgress = Mathf.InverseLerp(5f, 14f, currentHour); // Morning warming
            else
                dayProgress = 1f - Mathf.InverseLerp(14f, 29f, currentHour); // Afternoon/evening cooling

            float baseTemp = Mathf.Lerp(minTemp, maxTemp, dayProgress);

            // Weather modifiers
            switch (currentWeather)
            {
                case WeatherType.Rain_Light:
                    baseTemp -= 2f;
                    break;
                case WeatherType.Rain_Heavy:
                    baseTemp -= 4f;
                    // "deszcze ktore powoduja obnizenie temperatury powietrza"
                    break;
                case WeatherType.Rain_With_Snow:
                    baseTemp -= 5f;
                    break;
                case WeatherType.Snow_Light:
                    baseTemp -= 3f;
                    break;
                case WeatherType.Snow_Heavy:
                    baseTemp -= 6f;
                    break;
                case WeatherType.Fog:
                    baseTemp -= 1f;
                    break;
                case WeatherType.Frost:
                    baseTemp -= 4f;
                    break;
                case WeatherType.Clear:
                    if (currentSeason == Season.Summer && IsDaytime)
                        baseTemp += 3f; // Heat stroke risk on clear summer days
                    break;
            }

            // Add slight randomness
            baseTemp += UnityEngine.Random.Range(-0.5f, 0.5f);
            currentTemperature = baseTemp;
        }

        /// <summary>
        /// Update weather duration and select new weather when current expires.
        /// </summary>
        private void UpdateWeather(float deltaTime)
        {
            float hoursElapsed = deltaTime / realSecondsPerGameHour;
            weatherTimer -= hoursElapsed;

            if (weatherTimer <= 0f)
            {
                SelectNewWeather();
            }
        }

        /// <summary>
        /// Select a new weather type based on seasonal probability weights.
        /// </summary>
        private void SelectNewWeather()
        {
            int s = (int)currentSeason;
            float totalWeight = 0f;

            for (int i = 0; i < 9; i++)
                totalWeight += weatherWeights[s, i];

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;

            WeatherType previousWeather = currentWeather;

            for (int i = 0; i < 9; i++)
            {
                cumulative += weatherWeights[s, i];
                if (roll <= cumulative)
                {
                    currentWeather = (WeatherType)i;
                    break;
                }
            }

            // Weather duration: 2-8 game hours
            weatherDurationHours = UnityEngine.Random.Range(2f, 8f);
            weatherTimer = weatherDurationHours;

            if (currentWeather != previousWeather)
            {
                Debug.Log($"[EnvironmentManager] Weather: {previousWeather} -> {currentWeather} (duration: {weatherDurationHours:F1}h)");
                OnWeatherChanged?.Invoke(currentWeather);
            }
        }

        /// <summary>
        /// Accumulate or melt snow coverage over time.
        /// "gruba pokrywa sniezna" in winter.
        /// </summary>
        private void UpdateSnowCoverage(float deltaTime)
        {
            float hoursElapsed = deltaTime / realSecondsPerGameHour;

            if (currentWeather == WeatherType.Snow_Light)
            {
                SnowCoverage += 0.02f * hoursElapsed;
            }
            else if (currentWeather == WeatherType.Snow_Heavy)
            {
                SnowCoverage += 0.06f * hoursElapsed;
            }
            else if (currentTemperature > 3f)
            {
                // Melting
                float meltRate = Mathf.Clamp01((currentTemperature - 3f) / 20f) * 0.03f;
                SnowCoverage -= meltRate * hoursElapsed;
            }
            else if (currentWeather == WeatherType.Rain_Heavy && currentTemperature > 0f)
            {
                // Rain melts snow
                SnowCoverage -= 0.04f * hoursElapsed;
            }

            SnowCoverage = Mathf.Clamp01(SnowCoverage);
        }

        /// <summary>
        /// Update wind chill factor for hypothermia calculations.
        /// </summary>
        private void UpdateWindChill()
        {
            // Base wind chill depends on weather
            float windSpeed = 0f;
            switch (currentWeather)
            {
                case WeatherType.Clear: windSpeed = 5f; break;
                case WeatherType.Cloudy: windSpeed = 8f; break;
                case WeatherType.Rain_Light: windSpeed = 10f; break;
                case WeatherType.Rain_Heavy: windSpeed = 20f; break;
                case WeatherType.Snow_Light: windSpeed = 12f; break;
                case WeatherType.Snow_Heavy: windSpeed = 25f; break;
                case WeatherType.Fog: windSpeed = 3f; break;
                default: windSpeed = 8f; break;
            }

            // Wind chill formula simplified
            if (currentTemperature < 10f && windSpeed > 5f)
            {
                WindChillFactor = 1f + (10f - currentTemperature) * windSpeed * 0.002f;
            }
            else
            {
                WindChillFactor = 1f;
            }
        }

        /// <summary>
        /// Get the normalized sun position (0 = horizon, 1 = zenith).
        /// Useful for lighting calculations.
        /// </summary>
        public float GetSunElevation()
        {
            if (!IsDaytime) return 0f;

            int s = (int)currentSeason;
            float sunrise = seasonSunrise[s];
            float sunset = seasonSunset[s];
            float midday = (sunrise + sunset) / 2f;

            if (currentHour <= midday)
                return Mathf.InverseLerp(sunrise, midday, currentHour);
            else
                return 1f - Mathf.InverseLerp(midday, sunset, currentHour);
        }

        /// <summary>
        /// Get ambient light level (0 = pitch dark, 1 = full daylight).
        /// Accounts for weather, moon, and twilight.
        /// </summary>
        public float GetAmbientLightLevel()
        {
            float baseLightLevel;

            if (IsDaytime)
            {
                baseLightLevel = 0.3f + 0.7f * GetSunElevation();
            }
            else
            {
                // Twilight zones
                int s = (int)currentSeason;
                float sunrise = seasonSunrise[s];
                float sunset = seasonSunset[s];

                float twilightDuration = 1.5f; // hours

                if (currentHour > sunset && currentHour < sunset + twilightDuration)
                {
                    baseLightLevel = Mathf.Lerp(0.3f, 0.05f,
                        (currentHour - sunset) / twilightDuration);
                }
                else if (currentHour > sunrise - twilightDuration && currentHour < sunrise)
                {
                    baseLightLevel = Mathf.Lerp(0.05f, 0.3f,
                        (currentHour - (sunrise - twilightDuration)) / twilightDuration);
                }
                else
                {
                    baseLightLevel = 0.05f; // Moonlight/starlight
                }
            }

            // Weather modifiers
            switch (currentWeather)
            {
                case WeatherType.Cloudy:
                    baseLightLevel *= 0.7f;
                    break;
                case WeatherType.Rain_Light:
                    baseLightLevel *= 0.6f;
                    break;
                case WeatherType.Rain_Heavy:
                    baseLightLevel *= 0.4f;
                    break;
                case WeatherType.Fog:
                    baseLightLevel *= 0.5f;
                    break;
                case WeatherType.Snow_Heavy:
                    // Snow reflects light, slightly brighter
                    if (SnowCoverage > 0.3f)
                        baseLightLevel *= 1.1f;
                    else
                        baseLightLevel *= 0.5f;
                    break;
            }

            return Mathf.Clamp01(baseLightLevel);
        }

        /// <summary>
        /// Check if conditions pose hypothermia risk.
        /// "zagrozenie hipotermia" (winter scenario).
        /// </summary>
        public bool IsHypothermiaRisk()
        {
            return currentTemperature < 5f ||
                   (currentTemperature < 10f && RainIntensity > 0.3f);
        }

        /// <summary>
        /// Check if conditions pose heat stroke risk.
        /// "wysokie temperatury ktore maja negatywny wplyw - udar mozgu" (cz.4)
        /// </summary>
        public bool IsHeatStrokeRisk()
        {
            return currentSeason == Season.Summer &&
                   currentTemperature > 30f &&
                   IsDaytime &&
                   currentHour >= 12f && currentHour <= 17f;
        }

        /// <summary>
        /// Force set the time (for testing/debugging).
        /// </summary>
        public void SetTime(float hour, int day, Season season)
        {
            currentHour = Mathf.Clamp(hour, 0f, 23.99f);
            currentDay = Mathf.Max(1, day);
            currentSeason = season;
            UpdateTemperature();
        }

        /// <summary>
        /// Force set weather (for testing/debugging).
        /// </summary>
        public void SetWeather(WeatherType weather, float durationHours = 4f)
        {
            currentWeather = weather;
            weatherDurationHours = durationHours;
            weatherTimer = durationHours;
            OnWeatherChanged?.Invoke(currentWeather);
        }
    }
}
