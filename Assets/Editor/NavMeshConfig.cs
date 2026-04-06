// NavMeshConfig.cs -- CYBERNOMAD Editor Tool
//
// Steruje: ProjectSettings/NavMeshAreas.asset
//
// Public API:
//   NavMeshConfig.SetAreaCost("Walkable", 1.0f);
//   NavMeshConfig.SetAreaCost("Water", 5.0f);
//   NavMeshConfig.LogCurrent();

using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class NavMeshConfig
    {
        private const string LOG = "[PLAGA44]";
        private const string ASSET = "ProjectSettings/NavMeshAreas.asset";

        // ---------------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------------

        /// <summary>Ustaw koszt nawigacji dla danego area index (0-31).</summary>
        public static void SetAreaCost(int areaIndex, float cost)
        {
            var so = LoadAsset();
            if (so == null) return;

            var areas = so.FindProperty("areas");
            if (areas == null || areaIndex >= areas.arraySize) return;

            var area = areas.GetArrayElementAtIndex(areaIndex);
            var costProp = area.FindPropertyRelative("cost");
            if (costProp != null) costProp.floatValue = cost;

            so.ApplyModifiedProperties();
            Debug.Log($"{LOG} NavMesh area[{areaIndex}] cost={cost}");
        }

        /// <summary>Ustaw nazwe area.</summary>
        public static void SetAreaName(int areaIndex, string name)
        {
            var so = LoadAsset();
            if (so == null) return;

            var areas = so.FindProperty("areas");
            if (areas == null || areaIndex >= areas.arraySize) return;

            var area = areas.GetArrayElementAtIndex(areaIndex);
            var nameProp = area.FindPropertyRelative("name");
            if (nameProp != null) nameProp.stringValue = name;

            so.ApplyModifiedProperties();
            Debug.Log($"{LOG} NavMesh area[{areaIndex}] name={name}");
        }

        // ---------------------------------------------------------------------
        // Log
        // ---------------------------------------------------------------------

        public static void LogCurrent()
        {
            var so = LoadAsset();
            if (so == null) return;

            var areas = so.FindProperty("areas");
            if (areas == null) return;

            Debug.Log($"{LOG} NavMesh Areas ({areas.arraySize}):");
            for (int i = 0; i < areas.arraySize; i++)
            {
                var area = areas.GetArrayElementAtIndex(i);
                var name = area.FindPropertyRelative("name");
                var cost = area.FindPropertyRelative("cost");
                if (name != null && !string.IsNullOrEmpty(name.stringValue))
                    Debug.Log($"{LOG}   [{i}] {name.stringValue} cost={cost?.floatValue}");
            }
        }

        // ---------------------------------------------------------------------
        // Menu
        // ---------------------------------------------------------------------

        [MenuItem("CYBERNOMAD/Status/NavMesh", false, 100)]
        static void MenuShow() => LogCurrent();

        // ---------------------------------------------------------------------
        static SerializedObject LoadAsset()
        {
            var obj = AssetDatabase.LoadAllAssetsAtPath(ASSET);
            if (obj == null || obj.Length == 0) { Debug.LogError($"{LOG} {ASSET} not found"); return null; }
            return new SerializedObject(obj[0]);
        }
    }
}
