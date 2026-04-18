// =============================================================================
// TerrainSetup.cs
// Stawia teren z Scene_A_Terrain.asset, przypisuje material i warstwy terenu
// z FloodedGrounds (Asphalt, Dirt). Wywolywany przez Bootstrap.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class TerrainSetup
    {
        private const string LOG = "[PLAGA44][TerrainSetup]";
        private const string MissingShaderMarker = "Hidden/InternalErrorShader";
        private const string TerrainLitShader = "Universal Render Pipeline/Terrain/Lit";

        public static bool Run(BootstrapConfig cfg)
        {
            bool changed = false;
            var terrain = Object.FindFirstObjectByType<Terrain>();

            if (terrain == null)
            {
                terrain = CreateTerrain(cfg);
                if (terrain == null) return false;
                changed = true;
            }
            else
            {
                Debug.Log($"{LOG} [OK] Terrain: {terrain.name} ({terrain.terrainData.size})");
            }

            changed |= SetupMaterial(terrain, cfg);
            changed |= SetupTerrainLayers(terrain, cfg);
            changed |= SetupTerrainScale(terrain, cfg);
            changed |= ClearTreePrototypes(terrain);
            return changed;
        }

        // Issue #178: trees deprecated -- clear broken/placeholder treePrototypes.
        // Borys will plant trees manually via Terrain tools if needed later.
        private static bool ClearTreePrototypes(Terrain terrain)
        {
            var data = terrain.terrainData;
            bool hasPrototypes = data.treePrototypes != null && data.treePrototypes.Length > 0;
            bool hasInstances = data.treeInstances != null && data.treeInstances.Length > 0;
            if (!hasPrototypes && !hasInstances)
            {
                Debug.Log($"{LOG} [OK] Tree prototypes: 0, instances: 0 (clean)");
                return false;
            }

            Undo.RecordObject(data, "Bootstrap: Clear Tree Prototypes");
            data.treePrototypes = new TreePrototype[0];
            data.treeInstances = new TreeInstance[0];

            // CRITICAL: persist to asset file -- Scene save alone doesn't save TerrainData asset.
            // Without this, tree warnings reappear every scene reload (issue #178 hotfix).
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);

            Debug.Log($"{LOG} [FIX] Cleared tree prototypes + instances + saved asset (issue #178)");
            return true;
        }

        // Scales terrain horizontally (X,Z) by cfg.terrainHorizontalScale.
        // Modifies TerrainData asset -- changes are persistent across sessions.
        private static bool SetupTerrainScale(Terrain terrain, BootstrapConfig cfg)
        {
            if (Mathf.Approximately(cfg.terrainHorizontalScale, 1.0f))
            {
                Debug.Log($"{LOG} [OK] Terrain scale: 1.0 (default)");
                return false;
            }

            var data = terrain.terrainData;
            Vector3 currentSize = data.size;
            // Target size: we want default base size (1024x1024) * scale.
            // We derive "base" by checking if current is already scaled.
            // For simplicity: assume default base = 1024. Target = 1024 * scale.
            const float BaseSize = 1024f;
            float targetX = BaseSize * cfg.terrainHorizontalScale;
            float targetZ = BaseSize * cfg.terrainHorizontalScale;

            if (Mathf.Approximately(currentSize.x, targetX) && Mathf.Approximately(currentSize.z, targetZ))
            {
                Debug.Log($"{LOG} [OK] Terrain size already {currentSize.x:F0}x{currentSize.z:F0} (scale={cfg.terrainHorizontalScale:F1})");
                return false;
            }

            Debug.Log($"{LOG} [FIX] Scaling terrain: {currentSize.x:F0}x{currentSize.z:F0} -> {targetX:F0}x{targetZ:F0} (scale={cfg.terrainHorizontalScale:F1})");
            Undo.RecordObject(data, "Bootstrap: Scale Terrain");
            data.size = new Vector3(targetX, currentSize.y, targetZ);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            return true;
        }

        // ---- Tworzenie terenu -----------------------------------------------

        private static Terrain CreateTerrain(BootstrapConfig cfg)
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(cfg.terrainAssetPath);
            if (data == null)
            {
                Debug.LogError($"{LOG} [MISSING] TerrainData not found: {cfg.terrainAssetPath}");
                return null;
            }

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain_SceneA";
            // Pozycja: bez centrowania -- teren ustawiamy w (0,0,0), gracz startuje ponad nim
            go.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(go, "Bootstrap: Add Terrain");

            Debug.Log($"{LOG} [ADDED] Terrain {data.size.x:F0}x{data.size.z:F0}m at (0,0,0)");
            return go.GetComponent<Terrain>();
        }

        // ---- Material -------------------------------------------------------

        private static bool SetupMaterial(Terrain terrain, BootstrapConfig cfg)
        {
            if (HasValidMaterial(terrain))
            {
                Debug.Log($"{LOG} [OK] Material: {terrain.materialTemplate.name}");
                return false;
            }

            Debug.LogWarning($"{LOG} [FIX] Material missing or pink -- assigning");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(cfg.terrainMaterialPath);
            if (mat != null)
            {
                terrain.materialTemplate = mat;
                Debug.Log($"{LOG} [OK] Assigned {mat.name}");
                return true;
            }

            return CreateMaterial(terrain, cfg);
        }

        private static bool HasValidMaterial(Terrain terrain)
        {
            var mat = terrain.materialTemplate;
            return mat != null && mat.shader != null && mat.shader.name != MissingShaderMarker;
        }

        private static bool CreateMaterial(Terrain terrain, BootstrapConfig cfg)
        {
            var shader = Shader.Find(TerrainLitShader);
            if (shader == null)
            {
                Debug.LogError($"{LOG} [ERROR] Shader '{TerrainLitShader}' not found");
                return false;
            }

            BootstrapUtils.EnsureFolder("Assets/PLAGA44", "Materials");
            var mat = new Material(shader) { name = "TerrainLit" };
            AssetDatabase.CreateAsset(mat, cfg.terrainMaterialPath);
            AssetDatabase.SaveAssets();
            terrain.materialTemplate = mat;
            Debug.Log($"{LOG} [ADDED] Created TerrainLit.mat");
            return true;
        }

        // ---- Terrain layers (Asphalt, Dirt z FloodedGrounds) ----------------

        private static bool SetupTerrainLayers(Terrain terrain, BootstrapConfig cfg)
        {
            var data = terrain.terrainData;
            // Sprawdz czy sa warstwy -- ale ignoruj tablice pelna nulli (usuniety asset)
            if (data.terrainLayers != null && data.terrainLayers.Length > 0
                && System.Array.Exists(data.terrainLayers, l => l != null))
            {
                Debug.Log($"{LOG} [OK] Terrain layers: {data.terrainLayers.Length}");
                return false;
            }

            var layerGuids = AssetDatabase.FindAssets("t:TerrainLayer", new[] { cfg.terrainLayersFolder });
            if (layerGuids.Length == 0)
            {
                Debug.LogWarning($"{LOG} [MISSING] No TerrainLayer assets in {cfg.terrainLayersFolder}");
                return false;
            }

            var layers = new TerrainLayer[layerGuids.Length];
            for (int i = 0; i < layerGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(layerGuids[i]);
                layers[i] = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                Debug.Log($"{LOG}   layer[{i}]: {layers[i]?.name ?? "NULL"} ({path})");
            }

            data.terrainLayers = layers;
            Debug.Log($"{LOG} [ADDED] {layers.Length} terrain layers assigned");
            return true;
        }

    }
}
#endif
