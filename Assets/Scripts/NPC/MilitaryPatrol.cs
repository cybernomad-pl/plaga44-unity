using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.NPC
{
    /// <summary>
    /// Military patrol NPC representing German occupying forces.
    ///
    /// Patrols follow predefined waypoint routes. When the player enters the
    /// detection radius the patrol transitions through:
    ///   Patrol -> Investigate -> Alert -> Fight
    ///
    /// Patrols can also receive reports from civilian informants, which causes
    /// them to divert from their route and investigate the reported location.
    ///
    /// PLAGA '44 context: military, police, city guard, and fire department
    /// personnel are all threats to be avoided during wartime Warsaw.
    /// </summary>
    public class MilitaryPatrol : NPCBehavior
    {
        // ----- Inspector -----
        [Header("Patrol Settings")]
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private float waypointThreshold = 1.5f;
        [SerializeField] private float patrolPauseTime = 2f;
        [SerializeField] private bool loopRoute = true;

        [Header("Detection")]
        [SerializeField] private float innerDetectionRadius = 8f;
        [SerializeField] private float alertRadius = 25f;
        [SerializeField] private float fieldOfViewAngle = 120f;
        [SerializeField] private float investigationTime = 10f;
        [SerializeField] private float alertEscalationTime = 5f;
        [SerializeField] private float searchDuration = 20f;

        [Header("Combat")]
        [SerializeField] private float fireRate = 1.5f;
        [SerializeField] private float combatMoveSpeed = 5f;
        [SerializeField] private float callReinforcementRadius = 50f;

        [Header("Squad")]
        [SerializeField] private int squadSize = 3;
        [Tooltip("Other patrols in this squad for coordinated response.")]
        [SerializeField] private MilitaryPatrol[] squadMembers;

        // ----- Runtime -----
        private int currentWaypointIndex;
        private float pauseTimer;
        private bool movingForward = true;
        private float investigationTimer;
        private float alertTimer;
        private float searchTimer;
        private float fireCooldown;
        private Vector3 lastKnownPlayerPosition;
        private Vector3 reportedLocation;
        private bool hasReport;
        private bool reinforcementsCalled;

        // ----- Public -----
        public int SquadSize => squadSize;
        public bool IsOnAlert => currentState == BehaviorState.Alert || currentState == BehaviorState.Fight;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        protected override void Awake()
        {
            base.Awake();
            npcType = NPCType.MilitaryPatrol;
            initialState = BehaviorState.Patrol;
            currentState = BehaviorState.Patrol;
        }

        protected override void Start()
        {
            base.Start();
            moveSpeed = 2.5f;  // patrol speed
        }

        // =====================================================================
        // State evaluation
        // =====================================================================

        protected override void EvaluateState()
        {
            base.EvaluateState();
            if (!IsAlive) return;

            // Check for incoming reports
            if (hasReport && currentState == BehaviorState.Patrol)
            {
                lastKnownPlayerPosition = reportedLocation;
                hasReport = false;
                TransitionTo(BehaviorState.Investigate);
                return;
            }

            // Detection checks
            bool playerInInnerRadius = distanceToPlayer <= innerDetectionRadius;
            bool playerInFOV = IsPlayerInFieldOfView();
            bool playerInAlertRadius = distanceToPlayer <= alertRadius;

            switch (currentState)
            {
                case BehaviorState.Patrol:
                    if (playerInInnerRadius || (playerInAlertRadius && playerInFOV))
                    {
                        lastKnownPlayerPosition = playerTransform != null
                            ? playerTransform.position : transform.position;
                        TransitionTo(BehaviorState.Investigate);
                    }
                    break;

                case BehaviorState.Investigate:
                    investigationTimer += stateUpdateInterval;

                    // Player spotted clearly -> escalate
                    if (playerInInnerRadius || (playerInFOV && distanceToPlayer < detectionRadius))
                    {
                        TransitionTo(BehaviorState.Alert);
                    }
                    // Timeout -> return to patrol
                    else if (investigationTimer > investigationTime)
                    {
                        TransitionTo(BehaviorState.Patrol);
                    }
                    break;

                case BehaviorState.Alert:
                    alertTimer += stateUpdateInterval;

                    // Confirmed hostile -> fight
                    if (playerInInnerRadius)
                    {
                        TransitionTo(BehaviorState.Fight);
                    }
                    // Timeout without confirmation -> search
                    else if (alertTimer > alertEscalationTime)
                    {
                        searchTimer = 0f;
                        // Stay alert but begin searching
                    }

                    // Lost player for too long -> de-escalate
                    if (!playerDetected)
                    {
                        searchTimer += stateUpdateInterval;
                        if (searchTimer > searchDuration)
                        {
                            TransitionTo(BehaviorState.Patrol);
                        }
                    }
                    else
                    {
                        searchTimer = 0f;
                        lastKnownPlayerPosition = playerTransform.position;
                    }
                    break;

                case BehaviorState.Fight:
                    // Disengage if player escapes far enough
                    if (!playerDetected && distanceToPlayer > alertRadius * 1.5f)
                    {
                        searchTimer += stateUpdateInterval;
                        if (searchTimer > searchDuration)
                        {
                            TransitionTo(BehaviorState.Alert);
                        }
                    }
                    else
                    {
                        searchTimer = 0f;
                        if (playerTransform != null)
                            lastKnownPlayerPosition = playerTransform.position;
                    }
                    break;
            }
        }

        // =====================================================================
        // State ticks
        // =====================================================================

        protected override void TickPatrol()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            // Pause at waypoints
            if (pauseTimer > 0f)
            {
                pauseTimer -= stateUpdateInterval;
                return;
            }

            Transform wp = waypoints[currentWaypointIndex];
            if (wp == null) return;

            float dist = Vector3.Distance(transform.position, wp.position);
            if (dist < waypointThreshold)
            {
                pauseTimer = patrolPauseTime;
                AdvanceWaypoint();
            }
            else
            {
                moveSpeed = 2.5f;
                MoveToward(wp.position);
            }
        }

        protected override void TickInvestigate()
        {
            // Move toward last known position / reported location
            float dist = Vector3.Distance(transform.position, lastKnownPlayerPosition);
            if (dist > 2f)
            {
                moveSpeed = 3.5f;
                MoveToward(lastKnownPlayerPosition);
            }
            else
            {
                // Look around (rotate slowly)
                transform.Rotate(0f, 45f * stateUpdateInterval, 0f);
            }
        }

        protected override void TickAlert()
        {
            // Call reinforcements once
            if (!reinforcementsCalled)
            {
                CallReinforcements();
                reinforcementsCalled = true;
            }

            // Move toward last known position cautiously
            moveSpeed = 3f;
            if (playerDetected && playerTransform != null)
            {
                lastKnownPlayerPosition = playerTransform.position;
            }

            float dist = Vector3.Distance(transform.position, lastKnownPlayerPosition);
            if (dist > 3f)
            {
                MoveToward(lastKnownPlayerPosition);
            }
        }

        protected override void TickFight()
        {
            moveSpeed = combatMoveSpeed;

            if (playerTransform == null) return;

            lastKnownPlayerPosition = playerTransform.position;

            // Move into attack range
            if (distanceToPlayer > attackRange)
            {
                MoveToward(playerTransform.position);
            }
            else
            {
                // Face player
                transform.forward = (playerTransform.position - transform.position).normalized;

                // Fire weapon
                fireCooldown -= stateUpdateInterval;
                if (fireCooldown <= 0f)
                {
                    FireAtPlayer();
                    fireCooldown = 1f / fireRate;
                }
            }
        }

        // =====================================================================
        // Combat
        // =====================================================================

        private void FireAtPlayer()
        {
            // Raycast-based hit detection
            Vector3 dir = (playerTransform.position - transform.position).normalized;

            // Add some inaccuracy
            float spread = 0.05f;
            dir += new Vector3(
                Random.Range(-spread, spread),
                Random.Range(-spread, spread),
                Random.Range(-spread, spread)
            );

            if (Physics.Raycast(transform.position + Vector3.up, dir, out RaycastHit hit, attackRange * 2f))
            {
                NPCBehavior target = hit.collider.GetComponent<NPCBehavior>();
                if (target != null)
                {
                    target.TakeDamage(attackDamage, gameObject);
                }

                // Placeholder: apply damage to player if hit has player tag
                if (hit.collider.CompareTag("Player"))
                {
                    // PlayerHealth.Instance?.TakeDamage(attackDamage);
                    Debug.Log($"[MilitaryPatrol] {npcName} hit player for {attackDamage} damage");
                }
            }
        }

        private void CallReinforcements()
        {
            // Alert nearby patrols
            MilitaryPatrol[] allPatrols = FindObjectsOfType<MilitaryPatrol>();
            foreach (var patrol in allPatrols)
            {
                if (patrol == this || !patrol.IsAlive) continue;
                float dist = Vector3.Distance(transform.position, patrol.transform.position);
                if (dist < callReinforcementRadius)
                {
                    patrol.ReceiveReport(lastKnownPlayerPosition, "reinforcement_request");
                }
            }

            // Alert squad members regardless of distance
            if (squadMembers != null)
            {
                foreach (var member in squadMembers)
                {
                    if (member != null && member.IsAlive && member != this)
                    {
                        member.ReceiveReport(lastKnownPlayerPosition, "squad_alert");
                    }
                }
            }
        }

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Receive a report from a civilian informant or another patrol.
        /// </summary>
        public void ReceiveReport(Vector3 location, string reportType)
        {
            reportedLocation = location;
            hasReport = true;

            Debug.Log($"[MilitaryPatrol] {npcName} received report: {reportType} at {location}");

            // Squad alerts go straight to Alert state
            if (reportType == "squad_alert" || reportType == "reinforcement_request")
            {
                lastKnownPlayerPosition = location;
                if (currentState == BehaviorState.Patrol)
                    TransitionTo(BehaviorState.Alert);
            }
        }

        /// <summary>
        /// Set waypoints for this patrol at runtime.
        /// </summary>
        public void SetWaypoints(Transform[] points)
        {
            waypoints = points;
            currentWaypointIndex = 0;
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private void AdvanceWaypoint()
        {
            if (loopRoute)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            }
            else
            {
                if (movingForward)
                {
                    currentWaypointIndex++;
                    if (currentWaypointIndex >= waypoints.Length)
                    {
                        currentWaypointIndex = waypoints.Length - 2;
                        movingForward = false;
                    }
                }
                else
                {
                    currentWaypointIndex--;
                    if (currentWaypointIndex < 0)
                    {
                        currentWaypointIndex = 1;
                        movingForward = true;
                    }
                }
            }
        }

        private bool IsPlayerInFieldOfView()
        {
            if (playerTransform == null) return false;

            Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dirToPlayer);

            if (angle > fieldOfViewAngle * 0.5f) return false;

            // Line of sight check
            if (Physics.Raycast(transform.position + Vector3.up, dirToPlayer,
                out RaycastHit hit, alertRadius))
            {
                return hit.collider.CompareTag("Player");
            }
            return false;
        }

        protected override void OnEnterState(BehaviorState state)
        {
            switch (state)
            {
                case BehaviorState.Investigate:
                    investigationTimer = 0f;
                    break;
                case BehaviorState.Alert:
                    alertTimer = 0f;
                    searchTimer = 0f;
                    reinforcementsCalled = false;
                    break;
                case BehaviorState.Fight:
                    fireCooldown = 0f;
                    break;
                case BehaviorState.Patrol:
                    reinforcementsCalled = false;
                    hasReport = false;
                    break;
            }
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // Inner detection
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, innerDetectionRadius);

            // Alert radius
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, alertRadius);

            // Field of view
            Gizmos.color = Color.cyan;
            Vector3 leftDir = Quaternion.Euler(0, -fieldOfViewAngle * 0.5f, 0) * transform.forward;
            Vector3 rightDir = Quaternion.Euler(0, fieldOfViewAngle * 0.5f, 0) * transform.forward;
            Gizmos.DrawRay(transform.position, leftDir * alertRadius);
            Gizmos.DrawRay(transform.position, rightDir * alertRadius);

            // Waypoints
            if (waypoints != null)
            {
                Gizmos.color = Color.blue;
                for (int i = 0; i < waypoints.Length; i++)
                {
                    if (waypoints[i] == null) continue;
                    Gizmos.DrawSphere(waypoints[i].position, 0.3f);
                    if (i < waypoints.Length - 1 && waypoints[i + 1] != null)
                        Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                }
                if (loopRoute && waypoints.Length > 1 &&
                    waypoints[0] != null && waypoints[waypoints.Length - 1] != null)
                {
                    Gizmos.DrawLine(waypoints[waypoints.Length - 1].position, waypoints[0].position);
                }
            }

            // Last known player pos
            if (lastKnownPlayerPosition != Vector3.zero)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(lastKnownPlayerPosition, Vector3.one * 0.5f);
            }
        }
    }
}
