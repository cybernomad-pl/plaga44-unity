// PLAGA '44 - Compass UI
// Compass/navigation element for Warsaw 1944 orientation
// CYBERNOMAD 2024-2026

using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.UI.HUD
{
    /// <summary>
    /// Displays a compass strip/rose showing the player's facing direction
    /// and optionally waypoint markers for active objectives.
    /// Designed for VR with subtle, non-intrusive placement.
    /// </summary>
    public class CompassUI : MonoBehaviour
    {
        [Header("Compass Elements")]
        [SerializeField] private RawImage compassStrip;
        [SerializeField] private RectTransform waypointContainer;

        [Header("Reference")]
        [Tooltip("Transform to read rotation from (usually main camera or player head)")]
        [SerializeField] private Transform playerCamera;

        [Header("Waypoint Prefab")]
        [SerializeField] private GameObject waypointMarkerPrefab;

        [Header("Settings")]
        [Tooltip("Width of compass strip in UV space (how much of the strip is visible)")]
        [SerializeField] private float compassVisibleRange = 0.25f;

        [Tooltip("Compass strip width in pixels")]
        [SerializeField] private float compassWidth = 400f;

        [Header("Cardinal Labels")]
        [SerializeField] private Text northLabel;
        [SerializeField] private Text southLabel;
        [SerializeField] private Text eastLabel;
        [SerializeField] private Text westLabel;

        [Header("Colors - CYBERNOMAD Palette")]
        [SerializeField] private Color compassTint = new Color(0.8f, 1f, 0.8f, 0.7f);  // Faded green
        [SerializeField] private Color waypointColor = new Color(1f, 0.85f, 0f);         // Gold
        [SerializeField] private Color objectiveColor = new Color(0.3f, 0.9f, 1f);       // Cyan

        // Active waypoint data
        private struct WaypointData
        {
            public string id;
            public Vector3 worldPosition;
            public string label;
            public bool isObjective;
            public RectTransform uiMarker;
        }

        private System.Collections.Generic.List<WaypointData> activeWaypoints =
            new System.Collections.Generic.List<WaypointData>();

        private float currentHeading;

        private void Start()
        {
            if (playerCamera == null)
            {
                // Try to find main camera
                Camera mainCam = Camera.main;
                if (mainCam != null)
                    playerCamera = mainCam.transform;
            }
        }

        private void Update()
        {
            if (playerCamera == null) return;

            UpdateCompassHeading();
            UpdateWaypointPositions();
        }

        // ----- Public API -----

        /// <summary>
        /// Gets the current compass heading in degrees (0 = North, 90 = East).
        /// </summary>
        public float GetHeading()
        {
            return currentHeading;
        }

        /// <summary>
        /// Gets the current cardinal direction as a string.
        /// </summary>
        public string GetCardinalDirection()
        {
            if (currentHeading >= 337.5f || currentHeading < 22.5f) return "N";
            if (currentHeading < 67.5f) return "NE";
            if (currentHeading < 112.5f) return "E";
            if (currentHeading < 157.5f) return "SE";
            if (currentHeading < 202.5f) return "S";
            if (currentHeading < 247.5f) return "SW";
            if (currentHeading < 292.5f) return "W";
            return "NW";
        }

        /// <summary>
        /// Add a waypoint marker to the compass.
        /// </summary>
        public void AddWaypoint(string id, Vector3 worldPosition, string label, bool isObjective = false)
        {
            // Remove existing waypoint with same id
            RemoveWaypoint(id);

            WaypointData wp = new WaypointData
            {
                id = id,
                worldPosition = worldPosition,
                label = label,
                isObjective = isObjective
            };

            // Instantiate UI marker
            if (waypointMarkerPrefab != null && waypointContainer != null)
            {
                GameObject markerObj = Instantiate(waypointMarkerPrefab, waypointContainer);
                wp.uiMarker = markerObj.GetComponent<RectTransform>();

                // Set marker color
                Image markerImage = markerObj.GetComponent<Image>();
                if (markerImage != null)
                {
                    markerImage.color = isObjective ? objectiveColor : waypointColor;
                }

                // Set label if present
                Text markerLabel = markerObj.GetComponentInChildren<Text>();
                if (markerLabel != null)
                {
                    markerLabel.text = label;
                }
            }

            activeWaypoints.Add(wp);
        }

        /// <summary>
        /// Remove a waypoint marker from the compass.
        /// </summary>
        public void RemoveWaypoint(string id)
        {
            for (int i = activeWaypoints.Count - 1; i >= 0; i--)
            {
                if (activeWaypoints[i].id == id)
                {
                    if (activeWaypoints[i].uiMarker != null)
                    {
                        Destroy(activeWaypoints[i].uiMarker.gameObject);
                    }
                    activeWaypoints.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Clear all waypoints.
        /// </summary>
        public void ClearAllWaypoints()
        {
            foreach (var wp in activeWaypoints)
            {
                if (wp.uiMarker != null)
                    Destroy(wp.uiMarker.gameObject);
            }
            activeWaypoints.Clear();
        }

        // ----- Internal -----

        private void UpdateCompassHeading()
        {
            // Get Y-axis rotation (heading) from camera
            float rawYaw = playerCamera.eulerAngles.y;
            currentHeading = (rawYaw + 360f) % 360f;

            // Update compass strip UV offset
            if (compassStrip != null)
            {
                float uvOffset = currentHeading / 360f;
                Rect uvRect = compassStrip.uvRect;
                uvRect.x = uvOffset - compassVisibleRange * 0.5f;
                uvRect.width = compassVisibleRange;
                compassStrip.uvRect = uvRect;
            }
        }

        private void UpdateWaypointPositions()
        {
            if (playerCamera == null) return;

            Vector3 playerPos = playerCamera.position;
            Vector3 playerForward = playerCamera.forward;
            playerForward.y = 0f;
            playerForward.Normalize();

            for (int i = 0; i < activeWaypoints.Count; i++)
            {
                WaypointData wp = activeWaypoints[i];
                if (wp.uiMarker == null) continue;

                // Calculate angle to waypoint
                Vector3 dirToWaypoint = wp.worldPosition - playerPos;
                dirToWaypoint.y = 0f;

                float angleToWaypoint = Vector3.SignedAngle(playerForward, dirToWaypoint, Vector3.up);

                // Map angle to position on compass strip
                float halfRange = (compassVisibleRange * 360f) * 0.5f;

                if (Mathf.Abs(angleToWaypoint) <= halfRange)
                {
                    // Waypoint is within visible compass range
                    float normalizedPos = angleToWaypoint / halfRange; // -1 to 1
                    float xPos = normalizedPos * (compassWidth * 0.5f);

                    wp.uiMarker.anchoredPosition = new Vector2(xPos, wp.uiMarker.anchoredPosition.y);
                    wp.uiMarker.gameObject.SetActive(true);
                }
                else
                {
                    // Waypoint is behind or far off to the side
                    wp.uiMarker.gameObject.SetActive(false);
                }
            }
        }
    }
}
