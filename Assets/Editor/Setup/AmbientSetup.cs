// =============================================================================
// AmbientSetup.cs
// CYBERNOMAD -- Ustawia RenderSettings.ambient* z BootstrapConfig.
// Tryby (UnityEngine.Rendering.AmbientMode):
//   Skybox   -- ambientIntensity mnozy skybox IBL
//   Trilight -- sky/equator/ground gradient
//   Flat     -- pojedynczy ambientLight color
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plaga44.Editor.Setup
{
    public static class AmbientSetup
    {
        private const string LOG = "[PLAGA44][AmbientSetup]";

        public static bool Run(BootstrapConfig cfg)
        {
            bool changed = false;

            if (RenderSettings.ambientMode != cfg.ambientMode)
            { RenderSettings.ambientMode = cfg.ambientMode; changed = true; }

            if (!Mathf.Approximately(RenderSettings.ambientIntensity, cfg.ambientIntensity))
            { RenderSettings.ambientIntensity = cfg.ambientIntensity; changed = true; }

            if (RenderSettings.ambientLight != cfg.ambientLight)
            { RenderSettings.ambientLight = cfg.ambientLight; changed = true; }

            if (RenderSettings.ambientSkyColor != cfg.ambientSkyColor)
            { RenderSettings.ambientSkyColor = cfg.ambientSkyColor; changed = true; }

            if (RenderSettings.ambientEquatorColor != cfg.ambientEquatorColor)
            { RenderSettings.ambientEquatorColor = cfg.ambientEquatorColor; changed = true; }

            if (RenderSettings.ambientGroundColor != cfg.ambientGroundColor)
            { RenderSettings.ambientGroundColor = cfg.ambientGroundColor; changed = true; }

            Debug.Log($"{LOG} [{(changed ? "SET" : "OK")}] mode={cfg.ambientMode} intensity={cfg.ambientIntensity:F2}");
            return changed;
        }
    }
}
#endif
