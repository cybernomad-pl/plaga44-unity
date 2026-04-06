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

            CleanCamera();
            CleanTestObjects();
            var rig = PlaceVRRig();
            AddLocomotion(rig);
            LoadDemoLevelTerrain();
            SpawnPlayerAboveTerrain(rig);
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

        /// <summary>Usuwa smieci z poprzednich setupow (TestFloor, TestWall, FallbackFloor).</summary>
        static void CleanTestObjects()
        {
            string[] trash = { "TestFloor", "TestWall_N", "TestWall_E", "FallbackFloor" };
            foreach (var name in trash)
            {
                var obj = GameObject.Find(name);
                if (obj != null)
                {
                    Undo.DestroyObjectImmediate(obj);
                    Debug.Log($"{LOG} Usunieto {name}");
                }
            }
        }

        /// <summary>
        /// Umieszcza gracza wysoko nad srodkiem terenu -- spadnie na teren.
        /// Jesli brak terenu, stawia na y=50.
        /// </summary>
        static void SpawnPlayerAboveTerrain(GameObject rig)
        {
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain != null)
            {
                var data = terrain.terrainData;
                var terrainPos = terrain.transform.position;
                // Srodek terenu
                float cx = terrainPos.x + data.size.x * 0.5f;
                float cz = terrainPos.z + data.size.z * 0.5f;
                // Najwyzszy punkt terenu + 50m
                float maxHeight = terrainPos.y + data.size.y + 50f;
                rig.transform.position = new Vector3(cx, maxHeight, cz);
                Debug.Log($"{LOG} Gracz nad terenem: ({cx:F0}, {maxHeight:F0}, {cz:F0})");
            }
            else
            {
                rig.transform.position = new Vector3(0f, 50f, 0f);
                Debug.Log($"{LOG} Gracz na y=50 (brak terenu)");
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
        // 4. DemoLevel -- teren + woda + skybox
        // =====================================================================

        private const string DEMO_LEVEL = "Assets/DemoLevel";
        private const string TERRAIN_ASSET = "Assets/DemoLevel/Terrain/Scene_A_Terrain.asset";
        private const string SKYBOX_MAT = "Assets/DemoLevel/Skybox/BGR_Sky1.mat";
        private const string WATER_MESH = "Assets/DemoLevel/Water/WaterPlane.fbx";

        /// <summary>
        /// Stawia teren, wode i skybox z DemoLevel asset packa.
        /// Teren z heightmap, woda jako mesh pod terenem, skybox z materialu.
        /// </summary>
        static void LoadDemoLevelTerrain()
        {
            // Sprawdz czy teren juz jest
            var existingTerrain = Object.FindFirstObjectByType<Terrain>();
            if (existingTerrain != null)
            {
                Debug.Log($"{LOG} Teren juz na scenie: {existingTerrain.name}");
                return;
            }

            // --- TEREN ---
            var terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(TERRAIN_ASSET);
            if (terrainData != null)
            {
                var terrainGO = Terrain.CreateTerrainGameObject(terrainData);
                terrainGO.name = "DemoTerrain";
                // Pozycja terenu -- centrujemy (terrain size / 2)
                var size = terrainData.size;
                terrainGO.transform.position = new Vector3(-size.x * 0.5f, 0f, -size.z * 0.5f);
                Undo.RegisterCreatedObjectUndo(terrainGO, "Create DemoTerrain");
                Debug.Log($"{LOG} Teren zaladowany: {size.x}x{size.z}m");
            }
            else
            {
                Debug.LogWarning($"{LOG} Terrain asset nie znaleziony: {TERRAIN_ASSET}. Tworzenie fallback.");
                CreateFallbackFloor();
            }

            // --- WODA ---
            var waterMesh = AssetDatabase.LoadAssetAtPath<GameObject>(WATER_MESH);
            if (waterMesh != null)
            {
                var water = (GameObject)PrefabUtility.InstantiatePrefab(waterMesh);
                water.name = "DemoWater";
                water.transform.position = new Vector3(0f, 0.5f, 0f); // lekko nad poziomem 0
                water.transform.localScale = new Vector3(100f, 1f, 100f);
                Undo.RegisterCreatedObjectUndo(water, "Create DemoWater");
                Debug.Log($"{LOG} Woda zaladowana");
            }

            // --- SKYBOX ---
            var skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(SKYBOX_MAT);
            if (skyboxMat != null)
            {
                RenderSettings.skybox = skyboxMat;
                Debug.Log($"{LOG} Skybox ustawiony");
            }
        }

        /// <summary>Prosta podloga fallback gdy DemoLevel assety niedostepne.</summary>
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
            Debug.Log($"{LOG} Stworzono FallbackFloor (DemoLevel niedostepne)");
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
