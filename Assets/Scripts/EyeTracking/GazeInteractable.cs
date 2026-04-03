// GazeInteractable.cs
// PLAGA '44 -- Component for objects that react to player gaze.
// Fires OnGazeEnter / OnGazeExit / OnGazeDwell(duration) events.
//
// Usage: Add to any GameObject. The GazeInteractableRegistry polls all
//        active instances each frame via EyeTrackingManager.
// Namespace: Plaga44.EyeTracking

using UnityEngine;
using UnityEngine.Events;

namespace Plaga44.EyeTracking
{
    /// <summary>
    /// Attach to any object that should respond to player gaze.
    /// Requires an EyeTrackingManager in the scene.
    /// </summary>
    public class GazeInteractable : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Header("Gaze Settings")]
        [Tooltip("Time in seconds the player must look at this object before OnGazeDwell fires.")]
        [SerializeField] private float _dwellTime = 2f;

        [Tooltip("Angular radius (degrees) within which the gaze is considered 'on' this object. " +
                 "Overrides distance-based checks when > 0.")]
        [SerializeField][Range(0f, 30f)] private float _gazeAngleRadius = 3f;

        [Tooltip("If > 0, use sphere-cast distance instead of angular check. " +
                 "Useful for objects at varying depths.")]
        [SerializeField][Min(0f)] private float _gazeRadius = 0f;

        [Header("Events")]
        [SerializeField] private UnityEvent _onGazeEnter  = new UnityEvent();
        [SerializeField] private UnityEvent _onGazeExit   = new UnityEvent();
        [SerializeField] private GazeDwellEvent _onGazeDwell = new GazeDwellEvent();

        // ── State ─────────────────────────────────────────────────────────

        private bool  _isGazed     = false;
        private float _gazeTimer   = 0f;
        private bool  _dwellFired  = false;

        private EyeTrackingManager _manager;

        // ── Public accessors ─────────────────────────────────────────────

        public bool   IsGazed        => _isGazed;
        public float  GazeTimer      => _gazeTimer;
        public float  DwellTime      { get => _dwellTime;       set => _dwellTime       = value; }
        public float  GazeAngleRadius{ get => _gazeAngleRadius; set => _gazeAngleRadius = value; }
        public float  GazeRadius     { get => _gazeRadius;      set => _gazeRadius      = value; }

        public UnityEvent OnGazeEnterEvent  => _onGazeEnter;
        public UnityEvent OnGazeExitEvent   => _onGazeExit;
        public GazeDwellEvent OnGazeDwellEvent => _onGazeDwell;

        // ── Lifecycle ────────────────────────────────────────────────────

        private void OnEnable()
        {
            GazeInteractableRegistry.Register(this);
        }

        private void OnDisable()
        {
            GazeInteractableRegistry.Unregister(this);
            if (_isGazed) ForceExit();
        }

        private void Start()
        {
            _manager = FindFirstObjectByType<EyeTrackingManager>();
            if (_manager == null)
                Debug.LogWarning($"[GazeInteractable] No EyeTrackingManager found in scene. " +
                                 $"Object '{name}' will not receive gaze events.");
        }

        // ── Called by GazeInteractableRegistry ──────────────────────────

        /// <summary>
        /// Evaluate gaze state for this frame. Called externally by the registry.
        /// </summary>
        internal void Tick(float deltaTime)
        {
            if (_manager == null) return;

            bool currentlyGazed = CheckGaze();

            if (currentlyGazed && !_isGazed)
            {
                _isGazed   = true;
                _gazeTimer = 0f;
                _dwellFired = false;
                _onGazeEnter.Invoke();
            }
            else if (!currentlyGazed && _isGazed)
            {
                ForceExit();
            }

            if (_isGazed)
            {
                _gazeTimer += deltaTime;
                if (!_dwellFired && _gazeTimer >= _dwellTime)
                {
                    _dwellFired = true;
                    _onGazeDwell.Invoke(_gazeTimer);
                }
            }
        }

        // ── Internal helpers ─────────────────────────────────────────────

        private bool CheckGaze()
        {
            if (_manager == null || !_manager.IsEyeTrackingAvailable)
            {
                // Fallback: use angular check from camera when tracking unavailable
                var cam = Camera.main;
                if (cam == null) return false;
                var fallbackRay = new Ray(cam.transform.position, cam.transform.forward);
                return IsRayOnThis(fallbackRay);
            }

            Ray gazeRay = _manager.GetGazeDirection(GazeEye.Center);
            return IsRayOnThis(gazeRay);
        }

        private bool IsRayOnThis(Ray ray)
        {
            if (_gazeRadius > 0f)
            {
                // Sphere-cast against this object's bounds / collider
                var col = GetComponent<Collider>();
                if (col != null)
                {
                    return col.bounds.IntersectRay(ray);
                }
            }

            // Angular check: angle between gaze ray and direction to object center
            Vector3 toObject = (transform.position - ray.origin).normalized;
            float angle = Vector3.Angle(ray.direction, toObject);
            return angle <= _gazeAngleRadius;
        }

        private void ForceExit()
        {
            _isGazed    = false;
            _gazeTimer  = 0f;
            _dwellFired = false;
            _onGazeExit.Invoke();
        }
    }

    // ── Helper types ─────────────────────────────────────────────────────

    /// <summary>UnityEvent that passes the dwell duration as a float parameter.</summary>
    [System.Serializable]
    public class GazeDwellEvent : UnityEvent<float> { }

    /// <summary>
    /// Static registry so GazeInteractable instances can self-register
    /// and be polled each frame without a manager dependency.
    /// </summary>
    public static class GazeInteractableRegistry
    {
        private static readonly System.Collections.Generic.List<GazeInteractable> _instances
            = new System.Collections.Generic.List<GazeInteractable>();

        private static GazeInteractableTicker _ticker;

        public static void Register(GazeInteractable interactable)
        {
            if (!_instances.Contains(interactable))
                _instances.Add(interactable);

            EnsureTicker();
        }

        public static void Unregister(GazeInteractable interactable)
        {
            _instances.Remove(interactable);
        }

        internal static void TickAll(float deltaTime)
        {
            for (int i = _instances.Count - 1; i >= 0; i--)
            {
                if (_instances[i] == null) { _instances.RemoveAt(i); continue; }
                _instances[i].Tick(deltaTime);
            }
        }

        private static void EnsureTicker()
        {
            if (_ticker != null) return;
            var go = new GameObject("GazeInteractableRegistry");
            Object.DontDestroyOnLoad(go);
            _ticker = go.AddComponent<GazeInteractableTicker>();
        }
    }

    /// <summary>
    /// Hidden MonoBehaviour that drives GazeInteractableRegistry.TickAll each frame.
    /// Created automatically when the first GazeInteractable registers.
    /// </summary>
    internal class GazeInteractableTicker : MonoBehaviour
    {
        private void Update() => GazeInteractableRegistry.TickAll(Time.deltaTime);
    }
}
