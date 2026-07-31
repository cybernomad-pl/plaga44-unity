// =============================================================================
// DefaultMaleNpcSetup.cs
// CYBERNOMAD -- Buduje DEFAULTMALE_NPC (grywalny NPC jak PINEA) z modelu
// Assets/PLAGA44/Npc/DefaultMale/Models/DefaultMale.fbx i rejestruje go w galerii NPC.
//
// Pipeline (identyczny wzor jak NpcSystemSetup.BuildPrefab):
//   1. FBX -> Humanoid rig, generuj Avatar z tego modelu (CreateFromThisModel).
//   2. Waliduj Avatar (valid + human) -- inaczej ABORT z dokladnym powodem.
//   3. Material URP/Lit (szary) -- model nie ma embedded tekstur.
//   4. Prefab Resources/Npc/DEFAULTMALE_NPC.prefab: model + material na SMR +
//      Animator(avatar, no root motion) + CapsuleCollider + NpcController(shared library).
//   5. Rebuild NpcRegistry.asset skanem Resources/Npc/*_NPC (prefaby z NpcController) --
//      Pinea + DefaultMale + cokolwiek jeszcze. Galeria NPC czyta ten rejestr.
//
// Wspoldzieli Resources/Npc/NpcAnimationLibrary (humanoid klipy retargetuja sie na
// kazdy humanoid Animator -- DefaultMale gra to samo idle/walk co Pinea).
//
// UWAGA: NIE dodaje regionow chwytu ISDK (NpcGrabSetup jest hardcoded na PINEA_NPC.prefab
// i kosci mixamorig:* -- DefaultMale ma inny szkielet: LeftUpperArm/LowerArm, brak Spine2/
// HeadTop_End). Generalizacja grabu = osobne zadanie (mapowanie kosci, ZERO zgadywania).
//
// ZERO FALLBACKOW: brak modelu / niepoprawny humanoid Avatar / brak library / brak SMR /
// zapis prefabu nieudany -> Debug.LogError + abort. Nic nie zgadujemy.
// Idempotent: nadpisuje prefab + material + przebudowuje rejestr.
// Menu: CYBERNOMAD/Setup/DefaultMale NPC
// =============================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Plaga44.Npc;

namespace Plaga44.Editor.Setup
{
    public static class DefaultMaleNpcSetup
    {
        private const string LOG = "[PLAGA44][DefaultMaleNpcSetup]";

        private const string ResourcesNpcDir = "Assets/Resources/Npc";
        private const string LibraryPath      = "Assets/Resources/Npc/NpcAnimationLibrary.asset";
        private const string RegistryPath     = "Assets/Resources/Npc/NpcRegistry.asset";

        private const string NpcName    = "DEFAULTMALE_NPC";
        private const string ModelPath    = "Assets/PLAGA44/Npc/DefaultMale/Models/DefaultMale.fbx";
        private const string MaterialDir  = "Assets/PLAGA44/Npc/DefaultMale/Materials";
        private const string MaterialPath = "Assets/PLAGA44/Npc/DefaultMale/Materials/DefaultMaleGrey.mat";
        private const string PrefabPath   = "Assets/Resources/Npc/DEFAULTMALE_NPC.prefab";

        private const string UrpLitShader = "Universal Render Pipeline/Lit";

        [MenuItem("CYBERNOMAD/Setup/DefaultMale NPC")]
        public static void Run()
        {
            Debug.Log($"{LOG} ===== DefaultMale NPC START =====");

            if (!BuildNpcPrefab())
                return; // error zalogowany

            if (!RebuildRegistry())
                return; // error zalogowany

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"{LOG} ===== DefaultMale NPC DONE =====");
        }

        // ---------------------------------------------------------------------
        // Build DEFAULTMALE_NPC.prefab (wzor: NpcSystemSetup.BuildPrefab)
        // ---------------------------------------------------------------------
        private static bool BuildNpcPrefab()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"{LOG} [ABORT] Brak ModelImporter: {ModelPath} " +
                               "(czy DefaultMale.fbx jest w projekcie i zaimportowany?).");
                return false;
            }

            // Humanoid + generuj Avatar z tego modelu.
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();

            Avatar avatar = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
                if (obj is Avatar a) { avatar = a; break; }

            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError($"{LOG} [ABORT] Brak poprawnego humanoid Avatara dla {ModelPath} " +
                               $"(avatar={(avatar == null ? "null" : avatar.name)}, valid={avatar?.isValid}, human={avatar?.isHuman}). " +
                               "Rig nie mapuje sie na humanoid (sprawdz nazwy kosci: Hips/Spine/Chest/Neck/Head, " +
                               "LeftUpperArm/LeftLowerArm/LeftHand, LeftUpperLeg/LeftLowerLeg/LeftFoot).");
                return false;
            }

            var library = AssetDatabase.LoadAssetAtPath<NpcAnimationLibrary>(LibraryPath);
            if (library == null)
            {
                Debug.LogError($"{LOG} [ABORT] Brak biblioteki animacji: {LibraryPath} " +
                               "(odpal najpierw PLAGA44/Setup/NPC System (Full) dla Piny -- buduje wspolna library).");
                return false;
            }

            var material = LoadOrCreateGreyMaterial();
            if (material == null)
                return false; // error zalogowany

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError($"{LOG} [ABORT] Nie zaladowalem modelu {ModelPath}.");
                return false;
            }

            var instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (instance == null)
            {
                Debug.LogError($"{LOG} [ABORT] InstantiatePrefab zwrocil null dla {ModelPath}.");
                return false;
            }

            try
            {
                instance.name = NpcName;

                var smrs = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (smrs.Length == 0)
                {
                    Debug.LogError($"{LOG} [ABORT] Brak SkinnedMeshRenderer w modelu {ModelPath} " +
                                   "(mesh nie zaimportowany albo model bez skinningu).");
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
                var saved = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath, out bool ok);
                if (!ok || saved == null)
                {
                    Debug.LogError($"{LOG} [ABORT] SaveAsPrefabAsset nieudany ({PrefabPath}).");
                    return false;
                }

                Debug.Log($"{LOG} [OK] {NpcName} prefab: {PrefabPath} " +
                          $"({smrs.Length} SkinnedMeshRenderer, avatar='{avatar.name}', library={library.Count} klipow).");
                return true;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        // ---------------------------------------------------------------------
        // Material URP/Lit szary -- DefaultMale nie ma embedded tekstur.
        // Deterministyczne stworzenie assetu (nie fallback zachowania).
        // ---------------------------------------------------------------------
        private static Material LoadOrCreateGreyMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null) return existing;

            var shader = Shader.Find(UrpLitShader);
            if (shader == null)
            {
                Debug.LogError($"{LOG} [ABORT] Shader nie znaleziony: {UrpLitShader} (czy URP aktywne?).");
                return null;
            }

            EnsureFolder(MaterialDir);
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.6f, 0.6f, 0.6f, 1f));
            AssetDatabase.CreateAsset(mat, MaterialPath);
            Debug.Log($"{LOG} Material stworzony: {MaterialPath}");
            return mat;
        }

        // ---------------------------------------------------------------------
        // Rebuild rejestru -- skan Resources/Npc/ dla prefabow z NpcController.
        // Rejestr = skan Resources/Npc/*_NPC.prefab z NpcController (Pinea + DefaultMale + ...).
        // ---------------------------------------------------------------------
        private static bool RebuildRegistry()
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

        // "DEFAULTMALE_NPC" -> "Defaultmale", "PINEA_NPC" -> "Pinea". Strip _NPC, TitleCase.
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
