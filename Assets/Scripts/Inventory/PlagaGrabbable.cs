// =============================================================================
// PlagaGrabbable.cs
// CYBERNOMAD -- OVRGrabbable subclass with integrated haptic feedback and
// holster return-on-release. Auto-wires HapticOnGrab on the same GameObject.
//
// When grabbed: plays grab haptic (modulated by mass).
// When released outside a holster: normal physics drop + release haptic.
// When released inside a holster volume: snaps back to holster anchor.
// =============================================================================

using UnityEngine;
using Plaga44.Feedback;

namespace Plaga44.Inventory
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class PlagaGrabbable : OVRGrabbable
    {
        private const string LOG = "[PLAGA44][Grabbable]";

        [Header("Holster Return")]
        [Tooltip("If assigned, item snaps to this anchor when released near it.")]
        public HolsterAnchor homeHolster;

        private HapticOnGrab _haptic;

        protected override void Start()
        {
            base.Start();
            _haptic = GetComponent<HapticOnGrab>();
            if (_haptic == null)
                Debug.LogWarning($"{LOG} {name} missing HapticOnGrab component -- no haptic feedback.");
        }

        public override void GrabBegin(OVRGrabber hand, Collider grabPoint)
        {
            base.GrabBegin(hand, grabPoint);
            var controller = ResolveController(hand);
            Debug.Log($"{LOG} GrabBegin: {name} by {controller}");
            if (_haptic != null) _haptic.OnGrab(controller);

            // Remove from holster if attached
            if (homeHolster != null && homeHolster.ContainedItem == gameObject)
                homeHolster.Release();
        }

        public override void GrabEnd(Vector3 linearVelocity, Vector3 angularVelocity)
        {
            var controller = ResolveController(m_grabbedBy);
            Debug.Log($"{LOG} GrabEnd: {name} released, vel={linearVelocity.magnitude:F2}m/s");
            if (_haptic != null) _haptic.OnRelease(controller);

            base.GrabEnd(linearVelocity, angularVelocity);

            // Auto-return to holster if released near it
            if (homeHolster != null && homeHolster.IsInRange(transform.position))
            {
                Debug.Log($"{LOG} {name} snapping back to {homeHolster.name}");
                homeHolster.Holster(gameObject);
            }
        }

        private static OVRInput.Controller ResolveController(OVRGrabber hand)
        {
            if (hand == null) return OVRInput.Controller.None;
            // OVRGrabber exposes m_controller -- check by name convention as fallback
            string n = hand.gameObject.name.ToLowerInvariant();
            if (n.Contains("right")) return OVRInput.Controller.RTouch;
            if (n.Contains("left"))  return OVRInput.Controller.LTouch;
            return OVRInput.Controller.None;
        }
    }
}
