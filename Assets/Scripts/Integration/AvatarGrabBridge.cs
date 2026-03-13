using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.Integration
{
    /// <summary>
    /// Avatar Grab Bridge -- synchronizes hand bone positions from OVRSkeleton (body tracking)
    /// with the Interaction SDK HandGrabInteractor during grab interactions.
    ///
    /// Current state: PLACEHOLDER
    ///   - Reads OVRSkeleton bone transforms from left/right OVRHand prefabs
    ///   - Exposes SyncHandBones() for future integration with ISDK Skeleton Processor
    ///   - Ready to receive CharacterRetargeter output bone positions
    ///
    /// Architecture (future):
    ///   OVRBody -> CharacterRetargeter -> Avatar skeleton
    ///                                          |
    ///                              ISDK Skeleton Processor
    ///                                          |
    ///                              HandGrabInteractor pose
    ///                                          |
    ///                                AvatarGrabBridge (this)
    ///                                          |
    ///                              Visual hand mesh / bone sync
    ///
    /// Related issue: #46 -- ISDK Integration: avatar chwyta przedmioty z body tracking
    /// Related issue: #28 -- Hand grab interactions (Interaction SDK)
    /// Related issue: #30 -- Body tracking (Movement SDK)
    /// </summary>
    public class AvatarGrabBridge : MonoBehaviour
    {
        private const string LOG = "[AvatarGrabBridge]";

        [Header("Source: OVR Skeleton (Body Tracking)")]
        [Tooltip("OVRSkeleton component on the left hand OVRHandPrefab. " +
                 "Provides bone positions from body tracking / controller-driven poses.")]
        public Component leftOVRSkeleton;  // Will be cast to OVRSkeleton at runtime

        [Tooltip("OVRSkeleton component on the right hand OVRHandPrefab.")]
        public Component rightOVRSkeleton;

        [Header("Target: Hand Rig Bones")]
        [Tooltip("Root transform of the left hand in the avatar rig. " +
                 "Bone children will be synced from OVRSkeleton output.")]
        public Transform leftHandRigRoot;

        [Tooltip("Root transform of the right hand in the avatar rig.")]
        public Transform rightHandRigRoot;

        [Header("Sync Settings")]
        [Tooltip("Sync hand bones every frame in LateUpdate (after animation). " +
                 "Disable to sync manually via SyncHandBones().")]
        public bool autoSyncEveryFrame = false;

        [Tooltip("Only sync when a grab is active. Reduces unnecessary work when hands are open.")]
        public bool syncOnlyDuringGrab = true;

        [Tooltip("Smoothing factor for bone position sync (0 = no smoothing, 1 = instant).")]
        [Range(0f, 1f)]
        public float syncSmoothFactor = 1.0f;

        // Runtime state
        private bool _leftGrabActive;
        private bool _rightGrabActive;
        private List<Transform> _leftSourceBones  = new List<Transform>();
        private List<Transform> _rightSourceBones = new List<Transform>();
        private List<Transform> _leftTargetBones  = new List<Transform>();
        private List<Transform> _rightTargetBones = new List<Transform>();

        void Start()
        {
            BuildBoneMaps();
        }

        void LateUpdate()
        {
            if (!autoSyncEveryFrame)
                return;

            bool shouldSyncLeft  = !syncOnlyDuringGrab || _leftGrabActive;
            bool shouldSyncRight = !syncOnlyDuringGrab || _rightGrabActive;

            if (shouldSyncLeft)
                SyncBoneList(_leftSourceBones, _leftTargetBones);

            if (shouldSyncRight)
                SyncBoneList(_rightSourceBones, _rightTargetBones);
        }

        // -------------------------------------------------------------------------
        // Public API (called by ISDKIntegrationManager or grab event handlers)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Sync all hand bones from OVRSkeleton to the avatar rig bones.
        /// Call this from grab start/update events, or enable autoSyncEveryFrame.
        /// </summary>
        public void SyncHandBones()
        {
            SyncBoneList(_leftSourceBones, _leftTargetBones);
            SyncBoneList(_rightSourceBones, _rightTargetBones);
        }

        /// <summary>
        /// Notify bridge that a grab started on the specified hand.
        /// Called by ISDKIntegrationManager when HandGrabInteractor fires grab event.
        /// </summary>
        public void OnGrabStarted(bool isLeftHand)
        {
            if (isLeftHand)
            {
                _leftGrabActive = true;
                Debug.Log($"{LOG} Left hand grab started -- bone sync activated.");
            }
            else
            {
                _rightGrabActive = true;
                Debug.Log($"{LOG} Right hand grab started -- bone sync activated.");
            }
        }

        /// <summary>
        /// Notify bridge that a grab ended on the specified hand.
        /// </summary>
        public void OnGrabEnded(bool isLeftHand)
        {
            if (isLeftHand)
            {
                _leftGrabActive = false;
                Debug.Log($"{LOG} Left hand grab ended.");
            }
            else
            {
                _rightGrabActive = false;
                Debug.Log($"{LOG} Right hand grab ended.");
            }
        }

        // -------------------------------------------------------------------------
        // Bone Map Building
        // -------------------------------------------------------------------------

        /// <summary>
        /// Build lists of matching source (OVRSkeleton) and target (avatar rig) bone transforms.
        /// PLACEHOLDER: currently collects all children by index.
        /// Future: match by OVRSkeleton.BoneId name against avatar rig bone names.
        /// </summary>
        private void BuildBoneMaps()
        {
            _leftSourceBones.Clear();
            _rightSourceBones.Clear();
            _leftTargetBones.Clear();
            _rightTargetBones.Clear();

            // Collect OVRSkeleton bones via reflection (avoids hard dependency on OVRSkeleton type)
            CollectSkeletonBones(leftOVRSkeleton,  _leftSourceBones,  "Left");
            CollectSkeletonBones(rightOVRSkeleton, _rightSourceBones, "Right");

            // Collect avatar rig bones
            if (leftHandRigRoot != null)
                CollectChildBones(leftHandRigRoot, _leftTargetBones);

            if (rightHandRigRoot != null)
                CollectChildBones(rightHandRigRoot, _rightTargetBones);

            Debug.Log($"{LOG} Bone maps built: " +
                      $"L-src={_leftSourceBones.Count} L-tgt={_leftTargetBones.Count} " +
                      $"R-src={_rightSourceBones.Count} R-tgt={_rightTargetBones.Count}");
        }

        /// <summary>
        /// Try to extract bone transforms from an OVRSkeleton via reflection.
        /// This avoids a compile-time dependency on OVRSkeleton.
        /// </summary>
        private void CollectSkeletonBones(Component skeleton, List<Transform> output, string side)
        {
            if (skeleton == null)
            {
                Debug.LogWarning($"{LOG} {side} OVRSkeleton not assigned.");
                return;
            }

            // PLACEHOLDER: With OVRSkeleton available, use:
            //   var ovrs = skeleton as OVRSkeleton;
            //   foreach (var bone in ovrs.Bones) output.Add(bone.Transform);

            // Reflection fallback -- works without OVRSkeleton in scope
            var bonesField = skeleton.GetType().GetProperty("Bones");
            if (bonesField != null)
            {
                var bones = bonesField.GetValue(skeleton) as System.Collections.IList;
                if (bones != null)
                {
                    foreach (var bone in bones)
                    {
                        var transformProp = bone.GetType().GetProperty("Transform");
                        if (transformProp != null)
                        {
                            var t = transformProp.GetValue(bone) as Transform;
                            if (t != null) output.Add(t);
                        }
                    }
                    Debug.Log($"{LOG} {side} skeleton: {output.Count} bones found via reflection.");
                    return;
                }
            }

            // Last resort: collect direct children of skeleton transform
            Debug.LogWarning($"{LOG} {side} OVRSkeleton.Bones not accessible. Falling back to children of {skeleton.name}.");
            CollectChildBones(skeleton.transform, output);
        }

        private void CollectChildBones(Transform root, List<Transform> output)
        {
            output.Add(root);
            foreach (Transform child in root)
                CollectChildBones(child, output);
        }

        // -------------------------------------------------------------------------
        // Sync
        // -------------------------------------------------------------------------

        private void SyncBoneList(List<Transform> sources, List<Transform> targets)
        {
            int count = Mathf.Min(sources.Count, targets.Count);
            for (int i = 0; i < count; i++)
            {
                if (sources[i] == null || targets[i] == null)
                    continue;

                if (syncSmoothFactor >= 1f)
                {
                    targets[i].position = sources[i].position;
                    targets[i].rotation = sources[i].rotation;
                }
                else
                {
                    float t = syncSmoothFactor;
                    targets[i].position = Vector3.Lerp(targets[i].position, sources[i].position, t);
                    targets[i].rotation = Quaternion.Slerp(targets[i].rotation, sources[i].rotation, t);
                }
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild Bone Maps")]
        private void EditorRebuildBoneMaps()
        {
            BuildBoneMaps();
        }

        [ContextMenu("Log Bone Map Status")]
        private void EditorLogStatus()
        {
            Debug.Log($"{LOG} Left  source bones : {_leftSourceBones.Count}");
            Debug.Log($"{LOG} Left  target bones : {_leftTargetBones.Count}");
            Debug.Log($"{LOG} Right source bones : {_rightSourceBones.Count}");
            Debug.Log($"{LOG} Right target bones : {_rightTargetBones.Count}");
            Debug.Log($"{LOG} Left  grab active  : {_leftGrabActive}");
            Debug.Log($"{LOG} Right grab active  : {_rightGrabActive}");
        }
#endif
    }
}
