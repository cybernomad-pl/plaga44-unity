using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class FixTerrainMaterial
    {
        [MenuItem("Plaga44/Fix Terrain Material")]
        public static void Fix()
        {
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null)
            {
                Debug.LogError("[Plaga44] No terrain found in scene");
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader == null)
            {
                Debug.LogError("[Plaga44] URP Terrain/Lit shader not found");
                return;
            }

            var mat = new Material(shader);
            mat.name = "Terrain_URP_Lit";

            // Zapisz jako asset zeby nie byl tymczasowy
            string matPath = "Assets/PLAGA44/Materials/Terrain_URP_Lit.mat";
            System.IO.Directory.CreateDirectory(
                System.IO.Path.GetDirectoryName(
                    System.IO.Path.Combine(Application.dataPath, "../", matPath)));
            AssetDatabase.CreateAsset(mat, matPath);

            terrain.materialTemplate = mat;
            EditorUtility.SetDirty(terrain);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"[Plaga44] Terrain material set to URP Terrain/Lit ({matPath})");
        }
    }
}
