using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Plaga44.NPC
{
    /// <summary>
    /// Instantiates NPC prefabs at runtime within a configurable radius.
    /// Spawned NPCs are placed on the NavMesh; positions that can't be projected
    /// are skipped with a warning.
    ///
    /// Usage:
    ///   1. Assign an NPC prefab that has NPCLocomotion (+ NavMeshAgent).
    ///   2. Optionally assign a shared WaypointPath -- each NPC will reference it.
    ///   3. Call Spawn() at runtime (or enable spawnOnStart).
    /// </summary>
    public class NPCSpawner : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Inspector
        // ------------------------------------------------------------------ //

        [Header("Prefab")]
        [Tooltip("NPC prefab to instantiate. Must have NPCLocomotion.")]
        public GameObject npcPrefab;

        [Header("Spawn Settings")]
        [Tooltip("Number of NPCs to spawn.")]
        [Min(1)]
        public int spawnCount = 3;

        [Tooltip("Radius around this transform's position within which NPCs are placed.")]
        [Min(0.5f)]
        public float spawnRadius = 5f;

        [Tooltip("NavMesh sample distance for projecting random positions onto the mesh.")]
        [Min(0.1f)]
        public float navMeshSampleDistance = 2f;

        [Tooltip("Spawn automatically when the scene starts.")]
        public bool spawnOnStart = true;

        [Header("Patrol Path")]
        [Tooltip("Optional shared WaypointPath assigned to every spawned NPC.")]
        public WaypointPath sharedWaypointPath;

        [Header("Parent")]
        [Tooltip("Optional parent Transform for spawned NPCs. Keeps hierarchy clean.")]
        public Transform spawnParent;

        // ------------------------------------------------------------------ //
        //  Runtime state
        // ------------------------------------------------------------------ //

        private readonly List<GameObject> _spawnedNPCs = new List<GameObject>();

        public IReadOnlyList<GameObject> SpawnedNPCs => _spawnedNPCs;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Start()
        {
            if (spawnOnStart)
                Spawn();
        }

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Spawns spawnCount NPCs around this transform.
        /// Existing spawned NPCs are NOT destroyed -- call DespawnAll() first if needed.
        /// </summary>
        public void Spawn()
        {
            if (npcPrefab == null)
            {
                Debug.LogWarning($"[NPCSpawner] {name}: npcPrefab is not assigned. Aborting spawn.");
                return;
            }

            int spawned = 0;
            int maxAttempts = spawnCount * 5; // give up after N attempts per NPC

            for (int attempt = 0; attempt < maxAttempts && spawned < spawnCount; attempt++)
            {
                Vector3 candidate = transform.position + (Vector3)(Random.insideUnitCircle * spawnRadius);
                candidate.y = transform.position.y;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
                    continue;

                GameObject npc = Instantiate(npcPrefab, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), spawnParent);
                npc.name = $"{npcPrefab.name}_{spawned:D2}";

                // Assign shared patrol path if provided
                if (sharedWaypointPath != null)
                {
                    var locomotion = npc.GetComponent<NPCLocomotion>();
                    if (locomotion != null)
                        locomotion.waypointPath = sharedWaypointPath;
                }

                _spawnedNPCs.Add(npc);
                spawned++;
            }

            if (spawned < spawnCount)
                Debug.LogWarning($"[NPCSpawner] {name}: Only {spawned}/{spawnCount} NPCs placed -- not enough valid NavMesh positions within radius {spawnRadius}m.");
            else
                Debug.Log($"[NPCSpawner] {name}: Spawned {spawned} NPCs.");
        }

        /// <summary>Destroys all NPCs created by this spawner.</summary>
        public void DespawnAll()
        {
            foreach (var npc in _spawnedNPCs)
            {
                if (npc != null)
                    Destroy(npc);
            }
            _spawnedNPCs.Clear();
        }

        // ------------------------------------------------------------------ //
        //  Gizmo
        // ------------------------------------------------------------------ //

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, spawnRadius);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }
    }
}
