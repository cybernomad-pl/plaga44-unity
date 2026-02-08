// PLAGA '44 - Game Manager
// Singleton managing overall game state, scene transitions, and initialization.
// Part of issue #23: Unity VR project structure and dual-mode scene architecture

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plaga44.Core
{
    /// <summary>
    /// Central game manager singleton. Persists across scenes.
    /// Manages game flow: BARKA hub -> Olsztyn map -> death -> BARKA respawn.
    ///
    /// Scene architecture from IPK grant:
    /// - BARKA: Orbital hub scene (respawn point, noEZUS interface, equipment selection)
    /// - Olsztyn: Main gameplay map (~2km^2 around Olsztyn castle ruins, Jura KCz)
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Scene Names")]
        [SerializeField] private string barkaSceneName = "BARKA";
        [SerializeField] private string olsztynSceneName = "Olsztyn";

        [Header("Game Settings")]
        [SerializeField] private GameMode currentGameMode = GameMode.HardcoreSurvival;
        [SerializeField] private int maxPlayers = 4;  // Photon co-op

        public GameMode CurrentGameMode => currentGameMode;
        public bool IsGameRunning { get; private set; }
        public bool IsPaused { get; private set; }

        // Events
        public event System.Action<GameMode> OnGameModeChanged;
        public event System.Action OnGameStarted;
        public event System.Action<string> OnPlayerDied;
        public event System.Action OnGamePaused;
        public event System.Action OnGameResumed;

        private float sessionStartTime;
        private string causeOfDeath;

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

        /// <summary>
        /// Set game mode before starting.
        /// Mode A: Edu-Tourist (free exploration, no survival pressure)
        /// Mode B: Hardcore Survival (full physiology, permadeath)
        /// </summary>
        public void SetGameMode(GameMode mode)
        {
            currentGameMode = mode;
            OnGameModeChanged?.Invoke(mode);
        }

        /// <summary>
        /// Start a new game session. Transitions from BARKA to Olsztyn.
        /// </summary>
        public void StartNewGame()
        {
            IsGameRunning = true;
            IsPaused = false;
            sessionStartTime = Time.time;
            causeOfDeath = null;

            OnGameStarted?.Invoke();
            LoadScene(olsztynSceneName);
        }

        /// <summary>
        /// Return to BARKA hub (death, manual exit, or session end).
        /// </summary>
        public void ReturnToBarka()
        {
            IsGameRunning = false;
            IsPaused = false;
            LoadScene(barkaSceneName);
        }

        /// <summary>
        /// Handle player death. Generates noEZUS death report.
        /// In Mode B (Hardcore): permadeath - returns to BARKA for new session.
        /// In Mode A (Edu-Tourist): respawn at nearest checkpoint.
        /// </summary>
        public void HandlePlayerDeath(string cause)
        {
            causeOfDeath = cause;
            float survivalTime = Time.time - sessionStartTime;

            OnPlayerDied?.Invoke(cause);

            if (currentGameMode == GameMode.HardcoreSurvival)
            {
                // Permadeath - generate death report and return to BARKA
                Debug.Log($"[PLAGA44] DEATH: {cause} | Survival time: {survivalTime:F1}s");
                ReturnToBarka();
            }
            // Mode A: handled by checkpoint system
        }

        /// <summary>
        /// Toggle pause state.
        /// </summary>
        public void TogglePause()
        {
            if (!IsGameRunning) return;

            IsPaused = !IsPaused;
            Time.timeScale = IsPaused ? 0f : 1f;

            if (IsPaused)
                OnGamePaused?.Invoke();
            else
                OnGameResumed?.Invoke();
        }

        /// <summary>
        /// Get survival duration for current session.
        /// </summary>
        public float GetSurvivalTime()
        {
            return IsGameRunning ? Time.time - sessionStartTime : 0f;
        }

        /// <summary>
        /// Get cause of death for noEZUS report.
        /// </summary>
        public string GetCauseOfDeath()
        {
            return causeOfDeath;
        }

        private void LoadScene(string sceneName)
        {
            SceneManager.LoadSceneAsync(sceneName);
        }
    }

    /// <summary>
    /// Game mode enum matching IPK grant D.VI specification.
    /// Mode A: Edu-Tourist - accessibility focused, heritage exploration
    /// Mode B: Hardcore Survival - full physiology simulation, permadeath
    /// </summary>
    public enum GameMode
    {
        EduTourist,         // Mode A: Free teleport, debug camera, info points, no survival pressure
        HardcoreSurvival    // Mode B: Full physiology, permadeath, diegetic interface
    }
}
