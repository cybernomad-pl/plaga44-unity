// PLAGA '44 -- Editor tool: CYBERNOMAD / Scene / Build FloodedGrounds Showcase
// Scans Assets/FloodedGrounds for all prefabs, places them on a grid in a new scene
// with labels, lighting, floor, and OVRCameraRig (or fallback camera).

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class FloodedGroundsShowcase
{
    private const string LOG = "[FloodedGroundsShowcase] ";
    private const string SCENE_PATH = "Assets/Scenes/FloodedGrounds_Showcase.unity";
    private const string PREFAB_ROOT = "Assets/FloodedGrounds";

    // Grid spacing -- large enough for buildings
    private const float CELL_SIZE = 15f;
    private const int COLUMNS = 12;
    private const float LABEL_HEIGHT = 8f;

    [MenuItem("CYBERNOMAD/Scene/Build FloodedGrounds Showcase", false, 20)]
    public static void Build()
    {
        // 1. Collect all prefabs
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PREFAB_ROOT });
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("FloodedGrounds Showcase",
                "No prefabs found in " + PREFAB_ROOT, "OK");
            return;
        }

        // Sort by path for grouped layout
        var prefabPaths = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p)
            .ToList();

        Debug.Log($"{LOG}Found {prefabPaths.Count} prefabs in {PREFAB_ROOT}");

        // 2. Create or open scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 3. Directional light
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.96f, 0.88f);
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Ambient
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.53f, 0.61f, 0.69f);
        RenderSettings.ambientEquatorColor = new Color(0.42f, 0.45f, 0.47f);
        RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.19f);

        // 4. Ground plane -- large enough for the grid
        int totalRows = Mathf.CeilToInt((float)prefabPaths.Count / COLUMNS);
        float groundW = (COLUMNS + 2) * CELL_SIZE;
        float groundD = (totalRows + 2) * CELL_SIZE;

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = new Vector3(
            (COLUMNS - 1) * CELL_SIZE * 0.5f,
            -0.01f,
            (totalRows - 1) * CELL_SIZE * 0.5f);
        ground.transform.localScale = new Vector3(groundW * 0.1f, 1f, groundD * 0.1f);

        // Dark ground material
        var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard"));
        groundMat.color = new Color(0.18f, 0.2f, 0.15f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;
        AssetDatabase.CreateAsset(groundMat, "Assets/Scenes/FloodedGrounds_Showcase_GroundMat.mat");

        // 5. Camera rig -- try OVRCameraRig first
        bool ovrFound = false;
        string[] ovrGuids = AssetDatabase.FindAssets("OVRCameraRig t:Prefab");
        if (ovrGuids.Length > 0)
        {
            string ovrPath = AssetDatabase.GUIDToAssetPath(ovrGuids[0]);
            var ovrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ovrPath);
            if (ovrPrefab != null)
            {
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(ovrPrefab);
                rig.name = "OVRCameraRig";
                rig.transform.position = new Vector3(-5f, 1.7f, -5f);
                ovrFound = true;
                Debug.Log($"{LOG}Placed OVRCameraRig from {ovrPath}");
            }
        }

        if (!ovrFound)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 70f;
            camGo.transform.position = new Vector3(-5f, 3f, -5f);
            camGo.transform.rotation = Quaternion.Euler(10f, 30f, 0f);
            Debug.Log($"{LOG}No OVRCameraRig found -- using fallback camera");
        }

        // 6. Place prefabs on grid, grouped by category
        var rootParent = new GameObject("--- FloodedGrounds Prefabs ---");
        string currentCategory = "";
        GameObject categoryParent = null;
        int placed = 0;

        for (int i = 0; i < prefabPaths.Count; i++)
        {
            string path = prefabPaths[i];
            string relativePath = path.Replace(PREFAB_ROOT + "/", "");
            string category = Path.GetDirectoryName(relativePath).Replace("\\", "/");

            // Category parent
            if (category != currentCategory)
            {
                currentCategory = category;
                categoryParent = new GameObject($"[{category}]");
                categoryParent.transform.SetParent(rootParent.transform);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"{LOG}Could not load: {path}");
                continue;
            }

            int col = placed % COLUMNS;
            int row = placed / COLUMNS;
            Vector3 pos = new Vector3(col * CELL_SIZE, 0f, row * CELL_SIZE);

            // Instantiate prefab
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = pos;
            instance.transform.SetParent(categoryParent.transform);

            // 3D text label above
            var labelGo = new GameObject($"Label_{instance.name}");
            labelGo.transform.SetParent(categoryParent.transform);
            labelGo.transform.position = pos + Vector3.up * LABEL_HEIGHT;

            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = Path.GetFileNameWithoutExtension(path);
            tm.fontSize = 32;
            tm.characterSize = 0.15f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;

            // Billboard -- face camera roughly
            labelGo.transform.rotation = Quaternion.identity;

            placed++;

            // Progress
            if (placed % 50 == 0)
            {
                EditorUtility.DisplayProgressBar("FloodedGrounds Showcase",
                    $"Placing prefabs... {placed}/{prefabPaths.Count}",
                    (float)placed / prefabPaths.Count);
            }
        }

        EditorUtility.ClearProgressBar();

        // 7. Category header signs at row starts (optional visual aid)
        Debug.Log($"{LOG}Placed {placed} prefabs in {totalRows} rows x {COLUMNS} columns");

        // 8. Save scene
        string sceneDir = Path.GetDirectoryName(SCENE_PATH);
        if (!AssetDatabase.IsValidFolder(sceneDir))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        AssetDatabase.Refresh();

        Debug.Log($"{LOG}Scene saved: {SCENE_PATH}");
        EditorUtility.DisplayDialog("FloodedGrounds Showcase",
            $"Done! {placed} prefabs placed.\nScene: {SCENE_PATH}",
            "OK");
    }
}
