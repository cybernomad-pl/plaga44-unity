// =============================================================================
// ExoRetargeterSetup.cs
// CYBERNOMAD -- Dodaje CharacterRetargeter + MetaSourceDataProvider do
// avatarow w Assets/PLAGA44/Avatars/ uzywajac Meta SDK Building Block.
//
// Mechanizm:
//   1. Instancjonuje prefab avatara w aktualnej scenie
//   2. Selection = instance
//   3. ExecuteMenuItem("GameObject/Movement SDK/Body Tracking/Add Character Retargeter")
//      -- Meta SDK dodaje komponenty + auto-konfiguruje bone mapping
//   4. ApplyPrefabInstance -> zapisuje zmiany w oryginalnym prefab
//   5. DestroyImmediate instance
//
// Menu: CYBERNOMAD > Fix > Setup Retargeter on Mixamo Avatars
// =============================================================================
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plaga44.Editor.Setup
{
    public static class ExoRetargeterSetup
    {
        private const string LOG = "[PLAGA44][ExoRetargeter]";
        private const string AvatarsRoot = "Assets/PLAGA44/Avatars";
        private const string MetaMenuItem = "GameObject/Movement SDK/Body Tracking/Add Character Retargeter";

        // Typ komponenty sprawdzany przez string (unikamy asmdef refu do Meta package).
        private const string RetargeterTypeName = "Meta.XR.Movement.Retargeting.CharacterRetargeter";

        [MenuItem("CYBERNOMAD/Fix/Setup Retargeter on Mixamo Avatars", false, 430)]
        public static void SetupAllAvatars()
        {
            if (!AssetDatabase.IsValidFolder(AvatarsRoot))
            {
                Debug.LogWarning($"{LOG} {AvatarsRoot} not found");
                return;
            }

            int configured = 0;
            int skipped = 0;
            foreach (var dir in Directory.GetDirectories(AvatarsRoot))
            {
                string name = Path.GetFileName(dir);
                string prefabPath = Path.Combine(dir, name + ".prefab").Replace('\\', '/');
                if (!File.Exists(prefabPath)) continue;

                var result = EnsureRetargeter(prefabPath);
                if (result == Result.Configured) configured++;
                else if (result == Result.AlreadyHas) skipped++;
            }

            string msg = $"Retargeter setup: {configured} configured, {skipped} already had retargeter.";
            Debug.Log($"{LOG} {msg}");
            EditorUtility.DisplayDialog("Retargeter Setup", msg, "OK");
        }

        public enum Result { Configured, AlreadyHas, Failed }

        /// <summary>Dodaje retargeter do prefaba. Bezpiecznie (idempotent).</summary>
        public static Result EnsureRetargeter(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { Debug.LogWarning($"{LOG} Prefab not found: {prefabPath}"); return Result.Failed; }

            // Check if already has retargeter (by type name via reflection, cross-asm)
            if (HasComponentByTypeName(prefab, RetargeterTypeName))
            {
                Debug.Log($"{LOG} [OK] {prefab.name} already has CharacterRetargeter");
                return Result.AlreadyHas;
            }

            // Validate: must be Humanoid
            var animator = prefab.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning($"{LOG} [SKIP] {prefab.name} -- not Humanoid (required for retargeter)");
                return Result.Failed;
            }

            // Instantiate in active scene temporarily so Meta menu item can operate
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError($"{LOG} No active scene -- open a scene first");
                return Result.Failed;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = prefab.name + "_RetargeterSetup";
            Selection.activeGameObject = instance;

            // Execute Meta SDK menu item (installs retargeter via BlockData.ContextMenuInstall)
            bool ok = EditorApplication.ExecuteMenuItem(MetaMenuItem);
            if (!ok)
            {
                Debug.LogError($"{LOG} ExecuteMenuItem failed: '{MetaMenuItem}' (Meta SDK not installed?)");
                Object.DestroyImmediate(instance);
                return Result.Failed;
            }

            // Verify retargeter was added
            if (!HasComponentByTypeName(instance, RetargeterTypeName))
            {
                Debug.LogError($"{LOG} Menu item ran but no retargeter added on {instance.name}");
                Object.DestroyImmediate(instance);
                return Result.Failed;
            }

            // Apply instance changes back to prefab asset
            PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(instance);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{LOG} [OK] Configured retargeter on {prefab.name}");
            return Result.Configured;
        }

        private static bool HasComponentByTypeName(GameObject go, string typeName)
        {
            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue; // missing scripts
                if (c.GetType().FullName == typeName) return true;
            }
            return false;
        }
    }
}
#endif
