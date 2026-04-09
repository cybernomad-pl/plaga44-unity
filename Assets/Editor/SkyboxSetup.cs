using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class SkyboxSetup
    {
        [MenuItem("CYBERNOMAD/Scene Setup/Setup Skybox Clouds", false, 21)]
        public static void SetupClouds()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Potok/Skybox/BGR_Sky1.mat");
            if (mat == null) { Debug.LogError("[PLAGA44] BGR_Sky1.mat not found"); return; }

            // Sky cubemap (bez chmur)
            var skyCube = AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Potok/Skybox/BGR_Sky1_sky.tif");
            if (skyCube != null)
            {
                mat.SetTexture("_Tex", skyCube);
                Debug.Log("[PLAGA44] Sky cubemap -> BGR_Sky1_sky.tif");
            }
            else
            {
                Debug.LogWarning("[PLAGA44] BGR_Sky1_sky.tif not found -- using original cubemap");
            }

            // Cloud layer
            var cloudTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Potok/Skybox/BGR_Sky1_clouds.png");
            if (cloudTex != null)
            {
                mat.SetTexture("_CloudTex", cloudTex);
                Debug.Log("[PLAGA44] Cloud texture -> BGR_Sky1_clouds.png");
            }
            else
            {
                Debug.LogError("[PLAGA44] BGR_Sky1_clouds.png not found!");
            }

            mat.SetFloat("_CloudOpacity", 1.0f);
            mat.SetColor("_CloudTint", Color.white);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log("[PLAGA44] Skybox cloud setup done");
        }
    }
}
