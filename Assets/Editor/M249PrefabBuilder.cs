// =============================================================================
// M249PrefabBuilder.cs
// CYBERNOMAD -- Buduje runtime prefab M249 z looted FBX (bleeding-edge).
// Prefab -> Assets/Resources/Items/M249.prefab -> Resources.Load("Items/M249").
// Tworzy tez URP/Lit material (BaseMap + Normal), bo FBX nie ma gotowego .mat.
// Idempotentny: EnsurePrefab() buduje tylko gdy brak pliku.
// Wzorzec z RevolverPrefabBuilder (V8).
// =============================================================================
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Plaga44.Feedback;
using Plaga44.Inventory;

namespace Plaga44.Editor
{
    public static class M249PrefabBuilder
    {
        private const string LOG = "[PLAGA44][M249Builder]";
        private const string SourceFbx = "Assets/PLAGA44/Weapons/Models/M249/M249_low.fbx";
        private const string BaseMapPath = "Assets/PLAGA44/Weapons/Textures/M249/M249_low_M249 Gun_BaseMap.png";
        private const string NormalMapPath = "Assets/PLAGA44/Weapons/Textures/M249/M249_low_M249 Gun_Normal.png";
        private const string MaterialsFolder = "Assets/PLAGA44/Weapons/Materials";
        private const string MaterialPath = "Assets/PLAGA44/Weapons/Materials/M249.mat";
        private const string ResourcesRoot = "Assets/Resources";
        private const string ItemsFolder = "Assets/Resources/Items";
        private const string PrefabPath = "Assets/Resources/Items/M249.prefab";
        private const string PrefabInstanceName = "M249";

        // Physics -- M249 realnie ~7.5 kg; 5 kg dla znosniejszego handlingu w VR (tunable via ITEM GRIP).
        private const float MassKg = 5.0f;
        private const float LinearDamping = 0f;
        private const float AngularDamping = 0.05f;

        private const string UrpLitShader = "Universal Render Pipeline/Lit";
        private static readonly Vector3 FallbackBoundsSize = new Vector3(0.9f, 0.25f, 0.12f);

        [MenuItem("CYBERNOMAD/Inventory/Rebuild M249 Prefab", false, 301)]
        public static void RebuildMenu()
        {
            if (BuildPrefab())
                Debug.Log($"{LOG} Prefab rebuilt: {PrefabPath}");
        }

        // Auto-build once on editor load (after asset import) if prefab missing.
        // delayCall defers until AssetDatabase is ready, so the looted FBX is imported.
        [InitializeOnLoadMethod]
        private static void AutoBuildOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(PrefabPath)) return;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(SourceFbx) == null) return; // FBX not imported yet
                if (BuildPrefab())
                    Debug.Log($"{LOG} Auto-built on load: {PrefabPath}");
            };
        }

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
                Debug.LogError($"{LOG} Source FBX not found: {SourceFbx} -- czy loot sie zaimportowal?");
                return false;
            }

            EnsureResourcesItemsFolder();
            var mat = BuildMaterial();

            var instance = UnityEngine.Object.Instantiate(fbx);
            instance.name = PrefabInstanceName;

            ApplyMaterial(instance, mat);
            AttachPhysics(instance);
            AttachCollider(instance, ComputeRendererBounds(instance));
            AttachFeedbackAndGrab(instance);

            bool ok = SaveAsResourcesPrefab(instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (ok) Debug.Log($"{LOG} Built prefab: {PrefabPath} (mass={MassKg}kg, mat={(mat != null ? "OK" : "NULL")})");
            return ok;
        }

        // URP/Lit material z BaseMap + Normal. MaskMap (HDRP-style) pominiety --
        // BaseMap+Normal wystarczy zeby bron byla teksturowana. Metallic/smoothness
        // do dostrojenia pozniej jesli trzeba.
        private static Material BuildMaterial()
        {
            var shader = Shader.Find(UrpLitShader);
            if (shader == null)
            {
                Debug.LogError($"{LOG} Shader '{UrpLitShader}' not found -- czy URP jest w projekcie?");
                return null;
            }

            var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseMapPath);
            var normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalMapPath);
            if (baseMap == null) Debug.LogWarning($"{LOG} BaseMap not found: {BaseMapPath}");
            if (normalMap == null) Debug.LogWarning($"{LOG} Normal not found: {NormalMapPath}");

            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
                AssetDatabase.CreateFolder("Assets/PLAGA44/Weapons", "Materials");

            var mat = new Material(shader);
            if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
            if (normalMap != null)
            {
                mat.SetTexture("_BumpMap", normalMap);
                mat.EnableKeyword("_NORMALMAP");
            }

            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }

        private static void ApplyMaterial(GameObject instance, Material mat)
        {
            if (mat == null) return;
            foreach (var r in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        private static void EnsureResourcesItemsFolder()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesRoot))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(ItemsFolder))
                AssetDatabase.CreateFolder(ResourcesRoot, "Items");
        }

        private static void AttachPhysics(GameObject instance)
        {
            var rb = instance.GetComponent<Rigidbody>();
            if (rb == null) rb = instance.AddComponent<Rigidbody>();
            rb.mass = MassKg;
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
            UnityEngine.Object.DestroyImmediate(instance);
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
