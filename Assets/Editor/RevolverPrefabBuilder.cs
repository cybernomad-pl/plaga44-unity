// =============================================================================
// RevolverPrefabBuilder.cs
// CYBERNOMAD -- Buduje gotowy runtime prefab rewolweru z surowego FBX w GameDevHQ.
// Prefab trafia do Assets/Resources/Items/ -> Resources.Load<GameObject>("Items/Revolver").
// Idempotentny: EnsurePrefab() rebuilduje tylko gdy brakuje pliku.
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
        private const string LOG = "[PLAGA44][RevolverBuilder]";
        private const string SourceFbx = "Assets/GameDevHQ/FileBase/3D/Props/Weapons/Revolver/FBX/Revolver.fbx";
        private const string ResourcesRoot = "Assets/Resources";
        private const string ItemsFolder = "Assets/Resources/Items";
        private const string PrefabPath = "Assets/Resources/Items/Revolver.prefab";
        private const string PrefabInstanceName = "Revolver";

        // Physics -- realistyczny rewolwer ~1.1 kg
        private const float RevolverMassKg = 1.1f;
        private const float LinearDamping = 0.5f;
        private const float AngularDamping = 0.8f;

        // Fallback bounds jesli brak MeshRenderer (mesh broken)
        private static readonly Vector3 FallbackBoundsSize = new Vector3(0.2f, 0.15f, 0.05f);

        [MenuItem("CYBERNOMAD/Inventory/Rebuild Revolver Prefab", false, 300)]
        public static void RebuildMenu()
        {
            if (BuildPrefab())
                Debug.Log($"{LOG} Prefab rebuilt: {PrefabPath}");
        }

        /// <summary>Build prefab if missing or outdated. Returns true if prefab exists after.</summary>
        public static bool EnsurePrefab()
        {
            if (File.Exists(PrefabPath)) return true;
            return BuildPrefab();
        }

        private static bool BuildPrefab()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFbx);
            if (fbx == null)
            {
                Debug.LogError($"{LOG} Source FBX not found: {SourceFbx}");
                return false;
            }

            EnsureResourcesItemsFolder();

            var instance = BuildRevolverInstance(fbx);
            var bounds = ComputeRendererBounds(instance);
            if (!SaveAsResourcesPrefab(instance, bounds))
                return false;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"{LOG} Built prefab: {PrefabPath} (bounds={bounds.size}, mass={RevolverMassKg}kg)");
            return true;
        }

        private static void EnsureResourcesItemsFolder()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesRoot))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(ItemsFolder))
                AssetDatabase.CreateFolder(ResourcesRoot, "Items");
        }

        private static GameObject BuildRevolverInstance(GameObject fbx)
        {
            var instance = Object.Instantiate(fbx);
            instance.name = PrefabInstanceName;
            AttachPhysics(instance);
            AttachCollider(instance, ComputeRendererBounds(instance));
            AttachFeedbackAndGrab(instance);
            return instance;
        }

        private static void AttachPhysics(GameObject instance)
        {
            var rb = instance.GetComponent<Rigidbody>() ?? instance.AddComponent<Rigidbody>();
            rb.mass = RevolverMassKg;
            rb.linearDamping = LinearDamping;
            rb.angularDamping = AngularDamping;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        private static void AttachCollider(GameObject instance, Bounds bounds)
        {
            var col = instance.GetComponent<BoxCollider>() ?? instance.AddComponent<BoxCollider>();
            col.center = instance.transform.InverseTransformPoint(bounds.center);
            col.size = bounds.size;
        }

        private static void AttachFeedbackAndGrab(GameObject instance)
        {
            if (instance.GetComponent<HapticOnGrab>() == null)
                instance.AddComponent<HapticOnGrab>();
            if (instance.GetComponent<PlagaGrabbable>() == null)
                instance.AddComponent<PlagaGrabbable>();
        }

        private static bool SaveAsResourcesPrefab(GameObject instance, Bounds bounds)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath, out bool success);
            Object.DestroyImmediate(instance);
            if (success && prefab != null) return true;
            Debug.LogError($"{LOG} Failed to save prefab at {PrefabPath}");
            return false;
        }

        private static Bounds ComputeRendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, FallbackBoundsSize);

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}
#endif
