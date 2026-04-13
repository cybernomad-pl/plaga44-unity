using UnityEngine;

namespace Plaga44.Locomotion
{
    /// <summary>
    /// Smooth turn via right thumbstick. Rotates the VR rig around Y axis.
    /// Attach to OVRCameraRig root (same GO as LocomotionController).
    /// </summary>
    [DisallowMultipleComponent]
    public class SmoothTurnController : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][SmoothTurn]";

        [Header("Turn Settings")]
        [Tooltip("Max rotation speed in degrees per second at full stick deflection.")]
        public float turnSpeed = 120f;

        [Tooltip("Dead zone -- stick values below this are ignored.")]
        [Range(0.05f, 0.5f)]
        public float deadZone = 0.15f;

        private void Awake()
        {
            Debug.Log($"{LOG} Awake: turnSpeed={turnSpeed}, deadZone={deadZone}, GO={gameObject.name}");
        }

        private void OnEnable()
        {
            Debug.Log($"{LOG} OnEnable");
        }

        private void Update()
        {
            if (!GameState.CanMove) return;

            // Read right thumbstick X axis
            float turnInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).x;

            // Dead zone
            if (Mathf.Abs(turnInput) < deadZone) return;

            // Remap: remove dead zone from input range
            float sign = Mathf.Sign(turnInput);
            float remapped = (Mathf.Abs(turnInput) - deadZone) / (1f - deadZone);

            float rotationDelta = sign * remapped * turnSpeed * Time.deltaTime;

            // Rotate rig around Y
            transform.Rotate(0f, rotationDelta, 0f);

            // Log occasionally (every 60 frames)
            if (Time.frameCount % 60 == 0 && Mathf.Abs(turnInput) > deadZone)
            {
                Debug.Log($"{LOG} Turn: input={turnInput:F2}, delta={rotationDelta:F2}, rigY={transform.eulerAngles.y:F1}");
            }
        }
    }
}
