// =============================================================================
// HapticOnGrab.cs
// CYBERNOMAD -- Trigger grip vibration on grab/release.
// Attach to any grabbable object (Rigidbody auto-required).
//
// Mass influence: heavier = stronger amplitude (configurable curve).
// Integration: call OnGrab(controller) / OnRelease(controller) from grab system.
// Used by OVRGrabbable wrapper (PlagaOVRGrabbable) -- no manual wiring needed.
// =============================================================================

using UnityEngine;
using Plaga44.Core;

namespace Plaga44.Feedback
{
    [RequireComponent(typeof(Rigidbody))]
    public class HapticOnGrab : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][HapticOnGrab]";

        [Header("Mass Influence")]
        [Tooltip("Multiplier applied to grab amplitude based on mass (kg). X=mass, Y=multiplier.")]
        public AnimationCurve massAmplitudeMultiplierCurve = DefaultMassCurve();

        [Tooltip("Use Rigidbody.mass if true, else overrideMass.")]
        public bool useMass = true;

        [Min(0f)]
        public float overrideMass = 1f;

        private Rigidbody _rb;

        private void Awake() { _rb = GetComponent<Rigidbody>(); }

        /// <summary>Call on grab. Scales grab event amplitude by mass curve.</summary>
        public void OnGrab(OVRInput.Controller controller)
        {
            float multiplier = GetMassMultiplier();
            var mgr = HapticManager.Instance;
            if (mgr == null)
            {
                Debug.LogWarning($"{LOG} HapticManager missing -- grab on {name} will be silent.");
                return;
            }

            float scaledAmp = Mathf.Clamp01(mgr.grab.amplitude * multiplier);
            mgr.PlayCustom(controller, scaledAmp, mgr.grab.frequency, mgr.grab.duration,
                $"grab({name}, mass={GetEffectiveMass():F2}kg, mult={multiplier:F2})");
        }

        /// <summary>Call on release.</summary>
        public void OnRelease(OVRInput.Controller controller)
        {
            var mgr = HapticManager.Instance;
            if (mgr == null)
            {
                Debug.LogWarning($"{LOG} HapticManager missing -- release on {name} silent.");
                return;
            }
            mgr.PlayRelease(controller);
        }

        private float GetEffectiveMass()
            => useMass ? (_rb != null ? _rb.mass : overrideMass) : overrideMass;

        private float GetMassMultiplier()
            => massAmplitudeMultiplierCurve.Evaluate(GetEffectiveMass());

        /// <summary>Default: 0kg -> 0.4x, 1kg -> 1.0x, 5kg -> 1.5x, 20kg -> 2.0x.</summary>
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
