using UnityEngine;
using Plaga44.Gameplay;

namespace Plaga44.AI
{
    /// <summary>
    /// Placed on each enemy body part collider to receive stone impacts from HitDetector.
    ///
    /// Design:
    ///   HitDetector (on stone) calls OnCollisionEnter.
    ///   HitDetector already handles HitZone targets (mannequins).
    ///   For enemies we use a separate path: EnemyHitReceiver detects the HitDetector
    ///   component on the colliding object and routes damage to EnemyHealth.
    ///
    ///   Damage formula: force * damagePer Newton (default: 1 dmg per N).
    ///   Force is calculated the same way HitDetector does: velocity * mass.
    ///   Minimum force threshold prevents tiny grazes from dealing damage.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class EnemyHitReceiver : MonoBehaviour
    {
        private const string LOG = "[PLAGA44]";

        [Tooltip("Which body zone this collider represents. 'Head' = 2x damage.")]
        public string zoneName = "Body";

        [Tooltip("Reference to the EnemyHealth on the root enemy GameObject.")]
        public EnemyHealth enemyHealth;

        [Tooltip("Damage multiplier: damage = force * damagePerNewton.")]
        public float damagePerNewton = 3f;

        [Tooltip("Minimum impact force (velocity * mass) to register damage. Prevents grazes.")]
        public float minForceThreshold = 1.5f;

        private void Awake()
        {
            // Auto-find EnemyHealth on root if not assigned
            if (enemyHealth == null)
                enemyHealth = GetComponentInParent<EnemyHealth>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (enemyHealth == null || enemyHealth.IsDead) return;

            // Only react to projectiles that have a HitDetector (stones)
            var hitDetector = collision.collider.GetComponent<HitDetector>();
            if (hitDetector == null) return;

            var rb = collision.rigidbody;
            if (rb == null) return;

            float force = rb.linearVelocity.magnitude * rb.mass;
            if (force < minForceThreshold) return;

            float damage = force * damagePerNewton;
            Debug.Log($"{LOG} Stone hit enemy {enemyHealth.name} on {zoneName}. Force: {force:F2}N, Damage: {damage:F1}");

            enemyHealth.TakeDamage(damage, zoneName);
        }
    }
}
