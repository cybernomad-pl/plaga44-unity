// =============================================================================
// MissingScriptCleaner.cs
// CYBERNOMAD -- Usuwa MonoBehaviour'y z broken script reference ze WSZYSTKICH
// GameObject-ow w aktywnej scenie. Unity loguje "The referenced script (Unknown)
// on this Behaviour is missing!" kiedy komponent wskazuje na usunięty/zmieniony
// skrypt. Pollutes Console i czasem lagi.
//
// Wywolywane automatycznie przez Bootstrap (po OpenScene, przed setupami).
// Bez menu item (polityka "wszystko automatycznie").
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class MissingScriptCleaner
    {
        private const string LOG = "[PLAGA44][MissingScriptCleaner]";

        public static void CleanActiveScene()
        {
            int totalRemoved = 0;
            int gameObjectsCleaned = 0;

            foreach (var go in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go == null) continue;
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                if (removed > 0)
                {
                    totalRemoved += removed;
                    gameObjectsCleaned++;
                }
            }

            if (totalRemoved > 0)
                Debug.Log($"{LOG} Removed {totalRemoved} missing script(s) from {gameObjectsCleaned} GameObject(s)");
        }
    }
}
#endif
