#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    public static class InteractableSetup
    {
        private const string LOG = "[PLAGA44]";

        [MenuItem("CYBERNOMAD/Scene Setup/Add Test Interactables", false, 101)]
        public static void AddTestInteractables()
        {
            Debug.Log($"{LOG} Adding test interactables...");

            GameObject interactablesParent = new GameObject("TestInteractables");
            Undo.RegisterCreatedObjectUndo(interactablesParent, "Create Test Interactables");

            CreateGrabbableCube(interactablesParent.transform);
            CreateGrabbableSphere(interactablesParent.transform);
            CreatePokeableCube(interactablesParent.transform);

            Selection.activeGameObject = interactablesParent;
            Debug.Log($"{LOG} Test interactables created. Add Meta XR Interaction components manually via:");
            Debug.Log($"{LOG}   - Grabbables: Add HandGrabInteractable + Rigidbody");
            Debug.Log($"{LOG}   - Pokeable: Add PokeInteractable + collider + surface");
            Debug.Log($"{LOG} Or use Meta XR Building Blocks / Quick Actions in Unity Editor.");
        }

        private static void CreateGrabbableCube(Transform parent)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "GrabbableCube";
            cube.transform.SetParent(parent);
            cube.transform.localPosition = new Vector3(-0.5f, 1.2f, 1.0f);
            cube.transform.localScale = Vector3.one * 0.2f;

            SetMaterial(cube, new Color(0.8f, 0.3f, 0.3f));
            AddPhysics(cube);

            Undo.RegisterCreatedObjectUndo(cube, "Create Grabbable Cube");
        }

        private static void CreateGrabbableSphere(Transform parent)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "GrabbableSphere";
            sphere.transform.SetParent(parent);
            sphere.transform.localPosition = new Vector3(0.5f, 1.2f, 1.0f);
            sphere.transform.localScale = Vector3.one * 0.15f;

            SetMaterial(sphere, new Color(0.3f, 0.8f, 0.3f));
            AddPhysics(sphere);

            Undo.RegisterCreatedObjectUndo(sphere, "Create Grabbable Sphere");
        }

        private static void CreatePokeableCube(Transform parent)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "PokeableCube";
            cube.transform.SetParent(parent);
            cube.transform.localPosition = new Vector3(0.0f, 1.0f, 1.5f);
            cube.transform.localScale = new Vector3(0.3f, 0.3f, 0.1f);

            SetMaterial(cube, new Color(0.3f, 0.3f, 0.8f));

            Undo.RegisterCreatedObjectUndo(cube, "Create Pokeable Cube");
        }

        private static void AddPhysics(GameObject obj)
        {
            Rigidbody rb = obj.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
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
