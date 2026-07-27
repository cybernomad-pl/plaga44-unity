// =============================================================================
// NpcRegistrySetup.cs
// Buduje NPC do galerii + rejestr (NpcRegistry).
//   1. ETHAN_NPC.prefab z modelu Ethan (humanoid, wspolna NpcAnimationLibrary) --
//      wzor: NpcSystemSetup.BuildPrefab (Pinea).
//   2. NpcRegistry.asset skanem Resources/Npc/*_NPC.prefab (kazdy prefab z
//      NpcController). Dodanie kolejnego NPC = zbuduj prefab + odpal to menu.
// Wspoldzieli NpcAnimationLibrary (klipy humanoid retargetuja sie na kazdy
// humanoid Animator -- Ethan gra to samo co Pinea).
// ZERO fallbacks: brak modelu/materialu/library/avatara -> LogError + abort.
// Menu: CYBERNOMAD/Setup/NPC Registry (Ethan + rejestr)
// =============================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Plaga44.Npc;

namespace Plaga44.Editor.Setup
{
    public static class NpcRegistrySetup
    {
        private const string LOG = "[PLAGA44][NpcRegistrySetup]";

        private const string ResourcesNpcDir = "Assets/Resources/Npc";
        private const string LibraryPath     = "Assets/Resources/Npc/NpcAnimationLibrary.asset";
        private const string RegistryPath    = "Assets/Resources/Npc/NpcRegistry.asset";

        private const string EthanModelPath    = "Assets/PLAGA44/Npc/Ethan/Models/Ethan.fbx";
        private const string EthanMaterialPath = "Assets/PLAGA44/Npc/Ethan/Materials/EthanGrey.mat";
        private const string EthanPrefabPath   = "Assets/Resources/Npc/ETHAN_NPC.prefab";

        [MenuItem("CYBERNOMAD/Setup/NPC Registry (Ethan + rejestr)")]
        public static void Run()
        {
            Debug.Log($"{LOG} ===== NPC Registry START =====");

            // 1. ETHAN_NPC.prefab (idempotentnie -- nadpisuje jesli byl).
            if (!BuildNpcPrefab(EthanModelPath, EthanMaterialPath, "ETHAN_NPC", EthanPrefabPath))
                return; // error zalogowany

            // 2. Rejestr = skan Resources/Npc/ (Pinea + Ethan + cokolwiek jeszcze).
            if (!BuildRegistry())
                return; // error zalogowany

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"{LOG} ===== NPC Registry DONE =====");
        }

        // ---------------------------------------------------------------------
        // Build pojedynczego NPC prefab z modelu humanoid (wzor: NpcSystemSetup)
        // ---------------------------------------------------------------------
        private static bool BuildNpcPrefab(string modelPath, string materialPath, string npcName, string prefabPath)
        {
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"{LOG} [ABORT] Brak ModelImporter: {modelPath} (czy model istnieje?).");
                return false;
            }

            // Humanoid + generuj Avatar z tego modelu.
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();

            Avatar avatar = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (obj is Avatar a) { avatar = a; break; }

            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError($"{LOG} [ABORT] {npcName}: brak poprawnego humanoid Avatara dla {modelPath} " +
                               $"(valid={avatar?.isValid}, human={avatar?.isHuman}). Rig nie mapuje sie na humanoid?");
                return false;
            }

            var library = AssetDatabase.LoadAssetAtPath<NpcAnimationLibrary>(LibraryPath);
            if (library == null)
            {
                Debug.LogError($"{LOG} [ABORT] Brak biblioteki animacji: {LibraryPath} " +
                               "(odpal najpierw PLAGA44/Setup/NPC System (Full) dla Piny).");
                return false;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Debug.LogError($"{LOG} [ABORT] {npcName}: brak materialu {materialPath}.");
                return false;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogError($"{LOG} [ABORT] {npcName}: nie zaladowalem modelu {modelPath}.");
                return false;
            }

            var instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (instance == null)
            {
                Debug.LogError($"{LOG} [ABORT] {npcName}: InstantiatePrefab zwrocil null.");
                return false;
            }

            try
            {
                instance.name = npcName;

                var smrs = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (smrs.Length == 0)
                {
                    Debug.LogError($"{LOG} [ABORT] {npcName}: brak SkinnedMeshRenderer w modelu.");
                    return false;
                }
                foreach (var smr in smrs)
                {
                    var mats = new Material[smr.sharedMaterials.Length == 0 ? 1 : smr.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = material;
                    smr.sharedMaterials = mats;
                }

                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.applyRootMotion = false;

                if (instance.GetComponent<CapsuleCollider>() == null)
                    instance.AddComponent<CapsuleCollider>();

                var controller = instance.GetComponent<NpcController>();
                if (controller == null) controller = instance.AddComponent<NpcController>();
                controller.library = library;

                EnsureFolder(ResourcesNpcDir);
                var saved = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out bool ok);
                if (!ok || saved == null)
                {
                    Debug.LogError($"{LOG} [ABORT] {npcName}: SaveAsPrefabAsset nieudany ({prefabPath}).");
                    return false;
                }

                Debug.Log($"{LOG} [OK] {npcName} prefab: {prefabPath} " +
                          $"({smrs.Length} SkinnedMeshRenderer, library={library.Count} klipow).");
                return true;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        // ---------------------------------------------------------------------
        // Build rejestru -- skan Resources/Npc/ dla prefabow z NpcController
        // ---------------------------------------------------------------------
        private static bool BuildRegistry()
        {
            EnsureFolder(ResourcesNpcDir);

            var entries = new List<NpcRegistry.Entry>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { ResourcesNpcDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                if (go.GetComponent<NpcController>() == null) continue; // tylko realne NPC

                entries.Add(new NpcRegistry.Entry { name = PrettyName(go.name), prefab = go });
            }

            if (entries.Count == 0)
            {
                Debug.LogError($"{LOG} [ABORT] Rejestr pusty -- brak prefabow z NpcController w {ResourcesNpcDir}.");
                return false;
            }

            entries.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));

            var registry = AssetDatabase.LoadAssetAtPath<NpcRegistry>(RegistryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<NpcRegistry>();
                registry.npcs = entries;
                AssetDatabase.CreateAsset(registry, RegistryPath);
            }
            else
            {
                registry.npcs = entries;
                EditorUtility.SetDirty(registry);
            }
            AssetDatabase.SaveAssets();

            var names = new List<string>();
            foreach (var e in entries) names.Add(e.name);
            Debug.Log($"{LOG} [OK] NpcRegistry ({entries.Count}): {string.Join(", ", names)} -> {RegistryPath}");
            return true;
        }

        // "ETHAN_NPC" -> "Ethan", "PINEA_NPC" -> "Pinea". Strip sufiks _NPC, TitleCase.
        private static string PrettyName(string prefabName)
        {
            string core = prefabName.EndsWith("_NPC", System.StringComparison.OrdinalIgnoreCase)
                ? prefabName.Substring(0, prefabName.Length - 4)
                : prefabName;
            if (core.Length == 0) return prefabName;
            return char.ToUpper(core[0]) + core.Substring(1).ToLower();
        }

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
