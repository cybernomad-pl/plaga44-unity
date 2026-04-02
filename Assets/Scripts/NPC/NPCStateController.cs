using System;
using UnityEngine;

namespace Plaga44.NPC
{
    /// <summary>
    /// NPC behavioural states used by NPCLocomotion and future AI systems.
    /// </summary>
    public enum NPCState
    {
        Idle,
        Patrol,
        Alert,
        Chase,
        Dead
    }

    /// <summary>
    /// Manages NPC state transitions and exposes per-state configuration
    /// (speed multiplier, Animator parameter names).
    /// Raises OnStateChanged whenever the active state changes.
    /// </summary>
    [RequireComponent(typeof(NPCLocomotion))]
    public class NPCStateController : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Per-state configuration
        // ------------------------------------------------------------------ //

        [Serializable]
        public class StateConfig
        {
            public NPCState state;

            [Tooltip("NavMeshAgent speed multiplier relative to NPCLocomotion.baseSpeed.")]
            [Min(0f)]
            public float speedMultiplier = 1f;

            [Tooltip("Animator bool/trigger parameter to set when entering this state. Leave empty to skip.")]
            public string animatorParam = "";

            [Tooltip("If true, animatorParam is treated as a Trigger; otherwise as a Bool.")]
            public bool isTrigger = false;
        }

        [Header("State Configuration")]
        [SerializeField]
        private StateConfig[] stateConfigs = new StateConfig[]
        {
            new StateConfig { state = NPCState.Idle,   speedMultiplier = 0f,  animatorParam = "IsMoving", isTrigger = false },
            new StateConfig { state = NPCState.Patrol, speedMultiplier = 1f,  animatorParam = "IsMoving", isTrigger = false },
            new StateConfig { state = NPCState.Alert,  speedMultiplier = 0.6f, animatorParam = "IsAlert",  isTrigger = false },
            new StateConfig { state = NPCState.Chase,  speedMultiplier = 2f,  animatorParam = "IsChasing", isTrigger = false },
            new StateConfig { state = NPCState.Dead,   speedMultiplier = 0f,  animatorParam = "Die",       isTrigger = true  },
        };

        [Header("Transitions")]
        [Tooltip("Delay in seconds before transitioning from Alert back to Patrol when no threat is detected.")]
        [Min(0f)]
        public float alertCooldown = 5f;

        // ------------------------------------------------------------------ //
        //  Events
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Fired when the NPC transitions to a new state.
        /// Arguments: previousState, newState.
        /// </summary>
        public event Action<NPCState, NPCState> OnStateChanged;

        // ------------------------------------------------------------------ //
        //  State
        // ------------------------------------------------------------------ //

        public NPCState CurrentState { get; private set; } = NPCState.Idle;

        private NPCLocomotion _locomotion;
        private Animator _animator;
        private float _alertTimer;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            _locomotion = GetComponent<NPCLocomotion>();
            _animator   = GetComponent<Animator>();
        }

        private void Start()
        {
            // Apply initial state without firing the event
            ApplyConfig(CurrentState);
        }

        private void Update()
        {
            // Auto-reset Alert -> Patrol after cooldown
            if (CurrentState == NPCState.Alert)
            {
                _alertTimer -= Time.deltaTime;
                if (_alertTimer <= 0f)
                    SetState(NPCState.Patrol);
            }
        }

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>Transition to newState. No-op if already in that state.</summary>
        public void SetState(NPCState newState)
        {
            if (newState == CurrentState) return;
            if (CurrentState == NPCState.Dead) return; // dead is terminal

            NPCState previous = CurrentState;
            CurrentState = newState;

            if (newState == NPCState.Alert)
                _alertTimer = alertCooldown;

            ApplyConfig(newState);
            OnStateChanged?.Invoke(previous, newState);

            Debug.Log($"[NPC] {name}: {previous} -> {newState}");
        }

        /// <summary>Convenience helper: puts NPC into Alert state and starts cooldown timer.</summary>
        public void TriggerAlert()  => SetState(NPCState.Alert);

        /// <summary>Convenience helper: puts NPC into Chase state.</summary>
        public void TriggerChase()  => SetState(NPCState.Chase);

        /// <summary>Kills the NPC (terminal state).</summary>
        public void Die()           => SetState(NPCState.Dead);

        // ------------------------------------------------------------------ //
        //  Internal helpers
        // ------------------------------------------------------------------ //

        private void ApplyConfig(NPCState targetState)
        {
            StateConfig cfg = GetConfig(targetState);
            if (cfg == null) return;

            // Update locomotion speed
            if (_locomotion != null)
                _locomotion.SetSpeedMultiplier(cfg.speedMultiplier);

            // Update animator
            if (_animator != null && !string.IsNullOrEmpty(cfg.animatorParam))
            {
                if (cfg.isTrigger)
                {
                    _animator.SetTrigger(cfg.animatorParam);
                }
                else
                {
                    // For bool params: set all non-trigger params to false, then enable the active one.
                    // This ensures bools from previous states are cleared.
                    foreach (var c in stateConfigs)
                    {
                        if (!c.isTrigger && !string.IsNullOrEmpty(c.animatorParam))
                            _animator.SetBool(c.animatorParam, false);
                    }
                    _animator.SetBool(cfg.animatorParam, true);
                }
            }
        }

        private StateConfig GetConfig(NPCState targetState)
        {
            if (stateConfigs == null) return null;
            foreach (var cfg in stateConfigs)
            {
                if (cfg.state == targetState) return cfg;
            }
            return null;
        }
    }
}
