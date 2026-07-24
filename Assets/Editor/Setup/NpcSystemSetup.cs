// =============================================================================
// NpcSystemSetup.cs
// One-shot editor automation for the PINEA NPC system.
//   KROK 1: PINEA_YNG.fbx -> Humanoid rig, generate Avatar.
//   KROK 2: Mixamo .fbx    -> Humanoid, CopyFromOther(Pinea avatar), loop on.
//   KROK 3: Build Resources/Npc/NpcAnimationLibrary.asset from imported clips.
//   KROK 4: Build Resources/Npc/PINEA_NPC.prefab (model + material + Animator +
//           CapsuleCollider + NpcController).
// ZERO fallbacks: any missing prerequisite -> Debug.LogError + abort.
// Menu: PLAGA44/Setup/NPC System (Full)
// =============================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Plaga44.Npc;

namespace Plaga44.Editor.Setup
{
    public static class NpcSystemSetup
    {
        private const string LOG = "[PLAGA44][NpcSystemSetup]";

        private const string PineaGuid   = "5426c37ae48054a4ca80b70fdb37f0d0";
        private const string MaterialPath = "Assets/PINEA_YNG/PackedMaterial0mat.mat";
        private const string MixamoDir    = "Assets/PLAGA44/Animations/Mixamo";

        private const string ResourcesNpcDir = "Assets/Resources/Npc";
        private const string LibraryPath      = "Assets/Resources/Npc/NpcAnimationLibrary.asset";
        private const string PrefabPath       = "Assets/Resources/Npc/PINEA_NPC.prefab";

        [MenuItem("PLAGA44/Setup/NPC System (Full)")]
        public static void Run()
        {
            Debug.Log($"{LOG} ===== NPC System (Full) START =====");

            // --- KROK 1 --------------------------------------------------------
            Avatar pineaAvatar = SetupPineaHumanoid(out string pineaPath);
            if (pineaAvatar == null) return; // error already logged

            // --- KROK 2 --------------------------------------------------------
            List<string> animPaths = SetupMixamoAnimations(pineaAvatar);
            if (animPaths == null) return; // error already logged

            // --- KROK 3 --------------------------------------------------------
            NpcAnimationLibrary library = BuildLibrary(animPaths);
            if (library == null) return; // error already logged

            // --- KROK 4 --------------------------------------------------------
            if (!BuildPrefab(pineaPath, pineaAvatar, library)) return; // error already logged

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"{LOG} ===== NPC System (Full) DONE =====");
        }

        // ---------------------------------------------------------------------
        // KROK 1 -- Pinea humanoid + Avatar
        // ---------------------------------------------------------------------
        private static Avatar SetupPineaHumanoid(out string pineaPath)
        {
            pineaPath = AssetDatabase.GUIDToAssetPath(PineaGuid);
            if (string.IsNullOrEmpty(pineaPath) || !File.Exists(pineaPath))
            {
                Debug.LogError($"{LOG} [KROK 1][ABORT] PINEA_YNG.fbx not found (guid {PineaGuid}).");
                return null;
            }

            var importer = AssetImporter.GetAtPath(pineaPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"{LOG} [KROK 1][ABORT] No ModelImporter at {pineaPath}.");
                return null;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
            Debug.Log($"{LOG} [KROK 1] Reimported {pineaPath} as Humanoid (CreateFromThisModel).");

            Avatar avatar = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(pineaPath))
            {
                if (obj is Avatar a) { avatar = a; break; }
            }

            if (avatar == null)
            {
                Debug.LogError($"{LOG} [KROK 1][ABORT] No Avatar generated for {pineaPath}. " +
                               "Rig may not be humanoid-mappable.");
                return null;
            }
            if (!avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError($"{LOG} [KROK 1][ABORT] Avatar '{avatar.name}' invalid " +
                               $"(isValid={avatar.isValid}, isHuman={avatar.isHuman}).");
                return null;
            }

            Debug.Log($"{LOG} [KROK 1][OK] Avatar '{avatar.name}' (valid humanoid).");
            return avatar;
        }

        // ---------------------------------------------------------------------
        // KROK 2 -- Mixamo animations, humanoid, CopyFromOther(Pinea)
        // ---------------------------------------------------------------------
        private static List<string> SetupMixamoAnimations(Avatar pineaAvatar)
        {
            if (!AssetDatabase.IsValidFolder(MixamoDir))
            {
                Debug.LogError($"{LOG} [KROK 2][ABORT] Folder not found: {MixamoDir}.");
                return null;
            }

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { MixamoDir });
            if (guids.Length == 0)
            {
                Debug.LogError($"{LOG} [KROK 2][ABORT] No .fbx models in {MixamoDir}.");
                return null;
            }

            var paths = new List<string>();
            int configured = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    Debug.LogError($"{LOG} [KROK 2] No ModelImporter at {path} -- skipping this file.");
                    continue;
                }

                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = pineaAvatar;

                // loopTime on: idle/walk/run loop; one-shots looping too is acceptable per plan.
                var clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
                if (clips != null)
                {
                    for (int i = 0; i < clips.Length; i++) clips[i].loopTime = true;
                    importer.clipAnimations = clips;
                }

                importer.SaveAndReimport();
                paths.Add(path);
                configured++;
            }

            if (configured == 0)
            {
                Debug.LogError($"{LOG} [KROK 2][ABORT] Found {guids.Length} models but none were .fbx importable.");
                return null;
            }

            Debug.Log($"{LOG} [KROK 2][OK] Configured {configured} Mixamo animation fbx as Humanoid (CopyFromOther).");
            return paths;
        }

        // ---------------------------------------------------------------------
        // KROK 3 -- NpcAnimationLibrary.asset
        // ---------------------------------------------------------------------
        private static NpcAnimationLibrary BuildLibrary(List<string> animPaths)
        {
            var clips = new List<AnimationClip>();
            var names = new List<string>();

            foreach (var path in animPaths)
            {
                AnimationClip clip = null;
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (obj is AnimationClip c && !c.name.StartsWith("__preview"))
                    {
                        clip = c;
                        break;
                    }
                }

                if (clip == null)
                {
                    Debug.LogError($"{LOG} [KROK 3] No AnimationClip in {path} -- skipping.");
                    continue;
                }

                clips.Add(clip);
                names.Add(Path.GetFileNameWithoutExtension(path));
            }

            if (clips.Count == 0)
            {
                Debug.LogError($"{LOG} [KROK 3][ABORT] No AnimationClips collected -- library would be empty.");
                return null;
            }

            EnsureFolder(ResourcesNpcDir);

            var library = ScriptableObject.CreateInstance<NpcAnimationLibrary>();
            library.clips = clips.ToArray();
            library.displayNames = names.ToArray();

            var existing = AssetDatabase.LoadAssetAtPath<NpcAnimationLibrary>(LibraryPath);
            if (existing != null) AssetDatabase.DeleteAsset(LibraryPath);

            AssetDatabase.CreateAsset(library, LibraryPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LOG} [KROK 3][OK] NpcAnimationLibrary at {LibraryPath} with {library.clips.Length} clips.");
            return library;
        }

        // ---------------------------------------------------------------------
        // KROK 4 -- PINEA_NPC.prefab
        // ---------------------------------------------------------------------
        private static bool BuildPrefab(string pineaPath, Avatar pineaAvatar, NpcAnimationLibrary library)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(pineaPath);
            if (model == null)
            {
                Debug.LogError($"{LOG} [KROK 4][ABORT] Could not load model GameObject at {pineaPath}.");
                return false;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Debug.LogError($"{LOG} [KROK 4][ABORT] Material not found: {MaterialPath}.");
                return false;
            }

            var instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (instance == null)
            {
                Debug.LogError($"{LOG} [KROK 4][ABORT] InstantiatePrefab returned null for {pineaPath}.");
                return false;
            }

            try
            {
                instance.name = "PINEA_NPC";

                // Material on all SkinnedMeshRenderers.
                var smrs = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (smrs.Length == 0)
                {
                    Debug.LogError($"{LOG} [KROK 4][ABORT] No SkinnedMeshRenderer under {pineaPath}.");
                    return false;
                }
                foreach (var smr in smrs)
                {
                    var mats = new Material[smr.sharedMaterials.Length == 0 ? 1 : smr.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = material;
                    smr.sharedMaterials = mats;
                }

                // Animator (humanoid avatar, no root motion).
                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.avatar = pineaAvatar;
                animator.applyRootMotion = false;

                // CapsuleCollider.
                if (instance.GetComponent<CapsuleCollider>() == null)
                    instance.AddComponent<CapsuleCollider>();

                // NpcController with injected library.
                var controller = instance.GetComponent<NpcController>();
                if (controller == null) controller = instance.AddComponent<NpcController>();
                controller.library = library;

                EnsureFolder(ResourcesNpcDir);
                var saved = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath, out bool ok);
                if (!ok || saved == null)
                {
                    Debug.LogError($"{LOG} [KROK 4][ABORT] SaveAsPrefabAsset failed for {PrefabPath}.");
                    return false;
                }

                Debug.Log($"{LOG} [KROK 4][OK] Prefab saved: {PrefabPath} " +
                          $"({smrs.Length} SkinnedMeshRenderer(s), library={library.clips.Length} clips).");
                return true;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------
        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
