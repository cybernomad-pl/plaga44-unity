// LayersConfig.cs -- CYBERNOMAD Editor Tool
//
// Steruje: ProjectSettings/TagManager.asset (layers, tags, sorting layers)
//
// Public API:
//   LayersConfig.Apply(LayersConfig.INITIAL);
//   LayersConfig.AddLayer("Interactable", 8);
//   LayersConfig.AddTag("Enemy");
//   LayersConfig.LogCurrent();

using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public struct LayerSettings
    {
        public string[] tags;
        // (layerIndex, name) -- indices 0-7 are reserved by Unity
        public (int index, string name)[] layers;
    }

    public static class LayersConfig
    {
        private const string LOG = "[PLAGA44]";
        private const string ASSET = "ProjectSettings/TagManager.asset";

        // ---------------------------------------------------------------------
        // Presety
        // ---------------------------------------------------------------------

        public static readonly LayerSettings INITIAL = new LayerSettings
        {
            tags = new[] { "Player", "Enemy", "NPC", "Weapon", "Pickup", "Interactable", "Trigger" },
            layers = new[]
            {
                (8,  "Player"),
                (9,  "Enemy"),
                (10, "NPC"),
                (11, "Interactable"),
                (12, "Ground"),
                (13, "Projectile"),
                (14, "Trigger"),
                (15, "Hand"),
            },
        };

        // ---------------------------------------------------------------------
        // Apply all
        // ---------------------------------------------------------------------

        public static void Apply(LayerSettings s)
        {
            var so = LoadAsset();
            if (so == null) return;

            // Tags
            if (s.tags != null)
            {
                var tagsProp = so.FindProperty("tags");
                foreach (var tag in s.tags)
                    AddToArray(tagsProp, tag);
            }

            // Layers
            if (s.layers != null)
            {
                var layersProp = so.FindProperty("layers");
                foreach (var (index, name) in s.layers)
                {
                    if (index < 0 || index > 31) continue;
                    var element = layersProp.GetArrayElementAtIndex(index);
                    if (string.IsNullOrEmpty(element.stringValue))
                    {
                        element.stringValue = name;
                        Debug.Log($"{LOG} Layer {index} = {name}");
                    }
                    else if (element.stringValue != name)
                    {
                        Debug.LogWarning($"{LOG} Layer {index} already '{element.stringValue}', skipping '{name}'");
                    }
                }
            }

            so.ApplyModifiedProperties();
            Debug.Log($"{LOG} Layers/Tags applied.");
        }

        // ---------------------------------------------------------------------
        // Single value
        // ---------------------------------------------------------------------

        public static void AddLayer(string name, int index)
        {
            if (index < 8 || index > 31) { Debug.LogError($"{LOG} Layer index must be 8-31"); return; }
            var so = LoadAsset(); if (so == null) return;
            var prop = so.FindProperty("layers").GetArrayElementAtIndex(index);
            if (!string.IsNullOrEmpty(prop.stringValue))
            {
                Debug.LogWarning($"{LOG} Layer {index} already '{prop.stringValue}'");
                return;
            }
            prop.stringValue = name;
            so.ApplyModifiedProperties();
            Debug.Log($"{LOG} Layer {index} = {name}");
        }

        public static void AddTag(string tag)
        {
            var so = LoadAsset(); if (so == null) return;
            var tagsProp = so.FindProperty("tags");
            AddToArray(tagsProp, tag);
            so.ApplyModifiedProperties();
            Debug.Log($"{LOG} Tag added: {tag}");
        }

        public static void RemoveLayer(int index)
        {
            if (index < 8 || index > 31) return;
            var so = LoadAsset(); if (so == null) return;
            var prop = so.FindProperty("layers").GetArrayElementAtIndex(index);
            string old = prop.stringValue;
            prop.stringValue = "";
            so.ApplyModifiedProperties();
            Debug.Log($"{LOG} Layer {index} cleared (was '{old}')");
        }

        // ---------------------------------------------------------------------
        // Log
        // ---------------------------------------------------------------------

        public static void LogCurrent()
        {
            var so = LoadAsset(); if (so == null) return;

            Debug.Log($"{LOG} Tags:");
            var tagsProp = so.FindProperty("tags");
            for (int i = 0; i < tagsProp.arraySize; i++)
                Debug.Log($"{LOG}   {tagsProp.GetArrayElementAtIndex(i).stringValue}");

            Debug.Log($"{LOG} Custom Layers:");
            var layersProp = so.FindProperty("layers");
            for (int i = 8; i < layersProp.arraySize && i < 32; i++)
            {
                string val = layersProp.GetArrayElementAtIndex(i).stringValue;
                if (!string.IsNullOrEmpty(val))
                    Debug.Log($"{LOG}   [{i}] {val}");
            }
        }

        // ---------------------------------------------------------------------
        // Menu
        // ---------------------------------------------------------------------

        [MenuItem("CYBERNOMAD/Presets/Quest/Layers INITIAL", false, 1)]
        static void MenuInitial() => Apply(INITIAL);

        [MenuItem("CYBERNOMAD/Status/Layers "CYBERNOMAD/Layers & Tags/Show Current" Tags", false, 100)]
        static void MenuShow() => LogCurrent();

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        static SerializedObject LoadAsset()
        {
            var obj = AssetDatabase.LoadAllAssetsAtPath(ASSET);
            if (obj == null || obj.Length == 0) { Debug.LogError($"{LOG} {ASSET} not found"); return null; }
            return new SerializedObject(obj[0]);
        }

        static void AddToArray(SerializedProperty array, string value)
        {
            for (int i = 0; i < array.arraySize; i++)
                if (array.GetArrayElementAtIndex(i).stringValue == value) return;
            array.InsertArrayElementAtIndex(array.arraySize);
            array.GetArrayElementAtIndex(array.arraySize - 1).stringValue = value;
        }
    }
}
