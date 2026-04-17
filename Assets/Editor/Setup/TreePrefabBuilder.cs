// =============================================================================
// TreePrefabBuilder.cs
// CYBERNOMAD -- Generates 3 simple placeholder tree prefabs for terrain:
//   - Tree_Birch  (slim trunk + light-green canopy)
//   - Tree_Oak    (medium trunk + dark-green canopy)
//   - Tree_Pine   (tall narrow trunk + cone-shaped dark canopy)
//
// Each prefab is: cylinder trunk (URP/Lit brown) + sphere/cone canopy (URP/Lit green).
// Placeholder quality -- replace with real SpeedTree / URP Environment assets later.
// Uses primitive geometry + shared URP/Lit materials.
// =============================================================================
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class TreePrefabBuilder
    {
        private const string LOG = "[PLAGA44][TreePrefabBuilder]";
        private const string TreesFolder = "Assets/PLAGA44/Trees";
        private const string MaterialsSubfolder = "Materials";
        private const string UrpLitShader = "Universal Render Pipeline/Lit";

        private static readonly Color TrunkColor = new Color(0.35f, 0.22f, 0.13f); // brown
        private static readonly Color LeafBirchColor = new Color(0.55f, 0.75f, 0.35f); // light green
        private static readonly Color LeafOakColor   = new Color(0.25f, 0.50f, 0.20f); // dark green
        private static readonly Color LeafPineColor  = new Color(0.15f, 0.40f, 0.25f); // darker green

        // Tree prefab definitions (name, trunk height/radius, canopy type+size, leaf color)
        private struct TreeDef
        {
            public string name;
            public float trunkHeight;
            public float trunkRadius;
            public CanopyShape canopy;
            public float canopyRadius;
            public float canopyHeight;
            public Color leafColor;
        }

        private enum CanopyShape { Sphere, Cone }

        private static readonly TreeDef[] Trees = new[]
        {
            new TreeDef { name = "Tree_Birch", trunkHeight = 6f, trunkRadius = 0.15f,
                          canopy = CanopyShape.Sphere, canopyRadius = 1.8f, canopyHeight = 3f,
                          leafColor = LeafBirchColor },
            new TreeDef { name = "Tree_Oak", trunkHeight = 5f, trunkRadius = 0.3f,
                          canopy = CanopyShape.Sphere, canopyRadius = 2.5f, canopyHeight = 3.5f,
                          leafColor = LeafOakColor },
            new TreeDef { name = "Tree_Pine", trunkHeight = 8f, trunkRadius = 0.2f,
                          canopy = CanopyShape.Cone, canopyRadius = 1.5f, canopyHeight = 5f,
                          leafColor = LeafPineColor },
        };

        [MenuItem("CYBERNOMAD/Content/Rebuild Tree Prefabs")]
        public static void RebuildMenu() => EnsurePrefabs(force: true);

        /// <summary>Ensures all 3 tree prefabs exist. Returns array of loaded GameObjects.</summary>
        public static GameObject[] EnsurePrefabs(bool force = false)
        {
            BootstrapUtils.EnsureFolder("Assets/PLAGA44", "Trees");
            BootstrapUtils.EnsureFolder(TreesFolder, MaterialsSubfolder);

            var trunkMat = EnsureMaterial("Tree_Trunk_Mat", TrunkColor);
            var result = new GameObject[Trees.Length];

            for (int i = 0; i < Trees.Length; i++)
            {
                string prefabPath = $"{TreesFolder}/{Trees[i].name}.prefab";
                if (!force && File.Exists(prefabPath))
                {
                    result[i] = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    Debug.Log($"{LOG} [OK] {Trees[i].name} (existing)");
                    continue;
                }

                var leafMat = EnsureMaterial($"Tree_Leaves_{Trees[i].name}_Mat", Trees[i].leafColor);
                result[i] = BuildPrefab(Trees[i], trunkMat, leafMat, prefabPath);
                Debug.Log($"{LOG} [BUILT] {prefabPath}");
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        private static Material EnsureMaterial(string name, Color color)
        {
            string path = $"{TreesFolder}/{MaterialsSubfolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find(UrpLitShader);
            if (shader == null)
            {
                Debug.LogError($"{LOG} Shader '{UrpLitShader}' not found");
                return null;
            }
            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static GameObject BuildPrefab(TreeDef def, Material trunkMat, Material leafMat, string prefabPath)
        {
            var root = new GameObject(def.name);
            try
            {
                BuildTrunk(root.transform, def, trunkMat);
                BuildCanopy(root.transform, def, leafMat);

                // Create LOD Group for terrain billboard system (optional but terrain expects it).
                // Simple: no LODs, just the whole tree at all distances.

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildTrunk(Transform parent, TreeDef def, Material mat)
        {
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(parent, worldPositionStays: false);
            // Unity cylinder primitive is 2m tall by default (-1 to +1). Scale to height/2.
            trunk.transform.localScale = new Vector3(def.trunkRadius * 2f, def.trunkHeight * 0.5f, def.trunkRadius * 2f);
            trunk.transform.localPosition = new Vector3(0f, def.trunkHeight * 0.5f, 0f);
            var mr = trunk.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
            // Remove default collider -- terrain trees don't need per-instance colliders
            var col = trunk.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        private static void BuildCanopy(Transform parent, TreeDef def, Material mat)
        {
            GameObject canopy;
            if (def.canopy == CanopyShape.Sphere)
            {
                canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                canopy.transform.localScale = new Vector3(def.canopyRadius * 2f, def.canopyHeight, def.canopyRadius * 2f);
            }
            else // Cone (we use a stretched capsule as approximation of cone)
            {
                canopy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                canopy.transform.localScale = new Vector3(def.canopyRadius * 2f, def.canopyHeight * 0.5f, def.canopyRadius * 2f);
            }
            canopy.name = "Canopy";
            canopy.transform.SetParent(parent, worldPositionStays: false);
            canopy.transform.localPosition = new Vector3(0f, def.trunkHeight + def.canopyHeight * 0.4f, 0f);
            var mr = canopy.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
            var col = canopy.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }
    }
}
#endif
