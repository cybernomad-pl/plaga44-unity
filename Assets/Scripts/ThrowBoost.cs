// ThrowBoost.cs
// CYBERNOMAD -- Multiplies throw velocity when OVRGrabbable is released.
// OVRGrabber uses raw controller velocity which feels weak in VR.
// This detects grab->release transition and boosts Rigidbody velocity.

using UnityEngine;

public class ThrowBoost : MonoBehaviour
{
    [Tooltip("Velocity multiplier applied on release. 2.5 = throw 2.5x harder.")]
    public float multiplier = 2.5f;

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
            _rb.linearVelocity *= multiplier;
            _rb.angularVelocity *= multiplier;
        }

        _wasGrabbed = grabbed;
    }
}
