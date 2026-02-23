// GrabbableCohesion.cs
// CYBERNOMAD -- Contact-only friction boost for stacking grabbable objects.
// Only OnCollisionStay (no distance attraction). Applies ONCE per physics
// frame to prevent multi-contact force amplification (magnet effect).
// Disabled when grabbed or for 1s after release (protects throw).

using UnityEngine;

public class GrabbableCohesion : MonoBehaviour
{
    [Tooltip("Extra damping on relative sliding between touching grabbables.")]
    public float contactDamping = 3.0f;

    [Tooltip("Seconds after release where cohesion is disabled (protects throw).")]
    public float releaseCooldown = 1.0f;

    private Rigidbody _rb;
    private OVRGrabbable _grabbable;
    private bool _wasGrabbed;
    private float _releaseTime = -10f;
    private bool _appliedThisFrame;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _grabbable = GetComponent<OVRGrabbable>();

        if (Physics.defaultSolverIterations < 12)
            Physics.defaultSolverIterations = 12;
    }

    void FixedUpdate()
    {
        _appliedThisFrame = false;

        // Track grab/release for cooldown
        bool grabbed = _grabbable != null && _grabbable.isGrabbed;
        if (_wasGrabbed && !grabbed)
            _releaseTime = Time.time;
        _wasGrabbed = grabbed;
    }

    void OnCollisionStay(Collision collision)
    {
        // Only ONCE per physics frame -- prevents multi-contact amplification
        if (_appliedThisFrame) return;

        if (_grabbable != null && _grabbable.isGrabbed) return;
        if (_rb == null || _rb.isKinematic) return;
        if (Time.time - _releaseTime < releaseCooldown) return;

        // Only between grabbables
        var otherGrabbable = collision.gameObject.GetComponent<OVRGrabbable>();
        if (otherGrabbable == null) return;
        if (otherGrabbable.isGrabbed) return;

        var otherRb = collision.rigidbody;
        if (otherRb == null) return;

        // Oppose relative sliding -- like extra friction between grabbables
        Vector3 relativeVel = _rb.linearVelocity - otherRb.linearVelocity;
        _rb.AddForce(-relativeVel * contactDamping, ForceMode.Force);

        _appliedThisFrame = true;
    }
}
