// HapticOnGrab.cs
// PLAGA '44 -- MonoBehaviour that triggers haptic feedback when this
// GameObject is grabbed or released. Attach to any grabbable object.
//
// Integration points:
//   - Call OnGrab(controller)   from your grab system (e.g. OVRGrabbable, ISDK HandGrabInteractable)
//   - Call OnRelease(controller) when the object leaves the hand
//
// Mass influence: heavier objects produce stronger grab feedback.
// The mass curve is configurable via massAmplitudeMultiplierCurve in the Inspector.
//
// Guard: #if HAS_META_XR. Without the SDK the methods log to console.

using UnityEngine;
using Plaga44.Core;

namespace Plaga44.Feedback
{
    [RequireComponent(typeof(Rigidbody))]
    public class HapticOnGrab : MonoBehaviour
    {
        [Header("Mass Influence")]
        [Tooltip("Multiplier applied to grab amplitude based on object mass (kg). " +
                 "X axis = mass, Y axis = amplitude multiplier (0..2 range).")]
        public AnimationCurve massAmplitudeMultiplierCurve = DefaultMassCurve();

        [Tooltip("If true, the Rigidbody mass is used. If false, use overrideMass.")]
        public bool useMass = true;

        [Tooltip("Manual mass value when useMass = false.")]
        [Min(0f)]
        public float overrideMass = 1f;

        // -------------------------------------------------------------------------

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        // --- Public API (call from grab system) ----------------------------------

        /// <summary>
        /// Call this when the object is grabbed.
        /// </summary>
        /// <param name="controller">The controller that performed the grab.</param>
        public void OnGrab(OVRInput.Controller controller)
        {
            float multiplier = GetMassMultiplier();

#if HAS_META_XR
            if (HapticManager.Instance == null)
            {
                Debug.LogWarning("[HapticOnGrab] HapticManager not found in scene.");
                return;
            }

            // Temporarily scale grab event amplitude by mass multiplier.
            // We do this by cloning the event values locally and calling
            // a helper that accepts explicit parameters.
            var mgr = HapticManager.Instance;
            float scaledAmp = Mathf.Clamp01(mgr.grab.amplitude * multiplier);
            StartCoroutine(OneShot(controller, scaledAmp, mgr.grab.frequency, mgr.grab.duration));
#else
            Debug.Log($"[HapticOnGrab] OnGrab | ctrl={controller} massMultiplier={multiplier:F2}");
#endif
        }

        /// <summary>
        /// Call this when the object is released.
        /// </summary>
        /// <param name="controller">The controller that released the object.</param>
        public void OnRelease(OVRInput.Controller controller)
        {
#if HAS_META_XR
            if (HapticManager.Instance == null)
            {
                Debug.LogWarning("[HapticOnGrab] HapticManager not found in scene.");
                return;
            }
            HapticManager.Instance.PlayRelease(controller);
#else
            Debug.Log($"[HapticOnGrab] OnRelease | ctrl={controller}");
#endif
        }

        // --- Internal ------------------------------------------------------------

        private float GetMassMultiplier()
        {
            float mass = useMass ? (_rb != null ? _rb.mass : overrideMass) : overrideMass;
            return massAmplitudeMultiplierCurve.Evaluate(mass);
        }

#if HAS_META_XR
        private System.Collections.IEnumerator OneShot(
            OVRInput.Controller controller, float amplitude, float frequency, float duration)
        {
            if (!ControllerModeHelper.IsControllerActive(controller))
                yield break;

            OVRInput.SetControllerVibration(frequency, amplitude, controller);
            yield return new WaitForSeconds(duration);
            OVRInput.SetControllerVibration(0f, 0f, controller);
        }
#endif

        // --- Default curve -------------------------------------------------------

        /// <summary>
        /// Default curve: mass 0 kg -> multiplier 0.4, mass 1 kg -> 1.0, mass 5+ kg -> 1.6.
        /// </summary>
        private static AnimationCurve DefaultMassCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f,  0.4f),
                new Keyframe(1f,  1.0f),
                new Keyframe(5f,  1.5f),
                new Keyframe(20f, 2.0f)
            );
        }
    }
}
