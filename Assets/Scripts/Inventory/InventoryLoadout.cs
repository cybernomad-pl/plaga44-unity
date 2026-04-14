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
            public string resourcePath = "Items/Revolver";  // Resources.Load path
            public bool enabled = true;
        }

        [Header("Starting Loadout")]
        public List<LoadoutEntry> startingItems = new List<LoadoutEntry>
        {
            new LoadoutEntry { holsterId = "RightHip", resourcePath = "Items/Revolver", enabled = true }
        };

        private IEnumerator Start()
        {
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
            var holster = PlayerInventory.Instance.GetHolster(entry.holsterId);
            if (holster == null)
            {
                Debug.LogWarning($"{LOG} Holster '{entry.holsterId}' not found -- skipping {entry.resourcePath}");
                return false;
            }

            var prefab = Resources.Load<GameObject>(entry.resourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"{LOG} Resource not found: {entry.resourcePath}");
                return false;
            }

            var item = Instantiate(prefab);
            item.name = prefab.name;  // strip "(Clone)"

            // Wire homeHolster so it snaps back on release near this anchor.
            var grabbable = item.GetComponent<PlagaGrabbable>();
            if (grabbable != null) grabbable.homeHolster = holster;

            holster.Holster(item);
            Debug.Log($"{LOG} Spawned '{item.name}' into holster '{entry.holsterId}'");
            return true;
        }
    }
}
