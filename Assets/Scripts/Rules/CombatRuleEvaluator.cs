using UnityEngine;
using UnityEngine.Events;

namespace Plaga44.Rules
{
    /// <summary>
    /// Runtime MonoBehaviour that bridges hit detection with combat rule evaluation.
    ///
    /// Usage:
    ///   1. Attach to a character/NPC.
    ///   2. Assign a CombatRuleSet in the inspector.
    ///   3. Call ReceiveHit() from your HitDetector (or physics collision handler).
    ///   4. Wire up the UnityEvents to ragdoll, stun, audio, etc.
    ///
    /// This is the connection point between HitDetector and gameplay effects.
    /// </summary>
    public class CombatRuleEvaluator : MonoBehaviour
    {
        [Header("Rule Set")]
        [Tooltip("ScriptableObject rule set to evaluate hits against.")]
        public CombatRuleSet ruleSet;

        [Header("Events (wire up ragdoll, stun system, etc.)")]
        public UnityEvent onMorsCerebri;
        public UnityEvent<float> onStun;        // arg: duration in seconds
        public UnityEvent<float> onKnockBack;   // arg: force magnitude
        public UnityEvent<float> onWound;       // arg: damage points
        public UnityEvent onNone;

        [Header("Debug")]
        [Tooltip("Log every hit evaluation to the console.")]
        public bool verboseLogging = true;

        /// <summary>
        /// Call this from HitDetector or any collision/physics handler.
        /// </summary>
        /// <param name="source">What hit us.</param>
        /// <param name="zone">Where we were hit.</param>
        /// <param name="force">How hard (Newtons).</param>
        public void ReceiveHit(ObjectType source, BodyRegion zone, float force)
        {
            if (ruleSet == null)
            {
                Debug.LogError($"[Plaga44] {name}: CombatRuleEvaluator has no RuleSet assigned!", this);
                return;
            }

            if (verboseLogging)
                Debug.Log($"[Plaga44] {name}: ReceiveHit -- source={source} zone={zone} force={force:F2}N");

            CombatEffect effect = ruleSet.Evaluate(source, zone, force);
            ApplyEffect(effect);
        }

        /// <summary>
        /// Overload for callers that already have a constructed HitEvent struct.
        /// </summary>
        public void ReceiveHit(HitEvent hit)
        {
            ReceiveHit(hit.source, hit.zone, hit.force);
        }

        private void ApplyEffect(CombatEffect effect)
        {
            if (verboseLogging)
                Debug.Log($"[Plaga44] {name}: Applying effect -- {effect}");

            switch (effect.type)
            {
                case CombatEffectType.MorsCerebri:
                    onMorsCerebri?.Invoke();
                    break;

                case CombatEffectType.Stun:
                    onStun?.Invoke(effect.duration);
                    break;

                case CombatEffectType.KnockBack:
                    onKnockBack?.Invoke(effect.force);
                    break;

                case CombatEffectType.Wound:
                    onWound?.Invoke(effect.damage);
                    break;

                case CombatEffectType.None:
                default:
                    onNone?.Invoke();
                    break;
            }
        }
    }

    /// <summary>
    /// Lightweight struct for passing hit data from HitDetector to CombatRuleEvaluator.
    /// Create with: new HitEvent { source = ObjectType.Stone, zone = BodyRegion.Head, force = 12f }
    /// </summary>
    [System.Serializable]
    public struct HitEvent
    {
        public ObjectType source;
        public BodyRegion zone;
        public float force;
    }
}
