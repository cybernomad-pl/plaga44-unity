using UnityEngine;

namespace Plaga44.NPC
{
    /// <summary>
    /// Defines a patrol path as an ordered list of world-space waypoints.
    /// Draws Gizmos in the editor so the path is visible without Play mode.
    /// </summary>
    public class WaypointPath : MonoBehaviour
    {
        [Tooltip("Ordered list of waypoints. NPC walks in sequence and loops.")]
        public Transform[] waypoints = new Transform[0];

        [Tooltip("Draw gizmos even when this GameObject is not selected.")]
        public bool alwaysDrawGizmos = true;

        [Tooltip("Gizmo sphere radius at each waypoint.")]
        public float gizmoRadius = 0.2f;

        [Tooltip("Color of the patrol path gizmos.")]
        public Color gizmoColor = new Color(0f, 1f, 0.4f, 0.8f);

        /// <summary>Returns the number of valid waypoints in the path.</summary>
        public int Count => waypoints != null ? waypoints.Length : 0;

        /// <summary>
        /// Returns the world position of waypoint at index.
        /// Falls back to this transform's position if the waypoint is null.
        /// </summary>
        public Vector3 GetPosition(int index)
        {
            if (waypoints == null || waypoints.Length == 0)
                return transform.position;

            index = ((index % waypoints.Length) + waypoints.Length) % waypoints.Length;
            return waypoints[index] != null ? waypoints[index].position : transform.position;
        }

        /// <summary>Returns the next waypoint index after wrapping around.</summary>
        public int NextIndex(int current)
        {
            if (waypoints == null || waypoints.Length == 0) return 0;
            return (current + 1) % waypoints.Length;
        }

        // ------------------------------------------------------------------ //
        //  Gizmos
        // ------------------------------------------------------------------ //

        private void OnDrawGizmos()
        {
            if (alwaysDrawGizmos) DrawGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (!alwaysDrawGizmos) DrawGizmos();
        }

        private void DrawGizmos()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            Gizmos.color = gizmoColor;

            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;

                Vector3 current = waypoints[i].position;
                Gizmos.DrawSphere(current, gizmoRadius);

#if UNITY_EDITOR
                UnityEditor.Handles.color = gizmoColor;
                UnityEditor.Handles.Label(current + Vector3.up * (gizmoRadius + 0.1f), $"[{i}]");
#endif

                // Draw line to next waypoint
                int next = (i + 1) % waypoints.Length;
                if (waypoints[next] != null)
                {
                    Gizmos.DrawLine(current, waypoints[next].position);
                }
            }
        }
    }
}
