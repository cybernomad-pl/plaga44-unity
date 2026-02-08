using System;
using UnityEngine;

namespace Plaga44.Inventory
{
    // -------------------------------------------------------
    // Backpack size presets matching scenario data
    // -------------------------------------------------------

    public enum BackpackSize
    {
        Small20L,   // 20-litre day pack
        Medium60L,  // 60-litre hiking pack
        Large80L,   // 80-litre expedition pack
        XLarge90L   // 90-litre military/expedition pack
    }

    // -------------------------------------------------------
    // BackpackController
    // -------------------------------------------------------

    /// <summary>
    /// Manages backpack equip/unequip and applies weight-based
    /// stamina modifiers to the player. Works together with
    /// InventorySystem to enforce volume caps.
    /// </summary>
    public class BackpackController : MonoBehaviour
    {
        public static BackpackController Instance { get; private set; }

        // ----- configuration -----
        [Header("Stamina Curve")]
        [Tooltip("Stamina multiplier at 0% carry weight. 1 = normal.")]
        public float staminaMultiplierAtZeroWeight = 1.0f;

        [Tooltip("Stamina multiplier at 100% carry weight (25 kg).")]
        public float staminaMultiplierAtMaxWeight = 0.35f;

        [Tooltip("Additional stamina drain per second while overweight.")]
        public float overweightDrainPerSecond = 8f;

        [Header("Movement Modifiers")]
        [Tooltip("Move speed multiplier at max weight.")]
        public float moveSpeedMultiplierAtMaxWeight = 0.5f;

        [Tooltip("Can the player sprint while overweight?")]
        public bool allowSprintWhileOverweight = false;

        [Header("Visual")]
        [Tooltip("Transform where the backpack model is attached.")]
        public Transform backpackAttachPoint;

        // ----- runtime -----
        private string _currentBackpackId;
        private GameObject _backpackVisualInstance;

        // ----- events -----
        public event Action<string> OnBackpackEquipped;
        public event Action OnBackpackUnequipped;

        // ----- public read -----

        /// <summary>True when a backpack is equipped.</summary>
        public bool HasBackpack => !string.IsNullOrEmpty(_currentBackpackId);

        /// <summary>
        /// Current stamina regeneration multiplier based on carry weight.
        /// Ranges from staminaMultiplierAtZeroWeight (empty) down to
        /// staminaMultiplierAtMaxWeight (at 25 kg).
        /// </summary>
        public float StaminaRegenMultiplier
        {
            get
            {
                InventorySystem inv = InventorySystem.Instance;
                if (inv == null) return staminaMultiplierAtZeroWeight;
                float t = inv.WeightRatio; // 0..1
                return Mathf.Lerp(staminaMultiplierAtZeroWeight,
                                  staminaMultiplierAtMaxWeight, t);
            }
        }

        /// <summary>
        /// Current movement speed multiplier based on carry weight.
        /// </summary>
        public float MoveSpeedMultiplier
        {
            get
            {
                InventorySystem inv = InventorySystem.Instance;
                if (inv == null) return 1f;
                float t = inv.WeightRatio;
                return Mathf.Lerp(1f, moveSpeedMultiplierAtMaxWeight, t);
            }
        }

        /// <summary>
        /// Per-second stamina drain caused by being overweight.
        /// Returns 0 when not overweight.
        /// </summary>
        public float OverweightStaminaDrain
        {
            get
            {
                InventorySystem inv = InventorySystem.Instance;
                if (inv == null || !inv.IsOverweight) return 0f;
                return overweightDrainPerSecond;
            }
        }

        /// <summary>Whether the player may sprint given current load.</summary>
        public bool CanSprint
        {
            get
            {
                if (allowSprintWhileOverweight) return true;
                InventorySystem inv = InventorySystem.Instance;
                return inv == null || !inv.IsOverweight;
            }
        }

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
        // Equip / Unequip
        // -------------------------------------------------------

        /// <summary>
        /// Equip a backpack by item id. The item must already be in the
        /// inventory (or being picked up) and must have category == Backpack.
        /// </summary>
        public bool Equip(string backpackItemId)
        {
            ItemData data = ItemDatabase.Instance?.GetItem(backpackItemId);
            if (data == null || data.category != ItemCategory.Backpack)
            {
                Debug.LogWarning($"[BackpackController] '{backpackItemId}' is not a valid backpack.");
                return false;
            }

            // Unequip previous if any
            if (HasBackpack)
                Unequip();

            _currentBackpackId = backpackItemId;

            // Tell InventorySystem about new capacity
            InventorySystem.Instance?.EquipBackpack(backpackItemId);

            // Spawn visual (if prefab path available)
            SpawnBackpackVisual(data);

            Debug.Log($"[BackpackController] Equipped '{data.displayName}' " +
                      $"(capacity {data.capacityLitres}L).");
            OnBackpackEquipped?.Invoke(backpackItemId);
            return true;
        }

        /// <summary>Unequip current backpack.</summary>
        public void Unequip()
        {
            if (!HasBackpack) return;

            string old = _currentBackpackId;
            _currentBackpackId = null;

            InventorySystem.Instance?.UnequipBackpack();

            DestroyBackpackVisual();

            Debug.Log($"[BackpackController] Unequipped backpack '{old}'.");
            OnBackpackUnequipped?.Invoke();
        }

        // -------------------------------------------------------
        // Helpers: map BackpackSize enum to item ids
        // -------------------------------------------------------

        /// <summary>Returns the item id associated with a preset BackpackSize.</summary>
        public static string GetBackpackItemId(BackpackSize size)
        {
            switch (size)
            {
                case BackpackSize.Small20L:  return "backpack_20l";
                case BackpackSize.Medium60L: return "backpack_60l";
                case BackpackSize.Large80L:  return "backpack_80l";
                case BackpackSize.XLarge90L: return "backpack_90l";
                default: return "backpack_20l";
            }
        }

        /// <summary>Returns capacity in litres for a preset.</summary>
        public static float GetCapacityForSize(BackpackSize size)
        {
            switch (size)
            {
                case BackpackSize.Small20L:  return 20f;
                case BackpackSize.Medium60L: return 60f;
                case BackpackSize.Large80L:  return 80f;
                case BackpackSize.XLarge90L: return 90f;
                default: return 20f;
            }
        }

        // -------------------------------------------------------
        // Visual management
        // -------------------------------------------------------

        private void SpawnBackpackVisual(ItemData data)
        {
            if (backpackAttachPoint == null) return;

            // Attempt to load a prefab by convention: "Prefabs/Backpacks/{id}"
            string prefabPath = $"Prefabs/Backpacks/{data.id}";
            GameObject prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab != null)
            {
                _backpackVisualInstance = Instantiate(prefab, backpackAttachPoint);
                _backpackVisualInstance.transform.localPosition = Vector3.zero;
                _backpackVisualInstance.transform.localRotation = Quaternion.identity;
            }
        }

        private void DestroyBackpackVisual()
        {
            if (_backpackVisualInstance != null)
            {
                Destroy(_backpackVisualInstance);
                _backpackVisualInstance = null;
            }
        }
    }
}
