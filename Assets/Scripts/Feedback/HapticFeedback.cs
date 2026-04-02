// HapticFeedback.cs
// CYBERNOMAD -- Haptic feedback for VR interactions.
// Phase 1: OVRInput.SetControllerVibration (works without Haptics SDK)
// Phase 2: Meta Haptics SDK .haptic clips (future)
//
// Usage:
//   HapticFeedback.Grab(controller);         // short pulse on grab
//   HapticFeedback.Release(controller);      // sharp kick on throw release
//   HapticFeedback.HitTarget(controller, zoneType);  // impact feedback
//   HapticFeedback.HitMiss(controller);      // soft thud on wall/ground hit
//   HapticFeedback.ThrowWindup(controller, intensity);  // continuous while winding up
//   HapticFeedback.StopVibration(controller); // cut immediately
//
// Notes:
// - OVRInput.SetControllerVibration(frequency, amplitude, controller)
//   frequency and amplitude are 0..1, controller = OVRInput.Controller enum.
// - Coroutines are used to auto-stop timed pulses.
// - Instance is required for coroutines; static methods forward to it.

using UnityEngine;
using System.Collections;

public class HapticFeedback : MonoBehaviour
{
    private static HapticFeedback _instance;

    public static HapticFeedback Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<HapticFeedback>();
            return _instance;
        }
    }

    private void Awake()
    {
        _instance = this;
    }

    // --- Public API ---

    /// <summary>
    /// Short pulse when picking up an object. 0.3f amplitude, 0.1s.
    /// </summary>
    public static void Grab(OVRInput.Controller controller)
    {
        Vibrate(controller, 0.3f, 0.3f, 0.1f);
    }

    /// <summary>
    /// Sharp kick on throw release. 0.6f amplitude, 0.05s.
    /// </summary>
    public static void Release(OVRInput.Controller controller)
    {
        Vibrate(controller, 0.6f, 0.6f, 0.05f);
    }

    /// <summary>
    /// Impact feedback when stone hits a target zone.
    /// Amplitude and duration vary by anatomical zone.
    /// </summary>
    public static void HitTarget(OVRInput.Controller controller, string zone = "body")
    {
        float amp = zone == "head"  ? 1.0f :
                    zone == "torso" ? 0.7f : 0.5f;
        float dur = zone == "head"  ? 0.3f : 0.2f;
        Vibrate(controller, amp, amp, dur);
    }

    /// <summary>
    /// Soft thud when stone hits a non-target surface (ground, wall, etc.).
    /// </summary>
    public static void HitMiss(OVRInput.Controller controller)
    {
        Vibrate(controller, 0.15f, 0.15f, 0.08f);
    }

    /// <summary>
    /// Continuous windup vibration scaled by hand velocity intensity (0..1).
    /// Call every frame while charging a throw. Does NOT auto-stop -- call
    /// StopVibration() or Release() when done.
    /// </summary>
    public static void ThrowWindup(OVRInput.Controller controller, float intensity)
    {
        float amp = Mathf.Lerp(0.05f, 0.5f, Mathf.Clamp01(intensity));
        OVRInput.SetControllerVibration(amp, amp, controller);
    }

    /// <summary>
    /// Immediately stop all vibration on the given controller.
    /// </summary>
    public static void StopVibration(OVRInput.Controller controller)
    {
        OVRInput.SetControllerVibration(0f, 0f, controller);
    }

    // --- Internal ---

    /// <summary>
    /// Start a timed vibration pulse via coroutine.
    /// Falls back to a fire-and-forget if no Instance exists in scene.
    /// </summary>
    private static void Vibrate(OVRInput.Controller controller, float frequency, float amplitude, float duration)
    {
        if (Instance != null)
        {
            Instance.StartCoroutine(Instance.VibrateCoroutine(controller, frequency, amplitude, duration));
        }
        else
        {
            // No MonoBehaviour in scene -- just fire the vibration.
            // It will run until the next SetControllerVibration(0,0,...) call.
            Debug.LogWarning("[PLAGA44] HapticFeedback: no Instance in scene, vibration will not auto-stop.");
            OVRInput.SetControllerVibration(frequency, amplitude, controller);
        }
    }

    private IEnumerator VibrateCoroutine(OVRInput.Controller controller, float frequency, float amplitude, float duration)
    {
        OVRInput.SetControllerVibration(frequency, amplitude, controller);
        yield return new WaitForSeconds(duration);
        OVRInput.SetControllerVibration(0f, 0f, controller);
    }
}
