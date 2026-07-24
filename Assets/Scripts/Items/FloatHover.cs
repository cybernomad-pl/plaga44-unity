// =============================================================================
// FloatHover.cs
// CYBERNOMAD -- Keeps a spawned item drifting at its spawn position with a gentle
// vertical sinus (no gravity) until it is grabbed for the first time. On the first
// release after a grab it hands control back to physics (gravity ON) so the item
// falls. Attached by ObjectSpawner when SpawnEntry.floatAtEyeLevel is set.
//
// Cooperates with OVRGrabbable: the Rigidbody is left NON-kinematic (only useGravity
// is toggled). OVRGrabbable.Start() captures isKinematic as its release baseline, so
// touching isKinematic here would freeze the item mid-air on release.
// =============================================================================

using UnityEngine;

namespace Plaga44.Items
{
    [RequireComponent(typeof(Rigidbody))]
    public class FloatHover : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][FloatHover]";

        [Tooltip("Vertical bob amplitude in metres (peak offset from spawn height).")]
        public float amplitude = 0.03f;

        [Tooltip("Bob angular speed (rad/s). Lower = slower drift.")]
        public float speed = 1.0f;

        private Rigidbody _rb;
        private OVRGrabbable _grabbable;
        private Vector3 _center;
        private float _phase;
        private bool _hasBeenGrabbed;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _grabbable = GetComponent<OVRGrabbable>();
            _center = transform.position;
            _rb.useGravity = false;
            // Random phase so multiple floating items don't bob in lockstep.
            _phase = Random.value * Mathf.PI * 2f;
        }

        private void FixedUpdate()
        {
            // While grabbed OVRGrabber owns the transform -- do not fight it.
            if (_grabbable != null && _grabbable.isGrabbed)
            {
                _hasBeenGrabbed = true;
                return;
            }

            // First release after a grab -> hand back to physics and stop hovering.
            if (_hasBeenGrabbed)
            {
                _rb.useGravity = true;
                Debug.Log($"{LOG} {name} released after grab -- gravity ON, hover ended.");
                enabled = false;
                return;
            }

            // Hovering: sit at the spawn point + gentle vertical sinus, no drift.
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            float y = Mathf.Sin(Time.fixedTime * speed + _phase) * amplitude;
            _rb.position = _center + Vector3.up * y;
        }
    }
}
