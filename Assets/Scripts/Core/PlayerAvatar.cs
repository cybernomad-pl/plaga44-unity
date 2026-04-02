using UnityEngine;

/// <summary>
/// PlayerAvatar -- PINEA as full body. NO Animator -- direct bone driving.
/// Head=HMD, Hands=Controllers, Spine=interpolated, Legs=IK to ground.
/// Hides OVR hand models when avatar active.
/// </summary>
public class PlayerAvatar : MonoBehaviour
{
    private const string AVATAR_PATH = "PLAGA44/Characters/PINEA/PINEA_rigged";

    private Transform _root;
    private Animator _animator;

    // Bones
    private Transform _hips, _spine, _chest, _neck, _head;
    private Transform _leftShoulder, _leftUpperArm, _leftLowerArm, _leftHand;
    private Transform _rightShoulder, _rightUpperArm, _rightLowerArm, _rightHand;
    private Transform _leftUpperLeg, _leftLowerLeg, _leftFoot;
    private Transform _rightUpperLeg, _rightLowerLeg, _rightFoot;

    // VR anchors
    private Transform _hmd, _leftCtrl, _rightCtrl;
    private OVRCameraRig _rig;

    // Reference poses (from T-pose)
    private Quaternion _spineRef, _chestRef, _neckRef;
    private Quaternion _leftShoulderRef, _leftUpperArmRef, _leftLowerArmRef;
    private Quaternion _rightShoulderRef, _rightUpperArmRef, _rightLowerArmRef;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("_PlayerAvatar");
        go.AddComponent<PlayerAvatar>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        Debug.Log("[AVATAR] Start -- looking for OVRCameraRig...");
        _rig = FindAnyObjectByType<OVRCameraRig>();
        if (_rig == null) { Debug.LogWarning("[AVATAR] no OVRCameraRig"); return; }

        _hmd = _rig.centerEyeAnchor;
        _leftCtrl = _rig.leftHandAnchor;
        _rightCtrl = _rig.rightHandAnchor;

        // Spawn avatar
        var prefab = Resources.Load<GameObject>(AVATAR_PATH);
        if (prefab == null) { Debug.LogError($"[AVATAR] '{AVATAR_PATH}' not in Resources!"); return; }

        var avatar = Instantiate(prefab, _rig.transform);
        avatar.name = "PlayerBody";
        avatar.transform.localPosition = Vector3.zero;
        avatar.transform.localRotation = Quaternion.identity;
        avatar.transform.localScale = Vector3.one * 1.2f; // 20% bigger
        _root = avatar.transform;

        // Get animator, cache bones, then DISABLE animator
        _animator = avatar.GetComponent<Animator>();
        if (_animator != null && _animator.isHuman)
        {
            CacheBones();
            CacheReferencePoses();
            _animator.enabled = false; // KILL -- we drive bones directly
            Debug.Log("[AVATAR] Animator disabled -- direct bone driving");
        }
        else
        {
            Debug.LogError("[AVATAR] Not humanoid!");
            return;
        }

        // Hide head
        if (_head != null) _head.localScale = Vector3.one * 0.01f;

        // Hide OVR hand models
        HideOVRHands();

        Debug.Log("[AVATAR] READY -- bones cached, driving direct");
    }

    void CacheBones()
    {
        _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
        _spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
        _chest = _animator.GetBoneTransform(HumanBodyBones.Chest);
        _neck = _animator.GetBoneTransform(HumanBodyBones.Neck);
        _head = _animator.GetBoneTransform(HumanBodyBones.Head);
        _leftShoulder = _animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
        _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        _leftLowerArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
        _rightShoulder = _animator.GetBoneTransform(HumanBodyBones.RightShoulder);
        _rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        _rightLowerArm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
        _leftUpperLeg = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        _leftLowerLeg = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        _leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        _rightUpperLeg = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        _rightLowerLeg = _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        _rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
    }

    void CacheReferencePoses()
    {
        if (_spine) _spineRef = _spine.localRotation;
        if (_chest) _chestRef = _chest.localRotation;
        if (_neck) _neckRef = _neck.localRotation;
        if (_leftShoulder) _leftShoulderRef = _leftShoulder.localRotation;
        if (_leftUpperArm) _leftUpperArmRef = _leftUpperArm.localRotation;
        if (_leftLowerArm) _leftLowerArmRef = _leftLowerArm.localRotation;
        if (_rightShoulder) _rightShoulderRef = _rightShoulder.localRotation;
        if (_rightUpperArm) _rightUpperArmRef = _rightUpperArm.localRotation;
        if (_rightLowerArm) _rightLowerArmRef = _rightLowerArm.localRotation;
    }

    void HideOVRHands()
    {
        // Find and disable OVR hand renderers (the black controller hands)
        var handRenderers = _rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        int hidden = 0;
        foreach (var r in handRenderers)
        {
            string n = r.gameObject.name.ToLower();
            if (n.Contains("hand") || n.Contains("controller"))
            {
                r.enabled = false;
                hidden++;
            }
        }
        // Also try OVRControllerHelper
        var helpers = _rig.GetComponentsInChildren<OVRControllerHelper>(true);
        foreach (var h in helpers)
        {
            var renderers = h.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) r.enabled = false;
            hidden += renderers.Length;
        }
        Debug.Log($"[AVATAR] Hidden {hidden} OVR hand/controller renderers");
    }

    void LateUpdate()
    {
        if (_hips == null || _hmd == null) return;

        // === HIPS -- below head, facing head forward ===
        Vector3 headWorldPos = _hmd.position;
        Vector3 hipPos = new Vector3(headWorldPos.x, headWorldPos.y - 0.85f, headWorldPos.z);
        _hips.position = hipPos;

        Vector3 headFwd = _hmd.forward;
        headFwd.y = 0;
        if (headFwd.sqrMagnitude > 0.001f)
            _hips.rotation = Quaternion.LookRotation(headFwd, Vector3.up);

        // === SPINE CHAIN -- distribute head tilt across spine/chest/neck ===
        // Head pitch distributed: 30% spine, 30% chest, 40% neck
        float headPitch = _hmd.eulerAngles.x;
        if (headPitch > 180) headPitch -= 360; // normalize to -180..180

        if (_spine) _spine.localRotation = _spineRef * Quaternion.Euler(headPitch * 0.15f, 0, 0);
        if (_chest) _chest.localRotation = _chestRef * Quaternion.Euler(headPitch * 0.15f, 0, 0);
        if (_neck) _neck.localRotation = _neckRef * Quaternion.Euler(headPitch * 0.3f, 0, 0);
        if (_head)
        {
            _head.position = headWorldPos;
            _head.rotation = _hmd.rotation;
        }

        // === HANDS -- two-bone IK towards controllers ===
        if (_leftHand != null && _leftCtrl != null)
            SolveArmIK(_leftShoulder, _leftUpperArm, _leftLowerArm, _leftHand, _leftCtrl);

        if (_rightHand != null && _rightCtrl != null)
            SolveArmIK(_rightShoulder, _rightUpperArm, _rightLowerArm, _rightHand, _rightCtrl);

        // === LEGS -- IK to ground ===
        SolveLegIK(_leftUpperLeg, _leftLowerLeg, _leftFoot, -0.12f);
        SolveLegIK(_rightUpperLeg, _rightLowerLeg, _rightFoot, 0.12f);
    }

    void SolveArmIK(Transform shoulder, Transform upperArm, Transform lowerArm, Transform hand, Transform target)
    {
        if (upperArm == null || lowerArm == null || hand == null || target == null) return;

        // Simple: point hand at controller, bend elbow
        hand.position = target.position;
        hand.rotation = target.rotation;

        // Upper arm points toward hand
        Vector3 toHand = hand.position - upperArm.position;
        if (toHand.sqrMagnitude > 0.001f)
            upperArm.rotation = Quaternion.LookRotation(toHand, Vector3.up);

        // Lower arm points from elbow to hand
        Vector3 elbowToHand = hand.position - lowerArm.position;
        if (elbowToHand.sqrMagnitude > 0.001f)
            lowerArm.rotation = Quaternion.LookRotation(elbowToHand, Vector3.up);
    }

    void SolveLegIK(Transform upperLeg, Transform lowerLeg, Transform foot, float sideOffset)
    {
        if (upperLeg == null || lowerLeg == null || foot == null || _hips == null) return;

        // Foot target -- directly below hip, on ground
        Vector3 hipPos = _hips.position;
        Vector3 footTarget = new Vector3(
            hipPos.x + _hips.right.x * sideOffset,
            0,
            hipPos.z + _hips.right.z * sideOffset
        );

        // Raycast for ground
        if (Physics.Raycast(footTarget + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f))
            footTarget.y = hit.point.y + 0.02f;

        // Upper leg points toward foot
        Vector3 toFoot = footTarget - upperLeg.position;
        if (toFoot.sqrMagnitude > 0.001f)
        {
            upperLeg.rotation = Quaternion.LookRotation(toFoot, _hips.forward);
            upperLeg.Rotate(90, 0, 0, Space.Self); // leg bones point down in T-pose
        }

        // Lower leg
        Vector3 kneeToFoot = footTarget - lowerLeg.position;
        if (kneeToFoot.sqrMagnitude > 0.001f)
        {
            lowerLeg.rotation = Quaternion.LookRotation(kneeToFoot, _hips.forward);
            lowerLeg.Rotate(90, 0, 0, Space.Self);
        }

        // Foot flat on ground
        foot.rotation = Quaternion.LookRotation(_hips.forward, Vector3.up);
    }
}
