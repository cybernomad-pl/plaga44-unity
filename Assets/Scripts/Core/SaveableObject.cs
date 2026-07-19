// =============================================================================
// SaveableObject.cs
// CYBERNOMAD -- Znacznik obiektu ktory ma byc zapisany w world-save (#196).
// Trzyma EXPLICIT resourcePath (skad respawnowac) -- zero zgadywania z nazwy.
// Dodawany przy spawnie (ObjectSpawner / back-holster). WorldSaveManager
// zbiera wszystkie SaveableObject przy zapisie i respawnuje przy load.
// =============================================================================
using UnityEngine;

namespace Plaga44
{
    [DisallowMultipleComponent]
    public class SaveableObject : MonoBehaviour
    {
        [Tooltip("Resources path do respawnu (np. 'Items/M249'). Ustawiany przy spawnie.")]
        public string resourcePath;

        /// <summary>Ustaw resourcePath (wywolywane przez spawnery przy Instantiate).</summary>
        public static SaveableObject Tag(GameObject go, string resourcePath)
        {
            if (go == null) return null;
            var s = go.GetComponent<SaveableObject>();
            if (s == null) s = go.AddComponent<SaveableObject>();
            s.resourcePath = resourcePath;
            return s;
        }
    }
}
