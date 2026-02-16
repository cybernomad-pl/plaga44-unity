// VRInputDebugMenu.cs
// CYBERNOMAD -- Editor menu toggle for VR Input Debug HUD.
// Menu: CYBERNOMAD > Debug > VR Input Debug HUD

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    [InitializeOnLoad]
    public static class VRInputDebugMenu
    {
        private const string MENU_PATH = "CYBERNOMAD/Debug/VR Input Debug HUD";
        private const string ENABLED_KEY = "CYBERNOMAD_VRInputDebug";

        // Force off on editor startup -- must be enabled manually each session
        static VRInputDebugMenu()
        {
            EditorPrefs.SetBool(ENABLED_KEY, false);
        }

        [MenuItem(MENU_PATH, false, 500)]
        private static void ToggleDebugHUD()
        {
            bool current = EditorPrefs.GetBool(ENABLED_KEY, false);
            bool next = !current;
            EditorPrefs.SetBool(ENABLED_KEY, next);

            if (Application.isPlaying)
            {
                if (next)
                    VRInputDebug.Spawn();
                else
                    VRInputDebug.Kill();
            }

            Debug.Log($"[CYBERNOMAD] VR Input Debug HUD: {(next ? "ENABLED" : "DISABLED")}");
        }

        [MenuItem(MENU_PATH, true)]
        private static bool ToggleDebugHUD_Validate()
        {
            Menu.SetChecked(MENU_PATH, EditorPrefs.GetBool(ENABLED_KEY, false));
            return true;
        }
    }
}
#endif
