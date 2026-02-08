using System.Collections;
using UnityEngine;

namespace Plaga44.NPC
{
    /// <summary>
    /// Civilian NPC with trust / suspicion mechanics.
    ///
    /// Civilians start neutral. The player's actions near them (running, carrying
    /// weapons openly, looting, sneaking at night) raise suspicion over time.
    /// When suspicion crosses a threshold the civilian will either:
    ///   - Report the player to the nearest occupying patrol (Report state)
    ///   - Attempt to flee and rally neighbors into a lynch mob (Alert state)
    ///
    /// Friendly actions (giving food, talking, trading) build trust and lower
    /// suspicion. High trust unlocks the Trade state.
    ///
    /// Wartime context: civilians are unpredictable -- some cooperate with the
    /// occupiers out of fear, others are sympathetic but scared.
    /// </summary>
    public class CivilianNPC : NPCBehavior
    {
        // ----- Inspector -----
        [Header("Civilian Settings")]
        [SerializeField] private float initialTrust = 0.5f;
        [SerializeField] private float suspicionGainRate = 0.05f;
        [SerializeField] private float suspicionDecayRate = 0.01f;
        [SerializeField] private float reportThreshold = 0.75f;
        [SerializeField] private float lynchThreshold = 0.9f;
        [SerializeField] private float tradeThreshold = 0.6f;

        [Header("Informant")]
        [Tooltip("Chance (0-1) this civilian is secretly an informant for the occupiers.")]
        [SerializeField] private float informantChance = 0.15f;
        [SerializeField] private float informantReportRadius = 40f;

        [Header("Suspicion Triggers")]
        [SerializeField] private float weaponVisibleBonus = 0.15f;
        [SerializeField] private float runningBonus = 0.03f;
        [SerializeField] private float nightBonus = 0.05f;
        [SerializeField] private float lootingBonus = 0.10f;

        // ----- Runtime -----
        private float trust;
        private float suspicion;
        private bool isInformant;
        private bool hasReported;
        private Vector3 reportTarget;   // nearest patrol location
        private float reportCooldown;

        // ----- Public accessors -----
        public float TrustLevel => trust;
        public float SuspicionLevel => suspicion;
        public bool IsInformant => isInformant;
        public bool HasReported => hasReported;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        protected override void Awake()
        {
            base.Awake();
            npcType = NPCType.Civilian;
            trust = initialTrust;
            suspicion = 0f;
            isInformant = Random.value < informantChance;
        }

        protected override void Start()
        {
            base.Start();
            // Civilians start wandering
            if (currentState == BehaviorState.Idle)
                TransitionTo(BehaviorState.Wander);
        }

        // =====================================================================
        // State evaluation
        // =====================================================================

        protected override void EvaluateState()
        {
            base.EvaluateState();
            if (!IsAlive) return;

            // Suspicion decay when player is far away
            if (!playerDetected)
            {
                suspicion = Mathf.Max(0f, suspicion - suspicionDecayRate * stateUpdateInterval);
                return;
            }

            // Build suspicion based on player behavior
            AccumulateSuspicion();

            // Check thresholds
            if (suspicion >= lynchThreshold && currentState != BehaviorState.Alert)
            {
                TransitionTo(BehaviorState.Alert); // rally lynch mob
                return;
            }

            if (suspicion >= reportThreshold && !hasReported &&
                currentState != BehaviorState.Report &&
                currentState != BehaviorState.Alert)
            {
                if (isInformant || Random.value < suspicion * 0.5f)
                {
                    TransitionTo(BehaviorState.Report);
                    return;
                }
            }

            // If player is friendly and trust is high, allow trade
            if (trust >= tradeThreshold && suspicion < 0.3f &&
                distanceToPlayer < 4f &&
                currentState == BehaviorState.Wander)
            {
                TransitionTo(BehaviorState.Trade);
                return;
            }
        }

        private void AccumulateSuspicion()
        {
            float gain = suspicionGainRate * stateUpdateInterval;

            // Additional suspicion modifiers from player behavior
            // These would normally query the player state; placeholders here
            // Example: if player has weapon drawn, add bonus
            // gain += weaponVisibleBonus * stateUpdateInterval;

            // Informants gain suspicion faster
            if (isInformant)
                gain *= 1.5f;

            // Night time makes civilians more nervous
            // Placeholder: check time-of-day system
            // if (TimeOfDay.IsNight) gain += nightBonus * stateUpdateInterval;

            suspicion = Mathf.Clamp01(suspicion + gain);
        }

        // =====================================================================
        // State ticks
        // =====================================================================

        protected override void TickWander()
        {
            // Simple random wandering
            stateTimer += stateUpdateInterval;
            if (stateTimer > 3f)
            {
                stateTimer = 0f;
                Vector3 target = RandomPointInRadius(8f);
                MoveToward(target);
            }
        }

        protected override void TickReport()
        {
            // Move toward the nearest military patrol to report
            if (reportCooldown > 0f)
            {
                reportCooldown -= stateUpdateInterval;
                return;
            }

            MilitaryPatrol nearestPatrol = FindNearestPatrol();
            if (nearestPatrol != null)
            {
                float dist = Vector3.Distance(transform.position, nearestPatrol.transform.position);
                if (dist < 3f)
                {
                    // Deliver report -- alert the patrol
                    nearestPatrol.ReceiveReport(transform.position, "suspicious_player");
                    hasReported = true;
                    reportCooldown = 30f; // don't spam reports
                    TransitionTo(BehaviorState.Flee);
                }
                else
                {
                    MoveToward(nearestPatrol.transform.position);
                }
            }
            else
            {
                // No patrol found, flee instead
                TransitionTo(BehaviorState.Flee);
            }
        }

        protected override void TickAlert()
        {
            // Lynch mob rally: alert nearby civilians, then approach player
            AlertNearbyCivilians();

            if (playerTransform != null && distanceToPlayer > attackRange)
            {
                MoveToward(playerTransform.position);
            }
        }

        protected override void TickTrade()
        {
            // Face the player and wait (UI interaction handled elsewhere)
            if (playerTransform != null)
                transform.forward = (playerTransform.position - transform.position).normalized;

            // Exit trade if player moves away
            if (distanceToPlayer > 5f)
                TransitionTo(BehaviorState.Wander);
        }

        // =====================================================================
        // Public API for player interactions
        // =====================================================================

        /// <summary>
        /// Player performs a friendly action (give food, help, talk).
        /// Builds trust and reduces suspicion.
        /// </summary>
        public void PlayerFriendlyAction(float trustBonus = 0.1f)
        {
            trust = Mathf.Clamp01(trust + trustBonus);
            suspicion = Mathf.Max(0f, suspicion - trustBonus * 0.5f);
        }

        /// <summary>
        /// Player performs a hostile or suspicious action in sight of this civilian.
        /// </summary>
        public void PlayerHostileAction(float suspicionBonus = 0.2f)
        {
            suspicion = Mathf.Clamp01(suspicion + suspicionBonus);
            trust = Mathf.Max(0f, trust - suspicionBonus * 0.3f);
        }

        /// <summary>
        /// Called when the player is seen carrying a visible weapon.
        /// </summary>
        public void NoticeWeapon()
        {
            suspicion = Mathf.Clamp01(suspicion + weaponVisibleBonus);
        }

        /// <summary>
        /// Called when the player is seen looting nearby.
        /// </summary>
        public void NoticeLooting()
        {
            suspicion = Mathf.Clamp01(suspicion + lootingBonus);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private MilitaryPatrol FindNearestPatrol()
        {
            MilitaryPatrol[] patrols = FindObjectsOfType<MilitaryPatrol>();
            MilitaryPatrol nearest = null;
            float nearestDist = informantReportRadius;

            foreach (var p in patrols)
            {
                float d = Vector3.Distance(transform.position, p.transform.position);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearest = p;
                }
            }
            return nearest;
        }

        private void AlertNearbyCivilians()
        {
            Collider[] nearby = Physics.OverlapSphere(transform.position, 15f);
            foreach (var col in nearby)
            {
                CivilianNPC other = col.GetComponent<CivilianNPC>();
                if (other != null && other != this && other.IsAlive)
                {
                    other.suspicion = Mathf.Clamp01(other.suspicion + 0.3f);
                    if (other.currentState == BehaviorState.Wander ||
                        other.currentState == BehaviorState.Idle)
                    {
                        other.TransitionTo(BehaviorState.Alert);
                    }
                }
            }
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // Suspicion indicator
            Gizmos.color = Color.Lerp(Color.green, Color.red, suspicion);
            Gizmos.DrawWireSphere(transform.position, 1.5f);
        }
    }
}
