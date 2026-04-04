// ControllerModeHelper.cs
// PLAGA '44 -- Utility to detect controller vs hand tracking mode.
//
// Quest 3 can switch between hand tracking and controller mode at runtime.
// When in hand tracking mode, OVRHaptics.Config.SampleRateHz == 0 and
// calling OVRInput.SetControllerVibration is a no-op that may cause log spam.
//
// Usage:
//   if (ControllerModeHelper.IsControllerActive(OVRInput.Controller.RTouch))
//       OVRInput.SetControllerVibration(freq, amp, controller);

using UnityEngine;

namespace Plaga44.Core
{
    public static class ControllerModeHelper
    {
        /// <summary>
        /// Returns true if the specified controller is currently connected and
        /// haptics hardware is ready (SampleRateHz > 0).
        /// </summary>
        public static bool IsControllerActive(OVRInput.Controller controller)
        {
            // Check if the specific controller is connected
            if (!OVRInput.IsControllerConnected(controller))
                return false;

            // Check if haptics subsystem is initialized (SampleRateHz > 0)
            if (OVRHaptics.Config.SampleRateHz <= 0)
                return false;

            return true;
        }

        /// <summary>
        /// Returns true if ANY Touch controller is connected and haptics are ready.
        /// Useful for generic "are we in controller mode?" checks.
        /// </summary>
        public static bool AnyControllerActive()
        {
            return IsControllerActive(OVRInput.Controller.LTouch) ||
                   IsControllerActive(OVRInput.Controller.RTouch);
        }

        /// <summary>
        /// Safe wrapper for OVRInput.SetControllerVibration.
        /// Skips the call (and avoids log spam) if the controller is not connected
        /// or SampleRateHz == 0.
        /// </summary>
        public static void SafeVibration(float frequency, float amplitude,
                                          OVRInput.Controller controller)
        {
            if (!IsControllerActive(controller))
                return;

            OVRInput.SetControllerVibration(frequency, amplitude, controller);
        }
    }
}
