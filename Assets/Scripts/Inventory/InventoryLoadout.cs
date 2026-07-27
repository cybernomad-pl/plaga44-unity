// =============================================================================
// InventoryLoadout.cs
// CYBERNOMAD -- Spawns initial items into player's holsters on game start.
//
// Works alongside PlayerInventory. Reads default loadout (e.g. Revolver in
// RightHip) and instantiates each prefab from Resources/Items/, then attaches
// it to the corresponding holster via HolsterAnchor.Holster().
//
// Auto-wires homeHolster on PlagaGrabbable so the item snaps back on release.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.Inventory
{
    [DefaultExecutionOrder(-40)] // after PlayerInventory (-50)
    [RequireComponent(typeof(PlayerInventory))]
    public class InventoryLoadout : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Loadout]";

        [System.Serializable]
        public class LoadoutEntry
        {
            public string holsterId = "RightHip";
            public string resourcePath = "Items/Shotgun";  // Resources.Load path
            public bool enabled = true;
        }

        [Header("Starting Loadout")]
        [Tooltip("DEPRECATED per issue #163 -- holster system disabled. Items now spawn in-hand via ObjectSpawner. Keep list empty or set enabled=false.")]
        public List<LoadoutEntry> startingItems = new List<LoadoutEntry>(); // empty by default

        [Header("Enable Loadout (deprecated)")]
        [Tooltip("Issue #163: holster loadout disabled by default. Enable only for legacy test.")]
        public bool enableLoadout = false;

        private IEnumerator Start()
        {
            if (!enableLoadout)
            {
                Debug.Log($"{LOG} DISABLED -- holster loadout deprecated (issue #163). Use ObjectSpawner instead.");
                yield break;
            }

            // Wait one frame so PlayerInventory.Awake() has created anchors.
            yield return null;

            var inv = PlayerInventory.Instance;
            if (inv == null)
            {
                Debug.LogError($"{LOG} PlayerInventory.Instance is null -- cannot load loadout.");
                yield break;
            }

            int ok = 0, failed = 0;
            foreach (var entry in startingItems)
            {
                if (!entry.enabled) continue;
                if (SpawnInto(entry)) ok++; else failed++;
            }
            Debug.Log($"{LOG} Loadout complete: {ok} items placed, {failed} failed.");
        }

        private bool SpawnInto(LoadoutEntry entry)
        {
            if (!TryResolveHolster(entry, out HolsterAnchor holster)) return false;
            if (!TryLoadPrefab(entry, out GameObject prefab)) return false;
            InstantiateAndAttach(prefab, holster, entry);
            return true;
        }

        private bool TryResolveHolster(LoadoutEntry entry, out HolsterAnchor holster)
        {
            holster = PlayerInventory.Instance.GetHolster(entry.holsterId);
            if (holster != null) return true;
            Debug.LogWarning($"{LOG} Holster '{entry.holsterId}' not found -- skipping {entry.resourcePath}");
            return false;
        }

        private bool TryLoadPrefab(LoadoutEntry entry, out GameObject prefab)
        {
            prefab = Resources.Load<GameObject>(entry.resourcePath);
            if (prefab != null) return true;
            Debug.LogWarning($"{LOG} Resource not found: {entry.resourcePath}");
            return false;
        }

        private void InstantiateAndAttach(GameObject prefab, HolsterAnchor holster, LoadoutEntry entry)
        {
            var item = Instantiate(prefab);
            item.name = prefab.name; // strip "(Clone)"

            // Wire homeHolster so it snaps back on release near this anchor.
            var grabbable = item.GetComponent<PlagaGrabbable>();
            if (grabbable != null) grabbable.homeHolster = holster;

            holster.Holster(item);
            Debug.Log($"{LOG} Spawned '{item.name}' into holster '{entry.holsterId}'");
        }
    }
}
