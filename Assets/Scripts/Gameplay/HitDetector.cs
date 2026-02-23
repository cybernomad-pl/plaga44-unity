using UnityEngine;

namespace Plaga44.Gameplay
{
    /// <summary>
    /// Attach to a projectile (stone, rock, etc.) that has a Rigidbody.
    /// On collision, checks whether the struck object is part of a HitTarget,
    /// calculates impact force (velocity * mass) and calls HitTarget.RegisterHit.
    ///
    /// Optionally assign Thrower so HitTarget knows who threw the projectile.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class HitDetector : MonoBehaviour
    {
        private const string LOG = "[PLAGA44]";

        [Tooltip("Who threw or launched this projectile. Passed to HitTarget.OnHit.")]
        public Transform thrower;

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Check the collider we hit -- does it (or its parents) have a HitZone?
            HitZone zone = collision.collider.GetComponent<HitZone>();
            if (zone == null)
            {
                // No zone on this hit -- not a target collider.
                return;
            }

            // Walk up to find the owning HitTarget.
            HitTarget target = zone.GetOwner();
            if (target == null)
            {
                Debug.LogWarning($"{LOG} HitZone on {collision.collider.name} has no parent HitTarget.");
                return;
            }

            // Impact force = momentum magnitude at the moment of collision.
            // Using velocity magnitude * mass (units: kg*m/s, approximates N in impulse terms).
            float force = _rb.linearVelocity.magnitude * _rb.mass;

            target.RegisterHit(zone, force, thrower);
        }
    }
}
