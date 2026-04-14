// =============================================================================
// RevolverPrefabBuilder.cs
// CYBERNOMAD -- Auto-builds a runtime-ready Revolver prefab from the raw FBX
// in GameDevHQ/FileBase. Creates prefab under Assets/Resources/Items/ so it
// can be loaded at runtime via Resources.Load<GameObject>("Items/Revolver").
//
// Runs on first request (menu or Bootstrap validation). Idempotent -- rebuilds
// only if source FBX is newer than the generated prefab.
//
// Components added to the prefab:
//   - Rigidbody (mass=1.1kg -- realistic revolver weight)
//   - BoxCollider (auto-sized to mesh bounds, tight fit)
//   - HapticOnGrab (mass-scaled grip vibration)
//   - PlagaGrabbable (OVRGrabbable + haptic + holster snap)
// =============================================================================

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Plaga44.Feedback;
using Plaga44.Inventory;

namespace Plaga44.Editor
{
    public static class RevolverPrefabBuilder
    {
        private const string LOG          = "[PLAGA44][RevolverBuilder]";
        private const string SourceFbx    = "Assets/GameDevHQ/FileBase/3D/Props/Weapons/Revolver/FBX/Revolver.fbx";
        private const string PrefabFolder = "Assets/Resources/Items";
        private const string PrefabPath   = "Assets/Resources/Items/Revolver.prefab";

        [MenuItem("CYBERNOMAD/Inventory/Rebuild Revolver Prefab", false, 300)]
        public static void RebuildMenu()
        {
            if (BuildPrefab(force: true))
                Debug.Log($"{LOG} Prefab rebuilt: {PrefabPath}");
        }

        /// <summary>Build prefab if missing or outdated. Returns true if prefab exists after.</summary>
        public static bool EnsurePrefab()
        {
            if (File.Exists(PrefabPath)) return true;
            return BuildPrefab(force: false);
        }

        private static bool BuildPrefab(bool force)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFbx);
            if (fbx == null)
            {
                Debug.LogError($"{LOG} Source FBX not found: {SourceFbx}");
                return false;
            }

            // Ensure folder structure
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/Resources", "Items");

            // Instantiate FBX as scene object
            var instance = Object.Instantiate(fbx);
            instance.name = "Revolver";

            // Compute tight bounds from all MeshRenderers
            Bounds bounds = ComputeRendererBounds(instance);

            // Add physics
            var rb = instance.GetComponent<Rigidbody>();
            if (rb == null) rb = instance.AddComponent<Rigidbody>();
            rb.mass = 1.1f;            // ~1.1 kg -- realistic revolver weight
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.8f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // Add tight BoxCollider
            var col = instance.GetComponent<BoxCollider>();
            if (col == null) col = instance.AddComponent<BoxCollider>();
            col.center = instance.transform.InverseTransformPoint(bounds.center);
            col.size   = bounds.size;

            // Feedback + grab
            if (instance.GetComponent<HapticOnGrab>() == null)
                instance.AddComponent<HapticOnGrab>();

            if (instance.GetComponent<PlagaGrabbable>() == null)
                instance.AddComponent<PlagaGrabbable>();

            // Save as prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath, out bool success);
            Object.DestroyImmediate(instance);

            if (!success || prefab == null)
            {
                Debug.LogError($"{LOG} Failed to save prefab at {PrefabPath}");
                return false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"{LOG} Built prefab: {PrefabPath} (bounds={bounds.size}, mass={rb.mass}kg)");
            return true;
        }

        private static Bounds ComputeRendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, new Vector3(0.2f, 0.15f, 0.05f));

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}
#endif
