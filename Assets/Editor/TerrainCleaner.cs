// TerrainCleaner.cs -- jednorazowy skrypt do czyszczenia tree/detail z terrain tiles.
// Menu: CYBERNOMAD > Scene Setup > Clean Terrain Tiles
// Po uzyciu mozna usunac ten plik.

using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class TerrainCleaner
    {
        [MenuItem("CYBERNOMAD/Scene Setup/Clean Terrain Tiles", false, 20)]
        public static void CleanAllTiles()
        {
            int cleaned = 0;
            for (int i = 0; i < 9; i++)
            {
                string path = $"Assets/Potok/Terrain/Tile_{i}.asset";
                var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
                if (data == null) continue;

                data.treeInstances = new TreeInstance[0];
                data.treePrototypes = new TreePrototype[0];
                data.detailPrototypes = new DetailPrototype[0];

                // Wyczysc detail layers
                for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
                    data.SetDetailLayer(0, 0, layer, new int[data.detailWidth, data.detailHeight]);

                EditorUtility.SetDirty(data);
                cleaned++;
                Debug.Log($"[PLAGA44] Wyczyszczono drzewa/detale: {path}");
            }

            // Wyczysc tez oryginal
            var orig = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Potok/Terrain/Scene_A_Terrain.asset");
            if (orig != null)
            {
                orig.treeInstances = new TreeInstance[0];
                orig.treePrototypes = new TreePrototype[0];
                orig.detailPrototypes = new DetailPrototype[0];
                EditorUtility.SetDirty(orig);
                cleaned++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PLAGA44] Wyczyszczono {cleaned} terrain data assets.");
        }
    }
}
