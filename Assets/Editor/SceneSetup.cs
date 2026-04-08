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
            LoadTerrain();
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
                // Najwyzszy punkt terenu + 200m
                float maxHeight = terrainPos.y + data.size.y + 200f;
                rig.transform.position = new Vector3(cx, maxHeight, cz);
                Debug.Log($"{LOG} Gracz nad terenem: ({cx:F0}, {maxHeight:F0}, {cz:F0})");
            }
            else
            {
                rig.transform.position = new Vector3(0f, 50f, 0f);
                Debug.Log($"{LOG} Gracz na y=50 (brak terenu)");
            }
        }

        /// <summary>
        /// W edytorze bez headsetu OVRCameraRig ma CenterEyeAnchor na y=0.
        /// Szukamy CenterEyeAnchor i podnosimy na 1.65m (wysokosc oczu).
        /// Na Quescie head tracking to nadpisze -- nie szkodzi.
        /// </summary>
        static void EnsureCameraHeight(GameObject rig)
        {
            // Szukaj CenterEyeAnchor (OVRCameraRig hierarchy)
            var tracking = rig.transform.Find("TrackingSpace");
            if (tracking != null)
            {
                var eye = tracking.Find("CenterEyeAnchor");
                if (eye != null)
                {
                    eye.localPosition = new Vector3(0f, 1.65f, 0f);
                    Debug.Log($"{LOG} CenterEyeAnchor podniesiony na 1.65m");
                    return;
                }
            }

            // Fallback camera
            var cam = rig.GetComponentInChildren<Camera>();
            if (cam != null && cam.transform.localPosition.y < 0.1f)
            {
                cam.transform.localPosition = new Vector3(0f, 1.65f, 0f);
                Debug.Log($"{LOG} Fallback camera podniesiona na 1.65m");
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
            // W VR kamera jest na pozycji glowy gracza (tracking).
            // CC musi obejmowac cialo OD PODLOGI do czubka glowy.
            // height=1.8m, center y=0.9m = collider od y=0 do y=1.8m.
            // Na Quescie CenterEyeAnchor jest ~1.65m (oczy).
            // W edytorze bez headsetu fallback camera jest na 1.65m.
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

            // W edytorze bez headsetu -- podnieś kamerę na wysokość oczu
            EnsureCameraHeight(rig);

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

            // EditorCameraHeight -- wymusza wysokosc kamery w edytorze bez headsetu
            if (rig.GetComponent<Locomotion.EditorCameraHeight>() == null)
            {
                var camHeight = Undo.AddComponent<Locomotion.EditorCameraHeight>(rig);
                camHeight.eyeHeight = 1.65f;
                Debug.Log($"{LOG} Dodano EditorCameraHeight (1.65m)");
            }

            // EditorMouseLook -- obrot kamery myszka w edytorze (PPM + ruch)
            if (rig.GetComponent<Locomotion.EditorMouseLook>() == null)
            {
                var mouseLook = Undo.AddComponent<Locomotion.EditorMouseLook>(rig);
                mouseLook.sensitivity = 2f;
                Debug.Log($"{LOG} Dodano EditorMouseLook (PPM + ruch myszy)");
            }
        }

        // =====================================================================
        // 4. Level -- teren + woda + skybox
        // =====================================================================

        private const string LEVEL_ROOT = "Assets/Level";
        private const string TILE_PATH = "Assets/Level/Terrain/Tile_{0}.asset";
        private const string SKYBOX_MAT = "Assets/Level/Skybox/BGR_Sky1.mat";
        private const string WATER_MESH = "Assets/Level/Water/WaterPlane.fbx";
        private const int GRID_SIZE = 5; // 5x5 = 25 tiles

        /// <summary>
        /// Stawia 5x5 grid terenow, wode i skybox z Level asset packa.
        /// 9 tile assetow (Tile_0..8) uzywa cyklicznie na 25 pozycjach.
        /// Grid jest wycentrowany -- gracz spawnuje nad srodkowym tile.
        /// </summary>
        static void LoadTerrain()
        {
            // Sprawdz czy teren juz jest
            var existingTerrain = Object.FindFirstObjectByType<Terrain>();
            if (existingTerrain != null)
            {
                // Wyczysc brakujace drzewa ze WSZYSTKICH istniejacych terenow
                var allTerrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
                foreach (var t in allTerrains)
                {
                    if (t.terrainData != null && t.terrainData.treePrototypes.Length > 0)
                        CleanMissingTrees(t.terrainData);
                }
                Debug.Log($"{LOG} Teren juz na scenie: {existingTerrain.name}");
                return;
            }

            // Zaladuj pierwszy tile zeby poznac rozmiar
            var firstTile = AssetDatabase.LoadAssetAtPath<TerrainData>(string.Format(TILE_PATH, 0));
            if (firstTile == null)
            {
                Debug.LogWarning($"{LOG} Tile_0.asset nie znaleziony. Tworzenie fallback.");
                CreateFallbackFloor();
                return;
            }

            Vector3 tileSize = firstTile.size;
            float totalX = tileSize.x * GRID_SIZE;
            float totalZ = tileSize.z * GRID_SIZE;

            // Parent dla wszystkich terenow
            var terrainRoot = new GameObject("TerrainGrid");
            Undo.RegisterCreatedObjectUndo(terrainRoot, "Create TerrainGrid");

            // 9 tile assetow (Tile_0..8), uzycie cykliczne na wiekszym gridzie
            const int TILE_ASSET_COUNT = 9;
            int tileIndex = 0;
            for (int z = 0; z < GRID_SIZE; z++)
            {
                for (int x = 0; x < GRID_SIZE; x++)
                {
                    int assetIdx = tileIndex % TILE_ASSET_COUNT;
                    var tileData = AssetDatabase.LoadAssetAtPath<TerrainData>(
                        string.Format(TILE_PATH, assetIdx));

                    if (tileData == null)
                    {
                        Debug.LogWarning($"{LOG} Tile_{assetIdx}.asset nie znaleziony -- pomijam.");
                        tileIndex++;
                        continue;
                    }

                    CleanMissingTrees(tileData);

                    var terrainGO = Terrain.CreateTerrainGameObject(tileData);
                    terrainGO.name = $"Tile_{x}_{z}";
                    terrainGO.transform.SetParent(terrainRoot.transform);

                    // Pozycja: centrujemy grid tak zeby srodkowy tile byl na (0,0,0)
                    float posX = (x - GRID_SIZE / 2) * tileSize.x;
                    float posZ = (z - GRID_SIZE / 2) * tileSize.z;
                    terrainGO.transform.position = new Vector3(posX, 0f, posZ);

                    tileIndex++;
                }
            }

            Debug.Log($"{LOG} Teren 3x3: {GRID_SIZE * GRID_SIZE} tiles, {totalX}x{totalZ}m");

            // --- WODA (skalowana do calego gridu) ---
            var waterMesh = AssetDatabase.LoadAssetAtPath<GameObject>(WATER_MESH);
            if (waterMesh != null)
            {
                var water = (GameObject)PrefabUtility.InstantiatePrefab(waterMesh);
                water.name = "Water";
                water.transform.position = new Vector3(0f, 0.5f, 0f);
                // Skalujemy wode do rozmiaru calego gridu
                float waterScale = Mathf.Max(totalX, totalZ) * 0.15f;
                water.transform.localScale = new Vector3(waterScale, 1f, waterScale);
                Undo.RegisterCreatedObjectUndo(water, "Create Water");
                Debug.Log($"{LOG} Woda zaladowana (skala {waterScale})");
            }

            // --- SKYBOX ---
            var skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(SKYBOX_MAT);
            if (skyboxMat != null)
            {
                RenderSettings.skybox = skyboxMat;
                Debug.Log($"{LOG} Skybox ustawiony");
            }
        }

        /// <summary>Prosta podloga fallback gdy Level assety niedostepne.</summary>
        /// <summary>Usuwa brakujace drzewa i detale z terrain data.</summary>
        static void CleanMissingTrees(TerrainData data)
        {
            // Usun instancje drzew
            data.treeInstances = new TreeInstance[0];
            // Usun prototypy drzew (referencje do brakujacych prefabow)
            data.treePrototypes = new TreePrototype[0];
            // Usun detail prototypes (trawa itp.)
            data.detailPrototypes = new DetailPrototype[0];
            EditorUtility.SetDirty(data);
            Debug.Log($"{LOG} Wyczyszczono brakujace drzewa i detale z terenu.");
        }

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
            Debug.Log($"{LOG} Stworzono FallbackFloor (Level niedostepne)");
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
