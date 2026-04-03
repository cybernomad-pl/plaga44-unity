using UnityEngine;

namespace Plaga44.Audio
{
    /// <summary>
    /// Singleton managing spatial audio for PLAGA '44.
    /// Configures Meta XR Audio spatializer at runtime and provides
    /// a central pool of AudioSources for one-shot 3D sounds.
    /// </summary>
    public class SpatialAudioManager : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Audio]";

        // ------------------------------------------------------------------ //
        //  Inspector
        // ------------------------------------------------------------------ //

        [Header("Spatializer")]
        [Tooltip("Name of the Meta XR Audio spatializer plugin as reported by Unity.")]
        [SerializeField] private string _expectedSpatializerPlugin = "MetaXRAudioSpatializerUnity";

        [Tooltip("Enable audio spatializer on startup if not already active.")]
        [SerializeField] private bool _autoEnableSpatializer = true;

        [Header("Source Pool")]
        [Tooltip("Number of pooled AudioSources for one-shot impact / ambient sounds.")]
        [SerializeField] [Range(4, 32)] private int _poolSize = 16;

        [Tooltip("Default spatial blend for pooled sources (1 = full 3D).")]
        [SerializeField] [Range(0f, 1f)] private float _defaultSpatialBlend = 1f;

        [Tooltip("Default Doppler scale for pooled sources.")]
        [SerializeField] [Range(0f, 5f)] private float _dopplerLevel = 0.5f;

        [Tooltip("Default rolloff mode for pooled sources.")]
        [SerializeField] private AudioRolloffMode _rolloffMode = AudioRolloffMode.Logarithmic;

        [Tooltip("Max audible distance for pooled sources.")]
        [SerializeField] private float _maxDistance = 50f;

        // ------------------------------------------------------------------ //
        //  Singleton
        // ------------------------------------------------------------------ //

        private static SpatialAudioManager _instance;

        public static SpatialAudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<SpatialAudioManager>();
                    if (_instance == null)
                        Debug.LogWarning($"{LOG} SpatialAudioManager not found in scene. " +
                                         "Add it or use CYBERNOMAD/Audio/Setup Spatial Audio.");
                }
                return _instance;
            }
        }

        // ------------------------------------------------------------------ //
        //  Pool
        // ------------------------------------------------------------------ //

        private AudioSource[] _pool;
        private int _poolHead;

        // ------------------------------------------------------------------ //
        //  Lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"{LOG} Duplicate SpatialAudioManager destroyed.");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            ConfigureSpatializer();
            BuildPool();
        }

        // ------------------------------------------------------------------ //
        //  Spatializer
        // ------------------------------------------------------------------ //

        private void ConfigureSpatializer()
        {
            string active = AudioSettings.GetSpatializerPluginName();
            Debug.Log($"{LOG} Active spatializer plugin: \"{active}\"");

            if (_autoEnableSpatializer && (string.IsNullOrEmpty(active) || active != _expectedSpatializerPlugin))
            {
                // SetSpatializerPluginName was removed in Unity 6.
                // Spatializer must be configured in Project Settings > Audio.
                Debug.LogWarning($"{LOG} Spatializer mismatch. Expected \"{_expectedSpatializerPlugin}\", " +
                                 $"got \"{active}\". Set it in Project Settings > Audio > Spatializer Plugin.");
            }
            else
            {
                Debug.Log($"{LOG} Meta XR Audio spatializer already active.");
            }
        }

        // ------------------------------------------------------------------ //
        //  Pool
        // ------------------------------------------------------------------ //

        private void BuildPool()
        {
            _pool = new AudioSource[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject($"PooledAudioSource_{i:D2}");
                go.transform.SetParent(transform);
                var src = go.AddComponent<AudioSource>();

                src.playOnAwake = false;
                src.spatialBlend = _defaultSpatialBlend;
                src.dopplerLevel = _dopplerLevel;
                src.rolloffMode = _rolloffMode;
                src.maxDistance = _maxDistance;
                src.spatialize = true;
                src.spatializePostEffects = false;

                _pool[i] = src;
            }

            Debug.Log($"{LOG} AudioSource pool created ({_poolSize} sources).");
        }

        /// <summary>
        /// Returns the next available (non-playing) AudioSource from the pool.
        /// Falls back to the least-recently-used slot if all are active.
        /// </summary>
        public AudioSource GetPooledSource()
        {
            // First pass: find a free source
            for (int i = 0; i < _poolSize; i++)
            {
                int idx = (_poolHead + i) % _poolSize;
                if (!_pool[idx].isPlaying)
                {
                    _poolHead = (idx + 1) % _poolSize;
                    return _pool[idx];
                }
            }

            // All busy -- steal the head (oldest)
            AudioSource stolen = _pool[_poolHead];
            stolen.Stop();
            _poolHead = (_poolHead + 1) % _poolSize;
            Debug.LogWarning($"{LOG} Pool exhausted -- interrupting oldest source.");
            return stolen;
        }

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Plays a one-shot clip at a world-space position with 3D spatialisation.
        /// </summary>
        /// <param name="clip">AudioClip to play.</param>
        /// <param name="position">World-space position of the sound.</param>
        /// <param name="volume">Volume scale (0-1).</param>
        /// <param name="pitchVariance">+/- random pitch variance applied to the clip.</param>
        public void PlayAtPosition(AudioClip clip, Vector3 position,
                                   float volume = 1f, float pitchVariance = 0.05f)
        {
            if (clip == null)
            {
                Debug.LogWarning($"{LOG} PlayAtPosition called with null clip.");
                return;
            }

            AudioSource src = GetPooledSource();
            src.transform.position = position;
            src.clip = clip;
            src.volume = volume;
            src.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
            src.Play();
        }

        /// <summary>
        /// Reports whether the Meta XR Audio spatializer is currently active.
        /// </summary>
        public bool IsSpatializerActive()
        {
            return AudioSettings.GetSpatializerPluginName() == _expectedSpatializerPlugin;
        }

        /// <summary>
        /// Returns the name of the currently active spatializer plugin.
        /// </summary>
        public string GetActiveSpatializerName()
        {
            return AudioSettings.GetSpatializerPluginName();
        }
    }
}
