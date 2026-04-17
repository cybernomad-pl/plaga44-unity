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
            var reg = AssetDatabase.LoadAssetAtPath<AvatarRegistry>(cfg.avatarRegistryPath);

            // --- Auto-rescan when registry is missing or empty ---------------
            if (reg == null || reg.Count == 0)
            {
                string reason = reg == null ? "MISSING" : "EMPTY";
                Debug.Log($"{LOG} [{reason}] Triggering full avatar rescan...");
                AvatarAutoImport.ScanAllForce();

                // Reload after rescan
                reg = AssetDatabase.LoadAssetAtPath<AvatarRegistry>(cfg.avatarRegistryPath);
                if (reg == null || reg.Count == 0)
                {
                    Debug.LogWarning($"{LOG} [FAIL] Rescan finished but registry still {reason}. "
                        + "Check that DAE/FBX files exist in Assets/PLAGA44/Avatars/<Name>/ subfolders.");
                    return;
                }
            }

            // --- Report results -----------------------------------------------
            Debug.Log($"{LOG} [OK] {reg.Count} avatars");
            for (int i = 0; i < reg.Count; i++)
            {
                var e = reg.Get(i);
                Debug.Log($"{LOG}   [{i}] {(e?.name ?? "?")} -- {(e?.prefab != null ? "OK" : "MISSING")}");
            }
        }
    }
}
#endif
