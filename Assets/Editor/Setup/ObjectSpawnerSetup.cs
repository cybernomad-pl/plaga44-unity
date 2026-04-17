// =============================================================================
// ObjectSpawnerSetup.cs
// Stawia ObjectSpawner GO w scenie z domyslna konfiguracją (Revolver).
// Wywolywany przez Bootstrap.
// =============================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class ObjectSpawnerSetup
    {
        private const string LOG = "[PLAGA44][ObjectSpawnerSetup]";
        private const string SpawnerGoName = "_ObjectSpawner";

        public static bool Run(BootstrapConfig cfg)
        {
            var existing = Object.FindAnyObjectByType<ObjectSpawner>();
            if (existing != null)
            {
                Debug.Log($"{LOG} [OK] ObjectSpawner ({existing.spawnList.Count} entries)");
                return false;
            }

            var go = new GameObject(SpawnerGoName);
            Undo.RegisterCreatedObjectUndo(go, "Bootstrap: Add ObjectSpawner");
            var spawner = go.AddComponent<ObjectSpawner>();

            // Default loadout: Revolver
            spawner.spawnList = new List<ObjectSpawner.SpawnEntry>
            {
                new ObjectSpawner.SpawnEntry
                {
                    resourcePath = cfg.defaultSpawnItem,
                    offset = cfg.spawnerOffset,
                    autoRigidbody = true,
                    autoCollider = true,
                    autoGrabbable = true,
                    mass = 1.1f,
                    enabled = true
                }
            };
            spawner.spawnOnStart = true;

            Debug.Log($"{LOG} [ADDED] ObjectSpawner with default: {cfg.defaultSpawnItem}");
            return true;
        }
    }
}
#endif
