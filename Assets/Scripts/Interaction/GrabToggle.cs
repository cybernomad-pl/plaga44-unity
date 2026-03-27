// GrabToggle.cs
// Snap-grab toggle: grip press = find nearest item, snap to hand.
// Grip press again = release with throw velocity.
// Replaces OVRGrabber logic -- works independently.

using UnityEngine;

public class GrabToggle : MonoBehaviour
{
    [Header("Grab Settings")]
    public float grabRadius = 0.5f;         // how far to search for items
    public float snapSpeed = 20f;           // how fast item snaps to hand

    // Snap offsets per weapon type (from original weapon template)
    private static readonly Vector3 SWORD_POS = new Vector3(0.01f, 0.27f, 0.172f);
    private static readonly Vector3 SWORD_ROT = new Vector3(-54f, 0f, 0f);
    private static readonly Vector3 GUN_POS = new Vector3(-0.0139f, -0.0059f, 0.0228f);
    private static readonly Vector3 GUN_ROT = new Vector3(-64f, 10f, -101f);
    private static readonly Vector3 GUN_SCALE = new Vector3(0.01939102f, 0.01939102f, 0.01939102f);

    private OVRInput.Controller _controller;
    private Transform _handAnchor;
    private GameObject _heldObject;
    private bool _gripWasPressed;
    private Vector3 _originalScale;
    private SkinnedMeshRenderer[] _handRenderers; // hand model to hide when holding

    void Start()
    {
        string n = gameObject.name.ToLowerInvariant();
        bool isLeft = n.Contains("left");
        _controller = isLeft ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;

        // This script sits on LeftControllerAnchor or RightControllerAnchor
        // Hand anchor is the parent (LeftHandAnchor / RightHandAnchor)
        _handAnchor = transform.parent != null ? transform.parent : transform;

        // Find hand mesh renderers (OVRHandPrefab) to hide when holding weapon
        _handRenderers = _handAnchor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
    }

    void Update()
    {
        bool gripPressed = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, _controller) > 0.55f;

        // Toggle on press edge
        if (gripPressed && !_gripWasPressed)
        {
            if (_heldObject != null)
                Release();
            else
                TryGrab();
        }

        // Keep held object snapped to hand
        if (_heldObject != null)
        {
            _heldObject.transform.position = Vector3.Lerp(
                _heldObject.transform.position,
                _handAnchor.TransformPoint(GetSnapPos(_heldObject)),
                Time.deltaTime * snapSpeed);
            _heldObject.transform.rotation = Quaternion.Slerp(
                _heldObject.transform.rotation,
                _handAnchor.rotation * Quaternion.Euler(GetSnapRot(_heldObject)),
                Time.deltaTime * snapSpeed);
        }

        _gripWasPressed = gripPressed;
    }

    void TryGrab()
    {
        // Find nearest grabbable in range
        Collider[] hits = Physics.OverlapSphere(transform.position, grabRadius);
        float bestDist = float.MaxValue;
        GameObject bestObj = null;

        foreach (var hit in hits)
        {
            if (hit == null) continue;
            // Must have OVRGrabbable or be a known weapon
            var grabbable = hit.GetComponent<OVRGrabbable>();
            if (grabbable == null) grabbable = hit.GetComponentInParent<OVRGrabbable>();
            if (grabbable == null) continue;

            // Don't grab if already held by other hand
            var otherGrab = grabbable.GetComponent<GrabToggle>();
            if (otherGrab != null && otherGrab._heldObject == grabbable.gameObject) continue;

            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestObj = grabbable.gameObject;
            }
        }

        if (bestObj == null) return;

        _heldObject = bestObj;
        _originalScale = bestObj.transform.localScale;

        // Disable physics while held
        var rb = bestObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Parent to hand
        bestObj.transform.SetParent(_handAnchor);

        // Apply snap transform
        bestObj.transform.localPosition = GetSnapPos(bestObj);
        bestObj.transform.localRotation = Quaternion.Euler(GetSnapRot(bestObj));
        if (IsGun(bestObj))
            bestObj.transform.localScale = GUN_SCALE;

        // Enable shooting if gun
        var shooting = bestObj.GetComponent<Shooting>();
        if (shooting != null) shooting.enabled = true;

        // Hide hand model when holding weapon
        SetHandVisible(false);

        Debug.Log($"[GRAB] Picked up: {bestObj.name}");
    }

    void Release()
    {
        if (_heldObject == null) return;

        // Show hand model again
        SetHandVisible(true);

        // Unparent
        _heldObject.transform.SetParent(null);
        _heldObject.transform.localScale = _originalScale;

        // Re-enable physics + throw
        var rb = _heldObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            // Throw with controller velocity
            rb.linearVelocity = OVRInput.GetLocalControllerVelocity(_controller) * 1.5f;
            rb.angularVelocity = OVRInput.GetLocalControllerAngularVelocity(_controller);
        }

        // Disable shooting
        var shooting = _heldObject.GetComponent<Shooting>();
        if (shooting != null) shooting.enabled = false;

        Debug.Log($"[GRAB] Released: {_heldObject.name}");
        _heldObject = null;
    }

    Vector3 GetSnapPos(GameObject obj)
    {
        if (IsSword(obj)) return SWORD_POS;
        if (IsGun(obj)) return GUN_POS;
        return Vector3.zero;
    }

    Vector3 GetSnapRot(GameObject obj)
    {
        if (IsSword(obj)) return SWORD_ROT;
        if (IsGun(obj)) return GUN_ROT;
        return Vector3.zero;
    }

    bool IsSword(GameObject obj) =>
        obj.name.ToLowerInvariant().Contains("sword");

    bool IsGun(GameObject obj) =>
        obj.name.ToLowerInvariant().Contains("gun");

    void SetHandVisible(bool visible)
    {
        if (_handRenderers == null) return;
        foreach (var r in _handRenderers)
            if (r != null) r.enabled = visible;
    }
}
