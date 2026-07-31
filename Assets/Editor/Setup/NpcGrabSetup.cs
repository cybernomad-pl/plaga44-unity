#if UNITY_EDITOR
// =============================================================================
// NpcGrabSetup.cs
// CYBERNOMAD -- Piecze regiony chwytu ISDK w prefab NPC (Resources/Npc/PINEA_NPC).
//
// Gracz moze chwycic NPC za KONCZYNY (ten sam ISDK HandGrab co itemy):
//   L-reka  : mixamorig:LeftArm  -> LeftHand
//   P-reka  : mixamorig:RightArm -> RightHand
//   L-noga  : mixamorig:LeftUpLeg  -> LeftFoot
//   P-noga  : mixamorig:RightUpLeg -> RightFoot
//   Glowa   : mixamorig:Neck     -> HeadTop_End  (glowa+szyja)
// Tulow (mixamorig:Hips -> Spine2, kregoslup/miednica) = OSOBNY region, chwytalny
// TYLKO gdy NPC nieprzytomny/trup -- bramkuje go runtime NpcGrabRegions wg stanu.
//
// Kazdy region = dziecko kosci-korzenia z: CapsuleCollider(trigger, wzdluz kosci)
// + Rigidbody(kinematic) + Grabbable + HandGrabInteractable(free grab, bez pozy palcow).
// Transformer wg regionu:
//   konczyna -> NpcLimbPoseTransformer (FK: obrot kosci w stawie, limit maxSwing,
//               pierwszy grab woła EnterPosableMode -> baked pose),
//   torso    -> NpcBodyMoveTransformer (translacja CALEGO root NPC).
// Osobny Rigidbody na region -- HandGrabInteractable.Colliders = colliders w
// hierarchii JEGO Rigidbody; wspolny rb zlepilby wszystkie regiony w jeden.
// Kinematic + trigger -- powierzchnia chwytu, nie kolizja fizyczna.
// Idempotent: kasuje wczesniej wygenerowane GrabRegion_* i re-tworzy.
//
// ZERO FALLBACKOW: brak kosci-korzenia/konca -> LogError + return false (nie zgaduj
// innej kosci). Prefab bez NpcController -> LogError + return false.
// =============================================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Oculus.Interaction;                 // Grabbable, ITransformer
using Oculus.Interaction.HandGrab;        // HandGrabInteractable
using Oculus.Interaction.Grab;            // GrabTypeFlags
using Oculus.Interaction.GrabAPI;         // GrabbingRule
using Plaga44.Npc;                        // NpcController, NpcGrabRegions, NpcLimbPoseTransformer, NpcBodyMoveTransformer

namespace Plaga44.Editor.Setup
{
    public static class NpcGrabSetup
    {
        private const string LOG = "[PLAGA44][NpcGrabSetup]";
        private const string PrefabPath = "Assets/Resources/Npc/PINEA_NPC.prefab";
        private const string RegionPrefix = "GrabRegion_";

        // Katalog regionow EXPLICIT (nie zgadujemy z nazw kosci). Kosc-korzen + kosc-koniec
        // wyznaczaja os i dlugosc kapsuly; radius per region; torso=bramkowany stanem.
        private struct RegionDef
        {
            public string id, rootBone, tipBone;
            public float radius, maxSwing; // maxSwing: limit stawu (stopnie); nieuzywane dla torso
            public bool torso;
            public RegionDef(string id, string root, string tip, float r, float maxSwing, bool torso)
            { this.id = id; rootBone = root; tipBone = tip; radius = r; this.maxSwing = maxSwing; this.torso = torso; }
        }

        // maxSwing przyblizony anatomicznie (ramie/udo kula, szyja bardziej ograniczona) -- stroic recznie.
        private static readonly RegionDef[] Regions =
        {
            new RegionDef("LArm",  "mixamorig:LeftArm",   "mixamorig:LeftHand",   0.06f, 100f, false),
            new RegionDef("RArm",  "mixamorig:RightArm",  "mixamorig:RightHand",  0.06f, 100f, false),
            new RegionDef("LLeg",  "mixamorig:LeftUpLeg", "mixamorig:LeftFoot",   0.09f,  90f, false),
            new RegionDef("RLeg",  "mixamorig:RightUpLeg","mixamorig:RightFoot",  0.09f,  90f, false),
            new RegionDef("Head",  "mixamorig:Neck",      "mixamorig:HeadTop_End",0.10f,  45f, false),
            new RegionDef("Torso", "mixamorig:Hips",      "mixamorig:Spine2",     0.14f,   0f, true),
        };

        [MenuItem("PLAGA44/Setup/NPC Grab (ISDK)")]
        private static void MenuRun() => Run(null);

        // cfg nieuzywane -- sciezka prefabu stala. Podpis zgodny z pozostalymi Setup.
        public static bool Run(BootstrapConfig cfg)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Debug.LogError($"{LOG} brak prefabu {PrefabPath}");
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                NpcController controller = root.GetComponent<NpcController>();
                if (controller == null)
                {
                    Debug.LogError($"{LOG} prefab bez NpcController -- SKIP (nie dodaje, stan zycia wymagany)");
                    return false;
                }

                CleanupGenerated(root);

                var torsoRegions = new List<GameObject>();
                foreach (RegionDef def in Regions)
                {
                    GameObject region = BuildRegion(root, controller, def);
                    if (region == null) return false; // ZERO FALLBACK: brak kosci -> przerwij, nie zapisuj polowicznie
                    if (def.torso)
                    {
                        region.SetActive(false); // default Alive: tulow niechwytalny; NpcGrabRegions re-aplikuje
                        torsoRegions.Add(region);
                    }
                }

                WireRegionsComponent(root, controller, torsoRegions);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"{LOG} [OK] {Regions.Length} regionow chwytu ({torsoRegions.Count} tulow, brama stanem)");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Buduje jeden region chwytu jako dziecko kosci-korzenia. Zwraca GO lub null (brak kosci).
        private static GameObject BuildRegion(GameObject root, NpcController controller, RegionDef def)
        {
            Transform rootBone = FindDeep(root.transform, def.rootBone);
            if (rootBone == null)
            {
                Debug.LogError($"{LOG} [{def.id}] brak kosci-korzenia '{def.rootBone}' -- SKIP (nie zgaduje innej)");
                return null;
            }
            Transform tipBone = FindDeep(root.transform, def.tipBone);
            if (tipBone == null)
            {
                Debug.LogError($"{LOG} [{def.id}] brak kosci-konca '{def.tipBone}' -- SKIP (nie zgaduje innej)");
                return null;
            }

            var go = new GameObject(RegionPrefix + def.id);
            go.transform.SetParent(rootBone, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            // Os/dlugosc kapsuly z wektora korzen->koniec w local space korzenia
            // (= local space GO, bo localScale=1, localRot=identity, localPos=0).
            Vector3 dir = rootBone.InverseTransformPoint(tipBone.position);
            float length = dir.magnitude;
            int axis = DominantAxis(dir);

            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;      // powierzchnia chwytu ISDK, NIE kolizja fizyczna
            capsule.direction = axis;      // 0=X 1=Y 2=Z
            capsule.radius = def.radius;
            capsule.height = Mathf.Max(length, def.radius * 2f);
            capsule.center = dir * 0.5f;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;         // sterowany animacja kosci-rodzica, bez fizyki
            rb.useGravity = false;

            var grabbable = go.AddComponent<Grabbable>();
            // Torso -> przenosi CALEGO NPC (translacja root). Konczyna -> FK: obrot kosci w stawie.
            ITransformer transformer = def.torso
                ? WireBodyMover(go, root)
                : WireLimbPoser(go, controller, def.maxSwing);
            grabbable.InjectOptionalOneGrabTransformer(transformer);
            grabbable.InjectOptionalTwoGrabTransformer(transformer);
            grabbable.MaxGrabPoints = 1;   // chwyt jedna reka (manipulacja pojedynczej kosci/ciala)
            EditorUtility.SetDirty(grabbable);

            var hgi = go.AddComponent<HandGrabInteractable>();
            hgi.InjectRigidbody(rb);
            hgi.InjectOptionalPointableElement(grabbable);        // Grabbable : IPointableElement
            hgi.InjectSupportedGrabTypes(GrabTypeFlags.All);      // Pinch | Palm
            hgi.InjectPinchGrabRules(GrabbingRule.DefaultPinchRule);
            hgi.InjectPalmGrabRules(GrabbingRule.DefaultPalmRule);
            EditorUtility.SetDirty(hgi);

            Debug.Log($"{LOG} [{def.id}] region na '{def.rootBone}' (len={length:0.###}, axis={axis}, r={def.radius})");
            return go;
        }

        // Konczyna: NpcLimbPoseTransformer (FK) + wpiecie NpcController i limitu stawu.
        private static ITransformer WireLimbPoser(GameObject go, NpcController controller, float maxSwing)
        {
            var poser = go.AddComponent<NpcLimbPoseTransformer>();
            var so = new SerializedObject(poser);
            var ctrl = so.FindProperty("_controller");
            var max = so.FindProperty("_maxSwingAngle");
            if (ctrl == null || max == null)
            {
                Debug.LogError($"{LOG} NpcLimbPoseTransformer bez pol _controller/_maxSwingAngle -- wpiecie pominiete");
                return poser;
            }
            ctrl.objectReferenceValue = controller;
            max.floatValue = maxSwing;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(poser);
            return poser;
        }

        // Torso: NpcBodyMoveTransformer + wpiecie root NPC (translacja calego ciala).
        private static ITransformer WireBodyMover(GameObject go, GameObject root)
        {
            var mover = go.AddComponent<NpcBodyMoveTransformer>();
            var so = new SerializedObject(mover);
            var rootProp = so.FindProperty("_npcRoot");
            if (rootProp == null)
            {
                Debug.LogError($"{LOG} NpcBodyMoveTransformer bez pola _npcRoot -- wpiecie pominiete");
                return mover;
            }
            rootProp.objectReferenceValue = root.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mover);
            return mover;
        }

        // Dokleja/aktualizuje NpcGrabRegions z referencjami tulowia.
        private static void WireRegionsComponent(GameObject root, NpcController controller, List<GameObject> torsoRegions)
        {
            var comp = root.GetComponent<NpcGrabRegions>();
            if (comp == null) comp = root.AddComponent<NpcGrabRegions>();

            var so = new SerializedObject(comp);
            var ctrlProp = so.FindProperty("_controller");
            var arrProp = so.FindProperty("_torsoRegions");
            if (ctrlProp == null || arrProp == null)
            {
                Debug.LogError($"{LOG} NpcGrabRegions bez pol _controller/_torsoRegions -- pominieto wpiecie");
                return;
            }
            ctrlProp.objectReferenceValue = controller;
            arrProp.arraySize = torsoRegions.Count;
            for (int i = 0; i < torsoRegions.Count; i++)
                arrProp.GetArrayElementAtIndex(i).objectReferenceValue = torsoRegions[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(comp);
        }

        // Kasuje wczesniej wygenerowane GrabRegion_* (idempotencja) w calej hierarchii.
        private static void CleanupGenerated(GameObject root)
        {
            var toKill = new List<GameObject>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name.StartsWith(RegionPrefix)) toKill.Add(t.gameObject);
            foreach (var go in toKill) Object.DestroyImmediate(go, true);
        }

        private static int DominantAxis(Vector3 v)
        {
            float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
            if (ax >= ay && ax >= az) return 0;
            return ay >= az ? 1 : 2;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
#endif
