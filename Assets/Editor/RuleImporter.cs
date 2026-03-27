// AUTO-DISABLED: not needed for demo
#if PLAGA44_FULL_SDK
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Plaga44.Rules;

namespace Plaga44.Editor
{
    /// <summary>
    /// Imports CombatRule ScriptableObjects from a JSON file.
    /// Menu: CYBERNOMAD/Rules/Import from JSON
    ///
    /// Expected JSON format (future Neo4j export):
    /// [
    ///   {
    ///     "name": "Stone_Head_Fatal",
    ///     "source": "Stone",
    ///     "hitZone": "Head",
    ///     "threshold": 10.0,
    ///     "effect": "MorsCerebri",
    ///     "stunDuration": 0.0,
    ///     "knockBackForce": 0.0,
    ///     "woundDamage": 0.0,
    ///     "description": "Stone to head with sufficient force = instant death"
    ///   }
    /// ]
    /// </summary>
    public static class RuleImporter
    {
        private const string OUTPUT_DIR = "Assets/Data/Rules";
        private const string LOG = "[Plaga44/RuleImporter]";

        private const string SEED_JSON_PATH = "Assets/Data/Rules/default_rules_seed.json";
        private const string DEFAULT_RULESET_PATH = "Assets/Data/Rules/DefaultCombatRules.asset";

        /// <summary>
        /// One-click menu to seed default ScriptableObject rules from the bundled JSON.
        /// Run once after cloning to get Stone_Head_Fatal and other defaults.
        /// Menu: CYBERNOMAD/Rules/Seed Default Rules
        /// </summary>
        [MenuItem("CYBERNOMAD/Rules/Seed Default Rules", false, 199)]
        public static void SeedDefaultRules()
        {
            if (!File.Exists(Path.GetFullPath(SEED_JSON_PATH)))
            {
                Debug.LogError($"{LOG} Seed file not found: {SEED_JSON_PATH}");
                return;
            }

            string json = File.ReadAllText(Path.GetFullPath(SEED_JSON_PATH));
            int count = ImportFromJsonString(json, OUTPUT_DIR);

            if (count > 0)
            {
                // Build or update DefaultCombatRules ruleset asset.
                CreateOrUpdateDefaultRuleSet();
            }
        }

        private static void CreateOrUpdateDefaultRuleSet()
        {
            // Find all newly created rules.
            string[] guids = AssetDatabase.FindAssets("t:CombatRule", new[] { OUTPUT_DIR });
            var rules = new System.Collections.Generic.List<CombatRule>();
            foreach (string guid in guids)
            {
                CombatRule r = AssetDatabase.LoadAssetAtPath<CombatRule>(AssetDatabase.GUIDToAssetPath(guid));
                if (r != null) rules.Add(r);
            }

            CombatRuleSet ruleSet = AssetDatabase.LoadAssetAtPath<CombatRuleSet>(DEFAULT_RULESET_PATH);
            if (ruleSet == null)
            {
                ruleSet = ScriptableObject.CreateInstance<CombatRuleSet>();
                ruleSet.rules = rules.ToArray();
                AssetDatabase.CreateAsset(ruleSet, DEFAULT_RULESET_PATH);
                Debug.Log($"{LOG} Created DefaultCombatRules asset at {DEFAULT_RULESET_PATH}");
            }
            else
            {
                ruleSet.rules = rules.ToArray();
                EditorUtility.SetDirty(ruleSet);
                Debug.Log($"{LOG} Updated DefaultCombatRules with {rules.Count} rules.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("CYBERNOMAD/Rules/Import from JSON", false, 200)]
        public static void ImportFromJson()
        {
            string path = EditorUtility.OpenFilePanel("Import Combat Rules JSON", "", "json");
            if (string.IsNullOrEmpty(path))
            {
                Debug.Log($"{LOG} Import cancelled.");
                return;
            }

            string json = File.ReadAllText(path);
            ImportFromJsonString(json, OUTPUT_DIR);
        }

        /// <summary>
        /// Import rules from a JSON string into the given output directory.
        /// Callable from other editor scripts (e.g. CI/codegen pipeline).
        /// </summary>
        public static int ImportFromJsonString(string json, string outputDir)
        {
            RuleJsonEntry[] entries;
            try
            {
                // JsonUtility does not support top-level arrays -- wrap it.
                string wrapped = "{\"rules\":" + json + "}";
                RuleJsonWrapper wrapper = JsonUtility.FromJson<RuleJsonWrapper>(wrapped);
                entries = wrapper.rules;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LOG} Failed to parse JSON: {ex.Message}");
                return 0;
            }

            if (entries == null || entries.Length == 0)
            {
                Debug.LogWarning($"{LOG} No rule entries found in JSON.");
                return 0;
            }

            // Ensure output directory exists.
            if (!AssetDatabase.IsValidFolder(outputDir))
            {
                string[] parts = outputDir.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            int created = 0;
            int updated = 0;

            foreach (RuleJsonEntry entry in entries)
            {
                string assetName = string.IsNullOrEmpty(entry.name)
                    ? $"Rule_{entry.source}_{entry.hitZone}"
                    : entry.name;

                string assetPath = $"{outputDir}/{assetName}.asset";

                // Load existing or create new.
                CombatRule rule = AssetDatabase.LoadAssetAtPath<CombatRule>(assetPath);
                bool isNew = rule == null;
                if (isNew)
                    rule = ScriptableObject.CreateInstance<CombatRule>();

                // Apply data.
                rule.sourceObjectType = ParseEnum<ObjectType>(entry.source, ObjectType.Any);
                rule.hitZone          = ParseEnum<HitZoneType>(entry.hitZone, HitZoneType.Any);
                rule.forceThreshold   = entry.threshold;
                rule.resultEffect     = ParseEnum<CombatEffectType>(entry.effect, CombatEffectType.None);
                rule.stunDuration     = entry.stunDuration;
                rule.knockBackForce   = entry.knockBackForce;
                rule.woundDamage      = entry.woundDamage;
                rule.description      = entry.description;

                if (isNew)
                {
                    AssetDatabase.CreateAsset(rule, assetPath);
                    created++;
                    Debug.Log($"{LOG} Created: {assetPath}");
                }
                else
                {
                    EditorUtility.SetDirty(rule);
                    updated++;
                    Debug.Log($"{LOG} Updated: {assetPath}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string summary = $"{LOG} Import complete. Created: {created}, Updated: {updated}";
            Debug.Log(summary);
            EditorUtility.DisplayDialog("Rule Import Complete",
                $"Created: {created}\nUpdated: {updated}\n\nOutput: {outputDir}", "OK");

            return created + updated;
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct, Enum
        {
            if (string.IsNullOrEmpty(value)) return fallback;
            if (Enum.TryParse<T>(value, ignoreCase: true, out T result))
                return result;
            Debug.LogWarning($"{LOG} Unknown enum value '{value}' for {typeof(T).Name}, using {fallback}.");
            return fallback;
        }

        // JSON serialization helpers.

        [Serializable]
        private class RuleJsonWrapper
        {
            public RuleJsonEntry[] rules;
        }

        [Serializable]
        private class RuleJsonEntry
        {
            public string name        = "";
            public string source      = "Any";
            public string hitZone     = "Any";
            public float  threshold   = 0f;
            public string effect      = "None";
            public float  stunDuration   = 2f;
            public float  knockBackForce = 5f;
            public float  woundDamage    = 10f;
            public string description = "";
        }
    }
}
#endif
#endif
