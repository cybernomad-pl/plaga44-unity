// =============================================================================
// BuildPlayerRigSetup.cs
// Jednym kliknieciem buduje PELNY player rig + menu w AKTYWNEJ scenie:
//   1. OVRCameraRig                 (Meta SDK core prefab)      -- kamera VR
//   2. StylizedCharacterLocomotion  (Movement SDK sample prefab)-- SDK char (defaultRig)
//   3. PlayerRigSetup.Run(cfg)      -- CharacterController, Locomotion, SmoothTurn,
//                                      PlayerAvatar, PositionPersistence, FingerFreezer,
//                                      pozycja na terenie
//   4. _HamburgerMenu               (GO + HamburgerMenu.cs)     -- menu (autonomiczne)
// Kazdy krok idempotentny: jak element juz w scenie -> reuse, nie duplikuj.
// Menu: CYBERNOMAD/Tools/Build Player Rig.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class BuildPlayerRigSetup
    {
        private const string LOG = "[PLAGA44][BuildPlayerRig]";
        private const string OvrRigName = "OVRCameraRig";
        private const string SdkCharName = "StylizedCharacterLocomotion";
        private const string MenuName = "_HamburgerMenu";
        private const string ConfigPath = "Assets/PLAGA44/Config/BootstrapConfig_Quest.asset";

        [MenuItem("CYBERNOMAD/Tools/Build Player Rig", false, 2)]
        public static void Run()
        {
            // 1. Kamera VR -- bez niej rig nie istnieje.
            var rig = EnsureOvrCameraRig();
            if (rig == null) return; // blad zalogowany

            // 2. SDK char (defaultRig) -- PlayerRigSetup wiaze go do PlayerAvatar.
            //    Musi byc w scenie ZANIM PlayerRigSetup.Run, bo szuka go przez GameObject.Find.
            EnsureStylizedChar();

            // 3. Komponenty rig + pozycja na terenie. Wymaga configu -- bez niego
            //    parametry CC/Locomotion sa nieznane. NIE zgadujemy -> explicit fail.
            var cfg = AssetDatabase.LoadAssetAtPath<BootstrapConfig>(ConfigPath);
            if (cfg == null)
            {
                Debug.LogError($"{LOG} Brak {ConfigPath} -- nie skonfiguruje komponentow rig. "
                    + "OVRCameraRig i SDK char sa w scenie, ale bez CC/Locomotion/PlayerAvatar.");
                return;
            }
            PlayerRigSetup.Run(cfg);
            Debug.Log($"{LOG} Rig skonfigurowany przez PlayerRigSetup.");

            // 4. Menu -- autonomiczne (Start() samo znajduje rig i buduje canvas).
            EnsureHamburgerMenu();

            EditorUtility.SetDirty(rig);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rig.scene);
            Selection.activeGameObject = rig;
            SceneView.FrameLastActiveSceneView();
            Debug.Log($"{LOG} [OK] Pelny player rig + menu gotowe.");
        }

        // OVRCameraRig -- reuse istniejacego, albo instancjonuj z Meta SDK core prefab.
        private static GameObject EnsureOvrCameraRig()
        {
            var existing = GameObject.Find(OvrRigName);
            if (existing != null)
            {
                Debug.Log($"{LOG} [OK] {OvrRigName} juz w scenie.");
                return existing;
            }

            var prefab = LoadPrefabByExactName(OvrRigName);
            if (prefab == null)
            {
                Debug.LogError($"{LOG} Nie znalazlem prefabu {OvrRigName} (Meta SDK core). "
                    + "Czy pakiet com.meta.xr.sdk.core jest zainstalowany?");
                return null;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = OvrRigName;
            Undo.RegisterCreatedObjectUndo(inst, "Create OVRCameraRig");
            Debug.Log($"{LOG} [CREATED] {OvrRigName}");
            return inst;
        }

        // StylizedCharacterLocomotion -- SDK char sample. defaultRig dla PlayerAvatar.
        private static void EnsureStylizedChar()
        {
            var existing = GameObject.Find(SdkCharName);
            if (existing != null)
            {
                Debug.Log($"{LOG} [OK] {SdkCharName} juz w scenie.");
                return;
            }

            var prefab = LoadPrefabByExactName(SdkCharName);
            if (prefab == null)
            {
                Debug.LogError($"{LOG} Nie znalazlem prefabu {SdkCharName} (Meta XR Movement SDK sample). "
                    + "Czy sample 'ISDKLocomotion' jest zaimportowany?");
                return;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = SdkCharName;
            Undo.RegisterCreatedObjectUndo(inst, "Create StylizedCharacterLocomotion");
            Debug.Log($"{LOG} [CREATED] {SdkCharName}");
        }

        // _HamburgerMenu -- pusty GO + HamburgerMenu.cs. Komponent w Start() sam
        // znajduje OVRCameraRig i buduje canvas -- zero wiring w inspektorze.
        private static void EnsureHamburgerMenu()
        {
            if (Object.FindFirstObjectByType<Plaga44.UI.HamburgerMenu>() != null)
            {
                Debug.Log($"{LOG} [OK] HamburgerMenu juz w scenie.");
                return;
            }

            var go = new GameObject(MenuName);
            Undo.RegisterCreatedObjectUndo(go, "Create _HamburgerMenu");
            Undo.AddComponent<Plaga44.UI.HamburgerMenu>(go);
            Debug.Log($"{LOG} [CREATED] {MenuName} + HamburgerMenu");
        }

        // Znajdz prefab o DOKLADNEJ nazwie pliku (name.prefab). AssetDatabase widzi
        // Assets/ i Packages/. Pomija warianty (np. OVRCameraRigInteraction).
        private static GameObject LoadPrefabByExactName(string name)
        {
            string suffix = "/" + name + ".prefab";
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith(suffix))
                    return AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
            return null;
        }
    }
}
#endif
