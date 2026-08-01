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
        private const string ConfigPath = "Assets/PLAGA44/Config/BootstrapConfig.asset";
        private const string SessionKey = "Plaga44.Bootstrap.Done";

        static Bootstrap() => EditorApplication.update += WaitForReady;

        private static void WaitForReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            EditorApplication.update -= WaitForReady;

            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);

            Run();
        }

        [MenuItem("CYBERNOMAD/Bootstrap", false, 1)]
        public static void Run()
        {
            var cfg = LoadConfig();
            if (cfg == null) return;
            EditorApplication.delayCall += () => RunSetup(cfg);
        }

        private static BootstrapConfig LoadConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<BootstrapConfig>(ConfigPath);
            if (cfg != null) return cfg;

            BootstrapUtils.EnsureFolder("Assets/PLAGA44", "Config");
            cfg = ScriptableObject.CreateInstance<BootstrapConfig>();
            AssetDatabase.CreateAsset(cfg, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"{LOG} [CREATED] {ConfigPath}");
            return cfg;
        }

        private static void RunSetup(BootstrapConfig cfg)
        {
            var scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError($"{LOG} Brak zapisanej aktywnej sceny -- otworz i zapisz scene przed Bootstrap.");
                return;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            Debug.Log($"{LOG} === Setup START === scene={scene.name}");
            bool changed = false;

            changed |= LogStep("0-ClearScene", () => ClearScene(scene));

            // WHITEBOXING (2026-07-31): bialy zamkniety pokoj (konstruktor z Matrixa)
            // zamiast environmentu. Woda/teren/skybox/bounce-light USUNIETE.
            changed |= LogStep("1-WhiteRoom",          () => WhiteRoomSetup.Run(cfg));
            changed |= LogStep("3b-PostProcess",       () => PostProcessSetup.Run(cfg));
            LogStepVoid("5-BuildRig",                  () => BuildPlayerRigSetup.Run());
            changed |= LogStep("6-PlayerRig",          () => PlayerRigSetup.Run(cfg));
            changed |= LogStep("7-Inventory",          () => InventorySetup.Run(cfg));
            changed |= LogStep("7b-ItemGrab",          () => ItemGrabSetup.Run(cfg));
            changed |= LogStep("8-Singletons",         () => SceneSingletonsSetup.Run(cfg));
            changed |= LogStep("9-ObjectSpawner",      () => ObjectSpawnerSetup.Run(cfg));
            changed |= LogStep("10-NpcSpawner",        () => NpcSpawnerSetup.Run(cfg));
            changed |= LogStep("10b-NpcGrab",          () => NpcGrabSetup.Run(cfg));
            LogStepVoid("11-AvatarRegistry",           () => AvatarRegistrySetup.Run(cfg));

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveOpenScenes();
                Debug.Log($"{LOG} === Setup DONE, scene SAVED === ({sw.ElapsedMilliseconds}ms)");
            }
            else
            {
                Debug.Log($"{LOG} === Setup OK, no changes === ({sw.ElapsedMilliseconds}ms)");
            }
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

        private static bool ClearScene(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            if (roots.Length == 0) return false;
            foreach (var go in roots)
                Undo.DestroyObjectImmediate(go);
            Debug.Log($"{LOG}   scene wyczyszczona do 0 ({roots.Length} root GO)");
            return true;
        }

    }
}
#endif
