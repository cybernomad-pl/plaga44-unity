// =============================================================================
// AkslopeAISetup.cs
// One-shot editor automation: nadaje 5 modelom AKSLOPE humanoid rig + prymitywne
// AI wander z losowa animacja poruszania.
//   KROK 1: AKSLOPE.fbx -> Humanoid, CopyFromOther(Avatar Pinei). SaveAndReimport.
//   KROK 2: w otwartej scenie znajdz instancje AKSLOPE -> Animator(avatar,noRoot)
//           + CapsuleCollider + NpcController(library) + AkslopeWanderAI. Zapisz scene.
//
// WYMAGANIE: najpierw uruchom PLAGA44/Setup/NPC System (Full) -- Pinea musi byc
// humanoid z wygenerowanym Avatarem, a library musi istniec.
//
// ZERO FALLBACKOW: kazdy brak prerekwizytu -> Debug.LogError + abort/skip z logiem.
// Menu: PLAGA44/Setup/AKSLOPE AI
// =============================================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Plaga44.Npc;

namespace Plaga44.Editor.Setup
{
    public static class AkslopeAISetup
    {
        private const string LOG = "[PLAGA44][AkslopeAISetup]";

        private const string AkslopeGuid = "d06a9a23a1a2c954197552e2dc7a9c4b";
        private const string PineaGuid   = "5426c37ae48054a4ca80b70fdb37f0d0";
        private const string LibraryResourcePath = "Npc/NpcAnimationLibrary";

        [MenuItem("PLAGA44/Setup/AKSLOPE AI")]
        public static void Run()
        {
            Debug.Log($"{LOG} ===== AKSLOPE AI START =====");

            // --- KROK 1: rig humanoid AKSLOPE (CopyFromOther Pinea) ---------------
            Avatar akslopeAvatar = SetupAkslopeHumanoid(out string akslopePath);
            if (akslopeAvatar == null) return; // error already logged

            // --- Library (z Resources -- zbudowana przez NPC System Full) ---------
            var library = Resources.Load<NpcAnimationLibrary>(LibraryResourcePath);
            if (library == null)
            {
                Debug.LogError($"{LOG} [ABORT] Brak library Resources/{LibraryResourcePath}. " +
                               "Najpierw uruchom PLAGA44/Setup/NPC System (Full).");
                return;
            }
            if (library.Count == 0)
            {
                Debug.LogError($"{LOG} [ABORT] Library pusta (Count=0). " +
                               "Najpierw uruchom PLAGA44/Setup/NPC System (Full).");
                return;
            }

            // --- KROK 2: oprawa instancji w otwartej scenie -----------------------
            if (!SetupSceneInstances(akslopePath, akslopeAvatar, library)) return;

            Debug.Log($"{LOG} ===== AKSLOPE AI DONE =====");
        }

        // ---------------------------------------------------------------------
        // KROK 1 -- AKSLOPE.fbx humanoid, avatar skopiowany z Pinei
        // ---------------------------------------------------------------------
        private static Avatar SetupAkslopeHumanoid(out string akslopePath)
        {
            akslopePath = AssetDatabase.GUIDToAssetPath(AkslopeGuid);
            if (string.IsNullOrEmpty(akslopePath) || !File.Exists(akslopePath))
            {
                Debug.LogError($"{LOG} [KROK 1][ABORT] AKSLOPE.fbx nie znaleziony (guid {AkslopeGuid}).");
                return null;
            }

            // Avatar Pinei jako zrodlo (CopyFromOther).
            Avatar pineaAvatar = ResolvePineaAvatar();
            if (pineaAvatar == null) return null; // error already logged

            var importer = AssetImporter.GetAtPath(akslopePath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"{LOG} [KROK 1][ABORT] Brak ModelImporter na {akslopePath}.");
                return null;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = pineaAvatar;
            importer.SaveAndReimport();
            Debug.Log($"{LOG} [KROK 1] Reimport {akslopePath} jako Humanoid (CopyFromOther Pinea).");

            Avatar avatar = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(akslopePath))
            {
                if (obj is Avatar a) { avatar = a; break; }
            }

            if (avatar == null)
            {
                Debug.LogError($"{LOG} [KROK 1][ABORT] Nie wygenerowano Avatara dla {akslopePath}. " +
                               "Szkielet moze nie byc humanoid-mapowalny.");
                return null;
            }
            if (!avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError($"{LOG} [KROK 1][ABORT] Avatar '{avatar.name}' nieprawidlowy " +
                               $"(isValid={avatar.isValid}, isHuman={avatar.isHuman}).");
                return null;
            }

            Debug.Log($"{LOG} [KROK 1][OK] Avatar AKSLOPE '{avatar.name}' (poprawny humanoid).");
            return avatar;
        }

        private static Avatar ResolvePineaAvatar()
        {
            string pineaPath = AssetDatabase.GUIDToAssetPath(PineaGuid);
            if (string.IsNullOrEmpty(pineaPath) || !File.Exists(pineaPath))
            {
                Debug.LogError($"{LOG} [KROK 1][ABORT] PINEA_YNG.fbx nie znaleziony (guid {PineaGuid}).");
                return null;
            }

            var importer = AssetImporter.GetAtPath(pineaPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"{LOG} [KROK 1][ABORT] Brak ModelImporter na {pineaPath}.");
                return null;
            }
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                Debug.LogError($"{LOG} [KROK 1][ABORT] Pinea nie jest humanoid (animationType={importer.animationType}). " +
                               "Najpierw uruchom PLAGA44/Setup/NPC System (Full).");
                return null;
            }

            Avatar pineaAvatar = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(pineaPath))
            {
                if (obj is Avatar a) { pineaAvatar = a; break; }
            }
            if (pineaAvatar == null || !pineaAvatar.isValid || !pineaAvatar.isHuman)
            {
                Debug.LogError($"{LOG} [KROK 1][ABORT] Brak poprawnego Avatara Pinei w {pineaPath}. " +
                               "Najpierw uruchom PLAGA44/Setup/NPC System (Full).");
                return null;
            }
            return pineaAvatar;
        }

        // ---------------------------------------------------------------------
        // KROK 2 -- instancje AKSLOPE w otwartej scenie
        // ---------------------------------------------------------------------
        private static bool SetupSceneInstances(string akslopePath, Avatar akslopeAvatar, NpcAnimationLibrary library)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError($"{LOG} [KROK 2][ABORT] Brak zaladowanej aktywnej sceny.");
                return false;
            }

            var targets = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (IsAkslopeInstance(root, akslopePath))
                    targets.Add(root);
            }

            if (targets.Count == 0)
            {
                Debug.LogError($"{LOG} [KROK 2][ABORT] W scenie '{scene.name}' nie znaleziono zadnej instancji AKSLOPE " +
                               "(zrodlo=AKSLOPE.fbx lub nazwa zaczyna sie 'AKSLOPE').");
                return false;
            }

            int equipped = 0;
            foreach (var go in targets)
            {
                // Animator (humanoid avatar, brak root motion).
                var animator = go.GetComponent<Animator>();
                if (animator == null) animator = Undo.AddComponent<Animator>(go);
                animator.avatar = akslopeAvatar;
                animator.applyRootMotion = false;

                // CapsuleCollider.
                if (go.GetComponent<CapsuleCollider>() == null)
                    Undo.AddComponent<CapsuleCollider>(go);

                // NpcController (+ library).
                var controller = go.GetComponent<NpcController>();
                if (controller == null) controller = Undo.AddComponent<NpcController>(go);
                controller.library = library;

                // AkslopeWanderAI.
                if (go.GetComponent<AkslopeWanderAI>() == null)
                    Undo.AddComponent<AkslopeWanderAI>(go);

                EditorUtility.SetDirty(go);
                equipped++;
                Debug.Log($"{LOG} [KROK 2] Oprawiono '{go.name}' (Animator+Capsule+NpcController+AkslopeWanderAI).");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            if (!saved)
            {
                Debug.LogError($"{LOG} [KROK 2][ABORT] SaveScene nie powiodl sie dla '{scene.name}'.");
                return false;
            }

            Debug.Log($"{LOG} [KROK 2][OK] Oprawiono {equipped} instancji AKSLOPE (oczekiwano 5). Scena '{scene.name}' zapisana.");
            return true;
        }

        // Instancja AKSLOPE = zrodlo prefabu to AKSLOPE.fbx LUB nazwa zaczyna sie "AKSLOPE".
        private static bool IsAkslopeInstance(GameObject go, string akslopePath)
        {
            if (go.name.StartsWith("AKSLOPE", System.StringComparison.OrdinalIgnoreCase))
                return true;

            var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (source != null)
            {
                string srcPath = AssetDatabase.GetAssetPath(source);
                if (!string.IsNullOrEmpty(srcPath) &&
                    srcPath.Equals(akslopePath, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
#endif
