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

            if (EditorApplication.isPlaying)
            {
                // Play mode -- uzyj SceneManager (EditorSceneManager nie dziala w play mode)
                Debug.Log($"{LOG} Play mode -- reload via SceneManager");
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                return;
            }

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/TESTBED_V2.unity");

            CleanCamera();
            CleanTestObjects();
            var rig = PlaceVRRig();
            AddLocomotion(rig);
            LoadTerrain();
            SpawnPlayerAboveTerrain(rig);
            EnsureLight();
            // EnsureUplight(); -- wywalony
            SetFogAndAmbient();
            AddHamburgerMenu();
            AddPlayerAvatar(rig);
            AddInventoryScreen();
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
        /// Umieszcza gracza nad srodkiem swiata (0,200,0).
        /// Przy 15x15 grid terenow srodek gridu jest na (0,0,0).
        /// Gracz spada z 200m na teren -- CharacterController i grawitacja zalatwia ladowanie.
        /// </summary>
        static void SpawnPlayerAboveTerrain(GameObject rig)
        {
            rig.transform.position = new Vector3(0f, 200f, 0f);
            Debug.Log($"{LOG} Gracz nad srodkiem swiata: (0, 200, 0)");
        }

        /// <summary>
        /// W edytorze bez headsetu OVRCameraRig ma CenterEyeAnchor na y=0.
        /// Szukamy CenterEyeAnchor i podnosimy na 1.664m (wysokosc oczu).
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
                    eye.localPosition = new Vector3(0f, 1.664f, 0f);
                    Debug.Log($"{LOG} CenterEyeAnchor podniesiony na 1.664m");
                    return;
                }
            }

            // Fallback camera
            var cam = rig.GetComponentInChildren<Camera>();
            if (cam != null && cam.transform.localPosition.y < 0.1f)
            {
                cam.transform.localPosition = new Vector3(0f, 1.664f, 0f);
                Debug.Log($"{LOG} Fallback camera podniesiona na 1.664m");
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
            // Na Quescie CenterEyeAnchor jest ~1.664m (oczy).
            // W edytorze bez headsetu fallback camera jest na 1.664m.
            if (rig.GetComponent<CharacterController>() == null)
            {
                var cc = Undo.AddComponent<CharacterController>(rig);
                cc.height = 1.8f;
                cc.radius = 0.3f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.skinWidth = 0.08f;  // wiekszy skin = mniej drgania na terenie
                cc.stepOffset = 0.5f;  // wiekszy step = plynniejsze wchodzenie na nierówności
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
                camHeight.eyeHeight = 1.664f;
                Debug.Log($"{LOG} Dodano EditorCameraHeight (1.664m)");
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
        // 4. Potok -- teren + woda + skybox
        // =====================================================================

        private const string TERRAIN_ASSET = "Assets/Potok/Terrain/Tile_0.asset";
        private const string SKYBOX_MAT = "Assets/Potok/Skybox/BGR_Sky1.mat";
        private const string MOSS_LAYER = "Assets/Potok/TerrainLayers/layer_GR_Moss1_ASGR_Moss1_N3d1cca0d6d0e9938.terrainlayer";
        private const float TERRAIN_SCALE = 20f;

        /// <summary>
        /// Jeden duzy teren (Tile_0 skalowany 20x), 1 warstwa (Moss), wycentrowany.
        /// </summary>
        static void LoadTerrain()
        {
            var existingTerrain = Object.FindFirstObjectByType<Terrain>();
            if (existingTerrain != null)
            {
                if (existingTerrain.terrainData != null && existingTerrain.terrainData.treePrototypes.Length > 0)
                    CleanMissingTrees(existingTerrain.terrainData);
                Debug.Log($"{LOG} Teren juz na scenie: {existingTerrain.name}");
                return;
            }

            var terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(TERRAIN_ASSET);
            if (terrainData == null)
            {
                Debug.LogWarning($"{LOG} Tile_0.asset nie znaleziony.");
                CreateFallbackFloor();
                return;
            }

            CleanMissingTrees(terrainData);

            // Skaluj 20x
            Vector3 orig = terrainData.size;
            terrainData.size = new Vector3(orig.x * TERRAIN_SCALE, orig.y * TERRAIN_SCALE, orig.z * TERRAIN_SCALE);

            // 1 warstwa -- Moss, duzy UV repeat
            var mossLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(MOSS_LAYER);
            if (mossLayer != null)
            {
                mossLayer.tileSize = new Vector2(5f, 5f);
                EditorUtility.SetDirty(mossLayer);
                terrainData.terrainLayers = new TerrainLayer[] { mossLayer };
            }

            EditorUtility.SetDirty(terrainData);

            var terrainGO = Terrain.CreateTerrainGameObject(terrainData);
            terrainGO.name = "Terrain";
            float halfX = terrainData.size.x * 0.5f;
            float halfZ = terrainData.size.z * 0.5f;
            terrainGO.transform.position = new Vector3(-halfX, 0f, -halfZ);
            Undo.RegisterCreatedObjectUndo(terrainGO, "Create Terrain");

            Debug.Log($"{LOG} Teren: {terrainData.size.x:F0}x{terrainData.size.z:F0}m (skala {TERRAIN_SCALE}x)");

            // Skybox
            var skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(SKYBOX_MAT);
            if (skyboxMat != null)
            {
                skyboxMat.SetFloat("_CloudOpacity", 0f);
                EditorUtility.SetDirty(skyboxMat);
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
            Debug.Log($"{LOG} Stworzono FallbackFloor (Potok niedostepne)");
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
            light.color = new Color(0.4f, 0.45f, 0.6f); // zimny ksiezycowy
            light.intensity = 0.05f; // noc
            light.shadows = LightShadows.Soft;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Undo.RegisterCreatedObjectUndo(lightGO, "Create Light");
            Debug.Log($"{LOG} Stworzono Directional Light");
        }

        // =====================================================================
        // 6. Uplight -- zielone swiatlo od dolu (ZAWSZE)
        // =====================================================================

        static void EnsureUplight()
        {
            if (GameObject.Find("Ground Uplight") != null)
            {
                Debug.Log($"{LOG} Ground Uplight juz istnieje.");
                return;
            }

            var uplightGO = new GameObject("Ground Uplight");
            var uplight = uplightGO.AddComponent<Light>();
            uplight.type = LightType.Directional;
            uplight.color = new Color(0f, 0f, 0f);
            uplight.intensity = 0.8f;
            uplight.shadows = LightShadows.None;
            uplightGO.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Undo.RegisterCreatedObjectUndo(uplightGO, "Create Uplight");
            Debug.Log($"{LOG} Stworzono Ground Uplight");
        }

        // =====================================================================
        // 7. Fog + ambient (ZAWSZE)
        // =====================================================================

        static void SetFogAndAmbient()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.02f);
            RenderSettings.fogStartDistance = 50f;
            RenderSettings.fogEndDistance = 300f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientGroundColor = new Color(0.02f, 0.02f, 0.02f);
            RenderSettings.ambientEquatorColor = new Color(0.08f, 0.08f, 0.08f);
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.55f, 0.6f);

            Debug.Log($"{LOG} Fog + ambient ustawione");
        }

        // =====================================================================
        // 8. Hamburger menu
        // =====================================================================

        static void AddPlayerAvatar(GameObject rig)
        {
            // Komponent
            if (rig.GetComponent<Plaga44.PlayerAvatar>() == null)
            {
                var avatar = Undo.AddComponent<Plaga44.PlayerAvatar>(rig);
                avatar.modelScale = 0.655f;  // 2.75m mesh -> 1.8m gracz
                avatar.yOffset = -1.664f;  // wysokosc oczu z PLAYER.obj Eyes group
                avatar.hideHeadInFirstPerson = true;
            }

            // Retargeter -- IK body retargeting (head, arms, legs)
            if (rig.GetComponent<Plaga44.AvatarRetargeter>() == null)
            {
                var retargeter = Undo.AddComponent<Plaga44.AvatarRetargeter>(rig);
                retargeter.headToHipsRatio = 0.60f;
                retargeter.spineFollowHead = 0.4f;
                retargeter.stepFrequency = 2.0f;
                retargeter.stepLength = 0.35f;
                retargeter.stepHeight = 0.08f;
                Debug.Log($"{LOG} Dodano AvatarRetargeter (IK body tracking)");
            }

            // Model na scenie (widoczny w scene graph)
            if (GameObject.Find("PlayerAvatarModel") != null)
            {
                Debug.Log($"{LOG} PlayerAvatarModel juz na scenie.");
                return;
            }

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Player/PLAYER_rigged.fbx");
            if (fbx == null)
                fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PLAYER_rigged.fbx");

            if (fbx == null)
            {
                Debug.LogError($"{LOG} BRAK PLAYER_rigged.fbx!");
                return;
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            model.name = "PlayerAvatarModel";
            model.transform.SetParent(rig.transform);
            model.transform.localPosition = new Vector3(0f, -1.664f, 0f);
            model.transform.localRotation = UnityEngine.Quaternion.identity;
            model.transform.localScale = Vector3.one * 0.655f;  // 1.8m
            Undo.RegisterCreatedObjectUndo(model, "Create PlayerAvatarModel");
            Debug.Log($"{LOG} PlayerAvatarModel postawiony na scenie (dziecko {rig.name})");
        }

        static void AddHamburgerMenu()
        {
            if (Object.FindAnyObjectByType<Plaga44.UI.HamburgerMenu>() != null)
            {
                Debug.Log($"{LOG} HamburgerMenu juz istnieje.");
                return;
            }

            var menuGO = new GameObject("_HamburgerMenu");
            Undo.AddComponent<Plaga44.UI.HamburgerMenu>(menuGO);
            Undo.RegisterCreatedObjectUndo(menuGO, "Create HamburgerMenu");
            Debug.Log($"{LOG} Dodano HamburgerMenu (Escape / Menu button)");
        }

        // =====================================================================
        // 9. Inventory Screen + Menu Setup
        // =====================================================================

        static void AddInventoryScreen()
        {
            if (Object.FindAnyObjectByType<Plaga44.UI.InventoryScreen>() != null)
            {
                Debug.Log($"{LOG} InventoryScreen juz istnieje.");
                return;
            }

            // InventoryScreen -- world-space canvas z podgladem modelu i slotami
            var invGO = new GameObject("_InventoryScreen");
            Undo.AddComponent<Plaga44.UI.InventoryScreen>(invGO);
            Undo.RegisterCreatedObjectUndo(invGO, "Create InventoryScreen");

            // InventoryMenuSetup -- dodaje przycisk INVENTORY do HamburgerMenu
            Undo.AddComponent<Plaga44.UI.InventoryMenuSetup>(invGO);

            Debug.Log($"{LOG} Dodano InventoryScreen + InventoryMenuSetup (I / Menu button)");
        }

        // =====================================================================
        // 10. Auto-Play (GameState.Play() na starcie)
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
