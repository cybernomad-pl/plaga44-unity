#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Plaga44.NPC;

namespace Plaga44.Editor
{
    /// <summary>
    /// Editor tool: CYBERNOMAD > Scene Setup > Add Test NPC
    ///
    /// Creates a capsule NPC with:
    ///   - NavMeshAgent
    ///   - NPCLocomotion
    ///   - NPCStateController
    ///   - A WaypointPath with 4 patrol points arranged in a square
    ///   - Simple URP materials (body = warm grey, patrol waypoints = green)
    /// </summary>
    public static class NPCSetup
    {
        private const string LOG = "[PLAGA44]";

        [MenuItem("CYBERNOMAD/Scene Setup/Add Test NPC", false, 102)]
        public static void AddTestNPC()
        {
            Debug.Log($"{LOG} Creating test NPC...");

            // ----------------------------------------------------------------
            // Root container
            // ----------------------------------------------------------------
            GameObject root = new GameObject("TestNPC");
            Undo.RegisterCreatedObjectUndo(root, "Create Test NPC");

            // ----------------------------------------------------------------
            // Visual: Capsule body
            // ----------------------------------------------------------------
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0f, 1f, 0f); // capsule pivot is at centre
            body.transform.localScale    = Vector3.one;

            // Remove the capsule collider -- NavMeshAgent handles movement
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());

            SetMaterial(body, new Color(0.55f, 0.45f, 0.40f)); // warm grey / skin tone

            // Head indicator (small sphere so direction is obvious)
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform);
            head.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            head.transform.localScale    = Vector3.one * 0.4f;
            Object.DestroyImmediate(head.GetComponent<SphereCollider>());
            SetMaterial(head, new Color(0.85f, 0.72f, 0.60f)); // lighter skin

            // ----------------------------------------------------------------
            // NavMeshAgent (on root so it drives root position)
            // ----------------------------------------------------------------
            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.height          = 2f;
            agent.radius          = 0.35f;
            agent.speed           = 2f;
            agent.angularSpeed    = 180f;
            agent.acceleration    = 8f;
            agent.stoppingDistance = 0.4f;
            agent.autoBraking     = true;

            // ----------------------------------------------------------------
            // Waypoint path: 4 points in a square (5m side)
            // ----------------------------------------------------------------
            GameObject pathRoot = new GameObject("PatrolPath");
            pathRoot.transform.SetParent(root.transform);
            pathRoot.transform.localPosition = Vector3.zero;

            WaypointPath waypointPath = pathRoot.AddComponent<WaypointPath>();

            float half = 2.5f;
            Vector3[] corners = new Vector3[]
            {
                new Vector3( half, 0f,  half),
                new Vector3(-half, 0f,  half),
                new Vector3(-half, 0f, -half),
                new Vector3( half, 0f, -half),
            };

            Transform[] waypoints = new Transform[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                GameObject wp = new GameObject($"WP_{i:D2}");
                wp.transform.SetParent(pathRoot.transform);
                wp.transform.localPosition = corners[i];
                waypoints[i] = wp.transform;
            }
            waypointPath.waypoints = waypoints;

            // ----------------------------------------------------------------
            // NPCLocomotion
            // ----------------------------------------------------------------
            NPCLocomotion locomotion = root.AddComponent<NPCLocomotion>();
            locomotion.waypointPath         = waypointPath;
            locomotion.baseSpeed            = 2f;
            locomotion.waypointReachDistance = 0.5f;

            // ----------------------------------------------------------------
            // NPCStateController
            // ----------------------------------------------------------------
            root.AddComponent<NPCStateController>();

            // ----------------------------------------------------------------
            // Place in scene: 3 m in front of Scene View camera
            // ----------------------------------------------------------------
            root.transform.position = GetSceneViewSpawnPosition();

            // ----------------------------------------------------------------
            // Select and log
            // ----------------------------------------------------------------
            Selection.activeGameObject = root;

            Debug.Log($"{LOG} Test NPC created at {root.transform.position}.");
            Debug.Log($"{LOG} Next steps:");
            Debug.Log($"{LOG}   1. Bake a NavMesh (Window > AI > Navigation) or use NavMesh Surface.");
            Debug.Log($"{LOG}   2. Optionally add an Animator Controller with Speed / VelX / VelZ / IsMoving params.");
            Debug.Log($"{LOG}   3. Move patrol waypoints to desired positions in the scene.");
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        private static void SetMaterial(GameObject obj, Color color)
        {
            Renderer rend = obj.GetComponent<Renderer>();
            if (rend == null) return;

            // Try URP Lit first; fall back to Standard
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");

            if (shader == null)
            {
                Debug.LogWarning($"{LOG} Could not find a valid shader for NPC materials.");
                return;
            }

            Material mat = new Material(shader);
            mat.color = color;
            rend.sharedMaterial = mat;
        }

        private static Vector3 GetSceneViewSpawnPosition()
        {
            SceneView sv = SceneView.lastActiveSceneView;
            if (sv == null) return new Vector3(0f, 0f, 3f);

            // Place 5 units in front of the scene camera pivot
            Vector3 forward = sv.camera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            return sv.pivot + forward * 5f;
        }
    }
}
#endif
