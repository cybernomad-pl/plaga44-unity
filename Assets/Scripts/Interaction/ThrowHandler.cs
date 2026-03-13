// ThrowHandler.cs
// CYBERNOMAD -- Throw mechanics for grabbed Rigidbody objects.
// Tracks controller velocity over a rolling window and applies it on release.
//
// Usage:
//   1. Add ThrowHandler to the same GameObject as your grab logic.
//   2. Call BeginTracking(controller) when the player grabs an object.
//   3. Call Release(rb) when the player releases -- the Rigidbody gets the throw velocity.
//
// Requires: Rigidbody on the thrown object (isKinematic must be false after release).
// Optional: com.meta.xr.sdk.core (OVRInput) -- falls back to Unity XR InputDevice velocity.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Plaga44.Interaction
{
    /// <summary>
    /// Tracks controller velocity over a short rolling window and applies
    /// linear + angular velocity to a Rigidbody on throw-release.
    /// </summary>
    public class ThrowHandler : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("Throw Tuning")]
        [Tooltip("Multiplier applied to the tracked linear velocity on release.")]
        [SerializeField] private float throwMultiplier = 1.5f;

        [Tooltip("Maximum magnitude of the throw velocity after multiplier (m/s). 0 = no cap.")]
        [SerializeField] private float maxVelocity = 15f;

        [Tooltip("Multiplier applied to the tracked angular velocity on release.")]
        [SerializeField] private float angularMultiplier = 1.0f;

        [Tooltip("How many frames to average for velocity smoothing.")]
        [SerializeField] [Range(1, 20)] private int velocityAverageFrames = 5;

        // ── State ────────────────────────────────────────────────────────

        private bool _tracking;

        // Which hand is currently holding (OVRInput side)
        private bool _isLeftHand;

        // Rolling buffer for linear velocity samples
        private readonly Queue<Vector3> _linearSamples = new Queue<Vector3>();

        // Rolling buffer for angular velocity samples
        private readonly Queue<Vector3> _angularSamples = new Queue<Vector3>();

        // Unity XR InputDevice fallback reference
        private InputDevice _xrDevice;

        // ── Public API ───────────────────────────────────────────────────

        /// <summary>
        /// Start velocity tracking for the given hand.
        /// Call this when the player grabs an object.
        /// </summary>
        /// <param name="isLeft">True for left hand, false for right hand.</param>
        public void BeginTracking(bool isLeft)
        {
            _isLeftHand = isLeft;
            _tracking = true;
            _linearSamples.Clear();
            _angularSamples.Clear();
            _xrDevice = FindXRDevice(isLeft);
        }

        /// <summary>
        /// Stop tracking and apply accumulated velocity to the Rigidbody.
        /// Call this when the player releases the grab.
        /// </summary>
        /// <param name="rb">The Rigidbody to throw. Must not be kinematic after this call.</param>
        public void Release(Rigidbody rb)
        {
            if (rb == null)
            {
                _tracking = false;
                return;
            }

            Vector3 linearVelocity = GetSmoothedLinear();
            Vector3 angularVelocity = GetSmoothedAngular();

            // Apply throw multiplier
            linearVelocity *= throwMultiplier;

            // Clamp to max velocity
            if (maxVelocity > 0f && linearVelocity.magnitude > maxVelocity)
                linearVelocity = linearVelocity.normalized * maxVelocity;

            angularVelocity *= angularMultiplier;

            rb.linearVelocity = linearVelocity;
            rb.angularVelocity = angularVelocity;

            _tracking = false;
            _linearSamples.Clear();
            _angularSamples.Clear();
        }

        // ── Unity lifecycle ──────────────────────────────────────────────

        private void Update()
        {
            if (!_tracking) return;

            Vector3 linear = SampleLinearVelocity();
            Vector3 angular = SampleAngularVelocity();

            // Push sample into rolling window
            _linearSamples.Enqueue(linear);
            _angularSamples.Enqueue(angular);

            while (_linearSamples.Count > velocityAverageFrames)
                _linearSamples.Dequeue();
            while (_angularSamples.Count > velocityAverageFrames)
                _angularSamples.Dequeue();
        }

        // ── Velocity sampling ────────────────────────────────────────────

        private Vector3 SampleLinearVelocity()
        {
#if HAS_META_XR
            var controller = _isLeftHand
                ? OVRInput.Controller.LTouch
                : OVRInput.Controller.RTouch;

            return OVRInput.GetLocalControllerVelocity(controller);
#else
            return SampleXRLinearVelocity();
#endif
        }

        private Vector3 SampleAngularVelocity()
        {
#if HAS_META_XR
            var controller = _isLeftHand
                ? OVRInput.Controller.LTouch
                : OVRInput.Controller.RTouch;

            return OVRInput.GetLocalControllerAngularVelocity(controller);
#else
            return SampleXRAngularVelocity();
#endif
        }

        // ── Unity XR InputDevice fallback ────────────────────────────────

        private Vector3 SampleXRLinearVelocity()
        {
            if (!_xrDevice.isValid)
                _xrDevice = FindXRDevice(_isLeftHand);

            if (!_xrDevice.isValid)
                return Vector3.zero;

            Vector3 velocity;
            if (_xrDevice.TryGetFeatureValue(CommonUsages.deviceVelocity, out velocity))
                return velocity;

            return Vector3.zero;
        }

        private Vector3 SampleXRAngularVelocity()
        {
            if (!_xrDevice.isValid)
                _xrDevice = FindXRDevice(_isLeftHand);

            if (!_xrDevice.isValid)
                return Vector3.zero;

            Vector3 angular;
            if (_xrDevice.TryGetFeatureValue(CommonUsages.deviceAngularVelocity, out angular))
                return angular;

            return Vector3.zero;
        }

        private static InputDevice FindXRDevice(bool isLeft)
        {
            var characteristics = InputDeviceCharacteristics.Controller
                | (isLeft ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right);

            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(characteristics, devices);

            return devices.Count > 0 ? devices[0] : default;
        }

        // ── Smoothing helpers ─────────────────────────────────────────────

        private Vector3 GetSmoothedLinear()
        {
            if (_linearSamples.Count == 0) return Vector3.zero;

            Vector3 sum = Vector3.zero;
            foreach (var v in _linearSamples)
                sum += v;
            return sum / _linearSamples.Count;
        }

        private Vector3 GetSmoothedAngular()
        {
            if (_angularSamples.Count == 0) return Vector3.zero;

            Vector3 sum = Vector3.zero;
            foreach (var v in _angularSamples)
                sum += v;
            return sum / _angularSamples.Count;
        }

        // ── Editor helpers ───────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (throwMultiplier < 0f) throwMultiplier = 0f;
            if (maxVelocity < 0f) maxVelocity = 0f;
            if (angularMultiplier < 0f) angularMultiplier = 0f;
        }
#endif
    }
}
