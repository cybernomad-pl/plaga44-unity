// PlayerModelImporter.cs -- auto-ustawia Humanoid rig na PLAYER_rigged.fbx
// Odpala sie automatycznie po imporcie assetu.

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

            // Humanoid rig
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            // Skala -- Fuse OBJ jest w centymetrach
            importer.globalScale = 1f;
            importer.useFileScale = true;

            // Materialy
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

            Debug.Log("[PLAGA44] PlayerModelImporter: PLAYER_rigged -> Humanoid rig auto-configured");
        }
    }
}
