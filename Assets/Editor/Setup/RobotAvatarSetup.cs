// =============================================================================
// RobotAvatarSetup.cs
// CYBERNOMAD -- Tworzy Assets/PLAGA44/Avatars/ROBOT/ROBOT.prefab jako PREFAB
// VARIANT wskazujacy na StylizedCharacterLocomotion (Meta XR Movement SDK).
// Dziedziczy pelny rig, Animator, SkinnedMesh i animacje.
//
// Wywolanie: Bootstrap przed AvatarRegistrySetup -> rejestr go podlapie.
//
// Jesli source SDK prefab jest niedostepny (package missing) -> log error, skip.
// =============================================================================
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class RobotAvatarSetup
    {
        private const string LOG              = "[PLAGA44][RobotAvatarSetup]";
        private const string SourcePrefabPath = "Assets/Samples/Meta XR Movement SDK/83.0.0/Advanced Samples/ISDKLocomotion/Prefabs/StylizedCharacterLocomotion.prefab";
        private const string TargetFolder     = "Assets/PLAGA44/Avatars/ROBOT";
        private const string TargetPrefabPath = "Assets/PLAGA44/Avatars/ROBOT/ROBOT.prefab";
        private const string AvatarsRoot      = "Assets/PLAGA44/Avatars";

        public static bool Run(BootstrapConfig cfg)
        {
            // Idempotent -- jesli istnieje, nic nie rob
            if (File.Exists(TargetPrefabPath))
            {
                Debug.Log($"{LOG} [OK] ROBOT.prefab already exists at {TargetPrefabPath}");
                return false;
            }

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (source == null)
            {
                Debug.LogError($"{LOG} [MISSING] Source prefab not found: {SourcePrefabPath}. "
                    + "Meta XR Movement SDK samples nie zaimportowane?");
                return false;
            }

            if (!AssetDatabase.IsValidFolder(AvatarsRoot))
            {
                Debug.LogError($"{LOG} [MISSING] {AvatarsRoot} folder missing -- cannot create ROBOT avatar");
                return false;
            }
            if (!AssetDatabase.IsValidFolder(TargetFolder))
                AssetDatabase.CreateFolder(AvatarsRoot, "ROBOT");

            // Save as prefab variant -- SaveAsPrefabAsset on a prefab instance creates
            // a new prefab that can be configured as variant via CreateVariant below.
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = "ROBOT";

            bool success;
            var variant = PrefabUtility.SaveAsPrefabAsset(instance, TargetPrefabPath, out success);
            Object.DestroyImmediate(instance);

            if (!success || variant == null)
            {
                Debug.LogError($"{LOG} [FAIL] Could not save ROBOT prefab at {TargetPrefabPath}");
                return false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"{LOG} [CREATED] {TargetPrefabPath} (instance of {source.name})");
            return true;
        }
    }
}
#endif
