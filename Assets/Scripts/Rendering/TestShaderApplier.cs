// =============================================================================
// TestShaderApplier.cs
// CYBERNOMAD -- Wymusza "Custom/Test Shader" na wszystkich rendererach obiektu
// PRZY SPAWNIE. URP/Lit renderuje sie magenta w tym projekcie; Custom/Test Shader
// (ten sam ktory dziala na PINEA) renderuje poprawnie.
//
// Stosowane runtime na INSTANCJACH (renderer.materials, nie sharedMaterials) --
// nie modyfikuje assetow/prefabow, tylko sklonowany obiekt w scenie.
//
// UWAGA: shader lezy w URP PackageCache Tests/Editor/ -- dziala w EDYTORZE,
// ale NIE wejdzie do buildu Questa (Editor-only). Do buildu trzeba skopiowac
// .shader do Assets/ lub dodac do Always Included Shaders (osobny task).
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
                Debug.LogWarning($"{LOG} '{ShaderName}' nie znaleziony (Editor-only shader -- brak w buildzie?). "
                    + "Materialy bez zmian. Skopiuj .shader do Assets/ lub dodaj do Always Included Shaders.");
            }
            return _shader;
        }
    }
}
