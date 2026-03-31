// PlayerBody.cs
// PLAGA '44 -- Represents the player's physical body in VR.
// Uses OVRBody as the joint source and exposes them to other game systems.
//
// Designed to work with Meta Movement SDK CharacterRetargeter but does NOT
// require it -- the component is useful standalone for hit detection,
// animation, and gameplay logic that needs body joint positions.
//
// Usage:
//   1. Add to the same GameObject as BodyTrackingManager (or a child).
//   2. Optionally assign a CharacterRetargeter (when Movement SDK is present).
//   3. Query joint positions via GetJointTransform() or GetJointWorldPos().

using UnityEngine;

namespace Plaga44.BodyTracking
{
    /// <summary>
    /// Represents the player's body in VR.
    /// Bridges OVRBody joint data to gameplay systems and optional retargeting.
    /// </summary>
    public class PlayerBody : MonoBehaviour
    {
        private const string LOG = "[PlayerBody]";

        [Header("References")]
        [Tooltip("BodyTrackingManager on this rig. Auto-found on Start if not assigned.")]
        public BodyTrackingManager bodyTrackingManager;

        [Header("Body State")]
        [Tooltip("Current estimated player height in meters. Updated when tracking is active.")]
        [SerializeField] private float _estimatedHeight = 1.8f;

        /// <summary>Estimated height of the tracked player in meters.</summary>
        public float EstimatedHeight => _estimatedHeight;

        /// <summary>True when body tracking is live and joints are valid.</summary>
        public bool IsBodyTracked =>
            bodyTrackingManager != null && bodyTrackingManager.IsTrackingActive;

#if HAS_META_XR
        // References to CharacterRetargeter for animation retargeting.
        // The type is referenced loosely via Component to avoid hard dependency
        // when Movement SDK is not yet imported.
        private Component _retargeter;

        // Joint anchor transforms created at runtime for gameplay use.
        private Transform _headJoint;
        private Transform _leftHandJoint;
        private Transform _rightHandJoint;
        private Transform _hipsJoint;
        private Transform _leftFootJoint;
        private Transform _rightFootJoint;
#endif

        // -- Unity lifecycle --

        private void Start()
        {
            FindDependencies();
            CreateJointAnchors();
            TryFindRetargeter();
        }

        private void LateUpdate()
        {
#if HAS_META_XR
            if (!IsBodyTracked) return;

            UpdateJointAnchors();
            UpdateEstimatedHeight();
#endif
        }

        // -- setup --

        private void FindDependencies()
        {
            if (bodyTrackingManager == null)
                bodyTrackingManager = GetComponentInParent<BodyTrackingManager>();

            if (bodyTrackingManager == null)
                bodyTrackingManager = FindFirstObjectByType<BodyTrackingManager>();

            if (bodyTrackingManager == null)
                Debug.LogWarning($"{LOG} BodyTrackingManager not found. Body tracking will not work.");
            else
                Debug.Log($"{LOG} PlayerBody linked to BodyTrackingManager on '{bodyTrackingManager.gameObject.name}'.");
        }

        private void CreateJointAnchors()
        {
#if HAS_META_XR
            _headJoint       = CreateJointAnchor("Joint_Head");
            _leftHandJoint   = CreateJointAnchor("Joint_LeftHand");
            _rightHandJoint  = CreateJointAnchor("Joint_RightHand");
            _hipsJoint       = CreateJointAnchor("Joint_Hips");
            _leftFootJoint   = CreateJointAnchor("Joint_LeftFoot");
            _rightFootJoint  = CreateJointAnchor("Joint_RightFoot");
#endif
        }

        private void TryFindRetargeter()
        {
#if HAS_META_XR
            // Look for CharacterRetargeter (from Movement SDK com.meta.xr.sdk.movement).
            // We use string-based search so the file compiles without the Movement SDK package.
            var components = GetComponentsInChildren<Component>();
            foreach (var c in components)
            {
                if (c != null && c.GetType().FullName == "Oculus.Movement.Retargeting.CharacterRetargeter")
                {
                    _retargeter = c;
                    Debug.Log($"{LOG} Found CharacterRetargeter on '{c.gameObject.name}'. Retargeting ready.");
                    break;
                }
            }

            if (_retargeter == null)
                Debug.Log($"{LOG} CharacterRetargeter not found. " +
                          "Add Movement SDK (com.meta.xr.sdk.movement) to enable character retargeting.");
#endif
        }

        // -- joint update --

        private void UpdateJointAnchors()
        {
#if HAS_META_XR
            if (bodyTrackingManager == null) return;

            // Use enum names via reflection to be SDK-version agnostic
            UpdateAnchorByName(_headJoint,       "Body_Head", "Head");
            UpdateAnchorByName(_leftHandJoint,   "Body_LeftHandWrist", "Hand_WristRoot");
            UpdateAnchorByName(_rightHandJoint,  "Body_RightHandWrist", "Hand_WristRoot");
            UpdateAnchorByName(_hipsJoint,       "Body_Hips", "Hips");
            UpdateAnchorByName(_leftFootJoint,   "Body_LeftFootAnkle", "LeftFoot");
            UpdateAnchorByName(_rightFootJoint,  "Body_RightFootAnkle", "RightFoot");
#endif
        }

        private void UpdateEstimatedHeight()
        {
#if HAS_META_XR
            if (_headJoint == null || _hipsJoint == null) return;

            // Simple height estimate: head Y minus floor (0).
            // More accurate: head Y - feet Y midpoint.
            float headY  = _headJoint.position.y;
            float leftY  = _leftFootJoint  != null ? _leftFootJoint.position.y  : 0f;
            float rightY = _rightFootJoint != null ? _rightFootJoint.position.y : 0f;
            float feetY  = (leftY + rightY) * 0.5f;

            float height = headY - feetY;
            if (height > 0.5f && height < 3.0f) // sanity range
                _estimatedHeight = height;
#endif
        }

        // -- public API --

        /// <summary>
        /// Returns the Transform representing the requested body joint.
        /// The Transform is updated every LateUpdate when tracking is active.
        /// Returns null if joint is not available.
        /// </summary>
        public Transform GetJointTransform(BodyJoint joint)
        {
#if HAS_META_XR
            switch (joint)
            {
                case BodyJoint.Head:       return _headJoint;
                case BodyJoint.LeftHand:   return _leftHandJoint;
                case BodyJoint.RightHand:  return _rightHandJoint;
                case BodyJoint.Hips:       return _hipsJoint;
                case BodyJoint.LeftFoot:   return _leftFootJoint;
                case BodyJoint.RightFoot:  return _rightFootJoint;
                default:                   return null;
            }
#else
            #error "HAS_META_XR not defined -- Quest project requires Meta XR SDK"
#endif
        }

        /// <summary>
        /// Returns world position of the requested joint.
        /// Returns Vector3.zero if not available.
        /// </summary>
        public Vector3 GetJointWorldPosition(BodyJoint joint)
        {
            var t = GetJointTransform(joint);
            return t != null ? t.position : Vector3.zero;
        }

        // -- helpers --

#if HAS_META_XR
        private Transform CreateJointAnchor(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private void UpdateAnchorByName(Transform anchor, params string[] possibleNames)
        {
            if (anchor == null || bodyTrackingManager == null) return;

            foreach (var name in possibleNames)
            {
                if (System.Enum.TryParse<OVRSkeleton.BoneId>(name, out var boneId))
                {
                    if (bodyTrackingManager.TryGetJointPose(boneId, out OVRPlugin.Posef pose))
                    {
                        anchor.position = pose.Position.FromFlippedZVector3f();
                        anchor.rotation = pose.Orientation.FromFlippedZQuatf();
                        return;
                    }
                }
            }
        }
#endif
    }

    /// <summary>
    /// Key body joints exposed for gameplay use.
    /// Subset of OVRSkeleton.BoneId -- add more as needed.
    /// </summary>
    public enum BodyJoint
    {
        Head,
        LeftHand,
        RightHand,
        Hips,
        LeftFoot,
        RightFoot,
    }
}
