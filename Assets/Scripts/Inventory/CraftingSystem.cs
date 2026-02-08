using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Plaga44.Inventory
{
    // -------------------------------------------------------
    // Recipe data classes (mirror JSON)
    // -------------------------------------------------------

    [Serializable]
    public class RecipeIngredient
    {
        public string itemId;
        public int quantity;
    }

    [Serializable]
    public class RecipeOutput
    {
        public string itemId;
        public int quantity;
    }

    [Serializable]
    public class CraftingRecipe
    {
        public string recipeId;
        public string displayName;
        public string description;
        public string category;              // "weapon", "medical", "fire", "food", "tool"
        public float craftTimeSeconds;
        public List<RecipeIngredient> ingredients;
        public List<RecipeOutput> outputs;
        public List<string> requiredTools;   // item ids the player must have (not consumed)
        public List<string> tags;
    }

    [Serializable]
    public class CraftingRecipeWrapper
    {
        public List<CraftingRecipe> recipes;
    }

    // -------------------------------------------------------
    // Active crafting operation
    // -------------------------------------------------------

    public class CraftingOperation
    {
        public CraftingRecipe Recipe;
        public float StartTime;
        public float Duration;
        public bool IsComplete => Time.time >= StartTime + Duration;
        public float Progress => Mathf.Clamp01((Time.time - StartTime) / Duration);
    }

    // -------------------------------------------------------
    // CraftingSystem
    // -------------------------------------------------------

    /// <summary>
    /// Recipe-driven crafting system. Loads recipes from CraftingRecipes.json,
    /// checks ingredient availability in InventorySystem, and produces outputs.
    /// </summary>
    public class CraftingSystem : MonoBehaviour
    {
        public static CraftingSystem Instance { get; private set; }

        [Header("Data Source")]
        [Tooltip("TextAsset pointing to Assets/Data/CraftingRecipes.json")]
        public TextAsset recipesAsset;

        private Dictionary<string, CraftingRecipe> _recipesById;
        private Dictionary<string, List<CraftingRecipe>> _recipesByCategory;
        private CraftingOperation _activeCraft;

        // ----- events -----
        public event Action<CraftingRecipe> OnCraftStarted;
        public event Action<CraftingRecipe, List<RecipeOutput>> OnCraftCompleted;
        public event Action<CraftingRecipe, string> OnCraftFailed;

        public CraftingOperation ActiveCraft => _activeCraft;
        public bool IsCrafting => _activeCraft != null && !_activeCraft.IsComplete;

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

            LoadRecipes();
        }

        private void Update()
        {
            if (_activeCraft != null && _activeCraft.IsComplete)
            {
                FinishCraft();
            }
        }

        // -------------------------------------------------------
        // Loading
        // -------------------------------------------------------

        public void LoadRecipes()
        {
            _recipesById = new Dictionary<string, CraftingRecipe>();
            _recipesByCategory = new Dictionary<string, List<CraftingRecipe>>();

            if (recipesAsset == null)
            {
                Debug.LogError("[CraftingSystem] recipesAsset is not assigned.");
                return;
            }

            CraftingRecipeWrapper wrapper =
                JsonUtility.FromJson<CraftingRecipeWrapper>(recipesAsset.text);

            if (wrapper == null || wrapper.recipes == null)
            {
                Debug.LogError("[CraftingSystem] Failed to parse CraftingRecipes.json.");
                return;
            }

            foreach (CraftingRecipe recipe in wrapper.recipes)
            {
                if (string.IsNullOrEmpty(recipe.recipeId)) continue;

                _recipesById[recipe.recipeId] = recipe;

                string cat = recipe.category ?? "misc";
                if (!_recipesByCategory.ContainsKey(cat))
                    _recipesByCategory[cat] = new List<CraftingRecipe>();
                _recipesByCategory[cat].Add(recipe);
            }

            Debug.Log($"[CraftingSystem] Loaded {_recipesById.Count} recipes.");
        }

        // -------------------------------------------------------
        // Public API
        // -------------------------------------------------------

        /// <summary>Check whether the player can craft a given recipe right now.</summary>
        public bool CanCraft(string recipeId)
        {
            return CanCraft(recipeId, out _);
        }

        /// <summary>Check whether the player can craft a given recipe, with reason.</summary>
        public bool CanCraft(string recipeId, out string reason)
        {
            reason = null;

            if (IsCrafting)
            {
                reason = "Already crafting something.";
                return false;
            }

            if (!_recipesById.TryGetValue(recipeId, out CraftingRecipe recipe))
            {
                reason = $"Unknown recipe '{recipeId}'.";
                return false;
            }

            InventorySystem inv = InventorySystem.Instance;
            if (inv == null)
            {
                reason = "Inventory system not available.";
                return false;
            }

            // check ingredients
            foreach (RecipeIngredient ing in recipe.ingredients)
            {
                if (!inv.HasItem(ing.itemId, ing.quantity))
                {
                    ItemData itemData = ItemDatabase.Instance?.GetItem(ing.itemId);
                    string name = itemData != null ? itemData.displayName : ing.itemId;
                    reason = $"Missing ingredient: {name} x{ing.quantity} (have {inv.GetItemCount(ing.itemId)}).";
                    return false;
                }
            }

            // check required tools (must be present, not consumed)
            if (recipe.requiredTools != null)
            {
                foreach (string toolId in recipe.requiredTools)
                {
                    if (!inv.HasItem(toolId))
                    {
                        ItemData toolData = ItemDatabase.Instance?.GetItem(toolId);
                        string name = toolData != null ? toolData.displayName : toolId;
                        reason = $"Missing tool: {name}.";
                        return false;
                    }
                }
            }

            // check whether outputs would fit
            foreach (RecipeOutput output in recipe.outputs)
            {
                ItemData outData = ItemDatabase.Instance?.GetItem(output.itemId);
                if (outData == null)
                {
                    reason = $"Unknown output item '{output.itemId}'.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Start crafting a recipe. Ingredients are consumed immediately.
        /// The result is delivered after craftTimeSeconds.
        /// </summary>
        public bool StartCraft(string recipeId)
        {
            if (!CanCraft(recipeId, out string reason))
            {
                Debug.LogWarning($"[CraftingSystem] Cannot craft '{recipeId}': {reason}");
                OnCraftFailed?.Invoke(_recipesById.GetValueOrDefault(recipeId), reason);
                return false;
            }

            CraftingRecipe recipe = _recipesById[recipeId];
            InventorySystem inv = InventorySystem.Instance;

            // consume ingredients
            foreach (RecipeIngredient ing in recipe.ingredients)
            {
                inv.RemoveItem(ing.itemId, ing.quantity);
            }

            // reduce tool durability by 1 for each craft
            if (recipe.requiredTools != null)
            {
                foreach (string toolId in recipe.requiredTools)
                {
                    inv.ReduceDurability(toolId, 1);
                }
            }

            // start timed operation
            _activeCraft = new CraftingOperation
            {
                Recipe = recipe,
                StartTime = Time.time,
                Duration = recipe.craftTimeSeconds
            };

            Debug.Log($"[CraftingSystem] Started crafting '{recipe.displayName}' ({recipe.craftTimeSeconds}s).");
            OnCraftStarted?.Invoke(recipe);
            return true;
        }

        /// <summary>Cancel the active craft. Ingredients are lost.</summary>
        public void CancelCraft()
        {
            if (_activeCraft == null) return;
            Debug.Log($"[CraftingSystem] Cancelled crafting '{_activeCraft.Recipe.displayName}'.");
            OnCraftFailed?.Invoke(_activeCraft.Recipe, "Cancelled by player.");
            _activeCraft = null;
        }

        // -------------------------------------------------------
        // Queries
        // -------------------------------------------------------

        public CraftingRecipe GetRecipe(string recipeId)
        {
            _recipesById.TryGetValue(recipeId, out CraftingRecipe r);
            return r;
        }

        public List<CraftingRecipe> GetRecipesByCategory(string category)
        {
            if (_recipesByCategory.TryGetValue(category, out List<CraftingRecipe> list))
                return list;
            return new List<CraftingRecipe>();
        }

        /// <summary>Returns all recipes the player can currently craft.</summary>
        public List<CraftingRecipe> GetAvailableRecipes()
        {
            return _recipesById.Values.Where(r => CanCraft(r.recipeId)).ToList();
        }

        /// <summary>Returns every loaded recipe.</summary>
        public List<CraftingRecipe> GetAllRecipes()
        {
            return _recipesById.Values.ToList();
        }

        // -------------------------------------------------------
        // Internal
        // -------------------------------------------------------

        private void FinishCraft()
        {
            if (_activeCraft == null) return;

            CraftingRecipe recipe = _activeCraft.Recipe;
            InventorySystem inv = InventorySystem.Instance;
            List<RecipeOutput> produced = new List<RecipeOutput>();

            foreach (RecipeOutput output in recipe.outputs)
            {
                int added = inv.AddItem(output.itemId, output.quantity);
                if (added < output.quantity)
                {
                    Debug.LogWarning($"[CraftingSystem] Could only add {added}/{output.quantity} of '{output.itemId}' (inventory full?).");
                }
                produced.Add(new RecipeOutput { itemId = output.itemId, quantity = added });
            }

            Debug.Log($"[CraftingSystem] Completed crafting '{recipe.displayName}'.");
            OnCraftCompleted?.Invoke(recipe, produced);
            _activeCraft = null;
        }
    }
}
