using UnityEngine;

namespace Plaga44.Rules
{
    /// <summary>
    /// Enum of object types that can be a source of a hit.
    /// Maps to Neo4j ObjectType nodes (Stone, Fist, Knife, etc.)
    /// </summary>
    public enum ObjectType
    {
        Any = 0,
        Stone = 1,
        Fist = 2,
        Knife = 3,
        Rifle = 4,
        Blunt = 5,
        Explosion = 6
    }

    /// <summary>
    /// Abstract body region categories for combat rule matching.
    /// Maps to Neo4j HitZone nodes.
    /// Unlike Gameplay.HitZoneType (granular anatomical zones), these are
    /// broad regions used as rule conditions with wildcard support.
    /// </summary>
    public enum BodyRegion
    {
        Any = 0,
        Head = 1,
        Torso = 2,
        Limb = 3,
        Back = 4
    }

    /// <summary>
    /// A single combat rule defined as a ScriptableObject.
    /// Describes: IF (sourceObjectType hits hitZone with force >= forceThreshold) THEN resultEffect.
    ///
    /// Create via: Assets/Data/Rules/ right-click > Create > Plaga44 > Rules > Combat Rule
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewCombatRule",
        menuName = "Plaga44/Rules/Combat Rule",
        order = 10)]
    public class CombatRule : ScriptableObject
    {
        [Header("Condition")]
        [Tooltip("Type of object that must cause the hit. Use Any to match all objects.")]
        public ObjectType sourceObjectType = ObjectType.Any;

        [Tooltip("Body region that must be hit. Use Any to match all regions.")]
        public BodyRegion hitZone = BodyRegion.Any;

        [Tooltip("Minimum force (in Newtons) required to trigger this rule.")]
        [Min(0f)]
        public float forceThreshold = 0f;

        [Header("Result")]
        [Tooltip("Primary effect type that fires when this rule is matched.")]
        public CombatEffectType resultEffect = CombatEffectType.None;

        [Tooltip("Stun duration in seconds (only used when resultEffect = Stun).")]
        [Min(0f)]
        public float stunDuration = 2f;

        [Tooltip("KnockBack force magnitude in Newtons (only used when resultEffect = KnockBack).")]
        [Min(0f)]
        public float knockBackForce = 5f;

        [Tooltip("Wound damage points (only used when resultEffect = Wound).")]
        [Min(0f)]
        public float woundDamage = 10f;

        [Header("Meta")]
        [Tooltip("Human-readable rule description (for debugging and Neo4j export).")]
        [TextArea(1, 3)]
        public string description = "";

        /// <summary>
        /// Returns true if this rule matches the given source, zone, and force.
        /// ObjectType.Any and BodyRegion.Any act as wildcards.
        /// </summary>
        public bool Matches(ObjectType source, BodyRegion zone, float force)
        {
            bool sourceMatch = (sourceObjectType == ObjectType.Any) || (sourceObjectType == source);
            bool zoneMatch = (hitZone == BodyRegion.Any) || (hitZone == zone);
            bool forceMatch = force >= forceThreshold;
            return sourceMatch && zoneMatch && forceMatch;
        }

        /// <summary>
        /// Builds and returns a CombatEffect instance from this rule's parameters.
        /// </summary>
        public CombatEffect BuildEffect()
        {
            return resultEffect switch
            {
                CombatEffectType.MorsCerebri => CombatEffect.MorsCerebri(),
                CombatEffectType.Stun        => CombatEffect.Stun(stunDuration),
                CombatEffectType.KnockBack   => CombatEffect.KnockBack(knockBackForce),
                CombatEffectType.Wound       => CombatEffect.Wound(woundDamage),
                _                            => CombatEffect.None()
            };
        }
    }
}
