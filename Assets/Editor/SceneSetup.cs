// SceneSetup.cs -- CYBERNOMAD Editor Tool
//
// Jednym klikiem stawia testowa scene VR z lokomocja.
// Menu: CYBERNOMAD > Scene Setup > Locomotion Testbed
//
// Co robi:
//   1. Usuwa Main Camera (jesli jest)
//   2. Wstawia OVRCameraRig z prefaba Meta SDK (lub fallback Camera)
//   3. Dodaje CharacterController na rig root
//   4. Dodaje LocomotionController + SprintModifier + ComfortVignette
//   5. Dodaje LocomotionManager
//   6. Tworzy podloge (Plane 50x50m) z szachownica
//   7. Dodaje swiatlo kierunkowe
//   8. Ustawia GameState na Playing
//
// Public API:
//   SceneSetup.BuildLocomotionTestbed();

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
        public static void BuildLocomotionTestbed()
        {
            Debug.Log($"{LOG} === Building Locomotion Testbed ===");

            CleanCamera();
            var rig = PlaceVRRig();
            AddLocomotion(rig);
            CreateFloor();
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

            // ComfortVignette -- winieta komfortu
            if (rig.GetComponent<Locomotion.ComfortVignette>() == null)
            {
                Undo.AddComponent<Locomotion.ComfortVignette>(rig);
                Debug.Log($"{LOG} Dodano ComfortVignette");
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
        // 4. Podloga
        // =====================================================================

        static void CreateFloor()
        {
            // Sprawdz czy podloga juz jest
            if (GameObject.Find("TestFloor") != null)
            {
                Debug.Log($"{LOG} TestFloor juz istnieje.");
                return;
            }

            // Plane 50x50m z szachownica
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "TestFloor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(5f, 1f, 5f); // Plane = 10m default, 5x = 50m

            // Proba nadania szachownicy (Built-in checkered shader)
            var renderer = floor.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.name = "TestFloorMat";
                mat.color = new Color(0.3f, 0.3f, 0.3f);

                // Tiling daje efekt siatki -- latwiej zauwazyc ruch
                mat.mainTextureScale = new Vector2(50f, 50f);
                renderer.material = mat;
            }

            Undo.RegisterCreatedObjectUndo(floor, "Create TestFloor");
            Debug.Log($"{LOG} Stworzono TestFloor 50x50m");

            // Dodaj kilka scian do testowania kolizji
            CreateWall("TestWall_N", new Vector3(0f, 1.5f, 20f), new Vector3(10f, 3f, 0.3f));
            CreateWall("TestWall_E", new Vector3(20f, 1.5f, 0f), new Vector3(0.3f, 3f, 10f));
        }

        static void CreateWall(string name, Vector3 position, Vector3 scale)
        {
            if (GameObject.Find(name) != null) return;

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.position = position;
            wall.transform.localScale = scale;

            var renderer = wall.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.name = name + "Mat";
                mat.color = new Color(0.6f, 0.2f, 0.2f);
                renderer.material = mat;
            }

            Undo.RegisterCreatedObjectUndo(wall, $"Create {name}");
            Debug.Log($"{LOG} Stworzono {name} do testow kolizji");
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
