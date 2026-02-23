// GrabbableCohesion.cs
// CYBERNOMAD -- Attraction force between nearby grabbable objects.
// Keeps piles stable. Works on ANY OVRGrabbable, not just stones.
// Grabbed objects are completely excluded (no force on or toward them).

using UnityEngine;

public class GrabbableCohesion : MonoBehaviour
{
    [Tooltip("Max distance for attraction.")]
    public float attractRadius = 0.15f;

    [Tooltip("Base attraction strength.")]
    public float attractForce = 3.0f;

    [Tooltip("Close-range multiplier. Force ramps up sharply when touching.")]
    public float closeBoost = 4.0f;

    [Tooltip("Distance below which close-range boost kicks in.")]
    public float closeRange = 0.05f;

    [Tooltip("Velocity threshold -- only attract when nearly still.")]
    public float restThreshold = 0.2f;

    private Rigidbody _rb;
    private OVRGrabbable _grabbable;

    // Global list of all grabbables for fast iteration
    private static OVRGrabbable[] _allGrabbables;
    private static int _lastRefreshFrame = -1;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _grabbable = GetComponent<OVRGrabbable>();
    }

    void FixedUpdate()
    {
        // HARD STOP if grabbed or kinematic -- zero cohesion
        if (_grabbable != null && _grabbable.isGrabbed) return;
        if (_rb == null || _rb.isKinematic) return;
        if (_rb.linearVelocity.magnitude > restThreshold) return;

        // Refresh grabbable list once per frame (shared across all instances)
        if (_lastRefreshFrame != Time.frameCount)
        {
            _allGrabbables = FindObjectsByType<OVRGrabbable>(FindObjectsSortMode.None);
            _lastRefreshFrame = Time.frameCount;
        }

        Vector3 myPos = transform.position;

        for (int i = 0; i < _allGrabbables.Length; i++)
        {
            var other = _allGrabbables[i];
            if (other == null || other.gameObject == gameObject) continue;

            // Skip grabbed neighbors completely
            if (other.isGrabbed) continue;

            Vector3 delta = other.transform.position - myPos;
            float dist = delta.magnitude;

            if (dist < 0.005f || dist > attractRadius) continue;

            // Force ramps up when closer. Quadratic near contact.
            float t = 1f - dist / attractRadius;
            float strength = attractForce * t;

            // Extra boost at very close range (simulates contact adhesion)
            if (dist < closeRange)
            {
                float closeFactor = 1f - dist / closeRange;
                strength += closeBoost * closeFactor * closeFactor;
            }

            _rb.AddForce(delta.normalized * strength, ForceMode.Force);
        }
    }
}
