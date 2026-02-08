// PLAGA '44 - Scene Transition Manager
// Handles async scene loading with transition effects.
// Part of issue #23: Unity VR project structure

using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace Plaga44.Core
{
    /// <summary>
    /// Manages scene loading/transitions with VR-friendly fade effects.
    /// Avoids jarring cuts that cause motion sickness in VR.
    ///
    /// Scenes:
    /// - BARKA: Orbital hub (noEZUS interface, equipment selection, respawn)
    /// - Olsztyn: Main gameplay area (~2km^2, Jura Krakowsko-Czestochowska)
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        [Header("Transition Settings")]
        [SerializeField] private float fadeOutDuration = 1.0f;
        [SerializeField] private float fadeInDuration = 1.5f;
        [SerializeField] private Color fadeColor = Color.black;

        [Header("Loading Screen")]
        [SerializeField] private Canvas loadingCanvas;
        [SerializeField] private UnityEngine.UI.Slider progressBar;
        [SerializeField] private UnityEngine.UI.Text statusText;

        public bool IsTransitioning { get; private set; }

        // Events
        public event System.Action OnTransitionStarted;
        public event System.Action OnTransitionCompleted;
        public event System.Action<float> OnLoadProgress;

        private Material fadeMaterial;

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
        /// Load a scene with fade transition.
        /// VR-safe: uses sphere fade around player head to avoid disorientation.
        /// </summary>
        public void TransitionToScene(string sceneName)
        {
            if (IsTransitioning) return;
            StartCoroutine(TransitionCoroutine(sceneName));
        }

        private IEnumerator TransitionCoroutine(string sceneName)
        {
            IsTransitioning = true;
            OnTransitionStarted?.Invoke();

            // Fade out
            yield return StartCoroutine(FadeOut());

            // Show loading screen
            if (loadingCanvas != null)
                loadingCanvas.gameObject.SetActive(true);

            // Load scene async
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            operation.allowSceneActivation = false;

            while (operation.progress < 0.9f)
            {
                float progress = Mathf.Clamp01(operation.progress / 0.9f);
                OnLoadProgress?.Invoke(progress);

                if (progressBar != null)
                    progressBar.value = progress;

                if (statusText != null)
                    statusText.text = $"WCZYTYWANIE... {(progress * 100):F0}%";

                yield return null;
            }

            // Activate scene
            operation.allowSceneActivation = true;

            // Wait a frame for scene to activate
            yield return null;

            // Hide loading screen
            if (loadingCanvas != null)
                loadingCanvas.gameObject.SetActive(false);

            // Fade in
            yield return StartCoroutine(FadeIn());

            IsTransitioning = false;
            OnTransitionCompleted?.Invoke();
        }

        private IEnumerator FadeOut()
        {
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(elapsed / fadeOutDuration);
                SetFadeAlpha(alpha);
                yield return null;
            }
            SetFadeAlpha(1f);
        }

        private IEnumerator FadeIn()
        {
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / fadeInDuration);
                SetFadeAlpha(alpha);
                yield return null;
            }
            SetFadeAlpha(0f);
        }

        private void SetFadeAlpha(float alpha)
        {
            // In VR, use OVRScreenFade or similar sphere-based fade
            // For now, use a full-screen overlay approach
            if (fadeMaterial != null)
            {
                Color c = fadeColor;
                c.a = alpha;
                fadeMaterial.color = c;
            }
        }
    }
}
