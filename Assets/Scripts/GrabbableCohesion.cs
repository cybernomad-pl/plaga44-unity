// GrabbableCohesion.cs
// CYBERNOMAD -- Contact-only cohesion for stacking grabbable objects.
// NO distance-based attraction (no magnet effect). Only works when
// two grabbables are physically touching (OnCollisionStay).
// Disabled when grabbed or for 1s after release (so throws work).

using UnityEngine;

public class GrabbableCohesion : MonoBehaviour
{
    [Tooltip("Extra damping applied to relative motion when two grabbables touch.")]
    public float contactDamping = 8.0f;

    [Tooltip("Seconds after release where cohesion is disabled (protects throw).")]
    public float releaseCooldown = 1.0f;

    private Rigidbody _rb;
    private OVRGrabbable _grabbable;
    private bool _wasGrabbed;
    private float _releaseTime = -10f;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _grabbable = GetComponent<OVRGrabbable>();

        // More solver iterations = more stable stacking
        if (Physics.defaultSolverIterations < 12)
            Physics.defaultSolverIterations = 12;
    }

    void FixedUpdate()
    {
        // Track grab/release for cooldown
        bool grabbed = _grabbable != null && _grabbable.isGrabbed;
        if (_wasGrabbed && !grabbed)
            _releaseTime = Time.time;
        _wasGrabbed = grabbed;
    }

    void OnCollisionStay(Collision collision)
    {
        // HARD STOP: grabbed or kinematic = no cohesion
        if (_grabbable != null && _grabbable.isGrabbed) return;
        if (_rb == null || _rb.isKinematic) return;

        // HARD STOP: recently released = no cohesion (protect throw velocity)
        if (Time.time - _releaseTime < releaseCooldown) return;

        // Only between grabbables (not hand colliders, not table, not ground)
        var otherGrabbable = collision.gameObject.GetComponent<OVRGrabbable>();
        if (otherGrabbable == null) return;
        if (otherGrabbable.isGrabbed) return;

        // Extra contact damping: oppose relative sliding motion
        // Acts like super-friction between touching grabbables
        var otherRb = collision.rigidbody;
        if (otherRb == null) return;

        Vector3 relativeVel = _rb.linearVelocity - otherRb.linearVelocity;
        _rb.AddForce(-relativeVel * contactDamping, ForceMode.Force);
    }
}
