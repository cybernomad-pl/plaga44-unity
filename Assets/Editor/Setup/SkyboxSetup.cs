// =============================================================================
// SkyboxSetup.cs
// Przypisuje skybox i tworzy directional light. Wywolywany przez Bootstrap.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class SkyboxSetup
    {
        private const string LOG = "[PLAGA44][SkyboxSetup]";

        public static bool Run(BootstrapConfig cfg)
        {
            bool changed = false;
            changed |= SetupSkybox(cfg);
            changed |= SetupDirectionalLight(cfg);
            return changed;
        }

        private static bool SetupSkybox(BootstrapConfig cfg)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(cfg.skyboxMatPath);
            if (mat == null)
            {
                Debug.LogWarning($"{LOG} [MISSING] Skybox material not found: {cfg.skyboxMatPath}");
                return false;
            }
            if (RenderSettings.skybox == mat)
            {
                Debug.Log($"{LOG} [OK] Skybox: {mat.name}");
                return false;
            }
            RenderSettings.skybox = mat;
            Debug.Log($"{LOG} [SET] Skybox: {mat.name}");
            return true;
        }

        private static bool SetupDirectionalLight(BootstrapConfig cfg)
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    Debug.Log($"{LOG} [OK] Directional Light: {light.name}");
                    return false;
                }
            }

            var go = new GameObject("Directional Light");
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = cfg.sunColor;
            l.intensity = cfg.sunIntensity;
            l.shadows = cfg.sunShadows;
            go.transform.rotation = Quaternion.Euler(cfg.sunRotation);
            Undo.RegisterCreatedObjectUndo(go, "Bootstrap: Add Directional Light");

            Debug.Log($"{LOG} [ADDED] Directional Light (shadows: {cfg.sunShadows})");
            return true;
        }
    }
}
#endif
