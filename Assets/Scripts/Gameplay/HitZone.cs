using UnityEngine;

namespace Plaga44.Gameplay
{
    /// <summary>
    /// Identifies which anatomical zone a collider represents on a HitTarget.
    /// Attach to a child GameObject that has its own Collider component.
    /// On hit: detaches from parent, gets Rigidbody, flies off with impact force.
    /// Body zone does NOT detach (it's the core).
    /// </summary>
    public enum HitZoneType
    {
        Head,
        Body,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg
    }

    [RequireComponent(typeof(Collider))]
    public class HitZone : MonoBehaviour
    {
        private const string LOG = "[PLAGA44]";

        [Tooltip("Which body zone this collider represents.")]
        public HitZoneType zoneType = HitZoneType.Body;

        [Tooltip("If true, this zone detaches from the target on hit.")]
        public bool detachOnHit = true;

        private bool _detached;

        /// <summary>
        /// Walk up the hierarchy to find the HitTarget that owns this zone.
        /// </summary>
        public HitTarget GetOwner()
        {
            return GetComponentInParent<HitTarget>();
        }

        /// <summary>
        /// Called by HitTarget after RegisterHit. Detaches this body part,
        /// adds Rigidbody, and applies force from the projectile.
        /// </summary>
        public void OnHit(float force, Vector3 impactDirection)
        {
            if (_detached) return;
            if (!detachOnHit) return;
            if (zoneType == HitZoneType.Body) return; // Core never detaches

            _detached = true;

            // Detach from parent
            transform.SetParent(null);

            // Add Rigidbody so it falls with gravity
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;

            // Fling it in impact direction + upward
            Vector3 flingDir = (impactDirection.normalized + Vector3.up * 0.5f).normalized;
            rb.AddForce(flingDir * force * 2f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * force, ForceMode.Impulse);

            Debug.Log($"{LOG} {zoneType} detached from target!");

            // Destroy after 10s so it doesn't pile up
            Destroy(gameObject, 10f);
        }
    }
}
