using UnityEngine;

namespace PLAGA44
{
    /// <summary>
    /// Handles mannequin death mechanic (Mors Cerebri - brain death).
    /// Triggers ragdoll on fatal head impact.
    /// </summary>
    public class MorsCerebri : MonoBehaviour
    {
        [Header("Death Threshold")]
        [Tooltip("Minimum impact force on head to trigger death")]
        [SerializeField] private float fatalHeadImpactThreshold = 5.0f;

        [Header("Ragdoll Configuration")]
        [Tooltip("All rigidbodies to enable on death (bones)")]
        [SerializeField] private Rigidbody[] ragdollBones;

        [Tooltip("Animator to disable on death")]
        [SerializeField] private Animator animator;

        private bool isDead = false;

        /// <summary>
        /// Called when this character is hit by a throwable object.
        /// </summary>
        /// <param name="zoneType">Type of zone hit (Head, Body, Limb)</param>
        /// <param name="impactForce">Force of impact</param>
        /// <param name="attacker">GameObject that threw the object</param>
        public void OnHit(HitZoneType zoneType, float impactForce, GameObject attacker)
        {
            if (isDead)
                return;

            // Check for fatal head impact (Mors Cerebri condition)
            if (zoneType == HitZoneType.Head && impactForce >= fatalHeadImpactThreshold)
            {
                TriggerMorsCerebri(attacker);
            }
        }

        /// <summary>
        /// Triggers Mors Cerebri (brain death) and activates ragdoll.
        /// </summary>
        private void TriggerMorsCerebri(GameObject killer)
        {
            if (isDead)
                return;

            isDead = true;

            // Disable animator if present
            if (animator != null)
            {
                animator.enabled = false;
            }

            // Enable ragdoll physics on all bones
            foreach (Rigidbody bone in ragdollBones)
            {
                if (bone != null)
                {
                    bone.isKinematic = false;
                    bone.detectCollisions = true;
                }
            }

            // Optional: Log death for debugging
            Debug.Log($"{gameObject.name} suffered Mors Cerebri caused by {(killer != null ? killer.name : "unknown")}");
        }

        /// <summary>
        /// Returns whether the character is dead.
        /// </summary>
        public bool IsDead()
        {
            return isDead;
        }

        /// <summary>
        /// Auto-setup: finds all rigidbodies in children and animator on self.
        /// Useful for editor scripts.
        /// </summary>
        public void AutoSetupRagdoll()
        {
            // Find all rigidbodies in children (bones)
            ragdollBones = GetComponentsInChildren<Rigidbody>();

            // Initially disable all ragdoll bones (kinematic)
            foreach (Rigidbody bone in ragdollBones)
            {
                if (bone != null)
                {
                    bone.isKinematic = true;
                }
            }

            // Find animator on this object
            animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            // Ensure ragdoll bones are initially kinematic
            foreach (Rigidbody bone in ragdollBones)
            {
                if (bone != null)
                {
                    bone.isKinematic = true;
                }
            }
        }
    }
}
