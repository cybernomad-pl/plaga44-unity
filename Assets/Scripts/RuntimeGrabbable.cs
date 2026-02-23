// RuntimeGrabbable.cs
// CYBERNOMAD -- Safe subclass of OVRGrabbable for runtime AddComponent.
//
// Problem: OVRGrabbable.Awake() does m_grabPoints.Length on a null field.
// When AddComponent<OVRGrabbable>() is called at runtime, Awake() fires
// immediately -- there's no window to set m_grabPoints beforehand.
//
// Solution: Subclass with 'new void Awake()' that handles the null case.
// OVRGrabber sees RuntimeGrabbable as OVRGrabbable (inheritance).

using UnityEngine;

public class RuntimeGrabbable : OVRGrabbable
{
    /// <summary>
    /// Sets m_allowOffhandGrab (protected in base, no public setter).
    /// </summary>
    public void SetAllowOffhandGrab(bool allow)
    {
        m_allowOffhandGrab = allow;
    }

    new void Awake()
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
}
