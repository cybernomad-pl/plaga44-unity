// =============================================================================
// PlayerInventory.cs
// CYBERNOMAD -- Manages inventory holster anchors on the player rig.
//
// Auto-creates holster anchors as children of the OVRCameraRig (tracking space),
// positioned via local offsets. Anchors follow the player in world space but
// stay locked to the rig (not head-tracked -- body-relative).
//
// Default layout:
//   - RightHip   -- primary sidearm (revolver)
//   - LeftHip    -- secondary tool
//   - Chest      -- optional (e.g. ammo pouch)
//   - Back       -- optional (e.g. rifle)
//
// Runtime spawn: GetHolster(id).Holster(go) to place an item.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.Inventory
{
    [DefaultExecutionOrder(-50)]
    public class PlayerInventory : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Inventory]";

        [System.Serializable]
        public class HolsterDef
        {
            public string id;
            public Vector3 localPosition;
            public Vector3 localEulerRotation;
            public float snapRadius = 0.2f;
            public bool enabled = true;
        }

        [Header("Holster Layout (local to rig)")]
        public List<HolsterDef> holsterDefs = new List<HolsterDef>
        {
            new HolsterDef {
                id = "RightHip",
                localPosition      = new Vector3( 0.18f, 0.95f,  0.05f),
                localEulerRotation = new Vector3(0f, 0f, 90f),
                snapRadius = 0.20f
            },
            new HolsterDef {
                id = "LeftHip",
                localPosition      = new Vector3(-0.18f, 0.95f,  0.05f),
                localEulerRotation = new Vector3(0f, 0f, -90f),
                snapRadius = 0.20f,
                enabled = false
            },
            new HolsterDef {
                id = "Chest",
                localPosition      = new Vector3(0.00f, 1.35f, -0.10f),
                localEulerRotation = new Vector3(0f, 180f, 0f),
                snapRadius = 0.18f,
                enabled = false
            },
            new HolsterDef {
                id = "Back",
                localPosition      = new Vector3(0.00f, 1.30f, -0.20f),
                localEulerRotation = new Vector3(0f, 180f, 0f),
                snapRadius = 0.22f,
                enabled = false
            },
        };

        public static PlayerInventory Instance { get; private set; }

        private readonly Dictionary<string, HolsterAnchor> _anchors = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildAnchors();
        }

        private void BuildAnchors()
        {
            _anchors.Clear();
            foreach (var def in holsterDefs)
            {
                if (!def.enabled) continue;

                var go = new GameObject($"Holster_{def.id}");
                go.transform.SetParent(transform, worldPositionStays: false);
                go.transform.localPosition = def.localPosition;
                go.transform.localRotation = Quaternion.Euler(def.localEulerRotation);

                var anchor = go.AddComponent<HolsterAnchor>();
                anchor.holsterId  = def.id;
                anchor.snapRadius = def.snapRadius;

                _anchors[def.id] = anchor;
                Debug.Log($"{LOG} Created holster: {def.id} at local={def.localPosition}");
            }

            Debug.Log($"{LOG} {_anchors.Count} holsters ready");
        }

        public HolsterAnchor GetHolster(string id)
        {
            return _anchors.TryGetValue(id, out var a) ? a : null;
        }
    }
}
