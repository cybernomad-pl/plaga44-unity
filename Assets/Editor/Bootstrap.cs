// =============================================================================
// Bootstrap.cs
// CYBERNOMAD -- Ladowanie sceny PLAGA '44 + walidacja/napraw elementow.
// Uruchamia sie automatycznie przy starcie edytora (InitializeOnLoad).
// Menu: CYBERNOMAD > Scene > Load PLAGA44 Demo / CYBERNOMAD > Bootstrap.
// =============================================================================
#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Plaga44.Locomotion;
using Plaga44.UI;
using Plaga44.Feedback;
using Plaga44.Inventory;

namespace Plaga44.Editor
{
    [InitializeOnLoad]
    public static class Bootstrap
    {
        // ---- Paths ---------------------------------------------------------
        private const string ScenePath = "Assets/PLAGA44/TESTBED.unity";
        private const string TerrainAsset = "Assets/Potok/Terrain/Scene_A_Terrain.asset";
        private const string TerrainMatPath = "Assets/PLAGA44/Materials/TerrainLit.mat";
        private const string TerrainMatFolderParent = "Assets/PLAGA44";
        private const string TerrainMatFolderName = "Materials";
        private const string SkyboxMatPath = "Assets/Potok/Skybox/BGR_Sky1.mat";
        private const string AvatarRegistryPath = "Assets/PLAGA44/Resources/AvatarRegistry.asset";
        private const string BootstrapSessionKey = "Plaga44.OpenScene.Done";

        // ---- Scene object names -------------------------------------------
        private const string OvrRigName = "OVRCameraRig";
        private const string RightHandAnchorName = "RightHandAnchor";
        private const string LeftHandAnchorName = "LeftHandAnchor";
        private const string DefaultRigName = "StylizedCharacterLocomotion";
        private const string DefaultRigPartialMatch = "StylizedCharacter";
        private const string HamburgerMenuGoName = "_HamburgerMenu";
        private const string SkyRotatorGoName = "_SkyRotator";
        private const string DirectionalLightGoName = "Directional Light";
        private const string TerrainGoName = "Terrain_SceneA";
        private const string GrabVolumeGoName = "GrabVolume";

        // ---- Shaders ------------------------------------------------------
        private const string TerrainLitShader = "Universal Render Pipeline/Terrain/Lit";
        private const string MissingShaderMarker = "Hidden/InternalErrorShader";

        // ---- CharacterController defaults ---------------------------------
        private const float CcHeight = 1.8f;
        private const float CcRadius = 0.3f;
        private const float CcSkinWidth = 0.08f;
        private const float CcStepOffset = 0.5f;
        private static readonly Vector3 CcCenter = new Vector3(0f, 0.9f, 0f);

        // ---- Locomotion defaults ------------------------------------------
        private const float MoveSpeedDefault = 2.5f;
        private const float StrafeFactorDefault = 0.8f;
        private const float TurnSpeedDefault = 120f;
        private const float TurnDeadZoneDefault = 0.15f;
        private const float SkyRotateSpeedDefault = 0.5f;

        // ---- Player spawn -------------------------------------------------
        private const float SpawnAboveTerrain = 1000f;
        private const float SpawnFallbackY = 1200f;

        // ---- Lights -------------------------------------------------------
        private static readonly Color WarmSunlight = new Color(1f, 0.95f, 0.84f);
        private static readonly Quaternion SunRotation = Quaternion.Euler(50f, -30f, 0f);

        // ---- Grab volume --------------------------------------------------
        private const float GrabVolumeRadius = 0.08f;

        // ---- Logging ------------------------------------------------------
        private const string LOG = "[PLAGA44][Bootstrap]";

        // =====================================================================
        // Auto-run + menu
        // =====================================================================

        static Bootstrap() => EditorApplication.delayCall += AutoRun;

        private static void AutoRun()
        {
            if (SessionState.GetBool(BootstrapSessionKey, false)) return;
            SessionState.SetBool(BootstrapSessionKey, true);

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoRun;
                return;
            }

            Debug.Log($"{LOG} Auto-run: loading scene and validating...");
            LoadAndValidate();
        }

        [MenuItem("CYBERNOMAD/Scene/Load PLAGA44 Demo", false, 1)]
        public static void LoadFromMenu()
        {
            SessionState.SetBool(BootstrapSessionKey, true);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            LoadAndValidate();
        }

        [MenuItem("CYBERNOMAD/Bootstrap", false, 2)]
        public static void RunBootstrap()
        {
            Debug.Log($"{LOG} Manual bootstrap...");
            ValidateScene();
        }

        // =====================================================================
        // Load + validate orchestration
        // =====================================================================

        private static void LoadAndValidate()
        {
            if (!IsTargetSceneActive())
            {
                if (!System.IO.File.Exists(ScenePath))
                {
                    Debug.LogError($"{LOG} Scene not found: {ScenePath}");
                    return;
                }
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log($"{LOG} Scene opened: {ScenePath}");
            }
            EditorApplication.delayCall += ValidateScene;
        }

        private static bool IsTargetSceneActive()
        {
            var active = SceneManager.GetActiveScene();
            return active.IsValid() && active.path == ScenePath;
        }

        private static void ValidateScene()
        {
            bool changed = false;
            changed |= ValidateTerrain();
            changed |= ValidateSkybox();
            changed |= ValidateDirectionalLight();
            changed |= ValidatePlayerRig();
            changed |= ValidateHamburgerMenu();
            changed |= ValidateSkyRotator();
            changed |= ValidateInventorySystem();
            ValidateAvatarRegistry();

            SaveSceneIfDirty(changed);
            FocusCameraOnTerrain();
        }

        private static void SaveSceneIfDirty(bool changed)
        {
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                Debug.Log($"{LOG} === Validation done, scene saved ===");
            }
            else
            {
                Debug.Log($"{LOG} === Validation OK, nothing missing ===");
            }
        }

        private static void FocusCameraOnTerrain()
        {
            var terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) return;
            Selection.activeGameObject = terrain.gameObject;
            try { SceneView.lastActiveSceneView?.FrameSelected(); }
            catch (Exception e) { Debug.LogWarning($"{LOG} FrameSelected failed (Unity internal): {e.GetType().Name}"); }
        }

        // =====================================================================
        // Terrain
        // =====================================================================

        private static bool ValidateTerrain()
        {
            bool changed = false;
            var terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
            if (terrain == null)
            {
                terrain = CreateTerrainFromAsset();
                if (terrain == null) return false;
                changed = true;
            }
            else
            {
                Debug.Log($"{LOG} [OK] Terrain: {terrain.name} ({terrain.terrainData.size})");
            }
            changed |= ValidateTerrainMaterial(terrain);
            return changed;
        }

        private static Terrain CreateTerrainFromAsset()
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainAsset);
            if (data == null)
            {
                Debug.LogError($"{LOG} [MISSING] Scene_A_Terrain.asset not found: {TerrainAsset}");
                return null;
            }

            var terrainGO = Terrain.CreateTerrainGameObject(data);
            terrainGO.name = TerrainGoName;
            terrainGO.transform.position = new Vector3(-data.size.x * 0.5f, 0f, -data.size.z * 0.5f);

            Debug.Log($"{LOG} [ADDED] Terrain: {data.size.x:F0}x{data.size.z:F0}m, centered");
            return terrainGO.GetComponent<Terrain>();
        }

        private static bool ValidateTerrainMaterial(Terrain terrain)
        {
            if (HasValidMaterial(terrain))
            {
                var mat = terrain.materialTemplate;
                Debug.Log($"{LOG} [OK] Terrain material: {mat.name} (shader: {mat.shader.name})");
                return false;
            }

            Debug.LogWarning($"{LOG} [FIX] Terrain material missing or pink");
            var existing = AssetDatabase.LoadAssetAtPath<Material>(TerrainMatPath);
            if (existing != null)
            {
                terrain.materialTemplate = existing;
                Debug.Log($"{LOG} [OK] Assigned existing TerrainLit.mat");
                return true;
            }
            return CreateAndAssignTerrainMaterial(terrain);
        }

        private static bool HasValidMaterial(Terrain terrain)
        {
            var mat = terrain.materialTemplate;
            return mat != null && mat.shader != null && mat.shader.name != MissingShaderMarker;
        }

        private static bool CreateAndAssignTerrainMaterial(Terrain terrain)
        {
            var shader = Shader.Find(TerrainLitShader);
            if (shader == null)
            {
                Debug.LogError($"{LOG} [ERROR] Shader '{TerrainLitShader}' not found!");
                return false;
            }

            EnsureFolder(TerrainMatFolderParent, TerrainMatFolderName);
            var mat = new Material(shader) { name = "TerrainLit" };
            AssetDatabase.CreateAsset(mat, TerrainMatPath);
            AssetDatabase.SaveAssets();
            terrain.materialTemplate = mat;

            Debug.Log($"{LOG} [ADDED] Created and assigned TerrainLit.mat (URP Terrain/Lit)");
            return true;
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string full = $"{parent}/{folderName}";
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, folderName);
        }

        // =====================================================================
        // Skybox + directional light
        // =====================================================================

        private static bool ValidateSkybox()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMatPath);
            if (mat == null)
            {
                Debug.LogWarning($"{LOG} [MISSING] Skybox material not found: {SkyboxMatPath}");
                return false;
            }
            if (RenderSettings.skybox == mat)
            {
                Debug.Log($"{LOG} [OK] Skybox: {mat.name}");
                return false;
            }
            RenderSettings.skybox = mat;
            Debug.Log($"{LOG} [ADDED] Skybox: {mat.name}");
            return true;
        }

        private static bool ValidateDirectionalLight()
        {
            if (FindDirectionalLight() is Light existing)
            {
                Debug.Log($"{LOG} [OK] Directional Light: {existing.name}");
                return false;
            }
            CreateDirectionalLight();
            Debug.Log($"{LOG} [ADDED] Directional Light");
            return true;
        }

        private static Light FindDirectionalLight()
        {
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) return light;
            return null;
        }

        private static void CreateDirectionalLight()
        {
            var go = new GameObject(DirectionalLightGoName);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = WarmSunlight;
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = SunRotation;
        }

        // =====================================================================
        // Player rig (OVRCameraRig + CC + locomotion + avatar)
        // =====================================================================

        private static bool ValidatePlayerRig()
        {
            var rig = GameObject.Find(OvrRigName);
            if (rig == null)
            {
                Debug.LogWarning($"{LOG} [MISSING] {OvrRigName} not found in scene");
                return false;
            }

            bool changed = false;
            changed |= EnsureCharacterController(rig);
            changed |= EnsureLocomotion(rig);
            changed |= EnsureSmoothTurn(rig);
            changed |= EnsurePlayerAvatar(rig);
            PlacePlayerAboveTerrain(rig);
            return changed;
        }

        private static bool EnsureCharacterController(GameObject rig)
        {
            return EnsureComponent<CharacterController>(rig, "CharacterController on OVRCameraRig", cc =>
            {
                cc.height = CcHeight;
                cc.radius = CcRadius;
                cc.center = CcCenter;
                cc.skinWidth = CcSkinWidth;
                cc.stepOffset = CcStepOffset;
            });
        }

        private static bool EnsureLocomotion(GameObject rig)
        {
            return EnsureComponent<LocomotionController>(rig, "LocomotionController on OVRCameraRig", loco =>
            {
                loco.moveSpeed = MoveSpeedDefault;
                loco.strafeFactor = StrafeFactorDefault;
            });
        }

        private static bool EnsureSmoothTurn(GameObject rig)
        {
            return EnsureComponent<SmoothTurnController>(rig, "SmoothTurnController on OVRCameraRig (120 deg/s)", turn =>
            {
                turn.turnSpeed = TurnSpeedDefault;
                turn.deadZone = TurnDeadZoneDefault;
            });
        }

        private static bool EnsurePlayerAvatar(GameObject rig)
        {
            bool changed = EnsureComponent<PlayerAvatar>(rig, "PlayerAvatar", null);
            var avatar = rig.GetComponent<PlayerAvatar>();
            changed |= ResetAvatarToDefaultMode(avatar);
            changed |= ClearLegacyPrefabOverride(avatar);
            changed |= WireDefaultRig(avatar, rig);
            changed |= ActivateDefaultRig(avatar);
            return changed;
        }

        private static bool ResetAvatarToDefaultMode(PlayerAvatar avatar)
        {
            if (avatar.avatarMode == 0) return false;
            avatar.avatarMode = 0;
            Debug.Log($"{LOG} [FIX] PlayerAvatar.avatarMode reset to 0 (None)");
            return true;
        }

        private static bool ClearLegacyPrefabOverride(PlayerAvatar avatar)
        {
            if (avatar.avatarPrefab == null) return false;
            avatar.avatarPrefab = null;
            Debug.Log($"{LOG} [FIX] Cleared PlayerAvatar.avatarPrefab (uses Resources per mode)");
            return true;
        }

        private static bool WireDefaultRig(PlayerAvatar avatar, GameObject rig)
        {
            if (avatar.defaultRig != null) return false;
            var found = GameObject.Find(DefaultRigName) ?? FindChildContaining(rig.transform, DefaultRigPartialMatch);
            if (found == null)
            {
                Debug.LogWarning($"{LOG} [WARN] {DefaultRigName} not found -- assign defaultRig manually in inspector");
                return false;
            }
            avatar.defaultRig = found;
            Debug.Log($"{LOG} [FIX] PlayerAvatar.defaultRig -> {found.name}");
            return true;
        }

        private static bool ActivateDefaultRig(PlayerAvatar avatar)
        {
            if (avatar.defaultRig == null || avatar.defaultRig.activeSelf) return false;
            avatar.defaultRig.SetActive(true);
            Debug.Log($"{LOG} [FIX] defaultRig activated");
            return true;
        }

        private static GameObject FindChildContaining(Transform root, string partialName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name.Contains(partialName)) return t.gameObject;
            return null;
        }

        private static void PlacePlayerAboveTerrain(GameObject rig)
        {
            var terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
            float y = terrain != null ? terrain.terrainData.size.y + SpawnAboveTerrain : SpawnFallbackY;
            rig.transform.position = new Vector3(0f, y, 0f);
            Debug.Log($"{LOG} Player placed at (0, {y}, 0)");
        }

        // =====================================================================
        // Scene singletons (HamburgerMenu, SkyRotator)
        // =====================================================================

        private static bool ValidateHamburgerMenu()
        {
            return EnsureSceneSingleton<HamburgerMenu>(HamburgerMenuGoName, "HamburgerMenu", null);
        }

        private static bool ValidateSkyRotator()
        {
            return EnsureSceneSingleton<SkyRotator>(SkyRotatorGoName, "SkyRotator (0.5 deg/s)",
                sr => sr.rotationSpeed = SkyRotateSpeedDefault);
        }

        // =====================================================================
        // Avatar registry (read-only)
        // =====================================================================

        private static void ValidateAvatarRegistry()
        {
            var reg = AssetDatabase.LoadAssetAtPath<Plaga44.AvatarRegistry>(AvatarRegistryPath);
            if (reg == null)
            {
                Debug.LogWarning($"{LOG} [MISS] AvatarRegistry not found at {AvatarRegistryPath}. Run CYBERNOMAD > Import > Rescan Avatars.");
                return;
            }
            if (reg.Count == 0)
            {
                Debug.LogWarning($"{LOG} [EMPTY] AvatarRegistry has 0 avatars. Drop DAE into Assets/PLAGA44/Avatars/<Name>/ and rescan.");
                return;
            }
            Debug.Log($"{LOG} [OK] AvatarRegistry: {reg.Count} avatars");
            for (int i = 0; i < reg.Count; i++)
            {
                var e = reg.Get(i);
                string status = (e != null && e.prefab != null) ? "OK" : "MISSING";
                string name = e != null ? e.name : "?";
                Debug.Log($"{LOG}   [{i}] {name} -- {status}");
            }
        }

        // =====================================================================
        // Inventory / haptic / grab
        // =====================================================================

        private static bool ValidateInventorySystem()
        {
            if (!RevolverPrefabBuilder.EnsurePrefab())
                Debug.LogWarning($"{LOG} [WARN] Revolver prefab missing -- loadout will fail.");

            var rig = GameObject.Find(OvrRigName);
            if (rig == null)
            {
                Debug.LogWarning($"{LOG} [MISSING] {OvrRigName} not found -- skipping inventory setup");
                return false;
            }

            bool changed = false;
            changed |= EnsureComponent<HapticManager>(rig, "HapticManager", null);
            changed |= EnsureComponent<PlayerInventory>(rig, "PlayerInventory", null);
            changed |= EnsureComponent<InventoryLoadout>(rig, "InventoryLoadout (RightHip=Revolver)", null);
            changed |= EnsureGrabberOnHand(rig, RightHandAnchorName, OVRInput.Controller.RTouch);
            changed |= EnsureGrabberOnHand(rig, LeftHandAnchorName, OVRInput.Controller.LTouch);
            return changed;
        }

        private static bool EnsureGrabberOnHand(GameObject rig, string anchorName, OVRInput.Controller ctrl)
        {
            var anchor = FindChildByName(rig.transform, anchorName);
            if (anchor == null)
            {
                Debug.LogWarning($"{LOG} [MISSING] {anchorName} on {OvrRigName} -- grabber not added");
                return false;
            }
            if (anchor.GetComponent<OVRGrabber>() != null)
            {
                Debug.Log($"{LOG} [OK] OVRGrabber on {anchorName}");
                return false;
            }

            var grabVolume = CreateGrabVolume(anchor);
            EnsureKinematicRigidbody(anchor.gameObject);
            var grabber = anchor.gameObject.AddComponent<OVRGrabber>();
            ConfigureOVRGrabber(grabber, anchor, grabVolume, ctrl);

            Debug.Log($"{LOG} [ADDED] OVRGrabber on {anchorName} ({ctrl})");
            return true;
        }

        private static SphereCollider CreateGrabVolume(Transform parent)
        {
            var go = new GameObject(GrabVolumeGoName);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            var sph = go.AddComponent<SphereCollider>();
            sph.isTrigger = true;
            sph.radius = GrabVolumeRadius;
            return sph;
        }

        private static void EnsureKinematicRigidbody(GameObject go)
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) return;
            rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // OVRGrabber fields are protected -- reflection. Logs [SDK BREAK] if Oculus renames a field.
        private static void ConfigureOVRGrabber(OVRGrabber grabber, Transform gripXform, Collider volume, OVRInput.Controller ctrl)
        {
            var t = typeof(OVRGrabber);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            SetPrivateField(t, grabber, "m_gripTransform", gripXform, flags);
            SetPrivateField(t, grabber, "m_grabVolumes", new Collider[] { volume }, flags);
            SetPrivateField(t, grabber, "m_controller", ctrl, flags);
            SetPrivateField(t, grabber, "m_parentHeldObject", true, flags);
        }

        private static void SetPrivateField(Type t, object target, string fieldName, object value, BindingFlags flags)
        {
            var f = t.GetField(fieldName, flags);
            if (f == null)
            {
                Debug.LogError($"{LOG} [SDK BREAK] {t.Name}.{fieldName} not found -- Oculus SDK likely renamed this field. Update ConfigureOVRGrabber.");
                return;
            }
            f.SetValue(target, value);
        }

        // =====================================================================
        // Generic helpers
        // =====================================================================

        /// <summary>Adds component of type T if missing. Runs optional configure(comp) when added.</summary>
        private static bool EnsureComponent<T>(GameObject go, string label, Action<T> configure) where T : Component
        {
            if (go.GetComponent<T>() != null)
            {
                Debug.Log($"{LOG} [OK] {label}");
                return false;
            }
            var comp = go.AddComponent<T>();
            configure?.Invoke(comp);
            Debug.Log($"{LOG} [ADDED] {label}");
            return true;
        }

        /// <summary>Finds component T in scene, otherwise creates new GameObject with given name + T.</summary>
        private static bool EnsureSceneSingleton<T>(string goName, string label, Action<T> configure) where T : Component
        {
            if (UnityEngine.Object.FindAnyObjectByType<T>() != null)
            {
                Debug.Log($"{LOG} [OK] {label}");
                return false;
            }
            var go = new GameObject(goName);
            var comp = go.AddComponent<T>();
            configure?.Invoke(comp);
            Debug.Log($"{LOG} [ADDED] {label}");
            return true;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
#endif
