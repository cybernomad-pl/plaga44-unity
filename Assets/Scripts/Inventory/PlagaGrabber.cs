// =============================================================================
// PlagaGrabber.cs
// CYBERNOMAD -- OVRGrabber subclass. STANDARDOWY hold-to-release: press grip =
// grab, release grip = drop. Zero toggle logic (fragile, double-fire issues).
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
            Debug.Log($"{LOG} Standard hold-to-release mode (grabBegin={grabBegin}, grabEnd={grabEnd})");
        }
    }
}
