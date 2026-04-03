#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Strip scene to bare minimum: terrain + water + skybox.
/// Remove trees, grass, buildings, prefabs, props -- everything else.
/// </summary>
public static class StripScene
{
    [MenuItem("CYBERNOMAD/Scene/Strip to Terrain+Water+Sky", false, 5)]
    public static void Strip()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Open Scene_A
        var scene = EditorSceneManager.OpenScene(
            "Assets/FloodedGrounds/Scenes/Scene_A.unity", OpenSceneMode.Single);

        int removed = 0;
        var toDestroy = new List<GameObject>();

        // Find everything to keep
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go == null) continue;
            // Skip children -- we destroy from root
            if (go.transform.parent != null) continue;

            string name = go.name.ToLower();

            // KEEP: terrain
            if (go.GetComponent<Terrain>() != null) continue;
            if (name.Contains("terrain")) continue;

            // KEEP: water
            if (name.Contains("water") || name.Contains("3d_water")) continue;

            // KEEP: lights (sun, directional)
            if (go.GetComponent<Light>() != null) continue;

            // KEEP: cameras
            if (go.GetComponent<Camera>() != null) continue;

            // KEEP: wind zone (affects water)
            if (go.GetComponent<WindZone>() != null) continue;

            // KEEP: post processing
            if (name.Contains("postprocess") || name.Contains("post process") || name.Contains("volume")) continue;

            // KEEP: event system
            if (name.Contains("eventsystem")) continue;

            // REMOVE everything else
            toDestroy.Add(go);
        }

        foreach (var go in toDestroy)
        {
            if (go != null)
            {
                string n = go.name;
                Object.DestroyImmediate(go);
                removed++;
            }
        }

        // Remove terrain trees and grass details
        var terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain != null && terrain.terrainData != null)
        {
            var td = terrain.terrainData;

            // Remove trees
            int treeCount = td.treeInstanceCount;
            td.treeInstances = new TreeInstance[0];
            td.RefreshPrototypes();
            Debug.Log($"[PLAGA44] Removed {treeCount} trees from terrain");

            // Remove grass/details
            int detailLayers = td.detailPrototypes.Length;
            for (int i = 0; i < detailLayers; i++)
            {
                int[,] empty = new int[td.detailResolution, td.detailResolution];
                td.SetDetailLayer(0, 0, i, empty);
            }
            Debug.Log($"[PLAGA44] Cleared {detailLayers} detail layers (grass)");

            terrain.Flush();
        }

        Debug.Log($"[PLAGA44] STRIPPED: removed {removed} root objects. Keeping terrain + water + sky.");

        // Save as new scene
        EditorSceneManager.SaveScene(
            EditorSceneManager.GetActiveScene(),
            "Assets/PLAGA44_CLEAN.unity");
        Debug.Log("[PLAGA44] Saved as Assets/PLAGA44_CLEAN.unity");

        // Update build settings
        var guid = AssetDatabase.AssetPathToGUID("Assets/PLAGA44_CLEAN.unity");
        var buildScenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene("Assets/PLAGA44_CLEAN.unity", true)
        };
        EditorBuildSettings.scenes = buildScenes;
        Debug.Log("[PLAGA44] Build Settings updated to PLAGA44_CLEAN.unity");
    }
}
#endif
