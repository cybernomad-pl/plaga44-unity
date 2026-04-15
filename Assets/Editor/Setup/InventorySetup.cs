// =============================================================================
// InventorySetup.cs
// Dodaje HapticManager, PlayerInventory, InventoryLoadout i OVRGrabber
// na obu dloniach. Wywolywany przez Bootstrap.
// =============================================================================
#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Plaga44.Feedback;
using Plaga44.Inventory;

namespace Plaga44.Editor.Setup
{
    public static class InventorySetup
    {
        private const string LOG = "[PLAGA44][InventorySetup]";
        private const string OvrRigName = "OVRCameraRig";
        private const string RightAnchor = "RightHandAnchor";
        private const string LeftAnchor = "LeftHandAnchor";
        private const string GrabVolumeName = "GrabVolume";

        public static bool Run(BootstrapConfig cfg)
        {
            if (!RevolverPrefabBuilder.EnsurePrefab())
                Debug.LogWarning($"{LOG} [WARN] Revolver prefab missing -- loadout will fail");

            var rig = GameObject.Find(OvrRigName);
            if (rig == null)
            {
                Debug.LogWarning($"{LOG} [MISSING] {OvrRigName} not found -- skipping");
                return false;
            }

            bool changed = false;
            changed |= AddIfMissing<HapticManager>(rig, "HapticManager");
            changed |= AddIfMissing<PlayerInventory>(rig, "PlayerInventory");
            changed |= AddIfMissing<InventoryLoadout>(rig, "InventoryLoadout");
            changed |= SetupGrabber(rig, RightAnchor, OVRInput.Controller.RTouch, cfg);
            changed |= SetupGrabber(rig, LeftAnchor, OVRInput.Controller.LTouch, cfg);
            return changed;
        }

        private static bool AddIfMissing<T>(GameObject go, string label) where T : Component
        {
            if (go.GetComponent<T>() != null)
            {
                Debug.Log($"{LOG} [OK] {label}");
                return false;
            }
            Undo.AddComponent<T>(go);
            Debug.Log($"{LOG} [ADDED] {label}");
            return true;
        }

        private static bool SetupGrabber(GameObject rig, string anchorName, OVRInput.Controller ctrl, BootstrapConfig cfg)
        {
            var anchor = FindChild(rig.transform, anchorName);
            if (anchor == null)
            {
                Debug.LogWarning($"{LOG} [MISSING] {anchorName} -- grabber skipped");
                return false;
            }
            if (anchor.GetComponent<OVRGrabber>() != null)
            {
                Debug.Log($"{LOG} [OK] OVRGrabber on {anchorName}");
                return false;
            }

            var volume = CreateGrabVolume(anchor, cfg.grabVolumeRadius);
            EnsureKinematicRigidbody(anchor.gameObject);
            var grabber = anchor.gameObject.AddComponent<OVRGrabber>();

            if (!ConfigureGrabber(grabber, anchor, volume, ctrl))
            {
                Debug.LogError($"{LOG} [SDK BREAK] OVRGrabber on {anchorName} not configured -- removing. Check field names.");
                Object.DestroyImmediate(grabber);
                Object.DestroyImmediate(volume.gameObject);
                var rb = anchor.GetComponent<Rigidbody>();
                if (rb != null) Object.DestroyImmediate(rb);
                return false;
            }

            Undo.RegisterCreatedObjectUndo(volume.gameObject, $"Bootstrap: GrabVolume {anchorName}");
            Debug.Log($"{LOG} [ADDED] OVRGrabber on {anchorName} ({ctrl})");
            return true;
        }

        private static SphereCollider CreateGrabVolume(Transform parent, float radius)
        {
            var go = new GameObject(GrabVolumeName);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = radius;
            return col;
        }

        private static void EnsureKinematicRigidbody(GameObject go)
        {
            if (go.GetComponent<Rigidbody>() != null) return;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // OVRGrabber ma protected fieldy -- ustawiamy przez reflection.
        // Zwraca false jesli SDK zmienilo nazwy pol.
        private static bool ConfigureGrabber(OVRGrabber grabber, Transform grip, Collider volume, OVRInput.Controller ctrl)
        {
            var t = typeof(OVRGrabber);
            const BindingFlags f = BindingFlags.NonPublic | BindingFlags.Instance;
            bool ok = true;
            ok &= SetField(t, grabber, "m_gripTransform", grip, f);
            ok &= SetField(t, grabber, "m_grabVolumes", new Collider[] { volume }, f);
            ok &= SetField(t, grabber, "m_controller", ctrl, f);
            ok &= SetField(t, grabber, "m_parentHeldObject", true, f);
            return ok;
        }

        private static bool SetField(Type t, object target, string name, object value, BindingFlags flags)
        {
            var field = t.GetField(name, flags);
            if (field == null)
            {
                Debug.LogError($"{LOG} [SDK BREAK] {t.Name}.{name} not found");
                return false;
            }
            field.SetValue(target, value);
            return true;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
#endif
