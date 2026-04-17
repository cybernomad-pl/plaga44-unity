// =============================================================================
// BounceLightSetup.cs
// Tworzy/konfiguruje Bounce Light -- fill light odbijajacy swiatlo w gore
// (symulacja ground bounce). Wywolywany przez Bootstrap.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class BounceLightSetup
    {
        private const string LOG = "[PLAGA44][BounceLightSetup]";
        private const string BounceLightName = "Bounce Light";

        public static bool Run(BootstrapConfig cfg)
        {
            var existing = FindBounceLight();
            if (existing != null)
            {
                bool changed = ConfigureLight(existing, cfg);
                Debug.Log($"{LOG} {(changed ? "[UPDATED]" : "[OK]")} {BounceLightName}");
                return changed;
            }

            CreateBounceLight(cfg);
            return true;
        }

        private static Light FindBounceLight()
        {
            // Szukaj po nazwie -- Borys mogl dodac recznie
            var go = GameObject.Find(BounceLightName);
            if (go != null)
            {
                var light = go.GetComponent<Light>();
                if (light != null) return light;
            }

            // Fallback: szukaj drugiego directional lighta ktory patrzy w gore
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                if (light.gameObject.name.Contains("Bounce") || light.gameObject.name.Contains("bounce"))
                    return light;
            }
            return null;
        }

        private static void CreateBounceLight(BootstrapConfig cfg)
        {
            var go = new GameObject(BounceLightName);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            ConfigureLight(light, cfg);
            Undo.RegisterCreatedObjectUndo(go, "Bootstrap: Add Bounce Light");
            Debug.Log($"{LOG} [ADDED] {BounceLightName} (rot={cfg.bounceLightRotation}, intensity={cfg.bounceLightIntensity})");
        }

        /// <summary>Applies config to existing light. Returns true if anything changed.</summary>
        private static bool ConfigureLight(Light light, BootstrapConfig cfg)
        {
            bool changed = false;

            if (light.color != cfg.bounceLightColor)
            {
                light.color = cfg.bounceLightColor;
                changed = true;
            }
            if (!Mathf.Approximately(light.intensity, cfg.bounceLightIntensity))
            {
                light.intensity = cfg.bounceLightIntensity;
                changed = true;
            }
            if (light.shadows != cfg.bounceLightShadows)
            {
                light.shadows = cfg.bounceLightShadows;
                changed = true;
            }

            var targetRot = Quaternion.Euler(cfg.bounceLightRotation);
            if (Quaternion.Angle(light.transform.rotation, targetRot) > 0.1f)
            {
                light.transform.rotation = targetRot;
                changed = true;
            }

            if (changed) EditorUtility.SetDirty(light.gameObject);
            return changed;
        }
    }
}
#endif
