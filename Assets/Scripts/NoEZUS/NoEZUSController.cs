// PLAGA '44 - noEZUS AI Companion Controller
// Orbital AI overlord monitoring survivors, generating death reports.
// Part of issue #23: Unity VR project structure and dual-mode scene architecture

using UnityEngine;
using System.Collections.Generic;

namespace Plaga44.NoEZUS
{
    /// <summary>
    /// Controls the noEZUS orbital AI companion system.
    ///
    /// From IPK grant:
    /// - noEZUS is an orbital AI overlord monitoring survivors
    /// - Generates death reports: cause of death, survival time, collected data
    /// - Medical protocol aesthetic for UI (not heroic cutscenes)
    /// - Player treated as "disposable sample" in narrative frame
    ///
    /// Displayed in BARKA hub scene:
    /// - noEZUS terminal interface (data readouts, satellite status)
    /// - Death report review from previous sessions
    /// - Equipment loadout selection for next drop
    /// - Survivor statistics and rankings
    /// </summary>
    public class NoEZUSController : MonoBehaviour
    {
        public static NoEZUSController Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private Canvas noEZUSTerminal;
        [SerializeField] private UnityEngine.UI.Text reportText;
        [SerializeField] private UnityEngine.UI.Text statusText;
        [SerializeField] private UnityEngine.UI.Text sampleIdText;

        [Header("Audio")]
        [SerializeField] private AudioClip terminalBootSound;
        [SerializeField] private AudioClip dataTransmitSound;
        [SerializeField] private AudioClip alertSound;

        [Header("Satellite Telemetry")]
        [SerializeField] private float agcBaseline = -42f;
        [SerializeField] private float agcVariance = 5f;

        private AudioSource audioSource;
        private List<DeathReport> deathHistory = new List<DeathReport>();
        private string currentSampleId;
        private int sessionNumber = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            GenerateSampleId();
        }

        /// <summary>
        /// Generate a new sample ID for the current session.
        /// Format: BRK-VII-XXXX (matching the landing page satellite UI).
        /// </summary>
        private void GenerateSampleId()
        {
            sessionNumber++;
            currentSampleId = $"BRK-VII-{sessionNumber:D4}";

            if (sampleIdText != null)
                sampleIdText.text = currentSampleId;
        }

        /// <summary>
        /// Generate a death report from the current session data.
        /// Called when player dies in Mode B (Hardcore Survival).
        /// </summary>
        public DeathReport GenerateDeathReport(string causeOfDeath, float survivalTime,
            Vector3 deathPosition, Dictionary<string, object> sessionData)
        {
            var report = new DeathReport
            {
                SampleId = currentSampleId,
                SessionNumber = sessionNumber,
                Timestamp = System.DateTime.UtcNow,
                CauseOfDeath = causeOfDeath,
                SurvivalTimeSeconds = survivalTime,
                DeathPosition = deathPosition,
                SessionData = sessionData ?? new Dictionary<string, object>()
            };

            // Classify death cause using medical protocol terminology
            report.MedicalClassification = ClassifyCauseOfDeath(causeOfDeath);

            deathHistory.Add(report);

            if (audioSource != null && dataTransmitSound != null)
                audioSource.PlayOneShot(dataTransmitSound);

            return report;
        }

        /// <summary>
        /// Display death report on the noEZUS terminal.
        /// Medical/clinical aesthetic - not dramatic.
        /// </summary>
        public void DisplayDeathReport(DeathReport report)
        {
            if (reportText == null) return;

            string timeFormatted = FormatSurvivalTime(report.SurvivalTimeSeconds);

            reportText.text =
                $"=== noEZUS v.2144 RAPORT ===\n" +
                $"PROBKA: {report.SampleId}\n" +
                $"SESJA: {report.SessionNumber}\n" +
                $"---\n" +
                $"PRZYCZYNA ZGONU:\n  {report.MedicalClassification}\n" +
                $"CZAS PRZEZYCIA: {timeFormatted}\n" +
                $"POZYCJA: [{report.DeathPosition.x:F1}, {report.DeathPosition.z:F1}]\n" +
                $"---\n" +
                $"STATUS: PROBKA UTRACONA\n" +
                $"NASTEPNA PROBKA: GOTOWA\n" +
                $"=== KONIEC TRANSMISJI ===";
        }

        /// <summary>
        /// Boot the terminal (play startup sequence).
        /// </summary>
        public void BootTerminal()
        {
            if (noEZUSTerminal != null)
                noEZUSTerminal.gameObject.SetActive(true);

            if (audioSource != null && terminalBootSound != null)
                audioSource.PlayOneShot(terminalBootSound);

            UpdateStatusDisplay();
            GenerateSampleId();
        }

        /// <summary>
        /// Update the satellite telemetry status display.
        /// Matches the landing page satellite UI (AGC, TRK, AOS, LOCK LEDs).
        /// </summary>
        public void UpdateStatusDisplay()
        {
            if (statusText == null) return;

            float agc = agcBaseline + Random.Range(-agcVariance, agcVariance);
            string trkStatus = Random.value > 0.3f ? "OK" : "--";
            string aosStatus = Random.value > 0.6f ? "OK" : "--";
            string lockStatus = Random.value > 0.2f ? "LOCK" : "SRCH";

            statusText.text =
                $"BRK-VII | AGC {agc:F1}dB\n" +
                $"TRK [{trkStatus}] AOS [{aosStatus}] {lockStatus}";
        }

        /// <summary>
        /// Get death history for statistics display.
        /// </summary>
        public List<DeathReport> GetDeathHistory()
        {
            return new List<DeathReport>(deathHistory);
        }

        private string ClassifyCauseOfDeath(string cause)
        {
            // Medical protocol classification
            // Based on scenario death causes
            string lowerCause = cause.ToLower();

            if (lowerCause.Contains("hypothermia") || lowerCause.Contains("cold") || lowerCause.Contains("frozen"))
                return "MORS-FRIGORE: Wychlodzenie organizmu";
            if (lowerCause.Contains("dehydration") || lowerCause.Contains("heatstroke"))
                return "MORS-CALORE: Udar cieplny / odwodnienie";
            if (lowerCause.Contains("starvation") || lowerCause.Contains("hunger"))
                return "MORS-FAME: Smierc glodowa";
            if (lowerCause.Contains("blood") || lowerCause.Contains("wound") || lowerCause.Contains("injury"))
                return "MORS-VULNERE: Wykrwawienie z ran";
            if (lowerCause.Contains("poison") || lowerCause.Contains("mushroom") || lowerCause.Contains("water"))
                return "MORS-VENENO: Zatrucie (grzyby/woda)";
            if (lowerCause.Contains("combat") || lowerCause.Contains("shot") || lowerCause.Contains("military"))
                return "MORS-BELLO: Smierc w walce";
            if (lowerCause.Contains("fall") || lowerCause.Contains("terrain"))
                return "MORS-CASU: Upadek / wypadek terenowy";
            if (lowerCause.Contains("animal") || lowerCause.Contains("rabid") || lowerCause.Contains("boar"))
                return "MORS-BESTIA: Atak zwierzat";

            return $"MORS-CEREBRI: {cause}";
        }

        private string FormatSurvivalTime(float seconds)
        {
            int hours = Mathf.FloorToInt(seconds / 3600);
            int minutes = Mathf.FloorToInt((seconds % 3600) / 60);
            int secs = Mathf.FloorToInt(seconds % 60);
            return $"{hours:D2}:{minutes:D2}:{secs:D2}";
        }
    }

    /// <summary>
    /// Data structure for noEZUS death reports.
    /// Serializable for save/load and network transmission.
    /// </summary>
    [System.Serializable]
    public class DeathReport
    {
        public string SampleId;
        public int SessionNumber;
        public System.DateTime Timestamp;
        public string CauseOfDeath;
        public string MedicalClassification;
        public float SurvivalTimeSeconds;
        public Vector3 DeathPosition;
        public Dictionary<string, object> SessionData;
    }
}
