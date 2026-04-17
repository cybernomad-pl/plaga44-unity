// =============================================================================
// AvatarRegistrySetup.cs
// Sprawdza stan AvatarRegistry -- read only, nie modyfikuje.
// Wywolywany przez Bootstrap.
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
            if (reg == null)
            {
                Debug.LogWarning($"{LOG} [MISSING] AvatarRegistry not found at {cfg.avatarRegistryPath}. Run CYBERNOMAD > Import > Rescan Avatars.");
                return;
            }
            if (reg.Count == 0)
            {
                Debug.LogWarning($"{LOG} [EMPTY] 0 avatars. Drop DAE into Assets/PLAGA44/Avatars/<Name>/ and rescan.");
                return;
            }
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
