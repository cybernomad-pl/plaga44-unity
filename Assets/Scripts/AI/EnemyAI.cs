using UnityEngine;
using UnityEngine.AI;

namespace Plaga44.AI
{
    /// <summary>
    /// Main enemy AI controller. Uses a NavMeshAgent for pathfinding.
    ///
    /// State machine:
    ///   Idle    --> Patrol  (when PatrolPath is assigned)
    ///   Patrol  --> Alert   (player enters hearing range OR spotted in vision cone)
    ///   Alert   --> Chase   (player confirmed: in cone or within hearing range for 0.5s)
    ///   Alert   --> Patrol  (lost player before confirming)
    ///   Chase   --> Attack  (within melee range)
    ///   Chase   --> Alert   (player left vision cone -- give up after lostSightTimeout)
    ///   Attack  --> Chase   (player moved out of melee range)
    ///   Any     --> Dead    (EnemyHealth.OnDeath fires)
    ///
    /// Visual feedback: renderer color changes per state so it's obvious in VR testbed.
    ///   Idle/Patrol = green, Alert = yellow, Chase = orange, Attack = red, Dead = grey.
    ///
    /// NavMesh requirement: scene must have a baked NavMesh or a NavMeshSurface component.
    /// Run "CYBERNOMAD / Scene Setup / Setup AI Testbed" to get a ready scene.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyAI : MonoBehaviour
    {
        private const string LOG = "[PLAGA44]";

        // ---- Inspector tunables ----

        [Header("Detection")]
        [Tooltip("Half-angle of the forward vision cone in degrees. 60 = 120 deg total FOV.")]
        public float visionHalfAngle = 60f;

        [Tooltip("Maximum vision range in metres.")]
        public float visionRange = 15f;

        [Tooltip("Hearing radius -- player is detected even outside the vision cone.")]
        public float hearingRadius = 5f;

        [Tooltip("Layer mask used for vision raycasts (should include environment blockers).")]
        public LayerMask visionBlockMask = ~0;

        [Header("Movement")]
        [Tooltip("Walk speed during patrol.")]
        public float patrolSpeed = 1.8f;

        [Tooltip("Run speed when chasing.")]
        public float chaseSpeed = 4.5f;

        [Header("Combat")]
        [Tooltip("Distance at which the enemy switches to Attack state.")]
        public float meleeRange = 2.0f;

        [Tooltip("Damage dealt per melee attack.")]
        public float meleeDamage = 20f;

        [Tooltip("Time between melee hits in seconds.")]
        public float meleeRate = 1.5f;

        [Header("Behaviour")]
        [Tooltip("Seconds without line-of-sight before giving up chase and going to Alert.")]
        public float lostSightTimeout = 5f;

        [Tooltip("How long enemy stays in Alert before returning to patrol.")]
        public float alertTimeout = 3f;

        [Tooltip("Patrol path to follow. If null the enemy stays Idle.")]
        public PatrolPath patrolPath;

        [Tooltip("How close the enemy must get to a waypoint before moving to the next.")]
        public float waypointArrivalDistance = 0.5f;

        [Header("References")]
        [Tooltip("Transform of the VR player's camera / head. Assign at runtime or via spawner.")]
        public Transform playerTransform;

        // ---- State (read-only in Inspector) ----

        [Header("Debug -- read only")]
        [SerializeField] private EnemyState _state = EnemyState.Idle;

        // ---- Private ----

        private NavMeshAgent _agent;
        private EnemyHealth _health;
        private Renderer _renderer;

        private int _patrolIndex;
        private int _patrolDirection = 1;

        private float _lostSightTimer;
        private float _alertTimer;
        private float _meleeTimer;
        private float _alertConfirmTimer;

        // Last known player position used for chase target when LoS is lost
        private Vector3 _lastKnownPlayerPos;

        // State colors
        private static readonly Color ColorPatrol = new Color(0.15f, 0.75f, 0.15f);
        private static readonly Color ColorAlert   = new Color(0.95f, 0.85f, 0.0f);
        private static readonly Color ColorChase   = new Color(0.95f, 0.45f, 0.0f);
        private static readonly Color ColorAttack  = new Color(0.85f, 0.0f,  0.05f);
        private static readonly Color ColorDead    = new Color(0.35f, 0.35f, 0.35f);

        // ---- Lifecycle ----

        private void Awake()
        {
            _agent  = GetComponent<NavMeshAgent>();
            _health = GetComponent<EnemyHealth>();
            _renderer = GetComponentInChildren<Renderer>();

            _health.OnDeath += HandleDeath;
        }

        private void Start()
        {
            // If player not assigned, try to find via tag (OVRPlayerController uses "Player" tag)
            if (playerTransform == null)
            {
                var playerGO = GameObject.FindWithTag("Player");
                if (playerGO != null)
                    playerTransform = playerGO.transform;
            }

            EnterState(patrolPath != null && patrolPath.HasWaypoints ? EnemyState.Patrol : EnemyState.Idle);
        }

        private void Update()
        {
            if (_state == EnemyState.Dead) return;

            switch (_state)
            {
                case EnemyState.Idle:    UpdateIdle();   break;
                case EnemyState.Patrol:  UpdatePatrol(); break;
                case EnemyState.Alert:   UpdateAlert();  break;
                case EnemyState.Chase:   UpdateChase();  break;
                case EnemyState.Attack:  UpdateAttack(); break;
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
                _health.OnDeath -= HandleDeath;
        }

        // ---- State enter/exit ----

        private void EnterState(EnemyState next)
        {
            _state = next;

            switch (next)
            {
                case EnemyState.Idle:
                    _agent.isStopped = true;
                    SetColor(ColorPatrol);
                    break;

                case EnemyState.Patrol:
                    _agent.isStopped = false;
                    _agent.speed = patrolSpeed;
                    SetColor(ColorPatrol);
                    MoveToNextWaypoint();
                    break;

                case EnemyState.Alert:
                    _agent.isStopped = true;
                    _agent.speed = patrolSpeed;
                    _alertTimer = 0f;
                    _alertConfirmTimer = 0f;
                    SetColor(ColorAlert);
                    break;

                case EnemyState.Chase:
                    _agent.isStopped = false;
                    _agent.speed = chaseSpeed;
                    _lostSightTimer = 0f;
                    SetColor(ColorChase);
                    break;

                case EnemyState.Attack:
                    _agent.isStopped = true;
                    _meleeTimer = meleeRate; // ready to hit immediately
                    SetColor(ColorAttack);
                    break;

                case EnemyState.Dead:
                    _agent.enabled = false;
                    SetColor(ColorDead);
                    // Ragdoll: add rigidbody to all child colliders
                    EnableRagdoll();
                    break;
            }
        }

        // ---- Per-state updates ----

        private void UpdateIdle()
        {
            // Transition to patrol if a path is now assigned
            if (patrolPath != null && patrolPath.HasWaypoints)
            {
                EnterState(EnemyState.Patrol);
                return;
            }
            CheckPlayerDetection();
        }

        private void UpdatePatrol()
        {
            if (patrolPath == null || !patrolPath.HasWaypoints)
            {
                EnterState(EnemyState.Idle);
                return;
            }

            CheckPlayerDetection();

            // Arrived at waypoint?
            if (!_agent.pathPending && _agent.remainingDistance <= waypointArrivalDistance)
            {
                _patrolIndex = patrolPath.GetNextIndex(_patrolIndex, ref _patrolDirection);
                MoveToNextWaypoint();
            }
        }

        private void UpdateAlert()
        {
            _alertTimer += Time.deltaTime;

            bool playerVisible = CanSeePlayer();
            bool playerClose   = playerTransform != null &&
                                 Vector3.Distance(transform.position, playerTransform.position) <= hearingRadius;

            if (playerVisible || playerClose)
            {
                _alertConfirmTimer += Time.deltaTime;
                // Face the player slowly while in Alert
                if (playerTransform != null)
                    TurnTowards(playerTransform.position, 90f);

                // Transition to Chase after brief confirmation window
                if (_alertConfirmTimer >= 0.4f)
                {
                    EnterState(EnemyState.Chase);
                }
            }
            else
            {
                _alertConfirmTimer = 0f;
            }

            // Give up if player not found in time
            if (_alertTimer >= alertTimeout)
            {
                Debug.Log($"{LOG} {name} alert timed out, returning to patrol.");
                EnterState(patrolPath != null && patrolPath.HasWaypoints ? EnemyState.Patrol : EnemyState.Idle);
            }
        }

        private void UpdateChase()
        {
            if (playerTransform == null) return;

            float dist = Vector3.Distance(transform.position, playerTransform.position);

            // Switch to Attack if in melee range
            if (dist <= meleeRange)
            {
                EnterState(EnemyState.Attack);
                return;
            }

            // Update chase target
            if (CanSeePlayer())
            {
                _lastKnownPlayerPos = playerTransform.position;
                _lostSightTimer = 0f;
                _agent.SetDestination(_lastKnownPlayerPos);
            }
            else
            {
                _lostSightTimer += Time.deltaTime;

                // Still running to last known position
                if (_lostSightTimer < lostSightTimeout)
                {
                    // Already heading there -- no update needed
                }
                else
                {
                    Debug.Log($"{LOG} {name} lost player, going Alert.");
                    EnterState(EnemyState.Alert);
                }
            }
        }

        private void UpdateAttack()
        {
            if (playerTransform == null) return;

            float dist = Vector3.Distance(transform.position, playerTransform.position);

            // Player left melee range -- chase again
            if (dist > meleeRange + 0.3f)
            {
                EnterState(EnemyState.Chase);
                return;
            }

            // Always face player during attack
            TurnTowards(playerTransform.position, 360f);

            // Deal damage on cooldown
            _meleeTimer += Time.deltaTime;
            if (_meleeTimer >= meleeRate)
            {
                _meleeTimer = 0f;
                DealMeleeDamage();
            }
        }

        // ---- Detection ----

        private void CheckPlayerDetection()
        {
            if (playerTransform == null) return;

            if (CanSeePlayer() || IsPlayerWithinHearing())
                EnterState(EnemyState.Alert);
        }

        private bool CanSeePlayer()
        {
            if (playerTransform == null) return false;

            Vector3 eyePos  = transform.position + Vector3.up * 1.5f;
            Vector3 toPlayer = playerTransform.position - eyePos;
            float dist = toPlayer.magnitude;

            if (dist > visionRange) return false;

            float angle = Vector3.Angle(transform.forward, toPlayer);
            if (angle > visionHalfAngle) return false;

            // Raycast: if we hit something AND it is NOT the player, LoS is blocked.
            if (Physics.Raycast(eyePos, toPlayer.normalized, out RaycastHit hit, dist, visionBlockMask, QueryTriggerInteraction.Ignore))
            {
                // Check if the hit object is part of the player hierarchy
                return hit.transform.IsChildOf(playerTransform) || hit.transform == playerTransform;
            }

            // Ray reached the player without obstruction
            return true;
        }

        private bool IsPlayerWithinHearing()
        {
            if (playerTransform == null) return false;
            return Vector3.Distance(transform.position, playerTransform.position) <= hearingRadius;
        }

        // ---- Combat ----

        private void DealMeleeDamage()
        {
            // Placeholder: in future this will call a player health component
            Debug.Log($"{LOG} {name} MELEE HIT -- {meleeDamage} dmg to player (placeholder).");
        }

        // ---- Death ----

        private void HandleDeath(string killingZone)
        {
            EnterState(EnemyState.Dead);
        }

        private void EnableRagdoll()
        {
            // Get all child colliders and add a Rigidbody if none exists
            // Simple ragdoll: just let the capsule fall
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.mass = 70f;
                rb.linearDamping = 1f;
                rb.angularDamping = 2f;
            }
            rb.isKinematic = false;
            rb.useGravity = true;

            // Tip over
            rb.AddForce(Vector3.up * 2f + transform.forward * -1f, ForceMode.Impulse);
            rb.AddTorque(transform.right * 5f, ForceMode.Impulse);

            Debug.Log($"{LOG} {name} ragdoll activated.");
        }

        // ---- Patrol helpers ----

        private void MoveToNextWaypoint()
        {
            if (patrolPath == null || !patrolPath.HasWaypoints) return;
            Vector3 dest = patrolPath.GetWaypointPosition(_patrolIndex);
            if (dest != Vector3.zero)
                _agent.SetDestination(dest);
        }

        // ---- Utility ----

        private void TurnTowards(Vector3 target, float degreesPerSecond)
        {
            Vector3 dir = (target - transform.position).normalized;
            dir.y = 0f;
            if (dir == Vector3.zero) return;
            Quaternion look = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, degreesPerSecond * Time.deltaTime);
        }

        private void SetColor(Color c)
        {
            if (_renderer == null) return;
            // Avoid shared material mutation -- use instance
            _renderer.material.color = c;
        }

        // ---- Gizmos ----

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Vision cone
            UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.15f);
            Vector3 origin = transform.position + Vector3.up * 1.5f;
            UnityEditor.Handles.DrawSolidArc(origin, Vector3.up,
                Quaternion.Euler(0, -visionHalfAngle, 0) * transform.forward,
                visionHalfAngle * 2f, visionRange);

            // Hearing range
            UnityEditor.Handles.color = new Color(0f, 0.8f, 1f, 0.1f);
            UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.up, hearingRadius);

            // Melee range
            UnityEditor.Handles.color = new Color(1f, 0f, 0f, 0.1f);
            UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.up, meleeRange);
        }
#endif
    }
}
