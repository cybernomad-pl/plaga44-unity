using UnityEngine;

namespace Plaga44.Gameplay
{
    /// <summary>
    /// Attach to a projectile (stone, rock, etc.) that has a Rigidbody.
    /// On collision, checks whether the struck object is part of a HitTarget,
    /// calculates impact force (velocity * mass) and calls HitTarget.RegisterHit.
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
            HitZone zone = collision.collider.GetComponent<HitZone>();
            if (zone == null) return;

            HitTarget target = zone.GetOwner();
            if (target == null)
            {
                Debug.LogWarning($"{LOG} HitZone on {collision.collider.name} has no parent HitTarget.");
                return;
            }

            float force = _rb.linearVelocity.magnitude * _rb.mass;
            Vector3 impactDir = _rb.linearVelocity.normalized;

            target.RegisterHit(zone, force, thrower, impactDir);
        }
    }
}
