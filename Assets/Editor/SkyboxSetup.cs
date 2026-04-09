using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class SkyboxSetup
    {
        [MenuItem("CYBERNOMAD/Scene Setup/Setup Skybox", false, 21)]
        public static void SetupSkybox()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Potok/Skybox/BGR_Sky1.mat");
            if (mat == null) { Debug.LogError("[PLAGA44] BGR_Sky1.mat not found"); return; }

            // Oryginalny cubemap BGR_Sky1.tif jest juz ustawiony w _Tex -- nie nadpisujemy.
            // Chmury wylaczone.
            mat.SetFloat("_CloudOpacity", 0f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log("[PLAGA44] Skybox setup done (oryginalny cubemap, chmury wylaczone)");
        }
    }
}
