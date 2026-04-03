#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class OpenScene
{
    [MenuItem("CYBERNOMAD/Scene/Open Scene_A (Terrain)", false, 1)]
    public static void OpenSceneA()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/FloodedGrounds/Scenes/Scene_A.unity", OpenSceneMode.Single);

        // Znajdz teren i ustaw kamere na nim
        var terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain != null)
        {
            Selection.activeGameObject = terrain.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log($"[PLAGA44] Scene_A opened. Terrain: {terrain.terrainData.size}");
        }
        else
        {
            Debug.Log("[PLAGA44] Scene_A opened. No terrain found.");
        }
    }
}
#endif
