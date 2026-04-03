// HandCollisionIgnore.cs
// CYBERNOMAD -- Ignores collision between hand colliders and CharacterController.
// Without this, hand compound colliders (children of controller anchors) are
// INSIDE the CC volume and CC.Move() pushes the player up every frame.
// Add to OVRPlayerController root.

using UnityEngine;

namespace Plaga44.Core
{
public class HandCollisionIgnore : MonoBehaviour
{
    void Start()
    {
        var cc = GetComponent<CharacterController>();
        if (cc == null) return;

        // Find all colliders on controller anchors
        var rig = GetComponentInChildren<OVRCameraRig>();
        if (rig == null) return;

        Transform[] anchors = new Transform[]
        {
            rig.leftControllerAnchor,
            rig.rightControllerAnchor,
            rig.leftHandAnchor,
            rig.rightHandAnchor
        };

        int count = 0;
        foreach (var anchor in anchors)
        {
            if (anchor == null) continue;
            foreach (var col in anchor.GetComponentsInChildren<Collider>(true))
            {
                Physics.IgnoreCollision(col, cc, true);
                count++;
            }
        }

        Debug.Log($"[PLAGA44] HandCollisionIgnore: {count} colliders now ignore CharacterController.");
    }
}
} // namespace Plaga44.Core
