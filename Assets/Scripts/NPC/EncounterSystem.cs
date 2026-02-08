using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.NPC
{
    // =========================================================================
    // EncounterDefinition - defines a possible encounter
    // =========================================================================

    [System.Serializable]
    public class EncounterDefinition
    {
        public string id;
        public string displayName;
        public NPCType[] npcTypes;
        public int[] npcCounts;                  // parallel array with npcTypes
        public LocationType[] validLocations;
        public Season[] validSeasons;
        [Range(0f, 1f)] public float baseProbability;
        public float minPlayerDistance;
        public float maxPlayerDistance;
        public bool isScripted;                  // scripted = always happens at a trigger
        public string[] requiredFlags;           // game state flags that must be set
        public ThreatLevel estimatedThreat;
    }

    // =========================================================================
    // ActiveEncounter - a spawned encounter in the world
    // =========================================================================

    public class ActiveEncounter
    {
        public EncounterDefinition definition;
        public List<NPCBehavior> spawnedNPCs = new List<NPCBehavior>();
        public Vector3 center;
        public float startTime;
        public float lifetime;          // seconds before cleanup (-1 = permanent)
        public bool isComplete;

        public float Age => Time.time - startTime;
        public bool IsExpired => lifetime > 0f && Age > lifetime;
    }

    // =========================================================================
    // EncounterSystem - spawns random and scripted encounters by location
    // =========================================================================

    /// <summary>
    /// Manages random and scripted NPC encounters in PLAGA '44.
    ///
    /// Encounters are location-aware:
    ///   - Urban / Abandoned Buildings: scavengers, addicts, criminals, hostile civilians
    ///   - Forest: wildlife (boar, deer, foxes -- some rabid)
    ///   - Patrol Zones: military patrols, city guard
    ///   - Residential: civilians (potential informants)
    ///
    /// The system respects season (winter = fewer animals, more desperate civilians)
    /// and time of day (night = more criminals, fewer patrols in some areas).
    ///
    /// Random encounters use a weighted probability check each interval.
    /// Scripted encounters trigger when the player enters designated zones.
    /// </summary>
    public class EncounterSystem : MonoBehaviour
    {
        // ----- Singleton -----
        public static EncounterSystem Instance { get; private set; }

        // ----- Inspector -----
        [Header("Encounter Settings")]
        [SerializeField] private float randomEncounterInterval = 30f;
        [SerializeField] private int maxActiveEncounters = 5;
        [SerializeField] private float encounterCleanupRadius = 80f;
        [SerializeField] private float defaultEncounterLifetime = 300f;

        [Header("Location")]
        [SerializeField] private LocationType currentLocation = LocationType.Urban;
        [SerializeField] private Season currentSeason = Season.Summer;

        [Header("NPC Prefabs")]
        [SerializeField] private GameObject civilianPrefab;
        [SerializeField] private GameObject militaryPatrolPrefab;
        [SerializeField] private GameObject criminalPrefab;
        [SerializeField] private GameObject scavengerPrefab;
        [SerializeField] private GameObject addictPrefab;
        [SerializeField] private GameObject boarPrefab;
        [SerializeField] private GameObject deerPrefab;
        [SerializeField] private GameObject foxPrefab;
        [SerializeField] private GameObject wolfPrefab;
        [SerializeField] private GameObject dogPrefab;
        [SerializeField] private GameObject ratPrefab;

        [Header("Encounter Definitions")]
        [SerializeField] private List<EncounterDefinition> encounterTable = new List<EncounterDefinition>();

        // ----- Runtime -----
        private Transform playerTransform;
        private float nextEncounterCheck;
        private readonly List<ActiveEncounter> activeEncounters = new List<ActiveEncounter>();
        private readonly HashSet<string> gameFlags = new HashSet<string>();

        // ----- Public -----
        public IReadOnlyList<ActiveEncounter> ActiveEncounters => activeEncounters;
        public LocationType CurrentLocation => currentLocation;
        public Season CurrentSeason => currentSeason;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (encounterTable.Count == 0)
                BuildDefaultEncounterTable();
        }

        private void Start()
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        private void Update()
        {
            if (playerTransform == null) return;

            // Periodic random encounter check
            if (Time.time >= nextEncounterCheck)
            {
                nextEncounterCheck = Time.time + randomEncounterInterval;
                TrySpawnRandomEncounter();
            }

            // Clean up expired or distant encounters
            CleanupEncounters();
        }

        // =====================================================================
        // Random encounter spawning
        // =====================================================================

        private void TrySpawnRandomEncounter()
        {
            if (activeEncounters.Count >= maxActiveEncounters) return;

            // Gather eligible encounters
            List<EncounterDefinition> eligible = new List<EncounterDefinition>();
            float totalWeight = 0f;

            foreach (var def in encounterTable)
            {
                if (def.isScripted) continue;
                if (!IsLocationValid(def)) continue;
                if (!IsSeasonValid(def)) continue;
                if (!AreFlagsMet(def)) continue;

                float adjustedProb = AdjustProbability(def);
                if (adjustedProb <= 0f) continue;

                eligible.Add(def);
                totalWeight += adjustedProb;
            }

            if (eligible.Count == 0) return;

            // Weighted random selection
            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var def in eligible)
            {
                cumulative += AdjustProbability(def);
                if (roll <= cumulative)
                {
                    SpawnEncounter(def);
                    break;
                }
            }
        }

        // =====================================================================
        // Spawn logic
        // =====================================================================

        /// <summary>
        /// Spawn an encounter from a definition. Can be called externally
        /// for scripted triggers.
        /// </summary>
        public ActiveEncounter SpawnEncounter(EncounterDefinition def, Vector3? overridePosition = null)
        {
            Vector3 center;
            if (overridePosition.HasValue)
            {
                center = overridePosition.Value;
            }
            else
            {
                // Pick a random position in the valid distance ring around the player
                float dist = Random.Range(def.minPlayerDistance, def.maxPlayerDistance);
                Vector2 dir2D = Random.insideUnitCircle.normalized;
                center = playerTransform.position + new Vector3(dir2D.x, 0f, dir2D.y) * dist;
            }

            ActiveEncounter encounter = new ActiveEncounter
            {
                definition = def,
                center = center,
                startTime = Time.time,
                lifetime = def.isScripted ? -1f : defaultEncounterLifetime
            };

            // Spawn each NPC type
            for (int i = 0; i < def.npcTypes.Length; i++)
            {
                NPCType type = def.npcTypes[i];
                int count = (i < def.npcCounts.Length) ? def.npcCounts[i] : 1;

                for (int n = 0; n < count; n++)
                {
                    Vector3 offset = new Vector3(
                        Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));
                    Vector3 spawnPos = center + offset;

                    GameObject prefab = GetPrefab(type);
                    if (prefab == null)
                    {
                        Debug.LogWarning($"[EncounterSystem] No prefab for NPC type: {type}");
                        continue;
                    }

                    GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity);
                    NPCBehavior npc = obj.GetComponent<NPCBehavior>();
                    if (npc != null)
                    {
                        encounter.spawnedNPCs.Add(npc);

                        // Register with NPCManager if available
                        if (NPCManager.Instance != null)
                            NPCManager.Instance.RegisterNPC(npc);
                    }
                }
            }

            activeEncounters.Add(encounter);
            Debug.Log($"[EncounterSystem] Spawned encounter '{def.displayName}' " +
                      $"with {encounter.spawnedNPCs.Count} NPCs at {center}");

            return encounter;
        }

        /// <summary>
        /// Trigger a scripted encounter by ID.
        /// </summary>
        public ActiveEncounter TriggerScriptedEncounter(string encounterId, Vector3 position)
        {
            EncounterDefinition def = encounterTable.Find(e => e.id == encounterId && e.isScripted);
            if (def == null)
            {
                Debug.LogWarning($"[EncounterSystem] Scripted encounter '{encounterId}' not found.");
                return null;
            }
            return SpawnEncounter(def, position);
        }

        // =====================================================================
        // Cleanup
        // =====================================================================

        private void CleanupEncounters()
        {
            for (int i = activeEncounters.Count - 1; i >= 0; i--)
            {
                ActiveEncounter enc = activeEncounters[i];

                // Remove expired
                if (enc.IsExpired)
                {
                    DestroyEncounter(enc);
                    activeEncounters.RemoveAt(i);
                    continue;
                }

                // Remove if all NPCs are dead
                bool allDead = true;
                foreach (var npc in enc.spawnedNPCs)
                {
                    if (npc != null && npc.IsAlive) { allDead = false; break; }
                }
                if (allDead && enc.spawnedNPCs.Count > 0)
                {
                    enc.isComplete = true;
                    activeEncounters.RemoveAt(i);
                    continue;
                }

                // Remove if too far from player
                float dist = Vector3.Distance(playerTransform.position, enc.center);
                if (dist > encounterCleanupRadius && !enc.definition.isScripted)
                {
                    DestroyEncounter(enc);
                    activeEncounters.RemoveAt(i);
                }
            }
        }

        private void DestroyEncounter(ActiveEncounter enc)
        {
            foreach (var npc in enc.spawnedNPCs)
            {
                if (npc != null)
                {
                    if (NPCManager.Instance != null)
                        NPCManager.Instance.UnregisterNPC(npc);
                    Destroy(npc.gameObject);
                }
            }
        }

        // =====================================================================
        // Probability adjustments
        // =====================================================================

        private float AdjustProbability(EncounterDefinition def)
        {
            float prob = def.baseProbability;

            // Season modifiers
            switch (currentSeason)
            {
                case Season.Winter:
                    // Fewer animals, more desperate civilians/criminals
                    if (ContainsType(def, NPCType.Animal)) prob *= 0.4f;
                    if (ContainsType(def, NPCType.Criminal)) prob *= 1.3f;
                    if (ContainsType(def, NPCType.Scavenger)) prob *= 1.5f;
                    break;
                case Season.Summer:
                    // More animals, active patrols
                    if (ContainsType(def, NPCType.Animal)) prob *= 1.4f;
                    if (ContainsType(def, NPCType.MilitaryPatrol)) prob *= 1.2f;
                    break;
                case Season.Autumn:
                    // Animals preparing for winter, moderate activity
                    if (ContainsType(def, NPCType.Animal)) prob *= 1.1f;
                    break;
            }

            // Location modifiers
            if (currentLocation == LocationType.AbandonedBuilding)
            {
                if (ContainsType(def, NPCType.Criminal)) prob *= 2f;
                if (ContainsType(def, NPCType.Addict)) prob *= 2f;
                if (ContainsType(def, NPCType.Scavenger)) prob *= 1.8f;
            }

            return prob;
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private bool IsLocationValid(EncounterDefinition def)
        {
            if (def.validLocations == null || def.validLocations.Length == 0) return true;
            foreach (var loc in def.validLocations)
                if (loc == currentLocation) return true;
            return false;
        }

        private bool IsSeasonValid(EncounterDefinition def)
        {
            if (def.validSeasons == null || def.validSeasons.Length == 0) return true;
            foreach (var s in def.validSeasons)
                if (s == currentSeason) return true;
            return false;
        }

        private bool AreFlagsMet(EncounterDefinition def)
        {
            if (def.requiredFlags == null || def.requiredFlags.Length == 0) return true;
            foreach (var flag in def.requiredFlags)
                if (!gameFlags.Contains(flag)) return false;
            return true;
        }

        private static bool ContainsType(EncounterDefinition def, NPCType type)
        {
            if (def.npcTypes == null) return false;
            foreach (var t in def.npcTypes)
                if (t == type) return true;
            return false;
        }

        private GameObject GetPrefab(NPCType type)
        {
            switch (type)
            {
                case NPCType.Civilian:       return civilianPrefab;
                case NPCType.MilitaryPatrol: return militaryPatrolPrefab;
                case NPCType.CityGuard:      return militaryPatrolPrefab; // reuse
                case NPCType.Police:         return militaryPatrolPrefab; // reuse
                case NPCType.Criminal:       return criminalPrefab;
                case NPCType.Scavenger:      return scavengerPrefab;
                case NPCType.Addict:         return addictPrefab;
                case NPCType.Animal:         return boarPrefab; // default animal
                default:                     return null;
            }
        }

        /// <summary>Set a game flag for encounter conditions.</summary>
        public void SetFlag(string flag) => gameFlags.Add(flag);

        /// <summary>Remove a game flag.</summary>
        public void ClearFlag(string flag) => gameFlags.Remove(flag);

        /// <summary>Update the current location type (call when player moves zones).</summary>
        public void SetLocation(LocationType location) => currentLocation = location;

        /// <summary>Update the current season.</summary>
        public void SetSeason(Season season) => currentSeason = season;

        // =====================================================================
        // Default encounter table
        // =====================================================================

        private void BuildDefaultEncounterTable()
        {
            encounterTable = new List<EncounterDefinition>
            {
                // --- Urban encounters ---
                new EncounterDefinition
                {
                    id = "urban_scavengers",
                    displayName = "Scavengers in Ruins",
                    npcTypes = new[] { NPCType.Scavenger },
                    npcCounts = new[] { 2 },
                    validLocations = new[] { LocationType.Urban, LocationType.AbandonedBuilding },
                    validSeasons = new Season[0], // all seasons
                    baseProbability = 0.4f,
                    minPlayerDistance = 15f,
                    maxPlayerDistance = 40f,
                    estimatedThreat = ThreatLevel.Medium
                },
                new EncounterDefinition
                {
                    id = "urban_addicts",
                    displayName = "Addicts in Abandoned Building",
                    npcTypes = new[] { NPCType.Addict },
                    npcCounts = new[] { 3 },
                    validLocations = new[] { LocationType.AbandonedBuilding },
                    validSeasons = new Season[0],
                    baseProbability = 0.3f,
                    minPlayerDistance = 5f,
                    maxPlayerDistance = 20f,
                    estimatedThreat = ThreatLevel.Low
                },
                new EncounterDefinition
                {
                    id = "urban_criminals",
                    displayName = "Criminal Gang",
                    npcTypes = new[] { NPCType.Criminal },
                    npcCounts = new[] { 3 },
                    validLocations = new[] { LocationType.Urban, LocationType.AbandonedBuilding },
                    validSeasons = new Season[0],
                    baseProbability = 0.25f,
                    minPlayerDistance = 10f,
                    maxPlayerDistance = 30f,
                    estimatedThreat = ThreatLevel.High
                },
                new EncounterDefinition
                {
                    id = "civilian_group",
                    displayName = "Civilian Group",
                    npcTypes = new[] { NPCType.Civilian },
                    npcCounts = new[] { 4 },
                    validLocations = new[] { LocationType.Urban, LocationType.Residential },
                    validSeasons = new Season[0],
                    baseProbability = 0.5f,
                    minPlayerDistance = 10f,
                    maxPlayerDistance = 35f,
                    estimatedThreat = ThreatLevel.Low
                },

                // --- Patrol encounters ---
                new EncounterDefinition
                {
                    id = "military_patrol_small",
                    displayName = "Small Military Patrol",
                    npcTypes = new[] { NPCType.MilitaryPatrol },
                    npcCounts = new[] { 3 },
                    validLocations = new[] { LocationType.PatrolZone, LocationType.Urban },
                    validSeasons = new Season[0],
                    baseProbability = 0.35f,
                    minPlayerDistance = 20f,
                    maxPlayerDistance = 50f,
                    estimatedThreat = ThreatLevel.Critical
                },
                new EncounterDefinition
                {
                    id = "military_patrol_large",
                    displayName = "Large Military Patrol",
                    npcTypes = new[] { NPCType.MilitaryPatrol },
                    npcCounts = new[] { 6 },
                    validLocations = new[] { LocationType.PatrolZone },
                    validSeasons = new Season[0],
                    baseProbability = 0.15f,
                    minPlayerDistance = 30f,
                    maxPlayerDistance = 60f,
                    estimatedThreat = ThreatLevel.Critical
                },

                // --- Forest encounters ---
                new EncounterDefinition
                {
                    id = "forest_deer",
                    displayName = "Deer in Forest",
                    npcTypes = new[] { NPCType.Animal },
                    npcCounts = new[] { 3 },
                    validLocations = new[] { LocationType.Forest },
                    validSeasons = new[] { Season.Spring, Season.Summer, Season.Autumn },
                    baseProbability = 0.5f,
                    minPlayerDistance = 20f,
                    maxPlayerDistance = 45f,
                    estimatedThreat = ThreatLevel.None
                },
                new EncounterDefinition
                {
                    id = "forest_boar",
                    displayName = "Wild Boar",
                    npcTypes = new[] { NPCType.Animal },
                    npcCounts = new[] { 2 },
                    validLocations = new[] { LocationType.Forest },
                    validSeasons = new Season[0],
                    baseProbability = 0.3f,
                    minPlayerDistance = 15f,
                    maxPlayerDistance = 35f,
                    estimatedThreat = ThreatLevel.Medium
                },
                new EncounterDefinition
                {
                    id = "forest_fox_rabid",
                    displayName = "Foxes (Possibly Rabid)",
                    npcTypes = new[] { NPCType.Animal },
                    npcCounts = new[] { 1 },
                    validLocations = new[] { LocationType.Forest },
                    validSeasons = new[] { Season.Spring, Season.Summer },
                    baseProbability = 0.2f,
                    minPlayerDistance = 10f,
                    maxPlayerDistance = 25f,
                    estimatedThreat = ThreatLevel.Medium
                },
                new EncounterDefinition
                {
                    id = "urban_rats",
                    displayName = "Rat Infestation",
                    npcTypes = new[] { NPCType.Animal },
                    npcCounts = new[] { 5 },
                    validLocations = new[] { LocationType.AbandonedBuilding, LocationType.Urban },
                    validSeasons = new Season[0],
                    baseProbability = 0.35f,
                    minPlayerDistance = 3f,
                    maxPlayerDistance = 10f,
                    estimatedThreat = ThreatLevel.Low
                },
                new EncounterDefinition
                {
                    id = "forest_wolves",
                    displayName = "Wolf Pack",
                    npcTypes = new[] { NPCType.Animal },
                    npcCounts = new[] { 4 },
                    validLocations = new[] { LocationType.Forest },
                    validSeasons = new[] { Season.Autumn, Season.Winter },
                    baseProbability = 0.15f,
                    minPlayerDistance = 20f,
                    maxPlayerDistance = 40f,
                    estimatedThreat = ThreatLevel.High
                },

                // --- Mixed encounters ---
                new EncounterDefinition
                {
                    id = "stray_dogs",
                    displayName = "Feral Dog Pack",
                    npcTypes = new[] { NPCType.Animal },
                    npcCounts = new[] { 3 },
                    validLocations = new[] { LocationType.Urban, LocationType.Industrial },
                    validSeasons = new Season[0],
                    baseProbability = 0.25f,
                    minPlayerDistance = 10f,
                    maxPlayerDistance = 25f,
                    estimatedThreat = ThreatLevel.Medium
                },

                // --- Scripted encounters ---
                new EncounterDefinition
                {
                    id = "scripted_ambush",
                    displayName = "Criminal Ambush",
                    npcTypes = new[] { NPCType.Criminal },
                    npcCounts = new[] { 4 },
                    validLocations = new[] { LocationType.AbandonedBuilding },
                    validSeasons = new Season[0],
                    baseProbability = 1f,
                    minPlayerDistance = 5f,
                    maxPlayerDistance = 10f,
                    isScripted = true,
                    estimatedThreat = ThreatLevel.High
                },
                new EncounterDefinition
                {
                    id = "scripted_checkpoint",
                    displayName = "Military Checkpoint",
                    npcTypes = new[] { NPCType.MilitaryPatrol, NPCType.Civilian },
                    npcCounts = new[] { 4, 2 },
                    validLocations = new[] { LocationType.PatrolZone },
                    validSeasons = new Season[0],
                    baseProbability = 1f,
                    minPlayerDistance = 25f,
                    maxPlayerDistance = 30f,
                    isScripted = true,
                    estimatedThreat = ThreatLevel.Critical
                }
            };
        }
    }
}
