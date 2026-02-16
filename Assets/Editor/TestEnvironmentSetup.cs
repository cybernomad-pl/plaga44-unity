#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class TestEnvironmentSetup
    {
        private const string LOG = "[PLAGA44]";
        private const float ROOM_SIZE = 5f;
        private const float WALL_HEIGHT = 3f;
        private const float WALL_THICKNESS = 0.1f;

        [MenuItem("CYBERNOMAD/Scene Setup/Create Test Room", false, 100)]
        public static void CreateTestRoom()
        {
            Debug.Log($"{LOG} Creating test room environment...");

            GameObject roomParent = new GameObject("TestRoom");
            Undo.RegisterCreatedObjectUndo(roomParent, "Create Test Room");

            CreateFloor(roomParent.transform);
            CreateWalls(roomParent.transform);
            CreateCeiling(roomParent.transform);
            CreateLighting();

            Selection.activeGameObject = roomParent;
            Debug.Log($"{LOG} Test room created successfully.");
        }

        private static void CreateFloor(Transform parent)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(parent);
            floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(ROOM_SIZE, 0.1f, ROOM_SIZE);

            SetMaterial(floor, new Color(0.3f, 0.3f, 0.35f));
            Undo.RegisterCreatedObjectUndo(floor, "Create Floor");
        }

        private static void CreateWalls(Transform parent)
        {
            CreateWall(parent, "WallNorth", new Vector3(0f, WALL_HEIGHT / 2f, ROOM_SIZE / 2f), new Vector3(ROOM_SIZE, WALL_HEIGHT, WALL_THICKNESS));
            CreateWall(parent, "WallSouth", new Vector3(0f, WALL_HEIGHT / 2f, -ROOM_SIZE / 2f), new Vector3(ROOM_SIZE, WALL_HEIGHT, WALL_THICKNESS));
            CreateWall(parent, "WallEast", new Vector3(ROOM_SIZE / 2f, WALL_HEIGHT / 2f, 0f), new Vector3(WALL_THICKNESS, WALL_HEIGHT, ROOM_SIZE));
            CreateWall(parent, "WallWest", new Vector3(-ROOM_SIZE / 2f, WALL_HEIGHT / 2f, 0f), new Vector3(WALL_THICKNESS, WALL_HEIGHT, ROOM_SIZE));
        }

        private static void CreateWall(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent);
            wall.transform.localPosition = position;
            wall.transform.localScale = scale;

            SetMaterial(wall, new Color(0.4f, 0.35f, 0.3f));
            Undo.RegisterCreatedObjectUndo(wall, $"Create {name}");
        }

        private static void CreateCeiling(Transform parent)
        {
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(parent);
            ceiling.transform.localPosition = new Vector3(0f, WALL_HEIGHT + 0.05f, 0f);
            ceiling.transform.localScale = new Vector3(ROOM_SIZE, 0.1f, ROOM_SIZE);

            SetMaterial(ceiling, new Color(0.5f, 0.5f, 0.5f));
            Undo.RegisterCreatedObjectUndo(ceiling, "Create Ceiling");
        }

        private static void CreateLighting()
        {
            Light[] existingLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            if (existingLights.Length > 0)
            {
                Debug.Log($"{LOG} Lighting already exists in scene, skipping.");
                return;
            }

            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1f;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Undo.RegisterCreatedObjectUndo(lightObj, "Create Directional Light");
        }

        [MenuItem("CYBERNOMAD/Scene Setup/Add Locomotion", false, 102)]
        public static void AddLocomotion()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name == "OVRPlayerController")
                {
                    Debug.Log($"{LOG} OVRPlayerController already in scene.");
                    return;
                }
            }

            GameObject existingRig = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name == "OVRCameraRig")
                {
                    existingRig = t.gameObject;
                    break;
                }
            }

            if (existingRig == null)
            {
                Debug.LogError($"{LOG} OVRCameraRig not found. Add it first (Step 3).");
                return;
            }

            // Wrap existing rig -- don't destroy it
            Vector3 pos = existingRig.transform.position;
            Quaternion rot = existingRig.transform.rotation;

            GameObject wrapper = new GameObject("OVRPlayerController");
            wrapper.transform.position = pos;
            wrapper.transform.rotation = rot;
            Undo.RegisterCreatedObjectUndo(wrapper, "Add OVRPlayerController");

            var cc = Undo.AddComponent<CharacterController>(wrapper);
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            // OVRPlayerController expects OVRCameraRig as child
            Undo.SetTransformParent(existingRig.transform, wrapper.transform, "Reparent OVRCameraRig");
            existingRig.transform.localPosition = Vector3.zero;
            existingRig.transform.localRotation = Quaternion.identity;

            // Add OVRPlayerController component (searches for child OVRCameraRig)
            Undo.AddComponent(wrapper, typeof(OVRPlayerController));

            Debug.Log($"{LOG} OVRPlayerController wraps existing OVRCameraRig. VRInputDebug + all components preserved.");
        }

        [MenuItem("CYBERNOMAD/Scene Setup/Add Splash Screen", false, 103)]
        public static void AddSplashScreen()
        {
            var existing = Object.FindObjectsByType<SplashScreen>(FindObjectsSortMode.None);
            if (existing.Length > 0)
            {
                Debug.Log($"{LOG} SplashScreen already in scene.");
                return;
            }

            GameObject splashGO = new GameObject("SplashScreen");
            splashGO.AddComponent<SplashScreen>();
            Undo.RegisterCreatedObjectUndo(splashGO, "Add Splash Screen");

            Debug.Log($"{LOG} SplashScreen added. Black screen + PLAGA '44 title, fades on controller input.");
        }

        private static void SetMaterial(GameObject obj, Color color)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = color;
                renderer.sharedMaterial = mat;
            }
        }
    }
}
#endif
