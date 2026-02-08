// PLAGA '44 - Game State Manager
// Singleton managing overall game state, pause, game over
// CYBERNOMAD 2024-2026

using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Plaga44.SaveSystem;

namespace Plaga44.GameState
{
    /// <summary>
    /// Central singleton managing game state transitions, pause functionality,
    /// and coordinating between save system and gameplay systems.
    /// Persists across scene loads.
    /// </summary>
    public class GameStateManager : MonoBehaviour
    {
        public enum GameState
        {
            MainMenu,
            Loading,
            Playing,
            Paused,
            GameOver,
            Cutscene
        }

        public static GameStateManager Instance { get; private set; }

        [Header("State")]
        [SerializeField] private GameState currentState = GameState.MainMenu;

        [Header("Scene Names")]
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string gameplayScene = "Warsaw_1944";

        [Header("Time Tracking")]
        [SerializeField] private float totalPlayTimeSeconds;

        // Public accessors
        public GameState CurrentState => currentState;
        public bool IsPaused => currentState == GameState.Paused;
        public bool IsPlaying => currentState == GameState.Playing;
        public bool IsGameOver => currentState == GameState.GameOver;
        public float TotalPlayTime => totalPlayTimeSeconds;

        // Events for state changes
        public event Action<GameState, GameState> OnStateChanged; // oldState, newState
        public event Action OnGamePaused;
        public event Action OnGameResumed;
        public event Action<string> OnPlayerDied; // cause of death
        public event Action OnNewGameStarted;
        public event Action<SaveData> OnGameLoaded;

        // Current active save data (in-memory working copy)
        private SaveData activeSaveData;

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

        private void Update()
        {
            if (currentState == GameState.Playing)
            {
                totalPlayTimeSeconds += Time.unscaledDeltaTime;
            }
        }

        // ----- State Transitions -----

        /// <summary>
        /// Start a new game with default state.
        /// </summary>
        public void StartNewGame()
        {
            Debug.Log("[GameStateManager] Starting new game...");

            activeSaveData = new SaveData();
            activeSaveData.environment.currentSeason = "Lato"; // August 1944 = summer
            activeSaveData.environment.timeOfDay = 6f;         // Dawn
            activeSaveData.environment.dayNumber = 1;          // Day 1 of uprising
            activeSaveData.environment.ambientTemperature = 22f;
            activeSaveData.environment.currentWeather = "clear";
            activeSaveData.position.currentScene = gameplayScene;
            activeSaveData.position.currentZone = "Wola";      // Uprising started in Wola

            totalPlayTimeSeconds = 0f;

            ChangeState(GameState.Loading);
            OnNewGameStarted?.Invoke();

            // Load the gameplay scene
            SceneManager.LoadScene(gameplayScene);
        }

        /// <summary>
        /// Load a game from a save file.
        /// </summary>
        public void LoadGame(string saveFileName)
        {
            Debug.Log($"[GameStateManager] Loading game: {saveFileName}");

            SaveData data = SaveSystem.SaveSystem.Load(saveFileName);
            if (data == null)
            {
                Debug.LogError("[GameStateManager] Failed to load save file.");
                return;
            }

            activeSaveData = data;
            totalPlayTimeSeconds = data.playTimeSeconds;

            ChangeState(GameState.Loading);
            OnGameLoaded?.Invoke(data);

            // Load the appropriate scene
            string targetScene = data.position.currentScene;
            if (string.IsNullOrEmpty(targetScene))
                targetScene = gameplayScene;

            SceneManager.LoadScene(targetScene);
        }

        /// <summary>
        /// Called when a scene finishes loading. Transitions to Playing state.
        /// </summary>
        public void OnSceneReady()
        {
            if (currentState == GameState.Loading)
            {
                ChangeState(GameState.Playing);
                Time.timeScale = 1f;
            }
        }

        /// <summary>
        /// Pause the game.
        /// </summary>
        public void PauseGame()
        {
            if (currentState != GameState.Playing) return;

            ChangeState(GameState.Paused);
            Time.timeScale = 0f;
            OnGamePaused?.Invoke();
        }

        /// <summary>
        /// Resume from pause.
        /// </summary>
        public void ResumeGame()
        {
            if (currentState != GameState.Paused) return;

            ChangeState(GameState.Playing);
            Time.timeScale = 1f;
            OnGameResumed?.Invoke();
        }

        /// <summary>
        /// Toggle pause state.
        /// </summary>
        public void TogglePause()
        {
            if (currentState == GameState.Playing)
                PauseGame();
            else if (currentState == GameState.Paused)
                ResumeGame();
        }

        /// <summary>
        /// Trigger game over (player death). Called by Mors Cerebri system.
        /// </summary>
        public void TriggerGameOver(string causeOfDeath)
        {
            if (currentState == GameState.GameOver) return;

            Debug.Log($"[GameStateManager] Game Over - Cause: {causeOfDeath}");

            if (activeSaveData != null)
            {
                activeSaveData.physiology.causeOfDeath = causeOfDeath;
            }

            ChangeState(GameState.GameOver);
            Time.timeScale = 0f;
            OnPlayerDied?.Invoke(causeOfDeath);
        }

        /// <summary>
        /// Return to main menu.
        /// </summary>
        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            ChangeState(GameState.MainMenu);
            SceneManager.LoadScene(mainMenuScene);
        }

        /// <summary>
        /// Quit the application.
        /// </summary>
        public void QuitGame()
        {
            Debug.Log("[GameStateManager] Quitting game...");

            // Autosave before quit if we're in a game
            if (currentState == GameState.Playing || currentState == GameState.Paused)
            {
                SaveData data = CaptureCurrentState();
                SaveSystem.SaveSystem.AutoSave(data);
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ----- Save Data Operations -----

        /// <summary>
        /// Captures the current game state into a SaveData object.
        /// Systems should register their state capture callbacks.
        /// </summary>
        public SaveData CaptureCurrentState()
        {
            if (activeSaveData == null)
                activeSaveData = new SaveData();

            activeSaveData.playTimeSeconds = totalPlayTimeSeconds;
            activeSaveData.timestamp = DateTime.UtcNow.ToString("o");

            // Position is captured from the player transform
            // (Other systems update activeSaveData directly through their own references)

            return activeSaveData;
        }

        /// <summary>
        /// Returns the active save data for systems to read/update.
        /// </summary>
        public SaveData GetActiveSaveData()
        {
            return activeSaveData;
        }

        /// <summary>
        /// Manual save to a named slot.
        /// </summary>
        public bool SaveGame(string saveName)
        {
            SaveData data = CaptureCurrentState();
            data.saveName = saveName;
            return SaveSystem.SaveSystem.Save(data, saveName);
        }

        /// <summary>
        /// Quick save.
        /// </summary>
        public bool QuickSave()
        {
            SaveData data = CaptureCurrentState();
            return SaveSystem.SaveSystem.QuickSave(data);
        }

        /// <summary>
        /// Quick load.
        /// </summary>
        public void QuickLoad()
        {
            if (SaveSystem.SaveSystem.HasQuickSave())
            {
                LoadGame("quicksave");
            }
            else
            {
                Debug.LogWarning("[GameStateManager] No quick save found.");
            }
        }

        // ----- Internal -----

        private void ChangeState(GameState newState)
        {
            if (currentState == newState) return;

            GameState oldState = currentState;
            currentState = newState;

            Debug.Log($"[GameStateManager] State: {oldState} -> {newState}");
            OnStateChanged?.Invoke(oldState, newState);
        }
    }
}
