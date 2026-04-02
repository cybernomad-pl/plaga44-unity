// ThrowBoost.cs
// CYBERNOMAD -- Multiplies throw velocity when OVRGrabbable is released.
// OVRGrabber uses raw controller velocity which feels weak in small-space VR.
// Detects grab->release transition and boosts Rigidbody velocity.

using UnityEngine;

public class ThrowBoost : MonoBehaviour
{
    [Tooltip("Velocity multiplier applied on release. 5 = throw 5x harder.")]
    public float multiplier = 5.0f;

    private OVRGrabbable _grabbable;
    private Rigidbody _rb;
    private bool _wasGrabbed;

    void Start()
    {
        _grabbable = GetComponent<OVRGrabbable>();
        _rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (_grabbable == null || _rb == null) return;

        bool grabbed = _grabbable.isGrabbed;

        // Just released -- boost velocity
        if (_wasGrabbed && !grabbed)
        {
            Vector3 before = _rb.linearVelocity;
            _rb.linearVelocity *= multiplier;
            _rb.angularVelocity *= multiplier;
            Debug.Log($"[PLAGA44] ThrowBoost: {before.magnitude:F1} -> {_rb.linearVelocity.magnitude:F1} m/s (x{multiplier})");
        }

        _wasGrabbed = grabbed;
    }
}
