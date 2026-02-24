using UnityEngine;

namespace Plaga44.AI
{
    /// <summary>
    /// Defines a patrol route as an ordered list of waypoints.
    /// Supports Loop (circular) and PingPong (back-and-forth) traversal modes.
    /// Draws gizmos in the editor so the route is visible without entering play mode.
    /// </summary>
    public class PatrolPath : MonoBehaviour
    {
        public enum TraversalMode { Loop, PingPong }

        [Tooltip("Ordered list of waypoint transforms the enemy will walk between.")]
        public Transform[] waypoints = new Transform[0];

        [Tooltip("Loop = returns to first waypoint after last. PingPong = reverses direction.")]
        public TraversalMode mode = TraversalMode.Loop;

        // ---- Waypoint traversal ----

        /// <summary>
        /// Returns the next waypoint index given the current index and the current direction.
        /// Updates direction in place when PingPong reverses.
        /// </summary>
        public int GetNextIndex(int currentIndex, ref int direction)
        {
            if (waypoints == null || waypoints.Length == 0) return 0;

            if (mode == TraversalMode.Loop)
            {
                return (currentIndex + 1) % waypoints.Length;
            }

            // PingPong
            int next = currentIndex + direction;
            if (next >= waypoints.Length)
            {
                direction = -1;
                next = waypoints.Length - 2;
                if (next < 0) next = 0;
            }
            else if (next < 0)
            {
                direction = 1;
                next = 1;
                if (next >= waypoints.Length) next = 0;
            }
            return next;
        }

        /// <summary>Returns the world position of waypoint at index, or Vector3.zero if invalid.</summary>
        public Vector3 GetWaypointPosition(int index)
        {
            if (waypoints == null || index < 0 || index >= waypoints.Length) return Vector3.zero;
            if (waypoints[index] == null) return Vector3.zero;
            return waypoints[index].position;
        }

        public bool HasWaypoints => waypoints != null && waypoints.Length > 0;

        // ---- Gizmos ----

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            Gizmos.color = new Color(0.2f, 0.8f, 1.0f, 0.85f);

            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;

                // Draw sphere at each waypoint
                Gizmos.DrawWireSphere(waypoints[i].position, 0.25f);

                // Draw line to next waypoint
                int next = (i + 1) % waypoints.Length;
                if (mode == TraversalMode.PingPong && i == waypoints.Length - 1) continue;
                if (waypoints[next] != null)
                    Gizmos.DrawLine(waypoints[i].position, waypoints[next].position);
            }

            // Draw direction arrows (small lines from midpoint pointing forward)
            Gizmos.color = new Color(1f, 0.8f, 0.0f, 0.7f);
            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                if (waypoints[i] == null || waypoints[i + 1] == null) continue;
                Vector3 mid = (waypoints[i].position + waypoints[i + 1].position) * 0.5f;
                Vector3 dir = (waypoints[i + 1].position - waypoints[i].position).normalized;
                Gizmos.DrawLine(mid, mid + dir * 0.4f);
            }
        }
#endif
    }
}
