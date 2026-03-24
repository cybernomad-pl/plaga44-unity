// AUTO-DISABLED: PLAGA44_FULL_SDK
#if PLAGA44_FULL_SDK
// SeatInteractable.cs
// PLAGA '44 -- Place this on any seat object (chair, crate, ground).
// Requires a Trigger Collider on the same GameObject or a child.
// When the VR player enters the trigger zone: hips are pinned to the seat point
// and SimpleIKController is activated (full weight) so legs rest naturally.
// Player exits by pressing the configured eject button or moving away.
//
// Namespace: Plaga44.IK

using UnityEngine;
using UnityEngine.Events;

namespace Plaga44.IK
{
    /// <summary>
    /// Seat interactable. Pins the player's hips to a defined seat point and
    /// activates leg IK so legs rest on the floor rather than floating.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SeatInteractable : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("Seat Configuration")]
        [Tooltip("The exact world-space point where the player's hips are placed when seated. " +
                 "If null the transform of this GameObject is used.")]
        public Transform seatPoint;

        [Tooltip("Tag that identifies the VR player rig root collider.")]
        public string playerTag = "Player";

        [Tooltip("Transform that represents the player's hip/waist anchor. " +
                 "If null, the component searches the player for a child named 'Hips' or 'Hip'.")]
        public Transform hipAnchor;

        [Tooltip("SimpleIKController on the player rig. Auto-found via GetComponentInChildren if null.")]
        public SimpleIKController ikController;

        [Header("Eject")]
#if HAS_META_XR
        [Tooltip("OVR button that ejects the player from the seat (requires Meta XR SDK).")]
        public OVRInput.Button ejectButton = OVRInput.Button.Two; // B / Y
#endif

        [Tooltip("Minimum seconds the player must be seated before the eject button works. " +
                 "Prevents accidental instant eject.")]
        [Range(0.1f, 2f)]
        public float ejectCooldown = 0.5f;

        [Header("Hip Pinning")]
        [Tooltip("How strongly the hip is pulled to the seat point. 1 = snapped instantly.")]
        [Range(1f, 30f)]
        public float hipPinSpeed = 15f;

        [Tooltip("Offset applied on top of seatPoint: adjusts height of sitting pose.")]
        public Vector3 hipOffset = new Vector3(0f, 0.05f, 0f);

        [Header("Events")]
        public UnityEvent<Transform> OnSit;   // Passes the player transform
        public UnityEvent<Transform> OnStand; // Passes the player transform

        // ── Private ──────────────────────────────────────────────────────

        private bool _isSeated;
        private Transform _playerRoot;
        private Transform _resolvedHip;
        private Vector3 _hipOriginalLocalPos;
        private float _seatedTime;

        // ── Unity lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            // Make sure the collider is a trigger
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                col.isTrigger = true;
                Debug.LogWarning($"[SeatInteractable] '{name}': Collider was not a trigger -- set automatically.", this);
            }

            if (seatPoint == null)
                seatPoint = transform;
        }

        private void Update()
        {
            if (!_isSeated || _playerRoot == null) return;

            _seatedTime += Time.deltaTime;

            // Pin hips each frame
            PinHips();

            // Eject on button press (after cooldown)
            if (_seatedTime >= ejectCooldown)
            {
#if HAS_META_XR
                if (OVRInput.GetDown(ejectButton))
                {
                    Eject();
                    return;
                }
#endif
            }
        }

        // ── Trigger detection ────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (_isSeated) return;
            if (!other.CompareTag(playerTag)) return;

            Transform playerRoot = other.transform.root;
            TrySit(playerRoot);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!_isSeated) return;
            if (!other.CompareTag(playerTag)) return;

            // Only eject if this is the same player
            if (other.transform.root == _playerRoot)
                Eject();
        }

        // ── Sit / Stand ──────────────────────────────────────────────────

        private void TrySit(Transform playerRoot)
        {
            _playerRoot = playerRoot;

            // Resolve hip anchor
            _resolvedHip = hipAnchor;
            if (_resolvedHip == null)
            {
                _resolvedHip = playerRoot.Find("Hips") ?? playerRoot.Find("Hip");
                if (_resolvedHip == null)
                {
                    Debug.LogWarning($"[SeatInteractable] '{name}': Could not find Hips transform on player. " +
                                     "Assign hipAnchor manually or name the bone 'Hips'.", this);
                }
            }

            if (_resolvedHip != null)
                _hipOriginalLocalPos = _resolvedHip.localPosition;

            // Find and enable IK
            if (ikController == null)
                ikController = playerRoot.GetComponentInChildren<SimpleIKController>();

            if (ikController != null)
                ikController.SetIKWeight(1f);

            _isSeated = true;
            _seatedTime = 0f;

            OnSit?.Invoke(playerRoot);
            Debug.Log($"[SeatInteractable] Player sat on '{name}'.");
        }

        private void Eject()
        {
            if (!_isSeated) return;

            // Restore hip to its local-space rest pose
            if (_resolvedHip != null)
                _resolvedHip.localPosition = _hipOriginalLocalPos;

            // Fade out IK slightly so the stand-up looks natural
            if (ikController != null)
                ikController.SetIKWeight(0.5f);

            var prev = _playerRoot;
            _playerRoot = null;
            _resolvedHip = null;
            _isSeated = false;

            OnStand?.Invoke(prev);
            Debug.Log($"[SeatInteractable] Player stood up from '{name}'.");
        }

        private void PinHips()
        {
            if (_resolvedHip == null) return;

            Vector3 targetWorld = seatPoint.position + seatPoint.TransformDirection(hipOffset);
            _resolvedHip.position = Vector3.Lerp(
                _resolvedHip.position,
                targetWorld,
                hipPinSpeed * Time.deltaTime);
        }

        // ── Public API ───────────────────────────────────────────────────

        /// <summary>Returns true if a player is currently using this seat.</summary>
        public bool IsOccupied => _isSeated;

        /// <summary>Force-eject the current occupant (e.g., seat is destroyed).</summary>
        public void ForceEject() => Eject();
    }
}
#endif // PLAGA44_FULL_SDK
