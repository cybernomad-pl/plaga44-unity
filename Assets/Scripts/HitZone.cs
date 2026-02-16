using UnityEngine;

namespace PLAGA44
{
    /// <summary>
    /// Defines hit zone types for damage calculation.
    /// </summary>
    public enum HitZoneType
    {
        Head,
        Body,
        Limb
    }

    /// <summary>
    /// Marks a collider as a specific hit zone on a character.
    /// Attach to child colliders to define vulnerable areas.
    /// </summary>
    public class HitZone : MonoBehaviour
    {
        [Header("Hit Zone Configuration")]
        [Tooltip("Type of hit zone (Head, Body, Limb)")]
        [SerializeField] private HitZoneType zoneType = HitZoneType.Body;

        /// <summary>
        /// Gets the type of this hit zone.
        /// </summary>
        public HitZoneType GetZoneType()
        {
            return zoneType;
        }

        /// <summary>
        /// Sets the type of this hit zone.
        /// </summary>
        public void SetZoneType(HitZoneType type)
        {
            zoneType = type;
        }
    }
}
