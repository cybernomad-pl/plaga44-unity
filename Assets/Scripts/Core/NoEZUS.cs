// PLAGA '44 VR - noEZUS Orbital AI System
// From IPK: "noEZUS - orbitalny konstrukt szukajacy lekarstwa na
// zeszlowieczna pandemie, przedsiewziecia potencjalnie skazanego
// na niepowodzenie, lecz AI nie jest zdolne do przerwania cyklu."
//
// From SPARK: "Orbital AI overlord monitoring survivors.
// Controls access to technology and resources."
//
// noEZUS is NOT an antagonist. It is a research apparatus without
// ethical intent, unable to stop its procedure. The player delivers
// data, doesn't save the world. Death ends the trial, not the story.
//
// Aesthetic: medical protocols, not heroic cutscenes.
// "Gracz umiera jak zwierze laboratoryjne - cicho, udokumentowane,
// bez znaczenia."

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.Core
{
    using Plaga44.Physiology;

    /// <summary>
    /// noEZUS orbital AI system. Manages specimen lifecycle:
    /// deployment, monitoring, death documentation, redeployment.
    /// Operates from BARKA (orbital respawn hub).
    /// </summary>
    public class NoEZUS : MonoBehaviour
    {
        [Header("Experiment State")]
        [SerializeField] private int specimenNumber = 1;
        [SerializeField] private float experimentElapsedHours = 0f;
        [SerializeField] private int totalSpecimensDeployed = 0;
        [SerializeField] private int totalDataPointsCollected = 0;

        [Header("Current Specimen")]
        [SerializeField] private SpecimenRecord currentSpecimen;

        [Header("References")]
        [SerializeField] private PhysiologyController physiologyController;

        // Death report archive
        private List<DeathReport> deathArchive = new List<DeathReport>();

        // Events
        public event Action<DeathReport> OnSpecimenTerminated;
        public event Action<SpecimenRecord> OnSpecimenDeployed;
        public event Action<string> OnNoEZUSMessage;

        /// <summary>
        /// All death reports from this session.
        /// Displayed on BARKA orbital hub between deployments.
        /// </summary>
        public IReadOnlyList<DeathReport> DeathArchive => deathArchive.AsReadOnly();

        private void Start()
        {
            if (physiologyController != null)
            {
                physiologyController.OnPlayerDeath += HandleSpecimenDeath;
                physiologyController.OnStateChanged += MonitorSpecimen;
            }

            DeployNewSpecimen();
        }

        private void OnDestroy()
        {
            if (physiologyController != null)
            {
                physiologyController.OnPlayerDeath -= HandleSpecimenDeath;
                physiologyController.OnStateChanged -= MonitorSpecimen;
            }
        }

        /// <summary>
        /// Deploy a new specimen to the surface.
        /// Called at game start and after orbital respawn.
        /// </summary>
        public void DeployNewSpecimen()
        {
            specimenNumber++;
            totalSpecimensDeployed++;

            currentSpecimen = new SpecimenRecord
            {
                specimenId = $"PLG44-{specimenNumber:D4}",
                deploymentTimestamp = Time.time,
                deploymentGameHour = experimentElapsedHours,
                generation = totalSpecimensDeployed,
                notes = "Standard deployment. Monitoring initiated."
            };

            OnSpecimenDeployed?.Invoke(currentSpecimen);
            OnNoEZUSMessage?.Invoke(
                $"[noEZUS] SPECIMEN {currentSpecimen.specimenId} DEPLOYED.\n" +
                $"Generation: {currentSpecimen.generation}\n" +
                $"Experiment hour: {experimentElapsedHours:F0}\n" +
                $"Monitoring: ACTIVE\n" +
                $"Objective: DATA COLLECTION\n" +
                $"Note: Subject is unaware of experimental parameters."
            );
        }

        /// <summary>
        /// Continuous monitoring of specimen vitals.
        /// Generates periodic status updates in medical protocol format.
        /// </summary>
        private void MonitorSpecimen(PhysiologyState state)
        {
            experimentElapsedHours += Time.deltaTime * 0.01f; // Track total experiment time
            totalDataPointsCollected++;

            // Record specimen data at intervals
            if (currentSpecimen != null)
            {
                currentSpecimen.peakStress = Mathf.Max(currentSpecimen.peakStress, state.stressLevel);
                currentSpecimen.minCoreTemp = Mathf.Min(currentSpecimen.minCoreTemp, state.coreTemperature);
                currentSpecimen.maxCoreTemp = Mathf.Max(currentSpecimen.maxCoreTemp, state.coreTemperature);
                currentSpecimen.minHydration = Mathf.Min(currentSpecimen.minHydration, state.hydration);
                currentSpecimen.maxToxinLevel = Mathf.Max(currentSpecimen.maxToxinLevel, state.toxinLevel);
                currentSpecimen.totalWoundsReceived += state.activeWounds > currentSpecimen.lastKnownWounds ? 1 : 0;
                currentSpecimen.lastKnownWounds = state.activeWounds;
            }
        }

        /// <summary>
        /// Handle specimen death. Generate death report in medical protocol format.
        /// From IPK: "Smierc generuje raport: przyczyna zgonu, czas przezycia, zebrane dane."
        /// </summary>
        private void HandleSpecimenDeath(string cause)
        {
            if (currentSpecimen == null) return;

            float survivalDuration = Time.time - currentSpecimen.deploymentTimestamp;
            var state = physiologyController.State;

            var report = new DeathReport
            {
                specimenId = currentSpecimen.specimenId,
                generation = currentSpecimen.generation,
                causeOfDeath = cause,
                survivalDurationSeconds = survivalDuration,
                survivalDurationGameHours = state.daysSurvived * 24f,

                // Terminal vitals
                terminalCoreTemperature = state.coreTemperature,
                terminalHydration = state.hydration,
                terminalCaloricReserve = state.caloricReserve,
                terminalBloodVolume = state.bloodVolume,
                terminalOxygenSaturation = state.oxygenSaturation,
                terminalToxinLevel = state.toxinLevel,
                terminalActiveToxin = state.activeToxin,

                // Session statistics
                peakStress = currentSpecimen.peakStress,
                minCoreTemp = currentSpecimen.minCoreTemp,
                maxCoreTemp = currentSpecimen.maxCoreTemp,
                totalWoundsReceived = currentSpecimen.totalWoundsReceived,
                dataPointsCollected = totalDataPointsCollected,

                // Timestamp
                experimentHour = experimentElapsedHours,
                timestamp = DateTime.UtcNow
            };

            deathArchive.Add(report);
            OnSpecimenTerminated?.Invoke(report);

            // Generate the clinical, detached noEZUS death report
            // "Brak muzyki heroicznej, brak slow-motion."
            string reportText = GenerateDeathReportText(report);
            OnNoEZUSMessage?.Invoke(reportText);

            Debug.Log(reportText);
        }

        /// <summary>
        /// Generate clinical death report text in noEZUS medical protocol format.
        /// "Estetyka protokolow medycznych zamiast patetycznych cutscenes.
        /// Interfejs noEZUS wyswietla parametry fizjologiczne: tetno, saturacja,
        /// poziom weglowodanow."
        /// </summary>
        private string GenerateDeathReportText(DeathReport report)
        {
            return
                $"=========================================\n" +
                $"  noEZUS ORBITAL RESEARCH PLATFORM\n" +
                $"  SPECIMEN TERMINATION REPORT\n" +
                $"=========================================\n" +
                $"\n" +
                $"SPECIMEN ID:      {report.specimenId}\n" +
                $"GENERATION:       {report.generation}\n" +
                $"EXPERIMENT HOUR:  {report.experimentHour:F0}\n" +
                $"\n" +
                $"--- OUTCOME ---\n" +
                $"STATUS:           TERMINATED\n" +
                $"CAUSE:            {report.causeOfDeath}\n" +
                $"SURVIVAL TIME:    {report.survivalDurationGameHours:F1}h " +
                $"({report.survivalDurationSeconds:F0}s real)\n" +
                $"DATA COLLECTED:   {report.dataPointsCollected} points\n" +
                $"\n" +
                $"--- TERMINAL VITALS ---\n" +
                $"CORE TEMP:        {report.terminalCoreTemperature:F1}C\n" +
                $"HYDRATION:        {report.terminalHydration * 100:F0}%\n" +
                $"CALORIC RESERVE:  {report.terminalCaloricReserve:F0} kcal\n" +
                $"BLOOD VOLUME:     {report.terminalBloodVolume * 100:F0}%\n" +
                $"O2 SATURATION:    {report.terminalOxygenSaturation:F0}%\n" +
                $"TOXIN LEVEL:      {report.terminalToxinLevel * 100:F0}%\n" +
                (report.terminalActiveToxin != ToxinType.None ?
                $"TOXIN TYPE:       {report.terminalActiveToxin}\n" : "") +
                $"\n" +
                $"--- SESSION STATISTICS ---\n" +
                $"PEAK STRESS:      {report.peakStress * 100:F0}%\n" +
                $"TEMP RANGE:       {report.minCoreTemp:F1}C - {report.maxCoreTemp:F1}C\n" +
                $"WOUNDS RECEIVED:  {report.totalWoundsReceived}\n" +
                $"\n" +
                $"--- CONCLUSION ---\n" +
                $"Specimen data archived. Experiment continues.\n" +
                $"Preparing next deployment.\n" +
                $"=========================================\n";
        }
    }

    // ===== DATA STRUCTS =====

    [Serializable]
    public class SpecimenRecord
    {
        public string specimenId;
        public float deploymentTimestamp;
        public float deploymentGameHour;
        public int generation;
        public string notes;

        // Running statistics
        public float peakStress = 0f;
        public float minCoreTemp = 36.6f;
        public float maxCoreTemp = 36.6f;
        public float minHydration = 1f;
        public float maxToxinLevel = 0f;
        public int totalWoundsReceived = 0;
        public int lastKnownWounds = 0;
    }

    [Serializable]
    public class DeathReport
    {
        public string specimenId;
        public int generation;
        public string causeOfDeath;
        public float survivalDurationSeconds;
        public float survivalDurationGameHours;

        // Terminal vitals
        public float terminalCoreTemperature;
        public float terminalHydration;
        public float terminalCaloricReserve;
        public float terminalBloodVolume;
        public float terminalOxygenSaturation;
        public float terminalToxinLevel;
        public ToxinType terminalActiveToxin;

        // Session statistics
        public float peakStress;
        public float minCoreTemp;
        public float maxCoreTemp;
        public int totalWoundsReceived;
        public int dataPointsCollected;

        // Metadata
        public float experimentHour;
        public DateTime timestamp;
    }
}
