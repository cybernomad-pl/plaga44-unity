using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Plaga44.Gameplay;

namespace Plaga44.AI
{
    /// <summary>
    /// Klaszczur AI -- prosty state machine kompatybilny z Quest 3.
    /// Stany: Idle > Patrol > Chase > Attack > Death
    ///
    /// Wymaga: NavMeshAgent, Animator, HitTarget (na tym samym GO lub rodzicu)
    /// Animator parameters: "Speed" (float), "Attack" (trigger), "Death" (trigger), "Grounded" (bool)
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    public class EnemyAI : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][AI]";

        // -------------------------------------------------------------------------
        // Enums
        // -------------------------------------------------------------------------

        public enum AIState
        {
            Idle,
            Patrol,
            Chase,
            Attack,
            Death
        }

        // -------------------------------------------------------------------------
        // Inspector fields
        // -------------------------------------------------------------------------

        [Header("Detection")]
        [Tooltip("Promien wykrywania gracza (sphere cast)")]
        public float detectionRange = 12f;

        [Tooltip("Promien utraty gracza z zasięgu (powyżej = powrot do patrolu)")]
        public float loseRange = 18f;

        [Tooltip("Layer mask dla gracza")]
        public LayerMask playerLayerMask = ~0;

        [Tooltip("Tag gracza")]
        public string playerTag = "Player";

        [Header("Combat")]
        [Tooltip("Zasieg ataku melee")]
        public float attackRange = 1.8f;

        [Tooltip("Cooldown miedzy atakami [s]")]
        public float attackCooldown = 1.4f;

        [Tooltip("Obrazenia jednego ataku (przekazywane do HitTarget gracza jesli wykryty)")]
        public float attackDamage = 20f;

        [Header("Movement")]
        [Tooltip("Predkosc chodu (patrol)")]
        public float patrolSpeed = 1.8f;

        [Tooltip("Predkosc biegu (chase)")]
        public float chaseSpeed = 4.2f;

        [Tooltip("Waypoints dla patrolu -- jesli puste, Klaszczur stoi w Idle")]
        public Transform[] waypoints;

        [Tooltip("Czas stania na waypoint przed ruszeniem [s]")]
        public float waypointWaitTime = 2f;

        [Header("Health")]
        [Tooltip("Punkty zycia")]
        public float maxHealth = 100f;

        [Header("Ragdoll / Death")]
        [Tooltip("Jesli true -- wlacza ragdoll na smierc (wymaga Rigidbody na kosciach)")]
        public bool useRagdoll = false;

        [Tooltip("Czas zanim GO zostanie zniszczone po smierci [s]")]
        public float deathCleanupDelay = 8f;

        // -------------------------------------------------------------------------
        // Private state
        // -------------------------------------------------------------------------

        private NavMeshAgent _agent;
        private Animator _animator;
        private HitTarget _hitTarget;

        private AIState _currentState = AIState.Idle;
        private Transform _player;
        private float _currentHealth;

        private int _waypointIndex;
        private float _waypointWaitTimer;
        private bool _waitingAtWaypoint;

        private float _attackTimer;
        private bool _isDead;

        // Animator hashes (perf: avoid string lookups per frame)
        private static readonly int AnimSpeed    = Animator.StringToHash("Speed");
        private static readonly int AnimAttack   = Animator.StringToHash("Attack");
        private static readonly int AnimDeath    = Animator.StringToHash("Death");
        private static readonly int AnimGrounded = Animator.StringToHash("Grounded");

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            _agent    = GetComponent<NavMeshAgent>();
            _animator = GetComponent<Animator>();
            _hitTarget = GetComponent<HitTarget>() ?? GetComponentInParent<HitTarget>();

            _currentHealth = maxHealth;

            if (_hitTarget != null)
                _hitTarget.OnHit += HandleHit;
            else
                Debug.LogWarning($"{LOG} {name}: brak HitTarget -- Klaszczur nie bedzie reagowal na trafienia.");
        }

        private void Start()
        {
            // Szukamy gracza (moze jeszcze nie istniec przy Awake jesli spawner go tworzy)
            FindPlayer();

            TransitionTo(waypoints != null && waypoints.Length > 0 ? AIState.Patrol : AIState.Idle);
        }

        private void Update()
        {
            if (_isDead) return;

            _attackTimer -= Time.deltaTime;

            // Odswiez referencje jesli gracz jeszcze nie znaleziony
            if (_player == null) FindPlayer();

            switch (_currentState)
            {
                case AIState.Idle:   UpdateIdle();   break;
                case AIState.Patrol: UpdatePatrol(); break;
                case AIState.Chase:  UpdateChase();  break;
                case AIState.Attack: UpdateAttack(); break;
            }

            UpdateAnimatorParams();
        }

        private void OnDestroy()
        {
            if (_hitTarget != null)
                _hitTarget.OnHit -= HandleHit;
        }

        // -------------------------------------------------------------------------
        // State update methods
        // -------------------------------------------------------------------------

        private void UpdateIdle()
        {
            _agent.isStopped = true;

            if (CanSeePlayer())
                TransitionTo(AIState.Chase);
        }

        private void UpdatePatrol()
        {
            if (CanSeePlayer())
            {
                TransitionTo(AIState.Chase);
                return;
            }

            if (waypoints == null || waypoints.Length == 0)
            {
                TransitionTo(AIState.Idle);
                return;
            }

            if (_waitingAtWaypoint)
            {
                _agent.isStopped = true;
                _waypointWaitTimer -= Time.deltaTime;
                if (_waypointWaitTimer <= 0f)
                {
                    _waitingAtWaypoint = false;
                    AdvanceWaypoint();
                }
                return;
            }

            _agent.isStopped = false;
            _agent.speed = patrolSpeed;

            // Sprawdz czy dotarlismy do waypointa
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f)
            {
                _waitingAtWaypoint = true;
                _waypointWaitTimer = waypointWaitTime;
            }
        }

        private void UpdateChase()
        {
            if (_player == null)
            {
                TransitionTo(AIState.Patrol);
                return;
            }

            float dist = Vector3.Distance(transform.position, _player.position);

            // Utracilismy gracza
            if (dist > loseRange)
            {
                TransitionTo(AIState.Patrol);
                return;
            }

            // Zasieg ataku
            if (dist <= attackRange)
            {
                TransitionTo(AIState.Attack);
                return;
            }

            _agent.isStopped = false;
            _agent.speed = chaseSpeed;
            _agent.SetDestination(_player.position);
        }

        private void UpdateAttack()
        {
            if (_player == null)
            {
                TransitionTo(AIState.Patrol);
                return;
            }

            float dist = Vector3.Distance(transform.position, _player.position);

            // Gracz uciekl
            if (dist > attackRange * 1.3f)
            {
                TransitionTo(AIState.Chase);
                return;
            }

            // Obroc sie w strone gracza
            Vector3 dir = (_player.position - transform.position).normalized;
            dir.y = 0f;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);

            _agent.isStopped = true;

            // Atak
            if (_attackTimer <= 0f)
            {
                PerformAttack();
            }
        }

        // -------------------------------------------------------------------------
        // State transitions
        // -------------------------------------------------------------------------

        private void TransitionTo(AIState newState)
        {
            if (_currentState == newState) return;

            Debug.Log($"{LOG} {name}: {_currentState} -> {newState}");
            _currentState = newState;

            switch (newState)
            {
                case AIState.Idle:
                    _agent.isStopped = true;
                    _agent.ResetPath();
                    break;

                case AIState.Patrol:
                    _agent.isStopped = false;
                    _agent.speed = patrolSpeed;
                    if (waypoints != null && waypoints.Length > 0)
                        _agent.SetDestination(waypoints[_waypointIndex].position);
                    break;

                case AIState.Chase:
                    _agent.isStopped = false;
                    _agent.speed = chaseSpeed;
                    break;

                case AIState.Attack:
                    _agent.isStopped = true;
                    _agent.ResetPath();
                    _attackTimer = 0f; // pierwszy atak natychmiast
                    break;

                case AIState.Death:
                    _isDead = true;
                    _agent.isStopped = true;
                    _agent.enabled = false;

                    if (_animator != null)
                        _animator.SetTrigger(AnimDeath);

                    if (useRagdoll)
                        EnableRagdoll();

                    // Wylacz collider glowny zeby nie blokował nawigacji
                    var col = GetComponent<CapsuleCollider>();
                    if (col != null) col.enabled = false;

                    Destroy(gameObject, deathCleanupDelay);
                    break;
            }
        }

        // -------------------------------------------------------------------------
        // Combat
        // -------------------------------------------------------------------------

        private void PerformAttack()
        {
            _attackTimer = attackCooldown;

            if (_animator != null)
                _animator.SetTrigger(AnimAttack);

            // Prosta detekcja melee -- jesli gracz w zasiegu i w polu widzenia
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist > attackRange) return;

            // Tutaj mozna podpiac do systemu zdrowia gracza
            // Na razie logujemy -- integracja zalezy od VR health systemu
            Debug.Log($"{LOG} {name} atakuje gracza za {attackDamage} obrazen!");

            // Przykladowe wywolanie gdy gracz bedzie mial PlayerHealth komponent:
            // var playerHealth = _player.GetComponent<PlayerHealth>();
            // if (playerHealth != null) playerHealth.TakeDamage(attackDamage);
        }

        // -------------------------------------------------------------------------
        // Hit reaction (z HitTarget.OnHit)
        // -------------------------------------------------------------------------

        private void HandleHit(HitZone zone, float force, Transform thrower)
        {
            if (_isDead) return;

            // Mapowanie sily uderzenia na obrazenia (tuning: 1N ~= 1 obrazenie)
            float damage = force * 1f;
            _currentHealth -= damage;

            Debug.Log($"{LOG} {name} trafiony w {zone.zoneType} za {damage:F1} HP. Zostalo: {_currentHealth:F1}/{maxHealth}");

            // Headshot bonus
            if (zone.zoneType == HitZoneType.Head)
                _currentHealth -= damage * 1.5f; // dodatkowe 150% za headshot

            if (_currentHealth <= 0f)
            {
                TransitionTo(AIState.Death);
                return;
            }

            // Reakcja na trafienie -- przerwij patrol, zacznij gonić atakującego
            if (_currentState == AIState.Idle || _currentState == AIState.Patrol)
            {
                if (thrower != null && thrower.CompareTag(playerTag))
                {
                    _player = thrower;
                    TransitionTo(AIState.Chase);
                }
                else if (_player == null)
                {
                    FindPlayer();
                    if (_player != null) TransitionTo(AIState.Chase);
                }
            }
        }

        // -------------------------------------------------------------------------
        // Ragdoll
        // -------------------------------------------------------------------------

        private void EnableRagdoll()
        {
            if (_animator != null)
                _animator.enabled = false;

            // Wlacz Rigidbody na wszystkich kosciach (muszą byc ustawione jako kinematic = true przed smiercia)
            var rigidbodies = GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rigidbodies)
            {
                rb.isKinematic = false;
            }

            var colliders = GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = true;
            }

            Debug.Log($"{LOG} {name}: ragdoll wlaczony.");
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private void FindPlayer()
        {
            var playerGO = GameObject.FindGameObjectWithTag(playerTag);
            if (playerGO != null)
                _player = playerGO.transform;
        }

        private bool CanSeePlayer()
        {
            if (_player == null) return false;

            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist > detectionRange) return false;

            // Prosty raycast do gracza -- sprawdz czy nie ma przeszkody
            Vector3 origin = transform.position + Vector3.up * 1.4f; // oczy Klaszczura
            Vector3 target = _player.position + Vector3.up * 1.0f;   // srodek gracza
            Vector3 dir = (target - origin).normalized;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, detectionRange))
            {
                if (hit.transform == _player || hit.transform.IsChildOf(_player))
                    return true;
            }

            return false;
        }

        private void AdvanceWaypoint()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            _waypointIndex = (_waypointIndex + 1) % waypoints.Length;
            _agent.SetDestination(waypoints[_waypointIndex].position);
        }

        private void UpdateAnimatorParams()
        {
            if (_animator == null) return;

            float speed = _agent.velocity.magnitude;
            _animator.SetFloat(AnimSpeed, speed);
            _animator.SetBool(AnimGrounded, true); // Quest: pomijamy ground check dla perfu
        }

        // -------------------------------------------------------------------------
        // Public API (dla spawner/wave system)
        // -------------------------------------------------------------------------

        public AIState CurrentState => _currentState;
        public float HealthPercent => _currentHealth / maxHealth;
        public bool IsDead => _isDead;

        /// <summary>
        /// Resetuje AI do poczatkowego stanu (np. po respawnie z puli obiektow).
        /// </summary>
        public void ResetAI()
        {
            _isDead = false;
            _currentHealth = maxHealth;
            _attackTimer = 0f;
            _waypointIndex = 0;
            _waitingAtWaypoint = false;

            if (_agent != null)
            {
                _agent.enabled = true;
                _agent.isStopped = false;
                _agent.ResetPath();
            }

            var col = GetComponent<CapsuleCollider>();
            if (col != null) col.enabled = true;

            TransitionTo(waypoints != null && waypoints.Length > 0 ? AIState.Patrol : AIState.Idle);
        }

        // -------------------------------------------------------------------------
        // Gizmos (editor debug)
        // -------------------------------------------------------------------------

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Detection range
            Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, detectionRange);

            // Lose range
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, loseRange);

            // Attack range
            Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // Waypoints
            if (waypoints == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawSphere(waypoints[i].position, 0.15f);
                if (i + 1 < waypoints.Length && waypoints[i + 1] != null)
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            }
        }
#endif
    }
}
