using System;
using System.Collections;
using UnityEngine;

namespace Plaga44.NPC
{
    // =========================================================================
    // Enums shared across the NPC system
    // =========================================================================

    public enum NPCType
    {
        Civilian,
        MilitaryPatrol,
        CityGuard,
        Police,
        FireDept,
        Criminal,
        Scavenger,
        Addict,
        Animal
    }

    public enum ThreatLevel
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    public enum BehaviorState
    {
        Idle,
        Patrol,
        Wander,
        Flee,
        Fight,
        Trade,
        Report,        // civilian reporting player to occupiers
        Investigate,
        Alert,
        Dead
    }

    public enum LocationType
    {
        Urban,
        Forest,
        Industrial,
        Residential,
        PatrolZone,
        AbandonedBuilding
    }

    public enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }

    // =========================================================================
    // NPCBehavior - Base class for all NPC behaviors
    // =========================================================================

    /// <summary>
    /// Base class for all NPCs in PLAGA '44. Provides core behavior states
    /// (flee, fight, trade, report) and a simple state machine for transitions.
    /// Subclasses override TickState() and transition hooks for specialized AI.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NPCBehavior : MonoBehaviour
    {
        // ----- Inspector fields -----
        [Header("NPC Identity")]
        [SerializeField] protected NPCType npcType;
        [SerializeField] protected string npcName = "NPC";

        [Header("Stats")]
        [SerializeField] protected float maxHealth = 100f;
        [SerializeField] protected float moveSpeed = 3f;
        [SerializeField] protected float detectionRadius = 15f;
        [SerializeField] protected float attackRange = 2f;
        [SerializeField] protected float attackDamage = 10f;
        [SerializeField] protected float fleeHealthThreshold = 0.25f;

        [Header("Behavior")]
        [SerializeField] protected BehaviorState initialState = BehaviorState.Idle;
        [SerializeField] protected float stateUpdateInterval = 0.25f;

        // ----- Runtime state -----
        protected float currentHealth;
        protected BehaviorState currentState;
        protected BehaviorState previousState;
        protected Transform playerTransform;
        protected float distanceToPlayer = float.MaxValue;
        protected bool playerDetected;
        protected float stateTimer;

        // ----- Events -----
        public event Action<BehaviorState, BehaviorState> OnStateChanged;
        public event Action<NPCBehavior> OnDeath;
        public event Action<NPCBehavior, float> OnDamaged;

        // ----- Public accessors -----
        public NPCType Type => npcType;
        public string Name => npcName;
        public float Health => currentHealth;
        public float HealthPercent => currentHealth / maxHealth;
        public BehaviorState State => currentState;
        public float DetectionRadius => detectionRadius;
        public bool IsAlive => currentState != BehaviorState.Dead;
        public bool IsPlayerDetected => playerDetected;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        protected virtual void Awake()
        {
            currentHealth = maxHealth;
            currentState = initialState;
            previousState = initialState;
        }

        protected virtual void Start()
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                playerTransform = playerObj.transform;

            StartCoroutine(BehaviorLoop());
        }

        protected virtual void Update()
        {
            if (!IsAlive) return;

            UpdatePlayerDistance();
        }

        // =====================================================================
        // Core behavior loop  (coroutine ticks at stateUpdateInterval)
        // =====================================================================

        private IEnumerator BehaviorLoop()
        {
            WaitForSeconds wait = new WaitForSeconds(stateUpdateInterval);
            while (IsAlive)
            {
                EvaluateState();
                TickState();
                yield return wait;
            }
        }

        /// <summary>
        /// Called every tick to decide whether the NPC should transition states.
        /// Base implementation handles generic flee/fight thresholds.
        /// Override in subclasses for custom logic.
        /// </summary>
        protected virtual void EvaluateState()
        {
            if (!IsAlive) return;

            // Flee when health is critically low
            if (HealthPercent <= fleeHealthThreshold &&
                currentState != BehaviorState.Flee &&
                currentState != BehaviorState.Dead)
            {
                TransitionTo(BehaviorState.Flee);
                return;
            }

            // Detect player
            playerDetected = distanceToPlayer <= detectionRadius;
        }

        /// <summary>
        /// Executes logic for the current state each tick.
        /// Override in subclasses for type-specific behavior.
        /// </summary>
        protected virtual void TickState()
        {
            switch (currentState)
            {
                case BehaviorState.Idle:
                    TickIdle();
                    break;
                case BehaviorState.Patrol:
                    TickPatrol();
                    break;
                case BehaviorState.Wander:
                    TickWander();
                    break;
                case BehaviorState.Flee:
                    TickFlee();
                    break;
                case BehaviorState.Fight:
                    TickFight();
                    break;
                case BehaviorState.Trade:
                    TickTrade();
                    break;
                case BehaviorState.Report:
                    TickReport();
                    break;
                case BehaviorState.Investigate:
                    TickInvestigate();
                    break;
                case BehaviorState.Alert:
                    TickAlert();
                    break;
            }
        }

        // =====================================================================
        // State tick virtuals (override in subclasses)
        // =====================================================================

        protected virtual void TickIdle() { }
        protected virtual void TickPatrol() { }
        protected virtual void TickWander() { }

        protected virtual void TickFlee()
        {
            if (playerTransform == null) return;

            Vector3 away = (transform.position - playerTransform.position).normalized;
            transform.position += away * moveSpeed * stateUpdateInterval;
        }

        protected virtual void TickFight()
        {
            if (playerTransform == null) return;

            if (distanceToPlayer > attackRange)
            {
                Vector3 toward = (playerTransform.position - transform.position).normalized;
                transform.position += toward * moveSpeed * stateUpdateInterval;
            }
        }

        protected virtual void TickTrade() { }
        protected virtual void TickReport() { }
        protected virtual void TickInvestigate() { }
        protected virtual void TickAlert() { }

        // =====================================================================
        // State transitions
        // =====================================================================

        public void TransitionTo(BehaviorState newState)
        {
            if (currentState == newState) return;
            if (currentState == BehaviorState.Dead) return; // no leaving death

            previousState = currentState;
            currentState = newState;
            stateTimer = 0f;

            OnExitState(previousState);
            OnEnterState(newState);
            OnStateChanged?.Invoke(previousState, newState);
        }

        protected virtual void OnEnterState(BehaviorState state) { }
        protected virtual void OnExitState(BehaviorState state) { }

        // =====================================================================
        // Damage / death
        // =====================================================================

        public virtual void TakeDamage(float amount, GameObject source = null)
        {
            if (!IsAlive) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnDamaged?.Invoke(this, amount);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        protected virtual void Die()
        {
            TransitionTo(BehaviorState.Dead);
            OnDeath?.Invoke(this);
            StopAllCoroutines();
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private void UpdatePlayerDistance()
        {
            if (playerTransform != null)
                distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            else
                distanceToPlayer = float.MaxValue;
        }

        /// <summary>Move toward a world position at moveSpeed.</summary>
        protected void MoveToward(Vector3 target)
        {
            Vector3 dir = (target - transform.position).normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;
            if (dir != Vector3.zero)
                transform.forward = Vector3.Lerp(transform.forward, dir, Time.deltaTime * 5f);
        }

        /// <summary>Pick a random point on the NavMesh within a given radius.</summary>
        protected Vector3 RandomPointInRadius(float radius)
        {
            Vector2 rnd = UnityEngine.Random.insideUnitCircle * radius;
            return transform.position + new Vector3(rnd.x, 0f, rnd.y);
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
