#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Auto-configure PINEA_rigged.fbx as Humanoid avatar on import.
/// </summary>
public class PINEAImportSettings : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.Contains("PINEA_rigged")) return;

        var importer = assetImporter as ModelImporter;
        if (importer == null) return;

        // Humanoid rig
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

        // Scale (FBX exported with 0.01 scale, but double check)
        importer.globalScale = 1f;
        importer.useFileScale = true;

        // Mesh
        importer.meshCompression = ModelImporterMeshCompression.Medium;
        importer.isReadable = false;
        importer.optimizeMeshPolygons = true;
        importer.optimizeMeshVertices = true;

        // Materials
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

        // Quest optimization
        importer.weldVertices = true;
        importer.importBlendShapes = false;
        importer.importVisibility = false;
        importer.importCameras = false;
        importer.importLights = false;

        Debug.Log("[PLAGA44] PINEA_rigged: configured as Humanoid avatar");
    }

    void OnPostprocessModel(GameObject obj)
    {
        if (!assetPath.Contains("PINEA_rigged")) return;

        var avatar = (assetImporter as ModelImporter)?.sourceAvatar;
        if (avatar != null && avatar.isHuman)
        {
            Debug.Log($"[PLAGA44] PINEA_rigged: Humanoid avatar OK -- {avatar.humanDescription.human.Length} bones mapped");
        }
        else
        {
            Debug.LogWarning("[PLAGA44] PINEA_rigged: Humanoid mapping may need manual adjustment in Inspector");
        }
    }
}
#endif
