#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Plaga44.Core;
using System.IO;
using System.Collections.Generic;
using System.Reflection;

namespace Plaga44.Editor
{
    /// <summary>
    /// NPC Spawner -- spawnuje warianty PINEA jako poseable ragdoll.
    /// Kazda czesc ciala = Rigidbody (kinematic) + Collider + grabbable.
    /// Do keyframe'owania animacji -- lapiesz czesc ciala, ustawiasz poze, nagrywasz klatke.
    ///
    /// Menu: CYBERNOMAD > NPC Spawner > ...
    /// </summary>
    public static class NPCSpawner
    {
        private const string LOG = "[PLAGA44][NPCSpawner]";
        private const string NPC_DIR = "Assets/PLAGA44/NPC/PINEA";
        private static int boneCounter = 0;

        // =========================================================================
        // Menu items -- jeden per wariant
        // =========================================================================

        [MenuItem("CYBERNOMAD/NPC Spawner/PINEA Base", priority = 200)]
        public static void SpawnPINEA_Base() => SpawnNPC("PINEA_base");

        [MenuItem("CYBERNOMAD/NPC Spawner/PINEA v1 - ACU Pants", priority = 201)]
        public static void SpawnPINEA_V1() => SpawnNPC("PINEA_v1_brownshirt");

        [MenuItem("CYBERNOMAD/NPC Spawner/PINEA v2 - Fleck Military", priority = 202)]
        public static void SpawnPINEA_V2() => SpawnNPC("PINEA_v2_fleck");

        [MenuItem("CYBERNOMAD/NPC Spawner/PINEA v3 - IOTV Tactical", priority = 203)]
        public static void SpawnPINEA_V3() => SpawnNPC("PINEA_v3_chainmail");

        [MenuItem("CYBERNOMAD/NPC Spawner/PINEA v4 - Armored Gambeson", priority = 204)]
        public static void SpawnPINEA_V4() => SpawnNPC("PINEA_v4_gambeson");

        [MenuItem("CYBERNOMAD/NPC Spawner/PINEA v5 - Doublet", priority = 205)]
        public static void SpawnPINEA_V5() => SpawnNPC("PINEA_v5_doublet");

        [MenuItem("CYBERNOMAD/NPC Spawner/-- Spawn ALL Variants --", priority = 220)]
        public static void SpawnAll()
        {
            string[] variants = { "PINEA_base", "PINEA_v1_brownshirt", "PINEA_v2_fleck",
                                  "PINEA_v3_chainmail", "PINEA_v4_gambeson", "PINEA_v5_doublet" };
            float spacing = 1.5f;
            for (int i = 0; i < variants.Length; i++)
            {
                var npc = BuildPoseableNPC(variants[i]);
                npc.transform.position += Vector3.right * (i * spacing - (variants.Length - 1) * spacing * 0.5f);
            }
            Debug.Log($"{LOG} Wszystkie {variants.Length} wariantow zespawnowane.");
        }

        // =========================================================================
        // Core -- budowanie poseable NPC
        // =========================================================================

        private static void SpawnNPC(string variantName)
        {
            boneCounter = 0;
            var npc = BuildPoseableNPC(variantName);
            PositionInFrontOfCamera(npc.transform, 3f);
            EnsurePoseRecorder();
            Undo.RegisterCreatedObjectUndo(npc, $"Spawn {variantName}");
            Selection.activeGameObject = npc;
            EditorGUIUtility.PingObject(npc);
            Debug.Log($"{LOG} {variantName} zespawnowany na {npc.transform.position}");
        }

        /// <summary>
        /// Upewnij sie ze w scenie jest JEDEN globalny PoseRecorder.
        /// Lapie keyframe WSZYSTKICH NPC naraz (A/X na Quecie).
        /// </summary>
        private static void EnsurePoseRecorder()
        {
            var existing = Object.FindFirstObjectByType<PoseRecorder>();
            if (existing != null) return;

            var recorderGO = new GameObject("[PoseRecorder]");
            recorderGO.AddComponent<PoseRecorder>();
            Undo.RegisterCreatedObjectUndo(recorderGO, "Create PoseRecorder");
            Debug.Log($"{LOG} PoseRecorder dodany do sceny. A/X = keyframe wszystkich NPC.");
        }

        private static GameObject BuildPoseableNPC(string variantName)
        {
            // Root
            GameObject root = new GameObject(variantName);

            // Animator (pusty -- do pozniejszego przypięcia AnimatorController)
            var animator = root.AddComponent<Animator>();
            animator.applyRootMotion = false;

            // --- Buduj humanoidalny szkielet jako poseable ragdoll ---
            // Kazda kosc = child GO z Rigidbody(kinematic) + Collider
            // Struktura: Hips -> Spine -> Chest -> Head
            //                                   -> LeftArm -> LeftForeArm -> LeftHand
            //                                   -> RightArm -> RightForeArm -> RightHand
            //            Hips -> LeftUpLeg -> LeftLeg -> LeftFoot
            //            Hips -> RightUpLeg -> RightLeg -> RightFoot

            var hips = CreateBone(root.transform, "Hips", Vector3.up * 0.95f,
                BoneShape.Capsule, 0.15f, 0.3f, capsuleDir: 0);

            // Spine chain
            var spine = CreateBone(hips.transform, "Spine", Vector3.up * 0.15f,
                BoneShape.Capsule, 0.14f, 0.2f, capsuleDir: 1);
            var chest = CreateBone(spine.transform, "Chest", Vector3.up * 0.2f,
                BoneShape.Capsule, 0.16f, 0.25f, capsuleDir: 0);
            var neck = CreateBone(chest.transform, "Neck", Vector3.up * 0.2f,
                BoneShape.Capsule, 0.04f, 0.1f, capsuleDir: 1);
            var head = CreateBone(neck.transform, "Head", Vector3.up * 0.1f,
                BoneShape.Sphere, 0.12f);

            // Left arm
            var lShoulder = CreateBone(chest.transform, "LeftShoulder",
                new Vector3(-0.18f, 0.15f, 0f), BoneShape.Capsule, 0.04f, 0.15f, capsuleDir: 0);
            var lUpperArm = CreateBone(lShoulder.transform, "LeftUpperArm",
                new Vector3(-0.15f, 0f, 0f), BoneShape.Capsule, 0.05f, 0.25f, capsuleDir: 0);
            var lForeArm = CreateBone(lUpperArm.transform, "LeftForeArm",
                new Vector3(-0.25f, 0f, 0f), BoneShape.Capsule, 0.04f, 0.22f, capsuleDir: 0);
            var lHand = CreateBone(lForeArm.transform, "LeftHand",
                new Vector3(-0.22f, 0f, 0f), BoneShape.Box, 0.08f, 0.03f);

            // Right arm
            var rShoulder = CreateBone(chest.transform, "RightShoulder",
                new Vector3(0.18f, 0.15f, 0f), BoneShape.Capsule, 0.04f, 0.15f, capsuleDir: 0);
            var rUpperArm = CreateBone(rShoulder.transform, "RightUpperArm",
                new Vector3(0.15f, 0f, 0f), BoneShape.Capsule, 0.05f, 0.25f, capsuleDir: 0);
            var rForeArm = CreateBone(rUpperArm.transform, "RightForeArm",
                new Vector3(0.25f, 0f, 0f), BoneShape.Capsule, 0.04f, 0.22f, capsuleDir: 0);
            var rHand = CreateBone(rForeArm.transform, "RightHand",
                new Vector3(0.22f, 0f, 0f), BoneShape.Box, 0.08f, 0.03f);

            // Left leg
            var lUpLeg = CreateBone(hips.transform, "LeftUpLeg",
                new Vector3(-0.1f, -0.05f, 0f), BoneShape.Capsule, 0.07f, 0.4f, capsuleDir: 1);
            var lLeg = CreateBone(lUpLeg.transform, "LeftLeg",
                new Vector3(0f, -0.4f, 0f), BoneShape.Capsule, 0.06f, 0.38f, capsuleDir: 1);
            var lFoot = CreateBone(lLeg.transform, "LeftFoot",
                new Vector3(0f, -0.38f, 0.05f), BoneShape.Box, 0.08f, 0.05f, boxDepth: 0.2f);

            // Right leg
            var rUpLeg = CreateBone(hips.transform, "RightUpLeg",
                new Vector3(0.1f, -0.05f, 0f), BoneShape.Capsule, 0.07f, 0.4f, capsuleDir: 1);
            var rLeg = CreateBone(rUpLeg.transform, "RightLeg",
                new Vector3(0f, -0.4f, 0f), BoneShape.Capsule, 0.06f, 0.38f, capsuleDir: 1);
            var rFoot = CreateBone(rLeg.transform, "RightFoot",
                new Vector3(0f, -0.38f, 0.05f), BoneShape.Box, 0.08f, 0.05f, boxDepth: 0.2f);

            // Dodaj wizualne placeholder mesze (debug wireframe)
            AddBoneVisuals(root);

            // Oznacz variant info
            var info = root.AddComponent<NPCVariantInfo>();
            info.variantName = variantName;
            info.fusePath = $"{NPC_DIR}/{variantName}.fuse";

            return root;
        }

        // =========================================================================
        // Bone builder
        // =========================================================================

        private enum BoneShape { Sphere, Capsule, Box }

        private static GameObject CreateBone(Transform parent, string name, Vector3 localPos,
            BoneShape shape, float radius, float height = 0f,
            int capsuleDir = 1, float boxDepth = 0.1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;

            // Rigidbody -- kinematic (sztywny, nie floppy)
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.mass = GetBoneMass(name);

            // --- GRAB POINT 1: glowny collider kosci ---
            Collider mainCol = null;
            switch (shape)
            {
                case BoneShape.Sphere:
                    var sc = go.AddComponent<SphereCollider>();
                    sc.radius = radius;
                    mainCol = sc;
                    break;
                case BoneShape.Capsule:
                    var cc = go.AddComponent<CapsuleCollider>();
                    cc.radius = radius;
                    cc.height = height;
                    cc.direction = capsuleDir;
                    mainCol = cc;
                    break;
                case BoneShape.Box:
                    var bc = go.AddComponent<BoxCollider>();
                    bc.size = new Vector3(radius * 2f, height > 0 ? height : radius, boxDepth);
                    mainCol = bc;
                    break;
            }

            // --- GRAB POINT 2: offset collider (drugi punkt przyczepu) ---
            var grabPoint2 = new GameObject(name + "_GrabB");
            grabPoint2.transform.SetParent(go.transform);
            // Offset wzdluz glownej osi kosci
            Vector3 offset = capsuleDir == 0 ? Vector3.right * radius * 1.5f
                           : capsuleDir == 1 ? Vector3.up * (height > 0 ? height * 0.3f : radius)
                           : Vector3.forward * radius * 1.5f;
            grabPoint2.transform.localPosition = offset;
            grabPoint2.transform.localRotation = Quaternion.identity;
            var grabCol2 = grabPoint2.AddComponent<SphereCollider>();
            grabCol2.radius = Mathf.Max(radius * 0.8f, 0.04f);
            grabCol2.isTrigger = true; // trigger -- nie koliduje fizycznie

            // --- PoseableBone (audyt log) ---
            var bone = go.AddComponent<PoseableBone>();
            bone.boneName = name;
            bone.boneIndex = boneCounter++;

            // --- PoseableGrabbable (VR grab z OVRGrabbable) ---
            var grabbable = go.AddComponent<PoseableGrabbable>();
            // Ustaw grab points przez reflection (m_grabPoints jest protected)
            var grabPointsField = typeof(OVRGrabbable).GetField("m_grabPoints",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (grabPointsField != null && mainCol != null)
            {
                grabPointsField.SetValue(grabbable, new Collider[] { mainCol, grabCol2 });
            }
            // Zezwol na grab obu rekami
            var allowField = typeof(OVRGrabbable).GetField("m_allowOffhandGrab",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (allowField != null)
            {
                allowField.SetValue(grabbable, true);
            }

            return go;
        }

        private static float GetBoneMass(string boneName)
        {
            // Realistyczne masy (kg) dla poszczegolnych czesci ciala
            if (boneName.Contains("Head")) return 4.5f;
            if (boneName.Contains("Chest")) return 12f;
            if (boneName.Contains("Spine")) return 8f;
            if (boneName.Contains("Hips")) return 10f;
            if (boneName.Contains("UpperArm")) return 2.5f;
            if (boneName.Contains("ForeArm")) return 1.5f;
            if (boneName.Contains("Hand")) return 0.5f;
            if (boneName.Contains("UpLeg")) return 7f;
            if (boneName.Contains("Leg")) return 4f;
            if (boneName.Contains("Foot")) return 1f;
            return 1f;
        }

        // =========================================================================
        // Visual debug -- wireframe spheres na kazdej kosci
        // =========================================================================

        private static void AddBoneVisuals(GameObject root)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Color boneColor = new Color(0.8f, 0.6f, 0.4f, 0.6f); // skin-ish, semi-transparent
            Color jointColor = new Color(0.3f, 0.9f, 0.3f, 0.8f); // green joints

            foreach (var rb in root.GetComponentsInChildren<Rigidbody>())
            {
                var bone = rb.gameObject;
                var col = bone.GetComponent<Collider>();
                if (col == null) continue;

                // Sphere visual na kazda kosc
                var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = bone.name + "_visual";
                visual.transform.SetParent(bone.transform);
                visual.transform.localPosition = Vector3.zero;

                // Skaluj na podstawie collidera
                float scale = 0.05f;
                if (col is SphereCollider sc) scale = sc.radius * 2f;
                else if (col is CapsuleCollider cc) scale = cc.radius * 2f;
                else if (col is BoxCollider bc) scale = Mathf.Max(bc.size.x, bc.size.y);

                visual.transform.localScale = Vector3.one * scale;

                // Usun collider z visuala (kosc juz ma swoj)
                Object.DestroyImmediate(visual.GetComponent<Collider>());

                // Material
                var mat = new Material(shader);
                bool isJoint = bone.name.Contains("Shoulder") || bone.name.Contains("Neck");
                mat.color = isJoint ? jointColor : boneColor;

                // Transparency
                mat.SetFloat("_Surface", 1); // transparent
                mat.SetFloat("_Blend", 0);
                mat.SetFloat("_AlphaClip", 0);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;

                visual.GetComponent<Renderer>().sharedMaterial = mat;

                // Linia do parent bone (debug)
                if (bone.transform.parent != root.transform)
                {
                    var lr = bone.AddComponent<LineRenderer>();
                    lr.useWorldSpace = false;
                    lr.positionCount = 2;
                    lr.SetPosition(0, Vector3.zero);
                    lr.SetPosition(1, bone.transform.InverseTransformPoint(bone.transform.parent.position));
                    lr.startWidth = 0.015f;
                    lr.endWidth = 0.015f;

                    var lineMat = new Material(Shader.Find("Sprites/Default"));
                    lineMat.color = new Color(1f, 1f, 1f, 0.5f);
                    lr.sharedMaterial = lineMat;
                }
            }
        }

        // =========================================================================
        // Helpers
        // =========================================================================

        private static void PositionInFrontOfCamera(Transform t, float distance)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
            {
                Camera cam = sceneView.camera;
                t.position = cam.transform.position + cam.transform.forward * distance;
                Vector3 dir = cam.transform.position - t.position;
                dir.y = 0f;
                if (dir != Vector3.zero)
                    t.rotation = Quaternion.LookRotation(-dir);
            }
            else
            {
                t.position = new Vector3(0f, 0f, distance);
            }
        }
    }

    /// <summary>
    /// Komponent info -- trzyma nazwe wariantu i sciezke do .fuse
    /// </summary>
    public class NPCVariantInfo : MonoBehaviour
    {
        public string variantName;
        public string fusePath;
    }
}
#endif
