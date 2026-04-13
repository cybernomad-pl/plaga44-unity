#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace Plaga44.Editor
{
    /// <summary>
    /// Laduje scene PLAGA44 Demo, waliduje i naprawia brakujace elementy.
    /// Odpala sie automatycznie po otwarciu projektu w edytorze
    /// oraz dostepne z menu CYBERNOMAD > Scene > Load PLAGA44 Demo.
    /// </summary>
    [InitializeOnLoad]
    public static class Bootstrap
    {
        private const string ScenePath = "Assets/PLAGA44/TESTBED_V6.unity";
        private const string TerrainAsset = "Assets/Potok/Terrain/Scene_A_Terrain.asset";
        private const string TerrainMatPath = "Assets/PLAGA44/Materials/TerrainLit.mat";
        private const string SkyboxMat = "Assets/Potok/Skybox/BGR_Sky1.mat";
        private const string BootstrapKey = "Plaga44.OpenScene.Done";
        private const string LOG = "[PLAGA44][OpenScene]";

        // =====================================================================
        // Auto-run po starcie edytora
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

            Debug.Log($"{LOG} Auto-run: ladowanie sceny i walidacja...");
            LoadAndValidate();
        }

        // =====================================================================
        // Menu item
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
            Debug.Log($"{LOG} Reczny Bootstrap...");
            ValidateScene();
        }

        // =====================================================================
        // Glowna metoda: wczytaj + waliduj + napraw
        // =====================================================================

        private static void LoadAndValidate()
        {
            // --- 1. Otworz scene jesli nie jest otwarta ---
            var active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.path.Contains("TESTBED_V6"))
            {
                if (!System.IO.File.Exists(ScenePath))
                {
                    Debug.LogError($"{LOG} Scena nie istnieje: {ScenePath}");
                    return;
                }
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log($"{LOG} Scena otwarta: {ScenePath}");
            }

            // Daj edytorowi chwile na zaladowanie sceny
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

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                Debug.Log($"{LOG} === Walidacja zakonczona, scena zapisana ===");
            }
            else
            {
                Debug.Log($"{LOG} === Walidacja OK, nic nie brakuje ===");
            }

            // Fokus na teren w SceneView
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain != null)
            {
                Selection.activeGameObject = terrain.gameObject;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        }

        // =====================================================================
        // Walidatory -- kazdy sprawdza jeden element sceny
        // =====================================================================

        /// <summary>
        /// Sprawdza czy teren istnieje. Jesli nie -- tworzy z Scene_A_Terrain.asset.
        /// Sprawdza tez material -- jesli brakuje lub rozowy, ustawia URP Terrain/Lit.
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
                    Debug.LogError($"{LOG} [BRAK] Scene_A_Terrain.asset nie znaleziony: {TerrainAsset}");
                    return false;
                }

                var terrainGO = Terrain.CreateTerrainGameObject(terrainData);
                terrainGO.name = "Terrain_SceneA";

                float halfX = terrainData.size.x * 0.5f;
                float halfZ = terrainData.size.z * 0.5f;
                terrainGO.transform.position = new Vector3(-halfX, 0f, -halfZ);

                existing = terrainGO.GetComponent<Terrain>();
                changed = true;
                Debug.Log($"{LOG} [DODANO] Teren: {terrainData.size.x:F0}x{terrainData.size.z:F0}m, wycentrowany");
            }
            else
            {
                Debug.Log($"{LOG} [OK] Teren: {existing.name} ({existing.terrainData.size})");
            }

            // Walidacja materialu -- rozowy = brakujacy shader/material
            changed |= ValidateTerrainMaterial(existing);
            return changed;
        }

        /// <summary>
        /// Sprawdza material terenu. Jesli null lub uzywa brakujacego shadera
        /// (rozowy = "Hidden/InternalErrorShader") -- tworzy URP Terrain/Lit.
        /// </summary>
        private static bool ValidateTerrainMaterial(Terrain terrain)
        {
            var mat = terrain.materialTemplate;
            if (mat != null && mat.shader != null && mat.shader.name != "Hidden/InternalErrorShader")
            {
                Debug.Log($"{LOG} [OK] Terrain material: {mat.name} (shader: {mat.shader.name})");
                return false;
            }

            Debug.LogWarning($"{LOG} [NAPRAWIAM] Terrain material brakujacy lub rozowy");

            // Sprobuj zaladowac istniejacy material
            var existingMat = AssetDatabase.LoadAssetAtPath<Material>(TerrainMatPath);
            if (existingMat != null)
            {
                terrain.materialTemplate = existingMat;
                Debug.Log($"{LOG} [OK] Przypisano istniejacy TerrainLit.mat");
                return true;
            }

            // Stworz nowy URP Terrain/Lit material
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader == null)
            {
                Debug.LogError($"{LOG} [BLAD] Shader 'Universal Render Pipeline/Terrain/Lit' nie znaleziony!");
                return false;
            }

            var newMat = new Material(shader);
            newMat.name = "TerrainLit";

            // Upewnij sie ze folder istnieje
            if (!AssetDatabase.IsValidFolder("Assets/PLAGA44/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/PLAGA44", "Materials");
            }

            AssetDatabase.CreateAsset(newMat, TerrainMatPath);
            AssetDatabase.SaveAssets();

            terrain.materialTemplate = newMat;
            Debug.Log($"{LOG} [DODANO] Stworzono i przypisano TerrainLit.mat (URP Terrain/Lit)");
            return true;
        }

        /// <summary>
        /// Sprawdza czy skybox jest ustawiony. Jesli nie -- ustawia BGR_Sky1.
        /// </summary>
        private static bool ValidateSkybox()
        {
            var skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMat);
            if (skyboxMat == null)
            {
                Debug.LogWarning($"{LOG} [BRAK] Skybox material nie znaleziony: {SkyboxMat}");
                return false;
            }

            if (RenderSettings.skybox == skyboxMat)
            {
                Debug.Log($"{LOG} [OK] Skybox: {skyboxMat.name}");
                return false;
            }

            RenderSettings.skybox = skyboxMat;
            Debug.Log($"{LOG} [DODANO] Skybox: {skyboxMat.name}");
            return true;
        }

        /// <summary>
        /// Sprawdza czy jest Directional Light. Jesli nie -- tworzy.
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
            lightComp.color = new Color(1f, 0.95f, 0.84f); // ciepla barwa slonca
            lightComp.intensity = 1f;
            lightComp.shadows = LightShadows.Soft;

            // Slonce pod katem -- typowe oswietlenie terenu
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Debug.Log($"{LOG} [DODANO] Directional Light");
            return true;
        }

        /// <summary>
        /// Sprawdza OVRCameraRig: CharacterController + LocomotionController.
        /// Jesli brakuje komponentow -- dodaje.
        /// </summary>
        private static bool ValidatePlayerRig()
        {
            var rig = GameObject.Find("OVRCameraRig");
            if (rig == null)
            {
                Debug.LogWarning($"{LOG} [BRAK] OVRCameraRig nie znaleziony w scenie");
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
                Debug.Log($"{LOG} [DODANO] CharacterController na OVRCameraRig");
            }
            else
            {
                Debug.Log($"{LOG} [OK] CharacterController na OVRCameraRig");
            }

            // LocomotionController
            var loco = rig.GetComponent<Plaga44.Locomotion.LocomotionController>();
            if (loco == null)
            {
                loco = rig.AddComponent<Plaga44.Locomotion.LocomotionController>();
                loco.moveSpeed = 2.5f;
                loco.strafeFactor = 0.8f;
                changed = true;
                Debug.Log($"{LOG} [DODANO] LocomotionController na OVRCameraRig");
            }
            else
            {
                Debug.Log($"{LOG} [OK] LocomotionController na OVRCameraRig");
            }

            // Spawn gracza nad terenem jesli cos sie zmienilo
            if (changed)
            {
                var terrain = Object.FindFirstObjectByType<Terrain>();
                float spawnY = terrain != null ? terrain.terrainData.size.y + 10f : 200f;
                rig.transform.position = new Vector3(0f, spawnY, 0f);
                Debug.Log($"{LOG} Gracz ustawiony na (0, {spawnY}, 0)");
            }

            return changed;
        }

        /// <summary>
        /// Sprawdza czy HamburgerMenu jest na scenie. Jesli nie -- tworzy GO z komponentem.
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
            Debug.Log($"{LOG} [DODANO] HamburgerMenu");
            return true;
        }
    }
}
#endif
