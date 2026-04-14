// =============================================================================
// HapticManager.cs
// CYBERNOMAD -- Singleton controller haptic feedback.
// Wraps OVRInput.SetControllerVibration with per-event-type configurable
// amplitude, frequency, duration. Auto-created by Bootstrap if missing.
//
// Logging: every haptic call is logged with event type + controller + amplitude.
// Silenced when controller inactive (ControllerModeHelper guard).
// =============================================================================

using System.Collections;
using UnityEngine;
using Plaga44.Core;

namespace Plaga44.Feedback
{
    [System.Serializable]
    public class HapticEvent
    {
        [Range(0f, 1f)] public float amplitude = 0.5f;
        [Range(0f, 1f)] public float frequency = 0.5f;
        [Min(0f)]       public float duration  = 0.1f;
    }

    /// <summary>
    /// Singleton haptic manager. Place one on persistent GameObject (OVRCameraRig).
    /// Bootstrap auto-creates if missing. Survives scene loads via DontDestroyOnLoad.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class HapticManager : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Haptic]";

        // Impact scaling -- magic values wyciagniete na gore
        private const float ImpactLightFloor = 0.05f;           // min amplitude dla slabego impactu
        private const float ImpactMediumFromFactor = 0.7f;      // lerp start: 70% medium amp przy threshold
        private const float ImpactHeartbeatTailMul = 0.75f;     // drugi puls heartbeat = 75% amplitude
        private const float DivZeroGuard = 0.001f;              // unikniecie dzielenia przez 0 przy threshold

        // --- Singleton ----------------------------------------------------------
        private static HapticManager _instance;
        public static HapticManager Instance
        {
            get
            {
                if (_instance == null) _instance = FindFirstObjectByType<HapticManager>();
                return _instance;
            }
        }

        // --- Config (Inspector) -------------------------------------------------
        [Header("Verbose Logging")]
        [Tooltip("If true, logs every haptic call. Disable for production.")]
        public bool verboseLogs = true;

        [Header("Grab / Release")]
        public HapticEvent grab    = new HapticEvent { amplitude = 0.6f, frequency = 0.5f, duration = 0.12f };
        public HapticEvent release = new HapticEvent { amplitude = 0.3f, frequency = 0.3f, duration = 0.08f };

        [Header("Impact")]
        public HapticEvent impactLight  = new HapticEvent { amplitude = 0.20f, frequency = 0.4f, duration = 0.06f };
        public HapticEvent impactMedium = new HapticEvent { amplitude = 0.55f, frequency = 0.5f, duration = 0.12f };
        public HapticEvent impactHeavy  = new HapticEvent { amplitude = 1.00f, frequency = 0.8f, duration = 0.22f };

        [Tooltip("Force threshold (N) for medium impact.")]
        public float impactMediumThreshold = 5f;
        [Tooltip("Force threshold (N) for heavy impact.")]
        public float impactHeavyThreshold  = 15f;

        [Header("Heartbeat")]
        public HapticEvent heartbeatPulse = new HapticEvent { amplitude = 0.45f, frequency = 0.2f, duration = 0.08f };
        public float heartbeatInnerGap = 0.12f;
        public float heartbeatOuterGap = 0.55f;
        public int   heartbeatCycles   = 3;

        [Header("Warning")]
        public HapticEvent warning = new HapticEvent { amplitude = 0.8f, frequency = 0.7f, duration = 0.15f };
        public int   warningPulses   = 3;
        public float warningPulseGap = 0.18f;

        // --- Unity --------------------------------------------------------------
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log($"{LOG} HapticManager ready (verboseLogs={verboseLogs})");
        }

        private void OnDestroy() { StopAllCoroutines(); }

        // --- Public API ---------------------------------------------------------
        public void PlayGrab(OVRInput.Controller controller)   => PlayOnce(controller, grab,    "grab");
        public void PlayRelease(OVRInput.Controller controller)=> PlayOnce(controller, release, "release");

        /// <summary>Impact scaled by force magnitude (light/medium/heavy tier).</summary>
        public void PlayImpact(OVRInput.Controller controller, float force)
        {
            (HapticEvent evt, string tier) = ResolveImpactTier(force);
            float scaledAmp = ScaleAmplitudeByForce(force, evt);
            var scaled = new HapticEvent { amplitude = scaledAmp, frequency = evt.frequency, duration = evt.duration };
            PlayOnce(controller, scaled, $"impact.{tier}(force={force:F1}N)");
        }

        private (HapticEvent evt, string tier) ResolveImpactTier(float force)
        {
            if (force >= impactHeavyThreshold) return (impactHeavy, "heavy");
            if (force >= impactMediumThreshold) return (impactMedium, "medium");
            return (impactLight, "light");
        }

        private float ScaleAmplitudeByForce(float force, HapticEvent evt)
        {
            if (force < impactMediumThreshold)
            {
                float t = Mathf.Clamp01(force / Mathf.Max(impactMediumThreshold, DivZeroGuard));
                return Mathf.Clamp01(Mathf.Lerp(ImpactLightFloor, impactLight.amplitude, t));
            }
            if (force < impactHeavyThreshold)
            {
                float range = Mathf.Max(impactHeavyThreshold - impactMediumThreshold, DivZeroGuard);
                float t = Mathf.Clamp01((force - impactMediumThreshold) / range);
                return Mathf.Clamp01(Mathf.Lerp(impactMedium.amplitude * ImpactMediumFromFactor, impactMedium.amplitude, t));
            }
            return Mathf.Clamp01(evt.amplitude);
        }

        public void PlayHeartbeat(OVRInput.Controller controller) => StartCoroutine(HeartbeatCoroutine(controller));
        public void PlayWarning(OVRInput.Controller controller)   => StartCoroutine(WarningCoroutine(controller));

        /// <summary>Low-level: play arbitrary event with custom amplitude. Used by HapticOnGrab.</summary>
        public void PlayCustom(OVRInput.Controller controller, float amplitude, float frequency, float duration, string tag)
            => PlayVibration(controller, amplitude, frequency, duration, tag);

        // --- Internal -----------------------------------------------------------
        private void PlayOnce(OVRInput.Controller controller, HapticEvent evt, string tag)
            => PlayVibration(controller, evt.amplitude, evt.frequency, evt.duration, tag);

        private void PlayVibration(OVRInput.Controller controller, float amplitude, float frequency, float duration, string tag)
        {
            if (!ControllerModeHelper.IsControllerActive(controller)) return;
            if (verboseLogs)
                Debug.Log($"{LOG} {tag} | ctrl={controller} amp={amplitude:F2} freq={frequency:F2} dur={duration:F2}s");
            StartCoroutine(VibrationCoroutine(controller, amplitude, frequency, duration));
        }

        private IEnumerator VibrationCoroutine(OVRInput.Controller controller, float amplitude, float frequency, float duration)
        {
            if (!ControllerModeHelper.IsControllerActive(controller)) yield break;
            OVRInput.SetControllerVibration(frequency, amplitude, controller);
            yield return new WaitForSeconds(duration);
            OVRInput.SetControllerVibration(0f, 0f, controller);
        }

        private IEnumerator HeartbeatCoroutine(OVRInput.Controller controller)
        {
            int cycles = heartbeatCycles <= 0 ? int.MaxValue : heartbeatCycles;
            for (int i = 0; i < cycles; i++)
            {
                yield return StartCoroutine(VibrationCoroutine(controller,
                    heartbeatPulse.amplitude, heartbeatPulse.frequency, heartbeatPulse.duration));
                yield return new WaitForSeconds(heartbeatInnerGap);
                yield return StartCoroutine(VibrationCoroutine(controller,
                    heartbeatPulse.amplitude * ImpactHeartbeatTailMul, heartbeatPulse.frequency, heartbeatPulse.duration));
                if (i < cycles - 1) yield return new WaitForSeconds(heartbeatOuterGap);
            }
        }

        private IEnumerator WarningCoroutine(OVRInput.Controller controller)
        {
            for (int i = 0; i < warningPulses; i++)
            {
                yield return StartCoroutine(VibrationCoroutine(controller,
                    warning.amplitude, warning.frequency, warning.duration));
                if (i < warningPulses - 1) yield return new WaitForSeconds(warningPulseGap);
            }
        }
    }
}
