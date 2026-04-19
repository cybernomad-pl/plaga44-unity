// =============================================================================
// TerrainTreeCleaner.cs
// CYBERNOMAD -- Usuwa broken (null prefab) tree prototypes z TerrainData.
// Terrain worker spamuje "Tree prefab at index X is missing" przy kazdym renderze
// gdy prototype.prefab == null (np. po usunieciu GameDevHQ/FloodedGrounds).
//
// Menu: CYBERNOMAD > Fix > Remove Missing Tree Prototypes
// =============================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class TerrainTreeCleaner
    {
        private const string LOG = "[PLAGA44][TerrainTreeCleaner]";

        [MenuItem("CYBERNOMAD/Fix/Remove Missing Tree Prototypes", false, 400)]
        public static void CleanAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:TerrainData");
            int cleaned = 0;
            int totalRemoved = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
                if (data == null) continue;

                int removed = Clean(data, path);
                if (removed > 0)
                {
                    cleaned++;
                    totalRemoved += removed;
                }
            }

            if (cleaned > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            string msg = $"TerrainTreeCleaner: {totalRemoved} missing prototype(s) removed across {cleaned} terrain(s).";
            Debug.Log($"{LOG} {msg}");
            EditorUtility.DisplayDialog("Tree Cleaner", msg, "OK");
        }

        private static int Clean(TerrainData data, string path)
        {
            var original = data.treePrototypes;
            if (original == null || original.Length == 0) return 0;

            var kept = new List<TreePrototype>(original.Length);
            var indexMap = new Dictionary<int, int>(); // oldIndex -> newIndex

            for (int i = 0; i < original.Length; i++)
            {
                if (original[i] != null && original[i].prefab != null)
                {
                    indexMap[i] = kept.Count;
                    kept.Add(original[i]);
                }
            }

            int removed = original.Length - kept.Count;
            if (removed == 0) return 0;

            // Filter instances: keep only those whose prototypeIndex survives, remap index
            var originalInstances = data.treeInstances;
            var keptInstances = new List<TreeInstance>();
            if (originalInstances != null)
            {
                foreach (var inst in originalInstances)
                {
                    if (indexMap.TryGetValue(inst.prototypeIndex, out int newIndex))
                    {
                        var updated = inst;
                        updated.prototypeIndex = newIndex;
                        keptInstances.Add(updated);
                    }
                }
            }

            Undo.RecordObject(data, "Remove missing tree prototypes");
            data.treePrototypes = kept.ToArray();
            data.treeInstances  = keptInstances.ToArray();
            EditorUtility.SetDirty(data);

            Debug.Log($"{LOG} [{path}] removed {removed} prototype(s); kept {kept.Count}, instances {originalInstances?.Length ?? 0} -> {keptInstances.Count}");
            return removed;
        }
    }
}
#endif
