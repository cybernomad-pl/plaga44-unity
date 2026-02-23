#if UNITY_EDITOR
using Plaga44.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor
{
    /// <summary>
    /// Creates humanoid test targets in the scene with capsule/sphere colliders
    /// and HitZone components on each body part. Body parts detach on hit.
    ///
    /// Menu: CYBERNOMAD / Scene Setup / Add Test Targets
    /// </summary>
    public static class TargetFactory
    {
        private const string LOG = "[PLAGA44]";

        private static readonly Color TargetColor = new Color(0.85f, 0.55f, 0.2f);

        [MenuItem("CYBERNOMAD/Scene Setup/Add Test Targets", false, 102)]
        public static void AddTestTargets()
        {
            Debug.Log($"{LOG} Creating 10 test targets...");

            // 10 targets spread wide, 5-50m away, varied X positions
            Vector3[] positions =
            {
                new Vector3(-2.0f, 0f,   5.0f),
                new Vector3( 1.5f, 0f,   8.0f),
                new Vector3(-4.0f, 0f,  12.0f),
                new Vector3( 3.0f, 0f,  15.0f),
                new Vector3( 0.0f, 0f,  20.0f),
                new Vector3(-6.0f, 0f,  25.0f),
                new Vector3( 5.0f, 0f,  30.0f),
                new Vector3(-3.0f, 0f,  35.0f),
                new Vector3( 2.0f, 0f,  42.0f),
                new Vector3( 0.0f, 0f,  50.0f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                CreateTarget($"Target_{i}", positions[i]);
            }

            Debug.Log($"{LOG} 10 test targets created.");
        }

        static void CreateTarget(string name, Vector3 position)
        {
            GameObject root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, $"Create {name}");

            root.AddComponent<HitTarget>();
            root.transform.position = position;

            // HEAD -- sphere at top of neck
            CreateZoneSphere(root.transform, "Head", HitZoneType.Head,
                position: new Vector3(0f, 1.68f, 0f), radius: 0.12f);

            // TORSO (upper) -- hit here = explode everything
            CreateZoneCapsule(root.transform, "Body", HitZoneType.Body,
                position: new Vector3(0f, 1.30f, 0f),
                radius: 0.18f, height: 0.45f, direction: 1);

            // PELVIS -- bridge between torso and legs
            CreateZoneCapsule(root.transform, "Pelvis", HitZoneType.Body,
                position: new Vector3(0f, 0.95f, 0f),
                radius: 0.16f, height: 0.25f, direction: 1);

            // LEFT ARM -- T-pose: horizontal along X axis, attached to shoulder
            CreateZoneCapsule(root.transform, "LeftArm", HitZoneType.LeftArm,
                position: new Vector3(-0.48f, 1.38f, 0f),
                radius: 0.05f, height: 0.55f, direction: 0); // 0 = X axis

            // RIGHT ARM -- T-pose
            CreateZoneCapsule(root.transform, "RightArm", HitZoneType.RightArm,
                position: new Vector3(0.48f, 1.38f, 0f),
                radius: 0.05f, height: 0.55f, direction: 0);

            // LEFT LEG -- attached to pelvis
            CreateZoneCapsule(root.transform, "LeftLeg", HitZoneType.LeftLeg,
                position: new Vector3(-0.10f, 0.45f, 0f),
                radius: 0.07f, height: 0.80f, direction: 1);

            // RIGHT LEG
            CreateZoneCapsule(root.transform, "RightLeg", HitZoneType.RightLeg,
                position: new Vector3(0.10f, 0.45f, 0f),
                radius: 0.07f, height: 0.80f, direction: 1);
        }

        // -------------------------------------------------------------------------

        private static void CreateZoneSphere(
            Transform parent, string partName, HitZoneType zoneType,
            Vector3 position, float radius)
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
            Transform parent, string partName, HitZoneType zoneType,
            Vector3 position, float radius, float height, int direction)
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

        private static void AddVisualSphere(GameObject parent, float radius)
        {
            GameObject vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            vis.name = "Visual";
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

            float scaleXZ = radius / 0.5f;
            float scaleY  = height / 2f;

            switch (direction)
            {
                case 0:
                    vis.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    vis.transform.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);
                    break;
                case 2:
                    vis.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    vis.transform.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);
                    break;
                default:
                    vis.transform.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);
                    break;
            }

            ApplyMaterial(vis);
        }

        private static void ApplyMaterial(GameObject go)
        {
            Renderer rend = go.GetComponent<Renderer>();
            if (rend == null) return;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = TargetColor;
            rend.sharedMaterial = mat;
        }
    }
}
#endif
