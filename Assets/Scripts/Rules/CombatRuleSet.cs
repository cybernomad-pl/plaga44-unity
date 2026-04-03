using UnityEngine;

namespace Plaga44.Rules
{
    /// <summary>
    /// A collection of CombatRule assets evaluated in priority order.
    /// First matching rule wins (index 0 = highest priority).
    ///
    /// Create via: Assets/Data/Rules/ right-click > Create > Plaga44 > Rules > Combat Rule Set
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewCombatRuleSet",
        menuName = "Plaga44/Rules/Combat Rule Set",
        order = 11)]
    public class CombatRuleSet : ScriptableObject
    {
        [Tooltip("Ordered list of rules. First match wins. Rules with more specific conditions should come before wildcard rules.")]
        public CombatRule[] rules = new CombatRule[0];

        /// <summary>
        /// Evaluates all rules in order. Returns the CombatEffect of the FIRST matching rule.
        /// Returns CombatEffect.None() if no rule matches.
        /// </summary>
        /// <param name="source">Object type that caused the hit (Stone, Fist, etc.)</param>
        /// <param name="zone">Hit zone on the target body (Head, Torso, etc.)</param>
        /// <param name="force">Impact force in Newtons.</param>
        public CombatEffect Evaluate(ObjectType source, BodyRegion zone, float force)
        {
            if (rules == null || rules.Length == 0)
            {
                Debug.LogWarning($"[Plaga44] CombatRuleSet '{name}' has no rules defined.");
                return CombatEffect.None();
            }

            foreach (CombatRule rule in rules)
            {
                if (rule == null) continue;

                if (rule.Matches(source, zone, force))
                {
                    Debug.Log($"[Plaga44] Rule matched: '{rule.name}' ({rule.description}) -> {rule.resultEffect}");
                    return rule.BuildEffect();
                }
            }

            return CombatEffect.None();
        }

        /// <summary>
        /// Evaluates and returns the first matching rule object (for debugging).
        /// Returns null if no rule matches.
        /// </summary>
        public CombatRule FindMatchingRule(ObjectType source, BodyRegion zone, float force)
        {
            if (rules == null) return null;

            foreach (CombatRule rule in rules)
            {
                if (rule != null && rule.Matches(source, zone, force))
                    return rule;
            }

            return null;
        }
    }
}
