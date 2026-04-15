// =============================================================================
// Bootstrap.cs
// CYBERNOMAD -- jeden entry point, orkiestrator setupu sceny PLAGA '44.
// Uruchamia sie automatycznie przy starcie edytora (InitializeOnLoad).
// Menu: CYBERNOMAD > Bootstrap
//
// Konfiguracja: Assets/PLAGA44/Config/BootstrapConfig_Quest.asset
// Setup rozdzielony na osobne klasy w Assets/Editor/Setup/
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Plaga44.Editor.Setup;

namespace Plaga44.Editor
{
    [InitializeOnLoad]
    public static class Bootstrap
    {
        private const string LOG = "[PLAGA44][Bootstrap]";
        private const string ConfigPath = "Assets/PLAGA44/Config/BootstrapConfig_Quest.asset";
        private const string SessionKey = "Plaga44.Bootstrap.Done";

        // =====================================================================
        // Entry points
        // =====================================================================

        static Bootstrap() => EditorApplication.delayCall += AutoRun;

        private static void AutoRun()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoRun;
                return;
            }

            Run();
        }

        [MenuItem("CYBERNOMAD/Bootstrap", false, 1)]
        public static void Run()
        {
            var cfg = LoadConfig();
            if (cfg == null) return;

            OpenScene(cfg);
            EditorApplication.delayCall += () => RunSetup(cfg);
        }

        // =====================================================================
        // Config
        // =====================================================================

        private static BootstrapConfig LoadConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<BootstrapConfig>(ConfigPath);
            if (cfg == null)
                Debug.LogError($"{LOG} Config not found: {ConfigPath}. Create via Assets > Create > PLAGA44 > Bootstrap Config.");
            return cfg;
        }

        // =====================================================================
        // Scene
        // =====================================================================

        private static void OpenScene(BootstrapConfig cfg)
        {
            var active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.path == cfg.scenePath) return;

            if (!System.IO.File.Exists(cfg.scenePath))
            {
                Debug.LogError($"{LOG} Scene not found: {cfg.scenePath}");
                return;
            }

            EditorSceneManager.OpenScene(cfg.scenePath, OpenSceneMode.Single);
            Debug.Log($"{LOG} Opened: {cfg.scenePath}");
        }

        // =====================================================================
        // Setup orchestration
        // =====================================================================

        private static void RunSetup(BootstrapConfig cfg)
        {
            Debug.Log($"{LOG} === Setup start ===");
            bool changed = false;

            changed |= TerrainSetup.Run(cfg);
            changed |= SkyboxSetup.Run(cfg);
            changed |= PlayerRigSetup.Run(cfg);
            changed |= InventorySetup.Run(cfg);
            changed |= SceneSingletonsSetup.Run(cfg);
            AvatarRegistrySetup.Run();

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                Debug.Log($"{LOG} === Setup done, scene saved ===");
            }
            else
            {
                Debug.Log($"{LOG} === Setup OK, nothing changed ===");
            }

            FocusTerrain();
        }

        private static void FocusTerrain()
        {
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) return;
            Selection.activeGameObject = terrain.gameObject;
            try { SceneView.lastActiveSceneView?.FrameSelected(); }
            catch { }
        }
    }
}
#endif
