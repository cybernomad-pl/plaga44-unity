// MiscConfig.cs -- CYBERNOMAD Editor Tool
//
// Steruje reszta ProjectSettings ktore nie maja dedykowanego Configa:
//   - VFXManager, ShaderGraphSettings, XRSettings, URPProjectSettings
//   - MultiplayerManager, ClusterInputManager
//   - VersionControlSettings, UnityConnectSettings, PresetManager
//   - Physics2DSettings
//
// Generyczny dostep przez sciezke assetu + pole.
//
// Public API:
//   MiscConfig.SetInt("ProjectSettings/VFXManager.asset", "m_IndirectShader", 0);
//   MiscConfig.SetBool("ProjectSettings/Physics2DSettings.asset", "m_AutoSimulation", true);
//   MiscConfig.LogAsset("ProjectSettings/VFXManager.asset");

using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class MiscConfig
    {
        private const string LOG = "[PLAGA44]";

        // ---------------------------------------------------------------------
        // Generyczne settery -- dowolny ProjectSettings asset
        // ---------------------------------------------------------------------

        public static void SetInt(string assetPath, string field, int value)
        {
            var so = Load(assetPath); if (so == null) return;
            var p = so.FindProperty(field);
            if (p != null) { p.intValue = value; so.ApplyModifiedProperties(); }
            Debug.Log($"{LOG} {assetPath}: {field}={value}");
        }

        public static void SetFloat(string assetPath, string field, float value)
        {
            var so = Load(assetPath); if (so == null) return;
            var p = so.FindProperty(field);
            if (p != null) { p.floatValue = value; so.ApplyModifiedProperties(); }
            Debug.Log($"{LOG} {assetPath}: {field}={value}");
        }

        public static void SetBool(string assetPath, string field, bool value)
        {
            var so = Load(assetPath); if (so == null) return;
            var p = so.FindProperty(field);
            if (p != null) { p.boolValue = value; so.ApplyModifiedProperties(); }
            Debug.Log($"{LOG} {assetPath}: {field}={value}");
        }

        public static void SetString(string assetPath, string field, string value)
        {
            var so = Load(assetPath); if (so == null) return;
            var p = so.FindProperty(field);
            if (p != null) { p.stringValue = value; so.ApplyModifiedProperties(); }
            Debug.Log($"{LOG} {assetPath}: {field}={value}");
        }

        // ---------------------------------------------------------------------
        // Generyczny getter
        // ---------------------------------------------------------------------

        public static int GetInt(string assetPath, string field)
        {
            var so = Load(assetPath); if (so == null) return -1;
            var p = so.FindProperty(field);
            return p?.intValue ?? -1;
        }

        public static bool GetBool(string assetPath, string field)
        {
            var so = Load(assetPath); if (so == null) return false;
            var p = so.FindProperty(field);
            return p?.boolValue ?? false;
        }

        // ---------------------------------------------------------------------
        // Log -- wylistuj wszystkie top-level properties assetu
        // ---------------------------------------------------------------------

        public static void LogAsset(string assetPath)
        {
            var so = Load(assetPath);
            if (so == null) return;

            Debug.Log($"{LOG} {assetPath}:");
            var iter = so.GetIterator();
            iter.Next(true); // enter first child
            int depth = 0;
            int count = 0;
            while (iter.Next(depth == 0) && count < 50)
            {
                if (iter.depth > 1) continue;
                switch (iter.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        Debug.Log($"{LOG}   {iter.name} = {iter.intValue}");
                        break;
                    case SerializedPropertyType.Boolean:
                        Debug.Log($"{LOG}   {iter.name} = {iter.boolValue}");
                        break;
                    case SerializedPropertyType.Float:
                        Debug.Log($"{LOG}   {iter.name} = {iter.floatValue}");
                        break;
                    case SerializedPropertyType.String:
                        Debug.Log($"{LOG}   {iter.name} = \"{iter.stringValue}\"");
                        break;
                    default:
                        Debug.Log($"{LOG}   {iter.name} ({iter.propertyType})");
                        break;
                }
                count++;
            }
        }

        // ---------------------------------------------------------------------
        // Menu -- szybki podglad
        // ---------------------------------------------------------------------

        [MenuItem("CYBERNOMAD/Misc/Log VFXManager", false, 1)]
        static void MenuVFX() => LogAsset("ProjectSettings/VFXManager.asset");

        [MenuItem("CYBERNOMAD/Misc/Log Physics2D", false, 2)]
        static void MenuPhys2D() => LogAsset("ProjectSettings/Physics2DSettings.asset");

        [MenuItem("CYBERNOMAD/Misc/Log XRSettings", false, 3)]
        static void MenuXR() => LogAsset("ProjectSettings/XRSettings.asset");

        [MenuItem("CYBERNOMAD/Misc/Log ShaderGraph", false, 4)]
        static void MenuShaderGraph() => LogAsset("ProjectSettings/ShaderGraphSettings.asset");

        // ---------------------------------------------------------------------
        static SerializedObject Load(string path)
        {
            var obj = AssetDatabase.LoadAllAssetsAtPath(path);
            if (obj == null || obj.Length == 0) { Debug.LogError($"{LOG} {path} not found"); return null; }
            return new SerializedObject(obj[0]);
        }
    }
}
