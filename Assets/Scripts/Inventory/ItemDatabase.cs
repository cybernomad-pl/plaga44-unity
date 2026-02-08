using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Plaga44.Inventory
{
    // -------------------------------------------------------
    // Enums
    // -------------------------------------------------------

    public enum ItemCategory
    {
        Weapon,
        Food,
        Medical,
        Tool,
        Clothing,
        FireMaking,
        CraftingMaterial,
        Backpack,
        Ammunition
    }

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Military
    }

    public enum EquipmentVariant
    {
        None,
        Civilian,
        Military
    }

    public enum WeaponType
    {
        None,
        Melee,
        Ranged,
        Thrown,
        Trap,
        Improvised
    }

    public enum FoodState
    {
        None,
        Raw,
        Cooked,
        Canned,
        Dried,
        Foraged
    }

    // -------------------------------------------------------
    // Item data class (mirrors JSON structure)
    // -------------------------------------------------------

    [Serializable]
    public class ItemData
    {
        public string id;
        public string displayName;
        public string description;
        public ItemCategory category;
        public ItemRarity rarity;
        public EquipmentVariant variant;
        public float weight;           // kg
        public float volume;           // litres
        public int maxStack;
        public int durability;         // 0 = indestructible
        public string iconPath;

        // --- weapon-specific ---
        public WeaponType weaponType;
        public float damage;
        public float attackSpeed;
        public float range;

        // --- food-specific ---
        public FoodState foodState;
        public float nutritionValue;   // hunger restored (0-100)
        public float hydrationValue;   // thirst restored (0-100)
        public float cookTimeSeconds;
        public bool isPoisonous;

        // --- medical-specific ---
        public float healAmount;
        public float useTimeSeconds;

        // --- clothing / backpack ---
        public float armorValue;
        public float capacityLitres;   // for backpacks

        // --- fire-making ---
        public float burnDurationSeconds;

        // --- tags for flexible querying ---
        public List<string> tags;

        public ItemData()
        {
            tags = new List<string>();
            maxStack = 1;
        }
    }

    // -------------------------------------------------------
    // JSON wrapper for array deserialization
    // -------------------------------------------------------

    [Serializable]
    public class ItemConfigWrapper
    {
        public List<ItemData> items;
    }

    // -------------------------------------------------------
    // ItemDatabase ScriptableObject / MonoBehaviour singleton
    // -------------------------------------------------------

    /// <summary>
    /// Central registry that loads items from ItemConfig.json and provides
    /// fast look-ups by id, category, tag, and rarity.
    /// </summary>
    public class ItemDatabase : MonoBehaviour
    {
        public static ItemDatabase Instance { get; private set; }

        [Header("Data Source")]
        [Tooltip("TextAsset pointing to Assets/Data/ItemConfig.json")]
        public TextAsset itemConfigAsset;

        private Dictionary<string, ItemData> _itemsById;
        private Dictionary<ItemCategory, List<ItemData>> _itemsByCategory;

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

            LoadItems();
        }

        // -------------------------------------------------------
        // Loading
        // -------------------------------------------------------

        public void LoadItems()
        {
            _itemsById = new Dictionary<string, ItemData>();
            _itemsByCategory = new Dictionary<ItemCategory, List<ItemData>>();

            if (itemConfigAsset == null)
            {
                Debug.LogError("[ItemDatabase] itemConfigAsset is not assigned.");
                return;
            }

            ItemConfigWrapper wrapper =
                JsonUtility.FromJson<ItemConfigWrapper>(itemConfigAsset.text);

            if (wrapper == null || wrapper.items == null)
            {
                Debug.LogError("[ItemDatabase] Failed to parse ItemConfig.json.");
                return;
            }

            foreach (ItemData item in wrapper.items)
            {
                if (string.IsNullOrEmpty(item.id))
                {
                    Debug.LogWarning("[ItemDatabase] Skipping item with empty id.");
                    continue;
                }

                if (_itemsById.ContainsKey(item.id))
                {
                    Debug.LogWarning($"[ItemDatabase] Duplicate item id: {item.id}");
                    continue;
                }

                _itemsById[item.id] = item;

                if (!_itemsByCategory.ContainsKey(item.category))
                    _itemsByCategory[item.category] = new List<ItemData>();

                _itemsByCategory[item.category].Add(item);
            }

            Debug.Log($"[ItemDatabase] Loaded {_itemsById.Count} items.");
        }

        // -------------------------------------------------------
        // Public look-ups
        // -------------------------------------------------------

        /// <summary>Returns the item data for the given id, or null.</summary>
        public ItemData GetItem(string id)
        {
            if (_itemsById != null && _itemsById.TryGetValue(id, out ItemData data))
                return data;
            return null;
        }

        /// <summary>Returns all items in a given category.</summary>
        public List<ItemData> GetItemsByCategory(ItemCategory category)
        {
            if (_itemsByCategory != null && _itemsByCategory.TryGetValue(category, out List<ItemData> list))
                return list;
            return new List<ItemData>();
        }

        /// <summary>Returns all items that carry the given tag.</summary>
        public List<ItemData> GetItemsByTag(string tag)
        {
            if (_itemsById == null) return new List<ItemData>();
            return _itemsById.Values
                .Where(i => i.tags != null && i.tags.Contains(tag))
                .ToList();
        }

        /// <summary>Returns all items matching a rarity level.</summary>
        public List<ItemData> GetItemsByRarity(ItemRarity rarity)
        {
            if (_itemsById == null) return new List<ItemData>();
            return _itemsById.Values.Where(i => i.rarity == rarity).ToList();
        }

        /// <summary>Returns all items whose variant matches (military / civilian).</summary>
        public List<ItemData> GetItemsByVariant(EquipmentVariant variant)
        {
            if (_itemsById == null) return new List<ItemData>();
            return _itemsById.Values.Where(i => i.variant == variant).ToList();
        }

        /// <summary>Returns every registered item id.</summary>
        public List<string> GetAllItemIds()
        {
            if (_itemsById == null) return new List<string>();
            return _itemsById.Keys.ToList();
        }

        /// <summary>Returns total number of registered items.</summary>
        public int Count => _itemsById?.Count ?? 0;
    }
}
