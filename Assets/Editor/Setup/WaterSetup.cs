#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plaga44.Editor.Setup
{
    public static class WaterSetup
    {
        private const string LOG = "[PLAGA44][WaterSetup]";
        private const string SourceScene = "Assets/PLAGA44/TESTBED_V6.unity";
        private const string PrefabPath = "Assets/PLAGA44/Prefabs/Environment.prefab";
        private const string Name = "Environment";

        public static bool Run(BootstrapConfig cfg)
        {
            if (GameObject.Find(Name) != null) return false;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? ExtractFromV6();
            if (prefab == null)
            {
                Debug.LogError($"{LOG} brak {PrefabPath} i nie sklonowano {Name} z V6");
                return false;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = Name;
            SceneManager.MoveGameObjectToScene(inst, SceneManager.GetActiveScene());
            return true;
        }

        private static GameObject ExtractFromV6()
        {
            if (!System.IO.File.Exists(SourceScene)) return null;

            Scene src = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
            try
            {
                GameObject envSrc = null;
                foreach (var root in src.GetRootGameObjects())
                    if (root.name == Name) { envSrc = root; break; }
                if (envSrc == null) return null;

                var clone = Object.Instantiate(envSrc);
                clone.name = Name;

                BootstrapUtils.EnsureFolder("Assets/PLAGA44", "Prefabs");
                var prefab = PrefabUtility.SaveAsPrefabAsset(clone, PrefabPath, out bool ok);
                Object.DestroyImmediate(clone);
                return ok ? prefab : null;
            }
            finally { EditorSceneManager.CloseScene(src, true); }
        }
    }
}
#endif
