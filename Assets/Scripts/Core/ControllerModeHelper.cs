// =============================================================================
// ControllerModeHelper.cs
// CYBERNOMAD -- Utility to detect controller vs hand tracking mode.
//
// Quest 3 can switch between hand tracking and controller mode at runtime.
// When in hand tracking mode, OVRHaptics.Config.SampleRateHz == 0 and
// calling OVRInput.SetControllerVibration is a no-op that may cause log spam.
// =============================================================================

using UnityEngine;

namespace Plaga44.Core
{
    public static class ControllerModeHelper
    {
        /// <summary>Connected + haptics initialized (SampleRateHz > 0).</summary>
        public static bool IsControllerActive(OVRInput.Controller controller)
        {
            if (!OVRInput.IsControllerConnected(controller)) return false;
            if (OVRHaptics.Config.SampleRateHz <= 0) return false;
            return true;
        }

        /// <summary>True if any Touch controller is connected and ready.</summary>
        public static bool AnyControllerActive()
        {
            return IsControllerActive(OVRInput.Controller.LTouch) ||
                   IsControllerActive(OVRInput.Controller.RTouch);
        }

        /// <summary>Safe wrapper: skips call (no log spam) if controller inactive.</summary>
        public static void SafeVibration(float frequency, float amplitude, OVRInput.Controller controller)
        {
            if (!IsControllerActive(controller)) return;
            OVRInput.SetControllerVibration(frequency, amplitude, controller);
        }
    }
}
