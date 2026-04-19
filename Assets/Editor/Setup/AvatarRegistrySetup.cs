// =============================================================================
// AvatarRegistrySetup.cs
// Validates AvatarRegistry. If missing or empty, triggers a full rescan
// (build prefabs from DAE/FBX + rebuild registry). Called by Bootstrap.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class AvatarRegistrySetup
    {
        private const string LOG = "[PLAGA44][AvatarRegistrySetup]";

        public static void Run(BootstrapConfig cfg)
        {
            // CYBERNOMAD: Bootstrap ZAWSZE wykonuje pelny rescan.
            // Raz na sesje (Bootstrap sam ma SessionKey guard), wiec overhead akceptowalny.
            // Gwarantuje: nowe avatary sa zauwazone, uszkodzone matt (np. missing _SPECULAR_SETUP)
            // sa przebudowane, keywordy/workflow zgodne z ApplySpecularWorkflowDefaults.
            Debug.Log($"{LOG} Triggering unconditional rescan (Bootstrap auto-reimport policy)...");
            ForceReimportAvatarModels();
            AvatarAutoImport.ScanAllForce();

            var reg = AssetDatabase.LoadAssetAtPath<AvatarRegistry>(cfg.avatarRegistryPath);
            if (reg == null || reg.Count == 0)
            {
                Debug.LogWarning($"{LOG} [FAIL] Registry is empty after rescan. "
                    + "Check that DAE/FBX files exist in Assets/PLAGA44/Avatars/<Name>/ subfolders.");
                return;
            }

            // --- Report results -----------------------------------------------
            Debug.Log($"{LOG} [OK] {reg.Count} avatars");
            for (int i = 0; i < reg.Count; i++)
            {
                var e = reg.Get(i);
                string status = e?.broken == true ? $"BROKEN: {e.errorMessage}" : (e?.prefab != null ? "OK" : "MISSING");
                Debug.Log($"{LOG}   [{i}] {(e?.name ?? "?")} -- {status}");
            }
        }

        // Forces ModelImporter to rebuild DAE/FBX assets so meta changes (avatarSetup, etc.)
        // are applied. Without this, ConfigureDae preprocessor settings stay in meta but
        // animator.avatar remains null because the actual import wasn't re-run.
        private static void ForceReimportAvatarModels()
        {
            const string root = "Assets/PLAGA44/Avatars";
            if (!AssetDatabase.IsValidFolder(root)) return;

            string[] daeGuids = AssetDatabase.FindAssets("t:Model", new[] { root });
            int reimported = 0;
            foreach (var guid in daeGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".dae", System.StringComparison.OrdinalIgnoreCase)
                    && !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                reimported++;
            }
            Debug.Log($"{LOG} Reimported {reimported} avatar models with ForceUpdate");
        }
    }
}
#endif
