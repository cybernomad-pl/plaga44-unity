// PLAGA '44 - Save System
// JSON serialization of game state to persistent storage
// CYBERNOMAD 2024-2026

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Plaga44.SaveSystem
{
    /// <summary>
    /// Handles reading/writing save files to disk using JSON serialization.
    /// Save files are stored in Application.persistentDataPath/Saves/.
    /// </summary>
    public static class SaveSystem
    {
        private const string SAVE_DIRECTORY = "Saves";
        private const string SAVE_EXTENSION = ".plaga44";
        private const string AUTOSAVE_PREFIX = "autosave_";
        private const string QUICKSAVE_NAME = "quicksave";
        private const int MAX_AUTOSAVES = 3;

        public static event Action<string> OnSaveCompleted;
        public static event Action<string> OnLoadCompleted;
        public static event Action<string> OnSaveError;
        public static event Action<string> OnLoadError;

        /// <summary>
        /// Returns the full path to the save directory.
        /// </summary>
        public static string GetSaveDirectoryPath()
        {
            string path = Path.Combine(Application.persistentDataPath, SAVE_DIRECTORY);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        /// <summary>
        /// Saves game state to a named save file.
        /// </summary>
        public static bool Save(SaveData data, string fileName)
        {
            try
            {
                if (data == null)
                {
                    Debug.LogError("[SaveSystem] SaveData is null.");
                    OnSaveError?.Invoke("SaveData is null");
                    return false;
                }

                data.timestamp = DateTime.UtcNow.ToString("o");
                if (string.IsNullOrEmpty(data.saveName))
                {
                    data.GenerateDefaultName();
                }

                string json = JsonUtility.ToJson(data, prettyPrint: true);
                string filePath = GetSaveFilePath(fileName);

                File.WriteAllText(filePath, json);

                // Also write metadata sidecar for fast listing
                SaveMetadata meta = SaveMetadata.FromSaveData(data);
                string metaJson = JsonUtility.ToJson(meta, prettyPrint: false);
                string metaPath = filePath + ".meta";
                File.WriteAllText(metaPath, metaJson);

                Debug.Log($"[SaveSystem] Game saved to: {filePath}");
                OnSaveCompleted?.Invoke(fileName);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Save failed: {ex.Message}");
                OnSaveError?.Invoke(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Loads game state from a named save file.
        /// </summary>
        public static SaveData Load(string fileName)
        {
            try
            {
                string filePath = GetSaveFilePath(fileName);

                if (!File.Exists(filePath))
                {
                    Debug.LogError($"[SaveSystem] Save file not found: {filePath}");
                    OnLoadError?.Invoke($"File not found: {fileName}");
                    return null;
                }

                string json = File.ReadAllText(filePath);
                SaveData data = JsonUtility.FromJson<SaveData>(json);

                if (data == null)
                {
                    Debug.LogError("[SaveSystem] Failed to deserialize save data.");
                    OnLoadError?.Invoke("Corrupt save file");
                    return null;
                }

                Debug.Log($"[SaveSystem] Game loaded from: {filePath}");
                OnLoadCompleted?.Invoke(fileName);
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Load failed: {ex.Message}");
                OnLoadError?.Invoke(ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Quick save to a dedicated slot.
        /// </summary>
        public static bool QuickSave(SaveData data)
        {
            data.saveName = "Quick Save";
            return Save(data, QUICKSAVE_NAME);
        }

        /// <summary>
        /// Quick load from the dedicated quick save slot.
        /// </summary>
        public static SaveData QuickLoad()
        {
            return Load(QUICKSAVE_NAME);
        }

        /// <summary>
        /// Autosave with rotating slots (keeps MAX_AUTOSAVES most recent).
        /// </summary>
        public static bool AutoSave(SaveData data)
        {
            data.saveName = $"Autosave - Day {data.environment.dayNumber}";

            // Rotate autosave slots
            string[] existingAutosaves = GetAutosaveFiles();
            if (existingAutosaves.Length >= MAX_AUTOSAVES)
            {
                // Delete the oldest
                var oldest = existingAutosaves
                    .OrderBy(f => File.GetLastWriteTimeUtc(f))
                    .First();
                DeleteSaveFile(Path.GetFileNameWithoutExtension(oldest));
            }

            string autosaveName = $"{AUTOSAVE_PREFIX}{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            return Save(data, autosaveName);
        }

        /// <summary>
        /// Returns metadata for all save files, sorted newest first.
        /// </summary>
        public static List<SaveMetadata> GetAllSaveMetadata()
        {
            var metadataList = new List<SaveMetadata>();
            string saveDir = GetSaveDirectoryPath();
            string[] metaFiles = Directory.GetFiles(saveDir, $"*{SAVE_EXTENSION}.meta");

            foreach (string metaFile in metaFiles)
            {
                try
                {
                    string json = File.ReadAllText(metaFile);
                    SaveMetadata meta = JsonUtility.FromJson<SaveMetadata>(json);
                    if (meta != null)
                    {
                        metadataList.Add(meta);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SaveSystem] Could not read metadata: {metaFile} - {ex.Message}");
                }
            }

            return metadataList
                .OrderByDescending(m => m.timestamp)
                .ToList();
        }

        /// <summary>
        /// Deletes a save file and its metadata sidecar.
        /// </summary>
        public static bool DeleteSaveFile(string fileName)
        {
            try
            {
                string filePath = GetSaveFilePath(fileName);
                string metaPath = filePath + ".meta";

                if (File.Exists(filePath))
                    File.Delete(filePath);
                if (File.Exists(metaPath))
                    File.Delete(metaPath);

                Debug.Log($"[SaveSystem] Deleted save: {fileName}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Delete failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if any save files exist (for "Continue" button in main menu).
        /// </summary>
        public static bool HasAnySaves()
        {
            string saveDir = GetSaveDirectoryPath();
            return Directory.GetFiles(saveDir, $"*{SAVE_EXTENSION}").Length > 0;
        }

        /// <summary>
        /// Checks if a quick save exists.
        /// </summary>
        public static bool HasQuickSave()
        {
            return File.Exists(GetSaveFilePath(QUICKSAVE_NAME));
        }

        private static string GetSaveFilePath(string fileName)
        {
            return Path.Combine(GetSaveDirectoryPath(), fileName + SAVE_EXTENSION);
        }

        private static string[] GetAutosaveFiles()
        {
            string saveDir = GetSaveDirectoryPath();
            return Directory.GetFiles(saveDir, $"{AUTOSAVE_PREFIX}*{SAVE_EXTENSION}");
        }
    }
}
