// =============================================================================
// SaveRenderSettingsToConfig.cs
// CYBERNOMAD -- Zapisuje aktualny stan RenderSettings sceny (Fog, Ambient,
// Directional Light) do BootstrapConfig_Quest.asset. Po tym commit asset ->
// stan przetrwa reset. Nastepny Bootstrap odtworzy dokladnie te wartosci.
//
// Bez menu item (polityka "wszystko automatycznie"). Publiczna metoda Save()
// wywolywana z HamburgerMenu button lub innego editor scriptu.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class SaveRenderSettingsToConfig
    {
        private const string LOG        = "[PLAGA44][SaveToConfig]";
        private const string ConfigPath = "Assets/PLAGA44/Config/BootstrapConfig_Quest.asset";

        /// <summary>Kopiuje RenderSettings (Fog, Ambient, Sun) -> BootstrapConfig asset.
        /// Zwraca true gdy zapisane, false gdy config nie istnieje.</summary>
        public static bool Save()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<Plaga44.BootstrapConfig>(ConfigPath);
            if (cfg == null)
            {
                Debug.LogError($"{LOG} Config not found: {ConfigPath}");
                return false;
            }

            Undo.RecordObject(cfg, "Save RenderSettings to BootstrapConfig");

            CopyFogToConfig(cfg);
            CopyAmbientToConfig(cfg);
            CopySunToConfig(cfg);

            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssetIfDirty(cfg);

            Debug.Log($"{LOG} Saved to {ConfigPath}: "
                + $"fog={cfg.fogEnabled}/{cfg.fogMode}/density={cfg.fogDensity:F4}, "
                + $"ambient={cfg.ambientMode}/int={cfg.ambientIntensity:F2}");
            return true;
        }

        private static void CopyFogToConfig(Plaga44.BootstrapConfig cfg)
        {
            cfg.fogEnabled       = RenderSettings.fog;
            cfg.fogMode          = RenderSettings.fogMode;
            cfg.fogColor         = RenderSettings.fogColor;
            cfg.fogDensity       = RenderSettings.fogDensity;
            cfg.fogStartDistance = RenderSettings.fogStartDistance;
            cfg.fogEndDistance   = RenderSettings.fogEndDistance;
        }

        private static void CopyAmbientToConfig(Plaga44.BootstrapConfig cfg)
        {
            cfg.ambientMode         = RenderSettings.ambientMode;
            cfg.ambientIntensity    = RenderSettings.ambientIntensity;
            cfg.ambientLight        = RenderSettings.ambientLight;
            cfg.ambientSkyColor     = RenderSettings.ambientSkyColor;
            cfg.ambientEquatorColor = RenderSettings.ambientEquatorColor;
            cfg.ambientGroundColor  = RenderSettings.ambientGroundColor;
        }

        private static void CopySunToConfig(Plaga44.BootstrapConfig cfg)
        {
            var sun = FindSun();
            if (sun == null)
            {
                Debug.LogWarning($"{LOG} Sun (non-bounce Directional Light) not found -- skipping sun copy");
                return;
            }
            cfg.sunColor     = sun.color;
            cfg.sunIntensity = sun.intensity;
            cfg.sunRotation  = sun.transform.rotation.eulerAngles;
            cfg.sunShadows   = sun.shadows;
        }

        private static Light FindSun()
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && !l.gameObject.name.Contains("Bounce"))
                    return l;
            return null;
        }
    }
}
#endif
