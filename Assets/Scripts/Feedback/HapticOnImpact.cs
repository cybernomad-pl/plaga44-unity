// HapticOnImpact.cs
// PLAGA '44 -- MonoBehaviour that triggers haptic feedback on collision.
// Attach to any physics object that should "feel" when it hits something.
//
// On OnCollisionEnter: measures impulse magnitude (approximation of force),
// applies a minimum threshold to suppress micro-collisions, then calls
// HapticManager.PlayImpact on the tracked controller.
//
// The controller to vibrate is set via SetActiveController() -- call this
// from your grab system when the player picks up the object, and clear it
// when released so impacts from non-held objects don't produce feedback.
//
// Guard: #if HAS_META_XR. Without the SDK the methods log to console.

using UnityEngine;

namespace Plaga44.Feedback
{
    public class HapticOnImpact : MonoBehaviour
    {
        [Header("Thresholds")]
        [Tooltip("Minimum collision impulse magnitude (N*s) needed to trigger any feedback. " +
                 "Filters out resting contacts and micro-jitter.")]
        [Min(0f)]
        public float minimumImpulse = 0.5f;

        [Tooltip("Maximum impulse to clamp force passed to HapticManager.PlayImpact. " +
                 "Prevents absurdly large values from breaking anything.")]
        [Min(0f)]
        public float maxImpulse = 50f;

        [Header("Cooldown")]
        [Tooltip("Minimum time (seconds) between consecutive impact haptics. " +
                 "Prevents feedback spam when rolling on a surface.")]
        [Min(0f)]
        public float cooldown = 0.08f;

        [Header("Layer Filter")]
        [Tooltip("If set, only collisions with objects on these layers trigger feedback.")]
        public LayerMask collisionLayers = ~0; // default: all layers

        // -------------------------------------------------------------------------

        private OVRInput.Controller _activeController = OVRInput.Controller.None;
        private float _cooldownTimer;

        // --- Public API ----------------------------------------------------------

        /// <summary>
        /// Set the controller to vibrate on impact. Call when the player grabs this object.
        /// </summary>
        public void SetActiveController(OVRInput.Controller controller)
        {
            _activeController = controller;
        }

        /// <summary>
        /// Clear the active controller. Call when the player releases this object.
        /// Impacts from non-held objects will produce no haptic feedback.
        /// </summary>
        public void ClearActiveController()
        {
            _activeController = OVRInput.Controller.None;
        }

        // --- Unity lifecycle -----------------------------------------------------

        private void Update()
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= Time.deltaTime;
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Skip if not currently held by a controller.
            if (_activeController == OVRInput.Controller.None) return;

            // Cooldown guard.
            if (_cooldownTimer > 0f) return;

            // Layer filter.
            if ((collisionLayers.value & (1 << collision.gameObject.layer)) == 0) return;

            // Compute impulse magnitude as a proxy for impact force.
            float impulse = collision.impulse.magnitude;

            if (impulse < minimumImpulse) return;

            float clampedForce = Mathf.Min(impulse, maxImpulse);

            TriggerImpactFeedback(clampedForce);
            _cooldownTimer = cooldown;
        }

        // --- Internal ------------------------------------------------------------

        private void TriggerImpactFeedback(float force)
        {
#if HAS_META_XR
            if (HapticManager.Instance == null)
            {
                Debug.LogWarning("[HapticOnImpact] HapticManager not found in scene.");
                return;
            }
            HapticManager.Instance.PlayImpact(_activeController, force);
#else
            Debug.Log($"[HapticOnImpact] Impact | ctrl={_activeController} force={force:F2}N");
#endif
        }
    }
}
