using UnityEngine;

namespace Plaga44.Core
{
    /// <summary>
    /// Rozszerzenie OVRGrabbable dla poseable bones.
    /// Automatycznie laczy grab/release z PoseableBone logowaniem.
    /// Kosc po puszczeniu zostaje kinematic (nie spada).
    /// </summary>
    [RequireComponent(typeof(PoseableBone))]
    [RequireComponent(typeof(Rigidbody))]
    public class PoseableGrabbable : OVRGrabbable
    {
        private PoseableBone _bone;

        protected override void Start()
        {
            _bone = GetComponent<PoseableBone>();

            // Upewnij sie ze Rigidbody jest kinematic na start
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        public override void GrabBegin(OVRGrabber hand, Collider grabPoint)
        {
            base.GrabBegin(hand, grabPoint);
            _bone?.OnGrabBegin();
        }

        public override void GrabEnd(Vector3 linearVelocity, Vector3 angularVelocity)
        {
            // NIE przekazuj velocity -- kosc ma zostac w miejscu
            base.GrabEnd(Vector3.zero, Vector3.zero);

            // Wymus kinematic
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            _bone?.OnGrabEnd();
        }
    }
}
