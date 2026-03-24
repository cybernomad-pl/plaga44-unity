// AUTO-DISABLED: PLAGA44_FULL_SDK
#if PLAGA44_FULL_SDK
using UnityEngine;

namespace Plaga44.Integration
{
    /// <summary>
    /// ISDK Integration Manager -- scaffolding for Movement SDK + Interaction SDK integration.
    /// Prepares the avatar for hand grab interactions driven by body tracking.
    ///
    /// Current state: PLACEHOLDER
    ///   - Detects whether Movement SDK is present (via type reflection)
    ///   - Logs feature status at startup
    ///   - Exposes useBodyTracking and useHandGrab flags for future subsystems
    ///
    /// Future: wire up CharacterRetargeter, ISDKSkeletonProcessor, HandGrabInteractor
    ///         once Movement SDK (com.meta.xr.sdk.movement) is added to the project.
    ///
    /// Related issue: #46 -- ISDK Integration: avatar chwyta przedmioty z body tracking
    /// </summary>
    public class ISDKIntegrationManager : MonoBehaviour
    {
        private const string LOG = "[ISDKIntegration]";

        [Header("Body Tracking")]
        [Tooltip("Enable OVRBody-driven skeleton retargeting via Movement SDK.")]
        public bool useBodyTracking = true;

        [Tooltip("Reference to the OVRBody component on the avatar root. " +
                 "Movement SDK: drives CharacterRetargeter with source joints.")]
        public Component ovrBodyComponent; // typed as Component; cast to OVRBody when Movement SDK present

        [Header("Hand Grab")]
        [Tooltip("Enable Interaction SDK HandGrabInteractor on both hands.")]
        public bool useHandGrab = true;

        [Tooltip("Left hand HandGrabInteractor root. " +
                 "Interaction SDK: HandGrabInteractor drives finger pose matching.")]
        public Transform leftHandInteractorRoot;

        [Tooltip("Right hand HandGrabInteractor root.")]
        public Transform rightHandInteractorRoot;

        [Header("Debug")]
        [Tooltip("Log SDK presence and config status to Console on Start.")]
        public bool logStatusOnStart = true;

        // Runtime state
        private bool _movementSdkPresent;
        private bool _interactionSdkPresent;

        void Awake()
        {
            _movementSdkPresent  = DetectMovementSDK();
            _interactionSdkPresent = DetectInteractionSDK();
        }

        void Start()
        {
            if (logStatusOnStart)
                LogStatus();

            if (useBodyTracking)
                InitBodyTracking();

            if (useHandGrab)
                InitHandGrab();
        }

        // -------------------------------------------------------------------------
        // SDK Detection
        // -------------------------------------------------------------------------

        /// <summary>
        /// Detect Movement SDK by looking for OVRBody type.
        /// OVRBody lives in com.meta.xr.sdk.core (v77+) but CharacterRetargeter
        /// and the full Movement SDK pipeline require com.meta.xr.sdk.movement.
        /// </summary>
        private bool DetectMovementSDK()
        {
            // OVRBody is available in core SDK, CharacterRetargeter requires movement SDK
            bool ovrBodyFound = System.Type.GetType(
                "OVRBody, Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null") != null
                || System.Type.GetType("OVRBody") != null;

            bool retargeterFound = System.Type.GetType("CharacterRetargeter") != null
                || System.Type.GetType("Oculus.Movement.AnimationRigging.RetargetingLayer") != null;

            if (ovrBodyFound)
                Debug.Log($"{LOG} OVRBody type detected -- Core SDK body tracking available.");
            else
                Debug.LogWarning($"{LOG} OVRBody NOT found -- is com.meta.xr.sdk.core installed?");

            if (retargeterFound)
                Debug.Log($"{LOG} CharacterRetargeter detected -- Movement SDK fully available.");
            else
                Debug.Log($"{LOG} CharacterRetargeter NOT found -- Movement SDK (com.meta.xr.sdk.movement) not installed. Body retargeting placeholder only.");

            return ovrBodyFound;
        }

        /// <summary>
        /// Detect Interaction SDK by looking for HandGrabInteractor type.
        /// </summary>
        private bool DetectInteractionSDK()
        {
            bool found = System.Type.GetType("Oculus.Interaction.HandGrab.HandGrabInteractor") != null
                || System.Type.GetType("Oculus.Interaction.HandGrab.HandGrabInteractor, Oculus.Interaction.Runtime") != null;

            if (found)
                Debug.Log($"{LOG} HandGrabInteractor detected -- Interaction SDK available.");
            else
                Debug.LogWarning($"{LOG} HandGrabInteractor NOT found -- is com.meta.xr.sdk.interaction installed?");

            return found;
        }

        // -------------------------------------------------------------------------
        // Initialization
        // -------------------------------------------------------------------------

        private void InitBodyTracking()
        {
            if (!_movementSdkPresent)
            {
                Debug.LogWarning($"{LOG} useBodyTracking=true but Movement SDK not present. " +
                                 "Add com.meta.xr.sdk.movement to manifest to enable.");
                return;
            }

            if (ovrBodyComponent == null)
            {
                Debug.LogWarning($"{LOG} useBodyTracking=true but ovrBodyComponent is not assigned. " +
                                 "Assign OVRBody component in Inspector.");
                return;
            }

            // PLACEHOLDER: Future implementation
            //   1. Get OVRBody reference: var body = ovrBodyComponent as OVRBody;
            //   2. Get CharacterRetargeter on avatar root
            //   3. Assign body as retargeter source
            //   4. Wire ISDK Skeleton Processor to pipe hand poses into retargeted skeleton
            Debug.Log($"{LOG} Body tracking init PLACEHOLDER. OVRBody assigned: {ovrBodyComponent.name}");
        }

        private void InitHandGrab()
        {
            if (!_interactionSdkPresent)
            {
                Debug.LogWarning($"{LOG} useHandGrab=true but Interaction SDK HandGrabInteractor not found. " +
                                 "Ensure com.meta.xr.sdk.interaction is installed.");
                return;
            }

            if (leftHandInteractorRoot == null || rightHandInteractorRoot == null)
            {
                Debug.LogWarning($"{LOG} useHandGrab=true but hand interactor roots are not assigned. " +
                                 "Assign leftHandInteractorRoot and rightHandInteractorRoot in Inspector.");
                return;
            }

            // PLACEHOLDER: Future implementation
            //   1. Find HandGrabInteractor components on left/right roots
            //   2. Register grab start/stop events
            //   3. Notify AvatarGrabBridge to sync bone positions on grab
            Debug.Log($"{LOG} Hand grab init PLACEHOLDER. Left: {leftHandInteractorRoot.name}, Right: {rightHandInteractorRoot.name}");
        }

        // -------------------------------------------------------------------------
        // Status Logging
        // -------------------------------------------------------------------------

        private void LogStatus()
        {
            Debug.Log($"{LOG} === ISDK Integration Status ===");
            Debug.Log($"{LOG} Movement SDK present  : {_movementSdkPresent}");
            Debug.Log($"{LOG} Interaction SDK present: {_interactionSdkPresent}");
            Debug.Log($"{LOG} useBodyTracking        : {useBodyTracking}");
            Debug.Log($"{LOG} useHandGrab            : {useHandGrab}");
            Debug.Log($"{LOG} OVRBody component      : {(ovrBodyComponent != null ? ovrBodyComponent.name : "NOT ASSIGNED")}");
            Debug.Log($"{LOG} Left  hand root        : {(leftHandInteractorRoot  != null ? leftHandInteractorRoot.name  : "NOT ASSIGNED")}");
            Debug.Log($"{LOG} Right hand root        : {(rightHandInteractorRoot != null ? rightHandInteractorRoot.name : "NOT ASSIGNED")}");
            Debug.Log($"{LOG} ================================");
        }

#if UNITY_EDITOR
        [ContextMenu("Log Integration Status")]
        private void EditorLogStatus()
        {
            _movementSdkPresent    = DetectMovementSDK();
            _interactionSdkPresent = DetectInteractionSDK();
            LogStatus();
        }
#endif
    }
}
#endif // PLAGA44_FULL_SDK
