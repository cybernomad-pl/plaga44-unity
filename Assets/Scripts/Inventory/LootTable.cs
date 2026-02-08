using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Plaga44.Inventory
{
    // -------------------------------------------------------
    // Location types matching PLAGA '44 scenario
    // -------------------------------------------------------

    public enum LootLocationType
    {
        Market,
        GasStation,
        Apartment,
        MilitaryBody,
        Hospital,
        School,
        Workshop,
        Forest,
        Pharmacy,
        PoliceStation,
        FireStation,
        Street
    }

    // -------------------------------------------------------
    // Loot entry
    // -------------------------------------------------------

    [Serializable]
    public class LootEntry
    {
        [Tooltip("Item id from ItemDatabase.")]
        public string itemId;

        [Tooltip("Probability of this item appearing (0..1).")]
        [Range(0f, 1f)]
        public float spawnChance;

        [Tooltip("Minimum quantity if spawned.")]
        public int minQuantity;

        [Tooltip("Maximum quantity if spawned.")]
        public int maxQuantity;

        public LootEntry()
        {
            spawnChance = 0.5f;
            minQuantity = 1;
            maxQuantity = 1;
        }

        public LootEntry(string itemId, float chance, int min = 1, int max = 1)
        {
            this.itemId = itemId;
            this.spawnChance = chance;
            this.minQuantity = min;
            this.maxQuantity = max;
        }
    }

    // -------------------------------------------------------
    // Location-specific loot table
    // -------------------------------------------------------

    [Serializable]
    public class LocationLootTable
    {
        public LootLocationType locationType;
        public string locationLabel;
        public int minTotalItems;
        public int maxTotalItems;
        public List<LootEntry> entries;

        public LocationLootTable()
        {
            entries = new List<LootEntry>();
            minTotalItems = 1;
            maxTotalItems = 5;
        }
    }

    // -------------------------------------------------------
    // LootTable MonoBehaviour
    // -------------------------------------------------------

    /// <summary>
    /// Generates randomised loot lists per location type.
    /// Tables are defined in code to match PLAGA '44 survivalist
    /// scenarios (markets, gas stations, apartments, military bodies, etc.).
    /// </summary>
    public class LootTable : MonoBehaviour
    {
        public static LootTable Instance { get; private set; }

        private Dictionary<LootLocationType, LocationLootTable> _tables;

        // -------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildTables();
        }

        // -------------------------------------------------------
        // Table definitions
        // -------------------------------------------------------

        private void BuildTables()
        {
            _tables = new Dictionary<LootLocationType, LocationLootTable>();

            // ----- Market / Grocery store -----
            _tables[LootLocationType.Market] = new LocationLootTable
            {
                locationType = LootLocationType.Market,
                locationLabel = "Market / Grocery Store",
                minTotalItems = 2,
                maxTotalItems = 8,
                entries = new List<LootEntry>
                {
                    new LootEntry("canned_food",              0.70f, 1, 3),
                    new LootEntry("instant_noodles",          0.60f, 1, 4),
                    new LootEntry("bottled_water",            0.50f, 1, 2),
                    new LootEntry("plastic_bag",              0.40f, 1, 3),
                    new LootEntry("matches",                  0.30f, 1, 2),
                    new LootEntry("candle",                   0.25f, 1, 2),
                    new LootEntry("spirit_burner",            0.10f, 1, 1),
                    new LootEntry("gel_firelighter",          0.15f, 1, 2),
                    new LootEntry("white_cube_firelighter",   0.15f, 1, 3),
                    new LootEntry("kitchen_knife",            0.15f, 1, 1),
                    new LootEntry("duct_tape",                0.20f, 1, 1),
                    new LootEntry("salt",                     0.20f, 1, 1),
                    new LootEntry("sugar",                    0.15f, 1, 1),
                    new LootEntry("chocolate_bar",            0.25f, 1, 2),
                    new LootEntry("energy_drink",             0.20f, 1, 1),
                }
            };

            // ----- Gas station -----
            _tables[LootLocationType.GasStation] = new LocationLootTable
            {
                locationType = LootLocationType.GasStation,
                locationLabel = "Gas Station",
                minTotalItems = 2,
                maxTotalItems = 6,
                entries = new List<LootEntry>
                {
                    new LootEntry("fuel_canister",            0.30f, 1, 1),
                    new LootEntry("motor_oil",                0.20f, 1, 1),
                    new LootEntry("duct_tape",                0.25f, 1, 1),
                    new LootEntry("matches",                  0.35f, 1, 2),
                    new LootEntry("bottled_water",            0.40f, 1, 2),
                    new LootEntry("energy_drink",             0.30f, 1, 1),
                    new LootEntry("chocolate_bar",            0.35f, 1, 2),
                    new LootEntry("canned_food",              0.25f, 1, 1),
                    new LootEntry("road_flare",               0.15f, 1, 2),
                    new LootEntry("flashlight",               0.15f, 1, 1),
                    new LootEntry("glass_bottle",             0.40f, 1, 3),
                    new LootEntry("rag",                      0.30f, 1, 2),
                    new LootEntry("instant_noodles",          0.30f, 1, 2),
                    new LootEntry("lighter",                  0.20f, 1, 1),
                }
            };

            // ----- Apartment / Residential -----
            _tables[LootLocationType.Apartment] = new LocationLootTable
            {
                locationType = LootLocationType.Apartment,
                locationLabel = "Apartment",
                minTotalItems = 1,
                maxTotalItems = 6,
                entries = new List<LootEntry>
                {
                    new LootEntry("torn_clothes",             0.60f, 1, 3),
                    new LootEntry("table_leg",                0.30f, 1, 1),
                    new LootEntry("kitchen_knife",            0.20f, 1, 1),
                    new LootEntry("candle",                   0.30f, 1, 2),
                    new LootEntry("matches",                  0.25f, 1, 1),
                    new LootEntry("canned_food",              0.20f, 1, 1),
                    new LootEntry("bottled_water",            0.15f, 1, 1),
                    new LootEntry("plexiglass_shard",         0.15f, 1, 1),
                    new LootEntry("duct_tape",                0.15f, 1, 1),
                    new LootEntry("rope",                     0.10f, 1, 1),
                    new LootEntry("blanket",                  0.20f, 1, 1),
                    new LootEntry("glass_bottle",             0.35f, 1, 2),
                    new LootEntry("nail",                     0.25f, 2, 5),
                    new LootEntry("backpack_20l",             0.08f, 1, 1),
                    new LootEntry("lighter",                  0.15f, 1, 1),
                    new LootEntry("painkillers",              0.10f, 1, 1),
                    new LootEntry("instant_noodles",          0.15f, 1, 2),
                }
            };

            // ----- Military body -----
            _tables[LootLocationType.MilitaryBody] = new LocationLootTable
            {
                locationType = LootLocationType.MilitaryBody,
                locationLabel = "Military Body",
                minTotalItems = 1,
                maxTotalItems = 4,
                entries = new List<LootEntry>
                {
                    new LootEntry("military_knife",           0.25f, 1, 1),
                    new LootEntry("military_bandage",         0.35f, 1, 2),
                    new LootEntry("military_rations",         0.30f, 1, 1),
                    new LootEntry("ammo_pistol",              0.15f, 3, 8),
                    new LootEntry("compass",                  0.10f, 1, 1),
                    new LootEntry("flashlight",               0.20f, 1, 1),
                    new LootEntry("military_canteen",         0.15f, 1, 1),
                    new LootEntry("paracord",                 0.15f, 1, 1),
                    new LootEntry("backpack_60l",             0.05f, 1, 1),
                    new LootEntry("backpack_80l",             0.02f, 1, 1),
                    new LootEntry("morphine",                 0.05f, 1, 1),
                    new LootEntry("dog_tag",                  0.40f, 1, 1),
                    new LootEntry("military_jacket",          0.10f, 1, 1),
                }
            };

            // ----- Hospital -----
            _tables[LootLocationType.Hospital] = new LocationLootTable
            {
                locationType = LootLocationType.Hospital,
                locationLabel = "Hospital",
                minTotalItems = 2,
                maxTotalItems = 7,
                entries = new List<LootEntry>
                {
                    new LootEntry("military_bandage",         0.50f, 1, 3),
                    new LootEntry("painkillers",              0.45f, 1, 2),
                    new LootEntry("morphine",                 0.10f, 1, 1),
                    new LootEntry("antiseptic",               0.35f, 1, 1),
                    new LootEntry("surgical_kit",             0.08f, 1, 1),
                    new LootEntry("torn_clothes",             0.30f, 1, 2),
                    new LootEntry("splint_medical",           0.20f, 1, 1),
                    new LootEntry("saline",                   0.15f, 1, 1),
                    new LootEntry("bottled_water",            0.20f, 1, 1),
                    new LootEntry("rubber_gloves",            0.25f, 1, 2),
                }
            };

            // ----- Forest -----
            _tables[LootLocationType.Forest] = new LocationLootTable
            {
                locationType = LootLocationType.Forest,
                locationLabel = "Forest",
                minTotalItems = 1,
                maxTotalItems = 5,
                entries = new List<LootEntry>
                {
                    new LootEntry("branch",                   0.70f, 1, 3),
                    new LootEntry("berries",                  0.40f, 1, 3),
                    new LootEntry("mushroom",                 0.35f, 1, 2),
                    new LootEntry("mushroom_poisonous",       0.15f, 1, 1),
                    new LootEntry("stone",                    0.50f, 1, 3),
                    new LootEntry("bark",                     0.30f, 1, 2),
                    new LootEntry("moss",                     0.25f, 1, 1),
                    new LootEntry("pine_resin",               0.15f, 1, 1),
                    new LootEntry("wild_herbs",               0.20f, 1, 2),
                }
            };

            // ----- Workshop -----
            _tables[LootLocationType.Workshop] = new LocationLootTable
            {
                locationType = LootLocationType.Workshop,
                locationLabel = "Workshop / Garage",
                minTotalItems = 2,
                maxTotalItems = 6,
                entries = new List<LootEntry>
                {
                    new LootEntry("nail",                     0.55f, 3, 8),
                    new LootEntry("duct_tape",                0.40f, 1, 2),
                    new LootEntry("rope",                     0.30f, 1, 1),
                    new LootEntry("fuel_canister",            0.15f, 1, 1),
                    new LootEntry("metal_pipe",               0.25f, 1, 1),
                    new LootEntry("hammer",                   0.20f, 1, 1),
                    new LootEntry("pliers",                   0.15f, 1, 1),
                    new LootEntry("screwdriver",              0.20f, 1, 1),
                    new LootEntry("glass_bottle",             0.25f, 1, 2),
                    new LootEntry("motor_oil",                0.15f, 1, 1),
                    new LootEntry("table_leg",                0.20f, 1, 1),
                    new LootEntry("plexiglass_shard",         0.10f, 1, 1),
                    new LootEntry("wire",                     0.30f, 1, 2),
                }
            };

            // ----- Pharmacy -----
            _tables[LootLocationType.Pharmacy] = new LocationLootTable
            {
                locationType = LootLocationType.Pharmacy,
                locationLabel = "Pharmacy",
                minTotalItems = 1,
                maxTotalItems = 5,
                entries = new List<LootEntry>
                {
                    new LootEntry("painkillers",              0.55f, 1, 2),
                    new LootEntry("antiseptic",               0.45f, 1, 1),
                    new LootEntry("military_bandage",         0.30f, 1, 2),
                    new LootEntry("vitamins",                 0.25f, 1, 1),
                    new LootEntry("antibiotics",              0.15f, 1, 1),
                    new LootEntry("rubber_gloves",            0.20f, 1, 1),
                    new LootEntry("bottled_water",            0.15f, 1, 1),
                }
            };

            // ----- Police Station -----
            _tables[LootLocationType.PoliceStation] = new LocationLootTable
            {
                locationType = LootLocationType.PoliceStation,
                locationLabel = "Police Station",
                minTotalItems = 1,
                maxTotalItems = 4,
                entries = new List<LootEntry>
                {
                    new LootEntry("ammo_pistol",              0.20f, 5, 12),
                    new LootEntry("flashlight",               0.25f, 1, 1),
                    new LootEntry("handcuffs",                0.15f, 1, 1),
                    new LootEntry("baton",                    0.15f, 1, 1),
                    new LootEntry("military_bandage",         0.20f, 1, 1),
                    new LootEntry("kevlar_vest",              0.05f, 1, 1),
                    new LootEntry("road_flare",               0.20f, 1, 2),
                    new LootEntry("radio",                    0.08f, 1, 1),
                }
            };

            // ----- Fire Station -----
            _tables[LootLocationType.FireStation] = new LocationLootTable
            {
                locationType = LootLocationType.FireStation,
                locationLabel = "Fire Station",
                minTotalItems = 1,
                maxTotalItems = 5,
                entries = new List<LootEntry>
                {
                    new LootEntry("fire_axe",                 0.15f, 1, 1),
                    new LootEntry("rope",                     0.35f, 1, 2),
                    new LootEntry("flashlight",               0.25f, 1, 1),
                    new LootEntry("military_bandage",         0.30f, 1, 2),
                    new LootEntry("bottled_water",            0.25f, 1, 2),
                    new LootEntry("crowbar",                  0.10f, 1, 1),
                    new LootEntry("backpack_60l",             0.05f, 1, 1),
                    new LootEntry("gas_mask",                 0.08f, 1, 1),
                }
            };

            // ----- Street -----
            _tables[LootLocationType.Street] = new LocationLootTable
            {
                locationType = LootLocationType.Street,
                locationLabel = "Street / Rubble",
                minTotalItems = 0,
                maxTotalItems = 3,
                entries = new List<LootEntry>
                {
                    new LootEntry("stone",                    0.50f, 1, 3),
                    new LootEntry("glass_bottle",             0.35f, 1, 2),
                    new LootEntry("torn_clothes",             0.25f, 1, 1),
                    new LootEntry("nail",                     0.20f, 1, 3),
                    new LootEntry("branch",                   0.15f, 1, 1),
                    new LootEntry("rag",                      0.25f, 1, 1),
                    new LootEntry("metal_pipe",               0.10f, 1, 1),
                    new LootEntry("plexiglass_shard",         0.10f, 1, 1),
                }
            };

            // ----- School -----
            _tables[LootLocationType.School] = new LocationLootTable
            {
                locationType = LootLocationType.School,
                locationLabel = "School",
                minTotalItems = 1,
                maxTotalItems = 5,
                entries = new List<LootEntry>
                {
                    new LootEntry("table_leg",                0.30f, 1, 2),
                    new LootEntry("torn_clothes",             0.25f, 1, 1),
                    new LootEntry("plexiglass_shard",         0.15f, 1, 1),
                    new LootEntry("candle",                   0.15f, 1, 1),
                    new LootEntry("bottled_water",            0.15f, 1, 1),
                    new LootEntry("backpack_20l",             0.10f, 1, 1),
                    new LootEntry("chalk",                    0.20f, 1, 2),
                    new LootEntry("scissors",                 0.15f, 1, 1),
                }
            };

            Debug.Log($"[LootTable] Built {_tables.Count} location loot tables.");
        }

        // -------------------------------------------------------
        // Public API
        // -------------------------------------------------------

        /// <summary>
        /// Generate a randomised loot list for the given location type.
        /// Returns a list of (itemId, quantity) pairs.
        /// </summary>
        public List<(string itemId, int quantity)> GenerateLoot(LootLocationType location)
        {
            List<(string, int)> result = new List<(string, int)>();

            if (!_tables.TryGetValue(location, out LocationLootTable table))
            {
                Debug.LogWarning($"[LootTable] No table for location '{location}'.");
                return result;
            }

            int targetCount = UnityEngine.Random.Range(table.minTotalItems, table.maxTotalItems + 1);

            // shuffle entries to avoid bias toward early items
            List<LootEntry> shuffled = table.entries.OrderBy(_ => UnityEngine.Random.value).ToList();

            foreach (LootEntry entry in shuffled)
            {
                if (result.Count >= targetCount) break;

                float roll = UnityEngine.Random.value;
                if (roll <= entry.spawnChance)
                {
                    int qty = UnityEngine.Random.Range(entry.minQuantity, entry.maxQuantity + 1);
                    result.Add((entry.itemId, qty));
                }
            }

            return result;
        }

        /// <summary>
        /// Generate loot and add directly to the player's inventory.
        /// Returns the items that were actually added (capped by weight/volume).
        /// </summary>
        public List<(string itemId, int added)> GenerateAndAddToInventory(LootLocationType location)
        {
            List<(string, int)> loot = GenerateLoot(location);
            List<(string, int)> added = new List<(string, int)>();
            InventorySystem inv = InventorySystem.Instance;

            if (inv == null)
            {
                Debug.LogWarning("[LootTable] InventorySystem not available.");
                return added;
            }

            foreach ((string itemId, int qty) in loot)
            {
                int count = inv.AddItem(itemId, qty);
                if (count > 0)
                    added.Add((itemId, count));
            }

            return added;
        }

        /// <summary>Returns the loot table for a given location, or null.</summary>
        public LocationLootTable GetTable(LootLocationType location)
        {
            _tables.TryGetValue(location, out LocationLootTable t);
            return t;
        }
    }
}
