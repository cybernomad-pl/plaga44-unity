#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    /// <summary>
    /// Auto-configures Survivor A Lusth FBX imports: Humanoid rig, correct scale.
    /// Mixamo FBX ships with 1cm scale -- needs globalScale=1 + useFileScale=false
    /// to end up at human size (1.7m).
    /// </summary>
    public class PlayerAvatarImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.Contains("Survivor_A_Lusth")) return;

            var importer = assetImporter as ModelImporter;
            if (importer == null) return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.globalScale = 1f;
            importer.useFileScale = false;
            importer.importBlendShapes = true;

            Debug.Log($"[PLAGA44] PlayerAvatarImporter: Humanoid rig configured for {assetPath}");
        }
    }
}
#endif
