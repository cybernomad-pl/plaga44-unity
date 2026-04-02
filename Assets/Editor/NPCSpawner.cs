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

        [MenuItem("CYBERNOMAD/NPC Spawner/PINEA v1 - Tactical (ACU + Brownshirt)", priority = 201)]
        public static void SpawnPINEA_V1() => SpawnNPC("PINEA_v1_tactical");

        [MenuItem("CYBERNOMAD/NPC Spawner/PINEA v2 - Military (Fleck + IOTV)", priority = 202)]
        public static void SpawnPINEA_V2() => SpawnNPC("PINEA_v2_military");

        [MenuItem("CYBERNOMAD/NPC Spawner/PINEA v3 - Gambeson (ACU + Armor)", priority = 203)]
        public static void SpawnPINEA_V3() => SpawnNPC("PINEA_v3_gambeson");

        [MenuItem("CYBERNOMAD/NPC Spawner/PINEA v4 - Doublet (Fleck + Sleeve)", priority = 204)]
        public static void SpawnPINEA_V4() => SpawnNPC("PINEA_v4_doublet");

        [MenuItem("CYBERNOMAD/NPC Spawner/PINEA v5 - Masked (Tactical + Ski Mask)", priority = 205)]
        public static void SpawnPINEA_V5() => SpawnNPC("PINEA_v5_masked");

        [MenuItem("CYBERNOMAD/NPC Spawner/PINEA v6 - Gasmask (Military + Gas)", priority = 206)]
        public static void SpawnPINEA_V6() => SpawnNPC("PINEA_v6_gasmask");

        [MenuItem("CYBERNOMAD/NPC Spawner/-- Spawn ALL Variants --", priority = 220)]
        public static void SpawnAll()
        {
            string[] variants = { "PINEA_base", "PINEA_v1_tactical", "PINEA_v2_military",
                                  "PINEA_v3_gambeson", "PINEA_v4_doublet", "PINEA_v5_masked",
                                  "PINEA_v6_gasmask" };
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

        // Mapowanie wariant -> model OBJ
        private static readonly Dictionary<string, string> VariantModelMap = new Dictionary<string, string>
        {
            { "PINEA_base",        "Assets/PLAGA44/NPC/PINEA/Model/PINEA.obj" },
            { "PINEA_v1_tactical", "Assets/PLAGA44/NPC/PINEA/Model/PINEA.obj" },
            { "PINEA_v2_military", "Assets/PLAGA44/NPC/PINEA/Model/PINEA.obj" },
            { "PINEA_v3_gambeson", "Assets/PLAGA44/NPC/PINEA/Model/PINEA.obj" },
            { "PINEA_v4_doublet",  "Assets/PLAGA44/NPC/PINEA/Model/PINEA.obj" },
            { "PINEA_v5_masked",   "Assets/PLAGA44/NPC/PINEA/Model/PINEA.obj" },
            { "PINEA_v6_gasmask",  "Assets/PLAGA44/NPC/PINEA-NEO/Model/PINEA-NEO.obj" },
        };

        private static GameObject BuildPoseableNPC(string variantName)
        {
            // Root
            GameObject root = new GameObject(variantName);

            // Animator
            var animator = root.AddComponent<Animator>();
            animator.applyRootMotion = false;

            // --- Zaladuj mesh z OBJ ---
            string modelPath = VariantModelMap.ContainsKey(variantName)
                ? VariantModelMap[variantName]
                : "Assets/PLAGA44/NPC/PINEA/Model/PINEA.obj";

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset != null)
            {
                var meshInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                meshInstance.name = variantName + "_Mesh";
                meshInstance.transform.SetParent(root.transform);
                meshInstance.transform.localPosition = Vector3.zero; // stopy na Y=0 w OBJ
                meshInstance.transform.localRotation = Quaternion.identity;
                meshInstance.transform.localScale = Vector3.one * 0.01f; // Fuse cm -> Unity meters
                // Raycast w dol zeby postawic na terenie
                if (Physics.Raycast(root.transform.position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 50f))
                {
                    root.transform.position = new Vector3(root.transform.position.x, hit.point.y, root.transform.position.z);
                    Debug.Log($"{LOG} Postawiony na terenie Y={hit.point.y:F2}");
                }
                Debug.Log($"{LOG} Model zaladowany: {modelPath}");
            }
            else
            {
                Debug.LogWarning($"{LOG} Model nie znaleziony: {modelPath} -- spawning bez mesha");
            }

            // --- Meta FullBody Rig ---
            // Nazwy kosci = OVRPlugin.BoneId.FullBody_* (gotowe do retargetingu)
            // Hierarchia: Root -> Hips -> SpineLower -> SpineMiddle -> SpineUpper -> Chest -> Neck -> Head
            //             Chest -> L/R Shoulder -> L/R Scapula -> L/R ArmUpper -> L/R ArmLower -> L/R HandWrist
            //             Hips -> L/R UpperLeg -> L/R LowerLeg -> L/R FootAnkle -> L/R FootBall

            // === SPINE ===
            var bodyRoot = CreateBone(root.transform, "FullBody_Root", Vector3.zero,
                BoneShape.Sphere, 0.05f);
            var hips = CreateBone(bodyRoot.transform, "FullBody_Hips", Vector3.up * 0.95f,
                BoneShape.Capsule, 0.15f, 0.3f, capsuleDir: 0);
            var spineLower = CreateBone(hips.transform, "FullBody_SpineLower", Vector3.up * 0.1f,
                BoneShape.Capsule, 0.13f, 0.18f, capsuleDir: 1);
            var spineMiddle = CreateBone(spineLower.transform, "FullBody_SpineMiddle", Vector3.up * 0.1f,
                BoneShape.Capsule, 0.13f, 0.18f, capsuleDir: 1);
            var spineUpper = CreateBone(spineMiddle.transform, "FullBody_SpineUpper", Vector3.up * 0.1f,
                BoneShape.Capsule, 0.14f, 0.18f, capsuleDir: 1);
            var chest = CreateBone(spineUpper.transform, "FullBody_Chest", Vector3.up * 0.1f,
                BoneShape.Capsule, 0.16f, 0.22f, capsuleDir: 0);
            var neck = CreateBone(chest.transform, "FullBody_Neck", Vector3.up * 0.18f,
                BoneShape.Capsule, 0.04f, 0.1f, capsuleDir: 1);
            var head = CreateBone(neck.transform, "FullBody_Head", Vector3.up * 0.1f,
                BoneShape.Sphere, 0.12f);

            // === LEFT ARM ===
            var lShoulder = CreateBone(chest.transform, "FullBody_LeftShoulder",
                new Vector3(-0.08f, 0.14f, 0f), BoneShape.Capsule, 0.04f, 0.1f, capsuleDir: 0);
            var lScapula = CreateBone(lShoulder.transform, "FullBody_LeftScapula",
                new Vector3(-0.1f, 0f, 0f), BoneShape.Capsule, 0.04f, 0.1f, capsuleDir: 0);
            var lArmUpper = CreateBone(lScapula.transform, "FullBody_LeftArmUpper",
                new Vector3(-0.1f, 0f, 0f), BoneShape.Capsule, 0.05f, 0.28f, capsuleDir: 0);
            var lArmLower = CreateBone(lArmUpper.transform, "FullBody_LeftArmLower",
                new Vector3(-0.28f, 0f, 0f), BoneShape.Capsule, 0.04f, 0.24f, capsuleDir: 0);
            var lWristTwist = CreateBone(lArmLower.transform, "FullBody_LeftHandWristTwist",
                new Vector3(-0.12f, 0f, 0f), BoneShape.Capsule, 0.03f, 0.08f, capsuleDir: 0);
            var lWrist = CreateBone(lWristTwist.transform, "FullBody_LeftHandWrist",
                new Vector3(-0.08f, 0f, 0f), BoneShape.Box, 0.08f, 0.03f);

            // === RIGHT ARM ===
            var rShoulder = CreateBone(chest.transform, "FullBody_RightShoulder",
                new Vector3(0.08f, 0.14f, 0f), BoneShape.Capsule, 0.04f, 0.1f, capsuleDir: 0);
            var rScapula = CreateBone(rShoulder.transform, "FullBody_RightScapula",
                new Vector3(0.1f, 0f, 0f), BoneShape.Capsule, 0.04f, 0.1f, capsuleDir: 0);
            var rArmUpper = CreateBone(rScapula.transform, "FullBody_RightArmUpper",
                new Vector3(0.1f, 0f, 0f), BoneShape.Capsule, 0.05f, 0.28f, capsuleDir: 0);
            var rArmLower = CreateBone(rArmUpper.transform, "FullBody_RightArmLower",
                new Vector3(0.28f, 0f, 0f), BoneShape.Capsule, 0.04f, 0.24f, capsuleDir: 0);
            var rWristTwist = CreateBone(rArmLower.transform, "FullBody_RightHandWristTwist",
                new Vector3(0.12f, 0f, 0f), BoneShape.Capsule, 0.03f, 0.08f, capsuleDir: 0);
            var rWrist = CreateBone(rWristTwist.transform, "FullBody_RightHandWrist",
                new Vector3(0.08f, 0f, 0f), BoneShape.Box, 0.08f, 0.03f);

            // === LEFT LEG ===
            var lUpperLeg = CreateBone(hips.transform, "FullBody_LeftUpperLeg",
                new Vector3(-0.1f, -0.05f, 0f), BoneShape.Capsule, 0.07f, 0.42f, capsuleDir: 1);
            var lLowerLeg = CreateBone(lUpperLeg.transform, "FullBody_LeftLowerLeg",
                new Vector3(0f, -0.42f, 0f), BoneShape.Capsule, 0.06f, 0.4f, capsuleDir: 1);
            var lAnkleTwist = CreateBone(lLowerLeg.transform, "FullBody_LeftFootAnkleTwist",
                new Vector3(0f, -0.2f, 0f), BoneShape.Capsule, 0.04f, 0.08f, capsuleDir: 1);
            var lAnkle = CreateBone(lAnkleTwist.transform, "FullBody_LeftFootAnkle",
                new Vector3(0f, -0.2f, 0.02f), BoneShape.Box, 0.06f, 0.04f, boxDepth: 0.1f);
            var lBall = CreateBone(lAnkle.transform, "FullBody_LeftFootBall",
                new Vector3(0f, 0f, 0.1f), BoneShape.Sphere, 0.03f);

            // === RIGHT LEG ===
            var rUpperLeg = CreateBone(hips.transform, "FullBody_RightUpperLeg",
                new Vector3(0.1f, -0.05f, 0f), BoneShape.Capsule, 0.07f, 0.42f, capsuleDir: 1);
            var rLowerLeg = CreateBone(rUpperLeg.transform, "FullBody_RightLowerLeg",
                new Vector3(0f, -0.42f, 0f), BoneShape.Capsule, 0.06f, 0.4f, capsuleDir: 1);
            var rAnkleTwist = CreateBone(rLowerLeg.transform, "FullBody_RightFootAnkleTwist",
                new Vector3(0f, -0.2f, 0f), BoneShape.Capsule, 0.04f, 0.08f, capsuleDir: 1);
            var rAnkle = CreateBone(rAnkleTwist.transform, "FullBody_RightFootAnkle",
                new Vector3(0f, -0.2f, 0.02f), BoneShape.Box, 0.06f, 0.04f, boxDepth: 0.1f);
            var rBall = CreateBone(rAnkle.transform, "FullBody_RightFootBall",
                new Vector3(0f, 0f, 0.1f), BoneShape.Sphere, 0.03f);

            // Dodaj wizualne placeholder mesze (debug wireframe)
            // AddBoneVisuals(root); // debug sfery wylaczone -- mamy prawdziwe mesze

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
