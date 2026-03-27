// AUTO-DISABLED: not needed for demo
#if PLAGA44_FULL_SDK
#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using Plaga44.Interaction;

namespace Plaga44.Editor
{
    /// <summary>
    /// Sets up HandGrabInteractor on both controller anchors of OVRCameraRig.
    /// Menu: CYBERNOMAD > Scene Setup > Add Hand Grab Interactors
    ///
    /// Requires: com.meta.xr.sdk.interaction + com.meta.xr.sdk.interaction.ovr (v81+)
    /// Uses reflection to add Interaction SDK components so this script compiles
    /// even when the SDK packages are not yet downloaded.
    /// </summary>
    public static class HandGrabSetup
    {
        private const string LOG = "[PLAGA44]";

#if HAS_META_XR
        // Interaction SDK type names used via reflection (com.meta.xr.sdk.interaction v81).
        // Full namespace path: Oculus.Interaction.HandGrab.*
        // OVR bridge: Oculus.Interaction.OVR.*  (com.meta.xr.sdk.interaction.ovr)
        private const string TYPE_HAND_GRAB_INTERACTOR =
            "Oculus.Interaction.HandGrab.HandGrabInteractor";

        // Prefab search hints for ControllerHands (from interaction.ovr samples)
        private static readonly string[] CONTROLLER_HANDS_PREFAB_HINTS = new[]
        {
            "ControllerHands",
            "OVRControllerPrefab",
            "HandGrabInteractor",
        };
#endif

        [MenuItem("CYBERNOMAD/Scene Setup/Add Hand Grab Interactors", false, 110)]
        public static void AddHandGrabInteractors()
        {
            Debug.Log($"{LOG} === Add Hand Grab Interactors ===");

#if HAS_META_XR
            var rig = FindOVRCameraRig();
            if (rig == null)
            {
                Debug.LogError(
                    $"{LOG} OVRCameraRig not found in scene. " +
                    "Run CYBERNOMAD > Scene Setup > Setup TESTBED first.");
                return;
            }

            Transform leftAnchor  = MetaQuestSetup.FindChildRecursive(rig.transform, "LeftControllerAnchor");
            Transform rightAnchor = MetaQuestSetup.FindChildRecursive(rig.transform, "RightControllerAnchor");

            if (leftAnchor == null || rightAnchor == null)
            {
                // Fallback anchor names used in some OVRCameraRig versions
                leftAnchor  = MetaQuestSetup.FindChildRecursive(rig.transform, "LeftHandAnchor");
                rightAnchor = MetaQuestSetup.FindChildRecursive(rig.transform, "RightHandAnchor");
            }

            if (leftAnchor == null || rightAnchor == null)
            {
                Debug.LogError(
                    $"{LOG} Controller anchors not found in OVRCameraRig. " +
                    "Expected: LeftControllerAnchor / RightControllerAnchor.");
                return;
            }

            // Try to instantiate from prefab first (preferred, keeps all wiring intact)
            bool prefabInstalled = TryInstallFromPrefab(rig, leftAnchor, rightAnchor);

            if (!prefabInstalled)
            {
                // Fallback: create minimal GameObjects with required components via reflection
                InstallViaReflection(leftAnchor, rightAnchor);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"{LOG} === Hand Grab Interactors setup complete ===");
#else
            Debug.LogError(
                $"{LOG} HAS_META_XR define not set. " +
                "Switch to Android build target and run Meta SDK Setup first.");
#endif
        }

#if HAS_META_XR
        private static OVRCameraRig FindOVRCameraRig()
        {
            var rig = UnityEngine.Object.FindFirstObjectByType<OVRCameraRig>();
            if (rig != null) return rig;

            // Also search by name in case FindFirstObjectByType misses it
            var go = GameObject.Find("OVRCameraRig");
            return go != null ? go.GetComponent<OVRCameraRig>() : null;
        }

        /// <summary>
        /// Tries to find ControllerHands prefab (from com.meta.xr.sdk.interaction.ovr)
        /// and instantiate it under the rig. Returns true if successful.
        /// </summary>
        private static bool TryInstallFromPrefab(
            OVRCameraRig rig, Transform leftAnchor, Transform rightAnchor)
        {
            // Check if already installed
            if (leftAnchor.Find("HandGrabInteractor_L") != null ||
                rightAnchor.Find("HandGrabInteractor_R") != null)
            {
                Debug.Log($"{LOG} HandGrabInteractors already present under controller anchors.");
                return true;
            }

            GameObject prefab = FindInteractionPrefab();

            if (prefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rig.transform);
                instance.name = "HandGrabInteractors";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                Undo.RegisterCreatedObjectUndo(instance, "Add HandGrab Prefab");
                Debug.Log(
                    $"{LOG} Installed HandGrab prefab '{prefab.name}' under OVRCameraRig.");
                return true;
            }

            Debug.LogWarning(
                $"{LOG} ControllerHands / HandGrabInteractor prefab not found in AssetDatabase. " +
                "Falling back to manual component setup. " +
                "Make sure com.meta.xr.sdk.interaction.ovr is fully downloaded.");
            return false;
        }

        private static GameObject FindInteractionPrefab()
        {
            foreach (string hint in CONTROLLER_HANDS_PREFAB_HINTS)
            {
                string[] guids = AssetDatabase.FindAssets($"{hint} t:prefab");
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    // Prefer prefabs from the Meta XR package paths
                    if (path.Contains("com.meta.xr") || path.Contains("Interaction"))
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (prefab != null)
                        {
                            Debug.Log($"{LOG} Found interaction prefab: {path}");
                            return prefab;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Creates HandGrabInteractor GameObjects via reflection (no hard SDK dependency).
        /// Works even if Interaction SDK assemblies are not yet loaded in the editor.
        /// </summary>
        private static void InstallViaReflection(Transform leftAnchor, Transform rightAnchor)
        {
            Type handGrabInteractorType = ResolveType(TYPE_HAND_GRAB_INTERACTOR);

            if (handGrabInteractorType == null)
            {
                // SDK types not resolvable -- create placeholder GameObjects with instructions
                CreatePlaceholderInteractor(leftAnchor, "HandGrabInteractor_L", "Left");
                CreatePlaceholderInteractor(rightAnchor, "HandGrabInteractor_R", "Right");
                Debug.LogWarning(
                    $"{LOG} Interaction SDK types not found in loaded assemblies. " +
                    "Created placeholder GameObjects. " +
                    "After Unity downloads com.meta.xr.sdk.interaction packages, " +
                    "add HandGrabInteractor component to each placeholder manually, " +
                    "or re-run this menu item.");
                return;
            }

            // Create left interactor
            var leftGO = CreateInteractorGO(leftAnchor, "HandGrabInteractor_L", handGrabInteractorType);
            Undo.RegisterCreatedObjectUndo(leftGO, "Add Left HandGrabInteractor");

            // Create right interactor
            var rightGO = CreateInteractorGO(rightAnchor, "HandGrabInteractor_R", handGrabInteractorType);
            Undo.RegisterCreatedObjectUndo(rightGO, "Add Right HandGrabInteractor");

            Debug.Log(
                $"{LOG} HandGrabInteractor added to both controller anchors. " +
                "Configure OVRControllerRef / OVRHandRef references in Inspector.");
        }

        private static GameObject CreateInteractorGO(
            Transform parent, string goName, Type interactorType)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            // Add HandGrabInteractor via reflection
            go.AddComponent(interactorType);

            // Add required Rigidbody (kinematic -- position controlled by tracking)
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            return go;
        }

        private static void CreatePlaceholderInteractor(
            Transform parent, string goName, string side)
        {
            // Check if already exists
            if (parent.Find(goName) != null)
            {
                Debug.Log($"{LOG} {goName} already exists under {parent.name}. Skipping.");
                return;
            }

            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            // Add kinematic Rigidbody (required by HandGrabInteractor)
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Add a helper component to remind the dev what to add
            var helper = go.AddComponent<HandGrabInteractorPlaceholder>();
            helper.handSide = side;

            Undo.RegisterCreatedObjectUndo(go, $"Add {side} HandGrabInteractor Placeholder");
            Debug.Log(
                $"{LOG} Placeholder '{goName}' created under {parent.name}. " +
                $"Add HandGrabInteractor ({side}) component manually after SDK resolves.");
        }

        private static Type ResolveType(string typeName)
        {
            // Try loaded assemblies by full namespace + class name
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(typeName);
                if (t != null) return t;
            }
            return null;
        }
#endif
    }
}
#endif
#endif
