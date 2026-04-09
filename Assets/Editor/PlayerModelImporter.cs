// PlayerModelImporter.cs -- auto-ustawia Humanoid rig na PLAYER_rigged.fbx
// Mapuje kości Mixamo (LeftLeg, LeftArm) na Unity Humanoid (LeftLowerLeg, LeftUpperArm)

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
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

            // Ręczne mapowanie kości Mixamo -> Unity Humanoid
            var hd = importer.humanDescription;
            hd.human = new HumanBone[]
            {
                Bone("Hips", "Hips"),
                Bone("Spine", "Spine"),
                Bone("Chest", "Spine1"),
                Bone("UpperChest", "Spine2"),
                Bone("Neck", "Neck"),
                Bone("Head", "Head"),

                Bone("LeftShoulder", "LeftShoulder"),
                Bone("LeftUpperArm", "LeftArm"),
                Bone("LeftLowerArm", "LeftForeArm"),
                Bone("LeftHand", "LeftHand"),

                Bone("RightShoulder", "RightShoulder"),
                Bone("RightUpperArm", "RightArm"),
                Bone("RightLowerArm", "RightForeArm"),
                Bone("RightHand", "RightHand"),

                Bone("LeftUpperLeg", "LeftUpLeg"),
                Bone("LeftLowerLeg", "LeftLeg"),
                Bone("LeftFoot", "LeftFoot"),
                Bone("LeftToes", "LeftToeBase"),

                Bone("RightUpperLeg", "RightUpLeg"),
                Bone("RightLowerLeg", "RightLeg"),
                Bone("RightFoot", "RightFoot"),
                Bone("RightToes", "RightToeBase"),
            };
            importer.humanDescription = hd;

            Debug.Log("[PLAGA44] PlayerModelImporter: PLAYER_rigged -> Humanoid rig with manual bone mapping");
        }

        private static HumanBone Bone(string humanName, string boneName)
        {
            return new HumanBone
            {
                humanName = humanName,
                boneName = boneName,
                limit = new HumanLimit { useDefaultValues = true }
            };
        }
    }
}
