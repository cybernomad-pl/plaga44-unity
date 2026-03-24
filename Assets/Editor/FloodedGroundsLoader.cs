#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plaga44.Editor
{
    // -------------------------------------------------------------------------
    // FloodedGroundsLoader
    //
    // MenuItem 1: CYBERNOMAD/Scene/Load PLAGA 44 Demo
    //   - Otwiera Scene_A.unity z paczki HorrorSurvival2022
    //   - Usuwa FPS Controller (CharController_Motor, CharacterController na root)
    //   - Usuwa FPSDisplay
    //   - Usuwa Main Camera (jezeli nie jest juz pod OVR rigiem)
    //   - Dodaje OVRCameraRig jezeli brak (reuzywamy MetaQuestSetup.SetupVRSceneHands)
    //   - Konfiguruje rendering pod Quest 3 (shadows, quality, FFR hint w layerze)
    //
    // MenuItem 2: CYBERNOMAD/Scene/Prefab Picker
    //   - Tworzy nowa pusta scene
    //   - Dodaje OVRCameraRig
    //   - Otwiera okno EditorWindow z lista prefabow podzielona na kategorie
    //     (Buildings, Nature, Props, Atmospherics, Backgrounds)
    //   - Klik na prefab = instancja w scenie przed graczem
    // -------------------------------------------------------------------------

    public static class FloodedGroundsLoader
    {
        private const string LOG = "[PLAGA44]";

        private const string SCENE_PATH =
            "Assets/FloodedGrounds/Scenes/Scene_A.unity";

        private const string PREFABS_ROOT =
            "Assets/FloodedGrounds/Prefabs";

        // ------------------------------------------------------------------
        // MenuItem 1 -- Load Flooded Grounds
        // ------------------------------------------------------------------

        private const string LEVEL_SCENE_PATH = "Assets/Scenes/PLAGA44_Level.unity";
        private const string SPLASH_SCENE_PATH = "Assets/Scenes/PLAGA44_Splash.unity";

        [MenuItem("CYBERNOMAD/Scene/Load PLAGA 44 Demo", false, 10)]
        public static void LoadFloodedGrounds()
        {
            Debug.Log($"{LOG} === Building PLAGA '44 Demo ===");

            if (!File.Exists(Path.Combine(Application.dataPath, "..",  SCENE_PATH)))
            {
                Debug.LogError($"{LOG} Scene not found: {SCENE_PATH}");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log($"{LOG} Cancelled by user.");
                return;
            }

            // Ensure Scenes folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            // ---- STEP 1: Build Level Scene ----
            Debug.Log($"{LOG} [1/3] Building level scene...");

            Scene levelScene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            if (!levelScene.IsValid())
            {
                Debug.LogError($"{LOG} Failed to open scene: {SCENE_PATH}");
                return;
            }

            RemoveFPSController();
            RemoveFPSDisplay();
            RemoveOrphanCameras();
            RemoveLegacyEventSystems();
            RemoveUnwantedObjects();
            SetQuestRenderingSettings();

            var player = TestEnvironmentSetup.AddPlayerControllerPublic();
            if (player != null)
            {
                player.transform.position = new Vector3(420f, 36.5f, 241f);
                Debug.Log($"{LOG} OVRPlayerController spawned at (420, 36.5, 241).");
            }
            else
            {
                EnsureOVRCameraRig();
            }

            MaterialUpgrader.UpgradeMaterials();

            EditorSceneManager.SaveScene(levelScene, LEVEL_SCENE_PATH);
            Debug.Log($"{LOG} Level scene saved: {LEVEL_SCENE_PATH}");

            // ---- STEP 2: Build Splash Scene ----
            Debug.Log($"{LOG} [2/3] Building splash scene...");

            Scene splashScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Directional light
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0;
            lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);

            // OVRPlayerController (look around only -- locomotion disabled by SplashScreen)
            var splashPlayer = TestEnvironmentSetup.AddPlayerControllerPublic();
            if (splashPlayer != null)
            {
                splashPlayer.transform.position = Vector3.zero;
            }

            // SplashScreen component
            var splashGO = new GameObject("SplashScreen");
            var splash = splashGO.AddComponent<SplashScreen>();
            splash.gameSceneName = "PLAGA44_Level";
            splash.fadeDuration = 1.5f;
            splash.displayName = "PLAGA <color=#CC3333>'44</color>";
            Undo.RegisterCreatedObjectUndo(splashGO, "Add SplashScreen");

            EditorSceneManager.SaveScene(splashScene, SPLASH_SCENE_PATH);
            Debug.Log($"{LOG} Splash scene saved: {SPLASH_SCENE_PATH}");

            // ---- STEP 3: Configure Build Settings ----
            Debug.Log($"{LOG} [3/3] Configuring build settings...");

            var scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(SPLASH_SCENE_PATH, true),
                new EditorBuildSettingsScene(LEVEL_SCENE_PATH, true),
            };
            EditorBuildSettings.scenes = scenes;
            Debug.Log($"{LOG} Build Settings: scene 0 = Splash, scene 1 = Level");

            // Open splash scene for testing
            EditorSceneManager.OpenScene(SPLASH_SCENE_PATH, OpenSceneMode.Single);

            Debug.Log($"{LOG} === PLAGA '44 Demo READY. Press Play to test. ===");
        }

        // ------------------------------------------------------------------
        // MenuItem -- Add Splash Screen to current scene
        // ------------------------------------------------------------------

        [MenuItem("CYBERNOMAD/Scene/Add Splash Screen", false, 12)]
        public static void AddSplashScreen()
        {
            // Sprawdź czy już jest
            if (Object.FindFirstObjectByType<SplashScreen>() != null)
            {
                Debug.Log($"{LOG} SplashScreen already in scene.");
                return;
            }
            TestEnvironmentSetup.AddSplashScreenPublic();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log($"{LOG} SplashScreen added. Press both triggers to dismiss.");
        }

        // ------------------------------------------------------------------
        // MenuItem 2 -- Prefab Picker (nowa scena)
        // ------------------------------------------------------------------

        [MenuItem("CYBERNOMAD/Scene/Prefab Picker", false, 11)]
        public static void OpenPrefabPicker()
        {
            Debug.Log($"{LOG} === New Scene + Prefab Picker ===");

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log($"{LOG} Cancelled by user.");
                return;
            }

            // Nowa pusta scena
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Debug.Log($"{LOG} Created new empty scene.");

            // Usun domyslna Main Camera -- zastapimy OVR rigiem
            var defaultCam = GameObject.Find("Main Camera");
            if (defaultCam != null) Object.DestroyImmediate(defaultCam);

            SetQuestRenderingSettings();
            EnsureOVRCameraRig();

            EditorSceneManager.MarkSceneDirty(newScene);

            // Otworz okno pickera
            FloodedGroundsPrefabPicker.Open();
        }

        // ------------------------------------------------------------------
        // FPS Controller removal
        // ------------------------------------------------------------------

        static void RemoveFPSController()
        {
            // Szukamy po nazwie (typowe nazwy w asset packach) i po komponencie
            var allObjects = Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            var toDestroy = new List<GameObject>();

            foreach (var go in allObjects)
            {
                // CharController_Motor = FPS controller z tego packu
                if (go.GetComponent("CharController_Motor") != null)
                {
                    toDestroy.Add(go);
                    continue;
                }

                // Backup: nazwa root obiektu wskazuje na FPS/Player controller
                string nameLower = go.name.ToLowerInvariant();
                bool looksLikeFPS =
                    nameLower.Contains("fpscont") ||
                    nameLower.Contains("fps_cont") ||
                    nameLower.Contains("fps controller") ||
                    nameLower.Contains("fpscontroller") ||
                    nameLower.Contains("firstperson") ||
                    nameLower.Contains("first_person") ||
                    nameLower.Contains("wasd") ||
                    (nameLower.Contains("player") && go.transform.parent == null &&
                     go.GetComponent<CharacterController>() != null) ||
                    // CharacterController na root bez OVR = FPS controller
                    (go.transform.parent == null &&
                     go.GetComponent<CharacterController>() != null &&
                     go.GetComponent<OVRPlayerController>() == null);

                if (looksLikeFPS) toDestroy.Add(go);
            }

            if (toDestroy.Count == 0)
            {
                Debug.Log($"{LOG} No FPS Controller found in scene (already removed or not present).");
                return;
            }

            foreach (var go in toDestroy)
            {
                if (go == null) continue;
                Debug.Log($"{LOG} Removing FPS Controller: '{go.name}'");
                Undo.DestroyObjectImmediate(go);
            }
        }

        // Objects to remove from Flooded Grounds (not fitting PLAGA '44)
        private static readonly string[] UNWANTED_PATTERNS = new string[]
        {
            "ship", "saucer", "flyingsaucer", "flying_saucer",
            "blockercube", "_blocker",
            "decobush", "hedge",
        };

        static void RemoveUnwantedObjects()
        {
            var allObjects = Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var toDestroy = new List<GameObject>();

            foreach (var go in allObjects)
            {
                string nameLower = go.name.ToLowerInvariant();
                foreach (var pattern in UNWANTED_PATTERNS)
                {
                    if (nameLower.Contains(pattern))
                    {
                        // Don't remove children separately if parent is already marked
                        bool parentAlreadyMarked = false;
                        var parent = go.transform.parent;
                        while (parent != null)
                        {
                            string pName = parent.name.ToLowerInvariant();
                            foreach (var p2 in UNWANTED_PATTERNS)
                                if (pName.Contains(p2)) { parentAlreadyMarked = true; break; }
                            if (parentAlreadyMarked) break;
                            parent = parent.parent;
                        }
                        if (!parentAlreadyMarked)
                            toDestroy.Add(go);
                        break;
                    }
                }
            }

            foreach (var go in toDestroy)
            {
                if (go == null) continue;
                Debug.Log($"{LOG} Removing unwanted object: '{go.name}'");
                Undo.DestroyObjectImmediate(go);
            }

            if (toDestroy.Count > 0)
                Debug.Log($"{LOG} Removed {toDestroy.Count} unwanted objects.");
        }

        static void RemoveLegacyEventSystems()
        {
            // Usuwa wszystkie EventSystem z legacy StandaloneInputModule/TouchInputModule
            // (spamują InvalidOperationException bo projekt używa Input System package)
            var allES = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var es in allES)
            {
                if (es == null) continue;
                var standalone = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                var touch = es.GetComponent<UnityEngine.EventSystems.TouchInputModule>();
                if (standalone != null)
                {
                    Debug.Log($"{LOG} Removing legacy StandaloneInputModule from '{es.gameObject.name}'");
                    Undo.DestroyObjectImmediate(standalone);
                }
                if (touch != null)
                {
                    Debug.Log($"{LOG} Removing legacy TouchInputModule from '{es.gameObject.name}'");
                    Undo.DestroyObjectImmediate(touch);
                }
                // Jeśli EventSystem jest teraz pusty (bez input module) -- usuń cały GO
                // (nasz AddVRUI doda nowy z InputSystemUIInputModule)
                if (es == null) continue;
                if (es.GetComponents<UnityEngine.EventSystems.BaseInputModule>().Length == 0)
                {
                    Debug.Log($"{LOG} Removing empty EventSystem: '{es.gameObject.name}'");
                    Undo.DestroyObjectImmediate(es.gameObject);
                }
            }
        }

        static void RemoveOrphanCameras()
        {
            // Usuwa Main Camera i inne samodzielne kamery (nie będące pod OVR rigiem)
            var allCams = Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cam in allCams)
            {
                // Nie ruszaj kamer pod OVR rigiem
                bool underOVR = false;
                var parent = cam.transform.parent;
                while (parent != null)
                {
                    if (parent.name.Contains("OVR")) { underOVR = true; break; }
                    parent = parent.parent;
                }
                if (!underOVR)
                {
                    Debug.Log($"{LOG} Removing orphan camera: '{cam.gameObject.name}'");
                    Undo.DestroyObjectImmediate(cam.gameObject);
                }
            }
        }

        static void RemoveFPSDisplay()
        {
            var allObjects = Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var go in allObjects)
            {
                if (go.GetComponent("FPSDisplay") != null)
                {
                    Debug.Log($"{LOG} Removing FPSDisplay: '{go.name}'");
                    Undo.DestroyObjectImmediate(go);
                    return;
                }
            }
        }

        // ------------------------------------------------------------------
        // Quest 3 rendering settings
        // ------------------------------------------------------------------

        static void SetQuestRenderingSettings()
        {
            // Shadow distance -- Quest 3 ma ograniczony GPU, krotiszy shadow distance
            // redukuje koszt. 30m to dobry kompromis dla outdoor horroru.
            QualitySettings.shadowDistance = 30f;

            // MSAA x4 -- Quest 3 ma tile-based GPU gdzie MSAA jest tanie
            QualitySettings.antiAliasing = 4;

            // Bez vsync -- XR compositor sam kontroluje timing
            QualitySettings.vSyncCount = 0;

            // LOD bias -- troche nizej niz default zeby LOD wchodzil szybciej
            QualitySettings.lodBias = 0.7f;

            // Max pixel lights -- dynamic lights sa drogie na mobile VR
            QualitySettings.pixelLightCount = 1;

            // Realtime GI off -- zbyt drogie na Quest 3
            if (Lightmapping.realtimeGI)
            {
                Lightmapping.realtimeGI = false;
                Debug.Log($"{LOG} Realtime GI disabled (too expensive for Quest 3).");
            }

            // Baked GI zostawiamy -- Flooded Grounds ma prekalkulowane lightmapy

            // Ambient light -- jezeli scena ma sehr ciemny ambient, dostosuj
            // (nie nadpisujemy -- scena Flooded Grounds ma wlasny swiatlo setup)

            Debug.Log($"{LOG} Quest 3 rendering settings applied:" +
                      $" shadows=30m, MSAA=x4, vsync=0, LOD=0.7, pixelLights=1.");

            // FFR (Fixed Foveated Rendering) -- ustawiamy przez OVRManager po dodaniu OVR rига
            // (patrz EnsureOVRCameraRig)
        }

        // ------------------------------------------------------------------
        // OVR Camera Rig
        // ------------------------------------------------------------------

        static void EnsureOVRCameraRig()
        {
            // Sprawdz czy juz jest
            var existing = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in existing)
            {
                if (t.name == "OVRCameraRig" || t.name == "OVRPlayerController")
                {
                    Debug.Log($"{LOG} {t.name} already in scene -- skipping OVR rig setup.");
                    ConfigureOVRManagerFFR(t.gameObject);
                    return;
                }
            }

            // Usun Main Camera jezeli zostala
            var mainCam = GameObject.Find("Main Camera");
            if (mainCam != null)
            {
                Undo.DestroyObjectImmediate(mainCam);
                Debug.Log($"{LOG} Removed Main Camera.");
            }

            // Znajdz OVRCameraRig prefab
            string[] guids = AssetDatabase.FindAssets("OVRCameraRig t:prefab");
            if (guids.Length == 0)
            {
                Debug.LogError($"{LOG} OVRCameraRig prefab not found! " +
                               "Is com.meta.xr.sdk.core installed? Run CYBERNOMAD/Meta SDK Setup first.");
                return;
            }

            string prefabPath = null;
            foreach (var guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                // Preferuj canonical OVRCameraRig.prefab (nie warianty z Interaction SDK)
                if (p.EndsWith("/OVRCameraRig.prefab"))
                {
                    prefabPath = p;
                    break;
                }
            }
            if (prefabPath == null)
                prefabPath = AssetDatabase.GUIDToAssetPath(guids[0]);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"{LOG} Could not load OVRCameraRig from: {prefabPath}");
                return;
            }

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            // Spawn na wysokosci 0 -- gracz stoi na podlodze
            rig.transform.position = new Vector3(0f, 0f, 0f);
            rig.transform.rotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(rig, "Add OVRCameraRig (Flooded Grounds)");

            Debug.Log($"{LOG} OVRCameraRig instantiated from: {prefabPath}");

            // Konfiguracja OVRManager
            ConfigureOVRManagerFFR(rig);

            Selection.activeGameObject = rig;
        }

        static void ConfigureOVRManagerFFR(GameObject rigOrManager)
        {
            // Szukaj OVRManager na obiekcie lub w dzieciach
            var mgr = rigOrManager.GetComponent<OVRManager>() ??
                      rigOrManager.GetComponentInChildren<OVRManager>();

            if (mgr == null)
            {
                Debug.LogWarning($"{LOG} OVRManager not found on {rigOrManager.name} -- skipping FFR/tracking config.");
                return;
            }

            var so = new SerializedObject(mgr);

            // TrackingOriginType = FloorLevel (1) -- gracz VR stoi
            SetSerializedProp(so, "_trackingOriginType", 1, "TrackingOrigin=FloorLevel");

            // FFR -- Fixed Foveated Rendering
            // OVRManager.fixedFoveatedRenderingLevel: None=0, Low=1, Medium=2, High=3, HighTop=4
            // Dla horroru outdoor z duza scena -- Medium to dobry kompromis
            SetSerializedProp(so, "fixedFoveatedRenderingLevel", 2, "FFR=Medium");

            // Dynamic FFR -- pozwala SDK podnosic FFR gdy GPU jest przeciazony
            SetSerializedProp(so, "useDynamicFixedFoveatedRendering", true, "DynamicFFR=true");

            so.ApplyModifiedProperties();
            Debug.Log($"{LOG} OVRManager: FloorLevel tracking, FFR=Medium, DynamicFFR=on.");
        }

        static void SetSerializedProp(SerializedObject so, string name, int value, string label)
        {
            var prop = so.FindProperty(name);
            if (prop != null) { prop.intValue = value; Debug.Log($"{LOG} {label}"); }
            else Debug.LogWarning($"{LOG} SerializedProperty not found: {name} (SDK version mismatch?)");
        }

        static void SetSerializedProp(SerializedObject so, string name, bool value, string label)
        {
            var prop = so.FindProperty(name);
            if (prop != null) { prop.boolValue = value; Debug.Log($"{LOG} {label}"); }
            else Debug.LogWarning($"{LOG} SerializedProperty not found: {name} (SDK version mismatch?)");
        }
    }

    // =========================================================================
    // FloodedGroundsPrefabPicker -- EditorWindow
    // =========================================================================

    public class FloodedGroundsPrefabPicker : EditorWindow
    {
        private const string LOG = "[PLAGA44]";
        private const string PREFABS_ROOT =
            "Assets/FloodedGrounds/Prefabs";

        // Kategorie i odpowiadajace im podfoldery
        private static readonly (string Label, string Folder)[] Categories =
        {
            ("Buildings / Barns",        "Buildings/Barns"),
            ("Buildings / BrickHouse",   "Buildings/BrickHouse"),
            ("Buildings / Bridge",       "Buildings/Bridge"),
            ("Buildings / Cabins",       "Buildings/Cabins"),
            ("Buildings / Churches",     "Buildings/Churches"),
            ("Buildings / GreenHouse",   "Buildings/GreenHouse"),
            ("Buildings / GuardHouse",   "Buildings/GuardHouse"),
            ("Buildings / IndBuilding1", "Buildings/IndBuilding1"),
            ("Buildings / IndBuilding2", "Buildings/IndBuilding2"),
            ("Buildings / LightHouse",   "Buildings/LightHouse"),
            ("Buildings / Structures1",  "Buildings/Structures1"),
            ("Buildings / Villa1",       "Buildings/Villa1"),
            ("Buildings / Villa2",       "Buildings/Villa2"),
            ("Nature / Bushes",          "Nature/Bushes"),
            ("Nature / Grass",           "Nature/Grass"),
            ("Nature / Rocks",           "Nature/Rocks"),
            ("Nature / Trees",           "Nature/Trees"),
            ("Props",                    "Props"),
            ("Atmospherics",             "Atmospherics"),
            ("Backgrounds",              "Backgrounds"),
        };

        private int _selectedCategory = 0;
        private List<string> _prefabPaths = new List<string>();
        private Vector2 _scrollCat;
        private Vector2 _scrollPrefabs;
        private string _spawnOffset = "0 0 3"; // domyslnie 3m przed graczem
        private float _spawnY = 0f;

        public static void Open()
        {
            var window = GetWindow<FloodedGroundsPrefabPicker>("Flooded Grounds Prefabs");
            window.minSize = new Vector2(520, 400);
            window.SelectCategory(0);
            window.Show();
        }

        void OnGUI()
        {
            DrawHeader();

            EditorGUILayout.BeginHorizontal();
            DrawCategoryList();
            DrawPrefabList();
            EditorGUILayout.EndHorizontal();

            DrawFooter();
        }

        void DrawHeader()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Flooded Grounds -- Prefab Picker", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Kliknij prefab zeby dodac go do aktywnej sceny. Ctrl+Z cofa.",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(4);
        }

        void DrawCategoryList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("Kategoria", EditorStyles.boldLabel);
            _scrollCat = EditorGUILayout.BeginScrollView(_scrollCat, GUILayout.Width(200));

            for (int i = 0; i < Categories.Length; i++)
            {
                var style = (i == _selectedCategory)
                    ? new GUIStyle(EditorStyles.toolbarButton) { fontStyle = FontStyle.Bold }
                    : EditorStyles.toolbarButton;

                if (GUILayout.Button(Categories[i].Label, style))
                {
                    SelectCategory(i);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawPrefabList()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(
                $"Prefaby -- {Categories[_selectedCategory].Label} ({_prefabPaths.Count})",
                EditorStyles.boldLabel);

            _scrollPrefabs = EditorGUILayout.BeginScrollView(_scrollPrefabs);

            foreach (var path in _prefabPaths)
            {
                string displayName = Path.GetFileNameWithoutExtension(path);

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(displayName, GUILayout.ExpandWidth(true)))
                {
                    SpawnPrefab(path);
                }

                // Przycisk "ping" -- zaznacza asset w Project window
                if (GUILayout.Button(">>", GUILayout.Width(28)))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset != null) EditorGUIUtility.PingObject(asset);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawFooter()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Spawn Y offset:", GUILayout.Width(90));
            _spawnY = EditorGUILayout.FloatField(_spawnY, GUILayout.Width(60));
            EditorGUILayout.LabelField("(m nad podloga -- 0 = na poziomie terenu)", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        void SelectCategory(int index)
        {
            _selectedCategory = index;
            _prefabPaths.Clear();

            string folderPath = $"{PREFABS_ROOT}/{Categories[index].Folder}";
            string[] guids = AssetDatabase.FindAssets("t:prefab", new[] { folderPath });

            foreach (var guid in guids)
            {
                _prefabPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            _prefabPaths.Sort();
            Repaint();
        }

        void SpawnPrefab(string assetPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogError($"{LOG} Cannot load prefab: {assetPath}");
                return;
            }

            // Pozycja spawnu: przed kamera edytora lub przed SceneView
            Vector3 spawnPos = GetSpawnPosition();
            spawnPos.y += _spawnY;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = spawnPos;
            Undo.RegisterCreatedObjectUndo(go, $"Spawn {prefab.name}");
            Selection.activeGameObject = go;

            EditorSceneManager.MarkSceneDirty(
                EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} Spawned: {prefab.name} at {spawnPos}");
        }

        static Vector3 GetSpawnPosition()
        {
            // Probuj pobrac pozycje z aktywnego SceneView (pivot kamery edytora)
            if (SceneView.lastActiveSceneView != null)
            {
                var sv = SceneView.lastActiveSceneView;
                // pivot + 3m w kierunku patrzenia kamery
                Vector3 forward = sv.camera.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                else forward.Normalize();
                return sv.pivot + forward * 3f;
            }

            return new Vector3(0f, 0f, 3f);
        }
    }
}
#endif
