// BuildScenesConfig.cs -- CYBERNOMAD Editor Tool
//
// Steruje: ProjectSettings/EditorBuildSettings.asset (build scenes)
//
// Public API:
//   BuildScenesConfig.SetScenes(new[] { "Assets/TESTBED_V2.unity" });
//   BuildScenesConfig.AddScene("Assets/Scenes/Level1.unity");
//   BuildScenesConfig.RemoveScene("Assets/Scenes/Old.unity");
//   BuildScenesConfig.EnableScene("Assets/TESTBED_V2.unity", true);
//   BuildScenesConfig.LogCurrent();

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class BuildScenesConfig
    {
        private const string LOG = "[PLAGA44]";

        // ---------------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------------

        /// <summary>Zastap cala liste scen buildu.</summary>
        public static void SetScenes(string[] scenePaths)
        {
            var scenes = scenePaths.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            EditorBuildSettings.scenes = scenes;
            Debug.Log($"{LOG} Build scenes set: {string.Join(", ", scenePaths)}");
        }

        /// <summary>Dodaj scene do buildu (na koncu listy, enabled).</summary>
        public static void AddScene(string scenePath)
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (list.Any(s => s.path == scenePath))
            {
                Debug.Log($"{LOG} Scene already in build: {scenePath}");
                return;
            }
            list.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log($"{LOG} Scene added to build: {scenePath}");
        }

        /// <summary>Usun scene z buildu.</summary>
        public static void RemoveScene(string scenePath)
        {
            var list = EditorBuildSettings.scenes.ToList();
            int removed = list.RemoveAll(s => s.path == scenePath);
            if (removed > 0)
            {
                EditorBuildSettings.scenes = list.ToArray();
                Debug.Log($"{LOG} Scene removed from build: {scenePath}");
            }
        }

        /// <summary>Wlacz/wylacz scene w buildzie (bez usuwania).</summary>
        public static void EnableScene(string scenePath, bool enabled)
        {
            var list = EditorBuildSettings.scenes.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].path == scenePath)
                {
                    var s = list[i];
                    s.enabled = enabled;
                    list[i] = s;
                    EditorBuildSettings.scenes = list.ToArray();
                    Debug.Log($"{LOG} Scene {(enabled ? "enabled" : "disabled")}: {scenePath}");
                    return;
                }
            }
            Debug.LogWarning($"{LOG} Scene not found in build: {scenePath}");
        }

        /// <summary>Zwraca liste scen w buildzie.</summary>
        public static List<(string path, bool enabled)> GetScenes()
        {
            return EditorBuildSettings.scenes
                .Select(s => (s.path, s.enabled))
                .ToList();
        }

        // ---------------------------------------------------------------------
        // Log
        // ---------------------------------------------------------------------

        public static void LogCurrent()
        {
            var scenes = EditorBuildSettings.scenes;
            Debug.Log($"{LOG} Build scenes ({scenes.Length}):");
            for (int i = 0; i < scenes.Length; i++)
                Debug.Log($"{LOG}   [{i}] {(scenes[i].enabled ? "ON " : "OFF")} {scenes[i].path}");
        }

        // ---------------------------------------------------------------------
        // Menu
        // ---------------------------------------------------------------------

        [MenuItem("CYBERNOMAD/Config/Build Scenes/Show Current", false, 100)]
        static void MenuShow() => LogCurrent();
    }
}
