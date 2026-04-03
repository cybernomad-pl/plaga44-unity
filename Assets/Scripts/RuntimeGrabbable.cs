// RuntimeGrabbable.cs
// CYBERNOMAD -- Safe subclass of OVRGrabbable for runtime AddComponent.
//
// Problem: OVRGrabbable.Awake() does m_grabPoints.Length on a null field.
// When AddComponent<OVRGrabbable>() is called at runtime, Awake() fires
// immediately -- there's no window to set m_grabPoints beforehand.
//
// Solution: Subclass with Awake() that handles the null case.
// Also: ignores collision with CharacterController while grabbed,
// so stones held near/behind head don't push the player.

using UnityEngine;

public class RuntimeGrabbable : OVRGrabbable
{
    // Controller that last grabbed this object -- used for haptic feedback on release/hit.
    // Determined at grab time via OVRInput.GetActiveController().
    private OVRInput.Controller _lastGrabController = OVRInput.Controller.None;

    /// <summary>
    /// Returns the controller that is currently (or was last) holding this object.
    /// Used by HitDetector to route haptic feedback to the correct hand.
    /// </summary>
    public OVRInput.Controller LastGrabController => _lastGrabController;

    /// <summary>
    /// Sets m_allowOffhandGrab (protected in base, no public setter).
    /// </summary>
    public void SetAllowOffhandGrab(bool allow)
    {
        m_allowOffhandGrab = allow;
    }

    void Awake()
    {
        // m_grabPoints is protected in OVRGrabbable -- accessible here.
        if (m_grabPoints == null || m_grabPoints.Length == 0)
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                m_grabPoints = new Collider[] { col };
            }
            else
            {
                // Fallback: grab any collider in children
                col = GetComponentInChildren<Collider>();
                if (col != null)
                    m_grabPoints = new Collider[] { col };
            }
        }
    }

    public override void GrabBegin(OVRGrabber hand, Collider grabPoint)
    {
        base.GrabBegin(hand, grabPoint);
        SetPlayerCollision(ignore: true);

        // Determine which controller just grabbed via active controller at grab moment.
        // OVRGrabber.m_controller is protected so we can't access it directly from here.
        // The hand trigger that fired GrabBegin IS the active controller.
        _lastGrabController = OVRInput.GetActiveController();
        HapticFeedback.Grab(_lastGrabController);
    }

    public override void GrabEnd(Vector3 linearVelocity, Vector3 angularVelocity)
    {
        HapticFeedback.Release(_lastGrabController);
        SetPlayerCollision(ignore: false);
        base.GrabEnd(linearVelocity, angularVelocity);
    }

    /// <summary>
    /// Ignore/restore collision between this stone and the player's CharacterController.
    /// Prevents grabbed stones from pushing the player when held near/behind head.
    /// OVRGrabber.m_player was deliberately not set (SDK bug: never restores collision).
    /// We handle it ourselves with proper restore in GrabEnd.
    /// </summary>
    private void SetPlayerCollision(bool ignore)
    {
        var player = FindFirstObjectByType<OVRPlayerController>();
        if (player == null) return;

        var cc = player.GetComponent<CharacterController>();
        if (cc == null) return;

        foreach (var col in GetComponentsInChildren<Collider>())
        {
            if (!col.isTrigger)
                Physics.IgnoreCollision(col, cc, ignore);
        }
    }
}
