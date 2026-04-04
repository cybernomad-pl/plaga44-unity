// HapticManager.cs
// PLAGA '44 -- Singleton manager for controller haptic feedback.
// Wraps OVRInput.SetControllerVibration with per-event-type configurable
// amplitude, frequency, and duration. All public fields are tweakable
// at runtime via the Inspector (attach to any persistent GameObject).
//
// Guard: #if HAS_META_XR. Without Meta XR SDK the methods fall back to
// Debug.Log so the rest of the codebase compiles and runs in the Editor.

using System.Collections;
using UnityEngine;
using Plaga44.Core;

namespace Plaga44.Feedback
{
    /// <summary>
    /// Configurable parameters for a single haptic event.
    /// </summary>
    [System.Serializable]
    public class HapticEvent
    {
        [Range(0f, 1f)] public float amplitude = 0.5f;
        [Range(0f, 1f)] public float frequency = 0.5f;
        [Min(0f)]       public float duration  = 0.1f;
    }

    /// <summary>
    /// Singleton haptic manager. Place one instance in the scene on a
    /// persistent GameObject (e.g. the OVRCameraRig). Survives scene loads
    /// via DontDestroyOnLoad.
    /// </summary>
    public class HapticManager : MonoBehaviour
    {
        // --- Singleton -----------------------------------------------------------

        private static HapticManager _instance;

        public static HapticManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindFirstObjectByType<HapticManager>();
                return _instance;
            }
        }

        // --- Per-event configuration (Inspector-tweakable) -----------------------

        [Header("Grab / Release")]
        public HapticEvent grab    = new HapticEvent { amplitude = 0.6f, frequency = 0.5f, duration = 0.12f };
        public HapticEvent release = new HapticEvent { amplitude = 0.3f, frequency = 0.3f, duration = 0.08f };

        [Header("Impact")]
        public HapticEvent impactLight  = new HapticEvent { amplitude = 0.2f, frequency = 0.4f, duration = 0.06f };
        public HapticEvent impactMedium = new HapticEvent { amplitude = 0.55f, frequency = 0.5f, duration = 0.12f };
        public HapticEvent impactHeavy  = new HapticEvent { amplitude = 1.0f, frequency = 0.8f, duration = 0.22f };

        [Tooltip("Force threshold (N) for medium impact. Below = light, above = heavy.")]
        public float impactMediumThreshold = 5f;
        [Tooltip("Force threshold (N) for heavy impact.")]
        public float impactHeavyThreshold  = 15f;

        [Header("Heartbeat")]
        public HapticEvent heartbeatPulse = new HapticEvent { amplitude = 0.45f, frequency = 0.2f, duration = 0.08f };
        [Tooltip("Pause (seconds) between the two pulses of a heartbeat lub-dub.")]
        public float heartbeatInnerGap  = 0.12f;
        [Tooltip("Pause (seconds) after a full lub-dub before the next one.")]
        public float heartbeatOuterGap  = 0.55f;
        [Tooltip("How many lub-dub cycles to play per PlayHeartbeat call. 0 = loop until StopHeartbeat.")]
        public int   heartbeatCycles    = 3;

        [Header("Warning")]
        public HapticEvent warning = new HapticEvent { amplitude = 0.8f, frequency = 0.7f, duration = 0.15f };
        [Tooltip("Number of warning pulses.")]
        public int   warningPulses    = 3;
        [Tooltip("Gap (seconds) between warning pulses.")]
        public float warningPulseGap  = 0.18f;

        // --- Unity lifecycle -----------------------------------------------------

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        // --- Public API ----------------------------------------------------------

        /// <summary>Play grab feedback on the given controller.</summary>
        public void PlayGrab(OVRInput.Controller controller)
        {
            PlayOnce(controller, grab);
        }

        /// <summary>Play release feedback on the given controller.</summary>
        public void PlayRelease(OVRInput.Controller controller)
        {
            PlayOnce(controller, release);
        }

        /// <summary>
        /// Play impact feedback scaled by force magnitude.
        /// Selects light / medium / heavy event based on configured thresholds.
        /// </summary>
        public void PlayImpact(OVRInput.Controller controller, float force)
        {
            HapticEvent evt;
            if (force >= impactHeavyThreshold)
                evt = impactHeavy;
            else if (force >= impactMediumThreshold)
                evt = impactMedium;
            else
                evt = impactLight;

            // Scale amplitude proportionally within the chosen tier so small
            // forces still feel graduated.
            float scaledAmplitude = evt.amplitude;
            if (force < impactMediumThreshold)
            {
                // light tier: scale 0..1 across [0, mediumThreshold)
                scaledAmplitude = Mathf.Lerp(0.05f, impactLight.amplitude,
                    Mathf.Clamp01(force / Mathf.Max(impactMediumThreshold, 0.001f)));
            }
            else if (force < impactHeavyThreshold)
            {
                // medium tier: scale across [mediumThreshold, heavyThreshold)
                float t = Mathf.Clamp01((force - impactMediumThreshold) /
                    Mathf.Max(impactHeavyThreshold - impactMediumThreshold, 0.001f));
                scaledAmplitude = Mathf.Lerp(impactMedium.amplitude * 0.7f, impactMedium.amplitude, t);
            }

            HapticEvent scaled = new HapticEvent
            {
                amplitude = Mathf.Clamp01(scaledAmplitude),
                frequency = evt.frequency,
                duration  = evt.duration
            };

            PlayOnce(controller, scaled);
        }

        /// <summary>
        /// Play a heartbeat pattern (lub-dub) for the configured number of cycles.
        /// </summary>
        public void PlayHeartbeat(OVRInput.Controller controller)
        {
            StartCoroutine(HeartbeatCoroutine(controller));
        }

        /// <summary>
        /// Play repeating warning pulses.
        /// </summary>
        public void PlayWarning(OVRInput.Controller controller)
        {
            StartCoroutine(WarningCoroutine(controller));
        }

        // --- Internal helpers ----------------------------------------------------

        private void PlayOnce(OVRInput.Controller controller, HapticEvent evt)
        {
#if HAS_META_XR
            // Guard: skip when controller disconnected or SampleRateHz == 0 (hand tracking mode)
            if (!ControllerModeHelper.IsControllerActive(controller))
                return;

            StartCoroutine(VibrationCoroutine(controller, evt.amplitude, evt.frequency, evt.duration));
#else
            Debug.Log($"[HapticManager] PlayOnce | ctrl={controller} amp={evt.amplitude:F2} freq={evt.frequency:F2} dur={evt.duration:F2}s");
#endif
        }

#if HAS_META_XR
        private IEnumerator VibrationCoroutine(OVRInput.Controller controller, float amplitude, float frequency, float duration)
        {
            // Re-check at coroutine start in case controller disconnected between call and execution
            if (!ControllerModeHelper.IsControllerActive(controller))
                yield break;

            OVRInput.SetControllerVibration(frequency, amplitude, controller);
            yield return new WaitForSeconds(duration);
            OVRInput.SetControllerVibration(0f, 0f, controller);
        }
#endif

        private IEnumerator HeartbeatCoroutine(OVRInput.Controller controller)
        {
            int cycles = heartbeatCycles <= 0 ? int.MaxValue : heartbeatCycles;
            for (int i = 0; i < cycles; i++)
            {
                // Lub
#if HAS_META_XR
                yield return StartCoroutine(VibrationCoroutine(controller,
                    heartbeatPulse.amplitude, heartbeatPulse.frequency, heartbeatPulse.duration));
#else
                Debug.Log($"[HapticManager] Heartbeat LUB | ctrl={controller}");
                yield return new WaitForSeconds(heartbeatPulse.duration);
#endif
                yield return new WaitForSeconds(heartbeatInnerGap);

                // Dub (slightly softer)
#if HAS_META_XR
                yield return StartCoroutine(VibrationCoroutine(controller,
                    heartbeatPulse.amplitude * 0.75f, heartbeatPulse.frequency, heartbeatPulse.duration));
#else
                Debug.Log($"[HapticManager] Heartbeat DUB | ctrl={controller}");
                yield return new WaitForSeconds(heartbeatPulse.duration);
#endif
                if (i < cycles - 1)
                    yield return new WaitForSeconds(heartbeatOuterGap);
            }
        }

        private IEnumerator WarningCoroutine(OVRInput.Controller controller)
        {
            for (int i = 0; i < warningPulses; i++)
            {
#if HAS_META_XR
                yield return StartCoroutine(VibrationCoroutine(controller,
                    warning.amplitude, warning.frequency, warning.duration));
#else
                Debug.Log($"[HapticManager] Warning pulse {i + 1}/{warningPulses} | ctrl={controller}");
                yield return new WaitForSeconds(warning.duration);
#endif
                if (i < warningPulses - 1)
                    yield return new WaitForSeconds(warningPulseGap);
            }
        }
    }
}
