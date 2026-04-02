using System.Collections;
using UnityEngine;

namespace Plaga44.Audio
{
    /// <summary>
    /// Ambient zone defined by a trigger collider.
    /// When the player (or any tagged trigger object) enters, the zone's
    /// ambient AudioSource cross-fades in. On exit it cross-fades out.
    ///
    /// Multiple overlapping zones are supported -- each has its own source
    /// and fades independently. The loudest active zone naturally dominates.
    ///
    /// Setup:
    ///   1. Add this component to a GameObject.
    ///   2. Add a Collider with IsTrigger = true (Box, Sphere, or Mesh).
    ///   3. Assign Ambient Clip(s) in the Inspector.
    ///   4. Set Player Tag to the tag used by the VR rig root (default: "Player").
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AmbientZone : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][AmbientZone]";

        // ------------------------------------------------------------------ //
        //  Inspector
        // ------------------------------------------------------------------ //

        [Header("Audio")]
        [Tooltip("Ambient clip for this zone (wind, rain, silence intro, etc.).")]
        [SerializeField] private AudioClip _ambientClip;

        [Tooltip("Target volume when fully inside the zone.")]
        [SerializeField] [Range(0f, 1f)] private float _targetVolume = 0.6f;

        [Tooltip("Duration of the cross-fade in seconds.")]
        [SerializeField] [Range(0.1f, 10f)] private float _crossFadeDuration = 2.0f;

        [Tooltip("Whether the clip should loop.")]
        [SerializeField] private bool _loop = true;

        [Tooltip("Pitch applied to the ambient source.")]
        [SerializeField] [Range(0.5f, 2f)] private float _pitch = 1f;

        [Header("Spatial Settings")]
        [Tooltip("Spatial blend (0 = 2D, 1 = full 3D). " +
                 "For large ambient zones use 0 (omnidirectional).")]
        [SerializeField] [Range(0f, 1f)] private float _spatialBlend = 0f;

        [Tooltip("Enable Meta XR spatializer on this source. " +
                 "Usually false for wide ambient zones.")]
        [SerializeField] private bool _spatialize = false;

        [Header("Trigger")]
        [Tooltip("Tag of the object that activates this zone (VR player root).")]
        [SerializeField] private string _playerTag = "Player";

        // ------------------------------------------------------------------ //
        //  Runtime
        // ------------------------------------------------------------------ //

        private AudioSource _source;
        private Coroutine _fadeCoroutine;
        private bool _playerInside;

        // ------------------------------------------------------------------ //
        //  Lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            // Ensure trigger flag is set
            var col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                Debug.LogWarning($"{LOG} Collider on {gameObject.name} is not a trigger. " +
                                 "Enabling IsTrigger automatically.");
                col.isTrigger = true;
            }

            BuildAudioSource();
        }

        private void BuildAudioSource()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = _ambientClip;
            _source.loop = _loop;
            _source.volume = 0f;
            _source.pitch = _pitch;
            _source.spatialBlend = _spatialBlend;
            _source.spatialize = _spatialize;
            _source.playOnAwake = false;

            if (_ambientClip != null)
                _source.Play();
            else
                Debug.LogWarning($"{LOG} AmbientZone \"{gameObject.name}\" has no ambient clip assigned.");
        }

        // ------------------------------------------------------------------ //
        //  Trigger
        // ------------------------------------------------------------------ //

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(_playerTag))
                return;

            if (_playerInside)
                return;

            _playerInside = true;

            if (_ambientClip == null)
            {
                Debug.LogWarning($"{LOG} Enter zone \"{gameObject.name}\" -- no clip, skipping fade.");
                return;
            }

            if (!_source.isPlaying)
                _source.Play();

            StartFade(_targetVolume);
            Debug.Log($"{LOG} Player entered zone \"{gameObject.name}\" -- fading in.");
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(_playerTag))
                return;

            if (!_playerInside)
                return;

            _playerInside = false;
            StartFade(0f);
            Debug.Log($"{LOG} Player exited zone \"{gameObject.name}\" -- fading out.");
        }

        // ------------------------------------------------------------------ //
        //  Cross-fade
        // ------------------------------------------------------------------ //

        private void StartFade(float targetVolume)
        {
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            _fadeCoroutine = StartCoroutine(FadeRoutine(targetVolume));
        }

        private IEnumerator FadeRoutine(float targetVolume)
        {
            float startVolume = _source.volume;
            float elapsed = 0f;

            while (elapsed < _crossFadeDuration)
            {
                elapsed += Time.deltaTime;
                _source.volume = Mathf.Lerp(startVolume, targetVolume,
                                            elapsed / _crossFadeDuration);
                yield return null;
            }

            _source.volume = targetVolume;

            // Stop the source when fully silent to save CPU
            if (targetVolume <= 0f)
                _source.Stop();

            _fadeCoroutine = null;
        }

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Swap the ambient clip at runtime (e.g., wind changes to rain).
        /// Performs a brief cross-fade: fades out, swaps, fades in.
        /// </summary>
        public void SetClip(AudioClip newClip, float fadeDuration = -1f)
        {
            if (newClip == null) return;

            float duration = fadeDuration > 0f ? fadeDuration : _crossFadeDuration * 0.5f;
            StartCoroutine(SwapClipRoutine(newClip, duration));
        }

        private IEnumerator SwapClipRoutine(AudioClip newClip, float duration)
        {
            // Fade out
            float startVol = _source.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _source.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
                yield return null;
            }

            _source.Stop();
            _source.clip = newClip;
            _ambientClip = newClip;

            if (_playerInside)
            {
                _source.Play();
                // Fade back in
                elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    _source.volume = Mathf.Lerp(0f, _targetVolume, elapsed / duration);
                    yield return null;
                }
                _source.volume = _targetVolume;
            }
        }

        /// <summary>
        /// Returns true if the player is currently inside this zone.
        /// </summary>
        public bool IsPlayerInside => _playerInside;

        /// <summary>
        /// Current live volume of the ambient source.
        /// </summary>
        public float CurrentVolume => _source != null ? _source.volume : 0f;

        private void OnValidate()
        {
            // Sync Inspector changes to a live source in Play mode
            if (_source != null)
            {
                _source.loop = _loop;
                _source.pitch = _pitch;
                _source.spatialBlend = _spatialBlend;
                _source.spatialize = _spatialize;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = _playerInside
                ? new Color(0f, 1f, 0.5f, 0.15f)
                : new Color(0.2f, 0.6f, 1f, 0.08f);

            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = Matrix4x4.TRS(
                    transform.TransformPoint(box.center),
                    transform.rotation,
                    transform.lossyScale);
                Gizmos.DrawCube(Vector3.zero, box.size);
                Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.6f);
                Gizmos.DrawWireCube(Vector3.zero, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.matrix = Matrix4x4.TRS(
                    transform.TransformPoint(sphere.center),
                    transform.rotation,
                    Vector3.one);
                float r = sphere.radius * Mathf.Max(
                    transform.lossyScale.x,
                    transform.lossyScale.y,
                    transform.lossyScale.z);
                Gizmos.DrawSphere(Vector3.zero, r);
            }

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.3f,
                $"AmbientZone\n{gameObject.name}\n{(_ambientClip != null ? _ambientClip.name : "NO CLIP")}");
        }
#endif
    }
}
