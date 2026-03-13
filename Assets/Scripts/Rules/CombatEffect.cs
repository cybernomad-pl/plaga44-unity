using UnityEngine;

namespace Plaga44.Rules
{
    /// <summary>
    /// Enum of all possible combat outcomes. Maps 1:1 to Neo4j StatusEffect nodes.
    /// </summary>
    public enum CombatEffectType
    {
        None = 0,
        MorsCerebri = 1,
        Stun = 2,
        KnockBack = 3,
        Wound = 4
    }

    /// <summary>
    /// Result of a combat rule evaluation. Carries the effect type and its parameters.
    /// Produced by CombatRuleSet.Evaluate() and consumed by CombatRuleEvaluator.
    /// </summary>
    [System.Serializable]
    public class CombatEffect
    {
        public CombatEffectType type;

        /// <summary>Stun: duration in seconds.</summary>
        public float duration;

        /// <summary>KnockBack: force magnitude in Newtons.</summary>
        public float force;

        /// <summary>Wound: raw damage points.</summary>
        public float damage;

        public static CombatEffect None()
        {
            return new CombatEffect { type = CombatEffectType.None };
        }

        public static CombatEffect MorsCerebri()
        {
            return new CombatEffect { type = CombatEffectType.MorsCerebri };
        }

        public static CombatEffect Stun(float duration)
        {
            return new CombatEffect { type = CombatEffectType.Stun, duration = duration };
        }

        public static CombatEffect KnockBack(float force)
        {
            return new CombatEffect { type = CombatEffectType.KnockBack, force = force };
        }

        public static CombatEffect Wound(float damage)
        {
            return new CombatEffect { type = CombatEffectType.Wound, damage = damage };
        }

        public override string ToString()
        {
            return type switch
            {
                CombatEffectType.Stun => $"Stun({duration}s)",
                CombatEffectType.KnockBack => $"KnockBack({force}N)",
                CombatEffectType.Wound => $"Wound({damage}dmg)",
                CombatEffectType.MorsCerebri => "MorsCerebri",
                _ => "None"
            };
        }
    }
}
