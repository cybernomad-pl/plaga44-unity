using System;
using UnityEngine;

namespace Plaga44.AI
{
    /// <summary>
    /// Health component for enemies. Integrates with the stone-throwing hit system.
    /// Call TakeDamage() from any source -- stone hits, melee, etc.
    /// Headshots deal 2x damage.
    /// Fires OnDeath when HP reaches zero.
    /// </summary>
    public class EnemyHealth : MonoBehaviour
    {
        private const string LOG = "[PLAGA44]";

        [Header("Health")]
        [Tooltip("Maximum and starting HP.")]
        public float maxHP = 100f;

        [Tooltip("Current HP -- shown in Inspector for debugging.")]
        [SerializeField] private float _currentHP;

        public float CurrentHP => _currentHP;
        public bool IsDead => _currentHP <= 0f;

        /// <summary>Fired once when HP drops to or below zero. Carries final damage zone name.</summary>
        public event Action<string> OnDeath;

        // ---- Lifecycle ----

        private void Awake()
        {
            _currentHP = maxHP;
        }

        // ---- Public API ----

        /// <summary>
        /// Apply damage to this enemy.
        /// zone: body part identifier -- "Head" deals 2x damage, all others 1x.
        /// </summary>
        public void TakeDamage(float amount, string zone)
        {
            if (IsDead) return;

            float multiplier = string.Equals(zone, "Head", StringComparison.OrdinalIgnoreCase) ? 2f : 1f;
            float finalDamage = amount * multiplier;

            _currentHP -= finalDamage;
            _currentHP = Mathf.Max(0f, _currentHP);

            Debug.Log($"{LOG} Enemy {name} took {finalDamage:F1} dmg (zone: {zone}, mult: {multiplier}x). HP: {_currentHP:F1}/{maxHP}");

            if (_currentHP <= 0f)
            {
                Die(zone);
            }
        }

        // ---- Internal ----

        private void Die(string killingZone)
        {
            Debug.Log($"{LOG} Enemy {name} DEAD (killing zone: {killingZone}).");
            OnDeath?.Invoke(killingZone);
        }
    }
}
