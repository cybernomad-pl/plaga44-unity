#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plaga44.Editor.Setup
{
    // Teren proceduralnie z TerrainData (te sama co V6/Flooded: Scene_A_Terrain,
    // warstwy Asphalt/Dirt/Moss). Bez otwierania scen zrodlowych -- Flooded ma
    // 86 missing prefabow, klonowanie z niej sie wywala.
    public static class TerrainSetup
    {
        private const string LOG = "[PLAGA44][TerrainSetup]";
        private const string TerrainDataPath = "Assets/Potok/Terrain/Scene_A_Terrain.asset";
        private const string TerrainMatPath = "Assets/PLAGA44/Materials/TerrainLit.mat";
        private const string Name = "Terrain_SceneA";
        private static readonly Vector3 Pos = new Vector3(-512f, 0f, -512f); // pozycja z V6

        public static bool Run(BootstrapConfig cfg)
        {
            if (Object.FindFirstObjectByType<Terrain>() != null) return false;

            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null)
            {
                Debug.LogError($"{LOG} brak TerrainData: {TerrainDataPath}");
                return false;
            }

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = Name;
            go.transform.position = Pos;
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());

            var terrain = go.GetComponent<Terrain>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>(TerrainMatPath);
            if (mat != null) terrain.materialTemplate = mat;

            Undo.RegisterCreatedObjectUndo(go, "Bootstrap: Terrain");
            Debug.Log($"{LOG} [OK] {Name} @ {Pos} (data={data.name}, layers={data.terrainLayers.Length})");
            return true;
        }
    }
}
#endif
