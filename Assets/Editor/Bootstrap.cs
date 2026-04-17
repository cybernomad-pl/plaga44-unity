// =============================================================================
// Bootstrap.cs
// CYBERNOMAD -- jeden entry point, orkiestrator setupu sceny PLAGA '44.
// Uruchamia sie automatycznie przy starcie edytora (InitializeOnLoad).
//
// Menu:
//   CYBERNOMAD > Bootstrap          -- pelny setup sceny
//   StratoJump removed -- player spawns at last position.
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
        // Auto-run przy starcie edytora
        // =====================================================================

        // Unity 6: delayCall from [InitializeOnLoad] constructor is unreliable.
        // EditorApplication.update fires every editor tick -- we unhook after first call.
        static Bootstrap() => EditorApplication.update += WaitForReady;

        private static void WaitForReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            EditorApplication.update -= WaitForReady;

            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);

            Debug.Log($"{LOG} Auto-run: loading scene and validating...");
            Run();
        }

        // =====================================================================
        // Menu items
        // =====================================================================

        [MenuItem("CYBERNOMAD/Bootstrap", false, 1)]
        public static void Run()
        {
            var cfg = LoadConfig();
            if (cfg == null) return;
            OpenScene(cfg);
            EditorApplication.delayCall += () => RunSetup(cfg);
        }

        // StratoJump removed from menu -- player spawns at last saved position.

        // =====================================================================
        // Config
        // =====================================================================

        private static BootstrapConfig LoadConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<BootstrapConfig>(ConfigPath);
            if (cfg != null) return cfg;

            Debug.Log($"{LOG} Config not found -- creating default Quest config");
            BootstrapUtils.EnsureFolder("Assets/PLAGA44", "Config");
            cfg = ScriptableObject.CreateInstance<BootstrapConfig>();
            AssetDatabase.CreateAsset(cfg, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"{LOG} [CREATED] {ConfigPath}");
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
        // Setup
        // =====================================================================

        private static void RunSetup(BootstrapConfig cfg)
        {
            Debug.Log($"{LOG} === Setup start ===");
            bool changed = false;

            changed |= TerrainSetup.Run(cfg);
            changed |= SkyboxSetup.Run(cfg);
            changed |= BounceLightSetup.Run(cfg);
            changed |= PlayerRigSetup.Run(cfg);
            changed |= InventorySetup.Run(cfg);
            changed |= SceneSingletonsSetup.Run(cfg);
            changed |= ObjectSpawnerSetup.Run(cfg);
            AvatarRegistrySetup.Run(cfg);

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
