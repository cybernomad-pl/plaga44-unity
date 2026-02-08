using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Plaga44.Inventory
{
    // -------------------------------------------------------
    // Item instance that lives inside an inventory
    // -------------------------------------------------------

    [Serializable]
    public class InventoryItem
    {
        public string itemId;
        public int quantity;
        public int currentDurability;
        public int slotIndex;

        /// <summary>Resolved reference (not serialized).</summary>
        [NonSerialized] public ItemData data;

        public float TotalWeight => data != null ? data.weight * quantity : 0f;
        public float TotalVolume => data != null ? data.volume * quantity : 0f;

        public InventoryItem(string itemId, int quantity, int durability, int slot)
        {
            this.itemId = itemId;
            this.quantity = quantity;
            this.currentDurability = durability;
            this.slotIndex = slot;
        }
    }

    // -------------------------------------------------------
    // InventorySystem
    // -------------------------------------------------------

    /// <summary>
    /// Weight-and-volume based inventory attached to the player.
    /// Respects backpack capacity limits and a hard max carry weight.
    /// </summary>
    public class InventorySystem : MonoBehaviour
    {
        public static InventorySystem Instance { get; private set; }

        // ----- configuration -----
        [Header("Carry Limits")]
        [Tooltip("Absolute max weight in kg (with or without backpack).")]
        public float maxCarryWeightKg = 25f;

        [Tooltip("Base pocket capacity when no backpack equipped (litres).")]
        public float basePocketCapacityL = 5f;

        // ----- runtime state -----
        private List<InventoryItem> _items = new List<InventoryItem>();
        private float _equippedBackpackCapacityL;
        private string _equippedBackpackId;

        // ----- events -----
        public event Action<InventoryItem> OnItemAdded;
        public event Action<InventoryItem> OnItemRemoved;
        public event Action OnInventoryChanged;

        // ----- computed -----
        public float CurrentWeightKg => _items.Sum(i => i.TotalWeight);
        public float CurrentVolumeL  => _items.Sum(i => i.TotalVolume);
        public float MaxVolumeL      => basePocketCapacityL + _equippedBackpackCapacityL;
        public float WeightRatio     => Mathf.Clamp01(CurrentWeightKg / maxCarryWeightKg);
        public float VolumeRatio     => MaxVolumeL > 0 ? Mathf.Clamp01(CurrentVolumeL / MaxVolumeL) : 1f;
        public bool  IsOverweight    => CurrentWeightKg > maxCarryWeightKg;
        public string EquippedBackpackId => _equippedBackpackId;
        public IReadOnlyList<InventoryItem> Items => _items.AsReadOnly();

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
        }

        // -------------------------------------------------------
        // Backpack management
        // -------------------------------------------------------

        /// <summary>
        /// Equip a backpack item, increasing volume capacity.
        /// Returns false if the item is not a valid backpack.
        /// </summary>
        public bool EquipBackpack(string backpackItemId)
        {
            ItemData data = ItemDatabase.Instance?.GetItem(backpackItemId);
            if (data == null || data.category != ItemCategory.Backpack)
            {
                Debug.LogWarning($"[Inventory] '{backpackItemId}' is not a valid backpack.");
                return false;
            }

            _equippedBackpackId = backpackItemId;
            _equippedBackpackCapacityL = data.capacityLitres;
            Debug.Log($"[Inventory] Equipped backpack '{data.displayName}' ({data.capacityLitres}L).");
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Unequip the current backpack. Items that exceed the new capacity
        /// are NOT automatically dropped; the caller must handle overflow.
        /// </summary>
        public void UnequipBackpack()
        {
            _equippedBackpackId = null;
            _equippedBackpackCapacityL = 0f;
            Debug.Log("[Inventory] Backpack unequipped.");
            OnInventoryChanged?.Invoke();
        }

        // -------------------------------------------------------
        // Add / remove items
        // -------------------------------------------------------

        /// <summary>
        /// Attempt to add an item. Returns the amount actually added
        /// (may be less than requested if weight/volume limits hit).
        /// </summary>
        public int AddItem(string itemId, int amount = 1)
        {
            if (amount <= 0) return 0;

            ItemData data = ItemDatabase.Instance?.GetItem(itemId);
            if (data == null)
            {
                Debug.LogWarning($"[Inventory] Unknown item id '{itemId}'.");
                return 0;
            }

            int added = 0;

            for (int i = 0; i < amount; i++)
            {
                float newWeight = CurrentWeightKg + data.weight;
                float newVolume = CurrentVolumeL  + data.volume;

                if (newWeight > maxCarryWeightKg)
                {
                    Debug.Log($"[Inventory] Cannot add '{data.displayName}': weight limit ({maxCarryWeightKg}kg) would be exceeded.");
                    break;
                }

                if (newVolume > MaxVolumeL)
                {
                    Debug.Log($"[Inventory] Cannot add '{data.displayName}': volume limit ({MaxVolumeL}L) would be exceeded.");
                    break;
                }

                // try stacking first
                if (data.maxStack > 1)
                {
                    InventoryItem existing = _items.FirstOrDefault(
                        x => x.itemId == itemId && x.quantity < data.maxStack);
                    if (existing != null)
                    {
                        existing.quantity++;
                        added++;
                        continue;
                    }
                }

                // new slot
                int slot = NextFreeSlot();
                InventoryItem newItem = new InventoryItem(itemId, 1, data.durability, slot)
                {
                    data = data
                };
                _items.Add(newItem);
                OnItemAdded?.Invoke(newItem);
                added++;
            }

            if (added > 0)
                OnInventoryChanged?.Invoke();

            return added;
        }

        /// <summary>
        /// Remove a quantity of items by id. Returns the amount actually removed.
        /// </summary>
        public int RemoveItem(string itemId, int amount = 1)
        {
            if (amount <= 0) return 0;

            int removed = 0;

            // work through stacks, newest first
            for (int idx = _items.Count - 1; idx >= 0 && removed < amount; idx--)
            {
                InventoryItem slot = _items[idx];
                if (slot.itemId != itemId) continue;

                int take = Mathf.Min(slot.quantity, amount - removed);
                slot.quantity -= take;
                removed += take;

                if (slot.quantity <= 0)
                {
                    _items.RemoveAt(idx);
                    OnItemRemoved?.Invoke(slot);
                }
            }

            if (removed > 0)
                OnInventoryChanged?.Invoke();

            return removed;
        }

        /// <summary>Check whether the inventory contains at least 'amount' of itemId.</summary>
        public bool HasItem(string itemId, int amount = 1)
        {
            int total = _items.Where(i => i.itemId == itemId).Sum(i => i.quantity);
            return total >= amount;
        }

        /// <summary>Returns total count of a specific item across all stacks.</summary>
        public int GetItemCount(string itemId)
        {
            return _items.Where(i => i.itemId == itemId).Sum(i => i.quantity);
        }

        /// <summary>Clear the entire inventory.</summary>
        public void Clear()
        {
            _items.Clear();
            OnInventoryChanged?.Invoke();
        }

        // -------------------------------------------------------
        // Durability helpers
        // -------------------------------------------------------

        /// <summary>
        /// Reduce durability of the first stack of itemId by amount.
        /// Returns true if the item was destroyed (durability reached 0).
        /// </summary>
        public bool ReduceDurability(string itemId, int amount = 1)
        {
            InventoryItem slot = _items.FirstOrDefault(i => i.itemId == itemId);
            if (slot == null) return false;

            slot.currentDurability -= amount;
            if (slot.currentDurability <= 0)
            {
                slot.quantity--;
                if (slot.quantity <= 0)
                {
                    _items.Remove(slot);
                    OnItemRemoved?.Invoke(slot);
                }
                OnInventoryChanged?.Invoke();
                return true;
            }

            OnInventoryChanged?.Invoke();
            return false;
        }

        // -------------------------------------------------------
        // Utility
        // -------------------------------------------------------

        /// <summary>Returns all items whose data resolves to a given category.</summary>
        public List<InventoryItem> GetItemsByCategory(ItemCategory category)
        {
            return _items.Where(i =>
            {
                if (i.data == null)
                    i.data = ItemDatabase.Instance?.GetItem(i.itemId);
                return i.data != null && i.data.category == category;
            }).ToList();
        }

        /// <summary>
        /// Resolve ItemData references for every slot (call after database reload).
        /// </summary>
        public void ResolveAllItemData()
        {
            foreach (InventoryItem slot in _items)
            {
                slot.data = ItemDatabase.Instance?.GetItem(slot.itemId);
            }
        }

        private int NextFreeSlot()
        {
            HashSet<int> used = new HashSet<int>(_items.Select(i => i.slotIndex));
            int s = 0;
            while (used.Contains(s)) s++;
            return s;
        }
    }
}
