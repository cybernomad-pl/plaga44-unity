using UnityEngine;
using UnityEngine.AI;

namespace Plaga44.NPC
{
    /// <summary>
    /// Core NPC locomotion controller.
    /// Drives a NavMeshAgent along a WaypointPath and feeds an Animator with
    /// motion parameters (speed, velocity direction) ready for future integration
    /// with Meta Movement SDK / AI Motion Synthesizer.
    ///
    /// Animator parameter contract (all optional -- missing params are silently ignored):
    ///   float "Speed"     -- normalised agent speed [0..1]
    ///   float "VelX"      -- local velocity X (strafe, for blendtrees)
    ///   float "VelZ"      -- local velocity Z (forward, for blendtrees)
    ///   bool  "IsMoving"  -- true when agent has a destination and is moving
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NPCLocomotion : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Inspector
        // ------------------------------------------------------------------ //

        [Header("Navigation")]
        [Tooltip("Patrol path. Assign a WaypointPath in the scene.")]
        public WaypointPath waypointPath;

        [Tooltip("Base movement speed (m/s). State multipliers are applied on top.")]
        [Min(0.1f)]
        public float baseSpeed = 2f;

        [Tooltip("How close the agent needs to be to a waypoint before moving to the next.")]
        [Min(0.05f)]
        public float waypointReachDistance = 0.5f;

        [Header("Animator")]
        [Tooltip("Animator to drive. If null, auto-resolved from this GameObject.")]
        public Animator animator;

        [Tooltip("Smoothing time for Animator float parameters.")]
        [Min(0f)]
        public float animatorDampTime = 0.1f;

        // ------------------------------------------------------------------ //
        //  Animator parameter hashes (cached for perf)
        // ------------------------------------------------------------------ //

        private static readonly int HashSpeed    = Animator.StringToHash("Speed");
        private static readonly int HashVelX     = Animator.StringToHash("VelX");
        private static readonly int HashVelZ     = Animator.StringToHash("VelZ");
        private static readonly int HashIsMoving = Animator.StringToHash("IsMoving");

        // ------------------------------------------------------------------ //
        //  Runtime state
        // ------------------------------------------------------------------ //

        private NavMeshAgent _agent;
        private int _currentWaypointIndex;
        private float _speedMultiplier = 1f;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();

            if (animator == null)
                animator = GetComponent<Animator>();
        }

        private void Start()
        {
            _agent.speed = baseSpeed * _speedMultiplier;
            MoveToNextWaypoint();
        }

        private void Update()
        {
            UpdatePatrol();
            UpdateAnimator();
        }

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Adjusts agent speed by multiplier.
        /// Called by NPCStateController when state changes.
        /// </summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = Mathf.Max(0f, multiplier);
            _agent.speed = baseSpeed * _speedMultiplier;

            if (_speedMultiplier <= 0f)
                _agent.ResetPath();
            else if (!_agent.hasPath && waypointPath != null && waypointPath.Count > 0)
                MoveToNextWaypoint();
        }

        /// <summary>
        /// Immediately navigates to an arbitrary world position.
        /// Used by NPCStateController in Chase / Alert states.
        /// </summary>
        public void NavigateTo(Vector3 worldPosition)
        {
            if (!_agent.isOnNavMesh) return;
            _agent.SetDestination(worldPosition);
        }

        /// <summary>Stops the agent and clears its path.</summary>
        public void StopNavigation()
        {
            if (_agent.isOnNavMesh)
                _agent.ResetPath();
        }

        // ------------------------------------------------------------------ //
        //  Patrol logic
        // ------------------------------------------------------------------ //

        private void UpdatePatrol()
        {
            if (waypointPath == null || waypointPath.Count == 0) return;
            if (!_agent.isOnNavMesh) return;
            if (_agent.pathPending) return;
            if (_speedMultiplier <= 0f) return;

            // Check if close enough to current waypoint
            float dist = Vector3.Distance(transform.position, waypointPath.GetPosition(_currentWaypointIndex));
            if (dist <= waypointReachDistance || (_agent.remainingDistance <= waypointReachDistance && !_agent.pathPending))
            {
                _currentWaypointIndex = waypointPath.NextIndex(_currentWaypointIndex);
                MoveToNextWaypoint();
            }
        }

        private void MoveToNextWaypoint()
        {
            if (waypointPath == null || waypointPath.Count == 0) return;
            if (!_agent.isOnNavMesh) return;
            if (_speedMultiplier <= 0f) return;

            Vector3 target = waypointPath.GetPosition(_currentWaypointIndex);
            _agent.SetDestination(target);
        }

        // ------------------------------------------------------------------ //
        //  Animator bridge
        // ------------------------------------------------------------------ //

        private void UpdateAnimator()
        {
            if (animator == null) return;

            float agentMaxSpeed = _agent.speed > 0f ? _agent.speed : 1f;
            float normSpeed     = _agent.velocity.magnitude / agentMaxSpeed;
            bool  isMoving      = normSpeed > 0.05f;

            // Local velocity for directional blendtrees
            Vector3 localVel = transform.InverseTransformDirection(_agent.velocity);
            float normVelX   = agentMaxSpeed > 0f ? localVel.x / agentMaxSpeed : 0f;
            float normVelZ   = agentMaxSpeed > 0f ? localVel.z / agentMaxSpeed : 0f;

            // Use damp time for smooth transitions; check param existence via try-approach
            TrySetFloat(HashSpeed,    normSpeed, animatorDampTime);
            TrySetFloat(HashVelX,     normVelX,  animatorDampTime);
            TrySetFloat(HashVelZ,     normVelZ,  animatorDampTime);
            TrySetBool(HashIsMoving,  isMoving);
        }

        // Safe wrappers -- Unity 6 throws if param doesn't exist in controller
        private void TrySetFloat(int hash, float value, float dampTime)
        {
            try { animator.SetFloat(hash, value, dampTime, Time.deltaTime); }
            catch { /* Animator controller doesn't have this parameter -- skip */ }
        }

        private void TrySetBool(int hash, bool value)
        {
            try { animator.SetBool(hash, value); }
            catch { /* Animator controller doesn't have this parameter -- skip */ }
        }

        // ------------------------------------------------------------------ //
        //  Gizmo: show current destination
        // ------------------------------------------------------------------ //

        private void OnDrawGizmosSelected()
        {
            if (_agent == null || !Application.isPlaying) return;
            if (!_agent.hasPath) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _agent.destination);
            Gizmos.DrawWireSphere(_agent.destination, 0.15f);
        }
    }
}
