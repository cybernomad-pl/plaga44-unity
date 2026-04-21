// =============================================================================
// ISDKHandGrabSetup.cs
// CYBERNOMAD -- Migracja OVR Core grab (OVRGrabber/OVRGrabbable) na ISDK
// HandGrabInteractor. Meta Movement SDK V83 samples MovementISDKIntegration
// to baseline -- kopiujemy prefaby:
//
//   OVRFromLeftHandPrefab   -> child of LeftHandAnchor  (ISDK Hand adapter)
//   OVRFromRightHandPrefab  -> child of RightHandAnchor
//   HandGrabInteractor      -> child of each ISDK Hand, _hand wired przez SerializedObject
//
// Plus: disable PlagaGrabber na hand anchors (konflikt z HandGrabInteractor).
//
// Idempotent: jesli child GO juz istnieje, skip.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class ISDKHandGrabSetup
    {
        private const string LOG = "[PLAGA44][ISDKHandGrabSetup]";

        // Prefab paths (Library/PackageCache -- read-only, Instantiate OK)
        private const string LeftHandPrefabPath =
            "Packages/com.meta.xr.sdk.interaction.ovr/Runtime/Prefabs/Hands/OVRFromLeftHandPrefab.prefab";
        private const string RightHandPrefabPath =
            "Packages/com.meta.xr.sdk.interaction.ovr/Runtime/Prefabs/Hands/OVRFromRightHandPrefab.prefab";
        private const string HandGrabInteractorPath =
            "Packages/com.meta.xr.sdk.interaction/Runtime/Prefabs/HandGrab/HandGrabInteractor.prefab";

        public static void Run()
        {
            var leftAnchor  = GameObject.Find("LeftHandAnchor");
            var rightAnchor = GameObject.Find("RightHandAnchor");
            if (leftAnchor == null || rightAnchor == null)
            {
                Debug.LogWarning($"{LOG} LeftHandAnchor / RightHandAnchor not found in scene -- skip");
                return;
            }

            var leftHandPrefab       = LoadPrefab(LeftHandPrefabPath);
            var rightHandPrefab      = LoadPrefab(RightHandPrefabPath);
            var grabInteractorPrefab = LoadPrefab(HandGrabInteractorPath);
            if (leftHandPrefab == null || rightHandPrefab == null || grabInteractorPrefab == null)
            {
                Debug.LogError($"{LOG} ISDK prefabs not found. Checked:\n" +
                    $"  {LeftHandPrefabPath}\n  {RightHandPrefabPath}\n  {HandGrabInteractorPath}");
                return;
            }

            bool changed = false;
            changed |= SetupHand(leftAnchor,  leftHandPrefab,  grabInteractorPrefab, "Left");
            changed |= SetupHand(rightAnchor, rightHandPrefab, grabInteractorPrefab, "Right");

            // Wylacz PlagaGrabber (konflikt -- dwa grab systemy na tym samym GO).
            changed |= DisablePlagaGrabber(leftAnchor,  "Left");
            changed |= DisablePlagaGrabber(rightAnchor, "Right");

            if (changed)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                Debug.Log($"{LOG} Scene marked dirty");
            }
        }

        private static bool SetupHand(GameObject anchor, GameObject handPrefab,
            GameObject interactorPrefab, string sideLabel)
        {
            // ISDK Hand adapter (idempotent -- checkuj po nazwie)
            string handChildName = handPrefab.name; // "OVRFromLeftHandPrefab" etc.
            var existingHand = anchor.transform.Find(handChildName);
            GameObject isdkHand;
            if (existingHand != null)
            {
                isdkHand = existingHand.gameObject;
                Debug.Log($"{LOG} [OK] {sideLabel}: {handChildName} already present");
            }
            else
            {
                isdkHand = (GameObject)PrefabUtility.InstantiatePrefab(handPrefab, anchor.transform);
                isdkHand.name = handChildName;
                Undo.RegisterCreatedObjectUndo(isdkHand, $"ISDK Hand {sideLabel}");
                Debug.Log($"{LOG} [ADDED] {sideLabel}: {handChildName} -> {anchor.name}");
            }

            // HandGrabInteractor jako child ISDK Hand
            string interactorChildName = interactorPrefab.name; // "HandGrabInteractor"
            var existingInteractor = isdkHand.transform.Find(interactorChildName);
            GameObject interactor;
            if (existingInteractor != null)
            {
                interactor = existingInteractor.gameObject;
                Debug.Log($"{LOG} [OK] {sideLabel}: {interactorChildName} already under {handChildName}");
                return false;
            }
            else
            {
                interactor = (GameObject)PrefabUtility.InstantiatePrefab(interactorPrefab, isdkHand.transform);
                interactor.name = interactorChildName;
                Undo.RegisterCreatedObjectUndo(interactor, $"HandGrabInteractor {sideLabel}");
                Debug.Log($"{LOG} [ADDED] {sideLabel}: {interactorChildName} -> {handChildName}");
            }

            // Wire _hand reference w HandGrabInteractor -> ISDK Hand component
            WireHandReference(interactor, isdkHand, sideLabel);
            return true;
        }

        private static void WireHandReference(GameObject interactor, GameObject isdkHand, string side)
        {
            // ISDK Hand component (implementuje IHand). FindObjectOfType w prefab tree.
            var handComponent = isdkHand.GetComponentInChildren<MonoBehaviour>(true);
            UnityEngine.Component ihand = null;
            foreach (var c in isdkHand.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (c == null) continue;
                var typeName = c.GetType().FullName;
                if (typeName == "Oculus.Interaction.Input.Hand")
                {
                    ihand = c;
                    break;
                }
            }
            if (ihand == null)
            {
                Debug.LogWarning($"{LOG} {side}: Oculus.Interaction.Input.Hand not found in ISDK hand prefab");
                return;
            }

            // HandGrabInteractor._hand jest private SerializeField typ IHand (Object).
            var grabInteractor = interactor.GetComponent(System.Type.GetType(
                "Oculus.Interaction.HandGrab.HandGrabInteractor, Oculus.Interaction"));
            if (grabInteractor == null)
            {
                Debug.LogWarning($"{LOG} {side}: HandGrabInteractor component not found");
                return;
            }

            var so = new SerializedObject(grabInteractor);
            var prop = so.FindProperty("_hand");
            if (prop == null)
            {
                Debug.LogWarning($"{LOG} {side}: _hand property not found on HandGrabInteractor");
                return;
            }
            prop.objectReferenceValue = ihand;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(grabInteractor);
            Debug.Log($"{LOG} {side}: HandGrabInteractor._hand -> {ihand.name}");
        }

        private static bool DisablePlagaGrabber(GameObject anchor, string side)
        {
            var grabber = anchor.GetComponent<Plaga44.Inventory.PlagaGrabber>();
            if (grabber == null) return false;
            if (!grabber.enabled)
            {
                Debug.Log($"{LOG} {side}: PlagaGrabber already disabled");
                return false;
            }
            Undo.RecordObject(grabber, $"Disable PlagaGrabber {side}");
            grabber.enabled = false;
            EditorUtility.SetDirty(grabber);
            Debug.Log($"{LOG} {side}: PlagaGrabber disabled (ISDK HandGrabInteractor replaces)");
            return true;
        }

        private static GameObject LoadPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) Debug.LogError($"{LOG} Prefab not found: {path}");
            return prefab;
        }
    }
}
#endif
