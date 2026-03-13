using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.Gameplay
{
    /// <summary>
    /// RagdollController manages the physics-based death pose of a character.
    ///
    /// Setup (Inspector):
    ///   1. Assign the character's Animator component.
    ///   2. All bones that should ragdoll must have both a Rigidbody and a Collider.
    ///      Use Unity's built-in Ragdoll Wizard (right-click hierarchy) to generate them.
    ///   3. Optionally assign the specific bone that receives the impact force
    ///      (e.g. head bone for headshots). If left empty, the root Rigidbody is used.
    ///
    /// On ActivateRagdoll():
    ///   - Animator is disabled so animation stops driving transforms.
    ///   - All child Rigidbodies are switched from kinematic to dynamic.
    ///   - An impulse force is applied at the hit bone in the impact direction.
    /// </summary>
    public class RagdollController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The Animator controlling this character. Will be disabled on death.")]
        [SerializeField] private Animator animator;

        [Tooltip("Bone that receives the primary impact force (e.g. head bone Transform). " +
                 "If null, the nearest Rigidbody on the root is used.")]
        [SerializeField] private Transform impactBone;

        [Header("Force Config")]
        [Tooltip("Multiplier applied to the incoming hit force when calculating the ragdoll impulse.")]
        [SerializeField] private float ragdollForceMultiplier = 15f;

        [Tooltip("Minimum impact force (after multiplier) to apply as an impulse. " +
                 "Prevents micro-forces from doing nothing meaningful.")]
        [SerializeField] private float forceThreshold = 5f;

        [Header("Collision Layers")]
        [Tooltip("Layer mask for ragdoll bones to avoid self-collision issues.")]
        [SerializeField] private LayerMask ragdollLayer;

        // All Rigidbodies found under this GameObject at startup
        private Rigidbody[] _bones;
        private bool _ragdollActive;

        private void Awake()
        {
            // Collect all rigidbodies (bones) in the hierarchy
            _bones = GetComponentsInChildren<Rigidbody>(includeInactive: true);

            // Auto-find Animator if not assigned
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            // Start in animated mode (kinematic ragdoll bones)
            SetRagdollState(false);
        }

        /// <summary>
        /// Switches the character from animation-driven to physics-driven (ragdoll).
        /// Called by MorsCerebri when a lethal headshot is detected.
        /// </summary>
        /// <param name="hitForce">Raw force magnitude from HitData.</param>
        /// <param name="hitDirection">World-space direction of the projectile.</param>
        /// <param name="hitPoint">World-space point of impact.</param>
        public void ActivateRagdoll(float hitForce, Vector3 hitDirection, Vector3 hitPoint)
        {
            if (_ragdollActive) return;
            _ragdollActive = true;

            // 1. Stop the animator
            if (animator != null)
                animator.enabled = false;

            // 2. Enable all ragdoll Rigidbodies
            SetRagdollState(true);

            // 3. Apply impulse to the impact bone (or root)
            float impulse = hitForce * ragdollForceMultiplier;
            if (impulse < forceThreshold) impulse = forceThreshold;

            Rigidbody targetRb = FindImpactRigidbody(hitPoint);
            if (targetRb != null)
            {
                targetRb.AddForceAtPosition(
                    hitDirection.normalized * impulse,
                    hitPoint,
                    ForceMode.Impulse
                );
            }

            Debug.Log($"[RagdollController] Ragdoll activated on {gameObject.name}. " +
                      $"Impulse={impulse:F1} on bone={targetRb?.name ?? "none"}");
        }

        // -----------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------

        /// <summary>
        /// Enables or disables physics simulation on all ragdoll bones.
        /// When kinematic=true the Animator drives transforms; when false physics takes over.
        /// </summary>
        private void SetRagdollState(bool physicsActive)
        {
            foreach (Rigidbody rb in _bones)
            {
                rb.isKinematic = !physicsActive;
                rb.detectCollisions = physicsActive;
            }
        }

        /// <summary>
        /// Finds the best Rigidbody to receive the impact force.
        /// Prefers the explicitly assigned impactBone; falls back to nearest bone
        /// to the hit point; falls back to the first bone in the list.
        /// </summary>
        private Rigidbody FindImpactRigidbody(Vector3 hitPoint)
        {
            // Explicit assignment wins
            if (impactBone != null)
            {
                var rb = impactBone.GetComponent<Rigidbody>();
                if (rb != null) return rb;
            }

            // Nearest bone to hit point
            if (_bones == null || _bones.Length == 0) return null;

            Rigidbody nearest = null;
            float minDist = float.MaxValue;

            foreach (Rigidbody rb in _bones)
            {
                float dist = Vector3.SqrMagnitude(rb.position - hitPoint);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = rb;
                }
            }

            return nearest;
        }

        /// <summary>Whether the ragdoll is currently active.</summary>
        public bool IsRagdollActive => _ragdollActive;
    }
}
