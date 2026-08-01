// =============================================================================
// SceneSingletonsSetup.cs
// Stawia WSZYSTKIE singletony w scenie jesli ich nie ma.
// Wywolywany przez Bootstrap.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Plaga44.UI;

namespace Plaga44.Editor.Setup
{
    public static class SceneSingletonsSetup
    {
        private const string LOG = "[PLAGA44][SceneSingletonsSetup]";

        public static bool Run(BootstrapConfig cfg)
        {
            bool changed = false;
            changed |= EnsureSingleton<HamburgerMenu>("_HamburgerMenu", "HamburgerMenu", null);
            // SkyRotator USUNIETY (2026-07-31): whiteboxing bez skyboxa -- nie ma czego obracac.
            changed |= EnsureSingleton<AvatarGallery>("_AvatarGallery", "AvatarGallery", null);
            changed |= EnsureSingleton<ItemBrowser>("_ItemGallery", "Item Gallery", null);
            return changed;
        }

        private static bool EnsureSingleton<T>(string goName, string label, System.Action<T> configure) where T : Component
        {
            if (Object.FindAnyObjectByType<T>() != null)
            {
                Debug.Log($"{LOG} [OK] {label}");
                return false;
            }
            var go = new GameObject(goName);
            Undo.RegisterCreatedObjectUndo(go, $"Bootstrap: Add {label}");
            var comp = go.AddComponent<T>();
            configure?.Invoke(comp);
            Debug.Log($"{LOG} [ADDED] {label}");
            return true;
        }
    }
}
#endif
