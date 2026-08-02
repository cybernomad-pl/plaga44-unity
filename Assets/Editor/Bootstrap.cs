#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Plaga44.Editor.Setup;

namespace Plaga44.Editor
{
    // BOOTSTRAP DISABLED (2026-08-02, decyzja Borysa):
    // Nowe podejscie -- praca na TESTBED_BASE (klon Meta sampla), scena ma
    // zostac NIETKNIETA. Auto-run ([InitializeOnLoad] + static ctor) USUNIETY:
    // krok 0-ClearScene kasowal wszystkie root GO aktywnej sceny przy kazdym
    // starcie edytora. Bootstrap odpala sie TYLKO recznie z menu CYBERNOMAD.
    public static class Bootstrap
    {
        private const string LOG = "[PLAGA44][Bootstrap]";
        private const string ConfigPath = "Assets/PLAGA44/Config/BootstrapConfig.asset";

        [MenuItem("CYBERNOMAD/Bootstrap (DISABLED -- manual only)", false, 1)]
        public static void Run()
        {
            if (!EditorUtility.DisplayDialog("Bootstrap DISABLED",
                "Bootstrap jest wylaczony (praca na TESTBED_BASE).\n" +
                "Uruchomienie WYCZYSCI i przebuduje aktywna scene:\n" +
                SceneManager.GetActiveScene().name + "\n\nNa pewno?",
                "TAK, przebuduj scene", "Anuluj"))
            {
                Debug.Log($"{LOG} Anulowano (bootstrap disabled).");
                return;
            }
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
