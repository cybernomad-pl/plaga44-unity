// PLAGA '44 - Pause Menu Controller
// In-game pause menu with save, load, settings, resume, quit options
// CYBERNOMAD 2024-2026

using UnityEngine;
using UnityEngine.UI;
using Plaga44.GameState;
using Plaga44.SaveSystem;

namespace Plaga44.UI.Menus
{
    /// <summary>
    /// Manages the in-game pause menu overlay. Activated by Escape key or
    /// VR controller menu button. Provides save, resume, settings, and
    /// return-to-menu options.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject saveConfirmPanel;

        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button quickSaveButton;
        [SerializeField] private Button quickLoadButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;

        [Header("Save Confirm")]
        [SerializeField] private InputField saveNameInput;
        [SerializeField] private Button saveConfirmButton;
        [SerializeField] private Button saveCancelButton;

        [Header("Status")]
        [SerializeField] private Text statusMessage;
        [SerializeField] private float statusMessageDuration = 2f;

        [Header("Info Display")]
        [SerializeField] private Text playTimeText;
        [SerializeField] private Text dayText;
        [SerializeField] private Text locationText;

        [Header("Input")]
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip closeSound;
        [SerializeField] private AudioClip clickSound;
        [SerializeField] private AudioClip saveSound;

        private float statusTimer;
        private bool isOpen;

        private void Start()
        {
            SetupButtons();
            ClosePauseMenu();

            // Subscribe to game state events
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnGamePaused += OnGamePaused;
                GameStateManager.Instance.OnGameResumed += OnGameResumed;
            }

            SaveSystem.OnSaveCompleted += OnSaveSuccess;
            SaveSystem.OnSaveError += OnSaveError;
        }

        private void OnDestroy()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnGamePaused -= OnGamePaused;
                GameStateManager.Instance.OnGameResumed -= OnGameResumed;
            }

            SaveSystem.OnSaveCompleted -= OnSaveSuccess;
            SaveSystem.OnSaveError -= OnSaveError;
        }

        private void Update()
        {
            // Handle pause key input
            if (Input.GetKeyDown(pauseKey))
            {
                TogglePause();
            }

            // Status message timer
            if (statusTimer > 0f)
            {
                statusTimer -= Time.unscaledDeltaTime;
                if (statusTimer <= 0f && statusMessage != null)
                {
                    statusMessage.text = "";
                }
            }
        }

        private void SetupButtons()
        {
            if (resumeButton != null)
                resumeButton.onClick.AddListener(OnResumeClicked);
            if (saveButton != null)
                saveButton.onClick.AddListener(OnSaveClicked);
            if (quickSaveButton != null)
                quickSaveButton.onClick.AddListener(OnQuickSaveClicked);
            if (quickLoadButton != null)
                quickLoadButton.onClick.AddListener(OnQuickLoadClicked);
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsClicked);
            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(OnMainMenuClicked);
            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuitClicked);

            if (saveConfirmButton != null)
                saveConfirmButton.onClick.AddListener(OnSaveConfirmed);
            if (saveCancelButton != null)
                saveCancelButton.onClick.AddListener(OnSaveCancelled);
        }

        // ----- Pause Toggle -----

        public void TogglePause()
        {
            if (GameStateManager.Instance == null) return;

            if (GameStateManager.Instance.IsPlaying)
            {
                GameStateManager.Instance.PauseGame();
            }
            else if (GameStateManager.Instance.IsPaused)
            {
                GameStateManager.Instance.ResumeGame();
            }
        }

        private void OnGamePaused()
        {
            OpenPauseMenu();
        }

        private void OnGameResumed()
        {
            ClosePauseMenu();
        }

        private void OpenPauseMenu()
        {
            isOpen = true;

            if (pausePanel != null)
                pausePanel.SetActive(true);
            if (saveConfirmPanel != null)
                saveConfirmPanel.SetActive(false);

            UpdateInfoDisplay();
            UpdateQuickLoadButton();
            PlaySound(openSound);
        }

        private void ClosePauseMenu()
        {
            isOpen = false;

            if (pausePanel != null)
                pausePanel.SetActive(false);
            if (saveConfirmPanel != null)
                saveConfirmPanel.SetActive(false);
        }

        // ----- Button Handlers -----

        private void OnResumeClicked()
        {
            PlaySound(closeSound);
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.ResumeGame();
            }
        }

        private void OnSaveClicked()
        {
            PlaySound(clickSound);

            if (saveConfirmPanel != null)
                saveConfirmPanel.SetActive(true);

            // Pre-fill save name
            if (saveNameInput != null)
            {
                var gsm = GameStateManager.Instance;
                if (gsm != null)
                {
                    var data = gsm.GetActiveSaveData();
                    if (data != null)
                    {
                        saveNameInput.text = $"Day {data.environment.dayNumber} - {data.position.currentZone}";
                    }
                }
            }
        }

        private void OnSaveConfirmed()
        {
            PlaySound(saveSound);

            string saveName = saveNameInput != null ? saveNameInput.text : "Manual Save";
            if (string.IsNullOrWhiteSpace(saveName))
                saveName = "Manual Save";

            if (GameStateManager.Instance != null)
            {
                bool success = GameStateManager.Instance.SaveGame(saveName);
                if (success)
                {
                    ShowStatus("GRA ZAPISANA");
                }
            }

            if (saveConfirmPanel != null)
                saveConfirmPanel.SetActive(false);
        }

        private void OnSaveCancelled()
        {
            PlaySound(clickSound);
            if (saveConfirmPanel != null)
                saveConfirmPanel.SetActive(false);
        }

        private void OnQuickSaveClicked()
        {
            PlaySound(saveSound);

            if (GameStateManager.Instance != null)
            {
                bool success = GameStateManager.Instance.QuickSave();
                ShowStatus(success ? "SZYBKI ZAPIS OK" : "BLAD ZAPISU");
            }
        }

        private void OnQuickLoadClicked()
        {
            PlaySound(clickSound);

            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.QuickLoad();
            }
        }

        private void OnSettingsClicked()
        {
            PlaySound(clickSound);
            // Settings panel will be implemented separately
            ShowStatus("USTAWIENIA - WKROTCE");
        }

        private void OnMainMenuClicked()
        {
            PlaySound(clickSound);

            // Autosave before returning to menu
            if (GameStateManager.Instance != null)
            {
                var autosave = AutoSaveManager.Instance;
                if (autosave != null)
                {
                    autosave.ForceAutosave();
                }

                GameStateManager.Instance.ReturnToMainMenu();
            }
        }

        private void OnQuitClicked()
        {
            PlaySound(clickSound);

            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.QuitGame();
            }
        }

        // ----- UI Updates -----

        private void UpdateInfoDisplay()
        {
            if (GameStateManager.Instance == null) return;

            var data = GameStateManager.Instance.GetActiveSaveData();
            if (data == null) return;

            if (playTimeText != null)
            {
                float hours = GameStateManager.Instance.TotalPlayTime / 3600f;
                playTimeText.text = $"Czas gry: {hours:F1}h";
            }

            if (dayText != null)
            {
                dayText.text = $"Dzien {data.environment.dayNumber} | {data.environment.currentSeason}";
            }

            if (locationText != null)
            {
                locationText.text = data.position.currentZone ?? "---";
            }
        }

        private void UpdateQuickLoadButton()
        {
            if (quickLoadButton != null)
            {
                quickLoadButton.interactable = SaveSystem.HasQuickSave();
            }
        }

        private void ShowStatus(string message)
        {
            if (statusMessage != null)
            {
                statusMessage.text = message;
                statusTimer = statusMessageDuration;
            }
        }

        // ----- Save Events -----

        private void OnSaveSuccess(string fileName)
        {
            ShowStatus("ZAPISANO");
        }

        private void OnSaveError(string error)
        {
            ShowStatus($"BLAD: {error}");
        }

        // ----- Audio -----

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
