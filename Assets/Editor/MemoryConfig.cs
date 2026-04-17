// MemoryConfig.cs -- CYBERNOMAD Editor Tool
//
// Steruje: ProjectSettings/MemorySettings.asset
//
// Public API:
//   MemoryConfig.LogCurrent();
//   MemoryConfig.SetValue("m_EditorMemorySettings.m_MainAllocatorBlockSize", 16777216);

using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class MemoryConfig
    {
        private const string LOG = "[PLAGA44]";
        private const string ASSET = "ProjectSettings/MemorySettings.asset";

        public static void SetValue(string field, int value)
        {
            var so = LoadAsset(); if (so == null) return;
            var p = so.FindProperty(field);
            if (p != null) { p.intValue = value; so.ApplyModifiedProperties(); }
            Debug.Log($"{LOG} Memory tweak: {field}={value}");
        }

        public static void LogCurrent()
        {
            var so = LoadAsset();
            if (so == null) return;
            Debug.Log($"{LOG} MemorySettings: use Inspector for full view (complex nested structure)");
        }

        [MenuItem("CYBERNOMAD/Config/Memory/Show Current", false, 100)]
        static void MenuShow() => LogCurrent();

        static SerializedObject LoadAsset()
        {
            var obj = AssetDatabase.LoadAllAssetsAtPath(ASSET);
            if (obj == null || obj.Length == 0) { Debug.LogError($"{LOG} {ASSET} not found"); return null; }
            return new SerializedObject(obj[0]);
        }
    }
}
