using System;
using UnityEngine;

namespace Plaga44.Locomotion
{
    /// <summary>
    /// Trigger volume that disables controller-based locomotion and activates
    /// room-scale body tracking mode while the player is physically inside.
    ///
    /// Setup:
    ///   1. Add this component to any GameObject that has a Trigger Collider.
    ///   2. The VR rig root must have a <see cref="LocomotionManager"/> somewhere
    ///      in the scene (found automatically at Start).
    ///   3. The player's tracking origin should be tagged "Player" (configurable).
    ///
    /// When the player enters the zone:
    ///   - <see cref="LocomotionManager"/> switches to RoomScale mode.
    ///   - <see cref="OnEnterZone"/> event fires.
    ///
    /// When the player exits:
    ///   - <see cref="LocomotionManager"/> reverts to the previous mode.
    ///   - <see cref="OnExitZone"/> event fires.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BodyTrackingZone : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector fields
        // -------------------------------------------------------------------------

        [Header("Player Detection")]
        [Tooltip("Tag used to identify the player's collider. Must match the VR rig's tag.")]
        public string playerTag = "Player";

        [Header("Mode Restore")]
        [Tooltip("Locomotion mode to restore when the player leaves the zone.")]
        public LocomotionManager.LocomotionMode exitMode = LocomotionManager.LocomotionMode.SmoothLocomotion;

        [Header("Zone Identity")]
        [Tooltip("Optional human-readable name for this zone (used in logs and events).")]
        public string zoneName = "BodyTrackingZone";

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>
        /// Fired when the player enters the zone. Argument is this zone's <see cref="zoneName"/>.
        /// </summary>
        public event Action<string> OnEnterZone;

        /// <summary>
        /// Fired when the player exits the zone. Argument is this zone's <see cref="zoneName"/>.
        /// </summary>
        public event Action<string> OnExitZone;

        // -------------------------------------------------------------------------
        // Runtime state
        // -------------------------------------------------------------------------

        private LocomotionManager _manager;
        private bool _playerInside;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            // Ensure the collider is a trigger.
            var col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                Debug.LogWarning($"[BodyTrackingZone] '{zoneName}': Collider is not a trigger. Forcing isTrigger = true.");
                col.isTrigger = true;
            }
        }

        private void Start()
        {
            _manager = FindFirstObjectByType<LocomotionManager>();

            if (_manager == null)
                Debug.LogWarning($"[BodyTrackingZone] '{zoneName}': No LocomotionManager found in scene. Zone events will still fire but mode won't switch.");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            if (_playerInside) return;

            _playerInside = true;

            Debug.Log($"[BodyTrackingZone] Player entered zone '{zoneName}' -- switching to RoomScale.");

            _manager?.SetMode(LocomotionManager.LocomotionMode.RoomScale);
            OnEnterZone?.Invoke(zoneName);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            if (!_playerInside) return;

            _playerInside = false;

            Debug.Log($"[BodyTrackingZone] Player exited zone '{zoneName}' -- restoring mode {exitMode}.");

            _manager?.SetMode(exitMode);
            OnExitZone?.Invoke(zoneName);
        }

        // -------------------------------------------------------------------------
        // Debug visualisation
        // -------------------------------------------------------------------------

        private void OnDrawGizmos()
        {
            var col = GetComponent<Collider>();
            if (col == null) return;

            Gizmos.color = _playerInside
                ? new Color(0f, 1f, 0f, 0.35f)
                : new Color(0f, 0.6f, 1f, 0.25f);

            if (col is BoxCollider box)
            {
                Matrix4x4 old = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = old;
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.TransformPoint(sphere.center),
                                  sphere.radius * Mathf.Max(transform.lossyScale.x,
                                                            transform.lossyScale.z));
            }

#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.1f, zoneName);
#endif
        }
    }
}
