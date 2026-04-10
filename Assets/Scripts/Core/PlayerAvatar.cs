// PlayerAvatar.cs -- Avatar gracza podpiety pod OVRCameraRig.
// Laduje PLAYER_rigged z Resources, mapuje na rig.
// Retargeting bedzie dodany po dostarczeniu FBX z Mixamo.

using System.Collections.Generic;
using UnityEngine;

namespace Plaga44
{
    [DisallowMultipleComponent]
    public class PlayerAvatar : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Avatar]";

        public static PlayerAvatar Instance { get; private set; }

        [Header("Config")]
        public float modelScale = 0.655f;
        public float yOffset = 0f;
        public bool hideHeadInFirstPerson = true;

        private GameObject _avatarInstance;
        private readonly Dictionary<string, Renderer> _submeshRenderers = new Dictionary<string, Renderer>();

        public GameObject AvatarRoot => _avatarInstance;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void Start()
        {
            Debug.Log($"{LOG} Start");

            var existing = transform.Find("PlayerAvatarModel");
            if (existing != null)
            {
                _avatarInstance = existing.gameObject;
                Debug.Log($"{LOG} Model found: {_avatarInstance.name}");
            }
            else
            {
                SpawnAvatar();
            }

            if (_avatarInstance == null) return;

            IndexSubmeshes();
            Debug.Log($"{LOG} Ready, submeshes={_submeshRenderers.Count}");
        }

        private void LateUpdate()
        {
            if (_avatarInstance == null) return;
            _avatarInstance.transform.position = transform.position + Vector3.up * yOffset;
            float yaw = transform.eulerAngles.y;
            _avatarInstance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void SpawnAvatar()
        {
            var prefab = Resources.Load<GameObject>("PLAYER_rigged");
#if UNITY_EDITOR
            if (prefab == null)
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Player/PLAYER_rigged.fbx");
#endif
            if (prefab == null)
            {
                Debug.LogWarning($"{LOG} No PLAYER_rigged found -- avatar disabled");
                return;
            }

            _avatarInstance = Instantiate(prefab, transform.position, Quaternion.identity);
            _avatarInstance.name = "PlayerAvatar";
            _avatarInstance.transform.localScale = Vector3.one * modelScale;
            Debug.Log($"{LOG} Spawned: scale={modelScale}");
        }

        private void IndexSubmeshes()
        {
            if (_avatarInstance == null) return;
            _submeshRenderers.Clear();
            foreach (var r in _avatarInstance.GetComponentsInChildren<Renderer>(true))
                _submeshRenderers[r.gameObject.name] = r;
        }

        public void SetSubmeshVisible(string name, bool visible)
        {
            if (_submeshRenderers.TryGetValue(name, out var r))
                r.enabled = visible;
        }

        public bool IsSubmeshVisible(string name)
        {
            return _submeshRenderers.TryGetValue(name, out var r) && r.enabled;
        }

        public List<string> GetAllSubmeshNames() => new List<string>(_submeshRenderers.Keys);
    }
}
