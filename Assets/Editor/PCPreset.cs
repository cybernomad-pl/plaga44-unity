// PCPreset.cs -- CYBERNOMAD Editor Tool
//
// JEDEN PRZYCISK -- konfiguruje PC Pipeline + Renderer.
//
// Public API:
//   PCPreset.Apply();

using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class PCPreset
    {
        private const string LOG = "[PLAGA44/PC]";

        [MenuItem("CYBERNOMAD/Presets/PC/--- Apply All ---", false, 0)]
        public static void Apply()
        {
            Debug.Log($"{LOG} ========== APPLYING PC PRESET ==========");

            PCPipeline.Apply(PCPipeline.INITIAL);
            PCRenderer.Apply(PCRenderer.INITIAL);

            Debug.Log($"{LOG} ========== PC PRESET COMPLETE ==========");
        }

        [MenuItem("CYBERNOMAD/Presets/PC/--- Log All ---", false, 100)]
        public static void LogAll()
        {
            PCPipeline.LogCurrent();
            PCRenderer.LogCurrent();
        }
    }
}
