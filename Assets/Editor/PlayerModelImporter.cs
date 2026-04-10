using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public class PlayerModelImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.Contains("PLAYER_rigged")) return;

            var importer = assetImporter as ModelImporter;
            if (importer == null) return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.globalScale = 1f;
            importer.useFileScale = true;

            Debug.Log("[PLAGA44] PlayerModelImporter: Humanoid rig configured");
        }
    }
}
