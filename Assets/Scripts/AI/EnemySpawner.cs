using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.AI
{
    /// <summary>
    /// Spawner wrogów z puli spawn pointów.
    /// Obsługuje: limit wrogów (Quest performance), cooldown, fale.
    ///
    /// Wymaga: przynajmniej jeden SpawnPointMarker w scenie lub
    /// ręcznie przypisane spawnPoints.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Spawner]";

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------

        [Header("Prefab")]
        [Tooltip("Prefab Klaszczura z komponentem EnemyAI")]
        public GameObject enemyPrefab;

        [Header("Spawn Points")]
        [Tooltip("Jesli puste -- szuka automatycznie SpawnPointMarker w scenie")]
        public SpawnPointMarker[] spawnPoints;

        [Header("Limits -- Quest Performance")]
        [Tooltip("Max jednoczesnych wrogów w scenie. Quest 3: max 8-12 dla stabilnych 72fps")]
        [Range(1, 20)]
        public int maxEnemies = 6;

        [Tooltip("Cooldown miedzy kolejnymi spawnami [s]")]
        public float spawnInterval = 5f;

        [Header("Wave System")]
        [Tooltip("Wlacza system fal. Jesli false -- spawnuje ciagle az do limitu")]
        public bool useWaves = false;

        [Tooltip("Dane fal (ilosc wrogów, przerwa po fali)")]
        public WaveData[] waves;

        [Tooltip("Jesli true -- powtarza ostatnia fale w nieskonczonosc")]
        public bool loopLastWave = true;

        [Header("Debug")]
        public bool showDebugLogs = true;

        // -------------------------------------------------------------------------
        // Private
        // -------------------------------------------------------------------------

        private readonly List<EnemyAI> _activeEnemies = new List<EnemyAI>();
        private bool _spawningActive;
        private int _currentWaveIndex;
        private Coroutine _spawnCoroutine;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Start()
        {
            // Zbierz spawn pointy jesli nie przypisane ręcznie
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                spawnPoints = FindObjectsByType<SpawnPointMarker>(FindObjectsSortMode.None);
                if (showDebugLogs)
                    Debug.Log($"{LOG} Znaleziono {spawnPoints.Length} spawn pointów w scenie.");
            }

            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogWarning($"{LOG} Brak spawn pointów! Spawner bezczynny.");
                return;
            }

            if (enemyPrefab == null)
            {
                Debug.LogWarning($"{LOG} Brak enemyPrefab! Przypisz prefab Klaszczura.");
                return;
            }

            StartSpawning();
        }

        private void OnDestroy()
        {
            StopSpawning();
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        public void StartSpawning()
        {
            if (_spawningActive) return;
            _spawningActive = true;

            if (useWaves)
                _spawnCoroutine = StartCoroutine(WaveSpawnLoop());
            else
                _spawnCoroutine = StartCoroutine(ContinuousSpawnLoop());
        }

        public void StopSpawning()
        {
            _spawningActive = false;
            if (_spawnCoroutine != null)
            {
                StopCoroutine(_spawnCoroutine);
                _spawnCoroutine = null;
            }
        }

        public int ActiveEnemyCount => CountActiveEnemies();

        public int CurrentWave => _currentWaveIndex;

        // -------------------------------------------------------------------------
        // Spawn loops
        // -------------------------------------------------------------------------

        private IEnumerator ContinuousSpawnLoop()
        {
            while (_spawningActive)
            {
                CleanDeadEnemies();

                if (CountActiveEnemies() < maxEnemies)
                {
                    SpawnEnemy();
                }

                yield return new WaitForSeconds(spawnInterval);
            }
        }

        private IEnumerator WaveSpawnLoop()
        {
            if (waves == null || waves.Length == 0)
            {
                Debug.LogWarning($"{LOG} useWaves=true ale brak danych fal. Przelaczam na tryb ciagly.");
                yield return StartCoroutine(ContinuousSpawnLoop());
                yield break;
            }

            while (_spawningActive)
            {
                WaveData wave = waves[_currentWaveIndex];
                if (showDebugLogs)
                    Debug.Log($"{LOG} Fala {_currentWaveIndex + 1}/{waves.Length}: {wave.enemyCount} wrogów.");

                // Spawnuj wrogów fali
                int spawned = 0;
                while (spawned < wave.enemyCount && _spawningActive)
                {
                    CleanDeadEnemies();

                    if (CountActiveEnemies() < maxEnemies)
                    {
                        SpawnEnemy();
                        spawned++;
                        yield return new WaitForSeconds(spawnInterval);
                    }
                    else
                    {
                        // Czekaj az zwolni sie miejsce
                        yield return new WaitForSeconds(1f);
                    }
                }

                // Czekaj az wszyscy wrogowie z fali zgina (lub przerwa po fali)
                yield return new WaitForSeconds(wave.postWaveDelay);
                yield return new WaitUntil(() => CountActiveEnemies() == 0);

                if (showDebugLogs)
                    Debug.Log($"{LOG} Fala {_currentWaveIndex + 1} zakonczona.");

                // Przejdz do nastepnej fali
                if (_currentWaveIndex + 1 < waves.Length)
                {
                    _currentWaveIndex++;
                }
                else if (loopLastWave)
                {
                    // Powtarzaj ostatnia fale
                    if (showDebugLogs)
                        Debug.Log($"{LOG} Powtarzam ostatnia fale.");
                }
                else
                {
                    if (showDebugLogs)
                        Debug.Log($"{LOG} Wszystkie fale zakonczone.");
                    _spawningActive = false;
                }
            }
        }

        // -------------------------------------------------------------------------
        // Spawn single enemy
        // -------------------------------------------------------------------------

        private void SpawnEnemy()
        {
            SpawnPointMarker point = GetRandomSpawnPoint();
            if (point == null)
            {
                Debug.LogWarning($"{LOG} Brak dostępnego spawn pointu.");
                return;
            }

            GameObject go = Instantiate(enemyPrefab, point.transform.position, point.transform.rotation);
            go.name = $"Klaszczur_{System.DateTime.Now.Ticks % 10000}";

            EnemyAI ai = go.GetComponent<EnemyAI>();
            if (ai == null)
            {
                Debug.LogWarning($"{LOG} Prefab {go.name} nie ma komponentu EnemyAI!");
                Destroy(go);
                return;
            }

            _activeEnemies.Add(ai);

            if (showDebugLogs)
                Debug.Log($"{LOG} Spawned {go.name} at {point.name}. Aktywnych: {CountActiveEnemies()}/{maxEnemies}");
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private SpawnPointMarker GetRandomSpawnPoint()
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return null;

            // Prosty randomizer -- mozna rozbudowac o najdalszy od gracza etc.
            return spawnPoints[Random.Range(0, spawnPoints.Length)];
        }

        private void CleanDeadEnemies()
        {
            // Usun null (zniszczone GO) i martwe AI z listy
            _activeEnemies.RemoveAll(e => e == null || e.IsDead);
        }

        private int CountActiveEnemies()
        {
            CleanDeadEnemies();
            return _activeEnemies.Count;
        }

        // -------------------------------------------------------------------------
        // Gizmos
        // -------------------------------------------------------------------------

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (spawnPoints == null) return;
            foreach (var sp in spawnPoints)
            {
                if (sp == null) continue;
                Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
                Gizmos.DrawWireSphere(sp.transform.position, 0.4f);
                Gizmos.DrawRay(sp.transform.position, sp.transform.forward * 0.8f);
            }
        }
#endif
    }

    // -------------------------------------------------------------------------
    // Wave data (serializable struct dla inspector)
    // -------------------------------------------------------------------------

    [System.Serializable]
    public class WaveData
    {
        [Tooltip("Ilosc wrogów do uspawnowania w tej fali")]
        public int enemyCount = 5;

        [Tooltip("Czas przerwy po zakonczeniu fali [s]")]
        public float postWaveDelay = 10f;
    }
}
