// PLAGA '44 - Audio Manager
// Manages ambient soundscapes per environment, weather audio layers,
// and spatial audio for Meta Quest 3 VR.
// Part of issue #31: Audio system and VR emersion effects

using UnityEngine;
using System.Collections.Generic;

namespace Plaga44.Audio
{
    /// <summary>
    /// Environment types that determine which ambient soundscape plays.
    /// Based on scenario docs: forest (mixed, coniferous), urban ruins, underground shelters.
    /// </summary>
    public enum EnvironmentType
    {
        Forest,
        UrbanRuins,
        Underground,
        OpenField,
        Waterside
    }

    /// <summary>
    /// Weather conditions that layer additional audio on top of ambient soundscape.
    /// From scenario: rain, snow, fog, clear, rain-snow mix.
    /// </summary>
    public enum WeatherType
    {
        Clear,
        Rain,
        Snow,
        Fog,
        RainSnowMix,
        Storm
    }

    /// <summary>
    /// Time of day affecting ambient audio character.
    /// Scenario docs: movement recommended 3-5 AM, different NPC behavior at night.
    /// </summary>
    public enum TimeOfDay
    {
        Dawn,       // 04:00-06:00
        Morning,    // 06:00-12:00
        Afternoon,  // 12:00-17:00
        Dusk,       // 17:00-19:00
        Night       // 19:00-04:00
    }

    /// <summary>
    /// Manages all ambient and environmental audio for the game.
    /// Crossfades between environment soundscapes, layers weather audio,
    /// and adjusts based on time of day and season.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Environment Soundscapes")]
        [SerializeField] private AudioClip forestAmbience;
        [SerializeField] private AudioClip urbanRuinsAmbience;
        [SerializeField] private AudioClip undergroundAmbience;
        [SerializeField] private AudioClip openFieldAmbience;
        [SerializeField] private AudioClip watersideAmbience;

        [Header("Weather Layers")]
        [SerializeField] private AudioClip rainLoop;
        [SerializeField] private AudioClip snowWindLoop;
        [SerializeField] private AudioClip stormLoop;
        [SerializeField] private AudioClip fogWindLoop;

        [Header("Seasonal Ambient Layers")]
        [SerializeField] private AudioClip summerInsectsLoop;
        [SerializeField] private AudioClip winterWindLoop;
        [SerializeField] private AudioClip springBirdsLoop;
        [SerializeField] private AudioClip autumnWindLeavesLoop;

        [Header("Night Audio")]
        [SerializeField] private AudioClip nightOwlsLoop;
        [SerializeField] private AudioClip nightCricketsLoop;

        [Header("Configuration")]
        [SerializeField] private float crossfadeDuration = 2.0f;
        [SerializeField] private float weatherFadeDuration = 1.5f;
        [SerializeField] [Range(0f, 1f)] private float masterVolume = 0.7f;
        [SerializeField] [Range(0f, 1f)] private float weatherVolume = 0.5f;
        [SerializeField] [Range(0f, 1f)] private float seasonalVolume = 0.3f;

        // Audio sources
        private AudioSource environmentSourceA;
        private AudioSource environmentSourceB;
        private AudioSource weatherSource;
        private AudioSource seasonalSource;
        private AudioSource nightSource;

        private bool isSourceAActive = true;
        private EnvironmentType currentEnvironment;
        private WeatherType currentWeather = WeatherType.Clear;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SetupAudioSources();
        }

        private void SetupAudioSources()
        {
            environmentSourceA = CreateAudioSource("EnvironmentA");
            environmentSourceB = CreateAudioSource("EnvironmentB");
            weatherSource = CreateAudioSource("Weather");
            seasonalSource = CreateAudioSource("Seasonal");
            nightSource = CreateAudioSource("Night");

            environmentSourceA.loop = true;
            environmentSourceB.loop = true;
            weatherSource.loop = true;
            seasonalSource.loop = true;
            nightSource.loop = true;
        }

        private AudioSource CreateAudioSource(string name)
        {
            var go = new GameObject($"AudioSource_{name}");
            go.transform.SetParent(transform);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D for ambient
            source.volume = 0f;
            return source;
        }

        /// <summary>
        /// Transition to a new environment soundscape with crossfade.
        /// </summary>
        public void SetEnvironment(EnvironmentType environment)
        {
            if (environment == currentEnvironment) return;
            currentEnvironment = environment;

            AudioClip newClip = GetEnvironmentClip(environment);
            if (newClip == null) return;

            CrossfadeEnvironment(newClip);
        }

        /// <summary>
        /// Set current weather, layering appropriate audio.
        /// Weather affects audio: rain reduces hearing distance, snow muffles sounds.
        /// </summary>
        public void SetWeather(WeatherType weather)
        {
            if (weather == currentWeather) return;
            currentWeather = weather;

            AudioClip weatherClip = GetWeatherClip(weather);
            if (weatherClip != null)
            {
                StartCoroutine(FadeAudioSource(weatherSource, weatherVolume, weatherFadeDuration));
                weatherSource.clip = weatherClip;
                weatherSource.Play();
            }
            else
            {
                StartCoroutine(FadeAudioSource(weatherSource, 0f, weatherFadeDuration));
            }
        }

        /// <summary>
        /// Update time-of-day audio layers (night sounds, dawn chorus, etc.)
        /// </summary>
        public void SetTimeOfDay(TimeOfDay time)
        {
            bool isNight = (time == TimeOfDay.Night || time == TimeOfDay.Dusk);

            if (isNight && nightSource.clip == null)
            {
                // Summer nights: crickets. Other seasons: owls
                nightSource.clip = nightCricketsLoop != null ? nightCricketsLoop : nightOwlsLoop;
                if (nightSource.clip != null)
                {
                    nightSource.Play();
                    StartCoroutine(FadeAudioSource(nightSource, 0.25f, 3f));
                }
            }
            else if (!isNight && nightSource.isPlaying)
            {
                StartCoroutine(FadeAudioSource(nightSource, 0f, 3f));
            }
        }

        /// <summary>
        /// Set seasonal ambient layer. Scenario docs emphasize seasonal differences:
        /// summer - insects/mosquitoes, winter - howling wind, spring - birds,
        /// autumn - wind through falling leaves.
        /// </summary>
        public void SetSeason(int seasonIndex)
        {
            AudioClip[] seasonClips = { springBirdsLoop, summerInsectsLoop,
                                         autumnWindLeavesLoop, winterWindLoop };
            int idx = Mathf.Clamp(seasonIndex, 0, 3);
            AudioClip clip = seasonClips[idx];

            if (clip != null && seasonalSource.clip != clip)
            {
                seasonalSource.clip = clip;
                seasonalSource.Play();
                StartCoroutine(FadeAudioSource(seasonalSource, seasonalVolume, 2f));
            }
        }

        /// <summary>
        /// Apply weather-based audio filtering to simulate muffled hearing in snow,
        /// reduced clarity in rain, etc. Uses lowpass filter.
        /// </summary>
        public void ApplyWeatherAudioFilter(AudioLowPassFilter filter)
        {
            if (filter == null) return;

            switch (currentWeather)
            {
                case WeatherType.Rain:
                    filter.cutoffFrequency = 3000f; // Moderate muffling
                    break;
                case WeatherType.Snow:
                    filter.cutoffFrequency = 2000f; // Heavy muffling - snow absorbs sound
                    break;
                case WeatherType.Storm:
                    filter.cutoffFrequency = 1500f; // Very muffled
                    break;
                default:
                    filter.cutoffFrequency = 22000f; // No filtering
                    break;
            }
        }

        private AudioClip GetEnvironmentClip(EnvironmentType env)
        {
            switch (env)
            {
                case EnvironmentType.Forest: return forestAmbience;
                case EnvironmentType.UrbanRuins: return urbanRuinsAmbience;
                case EnvironmentType.Underground: return undergroundAmbience;
                case EnvironmentType.OpenField: return openFieldAmbience;
                case EnvironmentType.Waterside: return watersideAmbience;
                default: return null;
            }
        }

        private AudioClip GetWeatherClip(WeatherType weather)
        {
            switch (weather)
            {
                case WeatherType.Rain: return rainLoop;
                case WeatherType.Snow: return snowWindLoop;
                case WeatherType.Storm: return stormLoop;
                case WeatherType.Fog: return fogWindLoop;
                case WeatherType.RainSnowMix: return rainLoop; // Mix uses rain base
                default: return null;
            }
        }

        private void CrossfadeEnvironment(AudioClip newClip)
        {
            AudioSource fadeIn = isSourceAActive ? environmentSourceB : environmentSourceA;
            AudioSource fadeOut = isSourceAActive ? environmentSourceA : environmentSourceB;

            fadeIn.clip = newClip;
            fadeIn.Play();

            StartCoroutine(FadeAudioSource(fadeIn, masterVolume, crossfadeDuration));
            StartCoroutine(FadeAudioSource(fadeOut, 0f, crossfadeDuration));

            isSourceAActive = !isSourceAActive;
        }

        private System.Collections.IEnumerator FadeAudioSource(AudioSource source, float targetVolume, float duration)
        {
            float startVolume = source.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                source.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
                yield return null;
            }

            source.volume = targetVolume;

            if (targetVolume <= 0.001f)
            {
                source.Stop();
            }
        }
    }
}
