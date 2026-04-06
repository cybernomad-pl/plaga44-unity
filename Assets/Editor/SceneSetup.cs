// SceneSetup.cs -- CYBERNOMAD Editor Tool
//
// Otwiera TESTBED_V2 scene i stawia na niej wszystko co trzeba do testowania.
// Menu: CYBERNOMAD > Scene Setup > Load Testbed
//
// Co robi:
//   1. Otwiera TESTBED_V2.unity
//   2. Usuwa Main Camera (jesli jest)
//   3. Wstawia OVRCameraRig z prefaba Meta SDK (lub fallback Camera)
//   4. Dodaje CharacterController na rig root
//   5. Dodaje LocomotionController + SprintModifier + ComfortVignette
//   5. Dodaje LocomotionManager
//   6. Tworzy podloge (Plane 50x50m) z szachownica
//   7. Dodaje swiatlo kierunkowe
//   8. Ustawia GameState na Playing
//
// Public API:
//   SceneSetup.LoadTestbed();

using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class SceneSetup
    {
        private const string LOG = "[PLAGA44]";

        // =====================================================================
        // Menu
        // =====================================================================

        [MenuItem("CYBERNOMAD/Scene Setup/Load Testbed", false, 10)]
        public static void LoadTestbed()
        {
            Debug.Log($"{LOG} === Loading Testbed ===");

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/TESTBED_V2.unity");

            LinkFloodedGrounds();
            AssetDatabase.Refresh();

            CleanCamera();
            var rig = PlaceVRRig();
            AddLocomotion(rig);
            LoadFloodedGroundsTerrain();
            EnsureLight();
            AddAutoPlay();

            // Zaznacz rig w hierarchii
            Selection.activeGameObject = rig;

            // Oznacz scene jako zmieniona (zeby Unity pytal o zapis)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} === Locomotion Testbed Ready ===");
            Debug.Log($"{LOG} Wcisnij Play zeby przetestowac. WASD = ruch, Shift = sprint, Space = skok.");
        }

        // =====================================================================
        // 1. Usun domyslna kamere
        // =====================================================================

        static void CleanCamera()
        {
            var mainCam = GameObject.Find("Main Camera");
            if (mainCam != null)
            {
                Undo.DestroyObjectImmediate(mainCam);
                Debug.Log($"{LOG} Usunieto Main Camera.");
            }
        }

        // =====================================================================
        // 2. Wstaw VR rig
        // =====================================================================

        static GameObject PlaceVRRig()
        {
            // Sprawdz czy rig juz jest na scenie
            var existing = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in existing)
            {
                if (t.name == "OVRCameraRig" || t.name == "OVRPlayerController")
                {
                    Debug.Log($"{LOG} {t.name} juz na scenie -- uzywam istniejacego.");
                    return t.gameObject;
                }
            }

            // Szukaj OVRCameraRig prefab z Meta SDK
            GameObject rig = TryInstantiateOVRCameraRig();

            if (rig == null)
            {
                // Fallback: tworzymy prosta kamere VR bez Meta SDK
                Debug.LogWarning($"{LOG} OVRCameraRig nie znaleziony -- tworzę fallback camera rig.");
                rig = new GameObject("VRRig");
                var cam = new GameObject("Camera");
                cam.AddComponent<Camera>();
                cam.AddComponent<AudioListener>();
                cam.tag = "MainCamera";
                cam.transform.SetParent(rig.transform);
                cam.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            }

            rig.transform.position = new Vector3(0f, 0f, 0f);
            rig.transform.rotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(rig, "Add VR Rig");

            Debug.Log($"{LOG} VR Rig: {rig.name}");
            return rig;
        }

        static GameObject TryInstantiateOVRCameraRig()
        {
            string[] guids = AssetDatabase.FindAssets("OVRCameraRig t:prefab");
            if (guids.Length == 0) return null;

            // Preferuj canonical OVRCameraRig.prefab (nie warianty z Interaction SDK)
            string prefabPath = null;
            foreach (var guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith("/OVRCameraRig.prefab"))
                {
                    prefabPath = p;
                    break;
                }
            }
            if (prefabPath == null)
                prefabPath = AssetDatabase.GUIDToAssetPath(guids[0]);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return null;

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Debug.Log($"{LOG} OVRCameraRig z: {prefabPath}");
            return rig;
        }

        // =====================================================================
        // 3. Dodaj lokomocje
        // =====================================================================

        static void AddLocomotion(GameObject rig)
        {
            // CharacterController -- fizyka kolizji i grawitacja
            if (rig.GetComponent<CharacterController>() == null)
            {
                var cc = Undo.AddComponent<CharacterController>(rig);
                cc.height = 1.8f;
                cc.radius = 0.3f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.skinWidth = 0.02f;
                cc.stepOffset = 0.3f;
                Debug.Log($"{LOG} Dodano CharacterController (h=1.8, r=0.3)");
            }

            // LocomotionController -- ruch thumbstickiem
            if (rig.GetComponent<Locomotion.LocomotionController>() == null)
            {
                var loco = Undo.AddComponent<Locomotion.LocomotionController>(rig);
                loco.moveSpeed = 2.5f;
                loco.strafeFactor = 0.8f;
                Debug.Log($"{LOG} Dodano LocomotionController (speed=2.5, strafe=0.8)");
            }

            // SprintModifier -- L3 sprint, B skok
            if (rig.GetComponent<Locomotion.SprintModifier>() == null)
            {
                var sprint = Undo.AddComponent<Locomotion.SprintModifier>(rig);
                sprint.sprintMultiplier = 3f;
                sprint.jumpForce = 5f;
                sprint.jumpCooldown = 0.5f;
                Debug.Log($"{LOG} Dodano SprintModifier (sprint=3x, jump=5)");
            }


            // LocomotionManager -- orkiestracja
            if (rig.GetComponent<Locomotion.LocomotionManager>() == null)
            {
                var mgr = Undo.AddComponent<Locomotion.LocomotionManager>(rig);
                mgr.moveSpeed = 2.5f;
                Debug.Log($"{LOG} Dodano LocomotionManager");
            }
        }

        // =====================================================================
        // 4. FloodedGrounds -- link + teren
        // =====================================================================

        // Sciezka do FloodedGrounds w starym testbedzie
        private const string FG_SOURCE = "C:/Users/boris/NordLocker_8592730/PLAGA44/testbed/plaga44-unity/Assets/FloodedGrounds";
        private const string FG_TARGET = "Assets/FloodedGrounds";

        /// <summary>
        /// Tworzy junction (symlink katalogu na Windows) z FloodedGrounds
        /// w starym testbedzie do naszego projektu.
        /// Dzieki temu assety nie sa duplikowane na dysku.
        /// </summary>
        static void LinkFloodedGrounds()
        {
            string targetFull = System.IO.Path.Combine(Application.dataPath, "..", FG_TARGET);

            // Juz istnieje (junction lub katalog)
            if (System.IO.Directory.Exists(targetFull))
            {
                Debug.Log($"{LOG} FloodedGrounds juz podlinkowane.");
                return;
            }

            if (!System.IO.Directory.Exists(FG_SOURCE))
            {
                Debug.LogError($"{LOG} FloodedGrounds zrodlo nie znalezione: {FG_SOURCE}");
                Debug.LogError($"{LOG} Fallback: tworzenie prostej podlogi.");
                CreateFallbackFloor();
                return;
            }

            // Tworzymy junction (Windows directory symlink)
            // mklink /J nie wymaga uprawnien admina
            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = $"/C mklink /J \"{targetFull}\" \"{FG_SOURCE}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            process.WaitForExit();

            if (process.ExitCode == 0)
                Debug.Log($"{LOG} FloodedGrounds podlinkowane: {FG_SOURCE} -> {FG_TARGET}");
            else
                Debug.LogError($"{LOG} Nie udalo sie stworzyc junction. Kod: {process.ExitCode}");
        }

        /// <summary>
        /// Laduje teren FloodedGrounds Scene_A na aktywna scene.
        /// Szuka prefabow terenu lub otwiera scene addytywnie.
        /// </summary>
        static void LoadFloodedGroundsTerrain()
        {
            // Sprawdz czy teren juz jest
            var existingTerrain = Object.FindFirstObjectByType<Terrain>();
            if (existingTerrain != null)
            {
                Debug.Log($"{LOG} Teren juz na scenie: {existingTerrain.name}");
                return;
            }

            // Laduj Scene_A addytywnie -- zawiera teren, wode, drzewa, skybox
            string scenePath = FG_TARGET + "/Scenes/Scene_A.unity";
            if (!System.IO.File.Exists(
                System.IO.Path.Combine(Application.dataPath, "..", scenePath)))
            {
                Debug.LogWarning($"{LOG} Scene_A nie znaleziona: {scenePath}. Tworzenie fallback podlogi.");
                CreateFallbackFloor();
                return;
            }

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);

            Debug.Log($"{LOG} Zaladowano FloodedGrounds Scene_A (teren + woda + drzewa)");
        }

        /// <summary>Prosta podloga fallback gdy FloodedGrounds niedostepne.</summary>
        static void CreateFallbackFloor()
        {
            if (GameObject.Find("FallbackFloor") != null) return;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "FallbackFloor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(5f, 1f, 5f);

            var renderer = floor.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.3f, 0.3f, 0.3f);
                renderer.material = mat;
            }

            Undo.RegisterCreatedObjectUndo(floor, "Create FallbackFloor");
            Debug.Log($"{LOG} Stworzono FallbackFloor (FloodedGrounds niedostepne)");
        }

        // =====================================================================
        // 5. Swiatlo
        // =====================================================================

        static void EnsureLight()
        {
            // Sprawdz czy jest jakies swiatlo na scenie
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            if (lights.Length > 0)
            {
                Debug.Log($"{LOG} Swiatlo juz istnieje ({lights[0].name}).");
                return;
            }

            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.84f); // ciepla barwa
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Undo.RegisterCreatedObjectUndo(lightGO, "Create Light");
            Debug.Log($"{LOG} Stworzono Directional Light");
        }

        // =====================================================================
        // 6. Auto-Play (GameState.Play() na starcie)
        // =====================================================================

        static void AddAutoPlay()
        {
            // Sprawdz czy AutoPlay juz jest na scenie
            if (Object.FindAnyObjectByType<AutoPlayOnStart>() != null)
            {
                Debug.Log($"{LOG} AutoPlayOnStart juz istnieje.");
                return;
            }

            var go = new GameObject("_AutoPlay");
            Undo.AddComponent<AutoPlayOnStart>(go);
            Undo.RegisterCreatedObjectUndo(go, "Create AutoPlay");
            Debug.Log($"{LOG} Dodano AutoPlayOnStart (GameState.Play() w Start())");
        }
    }
}
