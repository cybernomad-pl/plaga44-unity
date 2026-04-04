#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Plaga44.Editor
{
    // -------------------------------------------------------------------------
    // Plaga44SceneBuilder
    //
    // MenuItem 1: CYBERNOMAD/Scene/Load PLAGA 44 Demo
    //   1. Tworzy nowa scene PLAGA44_Demo.unity
    //   2. Laduje Scene_A.unity additive (teren PLAGA44)
    //   3. Przenosi obiekty z Scene_A do nowej sceny
    //   4. Czysci (FPS Controller, kamery, event systemy, unwanted objects)
    //   5. Dodaje OVRPlayerController + bron w rekach
    //   6. Ustawia Build Settings (tylko ta scena)
    //   7. Usuwa stare sceny z projektu
    //
    // MenuItem 2: CYBERNOMAD/Scene/Prefab Picker
    //   - Tworzy nowa pusta scene + OVRCameraRig + Prefab Picker window
    // -------------------------------------------------------------------------

    public static class Plaga44SceneBuilder
    {
        private const string LOG = "[PLAGA44]";

        private const string SCENE_A_PATH =
            "Assets/FloodedGrounds/Scenes/Scene_A.unity";

        private const string DEMO_SCENE_PATH =
            "Assets/Scenes/PLAGA44_Demo.unity";

        private const string PREFABS_ROOT =
            "Assets/PLAGA44/Environment/Prefabs";

        private const string SPLASH_SCENE_PATH =
            "Assets/Scenes/SplashScene.unity";

        // Sceny do usuniecia z projektu (smieci z wczesniejszych iteracji)
        private static readonly string[] SCENES_TO_DELETE = new string[]
        {
            "Assets/Scenes/PLAGA44_Level.unity",
            "Assets/Scenes/HandGrabExamples.unity",
            "Assets/Scenes/LocomotionExamples.unity",
            "Assets/Scenes/SampleScene.unity",
            "Assets/Setup.unity",
            "Assets/Setup.unity2.unity",
            "Assets/setup2.unity",
            "Assets/TESTBED-1.unity",
            "Assets/testbed.unity",
            "Assets/_Recovery/0.unity",
        };

        // ------------------------------------------------------------------
        // MenuItem 1 -- Load PLAGA 44 Demo
        // ------------------------------------------------------------------

        [MenuItem("CYBERNOMAD/Scene/Load PLAGA 44 Demo", false, 10)]
        public static void BuildDemoScene()
        {
            if (!File.Exists(Path.Combine(Application.dataPath, "..", SCENE_A_PATH)))
            { Debug.LogError($"{LOG} {SCENE_A_PATH} not found"); return; }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            DeleteOldScenes();

            // Otwórz Scene_A, zrób wszystko, zapisz jako PLAGA44_Demo
            Scene scene = EditorSceneManager.OpenScene(SCENE_A_PATH, OpenSceneMode.Single);

            RemovePreviousSpawns();
            RemoveLegacyEventSystems();

            // v2: BLACKLIST -- destroy specific named objects, keep everything else
            DestroyByName();

            SetQuestRenderingSettings();

            var player = TestEnvironmentSetup.AddPlayerControllerPublic();
            if (player != null)
            {
                player.transform.position = ITEM_SPAWN_POINTS[UnityEngine.Random.Range(0, ITEM_SPAWN_POINTS.Length)];
                if (player.GetComponent<VRCrouch>() == null)
                    player.AddComponent<VRCrouch>();

                // Make player taller -- PLAGA44 architecture is oversized
                // OVRPlayerController sets camera Y = -(0.5*height) + center.y
                // Default: height=1.8, center=0.9 -> camera at 0
                // We want camera ~1.4m higher to match door handle height
                var cc = player.GetComponent<CharacterController>();
                if (cc != null)
                {
                    cc.height = 4.6f;       // tall capsule
                    cc.center = new Vector3(0f, 3.7f, 0f); // center high
                    cc.radius = 0.4f;
                    // camera Y = -(0.5*4.6) + 3.7 = -2.3 + 3.7 = 1.4m above player origin
                }
                // Also scale slightly for hand/world proportion
                player.transform.localScale = Vector3.one * 1.3f;
            }
            else
                EnsureOVRCameraRig();

            MaterialUpgrader.UpgradeMaterials();
            FixWaterMaterials();

            SpawnItems();
            SetupWeaponManagers();
            AddPostProcessing();
            FixParticleMaterials();
            FixLeavesAndGrass();

            // Zapisz jako nowa scena (Scene_A nietknięta)
            string scenesDir = Path.Combine(Application.dataPath, "Scenes");
            if (!Directory.Exists(scenesDir)) Directory.CreateDirectory(scenesDir);
            EditorSceneManager.SaveScene(scene, DEMO_SCENE_PATH);

            // Clean orphaned prefab refs from saved YAML
            CleanOrphanedPrefabs(Path.Combine(Application.dataPath, "..", DEMO_SCENE_PATH));

            EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(SPLASH_SCENE_PATH, true),
                new EditorBuildSettingsScene(DEMO_SCENE_PATH, true)
            };

            Debug.Log($"{LOG} === PLAGA '44 Demo READY ({DEMO_SCENE_PATH}). Press Play. ===");
        }

        static void DeleteOldScenes()
        {
            int deleted = 0;
            foreach (var scenePath in SCENES_TO_DELETE)
            {
                if (File.Exists(Path.Combine(Application.dataPath, "..", scenePath)))
                {
                    AssetDatabase.DeleteAsset(scenePath);
                    // Also delete .meta
                    string metaPath = scenePath + ".meta";
                    if (File.Exists(Path.Combine(Application.dataPath, "..", metaPath)))
                        FileUtil.DeleteFileOrDirectory(Path.Combine(Application.dataPath, "..", metaPath));
                    deleted++;
                    Debug.Log($"{LOG} Deleted old scene: {scenePath}");
                }
            }
            if (deleted > 0)
            {
                AssetDatabase.Refresh();
                Debug.Log($"{LOG} Cleaned up {deleted} old scenes.");
            }
        }

        // ------------------------------------------------------------------
        // Spawn grabbable items
        // ------------------------------------------------------------------

        static void AttachWeaponsToHands(GameObject player)
        {
            string swordPath = "Assets/PLAGA44/Weapons/Prefabs/Sword.prefab";
            string gunPath = "Assets/PLAGA44/Weapons/Prefabs/GunWithShooting.prefab";
            string bulletPath = "Assets/PLAGA44/Weapons/Prefabs/Bullet.prefab";

            var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(swordPath);
            var gunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(gunPath);
            var bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(bulletPath);

            // Find hand anchors in OVRPlayerController -> OVRCameraRig
            var rig = player.GetComponentInChildren<OVRCameraRig>();
            if (rig == null) return;

            // ---- Sword in LEFT hand ----
            // Wartości z oryginalnej GameScene.unity (scene overrides na prefab)
            GameObject swordInstance = null;
            if (swordPrefab != null && rig.leftHandAnchor != null)
            {
                var sword = (GameObject)PrefabUtility.InstantiatePrefab(swordPrefab);
                sword.transform.SetParent(rig.leftHandAnchor);
                sword.transform.localPosition = new Vector3(0.01f, 0.27f, 0.172f);
                sword.transform.localRotation = Quaternion.Euler(-54f, 0f, 0f);
                sword.transform.localScale = Vector3.one;
                sword.name = "Sword_LeftHand";
                swordInstance = sword;

                SetLayerRecursive(sword, 0);

                // Blade: remove MeshCollider (jak w oryginale), add BoxCollider
                var bladeMC = sword.GetComponentInChildren<MeshCollider>();
                if (bladeMC != null)
                {
                    var bladeGO = bladeMC.gameObject;
                    Object.DestroyImmediate(bladeMC);
                    // Replace with BoxCollider for OVRGrabbable + slicing
                    if (bladeGO.GetComponent<Collider>() == null)
                    {
                        var bc = bladeGO.AddComponent<BoxCollider>();
                        bc.size = new Vector3(0.05f, 0.8f, 0.05f);
                        bc.center = new Vector3(0f, 0.4f, 0f);
                    }
                }

                // Rigidbody on ROOT (OVRGrabbable needs it on same GO)
                var rb = sword.GetComponent<Rigidbody>();
                if (rb == null) rb = sword.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                // Collider on root for OVRGrabbable grab points
                if (sword.GetComponent<Collider>() == null)
                {
                    var rootCol = sword.AddComponent<BoxCollider>();
                    rootCol.size = new Vector3(0.05f, 0.3f, 0.05f);
                }

                if (sword.GetComponent<OVRGrabbable>() == null)
                    sword.AddComponent<OVRGrabbable>();

                SetupSwordSlicer(sword);
                Undo.RegisterCreatedObjectUndo(sword, "Attach Sword");
            }

            // ---- Gun in RIGHT hand ----
            // Wartości z oryginalnej GameScene.unity (scene overrides na prefab)
            if (gunPrefab != null && rig.rightHandAnchor != null)
            {
                var gun = (GameObject)PrefabUtility.InstantiatePrefab(gunPrefab);
                gun.transform.SetParent(rig.rightHandAnchor);
                gun.transform.localPosition = new Vector3(-0.0139f, -0.0059f, 0.0228f);
                gun.transform.localRotation = Quaternion.Euler(-64f, 10f, -101f);
                gun.transform.localScale = new Vector3(0.01939102f, 0.01939102f, 0.01939102f);
                gun.name = "Gun_RightHand";

                SetLayerRecursive(gun, 0);

                // Rigidbody + Collider on ROOT for OVRGrabbable
                var rb = gun.GetComponent<Rigidbody>();
                if (rb == null) rb = gun.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                if (gun.GetComponent<Collider>() == null)
                {
                    var gunCol = gun.AddComponent<BoxCollider>();
                    gunCol.size = new Vector3(8f, 12f, 25f); // scale 0.019 -> world ~0.15x0.23x0.48m
                    gunCol.center = new Vector3(0f, -3f, 5f);
                }

                if (gun.GetComponent<OVRGrabbable>() == null)
                    gun.AddComponent<OVRGrabbable>();

                var shooting = gun.GetComponent<Shooting>();
                if (shooting != null)
                {
                    if (bulletPrefab != null)
                        shooting.bulletPrefab = bulletPrefab;
                    if (swordInstance != null)
                        shooting.slicerGameobject = swordInstance;
                }

                Undo.RegisterCreatedObjectUndo(gun, "Attach Gun");
            }

            // ---- Scene singletons: VibrationManager + AudioManager ----
            SetupWeaponManagers();
        }

        static void SetupSwordSlicer(GameObject sword)
        {
            // Find the blade -- first child with MeshCollider
            MeshCollider bladeCollider = sword.GetComponentInChildren<MeshCollider>();
            if (bladeCollider == null)
            {
                Debug.LogWarning($"{LOG} No MeshCollider found on sword -- adding BoxCollider as trigger for slicing.");
                var col = sword.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(0.1f, 1.0f, 0.1f); // blade shape
            }

            GameObject bladeGO = bladeCollider != null ? bladeCollider.gameObject : sword;

            // TODO: Slicer + SliceListener (disabled in pre-alpha)
            Debug.Log($"{LOG} Blade collider setup on '{bladeGO.name}'.");
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        static void SetupWeaponManagers()
        {
            // VibrationManager singleton
            if (Object.FindAnyObjectByType<VibrationManager>() == null)
            {
                var vibGO = new GameObject("VibrationManager");
                vibGO.AddComponent<VibrationManager>();
                Undo.RegisterCreatedObjectUndo(vibGO, "Add VibrationManager");
                Debug.Log($"{LOG} VibrationManager singleton added to scene.");
            }

            // AudioManager singleton with gun/slice sounds
            if (Object.FindAnyObjectByType<AudioManager>() == null)
            {
                var audioGO = new GameObject("AudioManager");
                var am = audioGO.AddComponent<AudioManager>();

                // Load audio clips from PLAGA44
                var gunClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/PLAGA44/Audio/gunSound.wav");
                var sliceClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/PLAGA44/Audio/SliceSound.wav");

                if (gunClip != null)
                {
                    var gunSrc = audioGO.AddComponent<AudioSource>();
                    gunSrc.clip = gunClip;
                    gunSrc.playOnAwake = false;
                    gunSrc.spatialBlend = 1f;
                    am.gunSound = gunSrc;
                }

                if (sliceClip != null)
                {
                    var sliceSrc = audioGO.AddComponent<AudioSource>();
                    sliceSrc.clip = sliceClip;
                    sliceSrc.playOnAwake = false;
                    sliceSrc.spatialBlend = 1f;
                    am.sliceSound = sliceSrc;
                }

                Undo.RegisterCreatedObjectUndo(audioGO, "Add AudioManager");
                Debug.Log($"{LOG} AudioManager singleton added (gun={gunClip != null}, slice={sliceClip != null}).");
            }
        }

        static void AddPostProcessing()
        {
            string profilePath = "Assets/Settings/PLAGA44_PostProcess.asset";
            string settingsDir = Path.Combine(Application.dataPath, "Settings");
            if (!Directory.Exists(settingsDir)) Directory.CreateDirectory(settingsDir);

            // Always delete and recreate profile to ensure correct values
            if (File.Exists(Path.Combine(Application.dataPath, "..", profilePath)))
                AssetDatabase.DeleteAsset(profilePath);

            // Remove existing Volume from scene
            var existingVol = Object.FindAnyObjectByType<Volume>();
            if (existingVol != null)
                Object.DestroyImmediate(existingVol.gameObject);

            // Create profile asset FIRST (empty), then add components as sub-assets
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);

            // Create ColorAdjustments as sub-asset so it serializes to disk
            var colorAdj = ScriptableObject.CreateInstance<ColorAdjustments>();
            colorAdj.name = "ColorAdjustments";
            colorAdj.active = true;
            colorAdj.saturation.Override(76f);
            colorAdj.contrast.Override(30f);
            colorAdj.postExposure.Override(0.5f);

            // Add as sub-asset + register in profile component list
            AssetDatabase.AddObjectToAsset(colorAdj, profile);
            profile.components.Add(colorAdj);

            EditorUtility.SetDirty(colorAdj);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Reload from disk to confirm
            profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            Debug.Log($"{LOG} Profile components: {profile.components.Count} (expected 1)");

            var volGO = new GameObject("PostProcess_Volume");
            var volume = volGO.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1;
            volume.sharedProfile = profile;

            Undo.RegisterCreatedObjectUndo(volGO, "Add PostProcess Volume");
            Debug.Log($"{LOG} Post-processing saved: {profilePath} (sat=76, contrast=30, exp=0.5)");
        }

        // Player + item spawn point: bridge/gate area
        private static readonly Vector3[] ITEM_SPAWN_POINTS = new Vector3[]
        {
            new Vector3(457.94f, 16.45f, 409.63f),  // bridge gate entrance
        };

        static void FixWaterMaterials()
        {
            // Assign WaterMaterial.mat to all water objects in scene
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PLAGA44/Shaders/WaterMaterial.mat");
            if (waterMat == null)
            {
                Debug.LogWarning($"{LOG} WaterMaterial.mat not found at Assets/PLAGA44/Shaders/WaterMaterial.mat");
                return;
            }

            int fixed_ = 0;
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                string n = r.gameObject.name.ToLowerInvariant();
                if (!n.Contains("water") && !n.Contains("3d_water")) continue;

                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || mats[i].shader.name.Contains("Error") || mats[i].name.Contains("Default-Material"))
                    {
                        mats[i] = waterMat;
                        changed = true;
                    }
                }
                if (changed)
                {
                    r.sharedMaterials = mats;
                    fixed_++;
                    Debug.Log($"{LOG} Fixed water material on '{r.gameObject.name}'");
                }
            }
            Debug.Log($"{LOG} FixWaterMaterials: {fixed_} objects fixed");
        }

        static void FixParticleMaterials()
        {
            var urpParticleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urpParticleShader == null)
            {
                Debug.LogWarning($"{LOG} URP Particles/Unlit shader not found.");
                return;
            }

            // Fix ALL atmospheric/particle materials ON DISK
            // Catches: ATM_Leaf, ATM_DustParticle, ATM_HaloRing, etc.
            // regardless of current shader (URP/Lit, legacy Particles, etc.)
            string[] folders = { "Assets/PLAGA44" };
            string[] guids = AssetDatabase.FindAssets("t:Material", folders);
            int fixed_ = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                string matName = mat.name.ToLowerInvariant();
                string shaderName = mat.shader.name;

                // Fix if: (a) name contains ATM_ (atmospheric particles), or
                //          (b) shader is legacy Particles (not yet URP)
                bool isAtmospheric = matName.Contains("atm_");
                bool isLegacyParticle = shaderName.Contains("Particles/") && !shaderName.Contains("Universal");

                if (isAtmospheric || isLegacyParticle)
                {
                    mat.shader = urpParticleShader;
                    mat.SetFloat("_Surface", 1); // Transparent

                    // _Blend enum in URP Particles/Unlit:
                    // 0=Alpha (SrcAlpha/OneMinusSrcAlpha)
                    // 1=Premultiply (One/OneMinusSrcAlpha) <-- WRONG, causes white/black BG!
                    // 2=Additive (SrcAlpha/One)
                    // Leaves = Alpha blend (0), Dust/Halo = Additive (2)
                    bool isLeaf = matName.Contains("leaf");
                    int blendMode = isLeaf ? 0 : 2; // Alpha for leaves, Additive for dust/halo
                    mat.SetFloat("_Blend", blendMode);

                    if (isLeaf)
                    {
                        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.EnableKeyword("_BLENDMODE_ALPHA");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    }
                    else
                    {
                        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                        mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.DisableKeyword("_BLENDMODE_ALPHA");
                    }

                    mat.SetFloat("_ZWrite", 0);
                    mat.renderQueue = 3000;
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.DisableKeyword("_EMISSION");
                    EditorUtility.SetDirty(mat);
                    fixed_++;
                    Debug.Log($"{LOG} Fixed particle material: {mat.name} -> blend={blendMode} ({(isLeaf ? "Alpha" : "Additive")})");
                }
            }

            // Also fix in-scene particle renderers whose materials aren't in PLAGA44
            var allPS = Object.FindObjectsByType<ParticleSystemRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var psr in allPS)
            {
                foreach (var mat in psr.sharedMaterials)
                {
                    if (mat == null) continue;
                    string sn = mat.shader.name;
                    string mn = mat.name.ToLowerInvariant();
                    if ((sn.Contains("Particles/") && !sn.Contains("Universal")) || mn.Contains("atm_"))
                    {
                        mat.shader = urpParticleShader;
                        mat.SetFloat("_Surface", 1);
                        bool leaf = mn.Contains("leaf");
                        mat.SetFloat("_Blend", leaf ? 0 : 2);
                        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetFloat("_DstBlend", leaf
                            ? (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
                            : (float)UnityEngine.Rendering.BlendMode.One);
                        mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetFloat("_DstBlendAlpha", leaf
                            ? (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
                            : (float)UnityEngine.Rendering.BlendMode.One);
                        mat.SetFloat("_ZWrite", 0);
                        mat.renderQueue = 3000;
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        if (leaf) { mat.EnableKeyword("_BLENDMODE_ALPHA"); mat.DisableKeyword("_ALPHAPREMULTIPLY_ON"); }
                        else { mat.EnableKeyword("_ALPHAPREMULTIPLY_ON"); mat.DisableKeyword("_BLENDMODE_ALPHA"); }
                        EditorUtility.SetDirty(mat);
                        fixed_++;
                    }
                }
            }

            if (fixed_ > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"{LOG} Fixed {fixed_} particle materials to URP Particles/Unlit Additive (saved to disk).");
            }
        }

        static void SpawnItems()
        {
            string swordPath = "Assets/PLAGA44/Weapons/Prefabs/Sword.prefab";
            string gunPath = "Assets/PLAGA44/Weapons/Prefabs/GunWithShooting.prefab";

            var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(swordPath);
            var gunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(gunPath);

            if (swordPrefab == null) Debug.LogError($"{LOG} Sword prefab not found: {swordPath}");
            if (gunPrefab == null) Debug.LogError($"{LOG} Gun prefab not found: {gunPath}");

            // Find terrain for height sampling
            var terrain = Terrain.activeTerrain;

            // Spawn both weapons at the spawn point
            GameObject[] prefabs = new GameObject[] { swordPrefab, gunPrefab };
            int spawned = 0;
            for (int i = 0; i < prefabs.Length; i++)
            {
                var prefab = prefabs[i];
                if (prefab == null) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

                // Both weapons at same point, offset sideways
                Vector3 pos = ITEM_SPAWN_POINTS[0];
                if (terrain != null)
                    pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y + 0.8f;
                else
                    pos.y += 0.8f;
                pos.x += (i == 0) ? -0.3f : 0.3f; // sword left, gun right

                go.transform.position = pos;
                go.name = $"{prefab.name}_Spawn{i}";

                // Layer 0
                SetLayerRecursive(go, 0);

                // Rigidbody on root
                var rb = go.GetComponent<Rigidbody>();
                if (rb == null) rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.5f;
                rb.useGravity = true;
                rb.isKinematic = false;

                // Collider on root for grab
                if (go.GetComponent<Collider>() == null)
                    go.AddComponent<BoxCollider>();

                // OVRGrabbable
                if (go.GetComponent<OVRGrabbable>() == null)
                    go.AddComponent<OVRGrabbable>();

                // Disable Shooting script on spawned guns (only works when held)
                var shooting = go.GetComponent<Shooting>();
                if (shooting != null) shooting.enabled = false;

                Undo.RegisterCreatedObjectUndo(go, $"Spawn {go.name}");
                spawned++;
            }

            Debug.Log($"{LOG} Spawned {spawned} items on terrain at {ITEM_SPAWN_POINTS.Length} locations.");
        }

        // ------------------------------------------------------------------
        // MenuItem 2 -- Prefab Picker (nowa scena)
        // ------------------------------------------------------------------

        // [MenuItem("CYBERNOMAD/Scene/Prefab Picker", false, 11)]
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
            Plaga44PrefabPicker.Open();
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

        // v2 WHITELIST: ONLY raw nature -- zero man-made
        private static readonly string[] KEEP_PATTERNS = new string[]
        {
            "terrain",
            "water", "3d_water",
            "tree",
            "grass",
            "bush",
            "wind",
            "sun",
        };

        // v2: explicit blacklist -- destroy these root objects by name
        static void DestroyByName()
        {
            string[] destroyRoots = {
                "ScienceBuilding", "_FloodedBuilding2", "_GUI", "Canvas",
                "Point light", "GameObject",
            };
            foreach (var name in destroyRoots)
            {
                var go = GameObject.Find(name);
                while (go != null)
                {
                    Debug.Log($"{LOG} Destroying root: '{go.name}'");
                    Object.DestroyImmediate(go);
                    go = GameObject.Find(name);
                }
            }

            // Destroy orphaned prefab instances + numbered objects
            var all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in all)
            {
                if (go == null) continue;
                string n = go.name.ToLowerInvariant();
                if (n.StartsWith("pointlight_bounce") || n.StartsWith("reverb_interior") ||
                    n.Contains("windowglass") || n.Contains("interiordust") ||
                    n.Contains("deco_window") || n.Contains("_glass"))
                {
                    Object.DestroyImmediate(go);
                }
            }

            // Inside PLAGA44: destroy all man-made children
            var fg = GameObject.Find("Environment") ?? GameObject.Find("FloodedGrounds");
            if (fg != null)
            {
                // Blacklist patterns for PLAGA44 children
                string[] blacklist = {
                    "villa", "brick", "church", "ind_", "indbuilding",
                    "lighthouse", "cabin", "barn", "guard", "greenhouse",
                    "bridge", "struct_", "pavement", "blockout",
                    "prop_", "rock", "deco", "window",
                    "door", "wall", "base_", "top_", "cor_",
                    "floor", "roof", "stair", "chimney", "rail",
                    "column", "balcon", "ceil", "support",
                };
                for (int i = fg.transform.childCount - 1; i >= 0; i--)
                {
                    var child = fg.transform.GetChild(i).gameObject;
                    string cn = child.name.ToLowerInvariant();

                    // Keep ONLY: water, terrain, sky-related
                    bool keep = cn.Contains("water") || cn.Contains("terrain") ||
                                cn.Contains("sun") || cn.Contains("fog");

                    if (!keep)
                    {
                        Object.DestroyImmediate(child);
                    }
                }
                Debug.Log($"{LOG} PLAGA44: kept only nature children.");
                // Rename container to our name
                if (fg.name != "Environment")
                    fg.name = "Environment";
            }
        }

        static void RemoveUnwantedObjects()
        {
            // Step 1: Remove non-whitelisted ROOT objects (except PLAGA44 container)
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            int removedRoots = 0;
            foreach (var go in roots)
            {
                if (go == null) continue;
                string n = go.name.ToLowerInvariant();

                // Always keep these roots
                if (n.Contains("terrain") || n.Contains("water") || n.Contains("sun") ||
                    n.Contains("fog") || n == "floodedgrounds" || n == "environment" ||
                    n.Contains("ovr") || n.Contains("player") || n.Contains("postprocess") ||
                    n.Contains("vibration") || n.Contains("audio") || n.Contains("spawn"))
                    continue;

                Debug.Log($"{LOG} ROOT destroying: '{go.name}'");
                Object.DestroyImmediate(go);
                removedRoots++;
            }

            // Step 2: Remove non-nature children INSIDE PLAGA44
            var fg = GameObject.Find("Environment") ?? GameObject.Find("FloodedGrounds");
            int removedChildren = 0;
            if (fg != null)
            {
                for (int i = fg.transform.childCount - 1; i >= 0; i--)
                {
                    var child = fg.transform.GetChild(i).gameObject;
                    if (child == null) continue;
                    string cn = child.name.ToLowerInvariant();

                    bool keep = false;
                    foreach (var pattern in KEEP_PATTERNS)
                    {
                        if (cn.Contains(pattern)) { keep = true; break; }
                    }
                    if (!keep)
                    {
                        Object.DestroyImmediate(child);
                        removedChildren++;
                    }
                }
            }

            Debug.Log($"{LOG} Whitelist: removed {removedRoots} roots + {removedChildren} PLAGA44 children.");
        }

        static void RemoveAllNonNature()
        {
            // Brute force: find ALL GameObjects, destroy anything man-made
            // This catches orphaned prefab instances (WindowGlass etc.)
            var all = Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int removed = 0;
            foreach (var go in all)
            {
                if (go == null) continue;
                string n = go.name.ToLowerInvariant();

                // Skip system/VR objects
                if (n.Contains("ovr") || n.Contains("player") || n.Contains("hand") ||
                    n.Contains("controller") || n.Contains("anchor") || n.Contains("tracking") ||
                    n.Contains("collider") || n.Contains("camera") || n.Contains("spawn") ||
                    n.Contains("postprocess") || n.Contains("vibration") || n.Contains("audio") ||
                    n.Contains("grab") || n.Contains("event")) continue;

                // Skip nature
                bool isNature = false;
                foreach (var p in KEEP_PATTERNS)
                {
                    if (n.Contains(p)) { isNature = true; break; }
                }
                if (isNature) continue;

                // Skip if empty name or "GameObject" (could be terrain child)
                if (n == "" || n == "gameobject") continue;

                // Everything else = man-made = destroy
                Debug.Log($"{LOG} SWEEP destroying: '{go.name}' (parent: {(go.transform.parent != null ? go.transform.parent.name : "ROOT")})");
                Object.DestroyImmediate(go);
                removed++;
            }
            if (removed > 0)
                Debug.Log($"{LOG} Final sweep: removed {removed} non-nature objects.");
        }

        static void FixLeavesAndGrass()
        {
            // 1. Shrink leaf particle systems (3x too big)
            var allPS = Object.FindObjectsByType<ParticleSystem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var ps in allPS)
            {
                if (ps == null) continue;
                string n = ps.gameObject.name.ToLowerInvariant();
                if (n.Contains("leaf") || n.Contains("atm_"))
                {
                    ps.transform.localScale = Vector3.one * 0.33f;
                    Debug.Log($"{LOG} Shrunk particle: {ps.gameObject.name} to 0.33 scale.");
                }
            }

            // 2. Disable WindZone (kills grass animation)
            var windZones = Object.FindObjectsByType<WindZone>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var wz in windZones)
            {
                wz.gameObject.SetActive(false);
                Debug.Log($"{LOG} Disabled WindZone: {wz.gameObject.name}");
            }

            // 3. Disable terrain grass wave animation + kill billboards
            var terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                terrain.terrainData.wavingGrassSpeed = 0f;
                terrain.terrainData.wavingGrassAmount = 0f;
                terrain.terrainData.wavingGrassStrength = 0f;

                // KILL tree billboards -- they wobble in VR when head moves
                terrain.treeDistance = 50000f;          // render trees at any distance
                terrain.treeBillboardDistance = 50000f;  // NEVER switch to billboard
                terrain.treeCrossFadeLength = 0f;        // no cross-fade transition
                terrain.treeMaximumFullLODCount = 50000; // all trees at full LOD
                terrain.detailObjectDistance = 500f;     // grass/detail visibility
                terrain.detailObjectDensity = 1f;        // full density
                terrain.heightmapPixelError = 1f;        // max terrain mesh quality

                // STRIP: usun drzewa z terrain data
                int treeCount = terrain.terrainData.treeInstanceCount;
                terrain.terrainData.treeInstances = new TreeInstance[0];
                terrain.terrainData.RefreshPrototypes();
                Debug.Log($"{LOG} Removed {treeCount} trees from terrain data");

                // STRIP: usun trawe/details z terrain data
                int detailLayers = terrain.terrainData.detailPrototypes.Length;
                for (int i = 0; i < detailLayers; i++)
                {
                    int res = terrain.terrainData.detailResolution;
                    terrain.terrainData.SetDetailLayer(0, 0, i, new int[res, res]);
                }
                Debug.Log($"{LOG} Cleared {detailLayers} detail layers (grass)");

                terrain.Flush();
                Debug.Log($"{LOG} Terrain: STRIPPED -- no trees, no grass, terrain+water+sky only.");
            }
        }

        // Orphaned prefab GUIDs to strip from scene YAML after save
        private static readonly string[] ORPHAN_GUIDS = new string[]
        {
            "fa96530a2d3d74a4796598aa6fdfecb2", // Church1_Deco_WindowGlass_A
            "02a0e42f36bf76d4c946b93fc70c3cee", // IndBuilding2_Deco_WindowGlass_A
            "058d6c4dd4db88e4f8b9dbe1b34054ac", // Villa1_Deco_WindowGlass_C
            "15694160f3f4559468922f6c406043a6", // Villa1_Deco_WindowGlass_A
            "33d2396704f502c40bd674b135e08461", // Villa2_Deco_WindowGlass_A
            "475b1b6b7e2809143be41cfe67240ecf", // Villa1_Deco_WindowGlass_B
            "715fd63a46fbdbe4aaa1d6f63749e896", // Cabin1_Deco_WindowGlass_A
            "773c221436526d44f8af6a31cd3c49ad", // Cabin2_Deco_WindowGlass_A
            "7e9dc500b042d594f976c40103e5aa08", // BrickHouse_Deco_WindowGlass_A
            "8bdc869548e29fd4fbdf4c6b361ce4ce", // Church1_Deco_WindowGlass_B
            "eaaa713946d21c349acbc0f8b739f316", // BrickHouse_Deco_WindowGlass_B
        };

        static void CleanOrphanedPrefabs(string scenePath)
        {
            string text = File.ReadAllText(scenePath);
            // Split into YAML documents (separated by "--- !u!")
            var docs = System.Text.RegularExpressions.Regex.Split(text, @"(?=--- !u!)");
            var clean = new System.Text.StringBuilder();
            int removed = 0;

            foreach (var doc in docs)
            {
                bool orphan = false;
                foreach (var guid in ORPHAN_GUIDS)
                {
                    if (doc.Contains(guid)) { orphan = true; break; }
                }
                if (!orphan)
                    clean.Append(doc);
                else
                    removed++;
            }

            if (removed > 0)
            {
                File.WriteAllText(scenePath, clean.ToString());
                AssetDatabase.Refresh();
                Debug.Log($"{LOG} Cleaned {removed} orphaned prefab blocks from scene YAML.");
            }
        }

        static void RemovePreviousSpawns()
        {
            // Remove weapon spawns / managers from previous Load runs baked into Scene_A
            var allObjects = Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int removed = 0;
            foreach (var go in allObjects)
            {
                if (go == null) continue;
                string n = go.name;
                if (n.Contains("_Spawn") || n == "Sword_LeftHand" || n == "Gun_RightHand" ||
                    n == "VibrationManager" || n == "AudioManager" || n == "OVRPlayerController")
                {
                    Object.DestroyImmediate(go);
                    removed++;
                }
            }
            if (removed > 0)
                Debug.Log($"{LOG} Removed {removed} leftover objects from previous Load.");
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
                #pragma warning disable CS0618
                var touch = es.GetComponent<UnityEngine.EventSystems.TouchInputModule>();
                #pragma warning restore CS0618
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
            // MAX QUALITY -- ZERO performance optimizations
            QualitySettings.shadowDistance = 150f;
            QualitySettings.shadows = UnityEngine.ShadowQuality.All;
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.VeryHigh;
            QualitySettings.antiAliasing = 8;
            QualitySettings.vSyncCount = 0;
            QualitySettings.lodBias = 100f;           // NEVER switch to lower LOD
            QualitySettings.maximumLODLevel = 0;       // Force highest LOD detail
            QualitySettings.pixelLightCount = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.skinWeights = SkinWeights.FourBones;
            QualitySettings.softParticles = true;

            // LOD cross-fade disabled in URP .asset files on disk (m_EnableLODCrossFade: 0)

            Debug.Log($"{LOG} Render settings: MAX QUALITY (shadows=150m, MSAA=x8, LOD=100, pixelLights=4, NO LOD switching).");
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
            Undo.RegisterCreatedObjectUndo(rig, "Add OVRCameraRig (PLAGA44)");

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
    // Plaga44PrefabPicker -- EditorWindow
    // =========================================================================

    public class Plaga44PrefabPicker : EditorWindow
    {
        private const string LOG = "[PLAGA44]";
        private const string PREFABS_ROOT =
            "Assets/PLAGA44/Prefabs";

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
        #pragma warning disable CS0414
        private string _spawnOffset = "0 0 3"; // domyslnie 3m przed graczem
        #pragma warning restore CS0414
        private float _spawnY = 0f;

        public static void Open()
        {
            var window = GetWindow<Plaga44PrefabPicker>("PLAGA44 Prefabs");
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
            EditorGUILayout.LabelField("PLAGA44 -- Prefab Picker", EditorStyles.boldLabel);
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
