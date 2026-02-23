using System;
using UnityEngine;

namespace Plaga44.Gameplay
{
    /// <summary>
    /// Placed on the root of a target. Child GameObjects carry HitZone components
    /// with their own Colliders marking head, body, limbs, etc.
    /// On hit: logs impact, fires event, tells the zone to detach.
    /// </summary>
    public class HitTarget : MonoBehaviour
    {
        private const string LOG = "[PLAGA44]";

        /// <summary>
        /// Fired by HitDetector when a projectile contacts one of this target's zones.
        /// </summary>
        public event Action<HitZone, float, Transform> OnHit;

        public void RegisterHit(HitZone zone, float force, Transform thrower, Vector3 impactDirection)
        {
            Debug.Log($"{LOG} Hit on {name} -- zone: {zone.zoneType}, force: {force:F2} N, thrower: {(thrower != null ? thrower.name : "unknown")}");
            OnHit?.Invoke(zone, force, thrower);

            // Tell the zone to detach and fly off
            zone.OnHit(force, impactDirection);
        }
    }
}
