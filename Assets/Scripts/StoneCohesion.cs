// StoneCohesion.cs
// CYBERNOMAD -- Fake attraction force between nearby stones.
// Prevents piles from sliding apart. Only active when stone is at rest
// (not grabbed, low velocity). Pulls toward nearby stones gently.

using UnityEngine;

public class StoneCohesion : MonoBehaviour
{
    [Tooltip("Max distance to attract toward neighbor stones.")]
    public float attractRadius = 0.15f;

    [Tooltip("Attraction force strength.")]
    public float attractForce = 2.0f;

    [Tooltip("Velocity threshold -- only attract when nearly still.")]
    public float restThreshold = 0.15f;

    private Rigidbody _rb;
    private OVRGrabbable _grabbable;
    private static StoneCohesion[] _allStones = new StoneCohesion[0];

    void OnEnable()
    {
        _rb = GetComponent<Rigidbody>();
        _grabbable = GetComponent<OVRGrabbable>();
        RebuildList();
    }

    void OnDisable()
    {
        RebuildList();
    }

    static void RebuildList()
    {
        _allStones = FindObjectsByType<StoneCohesion>(FindObjectsSortMode.None);
    }

    void FixedUpdate()
    {
        // Skip if grabbed or moving fast
        if (_grabbable != null && _grabbable.isGrabbed) return;
        if (_rb.isKinematic) return;
        if (_rb.linearVelocity.magnitude > restThreshold) return;

        Vector3 myPos = transform.position;

        for (int i = 0; i < _allStones.Length; i++)
        {
            var other = _allStones[i];
            if (other == this || other == null) continue;
            if (other._grabbable != null && other._grabbable.isGrabbed) continue;

            Vector3 delta = other.transform.position - myPos;
            float dist = delta.magnitude;

            if (dist < 0.01f || dist > attractRadius) continue;

            // Gentle pull toward neighbor -- stronger when closer
            float strength = attractForce * (1f - dist / attractRadius);
            _rb.AddForce(delta.normalized * strength * Time.fixedDeltaTime, ForceMode.VelocityChange);
        }
    }
}
