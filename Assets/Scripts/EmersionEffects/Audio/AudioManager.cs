// PLAGA '44 - Emersion Effects System
// AudioManager.cs - Ambient soundscape management per environment with weather audio layers
// Manages crossfading between environments and dynamic weather audio mixing

using System;
using System.Collections;
using UnityEngine;
using Plaga44.EmersionEffects.Core;

namespace Plaga44.EmersionEffects.Audio
{
    /// <summary>
    /// Manages ambient soundscapes for each environment type (forest, urban, underground)
    /// and dynamic weather audio layers. Crossfades between environments on transitions.
    /// Attach to the AudioListener or a persistent audio manager GameObject.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the EmersionEffectsManager. Auto-found if null.")]
        public EmersionEffectsManager EmersionManager;

        [Header("Ambient Sound Sources")]
        [Tooltip("AudioSource for the primary ambient loop.")]
        public AudioSource AmbientSourceA;
        [Tooltip("AudioSource for crossfade target ambient loop.")]
        public AudioSource AmbientSourceB;
        [Tooltip("AudioSource for weather audio layer.")]
        public AudioSource WeatherSource;
        [Tooltip("AudioSource for thunder/lightning events.")]
        public AudioSource ThunderSource;

        [Header("Ambient Clips - Forest")]
        public AudioClip ForestDay;
        public AudioClip ForestNight;

        [Header("Ambient Clips - Urban")]
        public AudioClip UrbanDay;
        public AudioClip UrbanNight;

        [Header("Ambient Clips - Underground")]
        public AudioClip UndergroundAmbient;

        [Header("Weather Clips")]
        public AudioClip RainLight;
        public AudioClip RainHeavy;
        public AudioClip WindLight;
        public AudioClip WindStrong;
        public AudioClip[] ThunderClips;

        [Header("Settings")]
        [Range(0f, 1f)] public float MasterVolume = 1f;
        public float CrossfadeDuration = 3f;
        public float IndoorWeatherDampening = 0.4f;

        [Header("Thunder Settings")]
        public float ThunderMinInterval = 8f;
        public float ThunderMaxInterval = 30f;
        [Range(0f, 1f)] public float ThunderVolume = 0.9f;

        private EnvironmentType _currentEnvironment;
        private WeatherType _currentWeather;
        private bool _currentIsNight;
        private bool _isIndoors;
        private bool _sourceAActive = true;
        private Coroutine _crossfadeCoroutine;
        private Coroutine _thunderCoroutine;

        private void Start()
        {
            if (EmersionManager == null)
                EmersionManager = EmersionEffectsManager.Instance;

            if (EmersionManager == null)
            {
                Debug.LogError("[AudioManager] EmersionEffectsManager not found. Audio system disabled.");
                enabled = false;
                return;
            }

            InitializeAudioSources();

            // Set initial environment
            var state = EmersionManager.PlayerState;
            _currentEnvironment = state.CurrentEnvironment;
            _currentIsNight = state.IsNight;
            _currentWeather = state.CurrentWeather;
            _isIndoors = state.IsIndoors;

            PlayAmbientForEnvironment(_currentEnvironment, _currentIsNight, immediate: true);
            UpdateWeatherAudio(state.CurrentWeather, state.WeatherIntensity, state.IsIndoors);

            Debug.Log("[AudioManager] Ambient audio system initialized.");
        }

        private void Update()
        {
            if (EmersionManager == null || !EmersionManager.EffectsEnabled) return;

            var state = EmersionManager.PlayerState;

            // Check for environment change
            if (state.CurrentEnvironment != _currentEnvironment || state.IsNight != _currentIsNight)
            {
                _currentEnvironment = state.CurrentEnvironment;
                _currentIsNight = state.IsNight;
                PlayAmbientForEnvironment(_currentEnvironment, _currentIsNight, immediate: false);
            }

            // Check for weather change
            if (state.CurrentWeather != _currentWeather || state.IsIndoors != _isIndoors)
            {
                _currentWeather = state.CurrentWeather;
                _isIndoors = state.IsIndoors;
                UpdateWeatherAudio(state.CurrentWeather, state.WeatherIntensity, state.IsIndoors);

                // Manage thunder for storms
                if (_currentWeather == WeatherType.Storm && _thunderCoroutine == null)
                {
                    _thunderCoroutine = StartCoroutine(ThunderRoutine());
                }
                else if (_currentWeather != WeatherType.Storm && _thunderCoroutine != null)
                {
                    StopCoroutine(_thunderCoroutine);
                    _thunderCoroutine = null;
                }
            }

            // Continuously update weather volume based on intensity
            UpdateWeatherVolume(state.WeatherIntensity, state.IsIndoors);
        }

        /// <summary>
        /// Starts playing the ambient soundscape for the given environment.
        /// Crossfades from the current ambient to the new one unless immediate is true.
        /// </summary>
        private void PlayAmbientForEnvironment(EnvironmentType env, bool isNight, bool immediate)
        {
            AudioClip targetClip = GetAmbientClip(env, isNight);
            if (targetClip == null)
            {
                Debug.LogWarning($"[AudioManager] No ambient clip assigned for {env} (night={isNight}).");
                return;
            }

            float targetVolume = GetBaseVolumeForEnvironment(env) * MasterVolume;

            AudioSource incoming = _sourceAActive ? AmbientSourceB : AmbientSourceA;
            AudioSource outgoing = _sourceAActive ? AmbientSourceA : AmbientSourceB;

            incoming.clip = targetClip;
            incoming.loop = true;
            incoming.Play();

            if (immediate)
            {
                incoming.volume = targetVolume;
                outgoing.volume = 0f;
                outgoing.Stop();
            }
            else
            {
                if (_crossfadeCoroutine != null)
                    StopCoroutine(_crossfadeCoroutine);
                _crossfadeCoroutine = StartCoroutine(CrossfadeRoutine(outgoing, incoming, targetVolume));
            }

            _sourceAActive = !_sourceAActive;
        }

        private AudioClip GetAmbientClip(EnvironmentType env, bool isNight)
        {
            switch (env)
            {
                case EnvironmentType.Forest:
                    return isNight && ForestNight != null ? ForestNight : ForestDay;
                case EnvironmentType.Urban:
                    return isNight && UrbanNight != null ? UrbanNight : UrbanDay;
                case EnvironmentType.Underground:
                    return UndergroundAmbient;
                default:
                    return UrbanDay;
            }
        }

        private float GetBaseVolumeForEnvironment(EnvironmentType env)
        {
            switch (env)
            {
                case EnvironmentType.Forest: return 0.6f;
                case EnvironmentType.Urban: return 0.5f;
                case EnvironmentType.Underground: return 0.4f;
                default: return 0.5f;
            }
        }

        /// <summary>
        /// Updates weather audio source based on current weather type.
        /// </summary>
        private void UpdateWeatherAudio(WeatherType weather, float intensity, bool isIndoors)
        {
            if (WeatherSource == null) return;

            AudioClip weatherClip = null;

            switch (weather)
            {
                case WeatherType.Rain:
                    weatherClip = RainLight;
                    break;
                case WeatherType.HeavyRain:
                case WeatherType.Storm:
                    weatherClip = intensity > 0.5f ? RainHeavy : RainLight;
                    break;
                case WeatherType.Wind:
                    weatherClip = intensity > 0.5f ? WindStrong : WindLight;
                    break;
                case WeatherType.Snow:
                    weatherClip = WindLight; // Snow uses light wind
                    break;
                case WeatherType.Clear:
                default:
                    WeatherSource.Stop();
                    return;
            }

            // Don't play weather audio in underground environments
            if (_currentEnvironment == EnvironmentType.Underground)
            {
                WeatherSource.Stop();
                return;
            }

            if (weatherClip != null && WeatherSource.clip != weatherClip)
            {
                WeatherSource.clip = weatherClip;
                WeatherSource.loop = true;
                WeatherSource.Play();
            }

            UpdateWeatherVolume(intensity, isIndoors);
        }

        private void UpdateWeatherVolume(float intensity, bool isIndoors)
        {
            if (WeatherSource == null || !WeatherSource.isPlaying) return;

            float baseVolume = Mathf.Lerp(0.2f, 0.7f, intensity);
            float dampen = isIndoors ? IndoorWeatherDampening : 1f;

            WeatherSource.volume = baseVolume * dampen * MasterVolume;
        }

        /// <summary>
        /// Coroutine for smooth crossfading between two ambient audio sources.
        /// </summary>
        private IEnumerator CrossfadeRoutine(AudioSource outgoing, AudioSource incoming, float targetVolume)
        {
            float elapsed = 0f;
            float startVolumeOut = outgoing.volume;
            float startVolumeIn = incoming.volume;

            while (elapsed < CrossfadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / CrossfadeDuration;
                float smoothT = t * t * (3f - 2f * t); // Smoothstep

                outgoing.volume = Mathf.Lerp(startVolumeOut, 0f, smoothT);
                incoming.volume = Mathf.Lerp(startVolumeIn, targetVolume, smoothT);

                yield return null;
            }

            outgoing.volume = 0f;
            outgoing.Stop();
            incoming.volume = targetVolume;

            _crossfadeCoroutine = null;
        }

        /// <summary>
        /// Thunder event routine for storm weather. Plays random thunder clips at
        /// randomized intervals.
        /// </summary>
        private IEnumerator ThunderRoutine()
        {
            while (true)
            {
                float wait = UnityEngine.Random.Range(ThunderMinInterval, ThunderMaxInterval);
                yield return new WaitForSeconds(wait);

                if (ThunderSource != null && ThunderClips != null && ThunderClips.Length > 0)
                {
                    int idx = UnityEngine.Random.Range(0, ThunderClips.Length);
                    ThunderSource.clip = ThunderClips[idx];
                    ThunderSource.volume = ThunderVolume * MasterVolume *
                        (_isIndoors ? IndoorWeatherDampening : 1f);
                    ThunderSource.Play();
                }
            }
        }

        private void InitializeAudioSources()
        {
            if (AmbientSourceA == null)
            {
                AmbientSourceA = gameObject.AddComponent<AudioSource>();
                AmbientSourceA.playOnAwake = false;
                AmbientSourceA.spatialBlend = 0f; // 2D ambient
            }

            if (AmbientSourceB == null)
            {
                AmbientSourceB = gameObject.AddComponent<AudioSource>();
                AmbientSourceB.playOnAwake = false;
                AmbientSourceB.spatialBlend = 0f;
            }

            if (WeatherSource == null)
            {
                WeatherSource = gameObject.AddComponent<AudioSource>();
                WeatherSource.playOnAwake = false;
                WeatherSource.spatialBlend = 0f;
            }

            if (ThunderSource == null)
            {
                ThunderSource = gameObject.AddComponent<AudioSource>();
                ThunderSource.playOnAwake = false;
                ThunderSource.spatialBlend = 0f;
            }
        }

        private void OnDisable()
        {
            if (_crossfadeCoroutine != null)
                StopCoroutine(_crossfadeCoroutine);
            if (_thunderCoroutine != null)
                StopCoroutine(_thunderCoroutine);
        }
    }
}
