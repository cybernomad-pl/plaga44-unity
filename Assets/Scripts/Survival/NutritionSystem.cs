// PLAGA '44 VR - NutritionSystem
// Caloric tracking, food types, and seasonal food availability.
//
// From scenario docs (Gra_scenariusz parts 1-4, 7):
// Food items mentioned:
// - "zupki chinskie 12 sztuk" (instant noodles - high carb, quick prep)
// - "batoniki zbozowe 10 sztuk" (cereal bars)
// - "czekolada 4 tabliczki" (chocolate - glucose boost)
// - "barszcz czerwony w saszetkach" (beet soup packets)
// - "konserwy" (canned food - meat, fish)
// - "kasza" (groats - "latwa w przygotowaniu i zawierajaca duzo kalorii")
// - "maca z dzemem" (matzah with jam)
// - "kielbasa" (sausage)
// - Military rations: "racji zywnosciowych podgrzewanych chemicznie"
//
// Seasonal food from scenario (part 3):
// - Summer: "jagody, maliny" (berries), fish from clean streams
// - Autumn: "grzyby" (mushrooms - poisoning risk!), last berries
// - Winter: "ciezko jest o porzywienie", need to raid stores/apartments
// - Spring: "maliny, jezyny, jagody zaczyja powoli sie pojawiac"
// - Rural areas: "marchew, ziemniaki, zboze, jablka" (seasonal)
//
// From scenario (part 4):
// - "3 razy dziennie zjesc po jednym lub dwa batony czekoladowe z sezamem"
// - Meal during march break: "2 zupki chinskie, 1 konserwa, wafle, 2 batony, 2 herbaty, 1 kawa"
// - "1 lub pol tabliczki [czekolady] na dzien i 2 do 4 batonikow"
//
// Architecture: Manages food inventory and seasonal foraging availability.
// Drives PhysiologyController.ConsumeFood() API.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.Survival
{
    using Plaga44.Physiology;

    /// <summary>
    /// Manages food inventory, caloric tracking, seasonal food availability,
    /// and the nutritional effects of different food types on physiology.
    /// </summary>
    public class NutritionSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SeasonManager seasonManager;
        [SerializeField] private PhysiologyController physiologyController;

        [Header("Caloric State")]
        [Tooltip("Total calories consumed today.")]
        [SerializeField] private float caloriesConsumedToday = 0f;

        [Tooltip("Daily caloric target. Average survival need: 2000-3000 kcal.")]
        [SerializeField] private float dailyCaloricTarget = 2500f;

        [Tooltip("Hours since last meal.")]
        [SerializeField] private float hoursSinceLastMeal = 4f;

        [Header("Food Inventory")]
        [SerializeField] private List<FoodItem> foodInventory = new List<FoodItem>();

        [Header("Foraging")]
        [Tooltip("Current foraging availability factor (0-1). Depends on season and location.")]
        [SerializeField] private float foragingAvailability = 0f;

        [Header("Configuration")]
        [SerializeField] private NutritionConfig config;

        // Time tracking
        private float gameHoursPerRealSecond = 0.01f;
        private float lastDayResetHour = 0f;

        // Events
        public event Action OnHungry;                      // Need to eat warning
        public event Action<FoodItem> OnFoodConsumed;
        public event Action<float> OnCalorieDeficit;       // Daily deficit warning
        public event Action<ForageResult> OnForageResult;  // Foraging outcome
        public event Action<string> OnFoodPoisoningRisk;   // Mushroom/spoiled food warning

        // Public accessors
        public float CaloriesConsumedToday => caloriesConsumedToday;
        public float DailyCaloricTarget => dailyCaloricTarget;
        public float HoursSinceLastMeal => hoursSinceLastMeal;
        public float ForagingAvailability => foragingAvailability;
        public IReadOnlyList<FoodItem> FoodInventory => foodInventory.AsReadOnly();

        public float CalorieDeficit => Mathf.Max(0f, dailyCaloricTarget - caloriesConsumedToday);

        private void Update()
        {
            float dt = Time.deltaTime;
            float dtGameHours = dt * gameHoursPerRealSecond;

            hoursSinceLastMeal += dtGameHours;
            UpdateForagingAvailability();
            UpdateHungerWarnings(dtGameHours);
            UpdateDailyReset();
        }

        // ===== FORAGING AVAILABILITY =====
        // Seasonal food sources from scenario docs.
        // Summer is most abundant, winter is hardest.

        private void UpdateForagingAvailability()
        {
            if (seasonManager == null) return;

            Season season = seasonManager.CurrentSeason;
            float timeOfDay = seasonManager.CurrentTimeOfDay;
            bool isDaytime = seasonManager.IsDaytime;

            float baseAvailability = GetSeasonalForagingBase(season);

            // Can only forage during daylight
            // From scenario: movement recommended 6:00-19:00 in autumn
            if (!isDaytime)
            {
                baseAvailability *= 0.1f; // Almost impossible at night
            }

            // Weather affects foraging
            if (seasonManager.CurrentWeather == WeatherCondition.Storm ||
                seasonManager.CurrentWeather == WeatherCondition.Snow)
            {
                baseAvailability *= 0.3f;
            }

            foragingAvailability = Mathf.Clamp01(baseAvailability);
        }

        /// <summary>
        /// Seasonal foraging base availability.
        /// From scenario docs part 3:
        /// - Winter: "w lesie zimie ciezko jest o porzywienie"
        /// - Spring: "maliny, jezyny, jagody zaczyja powoli sie pojawiac"
        /// - Summer: berries, fish available; "jagody, maliny ktore podnosz poziom glukozy"
        /// - Autumn: mushrooms (risky!), last berries, "grzyby - bez znajomosci gatunkow
        ///   nie jest mozliwe ich zbieranie, grozi smiertelnym zatruciem"
        /// </summary>
        private float GetSeasonalForagingBase(Season season)
        {
            switch (season)
            {
                case Season.Winter:
                    // From scenario: very hard, rely on stores, raids, animal feeders
                    return config != null ? config.winterForagingBase : 0.05f;
                case Season.Spring:
                    // From scenario: "powoli pojawiaja sie maliny, jezyny, jagody"
                    return config != null ? config.springForagingBase : 0.25f;
                case Season.Summer:
                    // From scenario: berries, fish, rural crops available
                    return config != null ? config.summerForagingBase : 0.6f;
                case Season.Autumn:
                    // From scenario: mushrooms (risky), last berries, early shortage
                    return config != null ? config.autumnForagingBase : 0.35f;
                default:
                    return 0.2f;
            }
        }

        // ===== HUNGER WARNINGS =====

        private void UpdateHungerWarnings(float dtGameHours)
        {
            // From scenario: recommended meal breaks during marches
            // "spozycje posilku: 2 zupki chinskie, 1 konserwa, wafle, 2 batony"
            float mealInterval = config != null ? config.recommendedMealIntervalHours : 4f;

            if (hoursSinceLastMeal > mealInterval)
            {
                OnHungry?.Invoke();
            }
        }

        private void UpdateDailyReset()
        {
            if (seasonManager == null) return;

            float currentHour = seasonManager.CurrentTimeOfDay;
            // Reset daily calorie counter at 6 AM
            if (currentHour >= 6f && currentHour < 7f && lastDayResetHour < 6f)
            {
                // Check previous day's deficit
                if (caloriesConsumedToday < dailyCaloricTarget * 0.5f)
                {
                    OnCalorieDeficit?.Invoke(CalorieDeficit);
                }
                caloriesConsumedToday = 0f;
            }
            lastDayResetHour = currentHour;
        }

        // ===== PUBLIC API =====

        /// <summary>
        /// Consume a food item from inventory.
        /// Different food types provide different nutritional benefits.
        /// </summary>
        public bool ConsumeFood(int inventoryIndex)
        {
            if (inventoryIndex < 0 || inventoryIndex >= foodInventory.Count)
                return false;

            FoodItem item = foodInventory[inventoryIndex];
            foodInventory.RemoveAt(inventoryIndex);

            ApplyFoodEffects(item);
            hoursSinceLastMeal = 0f;
            caloriesConsumedToday += item.calories;

            OnFoodConsumed?.Invoke(item);
            return true;
        }

        /// <summary>
        /// Consume a specific food item directly (not from inventory).
        /// Used for foraging results and quick consumption.
        /// </summary>
        public void ConsumeDirectFood(FoodItem item)
        {
            ApplyFoodEffects(item);
            hoursSinceLastMeal = 0f;
            caloriesConsumedToday += item.calories;
            OnFoodConsumed?.Invoke(item);
        }

        /// <summary>
        /// Apply nutritional effects of food to physiology.
        /// Maps food types to PhysiologyController.ConsumeFood() params.
        /// </summary>
        private void ApplyFoodEffects(FoodItem item)
        {
            if (physiologyController == null) return;

            ToxinType toxin = ToxinType.None;
            float toxinAmount = 0f;

            // Check for poisoning risk
            if (item.type == FoodType.Mushroom && !item.isIdentifiedSafe)
            {
                // From scenario: "bez znajomosci gatunkow grzybow nie jest mozliwe
                // ich zbieranie - grozi smiertelnym zatruciem"
                float poisonChance = config != null ? config.mushroomPoisonChance : 0.4f;
                if (UnityEngine.Random.value < poisonChance)
                {
                    toxin = UnityEngine.Random.value > 0.5f
                        ? ToxinType.MushroomHallucinogenic
                        : ToxinType.MushroomGastrointestinal;
                    toxinAmount = UnityEngine.Random.Range(0.3f, 0.8f);
                    OnFoodPoisoningRisk?.Invoke("MUSHROOM_POISONING");
                }
            }
            else if (item.isSpoiled)
            {
                float spoilChance = config != null ? config.spoiledFoodPoisonChance : 0.5f;
                if (UnityEngine.Random.value < spoilChance)
                {
                    toxin = ToxinType.FoodPoisoning;
                    toxinAmount = UnityEngine.Random.Range(0.2f, 0.5f);
                    OnFoodPoisoningRisk?.Invoke("FOOD_POISONING");
                }
            }

            physiologyController.ConsumeFood(item.calories, item.glucoseBoost, toxin, toxinAmount);
        }

        /// <summary>
        /// Attempt to forage for food in the current area.
        /// Success depends on season, time, weather, and luck.
        /// From scenario: seasonal food availability varies greatly.
        /// </summary>
        public ForageResult AttemptForage()
        {
            if (seasonManager == null)
                return new ForageResult { success = false, message = "NO_SEASON_DATA" };

            if (foragingAvailability < 0.01f)
                return new ForageResult { success = false, message = "NOTHING_AVAILABLE" };

            float roll = UnityEngine.Random.value;
            if (roll > foragingAvailability)
                return new ForageResult { success = false, message = "SEARCH_FAILED" };

            // Generate food item based on season
            FoodItem foraged = GenerateSeasonalForageItem(seasonManager.CurrentSeason);

            var result = new ForageResult
            {
                success = true,
                item = foraged,
                message = $"FOUND_{foraged.type}"
            };

            OnForageResult?.Invoke(result);
            return result;
        }

        /// <summary>
        /// Generate a random food item appropriate for the current season.
        /// Based on scenario docs seasonal descriptions.
        /// </summary>
        private FoodItem GenerateSeasonalForageItem(Season season)
        {
            float roll = UnityEngine.Random.value;

            switch (season)
            {
                case Season.Summer:
                    // From scenario: "jagody, maliny", fish, rural crops
                    if (roll < 0.3f)
                        return FoodItem.Create("Jagody (blueberries)", FoodType.Berry, 45f, 0.15f);
                    if (roll < 0.5f)
                        return FoodItem.Create("Maliny (raspberries)", FoodType.Berry, 52f, 0.12f);
                    if (roll < 0.65f)
                        return FoodItem.Create("Ryba (fish)", FoodType.Fish, 200f, 0.05f);
                    if (roll < 0.8f)
                        return FoodItem.Create("Marchew (carrot)", FoodType.Vegetable, 35f, 0.08f);
                    return FoodItem.Create("Jablko (apple)", FoodType.Fruit, 95f, 0.1f);

                case Season.Autumn:
                    // From scenario: mushrooms (dangerous!), last berries
                    if (roll < 0.35f)
                    {
                        var mushroom = FoodItem.Create("Grzyb (mushroom)", FoodType.Mushroom, 30f, 0.02f);
                        mushroom.isIdentifiedSafe = false; // Danger!
                        return mushroom;
                    }
                    if (roll < 0.5f)
                        return FoodItem.Create("Jagody (blueberries)", FoodType.Berry, 40f, 0.12f);
                    if (roll < 0.7f)
                        return FoodItem.Create("Ziemniaki (potatoes)", FoodType.Vegetable, 130f, 0.08f);
                    return FoodItem.Create("Jablko (apple)", FoodType.Fruit, 80f, 0.1f);

                case Season.Winter:
                    // From scenario: "ciezko jest o porzywienie"
                    // Can find animal feed at "pasniki" (feeders)
                    if (roll < 0.5f)
                        return FoodItem.Create("Korma zwierzeca (animal feed)", FoodType.Grain, 60f, 0.03f);
                    return FoodItem.Create("Kora brzozy (birch bark)", FoodType.Foraged, 15f, 0.01f);

                case Season.Spring:
                    // From scenario: early berries, wild herbs
                    if (roll < 0.4f)
                        return FoodItem.Create("Mlode pokrzywy (young nettles)", FoodType.Foraged, 20f, 0.03f);
                    if (roll < 0.7f)
                        return FoodItem.Create("Jezyny (blackberries)", FoodType.Berry, 43f, 0.1f);
                    return FoodItem.Create("Szczaw (sorrel)", FoodType.Foraged, 15f, 0.02f);

                default:
                    return FoodItem.Create("Jagody (blueberries)", FoodType.Berry, 45f, 0.1f);
            }
        }

        /// <summary>
        /// Add food to inventory (from looting stores, apartments, soldiers).
        /// From scenario: "wlamywanie sie do marketow, sklepow, stacji benzynowych"
        /// </summary>
        public void AddToInventory(FoodItem item)
        {
            foodInventory.Add(item);
        }

        /// <summary>
        /// Get summary of daily nutritional status.
        /// </summary>
        public NutritionSummary GetDailySummary()
        {
            return new NutritionSummary
            {
                caloriesConsumed = caloriesConsumedToday,
                calorieTarget = dailyCaloricTarget,
                deficit = CalorieDeficit,
                mealsSinceReset = 0, // Could track meals count
                hoursSinceLastMeal = hoursSinceLastMeal,
                inventoryCalories = GetTotalInventoryCalories()
            };
        }

        private float GetTotalInventoryCalories()
        {
            float total = 0f;
            foreach (var item in foodInventory)
            {
                total += item.calories;
            }
            return total;
        }
    }

    // ===== FOOD DATA =====

    [Serializable]
    public class FoodItem
    {
        public string name;
        public FoodType type;
        public float calories;
        public float glucoseBoost;   // Immediate glucose effect (0-1)
        public float weight;         // kg
        public bool requiresCooking; // Needs fire to prepare
        public bool isIdentifiedSafe;// For mushrooms: has player identified it?
        public bool isSpoiled;       // Food gone bad
        public float spoilTimer;     // Hours until spoilage

        /// <summary>
        /// Factory method for creating standard food items.
        /// </summary>
        public static FoodItem Create(string name, FoodType type, float calories, float glucoseBoost,
            float weight = 0.1f, bool requiresCooking = false)
        {
            return new FoodItem
            {
                name = name,
                type = type,
                calories = calories,
                glucoseBoost = glucoseBoost,
                weight = weight,
                requiresCooking = requiresCooking,
                isIdentifiedSafe = type != FoodType.Mushroom,
                isSpoiled = false,
                spoilTimer = GetDefaultSpoilTime(type)
            };
        }

        private static float GetDefaultSpoilTime(FoodType type)
        {
            switch (type)
            {
                case FoodType.CannedFood: return 999f;     // Long-lasting
                case FoodType.MilitaryRation: return 999f;  // Long-lasting
                case FoodType.InstantNoodles: return 999f;  // Dry food
                case FoodType.Grain: return 720f;           // 30 days
                case FoodType.Chocolate: return 720f;       // Long shelf life
                case FoodType.CerealBar: return 480f;       // 20 days
                case FoodType.Fish: return 12f;             // Spoils fast
                case FoodType.Berry: return 48f;            // 2 days
                case FoodType.Mushroom: return 24f;         // 1 day
                case FoodType.Vegetable: return 168f;       // 7 days
                case FoodType.Fruit: return 120f;           // 5 days
                default: return 72f;                        // 3 days default
            }
        }
    }

    public enum FoodType
    {
        InstantNoodles,     // "zupki chinskie" - from scenario: 12 packs, quick prep
        CerealBar,          // "batoniki zbozowe" - from scenario: 10 pieces
        Chocolate,          // "czekolada" - from scenario: glucose boost, 4 bars
        CannedFood,         // "konserwy" - meat, fish, canned goods
        Grain,              // "kasza" - from scenario: "duzo kalorii, latwa w przygotowaniu"
        Sausage,            // "kielbasa" - from scenario
        SoupPacket,         // "barszcz czerwony w saszetkach" - from scenario
        MilitaryRation,     // "racje zywnosciowe" - from scenario: chemically heated
        Mushroom,           // "grzyby" - from scenario: DANGER without identification
        Berry,              // "jagody, maliny, jezyny" - seasonal
        Fish,               // "pstragi, wegorze" - from clean rivers
        Vegetable,          // "marchew, ziemniaki" - from rural areas
        Fruit,              // "jablka" - seasonal
        Bread,              // "maca, wafle" - from stores
        Foraged             // Generic foraged items (bark, herbs, etc.)
    }

    [Serializable]
    public struct ForageResult
    {
        public bool success;
        public FoodItem item;
        public string message;
    }

    [Serializable]
    public struct NutritionSummary
    {
        public float caloriesConsumed;
        public float calorieTarget;
        public float deficit;
        public int mealsSinceReset;
        public float hoursSinceLastMeal;
        public float inventoryCalories;
    }

    // ===== NUTRITION CONFIG =====

    /// <summary>
    /// Tunable configuration for nutrition parameters.
    /// Create assets: Assets > Create > Plaga44 > Nutrition Config
    /// </summary>
    [CreateAssetMenu(fileName = "NutritionConfig", menuName = "Plaga44/Nutrition Config")]
    public class NutritionConfig : ScriptableObject
    {
        [Header("Meal Schedule")]
        [Tooltip("Recommended hours between meals. From scenario: eat during march breaks.")]
        public float recommendedMealIntervalHours = 4f;

        [Header("Foraging Base Rates per Season")]
        [Tooltip("Winter foraging availability. From scenario: 'ciezko jest o porzywienie'.")]
        public float winterForagingBase = 0.05f;

        [Tooltip("Spring foraging availability. Early berries and herbs.")]
        public float springForagingBase = 0.25f;

        [Tooltip("Summer foraging availability. Berries, fish, rural crops.")]
        public float summerForagingBase = 0.6f;

        [Tooltip("Autumn foraging availability. Mushrooms (risky), last berries.")]
        public float autumnForagingBase = 0.35f;

        [Header("Poisoning Risks")]
        [Tooltip("Chance of poisoning from unidentified mushrooms. From scenario: very dangerous.")]
        public float mushroomPoisonChance = 0.4f;

        [Tooltip("Chance of poisoning from spoiled food.")]
        public float spoiledFoodPoisonChance = 0.5f;
    }
}
