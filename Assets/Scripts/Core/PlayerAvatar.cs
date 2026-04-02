using UnityEngine;

/// <summary>
/// PlayerAvatar -- spawns PINEA rigged model as full body avatar.
/// Head follows HMD, hands follow controllers, legs via IK.
/// Head mesh hidden (first person -- don't see inside your own skull).
///
/// Auto-creates on scene load. Same model works for bots (AI-driven).
/// </summary>
public class PlayerAvatar : MonoBehaviour
{
    private const string AVATAR_PATH = "PLAGA44/Characters/PINEA/PINEA_rigged";

    private Animator _animator;
    private Transform _head;
    private Transform _leftHand;
    private Transform _rightHand;
    private Transform _hips;

    // VR anchors
    private Transform _hmdAnchor;
    private Transform _leftControllerAnchor;
    private Transform _rightControllerAnchor;

    // IK
    private float _ikWeight = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("_PlayerAvatar");
        go.AddComponent<PlayerAvatar>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        // Find OVR rig
        var rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig == null)
        {
            Debug.LogWarning("[PLAGA44] PlayerAvatar: no OVRCameraRig -- skipping");
            return;
        }

        // Get anchors
        _hmdAnchor = rig.centerEyeAnchor;
        _leftControllerAnchor = rig.leftHandAnchor;
        _rightControllerAnchor = rig.rightHandAnchor;

        if (_hmdAnchor == null)
        {
            Debug.LogError("[PLAGA44] PlayerAvatar: no centerEyeAnchor");
            return;
        }

        // Spawn avatar
        var prefab = Resources.Load<GameObject>(AVATAR_PATH);
        if (prefab == null)
        {
            // Try direct asset path
            Debug.LogWarning($"[PLAGA44] PlayerAvatar: '{AVATAR_PATH}' not in Resources. Searching scene...");
            return;
        }

        var avatar = Instantiate(prefab, rig.transform);
        avatar.name = "PlayerBody";
        avatar.transform.localPosition = Vector3.zero;
        avatar.transform.localRotation = Quaternion.identity;

        SetupAvatar(avatar);
    }

    void SetupAvatar(GameObject avatar)
    {
        _animator = avatar.GetComponent<Animator>();
        if (_animator == null || !_animator.isHuman)
        {
            Debug.LogError("[PLAGA44] PlayerAvatar: not a Humanoid animator!");
            return;
        }

        // Cache bone transforms
        _head = _animator.GetBoneTransform(HumanBodyBones.Head);
        _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
        _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
        _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);

        // Hide head (first person -- you don't see your own head)
        if (_head != null)
        {
            var headRenderers = _head.GetComponentsInChildren<Renderer>();
            foreach (var r in headRenderers)
            {
                // Don't disable entire renderer -- just scale head bone tiny
            }
            _head.localScale = Vector3.one * 0.01f;
            Debug.Log("[PLAGA44] PlayerAvatar: head hidden (scaled to 0.01)");
        }

        // Position hips at player feet
        if (_hips != null)
        {
            // Hips offset -- avatar stands on ground, hips ~0.95m up
            avatar.transform.localPosition = new Vector3(0, -_hmdAnchor.localPosition.y + 0.05f, 0);
        }

        Debug.Log($"[PLAGA44] PlayerAvatar: READY -- head={_head?.name}, " +
                  $"leftHand={_leftHand?.name}, rightHand={_rightHand?.name}, hips={_hips?.name}");
    }

    void LateUpdate()
    {
        if (_animator == null || _hmdAnchor == null) return;

        // Head follows HMD
        if (_head != null)
        {
            _head.position = _hmdAnchor.position;
            _head.rotation = _hmdAnchor.rotation;
        }

        // Hands follow controllers
        if (_leftHand != null && _leftControllerAnchor != null)
        {
            _leftHand.position = _leftControllerAnchor.position;
            _leftHand.rotation = _leftControllerAnchor.rotation;
        }

        if (_rightHand != null && _rightControllerAnchor != null)
        {
            _rightHand.position = _rightControllerAnchor.position;
            _rightHand.rotation = _rightControllerAnchor.rotation;
        }

        // Hips -- midpoint between head and estimated feet, facing head forward
        if (_hips != null && _head != null)
        {
            Vector3 headPos = _hmdAnchor.position;
            Vector3 hipPos = new Vector3(headPos.x, headPos.y - 0.7f, headPos.z);
            _hips.position = hipPos;

            // Hips face same horizontal direction as head
            Vector3 headFwd = _hmdAnchor.forward;
            headFwd.y = 0;
            if (headFwd.sqrMagnitude > 0.001f)
                _hips.rotation = Quaternion.LookRotation(headFwd, Vector3.up);
        }
    }

    // IK callback -- legs grounding
    void OnAnimatorIK(int layerIndex)
    {
        if (_animator == null) return;

        // Left foot
        _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, _ikWeight);
        _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, _ikWeight);
        Vector3 leftFootPos = EstimateFootPosition(true);
        _animator.SetIKPosition(AvatarIKGoal.LeftFoot, leftFootPos);
        _animator.SetIKRotation(AvatarIKGoal.LeftFoot, Quaternion.LookRotation(_hips.forward, Vector3.up));

        // Right foot
        _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, _ikWeight);
        _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, _ikWeight);
        Vector3 rightFootPos = EstimateFootPosition(false);
        _animator.SetIKPosition(AvatarIKGoal.RightFoot, rightFootPos);
        _animator.SetIKRotation(AvatarIKGoal.RightFoot, Quaternion.LookRotation(_hips.forward, Vector3.up));
    }

    Vector3 EstimateFootPosition(bool isLeft)
    {
        if (_hips == null) return Vector3.zero;

        float side = isLeft ? -0.12f : 0.12f;
        Vector3 hipPos = _hips.position;
        Vector3 footEstimate = new Vector3(
            hipPos.x + _hips.right.x * side,
            0, // ground level
            hipPos.z + _hips.right.z * side
        );

        // Raycast down for actual ground
        if (Physics.Raycast(footEstimate + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f))
        {
            footEstimate.y = hit.point.y + 0.02f;
        }

        return footEstimate;
    }
}
