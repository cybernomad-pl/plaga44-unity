// =============================================================================
// ShotgunPrefabBuilder.cs
// CYBERNOMAD -- Buduje gotowy runtime prefab shotguna z FBX w
// Assets/PLAGA44/Items/Shotgun/FBX/Low.fbx (GameDevHQ FileBase).
// Prefab trafia do Assets/Resources/Items/Shotgun.prefab -> loadowany przez
// ItemBrowser (Content Editor) i ObjectSpawner przez Resources.Load("Items/Shotgun").
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
    public static class ShotgunPrefabBuilder
    {
        private const string LOG               = "[PLAGA44][ShotgunBuilder]";
        private const string SourceFbx         = "Assets/PLAGA44/Items/Shotgun/FBX/Low.fbx";
        private const string ResourcesRoot     = "Assets/Resources";
        private const string ItemsFolder       = "Assets/Resources/Items";
        private const string PrefabPath        = "Assets/Resources/Items/Shotgun.prefab";
        private const string PrefabInstanceName = "Shotgun";

        // Physics -- shotgun ~3.5kg realistic
        private const float ShotgunMassKg  = 3.5f;
        private const float LinearDamping  = 0f;
        private const float AngularDamping = 0.05f;

        private static readonly Vector3 FallbackBoundsSize = new Vector3(0.8f, 0.15f, 0.05f);

        // Wywolywane automatycznie przez InventorySetup (polityka "wszystko automatycznie").
        // Bez menu item. Rebuild = usun Assets/Resources/Items/Shotgun.prefab.

        /// <summary>Build prefab if missing. Returns true if prefab exists after.</summary>
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

            var instance = BuildShotgunInstance(fbx);
            var bounds = ComputeRendererBounds(instance);
            AttachPhysics(instance);
            AttachCollider(instance, bounds);
            AttachFeedbackAndGrab(instance);

            if (!SaveAsResourcesPrefab(instance)) return false;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"{LOG} Built prefab: {PrefabPath} (bounds={bounds.size}, mass={ShotgunMassKg}kg)");
            return true;
        }

        private static void EnsureResourcesItemsFolder()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesRoot))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(ItemsFolder))
                AssetDatabase.CreateFolder(ResourcesRoot, "Items");
        }

        private static GameObject BuildShotgunInstance(GameObject fbx)
        {
            var instance = Object.Instantiate(fbx);
            instance.name = PrefabInstanceName;
            return instance;
        }

        private static void AttachPhysics(GameObject instance)
        {
            var rb = instance.GetComponent<Rigidbody>();
            if (rb == null) rb = instance.AddComponent<Rigidbody>();
            rb.mass = ShotgunMassKg;
            rb.linearDamping = LinearDamping;
            rb.angularDamping = AngularDamping;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        private static void AttachCollider(GameObject instance, Bounds bounds)
        {
            var col = instance.GetComponent<BoxCollider>();
            if (col == null) col = instance.AddComponent<BoxCollider>();
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

        private static bool SaveAsResourcesPrefab(GameObject instance)
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
