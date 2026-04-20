// =============================================================================
// FreefallCameraController.cs
// CYBERNOMAD -- Podczas Freefall wymusza lookdown (pitch 60 deg).
// Aplikuje pitch offset na TrackingSpace OVRCameraRig -- nie konflikuje
// z real head tracking Questa (tylko rotuje coordinate system).
// Smooth fade-in/out: ~0.5s.
// =============================================================================
using UnityEngine;

namespace Plaga44.Locomotion
{
    [DisallowMultipleComponent]
    public class FreefallCameraController : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][FreefallCam]";

        [Tooltip("Target pitch (stopnie) podczas Freefall. 60 = patrzy sie w dol pod katem.")]
        [Range(0f, 90f)] public float freefallPitchDeg = 60f;

        [Tooltip("Szybkosc lerp-u pitch (1/speed = sekundy do osiagniecia). 3.0 = ~0.33s.")]
        public float pitchLerpSpeed = 3f;

        [Tooltip("TrackingSpace transform (child OVRCameraRig). Auto-found.")]
        public Transform trackingSpace;

        [Tooltip("Motion source. Auto-found on OVRCameraRig.")]
        public MonoBehaviour motionSourceBehaviour;

        private IPlayerMotionSource _motion;
        private float _currentPitch;
        private Vector3 _baseLocalEuler;
        private bool _captured;

        private void Start()
        {
            if (trackingSpace == null)
            {
                var rig = GameObject.Find("OVRCameraRig");
                if (rig != null)
                {
                    var ts = rig.transform.Find("TrackingSpace");
                    if (ts != null) trackingSpace = ts;
                }
            }
            if (trackingSpace == null) { Debug.LogError($"{LOG} TrackingSpace not found"); enabled = false; return; }

            _baseLocalEuler = trackingSpace.localEulerAngles;
            _captured = true;

            _motion = motionSourceBehaviour as IPlayerMotionSource;
            if (_motion == null)
            {
                var rig = GameObject.Find("OVRCameraRig");
                if (rig != null) _motion = rig.GetComponent<LocomotionController>();
            }
            if (_motion == null) Debug.LogError($"{LOG} IPlayerMotionSource not found");
        }

        private void LateUpdate()
        {
            if (!_captured || _motion == null) return;

            float target = (_motion.CurrentState == PlayerMotionState.Freefall) ? freefallPitchDeg : 0f;
            _currentPitch = Mathf.Lerp(_currentPitch, target, Time.deltaTime * pitchLerpSpeed);

            // Apply pitch offset on top of base euler
            var e = _baseLocalEuler;
            e.x += _currentPitch;
            trackingSpace.localEulerAngles = e;
        }
    }
}
