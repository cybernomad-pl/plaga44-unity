using UnityEngine;

namespace Plaga44.Gameplay
{
    /// <summary>
    /// Throwable stone with owner tracking and impact force calculation.
    ///
    /// Lifecycle:
    ///   OnGrab(thrower)  -- call this when a player/hand picks up the stone
    ///   OnRelease()      -- call this when the stone is released (thrown)
    ///   OnCollisionEnter -- calculates impact force = velocity * mass, raises OnImpact event
    ///
    /// Attach to any GameObject that has a Rigidbody and a Collider.
    /// The Rigidbody is set to kinematic while the stone is held;
    /// it switches to dynamic on release so physics drives the throw.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class ThrowableStone : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Inspector
        // ------------------------------------------------------------------ //

        [Header("Physics")]
        [Tooltip("Mass of the stone in kg. Affects impact force.")]
        [SerializeField] private float _mass = 0.3f;

        [Tooltip("Physics material applied to the collider (optional -- leave null for default).")]
        [SerializeField] private PhysicsMaterial _physicsMaterial;

        [Header("Debug")]
        [Tooltip("Print grab / release / impact events to the Unity Console.")]
        [SerializeField] private bool _debugLog = true;

        // ------------------------------------------------------------------ //
        //  Runtime state (public read, private write)
        // ------------------------------------------------------------------ //

        /// <summary>Transform that last grabbed this stone (null if never grabbed).</summary>
        public Transform CurrentHolder  { get; private set; }

        /// <summary>Transform that last threw this stone (null if never thrown).</summary>
        public Transform LastThrownBy   { get; private set; }

        /// <summary>True while the stone is held by someone.</summary>
        public bool IsHeld              { get; private set; }

        /// <summary>Velocity at the moment of release, in world space.</summary>
        public Vector3 ReleaseVelocity  { get; private set; }

        // ------------------------------------------------------------------ //
        //  Events
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Raised on the first physics frame the stone hits something after being thrown.
        /// Parameters: (stone, collision, impactForce).
        /// impactForce = |velocity| * mass at point of contact.
        /// </summary>
        public event System.Action<ThrowableStone, Collision, float> OnImpact;

        // ------------------------------------------------------------------ //
        //  Private
        // ------------------------------------------------------------------ //

        private Rigidbody  _rb;
        private Collider   _col;
        private bool       _impactFiredSinceRelease;

        private const string LOG = "[ThrowableStone]";

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            _rb  = GetComponent<Rigidbody>();
            _col = GetComponent<Collider>();

            _rb.mass = _mass;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            if (_physicsMaterial != null)
                _col.material = _physicsMaterial;

            // Start kinematic -- player must explicitly grab
            SetKinematic(true);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Only fire impact once per throw, and only after the stone has been released
            if (IsHeld || _impactFiredSinceRelease) return;

            _impactFiredSinceRelease = true;

            // Force = magnitude of momentum at impact: |v| * m
            float impactForce = collision.relativeVelocity.magnitude * _rb.mass;

            if (_debugLog)
            {
                Debug.Log(
                    $"{LOG} '{name}' hit '{collision.gameObject.name}' " +
                    $"| force={impactForce:F2} N " +
                    $"| thrownBy={LastThrownBy?.name ?? "unknown"}");
            }

            OnImpact?.Invoke(this, collision, impactForce);
        }

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Call when a hand / player grabs this stone.
        /// The stone becomes kinematic so it can be parented/moved by the holder.
        /// </summary>
        /// <param name="holder">The Transform of the hand or player grabbing the stone.</param>
        public void OnGrab(Transform holder)
        {
            if (holder == null)
            {
                Debug.LogWarning($"{LOG} OnGrab called with null holder on '{name}'.");
                return;
            }

            CurrentHolder = holder;
            IsHeld        = true;
            _impactFiredSinceRelease = false;

            SetKinematic(true);

            if (_debugLog)
                Debug.Log($"{LOG} '{name}' grabbed by '{holder.name}'");
        }

        /// <summary>
        /// Call when the stone is released (thrown).
        /// The stone becomes dynamic and inherits the provided velocity.
        /// </summary>
        /// <param name="releaseVelocity">
        /// Velocity to apply to the Rigidbody at release, in world space.
        /// Pass Vector3.zero if the hand was stationary.
        /// </param>
        public void OnRelease(Vector3 releaseVelocity)
        {
            if (!IsHeld)
            {
                Debug.LogWarning($"{LOG} OnRelease called on '{name}' but stone is not held.");
                return;
            }

            LastThrownBy     = CurrentHolder;
            CurrentHolder    = null;
            IsHeld           = false;
            ReleaseVelocity  = releaseVelocity;
            _impactFiredSinceRelease = false;

            SetKinematic(false);

            _rb.linearVelocity = releaseVelocity;

            if (_debugLog)
            {
                Debug.Log(
                    $"{LOG} '{name}' released by '{LastThrownBy?.name ?? "unknown"}' " +
                    $"| v={releaseVelocity.magnitude:F2} m/s " +
                    $"| expectedForce={releaseVelocity.magnitude * _rb.mass:F2} N");
            }
        }

        /// <summary>
        /// Convenience overload: release with zero velocity (drop in place).
        /// </summary>
        public void OnRelease() => OnRelease(Vector3.zero);

        /// <summary>
        /// Current impact force estimate based on present Rigidbody velocity.
        /// Useful for HUD / game logic before the stone actually hits anything.
        /// </summary>
        public float EstimatedImpactForce()
            => _rb.linearVelocity.magnitude * _rb.mass;

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        private void SetKinematic(bool kinematic)
        {
            _rb.isKinematic = kinematic;

            if (kinematic)
            {
                _rb.linearVelocity        = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
        }

        // ------------------------------------------------------------------ //
        //  Editor helpers
        // ------------------------------------------------------------------ //

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Keep mass in sync with Rigidbody so the Inspector shows
            // consistent values even before play mode.
            if (_rb == null)
                _rb = GetComponent<Rigidbody>();

            if (_rb != null)
                _rb.mass = _mass;
        }
#endif
    }
}
