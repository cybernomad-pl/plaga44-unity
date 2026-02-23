using UnityEngine;

namespace Plaga44.Gameplay
{
    /// <summary>
    /// Identifies which anatomical zone a collider represents on a HitTarget.
    /// Attach to a child GameObject that has its own Collider component.
    /// </summary>
    public enum HitZoneType
    {
        Head,
        Body,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg
    }

    [RequireComponent(typeof(Collider))]
    public class HitZone : MonoBehaviour
    {
        [Tooltip("Which body zone this collider represents.")]
        public HitZoneType zoneType = HitZoneType.Body;

        /// <summary>
        /// Walk up the hierarchy to find the HitTarget that owns this zone.
        /// </summary>
        public HitTarget GetOwner()
        {
            return GetComponentInParent<HitTarget>();
        }
    }
}
