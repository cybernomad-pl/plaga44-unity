// PLAGA '44 - Main Menu Controller
// Main menu: new game, load, settings, quit
// CYBERNOMAD 2024-2026

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Plaga44.GameState;
using Plaga44.SaveSystem;

namespace Plaga44.UI.Menus
{
    /// <summary>
    /// Controls the main menu interface. Provides new game, continue, load,
    /// settings, and quit options. Manages save file listing for the load screen.
    /// Styled with CYBERNOMAD terminal aesthetic.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Main Menu Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject loadGamePanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject confirmNewGamePanel;

        [Header("Main Menu Buttons")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button loadGameButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Load Game")]
        [SerializeField] private Transform saveListContainer;
        [SerializeField] private GameObject saveEntryPrefab;
        [SerializeField] private Button loadBackButton;
        [SerializeField] private Text loadEmptyMessage;

        [Header("Confirm New Game")]
        [SerializeField] private Button confirmNewGameYes;
        [SerializeField] private Button confirmNewGameNo;

        [Header("Settings Panel")]
        [SerializeField] private Button settingsBackButton;

        [Header("Branding")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text versionText;
        [SerializeField] private Text subtitleText;

        [Header("Audio")]
        [SerializeField] private AudioSource menuAudioSource;
        [SerializeField] private AudioClip buttonClickSound;
        [SerializeField] private AudioClip menuMusicClip;

        private List<SaveMetadata> cachedSaveList;

        private void Start()
        {
            SetupButtons();
            ShowMainPanel();
            UpdateContinueButton();

            // Set branding
            if (titleText != null) titleText.text = "PLAGA '44";
            if (subtitleText != null) subtitleText.text = "WARSZAWA WALCZY";
            if (versionText != null) versionText.text = $"v{Application.version}";

            // Play menu music
            if (menuAudioSource != null && menuMusicClip != null)
            {
                menuAudioSource.clip = menuMusicClip;
                menuAudioSource.loop = true;
                menuAudioSource.Play();
            }
        }

        private void SetupButtons()
        {
            if (newGameButton != null)
                newGameButton.onClick.AddListener(OnNewGameClicked);
            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueClicked);
            if (loadGameButton != null)
                loadGameButton.onClick.AddListener(OnLoadGameClicked);
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsClicked);
            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuitClicked);

            if (loadBackButton != null)
                loadBackButton.onClick.AddListener(ShowMainPanel);
            if (settingsBackButton != null)
                settingsBackButton.onClick.AddListener(ShowMainPanel);

            if (confirmNewGameYes != null)
                confirmNewGameYes.onClick.AddListener(OnConfirmNewGame);
            if (confirmNewGameNo != null)
                confirmNewGameNo.onClick.AddListener(ShowMainPanel);
        }

        // ----- Button Handlers -----

        private void OnNewGameClicked()
        {
            PlayClickSound();

            // If saves exist, confirm overwrite warning
            if (SaveSystem.HasAnySaves())
            {
                ShowPanel(confirmNewGamePanel);
            }
            else
            {
                OnConfirmNewGame();
            }
        }

        private void OnConfirmNewGame()
        {
            PlayClickSound();

            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.StartNewGame();
            }
            else
            {
                Debug.LogError("[MainMenu] GameStateManager not found!");
            }
        }

        private void OnContinueClicked()
        {
            PlayClickSound();

            // Load most recent save
            List<SaveMetadata> saves = SaveSystem.GetAllSaveMetadata();
            if (saves.Count > 0)
            {
                string mostRecentName = saves[0].saveName;
                if (GameStateManager.Instance != null)
                {
                    GameStateManager.Instance.LoadGame(mostRecentName);
                }
            }
        }

        private void OnLoadGameClicked()
        {
            PlayClickSound();
            ShowLoadGamePanel();
        }

        private void OnSettingsClicked()
        {
            PlayClickSound();
            ShowPanel(settingsPanel);
        }

        private void OnQuitClicked()
        {
            PlayClickSound();

            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.QuitGame();
            }
            else
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        // ----- Panel Management -----

        private void ShowMainPanel()
        {
            ShowPanel(mainPanel);
            UpdateContinueButton();
        }

        private void ShowLoadGamePanel()
        {
            ShowPanel(loadGamePanel);
            PopulateSaveList();
        }

        private void ShowPanel(GameObject panel)
        {
            // Hide all panels
            if (mainPanel != null) mainPanel.SetActive(false);
            if (loadGamePanel != null) loadGamePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (confirmNewGamePanel != null) confirmNewGamePanel.SetActive(false);

            // Show target panel
            if (panel != null) panel.SetActive(true);
        }

        private void UpdateContinueButton()
        {
            if (continueButton != null)
            {
                continueButton.interactable = SaveSystem.HasAnySaves();
            }
        }

        // ----- Save List -----

        private void PopulateSaveList()
        {
            // Clear existing entries
            if (saveListContainer != null)
            {
                foreach (Transform child in saveListContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            cachedSaveList = SaveSystem.GetAllSaveMetadata();

            if (cachedSaveList.Count == 0)
            {
                if (loadEmptyMessage != null)
                {
                    loadEmptyMessage.gameObject.SetActive(true);
                    loadEmptyMessage.text = "BRAK ZAPISANYCH GIER";
                }
                return;
            }

            if (loadEmptyMessage != null)
                loadEmptyMessage.gameObject.SetActive(false);

            foreach (SaveMetadata meta in cachedSaveList)
            {
                CreateSaveEntry(meta);
            }
        }

        private void CreateSaveEntry(SaveMetadata meta)
        {
            if (saveListContainer == null || saveEntryPrefab == null) return;

            GameObject entry = Instantiate(saveEntryPrefab, saveListContainer);

            // Find and populate text fields
            Text nameText = entry.transform.Find("SaveName")?.GetComponent<Text>();
            Text detailsText = entry.transform.Find("SaveDetails")?.GetComponent<Text>();
            Button loadButton = entry.transform.Find("LoadButton")?.GetComponent<Button>();
            Button deleteButton = entry.transform.Find("DeleteButton")?.GetComponent<Button>();

            if (nameText != null)
            {
                nameText.text = meta.saveName ?? "???";
            }

            if (detailsText != null)
            {
                float hours = meta.playTimeSeconds / 3600f;
                string playTime = hours >= 1f
                    ? $"{hours:F1}h"
                    : $"{meta.playTimeSeconds / 60f:F0}min";

                detailsText.text = $"Dzien {meta.dayNumber} | {meta.currentSeason} | {meta.currentZone} | HP: {meta.health:F0}% | {playTime}";
            }

            // Wire up load button
            if (loadButton != null)
            {
                string saveName = meta.saveName;
                loadButton.onClick.AddListener(() =>
                {
                    PlayClickSound();
                    if (GameStateManager.Instance != null)
                    {
                        GameStateManager.Instance.LoadGame(saveName);
                    }
                });
            }

            // Wire up delete button
            if (deleteButton != null)
            {
                string saveName = meta.saveName;
                deleteButton.onClick.AddListener(() =>
                {
                    PlayClickSound();
                    SaveSystem.DeleteSaveFile(saveName);
                    PopulateSaveList(); // Refresh list
                });
            }
        }

        // ----- Audio -----

        private void PlayClickSound()
        {
            if (menuAudioSource != null && buttonClickSound != null)
            {
                menuAudioSource.PlayOneShot(buttonClickSound);
            }
        }
    }
}
