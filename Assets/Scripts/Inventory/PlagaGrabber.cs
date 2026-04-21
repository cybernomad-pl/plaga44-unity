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
            if (m_grabCandidates.Count == 0)
                Debug.LogWarning($"{LOG} Toggle GRAB via {m_controller}: ZERO candidates -- reka za daleko od itemu.");
            else
                Debug.Log($"{LOG} Toggle GRAB via {m_controller} (candidates={m_grabCandidates.Count})");
            base.GrabBegin();
        }

        /// <summary>Nadpisz offset position/rotation trzymanego obiektu w grip
        /// local space. Uzywane przez PlagaGrabbable.ApplyGripConfig gdy user
        /// zmienia slider ITEM GRIP -- bez tego OVRGrabber.MoveGrabbedObject
        /// per FixedUpdate uzywa computed m_grabbedObjectPosOff (na moment
        /// GrabBegin) i nadpisuje nasze transform.localPosition mutate.</summary>
        public void UpdateGrabbedOffset(Vector3 posOffset, Quaternion rotOffset)
        {
            m_grabbedObjectPosOff = posOffset;
            m_grabbedObjectRotOff = rotOffset;
        }

        /// <summary>Wymus grab konkretnego obiektu bez polegania na grab-volume
        /// discovery. Uzywane przez ObjectSpawner do spawnowania itemu od razu
        /// w rece gracza. Gdy grabber juz cos trzyma -> release + destroy
        /// poprzedniego (caller odpowiada za Destroy).</summary>
        /// <returns>true jesli target zostal zlapany.</returns>
        public bool ForceGrab(OVRGrabbable target)
        {
            if (target == null)
            {
                Debug.LogWarning($"{LOG} ForceGrab: target == null");
                return false;
            }
            if (target.isGrabbed && !target.allowOffhandGrab)
            {
                Debug.LogWarning($"{LOG} ForceGrab: target '{target.name}' juz trzymany i nie allowOffhandGrab");
                return false;
            }

            // Zwolnij cokolwiek trzymamy. Destroy poprzedniego = odpowiedzialnosc callera.
            if (m_grabbedObj != null)
            {
                Debug.Log($"{LOG} ForceGrab: releasing current {m_grabbedObj.name} before forcing {target.name}");
                GrabEnd();
            }

            // Clear candidates + dodaj tylko target. base.GrabBegin() iteruje
            // po m_grabCandidates i wybiera najblizszy -- z jednym elementem
            // zawsze wybierze target.
            m_grabCandidates.Clear();
            m_grabCandidates[target] = 1;

            Debug.Log($"{LOG} ForceGrab: {target.name} via {m_controller}");
            // Wywolanie przez our-override trafia do base poniewaz m_grabbedObj==null.
            GrabBegin();
            return m_grabbedObj == target;
        }
    }
}
