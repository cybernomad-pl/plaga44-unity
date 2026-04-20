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

        private static int _waitTicks;

        private static void WaitForReady()
        {
            _waitTicks++;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                if (_waitTicks % 300 == 0) // co ~5s log status (assuming ~60 ticks/s)
                    Debug.Log($"{LOG} WaitForReady: still waiting (compiling={EditorApplication.isCompiling}, updating={EditorApplication.isUpdating})");
                return;
            }

            EditorApplication.update -= WaitForReady;

            if (SessionState.GetBool(SessionKey, false))
            {
                Debug.Log($"{LOG} Auto-run SKIPPED -- already ran this session (key={SessionKey}). Use CYBERNOMAD/Bootstrap to re-run.");
                return;
            }
            SessionState.SetBool(SessionKey, true);

            Debug.Log($"{LOG} Auto-run: loading scene and validating... (waited {_waitTicks} ticks)");
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
            // Don't capture cfg reference -- OpenScene + asset reload can destroy
            // the ScriptableObject; reload it fresh inside RunSetup.
            EditorApplication.delayCall += () =>
            {
                var freshCfg = LoadConfig();
                if (freshCfg == null)
                {
                    Debug.LogError($"{LOG} Config unavailable after delayCall -- abort");
                    return;
                }
                RunSetup(freshCfg);
            };
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
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Debug.Log($"{LOG} === Setup START === scene={SceneManager.GetActiveScene().name}, cfg={cfg.name}");
            bool changed = false;

            LogStepVoid("MissingScriptCleaner",       () => MissingScriptCleaner.CleanActiveScene());
            changed |= LogStep("TerrainSetup",        () => TerrainSetup.Run(cfg));
            LogStepVoid("TerrainTreeCleaner",         () => TerrainTreeCleaner.CleanAll());
            changed |= LogStep("SkyboxSetup",         () => SkyboxSetup.Run(cfg));
            changed |= LogStep("FogSetup",            () => FogSetup.Run(cfg));
            changed |= LogStep("AmbientSetup",        () => AmbientSetup.Run(cfg));
            changed |= LogStep("BounceLightSetup",    () => BounceLightSetup.Run(cfg));
            changed |= LogStep("PlayerRigSetup",      () => PlayerRigSetup.Run(cfg));
            changed |= LogStep("InventorySetup",      () => InventorySetup.Run(cfg));
            changed |= LogStep("SceneSingletonsSetup",() => SceneSingletonsSetup.Run(cfg));
            changed |= LogStep("ObjectSpawnerSetup",  () => ObjectSpawnerSetup.Run(cfg));
            LogStepVoid("AvatarRegistrySetup",        () => AvatarRegistrySetup.Run(cfg));
            changed |= LogStep("LightingCleanup",     () => LightingTools.AutoClearIfNeeded());

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                Debug.Log($"{LOG} === Setup DONE, scene SAVED === ({sw.ElapsedMilliseconds}ms)");
            }
            else
            {
                Debug.Log($"{LOG} === Setup OK, no changes === ({sw.ElapsedMilliseconds}ms)");
            }

            FocusTerrain();
        }

        private static bool LogStep(string name, System.Func<bool> step)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                bool result = step();
                Debug.Log($"{LOG}   [{(result ? "CHANGED" : "OK")}] {name} ({sw.ElapsedMilliseconds}ms)");
                return result;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{LOG}   [FAIL] {name} threw {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        private static void LogStepVoid(string name, System.Action step)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                step();
                Debug.Log($"{LOG}   [DONE] {name} ({sw.ElapsedMilliseconds}ms)");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{LOG}   [FAIL] {name} threw {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
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
