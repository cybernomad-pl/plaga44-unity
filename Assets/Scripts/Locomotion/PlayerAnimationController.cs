// =============================================================================
// PlayerAnimationController.cs
// CYBERNOMAD -- Per-frame bridge z IPlayerMotionSource do Animator.
// Ustawia parametry Animatora (Speed, IsFlying, IsFreefall, Trigger:Land)
// oraz CharacterRetargeter target processor weights zaleznie od stanu.
//
// Matrix weights (per PlayerMotionState):
//
//             | Hips [1] | Lower [2] | Upper [3] | HandsIK [4/5] |
//   ----------+----------+-----------+-----------+---------------+
//   Idle      |   0.0    |    0.0    |    0.0    |     1.0       |  (tracking)
//   Locomotion|   1.0    |    1.0    |    0.0    |     1.0       |
//   Fly       |   1.0    |    1.0    |    0.0    |     1.0       |
//   Freefall  |   1.0    |    1.0    |    1.0    |     0.0       |  (full pose)
//   Landing   |   1.0    |    1.0    |    1.0    |     0.0       |
// =============================================================================
using UnityEngine;

namespace Plaga44.Locomotion
{
    [DisallowMultipleComponent]
    public class PlayerAnimationController : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][PlayerAnim]";

        [Tooltip("Animator na avatarze (np. StylizedCharacterLocomotion). Auto-found on Start.")]
        public Animator targetAnimator;

        [Tooltip("Source IPlayerMotionSource -- zwykle LocomotionController na OVRCameraRig.")]
        public MonoBehaviour motionSourceBehaviour; // inspector-friendly (IPlayerMotionSource via cast)

        [Tooltip("Path do PLAGA44 AnimatorController w Resources. Auto-load na Start.")]
        public string controllerResourcePath = "PLAGA44_PlayerAnimator";

        private IPlayerMotionSource _motion;

        // Animator param hashes
        private static readonly int HashSpeed      = Animator.StringToHash("Speed");
        private static readonly int HashStrafeX    = Animator.StringToHash("StrafeX");
        private static readonly int HashForwardZ   = Animator.StringToHash("ForwardZ");
        private static readonly int HashIsFlying   = Animator.StringToHash("IsFlying");
        private static readonly int HashIsFreefall = Animator.StringToHash("IsFreefall");
        private static readonly int HashLand       = Animator.StringToHash("Land");

        private PlayerMotionState _prevState = PlayerMotionState.Idle;

        private void Start()
        {
            _motion = motionSourceBehaviour as IPlayerMotionSource;
            if (_motion == null)
            {
                // Auto-find on OVRCameraRig
                var rig = GameObject.Find("OVRCameraRig");
                if (rig != null) _motion = rig.GetComponent<LocomotionController>();
            }
            if (_motion == null) Debug.LogError($"{LOG} IPlayerMotionSource not found -- wire motionSourceBehaviour.");

            if (targetAnimator == null)
                targetAnimator = GetComponentInChildren<Animator>(true);
            if (targetAnimator == null)
            {
                Debug.LogError($"{LOG} Animator not found in children.");
                return;
            }

            // Load PLAGA44 controller z Resources i podmień na Animator.
            // Bez tego SDK LocomotionController.controller nie ma Freefall state.
            var ctrl = Resources.Load<RuntimeAnimatorController>(controllerResourcePath);
            if (ctrl == null)
            {
                Debug.LogWarning($"{LOG} Controller '{controllerResourcePath}' nie znaleziony w Resources. "
                    + "PlayerAnimatorSetup powinien go wygenerowac przy Bootstrap.");
            }
            else if (targetAnimator.runtimeAnimatorController != ctrl)
            {
                targetAnimator.runtimeAnimatorController = ctrl;
                Debug.Log($"{LOG} AnimatorController podmieniony na {ctrl.name}");
            }
        }

        private void Update()
        {
            if (_motion == null || targetAnimator == null) return;

            var state = _motion.CurrentState;

            // Detect landing edge (Freefall -> Landing/Idle/Locomotion)
            if (_prevState == PlayerMotionState.Freefall && state != PlayerMotionState.Freefall)
            {
                targetAnimator.SetTrigger(HashLand);
                Debug.Log($"{LOG} LAND trigger (freefall -> {state})");
            }
            _prevState = state;

            // Float parameters
            targetAnimator.SetFloat(HashSpeed,    _motion.Speed);
            targetAnimator.SetFloat(HashStrafeX,  _motion.StrafeX);
            targetAnimator.SetFloat(HashForwardZ, _motion.ForwardZ);

            // Bool parameters
            targetAnimator.SetBool(HashIsFlying,   state == PlayerMotionState.Fly);
            targetAnimator.SetBool(HashIsFreefall, state == PlayerMotionState.Freefall);
        }

        public PlayerMotionState DebugCurrentState => _motion != null ? _motion.CurrentState : PlayerMotionState.Idle;
    }
}
