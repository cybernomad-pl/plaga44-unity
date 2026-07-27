#if UNITY_EDITOR
// =============================================================================
// PostProcessSetup.cs
// Krok Bootstrap: przenosi PostProcess_Volume z V6 do aktywnej sceny.
// Wzorzec identyczny jak Terrain/Water -- ekstrakt roota z V6 RAZ do prefabu,
// potem instancjonuj. ZERO fallbackow: brak roota w V6 -> LogError + return.
// =============================================================================
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plaga44.Editor.Setup
{
    public static class PostProcessSetup
    {
        private const string LOG = "[PLAGA44][PostProcessSetup]";
        private const string SourceScene = "Assets/PLAGA44/TESTBED_V6.unity";
        private const string PrefabPath = "Assets/PLAGA44/Prefabs/PostProcess_Volume.prefab";
        private const string Name = "PostProcess_Volume";

        public static bool Run(BootstrapConfig cfg)
        {
            if (GameObject.Find(Name) != null) return false;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? ExtractFromV6();
            if (prefab == null)
            {
                Debug.LogError($"{LOG} brak {PrefabPath} i nie wyekstrahowano {Name} z V6");
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
                GameObject srcGo = null;
                foreach (var root in src.GetRootGameObjects())
                    if (root.name == Name) { srcGo = root; break; }
                if (srcGo == null) return null;

                var clone = Object.Instantiate(srcGo);
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
