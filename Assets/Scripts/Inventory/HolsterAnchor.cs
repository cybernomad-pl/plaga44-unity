// =============================================================================
// HolsterAnchor.cs
// CYBERNOMAD -- Marker + snap anchor for an inventory slot (hip, chest, back).
//
// Responsibilities:
//   - Position: local offset from player root (survives rig moves).
//   - Snap: call Holster(go) to attach an item at this anchor (parents +
//     freezes Rigidbody in kinematic mode).
//   - Release: Release() unparents the item and restores physics.
//   - Range check: IsInRange(worldPos) -- used by PlagaGrabbable to auto-snap.
//   - Debug gizmo: wire sphere in Scene view.
// =============================================================================

using UnityEngine;

namespace Plaga44.Inventory
{
    public class HolsterAnchor : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Holster]";

        [Header("Identity")]
        public string holsterId = "RightHip";

        [Header("Snap Range")]
        [Tooltip("Items released within this radius (m) snap back to anchor.")]
        public float snapRadius = 0.20f;

        [Header("Orientation")]
        [Tooltip("Local rotation applied to holstered item (relative to anchor).")]
        public Vector3 holsterEulerRotation = new Vector3(0f, 0f, 0f);

        [Header("Visual Debug")]
        public Color gizmoColor = new Color(0.9f, 0.5f, 0.1f, 0.7f);

        public GameObject ContainedItem { get; private set; }
        private Rigidbody _itemRb;

        public bool IsEmpty => ContainedItem == null;

        public bool IsInRange(Vector3 worldPos)
            => Vector3.Distance(transform.position, worldPos) <= snapRadius;

        public void Holster(GameObject item)
        {
            if (item == null) return;
            ContainedItem = item;
            _itemRb = item.GetComponent<Rigidbody>();

            if (_itemRb != null)
            {
                _itemRb.isKinematic = true;
                _itemRb.linearVelocity = Vector3.zero;
                _itemRb.angularVelocity = Vector3.zero;
            }

            item.transform.SetParent(transform, worldPositionStays: false);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.Euler(holsterEulerRotation);

            Debug.Log($"{LOG} [{holsterId}] Holstered: {item.name}");
        }

        public void Release()
        {
            if (ContainedItem == null) return;
            Debug.Log($"{LOG} [{holsterId}] Released: {ContainedItem.name}");

            ContainedItem.transform.SetParent(null, worldPositionStays: true);

            if (_itemRb != null) _itemRb.isKinematic = false;

            ContainedItem = null;
            _itemRb = null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, snapRadius);
            // small forward arrow
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * snapRadius);
        }
    }
}
