// =============================================================================
// MixamoAvatarImporter.cs
// CYBERNOMAD -- AssetPostprocessor dla Mixamo FBX w Assets/PLAGA44/Avatars/.
// Ustawia ModelImporter settings PRZED pierwszym importem Unity (nie post-factum
// reimport). Eliminuje Rig Error bo Unity od razu widzi:
//   - Humanoid rig
//   - Avatar CreateFromThisModel (regen z T-pose)
//   - importAnimation = false (zero klipow z Mixamo -> zero bone mismatch)
//   - optimizeGameObjects = false (retargeter SDK wymaga bone transformow)
//   - materialLocation = External (URP pipeline)
//
// Komplementarne do MixamoMaterialExtractor (post-import URP conversion).
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public class MixamoAvatarImporter : AssetPostprocessor
    {
        private const string LOG         = "[PLAGA44][MixamoAvatarImporter]";
        private const string AvatarsRoot = "Assets/PLAGA44/Avatars/";

        // -----------------------------------------------------------------
        // Pre-import: ustawia settings zanim Unity zacznie import
        // -----------------------------------------------------------------
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(AvatarsRoot, System.StringComparison.OrdinalIgnoreCase))
                return;
            if (!assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                return;

            var m = assetImporter as ModelImporter;
            if (m == null) return;

            // Humanoid + CreateFromThisModel -- regen avatar z aktualnego T-pose
            m.animationType = ModelImporterAnimationType.Human;
            m.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;

            // Zero klipow animacji -- eliminacja bone length mismatch warning.
            // Retarget = live body tracking z Questa, klipy Mixamo niepotrzebne.
            m.importAnimation   = false;
            m.importConstraints = false;

            // Retargeter SDK wymaga bone Transformow -- optimize je zwija do cache.
            m.optimizeGameObjects = false;

            // URP pipeline: materialy jako osobne assety w <Folder>/Materials/.
            m.materialLocation = ModelImporterMaterialLocation.External;

            // VRAM Quest -- mesh nie musi byc readable z CPU.
            m.isReadable = false;

            Debug.Log($"{LOG} [PRE] Configured {assetPath} (Humanoid, no-anim, no-optimize, external-mat)");
        }

        // -----------------------------------------------------------------
        // Post-import: walidacja (avatar valid? humanoid? bones mapped?)
        // -----------------------------------------------------------------
        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(AvatarsRoot, System.StringComparison.OrdinalIgnoreCase))
                return;
            if (!assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                return;
            if (root == null) return;

            var animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogWarning($"{LOG} [POST] {assetPath} -- no Animator (mesh without rig?)");
                return;
            }

            if (animator.avatar == null)
            {
                Debug.LogWarning($"{LOG} [POST] {assetPath} -- Animator.avatar is NULL (rig import failed)");
                return;
            }
            if (!animator.avatar.isValid)
            {
                Debug.LogWarning($"{LOG} [POST] {assetPath} -- Avatar.isValid=false (bone hierarchy broken)");
                return;
            }
            if (!animator.avatar.isHuman)
            {
                Debug.LogWarning($"{LOG} [POST] {assetPath} -- Avatar.isHuman=false (custom rig, Meta XR retargeter nie bedzie dzialac)");
                return;
            }

            Debug.Log($"{LOG} [POST] {assetPath} -- OK Humanoid avatar valid");
        }
    }
}
#endif
