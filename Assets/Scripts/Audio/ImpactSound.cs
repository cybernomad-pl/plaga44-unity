using UnityEngine;

namespace Plaga44.Audio
{
    /// <summary>
    /// Surface types for impact sound selection.
    /// Add new entries as new materials are introduced in the game.
    /// </summary>
    public enum SurfaceType
    {
        Stone,
        Metal,
        Wood,
        Flesh,
        Concrete
    }

    /// <summary>
    /// Plays a spatialised impact sound on collision, chosen by SurfaceType.
    /// Attach to any GameObject with a Rigidbody or a static collider that
    /// should produce impact audio.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ImpactSound : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][ImpactSound]";

        // ------------------------------------------------------------------ //
        //  Inspector
        // ------------------------------------------------------------------ //

        [Header("Surface Identity")]
        [Tooltip("The surface type of this object. Determines which clip bank is used.")]
        [SerializeField] private SurfaceType _surfaceType = SurfaceType.Stone;

        [Header("Impact Clips (per surface type)")]
        [Tooltip("Clips played when this object is struck as Stone.")]
        [SerializeField] private AudioClip[] _stoneClips   = new AudioClip[0];

        [Tooltip("Clips played when this object is struck as Metal.")]
        [SerializeField] private AudioClip[] _metalClips   = new AudioClip[0];

        [Tooltip("Clips played when this object is struck as Wood.")]
        [SerializeField] private AudioClip[] _woodClips    = new AudioClip[0];

        [Tooltip("Clips played when this object is struck as Flesh.")]
        [SerializeField] private AudioClip[] _fleshClips   = new AudioClip[0];

        [Tooltip("Clips played when this object is struck as Concrete.")]
        [SerializeField] private AudioClip[] _concreteClips = new AudioClip[0];

        [Header("Volume Scaling")]
        [Tooltip("Minimum collision impulse required to trigger a sound.")]
        [SerializeField] private float _minImpulseThreshold = 0.5f;

        [Tooltip("Impulse at which volume reaches maximum.")]
        [SerializeField] private float _maxImpulseForFullVolume = 10f;

        [Tooltip("Maximum volume for impact sounds.")]
        [SerializeField] [Range(0f, 1f)] private float _maxVolume = 1f;

        [Tooltip("Random pitch variance applied per impact.")]
        [SerializeField] [Range(0f, 0.5f)] private float _pitchVariance = 0.08f;

        [Header("Cooldown")]
        [Tooltip("Minimum time in seconds between consecutive impact sounds.")]
        [SerializeField] private float _cooldown = 0.1f;

        // ------------------------------------------------------------------ //
        //  State
        // ------------------------------------------------------------------ //

        private float _lastPlayTime = -999f;

        // ------------------------------------------------------------------ //
        //  Collision
        // ------------------------------------------------------------------ //

        private void OnCollisionEnter(Collision collision)
        {
            float impulse = collision.impulse.magnitude;
            if (impulse < _minImpulseThreshold)
                return;

            float now = Time.time;
            if (now - _lastPlayTime < _cooldown)
                return;

            AudioClip clip = PickClip(_surfaceType);
            if (clip == null)
            {
                Debug.LogWarning($"{LOG} No clip assigned for {_surfaceType} on {gameObject.name}.");
                return;
            }

            float volume = Mathf.Clamp01(impulse / _maxImpulseForFullVolume) * _maxVolume;

            // Impact position: first contact point for accurate 3D placement
            Vector3 contactPoint = collision.contacts.Length > 0
                ? collision.contacts[0].point
                : transform.position;

            if (SpatialAudioManager.Instance != null)
            {
                SpatialAudioManager.Instance.PlayAtPosition(clip, contactPoint, volume, _pitchVariance);
            }
            else
            {
                // Fallback: direct PlayClipAtPoint (no spatializer but still 3D-positioned)
                AudioSource.PlayClipAtPoint(clip, contactPoint, volume);
                Debug.LogWarning($"{LOG} SpatialAudioManager not found -- falling back to PlayClipAtPoint.");
            }

            _lastPlayTime = now;
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        private AudioClip PickClip(SurfaceType surface)
        {
            AudioClip[] bank = surface switch
            {
                SurfaceType.Stone    => _stoneClips,
                SurfaceType.Metal    => _metalClips,
                SurfaceType.Wood     => _woodClips,
                SurfaceType.Flesh    => _fleshClips,
                SurfaceType.Concrete => _concreteClips,
                _                    => _stoneClips
            };

            if (bank == null || bank.Length == 0)
                return null;

            return bank[Random.Range(0, bank.Length)];
        }

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Plays an impact sound at an arbitrary world position using this
        /// object's surface type. Useful for triggering from non-physics code
        /// (e.g., melee weapon hit-scan).
        /// </summary>
        public void PlayImpactAt(Vector3 worldPosition, float volume = 1f)
        {
            AudioClip clip = PickClip(_surfaceType);
            if (clip == null)
            {
                Debug.LogWarning($"{LOG} No clip for {_surfaceType} on {gameObject.name}.");
                return;
            }

            if (SpatialAudioManager.Instance != null)
                SpatialAudioManager.Instance.PlayAtPosition(clip, worldPosition, volume, _pitchVariance);
            else
                AudioSource.PlayClipAtPoint(clip, worldPosition, volume);
        }

        /// <summary>
        /// Changes the surface type at runtime (e.g., wet surface changes to Metal after flooding).
        /// </summary>
        public void SetSurfaceType(SurfaceType newType)
        {
            _surfaceType = newType;
        }

        public SurfaceType CurrentSurface => _surfaceType;
    }
}
