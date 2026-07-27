// =============================================================================
// BuildPlayerRigSetup.cs
// Ekstrahuje OVRCameraRig z TESTBED_V6 do prefabu (raz), potem instancjonuje go
// w aktywnej scenie. Robot, IK (CharacterRetargeter), crouch (LocomotionController),
// sterowanie, distance grab (OVRInteractionComprehensive), grabbery -- 1:1 z V6.
// Extract przez SaveAsPrefabAsset (nie Instantiate w additive) -- inaczej dwa
// aktywne OVRCameraRig na raz i "Only one instance" error.
// Menu: CYBERNOMAD/Tools/Build Player Rig.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plaga44.Editor.Setup
{
    public static class BuildPlayerRigSetup
    {
        private const string LOG = "[PLAGA44][BuildPlayerRig]";
        private const string OvrRigName = "OVRCameraRig";
        private const string MenuName = "_HamburgerMenu";
        private const string SourceScene = "Assets/PLAGA44/TESTBED_V6.unity";
        private const string PrefabPath = "Assets/PLAGA44/Prefabs/PlayerRig.prefab";
        private const string ConfigPath = "Assets/PLAGA44/Config/BootstrapConfig_Quest.asset";

        [MenuItem("CYBERNOMAD/Tools/Build Player Rig", false, 2)]
        public static void Run()
        {
            var rig = EnsureOvrCameraRig();
            if (rig == null) return;

            var cfg = AssetDatabase.LoadAssetAtPath<BootstrapConfig>(ConfigPath);
            if (cfg == null)
            {
                Debug.LogError($"{LOG} Brak {ConfigPath} -- nie dostroje pozycji rig.");
                return;
            }
            PlayerRigSetup.Run(cfg);

            EnsureHamburgerMenu();

            EditorUtility.SetDirty(rig);
            EditorSceneManager.MarkSceneDirty(rig.scene);
            Selection.activeGameObject = rig;
            SceneView.FrameLastActiveSceneView();
            Debug.Log($"{LOG} [OK] Player rig (z V6) + menu gotowe.");
        }

        private static GameObject EnsureOvrCameraRig()
        {
            var existing = GameObject.Find(OvrRigName);
            if (existing != null)
            {
                Debug.Log($"{LOG} [OK] {OvrRigName} juz w scenie.");
                return existing;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? ExtractFromV6();
            if (prefab == null)
            {
                Debug.LogError($"{LOG} brak {PrefabPath} i nie wyekstrahowano rig z V6");
                return null;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = OvrRigName;
            SceneManager.MoveGameObjectToScene(inst, SceneManager.GetActiveScene());
            Undo.RegisterCreatedObjectUndo(inst, "Instantiate PlayerRig");
            Debug.Log($"{LOG} [ADDED] {OvrRigName} z {PrefabPath}");
            return inst;
        }

        private static GameObject ExtractFromV6()
        {
            if (!System.IO.File.Exists(SourceScene))
            {
                Debug.LogError($"{LOG} zrodlowa scena nie istnieje: {SourceScene}");
                return null;
            }

            Scene src = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
            try
            {
                GameObject rigSrc = null;
                foreach (var root in src.GetRootGameObjects())
                    if (root.name == OvrRigName) { rigSrc = root; break; }
                if (rigSrc == null)
                {
                    Debug.LogError($"{LOG} brak {OvrRigName} w {SourceScene}");
                    return null;
                }

                int removed = RemoveMissingScripts(rigSrc.transform);
                if (removed > 0)
                    Debug.Log($"{LOG} usunieto {removed} missing script(ow) z rig przed zapisem");

                BootstrapUtils.EnsureFolder("Assets/PLAGA44", "Prefabs");
                var prefab = PrefabUtility.SaveAsPrefabAsset(rigSrc, PrefabPath, out bool ok);
                Debug.Log($"{LOG} [EXTRACTED] {OvrRigName} -> {PrefabPath}");
                return ok ? prefab : null;
            }
            finally { EditorSceneManager.CloseScene(src, true); }
        }

        private static int RemoveMissingScripts(Transform t)
        {
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            for (int i = 0; i < t.childCount; i++)
                removed += RemoveMissingScripts(t.GetChild(i));
            return removed;
        }

        private static void EnsureHamburgerMenu()
        {
            if (Object.FindFirstObjectByType<Plaga44.UI.HamburgerMenu>() != null)
            {
                Debug.Log($"{LOG} [OK] HamburgerMenu juz w scenie.");
                return;
            }
            var go = new GameObject(MenuName);
            Undo.RegisterCreatedObjectUndo(go, "Create _HamburgerMenu");
            Undo.AddComponent<Plaga44.UI.HamburgerMenu>(go);
            Debug.Log($"{LOG} [CREATED] {MenuName} + HamburgerMenu");
        }
    }
}
#endif
