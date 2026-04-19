// =============================================================================
// FogSetup.cs
// CYBERNOMAD -- Ustawia RenderSettings.fog* z BootstrapConfig.
// Wywolywany przez Bootstrap przy kazdym odpaleniu -> tuning z HamburgerMenu
// nie znika po reload sceny bo przy nastepnym Bootstrap wartosci wracaja z cfg.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class FogSetup
    {
        private const string LOG = "[PLAGA44][FogSetup]";

        public static bool Run(BootstrapConfig cfg)
        {
            bool changed = false;

            if (RenderSettings.fog != cfg.fogEnabled)
            { RenderSettings.fog = cfg.fogEnabled; changed = true; }

            if (RenderSettings.fogMode != cfg.fogMode)
            { RenderSettings.fogMode = cfg.fogMode; changed = true; }

            if (RenderSettings.fogColor != cfg.fogColor)
            { RenderSettings.fogColor = cfg.fogColor; changed = true; }

            if (!Mathf.Approximately(RenderSettings.fogDensity, cfg.fogDensity))
            { RenderSettings.fogDensity = cfg.fogDensity; changed = true; }

            if (!Mathf.Approximately(RenderSettings.fogStartDistance, cfg.fogStartDistance))
            { RenderSettings.fogStartDistance = cfg.fogStartDistance; changed = true; }

            if (!Mathf.Approximately(RenderSettings.fogEndDistance, cfg.fogEndDistance))
            { RenderSettings.fogEndDistance = cfg.fogEndDistance; changed = true; }

            Debug.Log($"{LOG} [{(changed ? "SET" : "OK")}] fog={cfg.fogEnabled} mode={cfg.fogMode} "
                + $"density={cfg.fogDensity:F4} linear=[{cfg.fogStartDistance:F0}..{cfg.fogEndDistance:F0}]");
            return changed;
        }
    }
}
#endif
