#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// CYBERNOMAD > Scene > Clean Scene
/// Strips current scene to terrain + water + sky only.
/// Run AFTER "Load PLAGA 44 Demo" to get a minimal walking test scene.
/// </summary>
public static class CleanScene
{
    private const string LOG = "[PLAGA44] CleanScene:";

    [MenuItem("CYBERNOMAD/Scene/Clean Scene (terrain+water+sky only)", false, 20)]
    public static void Clean()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        int removed = 0;
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (var go in roots)
        {
            if (go == null) continue;
            string n = go.name.ToLowerInvariant();

            // KEEP: terrain, water, sky, environment container, sun, post-process, OVR rig, audio
            if (n.Contains("terrain") ||
                n.Contains("water") || n.Contains("3d_water") ||
                n.Contains("sun") || n.Contains("light") ||
                n.Contains("fog") ||
                n == "environment" || n == "floodedgrounds" ||
                n.Contains("postprocess") || n.Contains("post_process") ||
                n.Contains("volume") ||
                n.Contains("ovr") || n.Contains("player") ||
                n.Contains("camera") ||
                n.Contains("audio") || n.Contains("vibration") ||
                n.Contains("eventsystem") || n.Contains("event system"))
                continue;

            Debug.Log($"{LOG} Removing root: '{go.name}'");
            Undo.DestroyObjectImmediate(go);
            removed++;
        }

        // Also clean inside Environment container
        var env = GameObject.Find("Environment") ?? GameObject.Find("FloodedGrounds");
        if (env != null)
        {
            for (int i = env.transform.childCount - 1; i >= 0; i--)
            {
                var child = env.transform.GetChild(i).gameObject;
                string cn = child.name.ToLowerInvariant();

                if (cn.Contains("terrain") || cn.Contains("water") || cn.Contains("3d_water") ||
                    cn.Contains("sun") || cn.Contains("fog") || cn.Contains("light"))
                    continue;

                Debug.Log($"{LOG} Removing child: '{child.name}'");
                Undo.DestroyObjectImmediate(child);
                removed++;
            }
        }

        // Remove any weapon spawns left in scene
        var allGOs = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go2 in allGOs)
        {
            if (go2 == null) continue;
            string n2 = go2.name.ToLowerInvariant();
            if (n2.Contains("spawn") || n2.Contains("sword") || n2.Contains("gun") ||
                n2.Contains("m249") || n2.Contains("pistol") || n2.Contains("weapon") ||
                n2.Contains("enemy") || n2.Contains("npc") || n2.Contains("pinea") ||
                n2.Contains("exhibition") || n2.Contains("benchmark"))
            {
                Debug.Log($"{LOG} Removing: '{go2.name}'");
                Undo.DestroyObjectImmediate(go2);
                removed++;
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"{LOG} Done. Removed {removed} objects. Only terrain+water+sky remain.");
    }
}
#endif
