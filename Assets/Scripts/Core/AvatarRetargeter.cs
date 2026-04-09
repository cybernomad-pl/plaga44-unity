// =============================================================================
// AvatarRetargeter.cs
// PLAGA '44 -- Retargeting ciala gracza z VR anchor'ow na kosciec Mixamo.
//
// STRATEGIE RETARGETINGU (w kolejnosci priorytetu):
//
// 1. QUEST BODY TRACKING (HAS_META_XR + OVRBody active):
//    OVRUnityHumanoidSkeletonRetargeter robi retargeting automatycznie.
//    Ten komponent w takim wypadku jedynie synchronizuje hips position.
//
// 2. VR ANCHOR IK (Quest/Editor z trackingiem glowy i rak):
//    - Head:  CenterEyeAnchor rotation -> mixamorig:Head + Neck
//    - Arms:  Two-bone IK z LeftHandAnchor/RightHandAnchor
//    - Hips:  Pozycja Y z head anchor (offset)
//    - Spine: Interpolacja rotacji head -> hips
//    - Legs:  Proceduralna animacja kroku (step animation)
//
// 3. EDITOR FALLBACK (bez headsetu):
//    - WASD input -> proceduralna animacja chodzenia
//    - Head z Camera.main
//
// BONE HIERARCHY (Mixamo humanoid, 22 kosci):
//   mixamorig:Hips
//     mixamorig:Spine
//       mixamorig:Spine1
//         mixamorig:Spine2
//           mixamorig:Neck
//             mixamorig:Head
//               mixamorig:HeadTop_End
//           mixamorig:LeftShoulder
//             mixamorig:LeftArm
//               mixamorig:LeftForeArm
//                 mixamorig:LeftHand
//           mixamorig:RightShoulder
//             mixamorig:RightArm
//               mixamorig:RightForeArm
//                 mixamorig:RightHand
//     mixamorig:LeftUpLeg
//       mixamorig:LeftLeg
//         mixamorig:LeftFoot
//           mixamorig:LeftToeBase
//     mixamorig:RightUpLeg
//       mixamorig:RightLeg
//         mixamorig:RightFoot
//           mixamorig:RightToeBase
//
// WYMAGANIA:
//   - Ten komponent powinien byc na tym samym GO co PlayerAvatar (OVRCameraRig root).
//   - Model PLAYER_rigged.fbx zaimportowany jako Humanoid z T-pose.
//   - Animator na modelu (dla humanoid bone mapping).
//
// UZYCIE:
//   Dodawany automatycznie przez SceneSetup.AddPlayerAvatar().
//   AvatarRetargeter.Initialize(avatarRoot, headAnchor, leftHand, rightHand);
// =============================================================================

using UnityEngine;

namespace Plaga44
{
    [DisallowMultipleComponent]
    public class AvatarRetargeter : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Retarget]";

        // =====================================================================
        // Config
        // =====================================================================

        [Header("IK Settings")]
        [Tooltip("Offset Y od glowy do hips (typ. 0.55-0.65 * height)")]
        public float headToHipsRatio = 0.60f;

        [Tooltip("Slerp factor dla spine interpolacji (0=hips rot, 1=head rot)")]
        [Range(0f, 1f)]
        public float spineFollowHead = 0.4f;

        [Tooltip("Predkosc proceduralnego kroku (cykl/sekunde)")]
        public float stepFrequency = 2.0f;

        [Tooltip("Dlugosc kroku w metrach")]
        public float stepLength = 0.35f;

        [Tooltip("Wysokosc podnoszenia stopy")]
        public float stepHeight = 0.08f;

        [Header("Debug")]
        public bool drawDebugGizmos = false;

        // =====================================================================
        // References (set by Initialize)
        // =====================================================================

        private GameObject _avatarRoot;
        private Transform _headAnchor;
        private Transform _leftHandAnchor;
        private Transform _rightHandAnchor;

        // =====================================================================
        // Bone cache
        // =====================================================================

        private Transform _hips;
        private Transform _spine;
        private Transform _spine1;
        private Transform _spine2;
        private Transform _neck;
        private Transform _head;

        private Transform _leftShoulder;
        private Transform _leftArm;   // upper arm
        private Transform _leftForeArm;
        private Transform _leftHand;

        private Transform _rightShoulder;
        private Transform _rightArm;
        private Transform _rightForeArm;
        private Transform _rightHand;

        private Transform _leftUpLeg;
        private Transform _leftLeg;
        private Transform _leftFoot;

        private Transform _rightUpLeg;
        private Transform _rightLeg;
        private Transform _rightFoot;

        // =====================================================================
        // IK working data
        // =====================================================================

        // T-pose reference rotations
        private Quaternion _tposeHipsRot;
        private Quaternion _tposeSpineRot;
        private Quaternion _tposeSpine1Rot;
        private Quaternion _tposeSpine2Rot;
        private Quaternion _tposeNeckRot;
        private Quaternion _tposeHeadRot;
        private Quaternion _tposeLeftShoulderRot;
        private Quaternion _tposeRightShoulderRot;

        // Limb lengths for IK
        private float _leftUpperArmLen;
        private float _leftForeArmLen;
        private float _rightUpperArmLen;
        private float _rightForeArmLen;
        private float _leftUpperLegLen;
        private float _leftLowerLegLen;
        private float _rightUpperLegLen;
        private float _rightLowerLegLen;

        // Procedural walk state
        private float _walkCycle = 0f;
        private Vector3 _lastPosition;
        private float _smoothSpeed = 0f;

        // State
        private bool _initialized = false;
        private float _modelScale = 1f;

        // Reserved for future OVR body tracking integration.
        // When OVRBody provides full body data, this retargeter can defer to
        // OVRUnityHumanoidSkeletonRetargeter for higher quality results.

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Initialize the retargeter with references to the avatar model and VR anchors.
        /// Called by PlayerAvatar after spawning the model.
        /// </summary>
        public void Initialize(GameObject avatarRoot, Transform headAnchor,
                               Transform leftHandAnchor, Transform rightHandAnchor)
        {
            _avatarRoot = avatarRoot;
            _headAnchor = headAnchor;
            _leftHandAnchor = leftHandAnchor;
            _rightHandAnchor = rightHandAnchor;

            if (_avatarRoot == null)
            {
                Debug.LogError($"{LOG} Initialize: avatarRoot is null!");
                return;
            }

            _modelScale = _avatarRoot.transform.localScale.x;
            Debug.Log($"{LOG} Initialize: model={_avatarRoot.name}, scale={_modelScale}");

            CacheBones();
            CacheTPoseRotations();
            CacheLimbLengths();

            _lastPosition = transform.position;

            _initialized = true;
            Debug.Log($"{LOG} Initialized. Bones cached, IK ready.");
        }

        /// <summary>True after successful Initialize().</summary>
        public bool IsInitialized => _initialized;

        // =====================================================================
        // Update (called by PlayerAvatar.LateUpdate after model positioning)
        // =====================================================================

        /// <summary>
        /// Perform one frame of retargeting. Called by PlayerAvatar.LateUpdate()
        /// AFTER the avatar root position and rotation are set. Do NOT call from
        /// Unity's LateUpdate directly -- order of execution is not guaranteed.
        /// </summary>
        public void UpdateRetargeting()
        {
            if (!_initialized || _avatarRoot == null) return;

            // Calculate movement speed for procedural legs
            Vector3 currentPos = transform.position;
            float deltaMove = Vector3.Distance(
                new Vector3(currentPos.x, 0, currentPos.z),
                new Vector3(_lastPosition.x, 0, _lastPosition.z));
            float instantSpeed = deltaMove / Mathf.Max(Time.deltaTime, 0.001f);
            _smoothSpeed = Mathf.Lerp(_smoothSpeed, instantSpeed, Time.deltaTime * 8f);
            _lastPosition = currentPos;

            // Core retargeting pipeline
            RetargetHips();
            RetargetSpine();
            RetargetHead();
            RetargetArm(_leftShoulder, _leftArm, _leftForeArm, _leftHand,
                        _leftHandAnchor, _leftUpperArmLen, _leftForeArmLen,
                        _tposeLeftShoulderRot, isLeft: true);
            RetargetArm(_rightShoulder, _rightArm, _rightForeArm, _rightHand,
                        _rightHandAnchor, _rightUpperArmLen, _rightForeArmLen,
                        _tposeRightShoulderRot, isLeft: false);
            RetargetLegs();
        }

        // =====================================================================
        // Bone caching
        // =====================================================================

        private void CacheBones()
        {
            // ZAWSZE szukaj po nazwie -- Animator humanoid mapping czesto zwraca null
            // na prefab instances. FindBone rekurencyjnie przeszukuje hierarchie.
            {
                // Szukaj obu wariantow: z i bez prefixu mixamorig:
                Debug.Log($"{LOG} Finding bones by name...");
                _hips = FindBone("Hips") ?? FindBone("mixamorig:Hips");
                _spine = FindBone("Spine") ?? FindBone("mixamorig:Spine");
                _spine1 = FindBone("Spine1") ?? FindBone("mixamorig:Spine1");
                _spine2 = FindBone("Spine2") ?? FindBone("mixamorig:Spine2");
                _neck = FindBone("Neck") ?? FindBone("mixamorig:Neck");
                _head = FindBone("Head") ?? FindBone("mixamorig:Head");

                _leftShoulder = FindBone("LeftShoulder") ?? FindBone("mixamorig:LeftShoulder");
                _leftArm = FindBone("LeftArm") ?? FindBone("mixamorig:LeftArm");
                _leftForeArm = FindBone("LeftForeArm") ?? FindBone("mixamorig:LeftForeArm");
                _leftHand = FindBone("LeftHand") ?? FindBone("mixamorig:LeftHand");

                _rightShoulder = FindBone("RightShoulder") ?? FindBone("mixamorig:RightShoulder");
                _rightArm = FindBone("RightArm") ?? FindBone("mixamorig:RightArm");
                _rightForeArm = FindBone("RightForeArm") ?? FindBone("mixamorig:RightForeArm");
                _rightHand = FindBone("RightHand") ?? FindBone("mixamorig:RightHand");

                _leftUpLeg = FindBone("LeftUpLeg") ?? FindBone("mixamorig:LeftUpLeg");
                _leftLeg = FindBone("LeftLeg") ?? FindBone("mixamorig:LeftLeg");
                _leftFoot = FindBone("LeftFoot") ?? FindBone("mixamorig:LeftFoot");

                _rightUpLeg = FindBone("RightUpLeg") ?? FindBone("mixamorig:RightUpLeg");
                _rightLeg = FindBone("RightLeg") ?? FindBone("mixamorig:RightLeg");
                _rightFoot = FindBone("RightFoot") ?? FindBone("mixamorig:RightFoot");
            }

            LogBoneStatus();
        }

        private void CacheTPoseRotations()
        {
            if (_hips != null) _tposeHipsRot = _hips.localRotation;
            if (_spine != null) _tposeSpineRot = _spine.localRotation;
            if (_spine1 != null) _tposeSpine1Rot = _spine1.localRotation;
            if (_spine2 != null) _tposeSpine2Rot = _spine2.localRotation;
            if (_neck != null) _tposeNeckRot = _neck.localRotation;
            if (_head != null) _tposeHeadRot = _head.localRotation;
            if (_leftShoulder != null) _tposeLeftShoulderRot = _leftShoulder.localRotation;
            if (_rightShoulder != null) _tposeRightShoulderRot = _rightShoulder.localRotation;
        }

        private void CacheLimbLengths()
        {
            _leftUpperArmLen = BoneLength(_leftArm, _leftForeArm);
            _leftForeArmLen = BoneLength(_leftForeArm, _leftHand);
            _rightUpperArmLen = BoneLength(_rightArm, _rightForeArm);
            _rightForeArmLen = BoneLength(_rightForeArm, _rightHand);

            _leftUpperLegLen = BoneLength(_leftUpLeg, _leftLeg);
            _leftLowerLegLen = BoneLength(_leftLeg, _leftFoot);
            _rightUpperLegLen = BoneLength(_rightUpLeg, _rightLeg);
            _rightLowerLegLen = BoneLength(_rightLeg, _rightFoot);

            Debug.Log($"{LOG} Limb lengths: " +
                      $"L.Arm={_leftUpperArmLen:F3}+{_leftForeArmLen:F3}, " +
                      $"R.Arm={_rightUpperArmLen:F3}+{_rightForeArmLen:F3}, " +
                      $"L.Leg={_leftUpperLegLen:F3}+{_leftLowerLegLen:F3}, " +
                      $"R.Leg={_rightUpperLegLen:F3}+{_rightLowerLegLen:F3}");
        }

        // =====================================================================
        // HIPS -- position from head anchor
        // =====================================================================

        private void RetargetHips()
        {
            if (_hips == null || _headAnchor == null) return;

            // Hips Y = head Y - headToHipsRatio * playerHeight
            // We estimate player height from head anchor Y relative to rig root
            float headLocalY = _headAnchor.position.y - transform.position.y;
            float hipsY = headLocalY * (1f - headToHipsRatio);

            // Hips XZ follows head with slight offset backward
            Vector3 headFwd = _headAnchor.forward;
            headFwd.y = 0;
            headFwd.Normalize();

            Vector3 hipsWorldPos = new Vector3(
                _headAnchor.position.x - headFwd.x * 0.05f,
                transform.position.y + hipsY,
                _headAnchor.position.z - headFwd.z * 0.05f
            );

            // Convert to local space of avatar
            _hips.position = hipsWorldPos;

            // Hips rotation: yaw follows head (no pitch/roll)
            float headYaw = _headAnchor.eulerAngles.y;
            _hips.rotation = Quaternion.Euler(0f, headYaw, 0f) * _tposeHipsRot;
        }

        // =====================================================================
        // SPINE -- interpolate between hips and head rotation
        // =====================================================================

        private void RetargetSpine()
        {
            if (_hips == null || _headAnchor == null) return;

            // Extract head pitch and yaw (relative to hips yaw)
            float hipsYaw = _hips.eulerAngles.y;
            Quaternion hipsWorldRot = Quaternion.Euler(0f, hipsYaw, 0f);
            Quaternion headWorldRot = _headAnchor.rotation;

            // Relative rotation from hips to head
            Quaternion hipsToHead = Quaternion.Inverse(hipsWorldRot) * headWorldRot;

            // Distribute across spine chain: spine, spine1, spine2 each get a fraction
            float[] weights = { 0.15f, 0.25f, 0.35f }; // spine, spine1, spine2
            Transform[] spines = { _spine, _spine1, _spine2 };
            Quaternion[] tposes = { _tposeSpineRot, _tposeSpine1Rot, _tposeSpine2Rot };

            for (int i = 0; i < 3; i++)
            {
                if (spines[i] == null) continue;
                float w = weights[i] * spineFollowHead;
                Quaternion partial = Quaternion.Slerp(Quaternion.identity, hipsToHead, w);
                spines[i].localRotation = tposes[i] * partial;
            }
        }

        // =====================================================================
        // HEAD + NECK -- follow head anchor rotation
        // =====================================================================

        private void RetargetHead()
        {
            if (_head == null || _headAnchor == null) return;

            // Head rotation = head anchor rotation in world space
            // But we need to compensate for avatar root rotation and spine chain
            Quaternion parentWorldRot = _head.parent != null ? _head.parent.rotation : Quaternion.identity;
            Quaternion targetLocal = Quaternion.Inverse(parentWorldRot) * _headAnchor.rotation;
            _head.localRotation = targetLocal;

            // Neck gets partial head rotation
            if (_neck != null)
            {
                Quaternion neckParentRot = _neck.parent != null ? _neck.parent.rotation : Quaternion.identity;
                Quaternion neckTarget = Quaternion.Inverse(neckParentRot) * _headAnchor.rotation;
                _neck.localRotation = Quaternion.Slerp(_tposeNeckRot, neckTarget, 0.4f);
            }
        }

        // =====================================================================
        // ARMS -- Two-bone IK
        // =====================================================================

        private void RetargetArm(Transform shoulder, Transform upperArm, Transform foreArm,
                                 Transform hand, Transform handAnchor,
                                 float upperLen, float foreLen,
                                 Quaternion tposeShoulder, bool isLeft)
        {
            if (upperArm == null || foreArm == null || hand == null) return;

            // If no hand anchor, keep T-pose
            if (handAnchor == null) return;

            // Shoulder: slight rotation toward target
            if (shoulder != null)
            {
                Vector3 toTarget = handAnchor.position - shoulder.position;
                if (toTarget.sqrMagnitude > 0.001f)
                {
                    // Shoulder lifts slightly when arm is raised
                    float armRaise = Mathf.Clamp01(
                        (handAnchor.position.y - shoulder.position.y) /
                        (upperLen + foreLen + 0.01f));
                    Quaternion shoulderOffset = Quaternion.Euler(
                        0f,
                        0f,
                        isLeft ? -armRaise * 30f : armRaise * 30f);
                    shoulder.localRotation = tposeShoulder * shoulderOffset;
                }
            }

            // Two-bone IK: upperArm -> foreArm -> hand targets handAnchor position
            Vector3 targetPos = handAnchor.position;
            SolveTwoBoneIK(upperArm, foreArm, hand, targetPos,
                           upperLen, foreLen, isLeft);

            // Hand rotation follows anchor
            hand.rotation = handAnchor.rotation;
        }

        /// <summary>
        /// Analytic two-bone IK solver.
        /// Positions upper and lower bones so that the end effector (hand/foot)
        /// reaches the target position. Uses law of cosines for elbow/knee angle.
        /// </summary>
        private void SolveTwoBoneIK(Transform upper, Transform lower, Transform end,
                                     Vector3 target, float upperLen, float lowerLen,
                                     bool bendBack)
        {
            if (upper == null || lower == null) return;

            Vector3 upperPos = upper.position;
            Vector3 toTarget = target - upperPos;
            float targetDist = toTarget.magnitude;

            // Clamp target distance to reachable range
            float totalLen = upperLen + lowerLen;
            if (targetDist > totalLen * 0.999f)
                targetDist = totalLen * 0.999f;
            if (targetDist < Mathf.Abs(upperLen - lowerLen) * 1.001f)
                targetDist = Mathf.Abs(upperLen - lowerLen) * 1.001f;

            // Law of cosines: angle at upper bone
            float cosUpper = (upperLen * upperLen + targetDist * targetDist - lowerLen * lowerLen)
                             / (2f * upperLen * targetDist + 0.0001f);
            cosUpper = Mathf.Clamp(cosUpper, -1f, 1f);
            float angleUpper = Mathf.Acos(cosUpper);

            // Direction to target
            Vector3 targetDir = toTarget.normalized;

            // Pole vector (hint for elbow/knee direction)
            // Arms: elbow bends backward (-forward of upper arm parent)
            // Legs would bend forward
            Vector3 poleDir;
            if (upper.parent != null)
            {
                // For arms, bend elbow backward (negative Z of character)
                poleDir = -_avatarRoot.transform.forward;
                if (!bendBack) poleDir = _avatarRoot.transform.forward;
            }
            else
            {
                poleDir = Vector3.back;
            }

            // Create rotation plane
            Vector3 perpendicular = Vector3.Cross(targetDir, poleDir).normalized;
            if (perpendicular.sqrMagnitude < 0.001f)
                perpendicular = Vector3.Cross(targetDir, Vector3.up).normalized;

            // Rotate upper bone
            Quaternion lookRot = Quaternion.LookRotation(targetDir, Vector3.Cross(targetDir, perpendicular));
            Quaternion upperRot = lookRot * Quaternion.AngleAxis(-angleUpper * Mathf.Rad2Deg, Vector3.right);
            upper.rotation = upperRot;

            // Lower bone: angle at joint
            float cosLower = (upperLen * upperLen + lowerLen * lowerLen - targetDist * targetDist)
                             / (2f * upperLen * lowerLen + 0.0001f);
            cosLower = Mathf.Clamp(cosLower, -1f, 1f);
            float angleLower = Mathf.Acos(cosLower);

            // Lower bone looks from its position toward the target
            Vector3 lowerToTarget = target - lower.position;
            if (lowerToTarget.sqrMagnitude > 0.001f)
            {
                lower.rotation = Quaternion.LookRotation(lowerToTarget.normalized,
                                     Vector3.Cross(lowerToTarget.normalized, perpendicular));
            }
        }

        // =====================================================================
        // LEGS -- Procedural walk animation
        // =====================================================================

        private void RetargetLegs()
        {
            if (_leftUpLeg == null || _rightUpLeg == null) return;
            if (_leftLeg == null || _rightLeg == null) return;
            if (_leftFoot == null || _rightFoot == null) return;
            if (_hips == null) return;

            // Advance walk cycle based on movement speed
            bool isMoving = _smoothSpeed > 0.15f;

            if (isMoving)
            {
                _walkCycle += Time.deltaTime * stepFrequency *
                              Mathf.Clamp(_smoothSpeed / 2.5f, 0.5f, 2f);
                if (_walkCycle > 1f) _walkCycle -= 1f;
            }
            else
            {
                // Smoothly return to idle pose
                _walkCycle = Mathf.Lerp(_walkCycle, 0f, Time.deltaTime * 4f);
            }

            // Movement direction for foot placement
            Vector3 moveDir = _avatarRoot.transform.forward;
            Vector3 hipsRight = _avatarRoot.transform.right;

            // Left leg: cycle phase 0.0
            // Right leg: cycle phase 0.5 (opposite)
            AnimateLeg(_leftUpLeg, _leftLeg, _leftFoot,
                       _leftUpperLegLen, _leftLowerLegLen,
                       _walkCycle, moveDir, hipsRight, isLeft: true, isMoving: isMoving);

            AnimateLeg(_rightUpLeg, _rightLeg, _rightFoot,
                       _rightUpperLegLen, _rightLowerLegLen,
                       (_walkCycle + 0.5f) % 1f, moveDir, hipsRight, isLeft: false, isMoving: isMoving);
        }

        private void AnimateLeg(Transform upLeg, Transform leg, Transform foot,
                                float upperLen, float lowerLen,
                                float phase, Vector3 moveDir, Vector3 hipsRight,
                                bool isLeft, bool isMoving)
        {
            if (_hips == null) return;

            // Hip joint position (where the leg starts)
            float hipSpread = 0.08f * _modelScale;
            Vector3 hipOffset = isLeft ? -hipsRight * hipSpread : hipsRight * hipSpread;
            Vector3 hipPos = _hips.position + hipOffset;

            // Ground plane Y (assume flat ground at rig root Y)
            float groundY = transform.position.y;

            if (!isMoving)
            {
                // Idle pose: legs straight down, slight bend
                Vector3 idleFootPos = hipPos + Vector3.down * (upperLen + lowerLen) * 0.97f;
                idleFootPos.y = Mathf.Max(idleFootPos.y, groundY);

                SolveTwoBoneIK(upLeg, leg, foot, idleFootPos, upperLen, lowerLen, bendBack: false);
                // Foot flat on ground
                foot.rotation = Quaternion.Euler(0f, _avatarRoot.transform.eulerAngles.y, 0f);
                return;
            }

            // Walking: sinusoidal step pattern
            // Phase 0.0-0.5: swing phase (foot moving forward + up)
            // Phase 0.5-1.0: stance phase (foot on ground, moving backward)
            float sinPhase = Mathf.Sin(phase * Mathf.PI * 2f);
            float cosPhase = Mathf.Cos(phase * Mathf.PI * 2f);

            // Forward/backward offset
            float forwardOffset = sinPhase * stepLength * _modelScale;
            // Vertical offset (foot lifts during swing phase, front half of cycle)
            float verticalOffset = Mathf.Max(0f, cosPhase) * stepHeight * _modelScale;

            Vector3 footTarget = hipPos
                + moveDir * forwardOffset
                + Vector3.down * (upperLen + lowerLen - verticalOffset) * 0.95f;

            footTarget.y = Mathf.Max(footTarget.y, groundY);

            SolveTwoBoneIK(upLeg, leg, foot, footTarget, upperLen, lowerLen, bendBack: false);

            // Foot rotation: follow ground with slight pitch during step
            float footPitch = sinPhase * -10f; // toe lifts when stepping forward
            foot.rotation = Quaternion.Euler(footPitch, _avatarRoot.transform.eulerAngles.y, 0f);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private Transform FindBone(string name)
        {
            if (_avatarRoot == null) return null;
            return FindBoneRecursive(_avatarRoot.transform, name);
        }

        private Transform FindBoneRecursive(Transform parent, string boneName)
        {
            if (parent.name == boneName) return parent;
            foreach (Transform child in parent)
            {
                var found = FindBoneRecursive(child, boneName);
                if (found != null) return found;
            }
            return null;
        }

        private float BoneLength(Transform from, Transform to)
        {
            if (from == null || to == null) return 0.3f; // fallback
            return Vector3.Distance(from.position, to.position);
        }

        private void LogBoneStatus()
        {
            int found = 0;
            int total = 20;

            if (_hips != null) found++; else Debug.LogWarning($"{LOG} Missing: Hips");
            if (_spine != null) found++; else Debug.LogWarning($"{LOG} Missing: Spine");
            if (_spine1 != null) found++; else Debug.LogWarning($"{LOG} Missing: Spine1/Chest");
            if (_spine2 != null) found++; else Debug.LogWarning($"{LOG} Missing: Spine2/UpperChest");
            if (_neck != null) found++; else Debug.LogWarning($"{LOG} Missing: Neck");
            if (_head != null) found++; else Debug.LogWarning($"{LOG} Missing: Head");

            if (_leftShoulder != null) found++;
            if (_leftArm != null) found++; else Debug.LogWarning($"{LOG} Missing: LeftArm");
            if (_leftForeArm != null) found++; else Debug.LogWarning($"{LOG} Missing: LeftForeArm");
            if (_leftHand != null) found++; else Debug.LogWarning($"{LOG} Missing: LeftHand");

            if (_rightShoulder != null) found++;
            if (_rightArm != null) found++; else Debug.LogWarning($"{LOG} Missing: RightArm");
            if (_rightForeArm != null) found++; else Debug.LogWarning($"{LOG} Missing: RightForeArm");
            if (_rightHand != null) found++; else Debug.LogWarning($"{LOG} Missing: RightHand");

            if (_leftUpLeg != null) found++; else Debug.LogWarning($"{LOG} Missing: LeftUpLeg");
            if (_leftLeg != null) found++; else Debug.LogWarning($"{LOG} Missing: LeftLeg");
            if (_leftFoot != null) found++; else Debug.LogWarning($"{LOG} Missing: LeftFoot");

            if (_rightUpLeg != null) found++; else Debug.LogWarning($"{LOG} Missing: RightUpLeg");
            if (_rightLeg != null) found++; else Debug.LogWarning($"{LOG} Missing: RightLeg");
            if (_rightFoot != null) found++; else Debug.LogWarning($"{LOG} Missing: RightFoot");

            Debug.Log($"{LOG} Bones: {found}/{total} found");
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos || !_initialized) return;

            // Draw bone chain
            Gizmos.color = Color.cyan;
            DrawBoneLink(_hips, _spine);
            DrawBoneLink(_spine, _spine1);
            DrawBoneLink(_spine1, _spine2);
            DrawBoneLink(_spine2, _neck);
            DrawBoneLink(_neck, _head);

            Gizmos.color = Color.yellow;
            DrawBoneLink(_leftShoulder, _leftArm);
            DrawBoneLink(_leftArm, _leftForeArm);
            DrawBoneLink(_leftForeArm, _leftHand);

            DrawBoneLink(_rightShoulder, _rightArm);
            DrawBoneLink(_rightArm, _rightForeArm);
            DrawBoneLink(_rightForeArm, _rightHand);

            Gizmos.color = Color.green;
            DrawBoneLink(_leftUpLeg, _leftLeg);
            DrawBoneLink(_leftLeg, _leftFoot);
            DrawBoneLink(_rightUpLeg, _rightLeg);
            DrawBoneLink(_rightLeg, _rightFoot);

            // Draw targets
            if (_headAnchor != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_headAnchor.position, 0.05f);
            }
            if (_leftHandAnchor != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(_leftHandAnchor.position, 0.03f);
            }
            if (_rightHandAnchor != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(_rightHandAnchor.position, 0.03f);
            }
        }

        private void DrawBoneLink(Transform a, Transform b)
        {
            if (a != null && b != null)
                Gizmos.DrawLine(a.position, b.position);
        }
#endif
    }
}
