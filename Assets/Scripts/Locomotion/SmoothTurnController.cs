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
        private const int LogFrameInterval = 60;

        [Header("Turn Settings")]
        [Tooltip("Max rotation speed in degrees per second at full stick deflection.")]
        public float turnSpeed = 120f;

        [Tooltip("Dead zone -- stick values below this are ignored.")]
        [Range(0.05f, 0.5f)]
        public float deadZone = 0.15f;

        private void Awake()
            => Debug.Log($"{LOG} Awake: turnSpeed={turnSpeed}, deadZone={deadZone}, GO={gameObject.name}");

        private void OnEnable() => Debug.Log($"{LOG} OnEnable");
        private void OnDisable() => Debug.Log($"{LOG} OnDisable");

        private void Update()
        {
            if (!GameState.CanMove) return;

            float turnInput = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).x;
            if (Mathf.Abs(turnInput) < deadZone) return;

            float rotationDelta = CalculateRotationDelta(turnInput);
            transform.Rotate(0f, rotationDelta, 0f);
            LogTurnOccasionally(turnInput, rotationDelta);
        }

        // Remap: zdejmuje dead zone z zakresu, wyniki od dead zone..1 -> 0..1.
        private float CalculateRotationDelta(float turnInput)
        {
            float sign = Mathf.Sign(turnInput);
            float remapped = (Mathf.Abs(turnInput) - deadZone) / (1f - deadZone);
            return sign * remapped * turnSpeed * Time.deltaTime;
        }

        private void LogTurnOccasionally(float turnInput, float rotationDelta)
        {
            if (Time.frameCount % LogFrameInterval != 0) return;
            Debug.Log($"{LOG} Turn: input={turnInput:F2}, delta={rotationDelta:F2}, rigY={transform.eulerAngles.y:F1}");
        }
    }
}
