using UnityEngine;

namespace Plaga44.Gameplay
{
    /// <summary>
    /// Attach to a projectile (stone, rock, etc.) that has a Rigidbody.
    /// On collision, checks whether the struck object is part of a HitTarget,
    /// calculates impact force (velocity * mass) and calls HitTarget.RegisterHit.
    /// Also triggers haptic feedback on the controller that threw the stone.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class HitDetector : MonoBehaviour
    {
        private const string LOG = "[PLAGA44]";

        [Tooltip("Who threw or launched this projectile. Passed to HitTarget.OnHit.")]
        public Transform thrower;

        private Rigidbody _rb;
        private RuntimeGrabbable _grabbable;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            // Cache the grabbable so we can read LastGrabController on impact.
            _grabbable = GetComponent<RuntimeGrabbable>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            HitZone zone = collision.collider.GetComponent<HitZone>();

            if (zone == null)
            {
                // Stone hit a non-target surface -- soft thud feedback.
                TriggerMissHaptic();
                return;
            }

            HitTarget target = zone.GetOwner();
            if (target == null)
            {
                Debug.LogWarning($"{LOG} HitZone on {collision.collider.name} has no parent HitTarget.");
                TriggerMissHaptic();
                return;
            }

            float force = _rb.linearVelocity.magnitude * _rb.mass;
            Vector3 impactDir = _rb.linearVelocity.normalized;

            target.RegisterHit(zone, force, thrower, impactDir);

            // Haptic: feedback on the hand that threw the stone.
            string zoneName = zone.zoneType.ToString().ToLower();
            // Map HitZoneType names to HapticFeedback zone strings.
            // Head -> "head", Body -> "torso", arms/legs -> "limb"
            string hapticZone = zoneName == "head"  ? "head"  :
                                zoneName == "body"  ? "torso" : "limb";
            OVRInput.Controller ctrl = GetThrowerController();
            HapticFeedback.HitTarget(ctrl, hapticZone);
        }

        /// <summary>
        /// Gets the OVRInput.Controller that last held this stone.
        /// Falls back to Controller.Active if no RuntimeGrabbable is present.
        /// </summary>
        private OVRInput.Controller GetThrowerController()
        {
            if (_grabbable != null && _grabbable.LastGrabController != OVRInput.Controller.None)
                return _grabbable.LastGrabController;
            return OVRInput.Controller.Active;
        }

        /// <summary>
        /// Triggers soft-thud haptic when the stone hits a non-target surface.
        /// Only fires once (first collision) to avoid rapid spam.
        /// </summary>
        private bool _missFired;

        private void TriggerMissHaptic()
        {
            if (_missFired) return;
            _missFired = true;
            HapticFeedback.HitMiss(GetThrowerController());
        }
    }
}
