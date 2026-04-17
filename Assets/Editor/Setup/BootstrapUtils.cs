// =============================================================================
// BootstrapUtils.cs
// Wspoldzielone helpery dla klas Setup.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;

namespace Plaga44.Editor.Setup
{
    public static class BootstrapUtils
    {
        public static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
