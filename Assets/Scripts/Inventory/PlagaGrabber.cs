// =============================================================================
// PlagaGrabber.cs
// CYBERNOMAD -- OVRGrabber subclass: TOGGLE grab (press grip to grab, press
// again to release). Replaces default hold-to-grab behaviour.
//
// How it works:
//   - grabEnd is set to -1 in Awake, making the base class release condition
//     impossible (flex can never reach -1). This disables hold-to-release.
//   - GrabBegin() override implements toggle: if already holding, release;
//     if not holding, grab nearest candidate.
//   - Grab volumes are re-enabled after toggle-release so next grab works.
// =============================================================================

using UnityEngine;

namespace Plaga44.Inventory
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlagaGrabber : OVRGrabber
    {
        private const string LOG = "[PLAGA44][PlagaGrabber]";

        /// <summary>Currently held object (or null). For editor tools read-only display.</summary>
        public OVRGrabbable CurrentGrabbed => m_grabbedObj;
        /// <summary>Which controller owns this grabber (LTouch / RTouch).</summary>
        public OVRInput.Controller OwnerController => m_controller;

        protected override void Awake()
        {
            base.Awake();
            // Disable hold-to-release: set grabEnd to impossible value.
            // Base CheckForGrabOrRelease needs m_prevFlex <= grabEnd which
            // can never happen when grabEnd is negative (flex range is 0..1).
            grabEnd = -1f;
            Debug.Log($"{LOG} Toggle-grab mode active (grabEnd=-1)");
        }

        protected override void GrabBegin()
        {
            if (m_grabbedObj != null)
            {
                // Already holding -- toggle release
                Debug.Log($"{LOG} Toggle RELEASE: {m_grabbedObj.name} from {m_controller}");
                GrabEnd();
                // GrabEnd re-enables grab volumes, so candidates can be detected
                // for next grab. No extra action needed.
                return;
            }

            // Not holding -- grab nearest candidate
            Debug.Log($"{LOG} Toggle GRAB via {m_controller}");
            base.GrabBegin();
        }
    }
}
