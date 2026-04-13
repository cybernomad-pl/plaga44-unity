#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace Plaga44.Editor
{
    /// <summary>
    /// Loads PLAGA44 Demo scene, validates and fixes missing elements.
    /// Runs automatically on editor open (InitializeOnLoad).
    /// Also available from menu: CYBERNOMAD > Scene > Load PLAGA44 Demo.
    /// </summary>
    [InitializeOnLoad]
    public static class Bootstrap
    {
        private const string ScenePath = "Assets/PLAGA44/TESTBED.unity";
        private const string TerrainAsset = "Assets/Potok/Terrain/Scene_A_Terrain.asset";
        private const string TerrainMatPath = "Assets/PLAGA44/Materials/TerrainLit.mat";
        private const string SkyboxMat = "Assets/Potok/Skybox/BGR_Sky1.mat";
        private const string BootstrapKey = "Plaga44.OpenScene.Done";
        private const string LOG = "[PLAGA44][Bootstrap]";

        // =====================================================================
        // Auto-run on editor start
        // =====================================================================

        static Bootstrap()
        {
            EditorApplication.delayCall += AutoRun;
        }

        private static void AutoRun()
        {
            if (SessionState.GetBool(BootstrapKey, false)) return;
            SessionState.SetBool(BootstrapKey, true);

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoRun;
                return;
            }

            Debug.Log($"{LOG} Auto-run: loading scene and validating...");
            LoadAndValidate();
        }

        // =====================================================================
        // Menu items
        // =====================================================================

        [MenuItem("CYBERNOMAD/Scene/Load PLAGA44 Demo", false, 1)]
        public static void LoadFromMenu()
        {
            SessionState.SetBool(BootstrapKey, true);
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
        // Main: load + validate + fix
        // =====================================================================

        private static void LoadAndValidate()
        {
            var active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.path.Contains("TESTBED"))
            {
                if (!System.IO.File.Exists(ScenePath))
                {
                    Debug.LogError($"{LOG} Scene not found: {ScenePath}");
                    return;
                }
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log($"{LOG} Scene opened: {ScenePath}");
            }

            // Give editor a frame to finish loading
            EditorApplication.delayCall += ValidateScene;
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

            // Focus on terrain in SceneView
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain != null)
            {
                Selection.activeGameObject = terrain.gameObject;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        }

        // =====================================================================
        // Validators -- each checks one scene element
        // =====================================================================

        /// <summary>
        /// Checks if terrain exists. If not -- creates from Scene_A_Terrain.asset.
        /// Also checks material -- if missing or pink, assigns URP Terrain/Lit.
        /// </summary>
        private static bool ValidateTerrain()
        {
            bool changed = false;
            var existing = Object.FindFirstObjectByType<Terrain>();

            if (existing == null)
            {
                var terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainAsset);
                if (terrainData == null)
                {
                    Debug.LogError($"{LOG} [MISSING] Scene_A_Terrain.asset not found: {TerrainAsset}");
                    return false;
                }

                var terrainGO = Terrain.CreateTerrainGameObject(terrainData);
                terrainGO.name = "Terrain_SceneA";

                float halfX = terrainData.size.x * 0.5f;
                float halfZ = terrainData.size.z * 0.5f;
                terrainGO.transform.position = new Vector3(-halfX, 0f, -halfZ);

                existing = terrainGO.GetComponent<Terrain>();
                changed = true;
                Debug.Log($"{LOG} [ADDED] Terrain: {terrainData.size.x:F0}x{terrainData.size.z:F0}m, centered");
            }
            else
            {
                Debug.Log($"{LOG} [OK] Terrain: {existing.name} ({existing.terrainData.size})");
            }

            // Validate material -- pink = missing shader/material
            changed |= ValidateTerrainMaterial(existing);
            return changed;
        }

        /// <summary>
        /// Checks terrain material. If null or using missing shader
        /// (pink = "Hidden/InternalErrorShader") -- creates URP Terrain/Lit.
        /// </summary>
        private static bool ValidateTerrainMaterial(Terrain terrain)
        {
            var mat = terrain.materialTemplate;
            if (mat != null && mat.shader != null && mat.shader.name != "Hidden/InternalErrorShader")
            {
                Debug.Log($"{LOG} [OK] Terrain material: {mat.name} (shader: {mat.shader.name})");
                return false;
            }

            Debug.LogWarning($"{LOG} [FIX] Terrain material missing or pink");

            var existingMat = AssetDatabase.LoadAssetAtPath<Material>(TerrainMatPath);
            if (existingMat != null)
            {
                terrain.materialTemplate = existingMat;
                Debug.Log($"{LOG} [OK] Assigned existing TerrainLit.mat");
                return true;
            }

            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader == null)
            {
                Debug.LogError($"{LOG} [ERROR] Shader 'Universal Render Pipeline/Terrain/Lit' not found!");
                return false;
            }

            var newMat = new Material(shader);
            newMat.name = "TerrainLit";

            if (!AssetDatabase.IsValidFolder("Assets/PLAGA44/Materials"))
                AssetDatabase.CreateFolder("Assets/PLAGA44", "Materials");

            AssetDatabase.CreateAsset(newMat, TerrainMatPath);
            AssetDatabase.SaveAssets();

            terrain.materialTemplate = newMat;
            Debug.Log($"{LOG} [ADDED] Created and assigned TerrainLit.mat (URP Terrain/Lit)");
            return true;
        }

        /// <summary>
        /// Checks if skybox is set. If not -- assigns BGR_Sky1.
        /// </summary>
        private static bool ValidateSkybox()
        {
            var skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMat);
            if (skyboxMat == null)
            {
                Debug.LogWarning($"{LOG} [MISSING] Skybox material not found: {SkyboxMat}");
                return false;
            }

            if (RenderSettings.skybox == skyboxMat)
            {
                Debug.Log($"{LOG} [OK] Skybox: {skyboxMat.name}");
                return false;
            }

            RenderSettings.skybox = skyboxMat;
            Debug.Log($"{LOG} [ADDED] Skybox: {skyboxMat.name}");
            return true;
        }

        /// <summary>
        /// Checks for Directional Light. If missing -- creates one.
        /// </summary>
        private static bool ValidateDirectionalLight()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    Debug.Log($"{LOG} [OK] Directional Light: {light.name}");
                    return false;
                }
            }

            var lightGO = new GameObject("Directional Light");
            var lightComp = lightGO.AddComponent<Light>();
            lightComp.type = LightType.Directional;
            lightComp.color = new Color(1f, 0.95f, 0.84f); // warm sunlight
            lightComp.intensity = 1f;
            lightComp.shadows = LightShadows.Soft;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Debug.Log($"{LOG} [ADDED] Directional Light");
            return true;
        }

        /// <summary>
        /// Validates OVRCameraRig: CharacterController + LocomotionController + SmoothTurnController.
        /// Adds missing components.
        /// </summary>
        private static bool ValidatePlayerRig()
        {
            var rig = GameObject.Find("OVRCameraRig");
            if (rig == null)
            {
                Debug.LogWarning($"{LOG} [MISSING] OVRCameraRig not found in scene");
                return false;
            }

            bool changed = false;

            // CharacterController
            var cc = rig.GetComponent<CharacterController>();
            if (cc == null)
            {
                cc = rig.AddComponent<CharacterController>();
                cc.height = 1.8f;
                cc.radius = 0.3f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                cc.skinWidth = 0.08f;
                cc.stepOffset = 0.5f;
                changed = true;
                Debug.Log($"{LOG} [ADDED] CharacterController on OVRCameraRig");
            }
            else
            {
                Debug.Log($"{LOG} [OK] CharacterController on OVRCameraRig");
            }

            // LocomotionController
            var loco = rig.GetComponent<Plaga44.Locomotion.LocomotionController>();
            if (loco == null)
            {
                loco = rig.AddComponent<Plaga44.Locomotion.LocomotionController>();
                loco.moveSpeed = 2.5f;
                loco.strafeFactor = 0.8f;
                changed = true;
                Debug.Log($"{LOG} [ADDED] LocomotionController on OVRCameraRig");
            }
            else
            {
                Debug.Log($"{LOG} [OK] LocomotionController on OVRCameraRig");
            }

            // SmoothTurnController
            var turn = rig.GetComponent<Plaga44.Locomotion.SmoothTurnController>();
            if (turn == null)
            {
                turn = rig.AddComponent<Plaga44.Locomotion.SmoothTurnController>();
                turn.turnSpeed = 120f;
                turn.deadZone = 0.15f;
                changed = true;
                Debug.Log($"{LOG} [ADDED] SmoothTurnController on OVRCameraRig (120 deg/s)");
            }
            else
            {
                Debug.Log($"{LOG} [OK] SmoothTurnController on OVRCameraRig");
            }

            // Spawn player above terrain if something changed
            if (changed)
            {
                var terrain = Object.FindFirstObjectByType<Terrain>();
                float spawnY = terrain != null ? terrain.terrainData.size.y + 1000f : 1200f;
                rig.transform.position = new Vector3(0f, spawnY, 0f);
                Debug.Log($"{LOG} Player placed at (0, {spawnY}, 0)");
            }

            return changed;
        }

        /// <summary>
        /// Checks if HamburgerMenu exists in scene. If not -- creates GO with component.
        /// </summary>
        private static bool ValidateHamburgerMenu()
        {
            if (Object.FindAnyObjectByType<Plaga44.UI.HamburgerMenu>() != null)
            {
                Debug.Log($"{LOG} [OK] HamburgerMenu");
                return false;
            }

            var menuGO = new GameObject("_HamburgerMenu");
            menuGO.AddComponent<Plaga44.UI.HamburgerMenu>();
            Debug.Log($"{LOG} [ADDED] HamburgerMenu");
            return true;
        }

        private static bool ValidateSkyRotator()
        {
            if (Object.FindAnyObjectByType<Plaga44.SkyRotator>() != null)
            {
                Debug.Log($"{LOG} [OK] SkyRotator");
                return false;
            }

            var go = new GameObject("_SkyRotator");
            var sr = go.AddComponent<Plaga44.SkyRotator>();
            sr.rotationSpeed = 0.5f;
            Debug.Log($"{LOG} [ADDED] SkyRotator (0.5 deg/s)");
            return true;
        }
    }
}
#endif
