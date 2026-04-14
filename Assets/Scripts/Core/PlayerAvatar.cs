using UnityEngine;

namespace Plaga44
{
    /// <summary>
    /// Third-person avatar for the player -- Survivor A Lusth (Mixamo humanoid).
    /// Spawned as a child of OVRCameraRig. Position follows rig, rotation follows head yaw.
    /// Head is hidden in first person to avoid clipping with the camera.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerAvatar : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Avatar]";
        private const string AVATAR_RESOURCE = "Survivor_A_Lusth";

        [Header("Avatar Config")]
        [Tooltip("FBX prefab -- auto-loaded from Resources if null")]
        public GameObject avatarPrefab;

        [Tooltip("Y offset of the avatar below the rig (avatar feet at rig base)")]
        public float yOffset = 0f;

        [Tooltip("Hide head in first person to avoid camera clipping")]
        public bool hideHead = true;

        private GameObject _instance;
        private Animator _animator;
        private Transform _headBone;

        private void Start()
        {
            if (avatarPrefab == null)
                avatarPrefab = Resources.Load<GameObject>(AVATAR_RESOURCE);

            if (avatarPrefab == null)
            {
                Debug.LogWarning($"{LOG} Avatar prefab not found. Expected in Resources/{AVATAR_RESOURCE}");
                enabled = false;
                return;
            }

            _instance = Instantiate(avatarPrefab, transform);
            _instance.name = "Avatar_" + avatarPrefab.name;
            _instance.transform.localPosition = new Vector3(0f, yOffset, 0f);

            _animator = _instance.GetComponent<Animator>();
            if (_animator != null && _animator.isHuman)
                _headBone = _animator.GetBoneTransform(HumanBodyBones.Head);

            Debug.Log($"{LOG} Spawned '{_instance.name}' (humanoid={(_animator != null && _animator.isHuman)})");
        }

        private void LateUpdate()
        {
            if (_instance == null) return;
            if (hideHead && _headBone != null)
                _headBone.localScale = Vector3.zero;
        }
    }
}
