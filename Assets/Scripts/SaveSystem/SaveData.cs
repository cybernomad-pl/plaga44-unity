// PLAGA '44 - Save Data Model
// Serializable data structures for persisting game state
// CYBERNOMAD 2024-2026

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.SaveSystem
{
    /// <summary>
    /// Root save data container. All game state that needs to persist
    /// across sessions is captured here.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public string saveVersion = "1.0.0";
        public string saveName;
        public string timestamp;
        public float playTimeSeconds;

        public PhysiologyData physiology;
        public InventoryData inventory;
        public PositionData position;
        public EnvironmentData environment;
        public ProgressData progress;

        public SaveData()
        {
            timestamp = DateTime.UtcNow.ToString("o");
            physiology = new PhysiologyData();
            inventory = new InventoryData();
            position = new PositionData();
            environment = new EnvironmentData();
            progress = new ProgressData();
        }

        /// <summary>
        /// Generates a default save name based on timestamp and location.
        /// </summary>
        public void GenerateDefaultName()
        {
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            saveName = $"PLAGA44 - {environment.currentSeason} - {dateStr}";
        }
    }

    /// <summary>
    /// Player physiology state - survival vitals tracked by the Mors Cerebri system.
    /// Values are normalized 0-100 unless otherwise noted.
    /// </summary>
    [Serializable]
    public class PhysiologyData
    {
        // Core vitals (0-100)
        public float health = 100f;
        public float hunger = 100f;       // 100 = full, 0 = starving
        public float thirst = 100f;       // 100 = hydrated, 0 = dehydrated
        public float stamina = 100f;
        public float bodyTemperature = 36.6f; // Celsius, normal human temp

        // Mors Cerebri (death brain) thresholds
        public float hypothermiaLevel = 0f;   // 0-100, triggers at body temp < 35C
        public float dehydrationLevel = 0f;   // 0-100, escalates as thirst drops
        public float starvationLevel = 0f;    // 0-100, escalates as hunger drops
        public float radiationLevel = 0f;     // 0-100, accumulated radiation exposure
        public float infectionLevel = 0f;     // 0-100, wound/disease infection
        public float bloodLoss = 0f;          // 0-100, from untreated wounds

        // Status effect flags
        public List<string> activeStatusEffects = new List<string>();

        // Cause of death (set by Mors Cerebri on player death)
        public string causeOfDeath = "";
    }

    /// <summary>
    /// Player inventory - items, weapons, resources carried.
    /// </summary>
    [Serializable]
    public class InventoryData
    {
        public List<InventoryItemData> items = new List<InventoryItemData>();
        public int maxSlots = 20;

        // Quick-access slot indices
        public int equippedWeaponIndex = -1;
        public int equippedToolIndex = -1;
    }

    [Serializable]
    public class InventoryItemData
    {
        public string itemId;
        public string itemName;
        public string category;   // weapon, tool, food, medical, material, key
        public int quantity;
        public float condition;   // 0-100, durability
        public Dictionary<string, string> metadata; // extra item-specific data

        public InventoryItemData()
        {
            metadata = new Dictionary<string, string>();
            condition = 100f;
            quantity = 1;
        }
    }

    /// <summary>
    /// Player position and orientation in the world.
    /// </summary>
    [Serializable]
    public class PositionData
    {
        public float posX;
        public float posY;
        public float posZ;
        public float rotX;
        public float rotY;
        public float rotZ;
        public float rotW;
        public string currentScene;    // Unity scene name
        public string currentZone;     // Gameplay zone identifier (e.g., "Mokotow", "Stare_Miasto")
        public bool isInShelter;

        public void SetFromTransform(Transform t)
        {
            posX = t.position.x;
            posY = t.position.y;
            posZ = t.position.z;
            rotX = t.rotation.x;
            rotY = t.rotation.y;
            rotZ = t.rotation.z;
            rotW = t.rotation.w;
        }

        public Vector3 GetPosition()
        {
            return new Vector3(posX, posY, posZ);
        }

        public Quaternion GetRotation()
        {
            return new Quaternion(rotX, rotY, rotZ, rotW);
        }
    }

    /// <summary>
    /// World environment state - time, weather, season.
    /// </summary>
    [Serializable]
    public class EnvironmentData
    {
        // Time system - Warsaw 1944
        public float timeOfDay;          // 0-24 hours (e.g., 14.5 = 2:30 PM)
        public int dayNumber = 1;        // Day since uprising start (August 1, 1944)
        public string currentSeason;     // "Lato" (summer), "Jesien" (autumn) - uprising was Aug-Oct

        // Weather
        public string currentWeather;    // clear, cloudy, rain, storm, fog
        public float ambientTemperature; // Celsius, affects body temperature

        // World state flags
        public List<string> destroyedLocations = new List<string>();
        public List<string> liberatedZones = new List<string>();
    }

    /// <summary>
    /// Game progress tracking - quests, discoveries, stats.
    /// </summary>
    [Serializable]
    public class ProgressData
    {
        public List<string> completedObjectives = new List<string>();
        public List<string> activeObjectives = new List<string>();
        public List<string> discoveredLocations = new List<string>();
        public Dictionary<string, string> storyFlags = new Dictionary<string, string>();

        // Statistics
        public int enemiesDefeated;
        public int daysUrvived;
        public int itemsCrafted;
        public float distanceTraveled;
    }

    /// <summary>
    /// Metadata for save file listing (loaded without full deserialization).
    /// </summary>
    [Serializable]
    public class SaveMetadata
    {
        public string saveName;
        public string timestamp;
        public float playTimeSeconds;
        public string currentSeason;
        public string currentZone;
        public int dayNumber;
        public float health;

        public static SaveMetadata FromSaveData(SaveData data)
        {
            return new SaveMetadata
            {
                saveName = data.saveName,
                timestamp = data.timestamp,
                playTimeSeconds = data.playTimeSeconds,
                currentSeason = data.environment.currentSeason,
                currentZone = data.position.currentZone,
                dayNumber = data.environment.dayNumber,
                health = data.physiology.health
            };
        }
    }
}
