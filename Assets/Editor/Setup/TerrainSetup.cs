#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plaga44.Editor.Setup
{
    // Teren z 9 kafli (Tile_0..8) w siatce 3x3, kazdy pelnej rozdzielczosci.
    // Scene_A_Terrain to tylko 1 kafel (1/9 mapy) -- stad dziury we wczesniejszej wersji.
    // Kafle row-major, SetNeighbors dla bezszwowego LOD. Srodek siatki na world (0,0,0).
    // Faza Bootstrap (1-Terrain) -- ClearScene kasuje, ta faza odtwarza.
    public static class TerrainSetup
    {
        private const string LOG = "[PLAGA44][TerrainSetup]";
        private const string Folder = "Assets/Potok/Terrain";
        private const string ParentName = "Terrain_SceneA";
        private const string MatPath = "Assets/PLAGA44/Materials/TerrainLit.mat";
        private const int Grid = 3;

        public static bool Run(BootstrapConfig cfg)
        {
            if (GameObject.Find(ParentName) != null)
            {
                Debug.Log($"{LOG} [OK] {ParentName} juz w scenie.");
                return false;
            }

            var parent = new GameObject(ParentName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            var terrains = new Terrain[Grid * Grid];
            Vector3 tileSize = Vector3.zero;

            for (int i = 0; i < terrains.Length; i++)
            {
                if (i == 0) { terrains[i] = null; continue; } // Tile_0 wywalony -- gigantyczna trawa

                var data = AssetDatabase.LoadAssetAtPath<TerrainData>($"{Folder}/Tile_{i}.asset");
                if (data == null)
                {
                    Debug.LogError($"{LOG} brak {Folder}/Tile_{i}.asset -- przerwano");
                    Object.DestroyImmediate(parent);
                    return false;
                }
                tileSize = data.size;

                int col = i % Grid, row = i / Grid;
                var go = Terrain.CreateTerrainGameObject(data);
                go.name = $"Tile_{i}";
                go.transform.SetParent(parent.transform);
                go.transform.localPosition = new Vector3(col * data.size.x, 0f, row * data.size.z);

                var t = go.GetComponent<Terrain>();
                if (mat != null) t.materialTemplate = mat;
                t.drawTreesAndFoliage = false; // NO GRASS -- master switch renderu trawy/detali/drzew OFF
                t.detailObjectDistance = 0f;
                terrains[i] = t;
            }

            // Sasiedztwo (left, top, right, bottom) -- bezszwowy LOD miedzy kaflami.
            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i] == null) continue; // Tile_0 wywalony

                int col = i % Grid, row = i / Grid;
                terrains[i].SetNeighbors(
                    col > 0 ? terrains[i - 1] : null,
                    row < Grid - 1 ? terrains[i + Grid] : null,
                    col < Grid - 1 ? terrains[i + 1] : null,
                    row > 0 ? terrains[i - Grid] : null);
            }

            // Srodek siatki 3x3 na world (0,0,0).
            parent.transform.position = new Vector3(-1.5f * tileSize.x, 0f, -1.5f * tileSize.z);
            SceneManager.MoveGameObjectToScene(parent, SceneManager.GetActiveScene());
            Undo.RegisterCreatedObjectUndo(parent, "Add Terrain (9 tiles)");

            Debug.Log($"{LOG} [OK] {ParentName}: 9 kafli {tileSize.x}x{tileSize.z}, srodek (0,0,0), mat={(mat != null ? mat.name : "brak")}");
            return true;
        }
    }
}
#endif
