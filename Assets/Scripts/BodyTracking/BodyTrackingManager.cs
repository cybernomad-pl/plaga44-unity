// BodyTrackingManager.cs
// PLAGA '44 -- Initializes OVRBody for Meta Movement SDK body tracking.
// Configures tracking fidelity (High) and joint set (FullBody).
// Optional debug skeleton visualization.
//
// Requires: com.meta.xr.sdk.core v74+ (OVRBody introduced in v50)
// Quest 3 / 3S supports full body tracking.
//
// Usage: Add to a GameObject in scene (e.g. "BodyTrackingManager").
// Run CYBERNOMAD/Scene Setup/Setup Body Tracking first to configure OVRManager.

using UnityEngine;

#if HAS_META_XR
using System.Collections.Generic;
#endif

namespace Plaga44.BodyTracking
{
    /// <summary>
    /// Initializes OVRBody for Meta Movement SDK body tracking.
    /// Configures fidelity (High) and joint set (FullBody) at runtime.
    /// Provides joint data to other components via GetJoint().
    /// </summary>
    public class BodyTrackingManager : MonoBehaviour
    {
        private const string LOG = "[BodyTracking]";

        [Header("Tracking Configuration")]
        [Tooltip("Show OVRSkeleton debug visualization. Requires OVRSkeleton component on same GO.")]
        public bool showDebugSkeleton = false;

        // -- runtime state --

        private bool _initialized = false;
        private bool _trackingActive = false;

#if HAS_META_XR
        [Header("Tracking Configuration (Meta XR)")]
        [Tooltip("Body tracking fidelity. High = full skeleton, uses more CPU.")]
        public OVRPlugin.BodyTrackingFidelity2 trackingFidelity =
            OVRPlugin.BodyTrackingFidelity2.High;

        [Tooltip("Which joints to track. FullBody includes legs and feet.")]
        public OVRPlugin.BodyJointSet jointSet =
            OVRPlugin.BodyJointSet.FullBody;

        private OVRBody _ovrBody;
        private OVRSkeleton _skeleton;

        // Cached joint positions for quick access by other components.
        private readonly Dictionary<OVRSkeleton.BoneId, OVRPlugin.Posef> _joints =
            new Dictionary<OVRSkeleton.BoneId, OVRPlugin.Posef>();
#endif

        // -- public API --

        /// <summary>True when OVRBody is present and tracking is active.</summary>
        public bool IsTrackingActive => _trackingActive;

#if HAS_META_XR
        /// <summary>
        /// Returns the world-space pose of the requested joint.
        /// Returns false if body tracking is not active or joint not available.
        /// </summary>
        public bool TryGetJointPose(OVRSkeleton.BoneId boneId, out OVRPlugin.Posef pose)
        {
            return _joints.TryGetValue(boneId, out pose);
        }
#endif

        // -- Unity lifecycle --

        private void Awake()
        {
            InitializeBodyTracking();
        }

        private void OnEnable()
        {
            if (!_initialized)
                InitializeBodyTracking();
        }

        private void Update()
        {
            UpdateTrackingState();

#if HAS_META_XR
            if (_trackingActive)
                CacheJoints();
#endif
        }

        // -- initialization --

        private void InitializeBodyTracking()
        {
#if HAS_META_XR
            // Ensure OVRBody component exists on this GameObject.
            _ovrBody = GetComponent<OVRBody>();
            if (_ovrBody == null)
            {
                _ovrBody = gameObject.AddComponent<OVRBody>();
                Debug.Log($"{LOG} Added OVRBody component.");
            }

            // Configure body tracking fidelity via OVRPlugin.
            ApplyBodyTrackingConfiguration();

            // Debug skeleton -- add/configure OVRSkeleton if requested.
            ConfigureDebugSkeleton();

            _initialized = true;
            Debug.Log($"{LOG} BodyTrackingManager initialized. Fidelity={trackingFidelity}, JointSet={jointSet}");
#else
            #error "HAS_META_XR not defined -- Quest project requires Meta XR SDK"
#endif
        }

        private void ApplyBodyTrackingConfiguration()
        {
#if HAS_META_XR
            // OVRPlugin.RequestBodyTrackingFidelity sets the fidelity level.
            // Available since Meta XR SDK v50.
            bool fidelityOk = OVRPlugin.RequestBodyTrackingFidelity(
                (OVRPlugin.BodyTrackingFidelity2)trackingFidelity);

            if (!fidelityOk)
                Debug.LogWarning($"{LOG} RequestBodyTrackingFidelity({trackingFidelity}) returned false. " +
                                 "Body tracking permission may not be granted.");
            else
                Debug.Log($"{LOG} Body tracking fidelity set to: {trackingFidelity}");

            // Configure OVRBody properties directly.
            _ovrBody.ProvidedSkeletonType = jointSet == OVRPlugin.BodyJointSet.FullBody
                ? OVRPlugin.BodyJointSet.FullBody
                : OVRPlugin.BodyJointSet.UpperBody;
#endif
        }

        private void ConfigureDebugSkeleton()
        {
#if HAS_META_XR
            _skeleton = GetComponent<OVRSkeleton>();

            if (showDebugSkeleton)
            {
                if (_skeleton == null)
                {
                    _skeleton = gameObject.AddComponent<OVRSkeleton>();
                    Debug.Log($"{LOG} Added OVRSkeleton for debug visualization.");
                }

                // Configure skeleton type via reflection (field is private in OVRSkeleton).
                // OVRSkeleton.SkeletonType.Body = 23 (Meta XR SDK v74+).
                SetSkeletonTypeViaReflection(_skeleton, (int)OVRSkeleton.SkeletonType.Body);

                _skeleton.enabled = true;
                Debug.Log($"{LOG} OVRSkeleton debug visualization enabled.");
            }
            else if (_skeleton != null)
            {
                _skeleton.enabled = false;
            }
#endif
        }

#if HAS_META_XR
        private static void SetSkeletonTypeViaReflection(OVRSkeleton skeleton, int skeletonTypeValue)
        {
            var field = typeof(OVRSkeleton).GetField(
                "_skeletonType",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(skeleton, skeletonTypeValue);
                Debug.Log($"{LOG} OVRSkeleton._skeletonType set to {skeletonTypeValue} via reflection.");
            }
            else
            {
                Debug.LogWarning($"{LOG} OVRSkeleton._skeletonType field not found. " +
                                 "Skeleton type must be set manually in Inspector.");
            }
        }
#endif

        private void UpdateTrackingState()
        {
#if HAS_META_XR
            if (_ovrBody == null) return;

            bool wasActive = _trackingActive;
            _trackingActive = _ovrBody.IsBodyTracked;

            if (_trackingActive != wasActive)
            {
                Debug.Log(_trackingActive
                    ? $"{LOG} Body tracking ACTIVE."
                    : $"{LOG} Body tracking LOST.");
            }
#else
            #error "HAS_META_XR not defined -- Quest project requires Meta XR SDK"
#endif
        }

        private void CacheJoints()
        {
#if HAS_META_XR
            if (_ovrBody == null || !_ovrBody.IsBodyTracked) return;

            // OVRBody.JointLocations is an IReadOnlyList<OVRPlugin.BodyJointLocation>
            // available in Meta XR SDK v74+.
            var jointLocations = _ovrBody.JointLocations;
            if (jointLocations == null) return;

            _joints.Clear();
            for (int i = 0; i < jointLocations.Count; i++)
            {
                var loc = jointLocations[i];
                // Only cache joints with valid orientation and position flags.
                if ((loc.LocationFlags & OVRPlugin.SpaceLocationFlags.OrientationValid) != 0
                    && (loc.LocationFlags & OVRPlugin.SpaceLocationFlags.PositionValid) != 0)
                {
                    _joints[(OVRSkeleton.BoneId)i] = loc.Pose;
                }
            }
#endif
        }

        // -- Editor gizmos --

#if UNITY_EDITOR && HAS_META_XR
        private void OnDrawGizmosSelected()
        {
            if (!_trackingActive || _joints.Count == 0) return;

            Gizmos.color = Color.cyan;
            foreach (var kv in _joints)
            {
                Vector3 pos = kv.Value.Position.FromFlippedZVector3f();
                Gizmos.DrawSphere(pos, 0.02f);
            }
        }
#endif
    }
}
