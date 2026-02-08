using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Plaga44.NPC
{
    // =========================================================================
    // NPCSpawnConfig - per-type spawn settings
    // =========================================================================

    [System.Serializable]
    public class NPCSpawnConfig
    {
        public NPCType type;
        public GameObject prefab;
        public int maxActive = 10;
        public float spawnCooldown = 30f;
        [Tooltip("Probability weight for ambient spawns (0 = never ambient-spawn).")]
        [Range(0f, 1f)] public float ambientWeight = 0.5f;
    }

    // =========================================================================
    // NPCManager - central NPC spawning and lifecycle management
    // =========================================================================

    /// <summary>
    /// Singleton manager for all NPCs in the game world.
    ///
    /// Responsibilities:
    ///   - Maintain a registry of every living NPC
    ///   - Enforce per-type population caps
    ///   - Provide fast lookup/query helpers (by type, by distance, etc.)
    ///   - Ambient (non-encounter) spawning when population is low
    ///   - Despawn NPCs that drift too far from the player
    ///
    /// Works alongside <see cref="EncounterSystem"/> (which handles encounter-
    /// driven spawning) and <see cref="ThreatAssessment"/> (which evaluates the
    /// danger of tracked NPCs).
    /// </summary>
    public class NPCManager : MonoBehaviour
    {
        // ----- Singleton -----
        public static NPCManager Instance { get; private set; }

        // ----- Inspector -----
        [Header("General")]
        [SerializeField] private int globalMaxNPCs = 40;
        [SerializeField] private float despawnDistance = 100f;
        [SerializeField] private float despawnCheckInterval = 5f;
        [SerializeField] private float ambientSpawnInterval = 20f;

        [Header("Spawn Configs")]
        [SerializeField] private List<NPCSpawnConfig> spawnConfigs = new List<NPCSpawnConfig>();

        // ----- Runtime -----
        private Transform playerTransform;
        private readonly Dictionary<NPCType, List<NPCBehavior>> npcsByType =
            new Dictionary<NPCType, List<NPCBehavior>>();
        private readonly List<NPCBehavior> allNPCs = new List<NPCBehavior>();
        private float nextDespawnCheck;
        private float nextAmbientSpawn;
        private readonly Dictionary<NPCType, float> spawnCooldowns =
            new Dictionary<NPCType, float>();

        // ----- Public -----
        public int TotalNPCCount => allNPCs.Count;
        public IReadOnlyList<NPCBehavior> AllNPCs => allNPCs;

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Initialize type buckets
            foreach (NPCType type in System.Enum.GetValues(typeof(NPCType)))
                npcsByType[type] = new List<NPCBehavior>();
        }

        private void Start()
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        private void Update()
        {
            if (playerTransform == null) return;

            // Despawn check
            if (Time.time >= nextDespawnCheck)
            {
                nextDespawnCheck = Time.time + despawnCheckInterval;
                DespawnFarNPCs();
            }

            // Ambient spawn
            if (Time.time >= nextAmbientSpawn)
            {
                nextAmbientSpawn = Time.time + ambientSpawnInterval;
                TryAmbientSpawn();
            }

            // Tick cooldowns
            var keys = spawnCooldowns.Keys.ToList();
            foreach (var key in keys)
            {
                if (spawnCooldowns[key] > 0f)
                    spawnCooldowns[key] -= ambientSpawnInterval; // approximate
            }
        }

        // =====================================================================
        // Registration (called by EncounterSystem or external spawners)
        // =====================================================================

        /// <summary>Register an NPC with the manager.</summary>
        public void RegisterNPC(NPCBehavior npc)
        {
            if (npc == null || allNPCs.Contains(npc)) return;

            allNPCs.Add(npc);
            npcsByType[npc.Type].Add(npc);

            npc.OnDeath += HandleNPCDeath;

            Debug.Log($"[NPCManager] Registered {npc.Type} '{npc.Name}' " +
                      $"(total: {allNPCs.Count})");
        }

        /// <summary>Unregister an NPC from the manager.</summary>
        public void UnregisterNPC(NPCBehavior npc)
        {
            if (npc == null) return;

            allNPCs.Remove(npc);
            if (npcsByType.ContainsKey(npc.Type))
                npcsByType[npc.Type].Remove(npc);

            npc.OnDeath -= HandleNPCDeath;
        }

        private void HandleNPCDeath(NPCBehavior npc)
        {
            Debug.Log($"[NPCManager] NPC died: {npc.Type} '{npc.Name}'");
            // Don't unregister immediately -- let the corpse persist briefly
            // The despawn pass will clean up dead NPCs after a delay
        }

        // =====================================================================
        // Spawning
        // =====================================================================

        /// <summary>
        /// Spawn a single NPC of the given type at a position.
        /// Returns null if population cap is reached.
        /// </summary>
        public NPCBehavior SpawnNPC(NPCType type, Vector3 position, Quaternion rotation = default)
        {
            if (allNPCs.Count >= globalMaxNPCs)
            {
                Debug.LogWarning("[NPCManager] Global NPC cap reached.");
                return null;
            }

            NPCSpawnConfig config = GetConfig(type);
            if (config == null || config.prefab == null)
            {
                Debug.LogWarning($"[NPCManager] No spawn config/prefab for {type}");
                return null;
            }

            if (GetCount(type) >= config.maxActive)
            {
                Debug.Log($"[NPCManager] Type cap reached for {type} ({config.maxActive})");
                return null;
            }

            if (rotation == default) rotation = Quaternion.identity;

            GameObject obj = Instantiate(config.prefab, position, rotation);
            NPCBehavior npc = obj.GetComponent<NPCBehavior>();
            if (npc != null)
                RegisterNPC(npc);

            return npc;
        }

        private void TryAmbientSpawn()
        {
            if (allNPCs.Count >= globalMaxNPCs) return;

            // Pick a random type weighted by ambientWeight
            float totalWeight = 0f;
            foreach (var cfg in spawnConfigs)
            {
                if (cfg.ambientWeight <= 0f) continue;
                if (GetCount(cfg.type) >= cfg.maxActive) continue;
                if (spawnCooldowns.ContainsKey(cfg.type) && spawnCooldowns[cfg.type] > 0f) continue;
                totalWeight += cfg.ambientWeight;
            }

            if (totalWeight <= 0f) return;

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var cfg in spawnConfigs)
            {
                if (cfg.ambientWeight <= 0f) continue;
                if (GetCount(cfg.type) >= cfg.maxActive) continue;
                if (spawnCooldowns.ContainsKey(cfg.type) && spawnCooldowns[cfg.type] > 0f) continue;

                cumulative += cfg.ambientWeight;
                if (roll <= cumulative)
                {
                    // Spawn at a random distance from the player
                    float dist = Random.Range(despawnDistance * 0.5f, despawnDistance * 0.8f);
                    Vector2 dir2D = Random.insideUnitCircle.normalized;
                    Vector3 pos = playerTransform.position + new Vector3(dir2D.x, 0f, dir2D.y) * dist;

                    SpawnNPC(cfg.type, pos);
                    spawnCooldowns[cfg.type] = cfg.spawnCooldown;
                    break;
                }
            }
        }

        // =====================================================================
        // Despawning
        // =====================================================================

        private void DespawnFarNPCs()
        {
            for (int i = allNPCs.Count - 1; i >= 0; i--)
            {
                NPCBehavior npc = allNPCs[i];
                if (npc == null)
                {
                    allNPCs.RemoveAt(i);
                    continue;
                }

                float dist = Vector3.Distance(npc.transform.position, playerTransform.position);

                // Despawn if too far
                if (dist > despawnDistance)
                {
                    UnregisterNPC(npc);
                    Destroy(npc.gameObject);
                    continue;
                }

                // Clean up dead NPCs after 30 seconds
                if (!npc.IsAlive && npc.State == BehaviorState.Dead)
                {
                    // Use a simple heuristic: if NPC has been dead "a while", remove
                    // (In production, track death timestamp)
                    UnregisterNPC(npc);
                    Destroy(npc.gameObject, 5f);
                }
            }
        }

        // =====================================================================
        // Queries
        // =====================================================================

        /// <summary>Get count of living NPCs of a specific type.</summary>
        public int GetCount(NPCType type)
        {
            if (!npcsByType.ContainsKey(type)) return 0;
            return npcsByType[type].Count(n => n != null && n.IsAlive);
        }

        /// <summary>Get all living NPCs of a specific type.</summary>
        public List<NPCBehavior> GetNPCsByType(NPCType type)
        {
            if (!npcsByType.ContainsKey(type)) return new List<NPCBehavior>();
            return npcsByType[type].Where(n => n != null && n.IsAlive).ToList();
        }

        /// <summary>Get all living NPCs within a radius of a point.</summary>
        public List<NPCBehavior> GetNPCsInRadius(Vector3 center, float radius)
        {
            float r2 = radius * radius;
            return allNPCs.Where(n =>
                n != null && n.IsAlive &&
                (n.transform.position - center).sqrMagnitude <= r2
            ).ToList();
        }

        /// <summary>Find the nearest NPC of a given type to a position.</summary>
        public NPCBehavior FindNearest(NPCType type, Vector3 position)
        {
            NPCBehavior nearest = null;
            float nearestDist = float.MaxValue;

            var list = npcsByType.ContainsKey(type) ? npcsByType[type] : null;
            if (list == null) return null;

            foreach (var npc in list)
            {
                if (npc == null || !npc.IsAlive) continue;
                float d = Vector3.Distance(position, npc.transform.position);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearest = npc;
                }
            }
            return nearest;
        }

        /// <summary>
        /// Get a summary of all NPC populations (for debug/UI).
        /// </summary>
        public Dictionary<NPCType, int> GetPopulationSummary()
        {
            var summary = new Dictionary<NPCType, int>();
            foreach (var kvp in npcsByType)
                summary[kvp.Key] = kvp.Value.Count(n => n != null && n.IsAlive);
            return summary;
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private NPCSpawnConfig GetConfig(NPCType type)
        {
            return spawnConfigs.Find(c => c.type == type);
        }
    }
}
