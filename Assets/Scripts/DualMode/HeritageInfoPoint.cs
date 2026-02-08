// PLAGA '44 - Heritage Info Point
// Interactive info points for Mode A (Edu-Tourist) with heritage context.
// Part of issue #23: Unity VR project structure and dual-mode scene architecture

using UnityEngine;

namespace Plaga44.DualMode
{
    /// <summary>
    /// Interactive information point for Mode A (Edu-Tourist).
    /// Displays heritage context about Szlak Orlich Gniazd (Trail of Eagles' Nests),
    /// Jura Krakowsko-Czestochowska geology, Olsztyn castle history.
    ///
    /// Only active in Mode A. In Mode B these are invisible.
    /// </summary>
    public class HeritageInfoPoint : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private string titlePL;         // Polish title
        [SerializeField] private string titleEN;         // English title
        [SerializeField] [TextArea(3, 10)]
        private string descriptionPL;                     // Polish description
        [SerializeField] [TextArea(3, 10)]
        private string descriptionEN;                     // English description

        [Header("Category")]
        [SerializeField] private HeritageCategory category;

        [Header("Interaction")]
        [SerializeField] private float interactionRadius = 3f;
        [SerializeField] private GameObject infoPanel;    // World-space UI panel
        [SerializeField] private GameObject highlightEffect;

        [Header("Audio")]
        [SerializeField] private AudioClip narrationClip;
        [SerializeField] private AudioClip interactSound;

        private bool isPlayerNear = false;
        private bool isDisplaying = false;
        private AudioSource audioSource;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
                audioSource.minDistance = 1f;
                audioSource.maxDistance = interactionRadius * 2f;
            }

            if (infoPanel != null)
                infoPanel.SetActive(false);
            if (highlightEffect != null)
                highlightEffect.SetActive(false);
        }

        private void Update()
        {
            // Check if Mode A is active
            if (DualModeController.Instance != null &&
                !DualModeController.Instance.IsFeatureEnabled("InfoPoints"))
            {
                HideInfo();
                return;
            }

            // Simple proximity check (replace with VR pointer in production)
            CheckPlayerProximity();
        }

        private void CheckPlayerProximity()
        {
            Transform player = Camera.main?.transform;
            if (player == null) return;

            float distance = Vector3.Distance(transform.position, player.position);
            bool wasNear = isPlayerNear;
            isPlayerNear = distance <= interactionRadius;

            if (isPlayerNear && !wasNear)
            {
                OnPlayerEnter();
            }
            else if (!isPlayerNear && wasNear)
            {
                OnPlayerExit();
            }
        }

        private void OnPlayerEnter()
        {
            if (highlightEffect != null)
                highlightEffect.SetActive(true);

            if (interactSound != null)
                audioSource.PlayOneShot(interactSound, 0.5f);
        }

        private void OnPlayerExit()
        {
            if (highlightEffect != null)
                highlightEffect.SetActive(false);

            HideInfo();
        }

        /// <summary>
        /// Show the info panel (called by VR interaction system).
        /// </summary>
        public void ShowInfo()
        {
            if (!isPlayerNear) return;

            isDisplaying = true;

            if (infoPanel != null)
                infoPanel.SetActive(true);

            if (narrationClip != null)
                audioSource.PlayOneShot(narrationClip);
        }

        /// <summary>
        /// Hide the info panel.
        /// </summary>
        public void HideInfo()
        {
            if (!isDisplaying) return;

            isDisplaying = false;

            if (infoPanel != null)
                infoPanel.SetActive(false);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }

    /// <summary>
    /// Categories of heritage information available at info points.
    /// Based on Jura Krakowsko-Czestochowska and WWII context.
    /// </summary>
    public enum HeritageCategory
    {
        Geology,            // Jura KCz limestone formations, caves, karst
        History,            // Olsztyn castle, WWII context, regional history
        Nature,             // Flora, fauna, seasonal changes
        TrailOfEaglesNests, // Szlak Orlich Gniazd - castle trail
        Archaeology,        // Ruins, historical artifacts
        Survival            // Survival knowledge contextualized for heritage
    }
}
