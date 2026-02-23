#if UNITY_EDITOR
using Plaga44.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    /// <summary>
    /// Creates a humanoid test target in the scene with capsule/sphere colliders
    /// and HitZone components on each body part.
    ///
    /// Menu: CYBERNOMAD / Scene Setup / Add Test Target
    /// </summary>
    public static class TargetFactory
    {
        private const string LOG = "[PLAGA44]";

        // Visual material color for the target.
        private static readonly Color TargetColor = new Color(0.85f, 0.55f, 0.2f);

        [MenuItem("CYBERNOMAD/Scene Setup/Add Test Target", false, 102)]
        public static void AddTestTarget()
        {
            Debug.Log($"{LOG} Creating humanoid test target...");

            // Root object carries the HitTarget component.
            GameObject root = new GameObject("TestTarget");
            Undo.RegisterCreatedObjectUndo(root, "Create Test Target");

            root.AddComponent<HitTarget>();

            // Position it 4 m in front of scene origin (away from table).
            root.transform.position = new Vector3(0f, 0f, 4f);

            // Build body parts as children.
            // All dimensions approximate a 1.75 m standing humanoid.

            // HEAD -- sphere collider, radius 0.12 m, centre at ~1.65 m
            CreateZoneSphere(root.transform, "Head",    HitZoneType.Head,
                position: new Vector3(0f, 1.65f, 0f),
                radius: 0.12f);

            // TORSO -- capsule collider, height 0.60 m, centre at ~1.20 m
            CreateZoneCapsule(root.transform, "Body",   HitZoneType.Body,
                position: new Vector3(0f, 1.20f, 0f),
                radius: 0.18f, height: 0.60f,
                direction: 1 /* Y-axis */);

            // LEFT ARM -- capsule, along Y, centre at ~1.20 m, offset -0.35 m on X
            CreateZoneCapsule(root.transform, "LeftArm",  HitZoneType.LeftArm,
                position: new Vector3(-0.35f, 1.20f, 0f),
                radius: 0.06f, height: 0.55f,
                direction: 1);

            // RIGHT ARM
            CreateZoneCapsule(root.transform, "RightArm", HitZoneType.RightArm,
                position: new Vector3(0.35f, 1.20f, 0f),
                radius: 0.06f, height: 0.55f,
                direction: 1);

            // LEFT LEG -- capsule, centre at ~0.50 m, offset -0.12 m on X
            CreateZoneCapsule(root.transform, "LeftLeg",  HitZoneType.LeftLeg,
                position: new Vector3(-0.12f, 0.50f, 0f),
                radius: 0.08f, height: 0.80f,
                direction: 1);

            // RIGHT LEG
            CreateZoneCapsule(root.transform, "RightLeg", HitZoneType.RightLeg,
                position: new Vector3(0.12f, 0.50f, 0f),
                radius: 0.08f, height: 0.80f,
                direction: 1);

            Selection.activeGameObject = root;
            Debug.Log($"{LOG} TestTarget created at {root.transform.position}. HitTarget + 6 HitZones ready.");
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static void CreateZoneSphere(
            Transform parent,
            string partName,
            HitZoneType zoneType,
            Vector3 position,
            float radius)
        {
            GameObject go = new GameObject(partName);
            go.transform.SetParent(parent);
            go.transform.localPosition = position;

            SphereCollider col = go.AddComponent<SphereCollider>();
            col.radius = radius;

            HitZone hz = go.AddComponent<HitZone>();
            hz.zoneType = zoneType;

            AddVisualSphere(go, radius);
            Undo.RegisterCreatedObjectUndo(go, $"Create {partName}");
        }

        private static void CreateZoneCapsule(
            Transform parent,
            string partName,
            HitZoneType zoneType,
            Vector3 position,
            float radius,
            float height,
            int direction)
        {
            GameObject go = new GameObject(partName);
            go.transform.SetParent(parent);
            go.transform.localPosition = position;

            CapsuleCollider col = go.AddComponent<CapsuleCollider>();
            col.radius = radius;
            col.height = height;
            col.direction = direction;

            HitZone hz = go.AddComponent<HitZone>();
            hz.zoneType = zoneType;

            AddVisualCapsule(go, radius, height, direction);
            Undo.RegisterCreatedObjectUndo(go, $"Create {partName}");
        }

        /// <summary>
        /// Creates a visible sphere child so the zones are easy to see in Scene view.
        /// Uses isTrigger=false on the collider above -- the visual is a separate
        /// child mesh so it doesn't interfere with physics.
        /// </summary>
        private static void AddVisualSphere(GameObject parent, float radius)
        {
            GameObject vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            vis.name = "Visual";
            // Remove the collider added by CreatePrimitive -- we already have one on the parent.
            Object.DestroyImmediate(vis.GetComponent<Collider>());
            vis.transform.SetParent(parent.transform);
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localScale = Vector3.one * (radius * 2f);
            ApplyMaterial(vis);
        }

        private static void AddVisualCapsule(GameObject parent, float radius, float height, int direction)
        {
            GameObject vis = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            vis.name = "Visual";
            Object.DestroyImmediate(vis.GetComponent<Collider>());
            vis.transform.SetParent(parent.transform);
            vis.transform.localPosition = Vector3.zero;

            // Unity's Capsule primitive is 2 m tall (height=2, radius=0.5) along Y by default.
            // Scale it to match the requested dimensions.
            float scaleXZ = radius / 0.5f;
            float scaleY  = height / 2f;

            switch (direction)
            {
                case 0: // X-axis
                    vis.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    vis.transform.localScale = new Vector3(scaleY, scaleXZ, scaleXZ);
                    break;
                case 2: // Z-axis
                    vis.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    vis.transform.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);
                    break;
                default: // Y-axis (1)
                    vis.transform.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);
                    break;
            }

            ApplyMaterial(vis);
        }

        private static void ApplyMaterial(GameObject go)
        {
            Renderer rend = go.GetComponent<Renderer>();
            if (rend == null) return;

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.shader.name == "Hidden/InternalErrorShader")
            {
                // Fallback when URP is not present (e.g., plain 3D project)
                mat = new Material(Shader.Find("Standard"));
            }
            mat.color = TargetColor;
            rend.sharedMaterial = mat;
        }
    }
}
#endif
