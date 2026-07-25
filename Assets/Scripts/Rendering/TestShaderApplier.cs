// =============================================================================
// TestShaderApplier.cs
// CYBERNOMAD -- Wymusza "Custom/Test Shader" na wszystkich rendererach obiektu
// PRZY SPAWNIE. URP/Lit renderuje sie magenta w tym projekcie; Custom/Test Shader
// (ten sam ktory dziala na PINEA) renderuje poprawnie.
//
// Stosowane runtime na INSTANCJACH (renderer.materials, nie sharedMaterials) --
// nie modyfikuje assetow/prefabow, tylko sklonowany obiekt w scenie.
//
// Shader jest ASSETEM: Assets/PLAGA44/Shaders/TestShader.shader (Custom/Test
// Shader, kopia z URP Tests). Build-safe -- wejdzie do APK Questa, o ile jest
// referowany przez material w buildzie (jest -- shotgun/M249) lub dodany do
// Always Included Shaders. Shader.Find dziala po nazwie shaderlab.
// =============================================================================

using UnityEngine;

namespace Plaga44.Rendering
{
    public static class TestShaderApplier
    {
        private const string LOG = "[PLAGA44][TestShader]";
        private const string ShaderName = "Custom/Test Shader";

        private static Shader _shader;
        private static bool _searched;
        private static bool _warned;

        /// <summary>Ustawia Custom/Test Shader na wszystkich rendererach obiektu (i dzieci).
        /// Zachowuje tekstury/properties o zgodnych nazwach. No-op gdy shader niedostepny.</summary>
        public static void Apply(GameObject go)
        {
            if (go == null) return;
            var sh = Resolve();
            if (sh == null) return; // shader nie w buildzie -> zostaw materialy bez zmian (widoczny brak, nie crash)

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                // .materials = INSTANCJE per renderer (klon) -- nie rusza sharedMaterial/asset.
                var mats = r.materials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || mats[i].shader == sh) continue;
                    mats[i].shader = sh;
                    changed = true;
                }
                if (changed) r.materials = mats;
            }
        }

        private static Shader Resolve()
        {
            if (_searched) return _shader;
            _searched = true;
            _shader = Shader.Find(ShaderName);
            if (_shader == null && !_warned)
            {
                _warned = true;
                Debug.LogWarning($"{LOG} '{ShaderName}' nie znaleziony przez Shader.Find. "
                    + "Materialy bez zmian. Sprawdz Assets/PLAGA44/Shaders/TestShader.shader "
                    + "lub dodaj shader do Always Included Shaders (Project Settings > Graphics).");
            }
            return _shader;
        }
    }
}
