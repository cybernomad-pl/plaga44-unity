// =============================================================================
// MigrateToISDK.cs
// CYBERNOMAD -- Editor helper do stopniowej migracji GameObjectow z
// TESTBED.unity -> TESTBED_ISDK.unity. Uzywa Unity SceneManager API, wiec
// fileID/references sa poprawnie zarzadzane (zero manual YAML edit).
//
// Workflow:
//   1. Menu: CYBERNOMAD/Migrate/Terrain+Light  (albo inne)
//   2. Otwiera obie sceny (TESTBED + TESTBED_ISDK) additively
//   3. MoveGameObjectToScene: target TESTBED_ISDK
//   4. SaveScene + CloseScene(TESTBED)
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plaga44.Editor.Setup
{
    public static class MigrateToISDK
    {
        private const string LOG        = "[PLAGA44][MigrateToISDK]";
        private const string SrcPath    = "Assets/PLAGA44/TESTBED.unity";
        private const string TargetPath = "Assets/PLAGA44/TESTBED_ISDK.unity";

        [MenuItem("CYBERNOMAD/Migrate/Terrain + Light")]
        public static void MigrateTerrainAndLight()
        {
            Migrate(new[] { "Terrain_SceneA", "Directional Light" });
        }

        private static void Migrate(string[] goNames)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo(true))
            {
                Debug.LogWarning($"{LOG} User cancelled save -- aborting migration.");
                return;
            }

            // Open target scene primary
            var targetScene = EditorSceneManager.OpenScene(TargetPath, OpenSceneMode.Single);
            if (!targetScene.IsValid())
            {
                Debug.LogError($"{LOG} Cannot open target scene: {TargetPath}");
                return;
            }

            // Open source additively
            var srcScene = EditorSceneManager.OpenScene(SrcPath, OpenSceneMode.Additive);
            if (!srcScene.IsValid())
            {
                Debug.LogError($"{LOG} Cannot open source scene: {SrcPath}");
                return;
            }

            int moved = 0;
            foreach (var name in goNames)
            {
                var found = FindInScene(srcScene, name);
                if (found == null)
                {
                    Debug.LogWarning($"{LOG} Not found in {SrcPath}: {name}");
                    continue;
                }
                SceneManager.MoveGameObjectToScene(found, targetScene);
                Debug.Log($"{LOG} Moved '{name}' -> {TargetPath}");
                moved++;
            }

            if (moved > 0)
            {
                EditorSceneManager.MarkSceneDirty(targetScene);
                EditorSceneManager.SaveScene(targetScene);
                Debug.Log($"{LOG} Saved {TargetPath} ({moved} GO moved)");
            }

            EditorSceneManager.CloseScene(srcScene, removeScene: true);
            Debug.Log($"{LOG} Done -- migrated {moved} GO");
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                var found = FindInChildren(root.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject FindInChildren(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child.gameObject;
                var found = FindInChildren(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
