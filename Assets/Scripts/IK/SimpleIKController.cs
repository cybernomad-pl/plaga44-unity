// SimpleIKController.cs
// PLAGA '44 -- Two-bone IK solver for VR legs.
// Handles foot grounding via raycasts so feet stay on floors/stairs/slopes.
// Attach to the VR rig root. Assign left/right foot transforms and their
// corresponding IK target/hint transforms (created procedurally or by hand).
//
// Namespace: Plaga44.IK

using UnityEngine;
using UnityEngine.Events;

namespace Plaga44.IK
{
    /// <summary>
    /// Lightweight two-bone IK solver for VR player legs.
    /// Performs foot grounding with raycasts and exposes ikWeight to fade in/out.
    /// Works without Animator -- drives transforms directly.
    /// </summary>
    public class SimpleIKController : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("Foot Bones")]
        [Tooltip("Upper leg bone (hip joint) -- left side.")]
        public Transform leftUpperLeg;

        [Tooltip("Lower leg bone (knee joint) -- left side.")]
        public Transform leftLowerLeg;

        [Tooltip("Foot bone -- left side.")]
        public Transform leftFoot;

        [Tooltip("Upper leg bone (hip joint) -- right side.")]
        public Transform rightUpperLeg;

        [Tooltip("Lower leg bone (knee joint) -- right side.")]
        public Transform rightLowerLeg;

        [Tooltip("Foot bone -- right side.")]
        public Transform rightFoot;

        [Header("IK Targets (optional -- auto-created if null)")]
        [Tooltip("World-space target for left foot. Created at runtime if null.")]
        public Transform leftFootTarget;

        [Tooltip("World-space target for right foot. Created at runtime if null.")]
        public Transform rightFootTarget;

        [Header("Grounding")]
        [Tooltip("LayerMask used for floor raycasts.")]
        public LayerMask groundLayer = ~0;

        [Tooltip("Vertical offset of foot above the ground hit point.")]
        [Range(0f, 0.2f)]
        public float footOffset = 0.04f;

        [Tooltip("How far above the foot to start the downward raycast.")]
        [Range(0.1f, 1.0f)]
        public float raycastOriginHeight = 0.6f;

        [Tooltip("Maximum downward reach of the raycast.")]
        [Range(0.2f, 2.0f)]
        public float raycastDistance = 1.2f;

        [Tooltip("How quickly the foot target tracks the grounded position.")]
        [Range(1f, 30f)]
        public float footTrackingSpeed = 12f;

        [Header("IK Weight")]
        [Tooltip("Global blend factor: 0 = no IK, 1 = full IK.")]
        [Range(0f, 1f)]
        public float ikWeight = 1f;

        [Tooltip("Speed at which ikWeight fades when changed via SetIKWeight().")]
        [Range(0.5f, 20f)]
        public float ikWeightFadeSpeed = 5f;

        [Header("Events")]
        public UnityEvent onLeftFootGrounded;
        public UnityEvent onRightFootGrounded;

        // ── Private ──────────────────────────────────────────────────────

        private float _targetIKWeight = 1f;

        // Cached initial leg lengths (computed once from bone positions)
        private float _leftUpperLength;
        private float _leftLowerLength;
        private float _rightUpperLength;
        private float _rightLowerLength;

        // Grounded positions (smoothed)
        private Vector3 _leftFootGroundPos;
        private Vector3 _rightFootGroundPos;
        private Quaternion _leftFootGroundRot;
        private Quaternion _rightFootGroundRot;
        private bool _leftGrounded;
        private bool _rightGrounded;

        // ── Unity lifecycle ──────────────────────────────────────────────

        private void Start()
        {
            if (leftUpperLeg != null && leftLowerLeg != null && leftFoot != null)
            {
                _leftUpperLength = Vector3.Distance(leftUpperLeg.position, leftLowerLeg.position);
                _leftLowerLength = Vector3.Distance(leftLowerLeg.position, leftFoot.position);
            }

            if (rightUpperLeg != null && rightLowerLeg != null && rightFoot != null)
            {
                _rightUpperLength = Vector3.Distance(rightUpperLeg.position, rightLowerLeg.position);
                _rightLowerLength = Vector3.Distance(rightLowerLeg.position, rightFoot.position);
            }

            // Auto-create foot targets if not assigned
            if (leftFoot != null && leftFootTarget == null)
            {
                var go = new GameObject("IK_LeftFootTarget");
                go.transform.position = leftFoot.position;
                go.transform.rotation = leftFoot.rotation;
                leftFootTarget = go.transform;
            }

            if (rightFoot != null && rightFootTarget == null)
            {
                var go = new GameObject("IK_RightFootTarget");
                go.transform.position = rightFoot.position;
                go.transform.rotation = rightFoot.rotation;
                rightFootTarget = go.transform;
            }

            _leftFootGroundPos = leftFoot != null ? leftFoot.position : Vector3.zero;
            _rightFootGroundPos = rightFoot != null ? rightFoot.position : Vector3.zero;
        }

        private void LateUpdate()
        {
            // Fade ikWeight toward target
            if (!Mathf.Approximately(ikWeight, _targetIKWeight))
                ikWeight = Mathf.MoveTowards(ikWeight, _targetIKWeight, ikWeightFadeSpeed * Time.deltaTime);

            if (ikWeight <= 0f) return;

            UpdateFootGrounding(
                leftFoot, leftFootTarget,
                ref _leftFootGroundPos, ref _leftFootGroundRot, ref _leftGrounded,
                onLeftFootGrounded);

            UpdateFootGrounding(
                rightFoot, rightFootTarget,
                ref _rightFootGroundPos, ref _rightFootGroundRot, ref _rightGrounded,
                onRightFootGrounded);

            // Solve IK for both legs
            if (leftUpperLeg != null && leftLowerLeg != null && leftFoot != null && leftFootTarget != null)
                SolveTwoBoneIK(leftUpperLeg, leftLowerLeg, leftFoot, leftFootTarget, _leftUpperLength, _leftLowerLength);

            if (rightUpperLeg != null && rightLowerLeg != null && rightFoot != null && rightFootTarget != null)
                SolveTwoBoneIK(rightUpperLeg, rightLowerLeg, rightFoot, rightFootTarget, _rightUpperLength, _rightLowerLength);
        }

        // ── Grounding ────────────────────────────────────────────────────

        private void UpdateFootGrounding(
            Transform foot,
            Transform target,
            ref Vector3 groundPos,
            ref Quaternion groundRot,
            ref bool wasGrounded,
            UnityEvent groundedEvent)
        {
            if (foot == null || target == null) return;

            Vector3 origin = foot.position + Vector3.up * raycastOriginHeight;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastDistance, groundLayer))
            {
                Vector3 desiredPos = hit.point + Vector3.up * footOffset;
                Quaternion desiredRot = Quaternion.FromToRotation(Vector3.up, hit.normal) * foot.rotation;

                groundPos = Vector3.Lerp(groundPos, desiredPos, footTrackingSpeed * Time.deltaTime);
                groundRot = Quaternion.Slerp(groundRot, desiredRot, footTrackingSpeed * Time.deltaTime);

                if (!wasGrounded)
                {
                    wasGrounded = true;
                    groundedEvent?.Invoke();
                }
            }
            else
            {
                // Not hitting ground -- relax toward current foot position
                groundPos = Vector3.Lerp(groundPos, foot.position, footTrackingSpeed * Time.deltaTime);
                groundRot = Quaternion.Slerp(groundRot, foot.rotation, footTrackingSpeed * Time.deltaTime);
                wasGrounded = false;
            }

            // Apply with weight blend
            target.position = Vector3.Lerp(foot.position, groundPos, ikWeight);
            target.rotation = Quaternion.Slerp(foot.rotation, groundRot, ikWeight);
        }

        // ── Two-Bone IK Solver ────────────────────────────────────────────

        /// <summary>
        /// Classic two-bone IK (FABRIK-lite).
        /// Rotates upper and lower bones so that the end effector reaches targetTransform.
        /// Uses the cross product of the limb plane as the pole/hint direction.
        /// </summary>
        private void SolveTwoBoneIK(
            Transform upper,
            Transform lower,
            Transform end,
            Transform target,
            float upperLength,
            float lowerLength)
        {
            Vector3 rootPos = upper.position;
            Vector3 targetPos = target.position;
            Quaternion targetRot = target.rotation;

            Vector3 toTarget = targetPos - rootPos;
            float totalLength = upperLength + lowerLength;
            float dist = Mathf.Clamp(toTarget.magnitude, 0.001f, totalLength - 0.001f);

            // Cosine rule to find angle at the upper joint
            float cosUpper = (upperLength * upperLength + dist * dist - lowerLength * lowerLength)
                             / (2f * upperLength * dist);
            cosUpper = Mathf.Clamp(cosUpper, -1f, 1f);
            float angleUpper = Mathf.Acos(cosUpper) * Mathf.Rad2Deg;

            // Pole vector: perpendicular to limb plane (prefer forward as fallback)
            Vector3 up = Vector3.Cross(toTarget, Vector3.Cross(toTarget, upper.forward)).normalized;
            if (up == Vector3.zero)
                up = Vector3.Cross(toTarget, Vector3.right).normalized;

            // Rotate upper bone toward target, then bend by knee angle
            Quaternion lookRot = Quaternion.LookRotation(toTarget.normalized, up);
            upper.rotation = Quaternion.Lerp(
                upper.rotation,
                lookRot * Quaternion.Euler(-(angleUpper), 0f, 0f),
                ikWeight);

            // Lower bone: point from knee toward target, then bend
            Vector3 kneePos = upper.position + upper.rotation * (Vector3.forward * upperLength);
            Vector3 toLower = targetPos - kneePos;

            float cosLower = (lowerLength * lowerLength + dist * dist - upperLength * upperLength)
                             / (2f * lowerLength * dist);
            cosLower = Mathf.Clamp(cosLower, -1f, 1f);
            float angleLower = 180f - Mathf.Acos(cosLower) * Mathf.Rad2Deg;

            lower.rotation = Quaternion.Lerp(
                lower.rotation,
                Quaternion.LookRotation(toLower.normalized, up) * Quaternion.Euler(angleLower, 0f, 0f),
                ikWeight);

            // Blend end effector rotation
            end.rotation = Quaternion.Lerp(end.rotation, targetRot, ikWeight);
        }

        // ── Public API ───────────────────────────────────────────────────

        /// <summary>
        /// Fades ikWeight toward the given value using ikWeightFadeSpeed.
        /// </summary>
        public void SetIKWeight(float weight)
        {
            _targetIKWeight = Mathf.Clamp01(weight);
        }

        /// <summary>
        /// Instantly sets ikWeight with no fade.
        /// </summary>
        public void SetIKWeightImmediate(float weight)
        {
            ikWeight = _targetIKWeight = Mathf.Clamp01(weight);
        }
    }
}
