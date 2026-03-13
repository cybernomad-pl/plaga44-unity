using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.AI
{
    /// <summary>
    /// Spawns capsule enemies at designated spawn points.
    /// No models yet -- each enemy is a capsule (same approach as mannequin targets).
    ///
    /// Visual coding:
    ///   Enemy capsule = green (Patrol), changes colour per state via EnemyAI.
    ///   Head sphere = slightly darker cap on top.
    ///
    /// NavMesh requirement: the scene must have a baked NavMesh.
    /// Use "CYBERNOMAD / Scene Setup / Setup AI Testbed" which bakes it at runtime via
    /// NavMeshSurface. If the surface is not present, spawned enemies will log a warning.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        private const string LOG = "[PLAGA44]";

        [Header("Spawn Settings")]
        [Tooltip("Points where enemies can spawn. If empty, uses this transform.")]
        public Transform[] spawnPoints = new Transform[0];

        [Tooltip("Maximum number of live enemies at any time.")]
        public int maxEnemies = 5;

        [Tooltip("Seconds before a dead enemy is replaced. 0 = no respawn.")]
        public float respawnDelay = 30f;

        [Header("Enemy Config")]
        [Tooltip("Patrol path assigned to spawned enemies. Can be null for idle enemies.")]
        public PatrolPath patrolPath;

        [Tooltip("Starting HP for each spawned enemy.")]
        public float enemyHP = 100f;

        // ---- Private ----

        private readonly List<GameObject> _liveEnemies = new List<GameObject>();
        private int _spawnCounter;

        // ---- Lifecycle ----

        private void Start()
        {
            SpawnInitialEnemies();
        }

        // ---- Spawn logic ----

        private void SpawnInitialEnemies()
        {
            int count = Mathf.Min(maxEnemies, spawnPoints.Length > 0 ? spawnPoints.Length : 1);
            for (int i = 0; i < count; i++)
            {
                SpawnEnemyAt(GetSpawnPoint(i));
            }
        }

        private Transform GetSpawnPoint(int index)
        {
            if (spawnPoints != null && spawnPoints.Length > 0 && index < spawnPoints.Length)
                return spawnPoints[index];
            return transform;
        }

        private void SpawnEnemyAt(Transform spawnPoint)
        {
            if (_liveEnemies.Count >= maxEnemies) return;

            Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

            GameObject enemy = CreateEnemyCapsule(pos, rot);
            _liveEnemies.Add(enemy);

            // Hook death to trigger respawn
            var health = enemy.GetComponent<EnemyHealth>();
            if (health != null)
            {
                health.OnDeath += (_) => StartCoroutine(HandleEnemyDeath(enemy));
            }

            Debug.Log($"{LOG} Spawned enemy {enemy.name} at {pos}.");
        }

        private IEnumerator HandleEnemyDeath(GameObject enemy)
        {
            // Wait for ragdoll to settle before removing from list
            yield return new WaitForSeconds(5f);

            _liveEnemies.Remove(enemy);
            Destroy(enemy, 2f);

            if (respawnDelay > 0f)
            {
                yield return new WaitForSeconds(respawnDelay - 5f);
                // Pick a random spawn point for the replacement
                int idx = Random.Range(0, Mathf.Max(1, spawnPoints.Length));
                SpawnEnemyAt(GetSpawnPoint(idx));
            }
        }

        // ---- Enemy prefab construction ----

        /// <summary>
        /// Builds a capsule enemy with EnemyAI, EnemyHealth and a NavMeshAgent.
        /// Hierarchy:
        ///   Enemy_N  (root: NavMeshAgent, EnemyAI, EnemyHealth, CapsuleCollider, Rigidbody)
        ///     Visual  (capsule mesh, Renderer -- colour changes with state)
        ///     Head    (sphere on top for head hit zone -- EnemyHitReceiver)
        /// </summary>
        private GameObject CreateEnemyCapsule(Vector3 position, Quaternion rotation)
        {
            _spawnCounter++;
            string enemyName = $"Enemy_{_spawnCounter}";

            // Root
            var root = new GameObject(enemyName);
            root.transform.position = position;
            root.transform.rotation = rotation;
            root.tag = "Enemy";

            // Capsule collider on root (for physics / stone impact)
            var capsuleCol = root.AddComponent<CapsuleCollider>();
            capsuleCol.height = 1.8f;
            capsuleCol.radius = 0.35f;
            capsuleCol.center = new Vector3(0f, 0.9f, 0f);

            // Rigidbody -- kinematic until death (then EnemyAI enables ragdoll)
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            // NavMeshAgent
            var agent = root.AddComponent<NavMeshAgent>();
            agent.height = 1.8f;
            agent.radius = 0.35f;
            agent.baseOffset = 0f;
            agent.speed = 2f;
            agent.angularSpeed = 180f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.3f;
            agent.autoBraking = true;

            // EnemyHealth
            var health = root.AddComponent<EnemyHealth>();
            health.maxHP = enemyHP;

            // EnemyAI
            var ai = root.AddComponent<EnemyAI>();
            ai.patrolPath = patrolPath;
            ai.visionRange = 15f;
            ai.visionHalfAngle = 60f;
            ai.hearingRadius = 5f;
            ai.patrolSpeed = 1.8f;
            ai.chaseSpeed = 4.5f;
            ai.meleeRange = 2.0f;

            // Visual: capsule body (visual only, no collider)
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            Object.Destroy(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            ApplyEnemyMaterial(visual, new Color(0.15f, 0.75f, 0.15f)); // Patrol green

            // Head: small sphere -- receives stone hits and routes to EnemyHealth
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform);
            head.transform.localPosition = new Vector3(0f, 1.75f, 0f);
            head.transform.localScale = Vector3.one * 0.28f;
            ApplyEnemyMaterial(head, new Color(0.1f, 0.55f, 0.1f));

            // EnemyHitReceiver on the head collider -- routes stone hits to EnemyHealth
            var headReceiver = head.AddComponent<EnemyHitReceiver>();
            headReceiver.zoneName = "Head";
            headReceiver.enemyHealth = health;

            // EnemyHitReceiver on the body collider -- routes stone hits to EnemyHealth
            var bodyReceiver = root.AddComponent<EnemyHitReceiver>();
            bodyReceiver.zoneName = "Body";
            bodyReceiver.enemyHealth = health;

            return root;
        }

        private static void ApplyEnemyMaterial(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            r.material = new Material(shader) { color = color };
        }
    }
}
