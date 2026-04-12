using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Plaga44.Editor
{
    [InitializeOnLoad]
    public static class Bootstrap
    {
        private const string ScenePath = "Assets/PLAGA44/TESTBED_V6.unity";
        private const string BootstrapKey = "Plaga44.Bootstrap.Done";
        private const string LOG = "[Plaga44.Bootstrap]";

        static Bootstrap()
        {
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (SessionState.GetBool(BootstrapKey, false)) return;
            SessionState.SetBool(BootstrapKey, true);

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Run;
                return;
            }

            // Otworz scene
            var active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.path.Contains("TESTBED_V6"))
            {
                if (System.IO.File.Exists(ScenePath))
                {
                    Debug.Log($"{LOG} Opening TESTBED_V6");
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
            }

            EditorApplication.delayCall += ConfigureScene;
        }

        private static void ConfigureScene()
        {
            var rig = GameObject.Find("OVRCameraRig");
            if (rig == null)
            {
                Debug.LogWarning($"{LOG} OVRCameraRig not found");
                return;
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
                Debug.Log($"{LOG} Added CharacterController");
            }

            // LocomotionController
            var loco = rig.GetComponent<Plaga44.Locomotion.LocomotionController>();
            if (loco == null)
            {
                loco = rig.AddComponent<Plaga44.Locomotion.LocomotionController>();
                loco.moveSpeed = 2.5f;
                loco.strafeFactor = 0.8f;
                changed = true;
                Debug.Log($"{LOG} Added LocomotionController");
            }

            // Teren Potok
            if (Object.FindFirstObjectByType<Terrain>() == null)
            {
                changed |= PlaceTerrain();
            }

            // Spawn gracza nad terenem
            if (changed)
            {
                rig.transform.position = new Vector3(0f, 200f, 0f);
                Debug.Log($"{LOG} Player spawned at (0, 200, 0) -- will fall onto terrain");
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                Debug.Log($"{LOG} Scene configured and saved");
            }
        }

        private static bool PlaceTerrain()
        {
            const string TERRAIN_ASSET = "Assets/Potok/Terrain/Tile_0.asset";
            const string MOSS_LAYER = "Assets/Potok/TerrainLayers/layer_GR_Moss1_ASGR_Moss1_N3d1cca0d6d0e9938.terrainlayer";
            const string SKYBOX_MAT = "Assets/Potok/Skybox/BGR_Sky1.mat";

            var terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(TERRAIN_ASSET);
            if (terrainData == null)
            {
                Debug.LogWarning($"{LOG} Tile_0.asset not found -- no terrain");
                return false;
            }

            // Wyczysc brakujace drzewa/detale
            terrainData.treeInstances = new TreeInstance[0];
            terrainData.treePrototypes = new TreePrototype[0];
            terrainData.detailPrototypes = new DetailPrototype[0];

            // Skaluj teren
            terrainData.size = new Vector3(512f * 20f, 600f * 20f, 512f * 20f);

            // Warstwa mchu
            var mossLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(MOSS_LAYER);
            if (mossLayer != null)
            {
                mossLayer.tileSize = new Vector2(5f, 5f);
                EditorUtility.SetDirty(mossLayer);
                terrainData.terrainLayers = new TerrainLayer[] { mossLayer };
            }

            EditorUtility.SetDirty(terrainData);

            // Stworz terrain GO, wycentruj
            var terrainGO = Terrain.CreateTerrainGameObject(terrainData);
            terrainGO.name = "Terrain_Potok";
            float halfX = terrainData.size.x * 0.5f;
            float halfZ = terrainData.size.z * 0.5f;
            terrainGO.transform.position = new Vector3(-halfX, 0f, -halfZ);

            Debug.Log($"{LOG} Terrain placed: {terrainData.size.x:F0}x{terrainData.size.z:F0}m");

            // Skybox
            var skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(SKYBOX_MAT);
            if (skyboxMat != null)
            {
                skyboxMat.SetFloat("_CloudOpacity", 0f);
                EditorUtility.SetDirty(skyboxMat);
                RenderSettings.skybox = skyboxMat;
                Debug.Log($"{LOG} Skybox set");
            }

            // Fog
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.02f);
            RenderSettings.fogStartDistance = 50f;
            RenderSettings.fogEndDistance = 300f;

            return true;
        }
    }
}
