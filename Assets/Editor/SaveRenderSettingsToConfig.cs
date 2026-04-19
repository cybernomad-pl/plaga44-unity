// =============================================================================
// SaveRenderSettingsToConfig.cs
// CYBERNOMAD -- Zapisuje aktualny stan RenderSettings sceny do
// BootstrapConfig_Quest.asset. Po tym commit -> stan przetrwa reset.
//
// Workflow:
//   1. Tuning w HamburgerMenu (runtime) albo Lighting window (edit)
//   2. CYBERNOMAD > Fix > Save Render Settings to Config
//   3. git commit BootstrapConfig_Quest.asset
//   4. Nastepny Bootstrap odtworzy dokladnie te wartosci
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

        [MenuItem("CYBERNOMAD/Fix/Save Render Settings to Config", false, 410)]
        public static void SaveMenu()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<Plaga44.BootstrapConfig>(ConfigPath);
            if (cfg == null)
            {
                Debug.LogError($"{LOG} Config not found: {ConfigPath}");
                return;
            }

            Undo.RecordObject(cfg, "Save RenderSettings to BootstrapConfig");

            // Fog
            cfg.fogEnabled       = RenderSettings.fog;
            cfg.fogMode          = RenderSettings.fogMode;
            cfg.fogColor         = RenderSettings.fogColor;
            cfg.fogDensity       = RenderSettings.fogDensity;
            cfg.fogStartDistance = RenderSettings.fogStartDistance;
            cfg.fogEndDistance   = RenderSettings.fogEndDistance;

            // Ambient
            cfg.ambientMode         = RenderSettings.ambientMode;
            cfg.ambientIntensity    = RenderSettings.ambientIntensity;
            cfg.ambientLight        = RenderSettings.ambientLight;
            cfg.ambientSkyColor     = RenderSettings.ambientSkyColor;
            cfg.ambientEquatorColor = RenderSettings.ambientEquatorColor;
            cfg.ambientGroundColor  = RenderSettings.ambientGroundColor;

            // Directional light (first non-bounce directional w scenie)
            var sun = FindSun();
            if (sun != null)
            {
                cfg.sunColor     = sun.color;
                cfg.sunIntensity = sun.intensity;
                cfg.sunRotation  = sun.transform.rotation.eulerAngles;
                cfg.sunShadows   = sun.shadows;
            }

            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssetIfDirty(cfg);

            string summary = $"Fog enabled={cfg.fogEnabled} mode={cfg.fogMode} density={cfg.fogDensity:F4}\n"
                + $"Ambient mode={cfg.ambientMode} intensity={cfg.ambientIntensity:F2}\n"
                + (sun != null
                    ? $"Sun color={ColorToStr(sun.color)} intensity={sun.intensity:F2} rot={sun.transform.rotation.eulerAngles}"
                    : "Sun: not found");

            Debug.Log($"{LOG} Saved to {ConfigPath}\n{summary}");
            EditorUtility.DisplayDialog("Saved", "RenderSettings saved to BootstrapConfig_Quest.asset.\n\n" + summary, "OK");
        }

        private static Light FindSun()
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && !l.gameObject.name.Contains("Bounce"))
                    return l;
            return null;
        }

        private static string ColorToStr(Color c) => $"({c.r:F2},{c.g:F2},{c.b:F2})";
    }
}
#endif
