// =============================================================================
// AkslopeWanderAI.cs
// CYBERNOMAD -- Prymitywne AI wander dla NPC AKSLOPE. BEZ NavMesh (projekt nie
// uzywa NavMesh runtime). Losuje cel w promieniu wokol pozycji startowej, obraca
// sie ku niemu i idzie przodem (translacja transformu). Y trzymany na terenie
// przez raycast z gory w dol. Animacje: LOSOWA animacja locomotion z
// NpcController.ClipNames na starcie i przy kazdym retargecie.
//
// ZERO FALLBACKOW: brak trafienia terenu -> LogWarning (raz) + zostaw Y bez zmian.
// Brak animacji locomotion -> LogError (raz) + nie ruszaj animacji.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.Npc
{
    [RequireComponent(typeof(NpcController))]
    public class AkslopeWanderAI : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][AkslopeWanderAI]";

        [Header("Ruch")]
        [Tooltip("Predkosc marszu (m/s).")]
        public float moveSpeed = 1.0f;
        [Tooltip("Promien wokol pozycji startowej, w ktorym losowane sa cele (m).")]
        public float wanderRadius = 8f;
        [Tooltip("Predkosc obrotu ku celowi (deg/s).")]
        public float turnSpeed = 120f;

        [Header("Retarget")]
        [Tooltip("Minimalny czas do wymuszonego retargetu (s).")]
        public float retargetMin = 3f;
        [Tooltip("Maksymalny czas do wymuszonego retargetu (s).")]
        public float retargetMax = 8f;

        [Header("Trzymanie na terenie")]
        [Tooltip("Wysokosc startu promienia nad NPC (m).")]
        public float groundRaycastUp = 5f;
        [Tooltip("Zasieg promienia w dol od punktu startu (m).")]
        public float groundRaycastDown = 20f;

        // Slowa kluczowe klipow locomotion (case-insensitive, Contains).
        private static readonly string[] LocomotionKeywords =
        {
            "walk", "run", "jog", "strafe", "sneak", "turn",
            "backwards", "forward", "march", "sprint", "step"
        };

        private NpcController _npc;
        private Vector3 _startPosition;
        private Vector3 _target;
        private float _retargetTimer;

        // Indeksy klipow locomotion w library -- cache raz w Start.
        private readonly List<int> _locomotionIndices = new List<int>();
        private bool _hasLocomotion;

        // Jednorazowe ostrzezenia (ZERO spamu w logu).
        private bool _warnedNoGround;
        private bool _erroredNoLocomotion;

        private void Start()
        {
            _npc = GetComponent<NpcController>();
            _startPosition = transform.position;

            CacheLocomotionIndices();

            _target = PickWanderTarget();
            _retargetTimer = RandomRetargetDelay();
            PlayRandomLocomotion();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // --- Obrot ku celowi (tylko Y) ---
            Vector3 toTarget = _target - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, desired, turnSpeed * dt);
            }

            // --- Marsz do przodu (translacja) ---
            Vector3 fwd = transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f)
            {
                fwd.Normalize();
                transform.position += fwd * (moveSpeed * dt);
            }

            // --- Trzymanie Y na terenie ---
            StickToGround();

            // --- Retarget: po dojsciu lub po uplywie czasu ---
            _retargetTimer -= dt;
            Vector3 flatDelta = _target - transform.position;
            flatDelta.y = 0f;
            bool arrived = flatDelta.sqrMagnitude < 0.25f; // dist < 0.5
            if (arrived || _retargetTimer <= 0f)
            {
                _target = PickWanderTarget();
                _retargetTimer = RandomRetargetDelay();
                PlayRandomLocomotion();
            }
        }

        // ---------------------------------------------------------------------
        // Teren
        // ---------------------------------------------------------------------
        private void StickToGround()
        {
            Vector3 origin = transform.position + Vector3.up * groundRaycastUp;
            float maxDist = groundRaycastUp + groundRaycastDown;

            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, maxDist);
            float bestDist = float.MaxValue;
            bool found = false;
            float bestY = 0f;

            for (int i = 0; i < hits.Length; i++)
            {
                // Pomin colliderY nalezace do tego NPC (inaczej ustawimy Y na wlasnej kapsule).
                if (hits[i].collider != null &&
                    hits[i].collider.transform.IsChildOf(transform)) continue;
                if (hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    bestY = hits[i].point.y;
                    found = true;
                }
            }

            if (found)
            {
                Vector3 p = transform.position;
                p.y = bestY;
                transform.position = p;
                return;
            }

            // Brak trafienia -> NIE zgaduj Y. Zostaw bez zmian, ostrzez raz.
            if (!_warnedNoGround)
            {
                _warnedNoGround = true;
                Debug.LogWarning($"{LOG} '{name}' -- brak trafienia terenu pod raycastem " +
                                 $"(origin={origin}, dist={maxDist}). Y bez zmian.");
            }
        }

        // ---------------------------------------------------------------------
        // Cel
        // ---------------------------------------------------------------------
        private Vector3 PickWanderTarget()
        {
            Vector2 disc = Random.insideUnitCircle * wanderRadius;
            return new Vector3(
                _startPosition.x + disc.x,
                transform.position.y,
                _startPosition.z + disc.y);
        }

        private float RandomRetargetDelay()
        {
            return Random.Range(retargetMin, retargetMax);
        }

        // ---------------------------------------------------------------------
        // Animacje locomotion
        // ---------------------------------------------------------------------
        private void CacheLocomotionIndices()
        {
            _locomotionIndices.Clear();
            _hasLocomotion = false;

            IReadOnlyList<string> names = _npc.ClipNames;
            for (int i = 0; i < names.Count; i++)
            {
                string n = names[i];
                if (string.IsNullOrEmpty(n)) continue;
                string lower = n.ToLowerInvariant();
                for (int k = 0; k < LocomotionKeywords.Length; k++)
                {
                    if (lower.Contains(LocomotionKeywords[k]))
                    {
                        _locomotionIndices.Add(i);
                        break;
                    }
                }
            }

            _hasLocomotion = _locomotionIndices.Count > 0;
            if (_hasLocomotion)
                Debug.Log($"{LOG} '{name}' -- {_locomotionIndices.Count} klipow locomotion " +
                          $"(z {names.Count} w library).");
        }

        private void PlayRandomLocomotion()
        {
            if (!_hasLocomotion)
            {
                if (!_erroredNoLocomotion)
                {
                    _erroredNoLocomotion = true;
                    Debug.LogError($"{LOG} '{name}' -- brak klipow locomotion w library. " +
                                   "Animacja nietknieta (uruchom PLAGA44/Setup/NPC System (Full)?).");
                }
                return;
            }

            int pick = _locomotionIndices[Random.Range(0, _locomotionIndices.Count)];
            _npc.Play(pick);
        }
    }
}
