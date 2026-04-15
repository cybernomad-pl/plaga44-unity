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
            return changed;
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
