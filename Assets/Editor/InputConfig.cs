// InputConfig.cs -- CYBERNOMAD Editor Tool
//
// Steruje: ProjectSettings/InputManager.asset (legacy input)
// + info o InputSystem (nowy)
//
// W VR na Queście używamy OVRInput (Meta SDK), nie Unity Input System.
// Ten Config jest głównie informacyjny + pozwala dodac legacy axes jesli potrzeba.
//
// Public API:
//   InputConfig.LogCurrent();
//   InputConfig.IsNewInputSystemEnabled();

using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class InputConfig
    {
        private const string LOG = "[PLAGA44]";
        private const string ASSET = "ProjectSettings/InputManager.asset";

        public static bool IsNewInputSystemEnabled()
        {
            // Check if com.unity.inputsystem is in manifest
            return PackagesConfig.GetVersion("com.unity.inputsystem") != null;
        }

        public static void LogCurrent()
        {
            Debug.Log($"{LOG} Input:");
            Debug.Log($"{LOG}   New Input System: {(IsNewInputSystemEnabled() ? "installed" : "not installed")}");
            Debug.Log($"{LOG}   VR input: OVRInput (Meta XR SDK)");

            var so = LoadAsset();
            if (so == null) return;

            var axes = so.FindProperty("m_Axes");
            if (axes != null)
                Debug.Log($"{LOG}   Legacy axes count: {axes.arraySize}");
        }

        [MenuItem("CYBERNOMAD/Config/Input/Show Current", false, 100)]
        static void MenuShow() => LogCurrent();

        static SerializedObject LoadAsset()
        {
            var obj = AssetDatabase.LoadAllAssetsAtPath(ASSET);
            if (obj == null || obj.Length == 0) { Debug.LogError($"{LOG} {ASSET} not found"); return null; }
            return new SerializedObject(obj[0]);
        }
    }
}
