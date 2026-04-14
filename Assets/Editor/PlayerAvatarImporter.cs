// =============================================================================
// PlayerAvatarImporter.cs
// CYBERNOMAD -- Legacy importer dla Survivor_A_Lusth FBX (Mixamo).
// UWAGA: AvatarImport.AvatarModelPreprocessor juz obsluguje wszystko w Avatars/,
// ten importer dziala dla sciezek z "Survivor_A_Lusth" w nazwie gdziekolwiek.
// Po przeniesieniu starego Survivora do Avatars/ -- ten plik mozna usunac.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public class PlayerAvatarImporter : AssetPostprocessor
    {
        private const string TargetNameToken = "Survivor_A_Lusth";
        private const string LOG = "[PLAGA44] PlayerAvatarImporter";

        private void OnPreprocessModel()
        {
            if (!assetPath.Contains(TargetNameToken)) return;
            if (assetImporter is not ModelImporter mi) return;

            ConfigureMixamoHumanoid(mi);
            Debug.Log($"{LOG}: Humanoid rig configured for {assetPath}");
        }

        private static void ConfigureMixamoHumanoid(ModelImporter mi)
        {
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.globalScale = 1f;
            mi.useFileScale = false;
            mi.importBlendShapes = true;
        }
    }
}
#endif
