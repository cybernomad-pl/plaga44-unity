// AUTO-DISABLED: depends on classes guarded by PLAGA44_FULL_SDK
#if PLAGA44_FULL_SDK
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

#if HAS_META_XR
using Plaga44.MixedReality;
#endif

namespace Plaga44.Editor
{
    /// <summary>
    /// One-click Mixed Reality scene setup.
    /// Menu: CYBERNOMAD / Scene Setup / Setup Mixed Reality
    ///
    /// What it does:
    ///   1. Locates or creates OVRManager in the scene.
    ///   2. Enables passthrough on OVRManager.
    ///   3. Adds OVRPassthroughLayer to the OVRManager GameObject.
    ///   4. Adds or locates OVRSceneManager in the scene.
    ///   5. Adds PassthroughManager (runtime script) to the OVRManager GameObject.
    ///   6. Adds SceneAnchorMapper to the OVRSceneManager GameObject.
    ///   7. Marks the scene dirty so Unity prompts to save.
    /// </summary>
    public static class MRSetup
    {
        private const string LOG = "[PLAGA44]";

        [MenuItem("CYBERNOMAD/Scene Setup/Setup Mixed Reality", false, 110)]
        public static void SetupMixedReality()
        {
            Debug.Log($"{LOG} === Setup Mixed Reality ===");

#if !HAS_META_XR
            Debug.LogWarning($"{LOG} HAS_META_XR is not defined. " +
                             "Install Meta XR SDK and add HAS_META_XR to Scripting Define Symbols " +
                             "before running this setup. " +
                             "(CYBERNOMAD / Meta SDK Setup / 1. Setup Meta SDK)");
            EditorUtility.DisplayDialog(
                "Meta XR SDK Required",
                "HAS_META_XR scripting define is missing.\n\n" +
                "Run: CYBERNOMAD > Meta SDK Setup > 1. Setup Meta SDK\n" +
                "then add HAS_META_XR to Player Settings > Scripting Define Symbols.",
                "OK");
            return;
#else
            SetupOVRManager();
            SetupOVRSceneManager();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"{LOG} === Mixed Reality setup complete. Save the scene. ===");
            EditorUtility.DisplayDialog(
                "Mixed Reality Setup",
                "Setup complete!\n\n" +
                "Components added:\n" +
                "  - OVRPassthroughLayer (on OVRManager)\n" +
                "  - PassthroughManager  (on OVRManager)\n" +
                "  - OVRSceneManager     (in scene)\n" +
                "  - SceneAnchorMapper   (on OVRSceneManager)\n\n" +
                "Next steps:\n" +
                "1. Assign gameplay prefabs in SceneAnchorMapper > Mappings.\n" +
                "2. Add PassthroughPortal to any trigger collider for VR<->MR transitions.\n" +
                "3. Build to Quest 3 and run Quest Space Setup if not already done.",
                "OK");
#endif
        }

#if HAS_META_XR
        // ------------------------------------------------------------------ //
        //  OVRManager                                                         //
        // ------------------------------------------------------------------ //

        private static void SetupOVRManager()
        {
            var ovrManager = FindOrCreateOVRManager();

            // Enable passthrough
            ovrManager.isInsightPassthroughEnabled = true;
            Debug.Log($"{LOG} OVRManager.isInsightPassthroughEnabled = true");

            // Add OVRPassthroughLayer if missing
            var passthroughLayer = ovrManager.GetComponent<OVRPassthroughLayer>();
            if (passthroughLayer == null)
            {
                passthroughLayer = Undo.AddComponent<OVRPassthroughLayer>(ovrManager.gameObject);
                passthroughLayer.projectionSurfaceType = OVRPassthroughLayer.ProjectionSurfaceType.Reconstruction;
                Debug.Log($"{LOG} Added OVRPassthroughLayer (Reconstruction surface)");
            }
            else
            {
                Debug.Log($"{LOG} OVRPassthroughLayer already present -- skipped");
            }

            // Add PassthroughManager runtime script if missing
            var ptManager = ovrManager.GetComponent<PassthroughManager>();
            if (ptManager == null)
            {
                Undo.AddComponent<PassthroughManager>(ovrManager.gameObject);
                Debug.Log($"{LOG} Added PassthroughManager");
            }
            else
            {
                Debug.Log($"{LOG} PassthroughManager already present -- skipped");
            }
        }

        private static OVRManager FindOrCreateOVRManager()
        {
            var existing = Object.FindObjectOfType<OVRManager>();
            if (existing != null)
            {
                Debug.Log($"{LOG} Found existing OVRManager on '{existing.gameObject.name}'");
                return existing;
            }

            Debug.Log($"{LOG} No OVRManager found -- creating OVRCameraRig");
            var ovrCameraRig = new GameObject("OVRCameraRig");
            Undo.RegisterCreatedObjectUndo(ovrCameraRig, "Create OVRCameraRig");

            var manager = Undo.AddComponent<OVRManager>(ovrCameraRig);
            Undo.AddComponent<OVRCameraRig>(ovrCameraRig);

            return manager;
        }

        // ------------------------------------------------------------------ //
        //  OVRSceneManager                                                    //
        // ------------------------------------------------------------------ //

        private static void SetupOVRSceneManager()
        {
            var sceneManager = Object.FindObjectOfType<OVRSceneManager>();

            if (sceneManager == null)
            {
                var go = new GameObject("OVRSceneManager");
                Undo.RegisterCreatedObjectUndo(go, "Create OVRSceneManager");
                sceneManager = Undo.AddComponent<OVRSceneManager>(go);
                Debug.Log($"{LOG} Created OVRSceneManager GameObject");
            }
            else
            {
                Debug.Log($"{LOG} OVRSceneManager already present on '{sceneManager.gameObject.name}'");
            }

            // Add SceneAnchorMapper if missing
            var mapper = sceneManager.GetComponent<SceneAnchorMapper>();
            if (mapper == null)
            {
                Undo.AddComponent<SceneAnchorMapper>(sceneManager.gameObject);
                Debug.Log($"{LOG} Added SceneAnchorMapper");
            }
            else
            {
                Debug.Log($"{LOG} SceneAnchorMapper already present -- skipped");
            }
        }
#endif // HAS_META_XR
    }
}
#endif // UNITY_EDITOR
#endif // PLAGA44_FULL_SDK
