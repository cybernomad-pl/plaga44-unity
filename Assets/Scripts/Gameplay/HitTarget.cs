using System;
using UnityEngine;

namespace Plaga44.Gameplay
{
    /// <summary>
    /// Placed on the root of a target. Child GameObjects carry HitZone components
    /// with their own Colliders marking head, body, limbs, etc.
    ///
    /// Subscribe to OnHit to react to incoming projectiles.
    /// Event parameters: (HitZone zone, float force, Transform thrower)
    /// </summary>
    public class HitTarget : MonoBehaviour
    {
        private const string LOG = "[PLAGA44]";

        /// <summary>
        /// Fired by HitDetector when a projectile contacts one of this target's zones.
        /// </summary>
        public event Action<HitZone, float, Transform> OnHit;

        /// <summary>
        /// Called by HitDetector when it confirms a valid hit on this target.
        /// </summary>
        /// <param name="zone">The HitZone that was struck.</param>
        /// <param name="force">Estimated impact force (velocity * mass) in N.</param>
        /// <param name="thrower">Transform of the object that threw/launched the projectile.</param>
        public void RegisterHit(HitZone zone, float force, Transform thrower)
        {
            Debug.Log($"{LOG} Hit on {name} -- zone: {zone.zoneType}, force: {force:F2} N, thrower: {(thrower != null ? thrower.name : "unknown")}");
            OnHit?.Invoke(zone, force, thrower);
        }
    }
}
