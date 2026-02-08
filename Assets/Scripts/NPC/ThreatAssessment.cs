using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Plaga44.NPC
{
    // =========================================================================
    // ThreatFactor - a single contributor to the overall threat score
    // =========================================================================

    [System.Serializable]
    public struct ThreatFactor
    {
        public string source;
        public float weight;       // 0-1  importance multiplier
        public float value;        // raw score  (0-100)
        public float Contribution => weight * value;
    }

    // =========================================================================
    // ThreatReport - snapshot of threat evaluation for a single NPC/encounter
    // =========================================================================

    public class ThreatReport
    {
        public NPCBehavior npc;
        public ThreatLevel level;
        public float rawScore;                       // 0-100
        public float distanceToPlayer;
        public List<ThreatFactor> factors = new List<ThreatFactor>();
        public float timestamp;

        public override string ToString()
        {
            string f = string.Join(", ", factors.Select(x => $"{x.source}:{x.Contribution:F1}"));
            return $"[{level}] {npc?.Name ?? "?"} score={rawScore:F1} dist={distanceToPlayer:F1} ({f})";
        }
    }

    // =========================================================================
    // ThreatAssessment - evaluates danger level of nearby NPCs/encounters
    // =========================================================================

    /// <summary>
    /// Singleton system that periodically scans for NPCs around the player and
    /// produces a <see cref="ThreatReport"/> for each.  Reports are sorted by
    /// descending danger so the UI / AI director can react to the most
    /// pressing threat first.
    ///
    /// Threat factors include:
    ///   - NPC type (military > criminal > animal > civilian)
    ///   - Proximity (closer = more dangerous)
    ///   - NPC state (fighting / alert > patrol > idle)
    ///   - Number of hostiles (group bonus)
    ///   - Special flags (rabies, suspicion, etc.)
    /// </summary>
    public class ThreatAssessment : MonoBehaviour
    {
        // ----- Singleton -----
        public static ThreatAssessment Instance { get; private set; }

        // ----- Config -----
        [Header("Scan Settings")]
        [SerializeField] private float scanRadius = 60f;
        [SerializeField] private float scanInterval = 0.5f;
        [SerializeField] private LayerMask npcLayerMask = ~0;

        [Header("Threat Weights")]
        [Tooltip("Base threat by NPC type (index = NPCType enum)")]
        [SerializeField] private float[] baseTypeThreat = new float[]
        {
            10f,  // Civilian
            80f,  // MilitaryPatrol
            70f,  // CityGuard
            65f,  // Police
            30f,  // FireDept
            50f,  // Criminal
            35f,  // Scavenger
            20f,  // Addict
            40f   // Animal
        };

        [SerializeField] private float proximityWeight = 0.3f;
        [SerializeField] private float typeWeight = 0.35f;
        [SerializeField] private float stateWeight = 0.2f;
        [SerializeField] private float groupWeight = 0.15f;

        // ----- Runtime -----
        private Transform playerTransform;
        private float nextScanTime;
        private readonly List<ThreatReport> currentReports = new List<ThreatReport>();
        private readonly Collider[] scanBuffer = new Collider[64];

        /// <summary>Current threat reports sorted by descending danger.</summary>
        public IReadOnlyList<ThreatReport> Reports => currentReports;

        /// <summary>Highest current threat level across all detected NPCs.</summary>
        public ThreatLevel HighestThreat { get; private set; }

        /// <summary>Sum of all raw scores (ambient danger metric).</summary>
        public float AmbientDanger { get; private set; }

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        private void Update()
        {
            if (playerTransform == null) return;
            if (Time.time < nextScanTime) return;

            nextScanTime = Time.time + scanInterval;
            PerformScan();
        }

        // =====================================================================
        // Scanning
        // =====================================================================

        private void PerformScan()
        {
            currentReports.Clear();
            HighestThreat = ThreatLevel.None;
            AmbientDanger = 0f;

            int count = Physics.OverlapSphereNonAlloc(
                playerTransform.position, scanRadius, scanBuffer, npcLayerMask);

            int hostileCount = 0;

            // First pass: count hostiles for group bonus
            for (int i = 0; i < count; i++)
            {
                NPCBehavior npc = scanBuffer[i].GetComponent<NPCBehavior>();
                if (npc == null || !npc.IsAlive) continue;
                if (IsHostileState(npc.State)) hostileCount++;
            }

            // Second pass: build reports
            for (int i = 0; i < count; i++)
            {
                NPCBehavior npc = scanBuffer[i].GetComponent<NPCBehavior>();
                if (npc == null || !npc.IsAlive) continue;

                ThreatReport report = Evaluate(npc, hostileCount);
                currentReports.Add(report);

                if (report.level > HighestThreat)
                    HighestThreat = report.level;

                AmbientDanger += report.rawScore;
            }

            // Sort descending by raw score
            currentReports.Sort((a, b) => b.rawScore.CompareTo(a.rawScore));
        }

        // =====================================================================
        // Evaluation
        // =====================================================================

        /// <summary>
        /// Evaluate a single NPC and produce a <see cref="ThreatReport"/>.
        /// Can be called externally for one-off assessments.
        /// </summary>
        public ThreatReport Evaluate(NPCBehavior npc, int nearbyHostiles = 0)
        {
            ThreatReport report = new ThreatReport
            {
                npc = npc,
                distanceToPlayer = playerTransform != null
                    ? Vector3.Distance(playerTransform.position, npc.transform.position)
                    : float.MaxValue,
                timestamp = Time.time
            };

            // Factor 1: NPC type
            float typeScore = GetBaseTypeThreat(npc.Type);
            report.factors.Add(new ThreatFactor
            {
                source = "type",
                weight = typeWeight,
                value = typeScore
            });

            // Factor 2: Proximity (inverse distance, clamped)
            float proxScore = Mathf.Clamp01(1f - report.distanceToPlayer / scanRadius) * 100f;
            report.factors.Add(new ThreatFactor
            {
                source = "proximity",
                weight = proximityWeight,
                value = proxScore
            });

            // Factor 3: Behavioral state
            float stateScore = GetStateThreat(npc.State);
            report.factors.Add(new ThreatFactor
            {
                source = "state",
                weight = stateWeight,
                value = stateScore
            });

            // Factor 4: Group / numbers
            float groupScore = Mathf.Clamp(nearbyHostiles * 15f, 0f, 100f);
            report.factors.Add(new ThreatFactor
            {
                source = "group",
                weight = groupWeight,
                value = groupScore
            });

            // Factor 5: Special flags (subclass-specific extras)
            AddSpecialFactors(npc, report);

            // Compute raw score
            float raw = 0f;
            foreach (var f in report.factors) raw += f.Contribution;
            report.rawScore = Mathf.Clamp(raw, 0f, 100f);

            // Map to threat level
            report.level = ScoreToLevel(report.rawScore);

            return report;
        }

        /// <summary>
        /// Hook for subclass-specific threat factors (rabies, suspicion, etc.).
        /// Override or call externally to inject extra factors before score is computed.
        /// </summary>
        protected virtual void AddSpecialFactors(NPCBehavior npc, ThreatReport report)
        {
            // CivilianNPC suspicion
            if (npc is CivilianNPC civilian)
            {
                float suspicion = civilian.SuspicionLevel;
                if (suspicion > 0.1f)
                {
                    report.factors.Add(new ThreatFactor
                    {
                        source = "suspicion",
                        weight = 0.2f,
                        value = suspicion * 100f
                    });
                }
            }

            // AnimalNPC rabies
            if (npc is AnimalNPC animal && animal.IsRabid)
            {
                report.factors.Add(new ThreatFactor
                {
                    source = "rabies",
                    weight = 0.25f,
                    value = 90f
                });
            }
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private float GetBaseTypeThreat(NPCType type)
        {
            int idx = (int)type;
            if (idx >= 0 && idx < baseTypeThreat.Length)
                return baseTypeThreat[idx];
            return 30f;
        }

        private static float GetStateThreat(BehaviorState state)
        {
            switch (state)
            {
                case BehaviorState.Fight:       return 100f;
                case BehaviorState.Alert:       return 80f;
                case BehaviorState.Report:      return 70f;
                case BehaviorState.Investigate:  return 60f;
                case BehaviorState.Patrol:      return 40f;
                case BehaviorState.Wander:      return 20f;
                case BehaviorState.Trade:       return 5f;
                case BehaviorState.Idle:        return 10f;
                case BehaviorState.Flee:        return 5f;
                default:                        return 0f;
            }
        }

        private static bool IsHostileState(BehaviorState state)
        {
            return state == BehaviorState.Fight ||
                   state == BehaviorState.Alert ||
                   state == BehaviorState.Report;
        }

        private static ThreatLevel ScoreToLevel(float score)
        {
            if (score >= 75f) return ThreatLevel.Critical;
            if (score >= 50f) return ThreatLevel.High;
            if (score >= 25f) return ThreatLevel.Medium;
            if (score > 5f)   return ThreatLevel.Low;
            return ThreatLevel.None;
        }

        /// <summary>
        /// Get threat report for a specific NPC, or null if not currently tracked.
        /// </summary>
        public ThreatReport GetReport(NPCBehavior npc)
        {
            return currentReports.Find(r => r.npc == npc);
        }

        /// <summary>
        /// Get all reports at or above the specified threat level.
        /// </summary>
        public List<ThreatReport> GetReportsAbove(ThreatLevel minLevel)
        {
            return currentReports.Where(r => r.level >= minLevel).ToList();
        }

        private void OnDrawGizmosSelected()
        {
            if (playerTransform != null)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
                Gizmos.DrawSphere(playerTransform.position, scanRadius);
            }
        }
    }
}
