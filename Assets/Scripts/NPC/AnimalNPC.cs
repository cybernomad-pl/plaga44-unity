using UnityEngine;

namespace Plaga44.NPC
{
    // =========================================================================
    // AnimalSpecies - defines the species of animal
    // =========================================================================

    public enum AnimalSpecies
    {
        Boar,
        Deer,
        Fox,
        Wolf,
        Dog,        // feral / stray
        Rat
    }

    // =========================================================================
    // AnimalNPC - wild animals with rabies and territorial behavior
    // =========================================================================

    /// <summary>
    /// Animal NPC for PLAGA '44 forest and urban encounters.
    ///
    /// Animals have territorial zones they defend. When the player enters the
    /// territory the animal may flee (deer) or become aggressive (boar, wolf).
    ///
    /// Rabies system:
    ///   - Some animals spawn rabid (white foam visual indicator).
    ///   - Rabid animals are always aggressive, ignore flee thresholds, and
    ///     their bite inflicts a rabies debuff on the player.
    ///   - The player can detect rabies by observing white foam around the
    ///     mouth before the animal gets too close.
    ///
    /// Species behavior:
    ///   - Boar: aggressive when approached, charges
    ///   - Deer: skittish, flees immediately
    ///   - Fox: cautious, avoids player, can be rabid
    ///   - Wolf: pack hunter (future: coordinate with other wolves)
    ///   - Dog: unpredictable, may befriend or attack
    ///   - Rat: urban pest, low threat, disease carrier
    /// </summary>
    public class AnimalNPC : NPCBehavior
    {
        // ----- Inspector -----
        [Header("Animal Settings")]
        [SerializeField] private AnimalSpecies species = AnimalSpecies.Fox;
        [SerializeField] private float territoryRadius = 20f;
        [SerializeField] private float aggressionBase = 0.3f;

        [Header("Rabies")]
        [SerializeField] private bool forceRabid = false;
        [SerializeField] private float rabiesChance = 0.1f;
        [SerializeField] private float rabiesDamageBonus = 15f;
        [SerializeField] private GameObject foamParticleEffect;

        [Header("Charge (Boar/Wolf)")]
        [SerializeField] private float chargeSpeed = 8f;
        [SerializeField] private float chargeCooldown = 5f;
        [SerializeField] private float chargeDistance = 15f;

        [Header("Flee (Deer/Fox)")]
        [SerializeField] private float fleeSpeedMultiplier = 1.8f;
        [SerializeField] private float safeDistance = 30f;

        // ----- Runtime -----
        private bool isRabid;
        private Vector3 spawnPosition;   // center of territory
        private float aggression;
        private float chargeTimer;
        private bool isCharging;
        private Vector3 chargeDirection;

        // ----- Public -----
        public AnimalSpecies Species => species;
        public bool IsRabid => isRabid;
        public float TerritoryRadius => territoryRadius;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        protected override void Awake()
        {
            base.Awake();
            npcType = NPCType.Animal;
            spawnPosition = transform.position;

            // Determine rabies
            isRabid = forceRabid || Random.value < rabiesChance;

            // Species-specific defaults
            ApplySpeciesDefaults();

            aggression = aggressionBase;
            if (isRabid) aggression = 1f;
        }

        protected override void Start()
        {
            base.Start();

            // Enable foam particle for rabid animals
            if (isRabid && foamParticleEffect != null)
                foamParticleEffect.SetActive(true);

            TransitionTo(BehaviorState.Wander);
        }

        private void ApplySpeciesDefaults()
        {
            switch (species)
            {
                case AnimalSpecies.Boar:
                    maxHealth = 120f;
                    moveSpeed = 4f;
                    attackDamage = 25f;
                    attackRange = 2.5f;
                    detectionRadius = 12f;
                    aggressionBase = 0.6f;
                    fleeHealthThreshold = 0.15f;
                    break;

                case AnimalSpecies.Deer:
                    maxHealth = 60f;
                    moveSpeed = 6f;
                    attackDamage = 5f;
                    attackRange = 1f;
                    detectionRadius = 25f;
                    aggressionBase = 0.0f;
                    fleeHealthThreshold = 0.9f; // flees at slightest danger
                    break;

                case AnimalSpecies.Fox:
                    maxHealth = 40f;
                    moveSpeed = 5f;
                    attackDamage = 8f;
                    attackRange = 1.5f;
                    detectionRadius = 18f;
                    aggressionBase = 0.1f;
                    fleeHealthThreshold = 0.5f;
                    rabiesChance = 0.15f; // foxes more likely rabid
                    break;

                case AnimalSpecies.Wolf:
                    maxHealth = 100f;
                    moveSpeed = 5.5f;
                    attackDamage = 20f;
                    attackRange = 2f;
                    detectionRadius = 20f;
                    aggressionBase = 0.7f;
                    fleeHealthThreshold = 0.2f;
                    break;

                case AnimalSpecies.Dog:
                    maxHealth = 50f;
                    moveSpeed = 4.5f;
                    attackDamage = 12f;
                    attackRange = 1.5f;
                    detectionRadius = 15f;
                    aggressionBase = Random.Range(0.1f, 0.7f); // unpredictable
                    fleeHealthThreshold = 0.3f;
                    break;

                case AnimalSpecies.Rat:
                    maxHealth = 10f;
                    moveSpeed = 3f;
                    attackDamage = 3f;
                    attackRange = 0.5f;
                    detectionRadius = 5f;
                    aggressionBase = 0.05f;
                    fleeHealthThreshold = 0.8f;
                    break;
            }

            currentHealth = maxHealth;
        }

        // =====================================================================
        // State evaluation
        // =====================================================================

        protected override void EvaluateState()
        {
            if (!IsAlive) return;

            // Rabid animals ignore normal flee logic
            if (isRabid)
            {
                if (playerDetected && currentState != BehaviorState.Fight)
                {
                    TransitionTo(BehaviorState.Fight);
                }
                return;
            }

            base.EvaluateState();

            if (!playerDetected)
            {
                // Return to territory if far from spawn
                float distFromSpawn = Vector3.Distance(transform.position, spawnPosition);
                if (distFromSpawn > territoryRadius * 1.5f && currentState != BehaviorState.Wander)
                {
                    TransitionTo(BehaviorState.Wander);
                }
                return;
            }

            // Player detected -- reaction depends on species aggression
            bool playerInTerritory = Vector3.Distance(playerTransform.position, spawnPosition) < territoryRadius;

            if (aggression >= 0.5f && playerInTerritory)
            {
                // Aggressive: fight if in territory
                if (currentState != BehaviorState.Fight)
                    TransitionTo(BehaviorState.Fight);
            }
            else if (aggression < 0.2f)
            {
                // Skittish: flee immediately
                if (currentState != BehaviorState.Flee)
                    TransitionTo(BehaviorState.Flee);
            }
            else
            {
                // Cautious: investigate then decide
                if (currentState == BehaviorState.Wander || currentState == BehaviorState.Idle)
                    TransitionTo(BehaviorState.Investigate);
            }
        }

        // =====================================================================
        // State ticks
        // =====================================================================

        protected override void TickWander()
        {
            stateTimer += stateUpdateInterval;
            if (stateTimer > 4f)
            {
                stateTimer = 0f;
                // Wander within territory
                Vector3 target = spawnPosition + new Vector3(
                    Random.Range(-territoryRadius, territoryRadius),
                    0f,
                    Random.Range(-territoryRadius, territoryRadius)
                );
                MoveToward(target);
            }
        }

        protected override void TickFlee()
        {
            if (playerTransform == null)
            {
                TransitionTo(BehaviorState.Wander);
                return;
            }

            Vector3 away = (transform.position - playerTransform.position).normalized;
            transform.position += away * moveSpeed * fleeSpeedMultiplier * stateUpdateInterval;

            // Stop fleeing when safe
            if (distanceToPlayer > safeDistance)
            {
                TransitionTo(BehaviorState.Wander);
            }
        }

        protected override void TickFight()
        {
            if (playerTransform == null) return;

            chargeTimer -= stateUpdateInterval;

            // Boar/Wolf charge attack
            if ((species == AnimalSpecies.Boar || species == AnimalSpecies.Wolf) &&
                chargeTimer <= 0f && distanceToPlayer < chargeDistance && !isCharging)
            {
                StartCharge();
            }

            if (isCharging)
            {
                // Execute charge
                transform.position += chargeDirection * chargeSpeed * stateUpdateInterval;

                // End charge after reaching target area
                if (distanceToPlayer < attackRange || chargeTimer < -1f)
                {
                    isCharging = false;

                    // Deal charge damage
                    if (distanceToPlayer < attackRange * 1.5f)
                    {
                        float damage = attackDamage * 2f; // charge does double damage
                        if (isRabid) damage += rabiesDamageBonus;
                        Debug.Log($"[AnimalNPC] {species} charge hit for {damage} damage" +
                                  (isRabid ? " (RABIES)" : ""));
                    }
                }
            }
            else if (distanceToPlayer > attackRange)
            {
                // Close distance
                MoveToward(playerTransform.position);
            }
            else
            {
                // Melee bite/claw
                stateTimer += stateUpdateInterval;
                if (stateTimer > 1f / (isRabid ? 2f : 1f))
                {
                    stateTimer = 0f;
                    float damage = attackDamage;
                    if (isRabid) damage += rabiesDamageBonus;
                    Debug.Log($"[AnimalNPC] {species} melee attack for {damage} damage" +
                              (isRabid ? " (RABIES)" : ""));
                }
            }
        }

        protected override void TickInvestigate()
        {
            if (playerTransform == null) return;

            // Slowly approach, then decide
            stateTimer += stateUpdateInterval;

            if (distanceToPlayer > detectionRadius * 0.6f)
            {
                // Cautious approach
                MoveToward(playerTransform.position);
            }

            if (stateTimer > 3f)
            {
                // Decide based on aggression
                stateTimer = 0f;
                if (Random.value < aggression)
                    TransitionTo(BehaviorState.Fight);
                else
                    TransitionTo(BehaviorState.Flee);
            }
        }

        // =====================================================================
        // Charge
        // =====================================================================

        private void StartCharge()
        {
            isCharging = true;
            chargeTimer = chargeCooldown;
            chargeDirection = (playerTransform.position - transform.position).normalized;
            Debug.Log($"[AnimalNPC] {species} begins charging!");
        }

        // =====================================================================
        // Damage override: rabid animals fight to the death
        // =====================================================================

        public override void TakeDamage(float amount, GameObject source = null)
        {
            base.TakeDamage(amount, source);

            // Rabid animals never flee -- re-enter fight if alive
            if (isRabid && IsAlive && currentState == BehaviorState.Flee)
            {
                TransitionTo(BehaviorState.Fight);
            }

            // Non-rabid: increase aggression when hit
            if (!isRabid)
            {
                aggression = Mathf.Clamp01(aggression + 0.2f);
            }
        }

        // =====================================================================
        // Rabies detection API
        // =====================================================================

        /// <summary>
        /// Returns true if the player is close enough to visually observe the
        /// rabies foam (white foam around mouth). Range ~5 units.
        /// </summary>
        public bool CanPlayerDetectRabies(float observationRange = 5f)
        {
            if (!isRabid) return false;
            return distanceToPlayer <= observationRange;
        }

        /// <summary>
        /// Get a visual cue description for UI/observation system.
        /// </summary>
        public string GetVisualCues()
        {
            string cues = $"{species}";
            if (isRabid && distanceToPlayer < 8f)
                cues += " - white foam around mouth (DANGER)";
            if (currentState == BehaviorState.Fight)
                cues += " - aggressive posture";
            if (currentState == BehaviorState.Flee)
                cues += " - fleeing";
            return cues;
        }

        // =====================================================================
        // Gizmos
        // =====================================================================

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // Territory
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            Vector3 center = Application.isPlaying ? spawnPosition : transform.position;
            Gizmos.DrawWireSphere(center, territoryRadius);

            // Rabies indicator
            if (isRabid || forceRabid)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(transform.position, 0.5f);
            }
        }
    }
}
