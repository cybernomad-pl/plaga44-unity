// PLAGA '44 - Game Over Screen
// Death screen showing cause of death from Mors Cerebri system
// CYBERNOMAD 2024-2026

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Plaga44.GameState;

namespace Plaga44.UI.Menus
{
    /// <summary>
    /// Displays the death/game over screen when the player dies.
    /// Shows the cause of death as determined by the Mors Cerebri (death brain)
    /// system, play statistics, and options to load or restart.
    /// Features a dramatic fade-in with CYBERNOMAD terminal aesthetic.
    /// </summary>
    public class GameOverScreen : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private CanvasGroup gameOverCanvasGroup;

        [Header("Death Info")]
        [SerializeField] private Text deathTitleText;
        [SerializeField] private Text causeOfDeathText;
        [SerializeField] private Text deathDescriptionText;
        [SerializeField] private Image deathIcon;

        [Header("Statistics")]
        [SerializeField] private Text survivalTimeText;
        [SerializeField] private Text daysSurvivedText;
        [SerializeField] private Text distanceTraveledText;
        [SerializeField] private Text enemiesDefeatedText;

        [Header("Buttons")]
        [SerializeField] private Button loadLastSaveButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Animation")]
        [SerializeField] private float fadeInDuration = 2f;
        [SerializeField] private float textRevealDelay = 1f;
        [SerializeField] private float statsRevealDelay = 3f;
        [SerializeField] private float buttonsRevealDelay = 5f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip deathSound;
        [SerializeField] private AudioClip ambientDeathLoop;

        [Header("Visual Effects")]
        [SerializeField] private Image backgroundOverlay;
        [SerializeField] private Color overlayColor = new Color(0.05f, 0f, 0f, 0.9f); // Near-black red
        [SerializeField] private Image vignetteEffect;

        // Cause of death descriptions (Polish)
        private static readonly Dictionary<string, DeathInfo> deathInfoLookup = new Dictionary<string, DeathInfo>
        {
            { "hypothermia", new DeathInfo {
                title = "SMIERC Z WYCHLODZENIA",
                description = "Temperatura ciala spadla ponizej progu przezycia. Zimno Warszawy okazalo sie bezlitosne.",
                iconColor = new Color(0.3f, 0.6f, 1f)
            }},
            { "dehydration", new DeathInfo {
                title = "SMIERC Z ODWODNIENIA",
                description = "Organizm nie wytrzymal braku wody. W ruinach Warszawy kazda kropla byla na wage zlota.",
                iconColor = new Color(1f, 0.8f, 0.2f)
            }},
            { "starvation", new DeathInfo {
                title = "SMIERC GLODOWA",
                description = "Glod zwycienzyl. W oblezonym miescie zywnosc stala sie najcenniejszym zasobem.",
                iconColor = new Color(0.8f, 0.5f, 0.2f)
            }},
            { "blood_loss", new DeathInfo {
                title = "WYKRWAWIENIE",
                description = "Rany okazaly sie smiertelne. Bez opatrunkow nie bylo szans.",
                iconColor = new Color(0.8f, 0.1f, 0.1f)
            }},
            { "radiation", new DeathInfo {
                title = "SMIERC RADIACYJNA",
                description = "Napromieniowanie przekroczylo smiertelna dawke. Niewidzialne zagrozenie okazalo sie najgroniejsze.",
                iconColor = new Color(0.4f, 1f, 0.2f)
            }},
            { "infection", new DeathInfo {
                title = "SMIERC Z INFEKCJI",
                description = "Zakazenie rozprzestrzienilo sie na caly organizm. Bez lekarstw nie bylo ratunku.",
                iconColor = new Color(0.7f, 0.9f, 0.1f)
            }},
            { "combat", new DeathInfo {
                title = "POLEGLES W WALCE",
                description = "Zginales od wrogiego ognia. Twoja ofiara nie bedzie zapomniana.",
                iconColor = new Color(1f, 0.3f, 0f)
            }},
            { "explosion", new DeathInfo {
                title = "SMIERC OD EKSPLOZJI",
                description = "Wybuch zakonczyl twoja walke. Warszawa plonie.",
                iconColor = new Color(1f, 0.5f, 0f)
            }},
            { "collapse", new DeathInfo {
                title = "PRZYSYPANIE GRUZAMI",
                description = "Budynek zawalil sie pogrzebujac cie pod tonami gruzu.",
                iconColor = new Color(0.5f, 0.5f, 0.5f)
            }},
            { "unknown", new DeathInfo {
                title = "KONIEC",
                description = "Twoja historia dobiega konca w ruinach Warszawy.",
                iconColor = new Color(0.6f, 0.6f, 0.6f)
            }}
        };

        private struct DeathInfo
        {
            public string title;
            public string description;
            public Color iconColor;
        }

        private void Start()
        {
            // Initially hidden
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);

            SetupButtons();

            // Subscribe to game over event
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnPlayerDied += OnPlayerDied;
            }
        }

        private void OnDestroy()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnPlayerDied -= OnPlayerDied;
            }
        }

        private void SetupButtons()
        {
            if (loadLastSaveButton != null)
                loadLastSaveButton.onClick.AddListener(OnLoadLastSave);
            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestart);
            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(OnMainMenu);
        }

        // ----- Game Over Trigger -----

        private void OnPlayerDied(string causeOfDeath)
        {
            StartCoroutine(ShowGameOverSequence(causeOfDeath));
        }

        private IEnumerator ShowGameOverSequence(string causeOfDeath)
        {
            // Resolve cause of death info
            DeathInfo info;
            if (!deathInfoLookup.TryGetValue(causeOfDeath.ToLower(), out info))
            {
                info = deathInfoLookup["unknown"];
            }

            // Show panel
            if (gameOverPanel != null)
                gameOverPanel.SetActive(true);

            // Hide all elements initially
            SetElementsVisible(false);

            // Play death sound
            if (audioSource != null && deathSound != null)
            {
                audioSource.PlayOneShot(deathSound);
            }

            // Phase 1: Fade in background overlay
            yield return StartCoroutine(FadeInOverlay());

            // Phase 2: Reveal death title
            yield return new WaitForSecondsRealtime(textRevealDelay);

            if (deathTitleText != null)
            {
                deathTitleText.gameObject.SetActive(true);
                deathTitleText.text = info.title;
            }

            // Phase 3: Reveal cause and description
            yield return new WaitForSecondsRealtime(1f);

            if (causeOfDeathText != null)
            {
                causeOfDeathText.gameObject.SetActive(true);
                causeOfDeathText.text = $"Mors Cerebri: {causeOfDeath.ToUpper()}";
            }

            if (deathDescriptionText != null)
            {
                deathDescriptionText.gameObject.SetActive(true);
                deathDescriptionText.text = info.description;
            }

            if (deathIcon != null)
            {
                deathIcon.gameObject.SetActive(true);
                deathIcon.color = info.iconColor;
            }

            // Phase 4: Reveal statistics
            yield return new WaitForSecondsRealtime(statsRevealDelay - textRevealDelay - 1f);

            RevealStatistics();

            // Phase 5: Reveal buttons
            yield return new WaitForSecondsRealtime(buttonsRevealDelay - statsRevealDelay);

            RevealButtons();

            // Start ambient death loop
            if (audioSource != null && ambientDeathLoop != null)
            {
                audioSource.clip = ambientDeathLoop;
                audioSource.loop = true;
                audioSource.Play();
            }
        }

        // ----- Visual Phases -----

        private void SetElementsVisible(bool visible)
        {
            if (deathTitleText != null) deathTitleText.gameObject.SetActive(visible);
            if (causeOfDeathText != null) causeOfDeathText.gameObject.SetActive(visible);
            if (deathDescriptionText != null) deathDescriptionText.gameObject.SetActive(visible);
            if (deathIcon != null) deathIcon.gameObject.SetActive(visible);
            if (survivalTimeText != null) survivalTimeText.gameObject.SetActive(visible);
            if (daysSurvivedText != null) daysSurvivedText.gameObject.SetActive(visible);
            if (distanceTraveledText != null) distanceTraveledText.gameObject.SetActive(visible);
            if (enemiesDefeatedText != null) enemiesDefeatedText.gameObject.SetActive(visible);
            if (loadLastSaveButton != null) loadLastSaveButton.gameObject.SetActive(visible);
            if (restartButton != null) restartButton.gameObject.SetActive(visible);
            if (mainMenuButton != null) mainMenuButton.gameObject.SetActive(visible);
        }

        private IEnumerator FadeInOverlay()
        {
            if (gameOverCanvasGroup == null) yield break;

            gameOverCanvasGroup.alpha = 0f;
            float elapsed = 0f;

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                gameOverCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                yield return null;
            }

            gameOverCanvasGroup.alpha = 1f;
        }

        private void RevealStatistics()
        {
            var gsm = GameStateManager.Instance;
            if (gsm == null) return;

            var data = gsm.GetActiveSaveData();
            if (data == null) return;

            if (survivalTimeText != null)
            {
                float hours = gsm.TotalPlayTime / 3600f;
                survivalTimeText.gameObject.SetActive(true);
                survivalTimeText.text = $"CZAS PRZETRWANIA: {hours:F1}h";
            }

            if (daysSurvivedText != null)
            {
                daysSurvivedText.gameObject.SetActive(true);
                daysSurvivedText.text = $"DNI: {data.environment.dayNumber}";
            }

            if (distanceTraveledText != null)
            {
                distanceTraveledText.gameObject.SetActive(true);
                distanceTraveledText.text = $"DYSTANS: {data.progress.distanceTraveled:F0}m";
            }

            if (enemiesDefeatedText != null)
            {
                enemiesDefeatedText.gameObject.SetActive(true);
                enemiesDefeatedText.text = $"WROGOWIE: {data.progress.enemiesDefeated}";
            }
        }

        private void RevealButtons()
        {
            if (loadLastSaveButton != null)
            {
                loadLastSaveButton.gameObject.SetActive(true);
                loadLastSaveButton.interactable = SaveSystem.SaveSystem.HasAnySaves();
            }
            if (restartButton != null) restartButton.gameObject.SetActive(true);
            if (mainMenuButton != null) mainMenuButton.gameObject.SetActive(true);
        }

        // ----- Button Handlers -----

        private void OnLoadLastSave()
        {
            var saves = SaveSystem.SaveSystem.GetAllSaveMetadata();
            if (saves.Count > 0 && GameStateManager.Instance != null)
            {
                GameStateManager.Instance.LoadGame(saves[0].saveName);
            }
        }

        private void OnRestart()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.StartNewGame();
            }
        }

        private void OnMainMenu()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.ReturnToMainMenu();
            }
        }
    }
}
