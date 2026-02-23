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
        /// If Body (torso) is hit -- explodes ALL sibling zones.
        /// </summary>
        public void OnHit(float force, Vector3 impactDirection)
        {
            if (_detached) return;

            if (zoneType == HitZoneType.Body)
            {
                // Torso hit = explode everything
                ExplodeTarget(force, impactDirection);
                return;
            }

            if (!detachOnHit) return;
            Detach(force, impactDirection);
        }

        private void Detach(float force, Vector3 impactDirection)
        {
            if (_detached) return;
            _detached = true;

            // Remember world position before detaching
            Vector3 worldPos = transform.position;
            Quaternion worldRot = transform.rotation;

            transform.SetParent(null);
            transform.position = worldPos;
            transform.rotation = worldRot;

            var rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;

            Vector3 flingDir = (impactDirection.normalized + Vector3.up * 0.5f).normalized;
            rb.AddForce(flingDir * force * 2f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * force, ForceMode.Impulse);

            Debug.Log($"{LOG} {zoneType} detached!");
            Destroy(gameObject, 10f);
        }

        private void ExplodeTarget(float force, Vector3 impactDirection)
        {
            var owner = GetOwner();
            if (owner == null) return;

            // Collect all zones first (iterating while detaching modifies hierarchy)
            var allZones = owner.GetComponentsInChildren<HitZone>();
            foreach (var zone in allZones)
            {
                // Each part flies in a slightly different direction
                Vector3 scatter = impactDirection + Random.insideUnitSphere * 0.5f;
                zone.Detach(force * 0.8f, scatter);
            }

            Debug.Log($"{LOG} Target {owner.name} EXPLODED!");
        }
    }
}
