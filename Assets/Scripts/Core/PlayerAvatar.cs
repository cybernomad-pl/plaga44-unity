// =============================================================================
// PlayerAvatar.cs
// CYBERNOMAD -- kontroler avatara gracza na OVRCameraRig.
// Deleguje liste avatarow do AvatarGallery (single source of truth).
// Mode=0 -> default rig (SDK StylizedCharacterLocomotion). Mode>=1 -> avatar z Gallery (indeks - 1).
// =============================================================================
using UnityEngine;

namespace Plaga44
{
    /// <summary>
    /// Player avatar controller. Default state = SDK rig visible (StylizedCharacterLocomotion).
    /// Avatar is optional, selected from HamburgerMenu > AVATAR tile.
    ///
    /// Modes:
    ///   0             -- None (default rig visible, SDK body with hand tracking)
    ///   1..N          -- avatar z AvatarGallery.Instance (index = mode - 1)
    ///
    /// Player-compatible avatars must hide head + face (prevents camera clipping from inside).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerAvatar : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Avatar]";

        [Header("Mode")]
        [Tooltip("0=None (default rig). 1..N = avatar z AvatarGallery (index = mode-1). Max dynamicznie z Gallery.Count+1.")]
        public int avatarMode = 0;

        [Header("Scene refs")]
        [Tooltip("Default rig GameObject w scenie (np. StylizedCharacterLocomotion). Widoczny przy mode=0.")]
        public GameObject defaultRig;

        [Header("Avatar spawn config")]
        [Tooltip("Override prefab -- jesli ustawiony, spawnuje to zamiast z Gallery (tylko dla mode>=1).")]
        public GameObject avatarPrefab;

        [Tooltip("Y offset od rig base (avatar stopy przy ziemi)")]
        public float yOffset = 0f;

        [Tooltip("Hide head + face bones w first person (anti-clip)")]
        public bool hideHead = true;

        private GameObject _instance;
        private int _spawnedMode = -1;
        private Animator _animator;
        private Transform _headBone;
        private Transform _neckBone;

        public int CurrentMode => avatarMode;

        // Cache zeby UI nie szukal co frame. Unity's null check laga null-po-destroy.
        private static PlayerAvatar _cached;
        public static PlayerAvatar FindCurrent()
        {
            if (_cached == null) _cached = Object.FindAnyObjectByType<PlayerAvatar>();
            return _cached;
        }

        /// <summary>Max valid mode = 0 (None) + Gallery.Count. Uzywane przez SettingsRegistry.</summary>
        public int MaxMode
        {
            get
            {
                var g = AvatarGallery.Instance;
                return (g != null && g.Count > 0) ? g.Count : 1;
            }
        }

        private const string AvatarPrefsKey = "Plaga44_Current_AVATAR_Mode";

        private void Start()
        {
            // Restore persisted avatar mode (SettingsRegistry may not be built yet)
            if (PlayerPrefs.HasKey(AvatarPrefsKey))
                avatarMode = (int)PlayerPrefs.GetFloat(AvatarPrefsKey, 0f);
            SetAvatarMode(avatarMode);
        }

        /// <summary>
        /// Preview avatar in gallery (does NOT swap yet). Called from slider.
        /// </summary>
        public void PreviewAvatarMode(int mode)
        {
            mode = Mathf.Clamp(mode, 0, MaxMode);
            avatarMode = mode;
            // Only update gallery preview -- don't spawn on player
            var g = AvatarGallery.Instance;
            if (g != null) g.SetActiveIndex(mode > 0 ? mode - 1 : -1);
            Debug.Log($"{LOG} Preview mode={mode} ({CurrentLabel})");
        }

        /// <summary>
        /// Actually SWAP avatar onto player. Called on confirm / GoBack from AVATAR section.
        /// </summary>
        public void SetAvatarMode(int mode)
        {
            mode = Mathf.Clamp(mode, 0, MaxMode);
            avatarMode = mode;

            if (mode == 0) ShowDefaultRigOnly();
            else { ShowDefaultRig(false); SpawnAvatar(mode); }
        }

        /// <summary>Apply currently previewed mode as the actual avatar.</summary>
        public void ConfirmPreview()
        {
            SetAvatarMode(avatarMode);
            Debug.Log($"{LOG} Confirmed avatar mode={avatarMode} ({CurrentLabel})");
        }

        private void ShowDefaultRigOnly()
        {
            DespawnAvatar();
            ShowDefaultRig(true);
            DeactivateGalleryPreviews();
            Debug.Log($"{LOG} Mode=None -- default rig visible, no avatar spawned");
        }

        private static void DeactivateGalleryPreviews()
        {
            if (AvatarGallery.Instance != null) AvatarGallery.Instance.SetActiveIndex(-1);
        }

        private void ShowDefaultRig(bool visible)
        {
            if (defaultRig == null) return;
            if (defaultRig.activeSelf != visible) defaultRig.SetActive(visible);
        }

        /// <summary>True jesli aktualnie wybrany mode wskazuje na broken entry w registry.
        /// SettingsRegistry/HamburgerMenu uzywaja tego do pokazania "AVATAR_ERROR" na czerwono.</summary>
        public bool IsCurrentBroken
        {
            get
            {
                if (avatarMode == 0) return false;
                var g = AvatarGallery.Instance;
                if (g == null) return false;
                int idx = avatarMode - 1;
                return g.IsBroken(idx);
            }
        }

        /// <summary>Nazwa wyswietlana dla aktualnego mode (None / nazwa avatara / AVATAR_ERROR).</summary>
        public string CurrentLabel
        {
            get
            {
                if (avatarMode == 0) return "None";
                var g = AvatarGallery.Instance;
                if (g == null || g.Count == 0) return "?";
                int idx = avatarMode - 1;
                if (idx < 0 || idx >= g.Count) return "?";
                if (g.IsBroken(idx)) return "AVATAR_ERROR";
                return g.GetName(idx);
            }
        }

        private GameObject ResolvePrefabForMode(int mode)
        {
            // 1. Explicit override
            if (avatarPrefab != null) return avatarPrefab;

            // 2. Gallery (primary source)
            var g = AvatarGallery.Instance;
            if (g != null && g.Count > 0)
            {
                int idx = mode - 1; // mode 1 -> gallery[0]
                if (g.IsBroken(idx))
                {
                    Debug.LogError($"{LOG} AVATAR_ERROR -- mode={mode} broken: {g.GetError(idx)}");
                    return null; // skip broken -- caller falls back to None
                }
                var p = g.GetPrefab(idx);
                if (p != null) return p;
                Debug.LogWarning($"{LOG} Gallery has no prefab at index {idx} (mode={mode})");
            }

            return null;
        }

        private void SpawnAvatar(int mode)
        {
            if (_instance != null && _spawnedMode == mode) return;
            if (_instance != null) DespawnAvatar();

            var prefab = ResolvePrefabForMode(mode);
            if (prefab == null) { FallbackToNone(mode); return; }

            InstantiateAvatar(prefab, mode);
            CacheAnimatorBones();
            SyncGalleryActiveIndex(mode);

            Debug.Log($"{LOG} Spawned '{_instance.name}' mode={mode} (humanoid={(_animator != null && _animator.isHuman)})");
        }

        private void FallbackToNone(int mode)
        {
            Debug.LogWarning($"{LOG} No prefab for mode={mode}. Falling back to None.");
            avatarMode = 0;
            ShowDefaultRig(true);
            DeactivateGalleryPreviews();
        }

        private const float TargetAvatarHeight = 1.8f;

        private void InstantiateAvatar(GameObject prefab, int mode)
        {
            _instance = Instantiate(prefab, transform);
            _instance.name = "Avatar_" + prefab.name;
            _instance.transform.localPosition = new Vector3(0f, yOffset, 0f);
            NormalizeToHeight(_instance, TargetAvatarHeight);
            _spawnedMode = mode;
        }

        private static void NormalizeToHeight(GameObject inst, float targetHeight)
        {
            var renderers = inst.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"[PLAGA44][Avatar] NormalizeToHeight: {inst.name} has NO renderers!");
                return;
            }
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float h = b.size.y;
            if (h < 0.001f)
            {
                Debug.LogWarning($"[PLAGA44][Avatar] NormalizeToHeight: {inst.name} height={h} too small!");
                return;
            }
            float scaleFactor = targetHeight / h;
            inst.transform.localScale *= scaleFactor;
            Debug.Log($"[PLAGA44][Avatar] NormalizeToHeight: {inst.name} h={h:F2} -> scale={inst.transform.localScale.x:F4}");
        }

        private void CacheAnimatorBones()
        {
            _animator = _instance.GetComponent<Animator>();
            _headBone = null;
            _neckBone = null;
            if (_animator == null || !_animator.isHuman) return;
            _headBone = _animator.GetBoneTransform(HumanBodyBones.Head);
            _neckBone = _animator.GetBoneTransform(HumanBodyBones.Neck);
        }

        private static void SyncGalleryActiveIndex(int mode)
        {
            var gallery = AvatarGallery.Instance;
            if (gallery == null) return;
            int idx = mode - 1;
            gallery.SetActiveIndex((idx >= 0 && idx < gallery.Count) ? idx : -1);
        }

        private void DespawnAvatar()
        {
            if (_instance == null) return;
            Destroy(_instance);
            _instance = null;
            _animator = null;
            _headBone = null;
            _neckBone = null;
            _spawnedMode = -1;
        }

        private void LateUpdate()
        {
            if (_instance == null || !hideHead) return;
            if (_headBone != null) _headBone.localScale = Vector3.zero;
            if (_neckBone != null) _neckBone.localScale = Vector3.zero;
        }
    }
}
