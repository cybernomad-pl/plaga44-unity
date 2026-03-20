using UnityEngine;
using UnityEngine.Events;

namespace Plaga44.Gameplay
{
    /// <summary>
    /// Mors Cerebri -- "brain death". MonoBehaviour placed on a target (enemy root).
    /// Listens for hit events from HitDetector. If BodyZone == Head and
    /// incoming force >= forceThreshold, triggers ragdoll death sequence.
    ///
    /// Wiring: HitDetector calls OnHit(HitData) on this component (BodyZone enum for zone ID).
    /// Requires: RagdollController on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(RagdollController))]
    public class MorsCerebri : MonoBehaviour
    {
        [Header("Death Config")]
        [Tooltip("Minimum force required on a head hit to trigger brain death.")]
        [SerializeField] private float forceThreshold = 10f;

        [Header("Optional FX")]
        [SerializeField] private DeathEffect deathEffect;

        [Header("Events")]
        public UnityEvent OnDeath;

        private RagdollController _ragdoll;
        private bool _isDead;

        private void Awake()
        {
            _ragdoll = GetComponent<RagdollController>();

            // Auto-wire DeathEffect if not assigned but present on same GO
            if (deathEffect == null)
                deathEffect = GetComponent<DeathEffect>();
        }

        /// <summary>
        /// Called by HitDetector when any body part is hit.
        /// </summary>
        /// <param name="data">Hit information including zone, force and direction.</param>
        public void OnHit(HitData data)
        {
            if (_isDead) return;
            if (data.zone != BodyZone.Head) return;
            if (data.force < forceThreshold) return;

            Die(data);
        }

        private void Die(HitData data)
        {
            _isDead = true;

            _ragdoll.ActivateRagdoll(data.force, data.direction, data.hitPoint);

            if (deathEffect != null)
                deathEffect.Play(data.hitPoint, data.direction);

            OnDeath?.Invoke();

            Debug.Log($"[MorsCerebri] {gameObject.name} killed by headshot. Force={data.force:F1}");
        }

        /// <summary>Whether this target is already dead.</summary>
        public bool IsDead => _isDead;
    }

    // -------------------------------------------------------------------------
    // Shared data types used across Gameplay scripts
    // -------------------------------------------------------------------------

    /// <summary>Body zone identifier for hit detection routing.</summary>
    public enum BodyZone
    {
        None,
        Head,
        Torso,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg
    }

    /// <summary>Data payload sent from HitDetector to damage receivers.</summary>
    [System.Serializable]
    public struct HitData
    {
        /// <summary>Which body zone was struck.</summary>
        public BodyZone zone;

        /// <summary>Magnitude of impact force (kg*m/s or arbitrary game units).</summary>
        public float force;

        /// <summary>World-space direction of the incoming projectile/impact.</summary>
        public Vector3 direction;

        /// <summary>World-space point of impact.</summary>
        public Vector3 hitPoint;

        public HitData(BodyZone zone, float force, Vector3 direction, Vector3 hitPoint)
        {
            this.zone      = zone;
            this.force     = force;
            this.direction = direction;
            this.hitPoint  = hitPoint;
        }
    }
}
