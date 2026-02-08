// PLAGA '44 - AutoSave Manager
// Periodic autosave and trigger-based save (shelter entry, etc.)
// CYBERNOMAD 2024-2026

using System;
using UnityEngine;

namespace Plaga44.SaveSystem
{
    /// <summary>
    /// Manages automatic saving: periodic interval saves and event-triggered saves
    /// (e.g., entering a shelter). Attaches to a persistent GameObject.
    /// </summary>
    public class AutoSaveManager : MonoBehaviour
    {
        [Header("Autosave Settings")]
        [Tooltip("Interval between periodic autosaves in seconds")]
        [SerializeField] private float autosaveIntervalSeconds = 300f; // 5 minutes

        [Tooltip("Enable periodic autosave")]
        [SerializeField] private bool periodicAutosaveEnabled = true;

        [Tooltip("Save when entering a shelter")]
        [SerializeField] private bool saveOnShelterEntry = true;

        [Tooltip("Save on scene transition")]
        [SerializeField] private bool saveOnSceneTransition = true;

        [Tooltip("Minimum seconds between any two autosaves (debounce)")]
        [SerializeField] private float minimumAutosaveInterval = 30f;

        private float timeSinceLastAutosave;
        private float timeSinceLastAnySave;
        private bool isAutosaving;

        public static AutoSaveManager Instance { get; private set; }

        public event Action OnAutosaveStarted;
        public event Action OnAutosaveCompleted;
        public event Action<string> OnAutosaveFailed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            timeSinceLastAutosave = 0f;
            timeSinceLastAnySave = 0f;

            // Subscribe to save system events
            SaveSystem.OnSaveCompleted += OnAnySaveCompleted;
        }

        private void OnDestroy()
        {
            SaveSystem.OnSaveCompleted -= OnAnySaveCompleted;

            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!periodicAutosaveEnabled) return;
            if (GameState.GameStateManager.Instance == null) return;
            if (GameState.GameStateManager.Instance.IsPaused) return;
            if (GameState.GameStateManager.Instance.CurrentState != GameState.GameStateManager.GameState.Playing) return;

            timeSinceLastAutosave += Time.unscaledDeltaTime;
            timeSinceLastAnySave += Time.unscaledDeltaTime;

            if (timeSinceLastAutosave >= autosaveIntervalSeconds)
            {
                PerformAutosave("periodic");
            }
        }

        /// <summary>
        /// Called when the player enters a shelter trigger zone.
        /// </summary>
        public void OnShelterEntered()
        {
            if (!saveOnShelterEntry) return;
            PerformAutosave("shelter_entry");
        }

        /// <summary>
        /// Called before a scene transition.
        /// </summary>
        public void OnSceneTransition()
        {
            if (!saveOnSceneTransition) return;
            PerformAutosave("scene_transition");
        }

        /// <summary>
        /// Force an autosave regardless of timer (e.g., before a dangerous area).
        /// </summary>
        public void ForceAutosave()
        {
            PerformAutosave("forced");
        }

        private void PerformAutosave(string reason)
        {
            if (isAutosaving) return;

            // Debounce: don't save too frequently
            if (timeSinceLastAnySave < minimumAutosaveInterval)
            {
                Debug.Log($"[AutoSave] Skipped ({reason}): too soon since last save ({timeSinceLastAnySave:F1}s < {minimumAutosaveInterval}s)");
                return;
            }

            var gsm = GameState.GameStateManager.Instance;
            if (gsm == null)
            {
                Debug.LogWarning("[AutoSave] GameStateManager not available.");
                return;
            }

            isAutosaving = true;
            OnAutosaveStarted?.Invoke();

            Debug.Log($"[AutoSave] Starting autosave (reason: {reason})");

            try
            {
                SaveData data = gsm.CaptureCurrentState();
                bool success = SaveSystem.AutoSave(data);

                if (success)
                {
                    timeSinceLastAutosave = 0f;
                    Debug.Log($"[AutoSave] Completed successfully (reason: {reason})");
                    OnAutosaveCompleted?.Invoke();
                }
                else
                {
                    Debug.LogWarning("[AutoSave] Save returned false.");
                    OnAutosaveFailed?.Invoke("Save operation returned false");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AutoSave] Failed: {ex.Message}");
                OnAutosaveFailed?.Invoke(ex.Message);
            }
            finally
            {
                isAutosaving = false;
            }
        }

        private void OnAnySaveCompleted(string fileName)
        {
            timeSinceLastAnySave = 0f;
        }

        /// <summary>
        /// Update autosave interval at runtime (e.g., from settings).
        /// </summary>
        public void SetAutosaveInterval(float seconds)
        {
            autosaveIntervalSeconds = Mathf.Max(60f, seconds);
            Debug.Log($"[AutoSave] Interval set to {autosaveIntervalSeconds}s");
        }

        /// <summary>
        /// Enable/disable periodic autosave at runtime.
        /// </summary>
        public void SetPeriodicAutosaveEnabled(bool enabled)
        {
            periodicAutosaveEnabled = enabled;
            Debug.Log($"[AutoSave] Periodic autosave {(enabled ? "enabled" : "disabled")}");
        }
    }
}
