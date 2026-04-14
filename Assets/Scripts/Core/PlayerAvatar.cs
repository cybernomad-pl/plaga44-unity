// =============================================================================
// PlayerAvatar.cs
// CYBERNOMAD -- kontroler avatara gracza na OVRCameraRig.
// Deleguje liste avatarow do AvatarGallery (single source of truth).
// Mode=0 -> default rig (robot). Mode>=1 -> avatar z Gallery (indeks - 1).
// Fallback: jesli Gallery brak, laduje stare Survivor_A_Lusth z Resources/.
// =============================================================================
using UnityEngine;

namespace Plaga44
{
    /// <summary>
    /// Player avatar controller. Default state = NO avatar (skeleton/robot rig visible).
    /// Avatar is optional, selected from HamburgerMenu > AVATAR tile.
    ///
    /// Modes:
    ///   0             -- None (default rig visible)
    ///   1..N          -- avatar z AvatarGallery.Instance (index = mode - 1)
    ///   (legacy)      -- jesli Gallery niedostepna, mode=1 ladue "Survivor_A_Lusth" z Resources
    ///
    /// Player-compatible avatars must hide head + face (prevents camera clipping from inside).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerAvatar : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Avatar]";
        private const string LEGACY_RESOURCE = "Survivor_A_Lusth";

        public enum Mode { None = 0 } // legacy enum zostawione dla kompatybilnosci inspektora

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

        // Cache zeby UI nie szukal co frame
        private static PlayerAvatar _cached;
        public static PlayerAvatar FindCurrent()
        {
            if (_cached != null) return _cached;
            _cached = Object.FindAnyObjectByType<PlayerAvatar>();
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

        private void Start()
        {
            SetAvatarMode(avatarMode);
        }

        /// <summary>
        /// Switch avatar mode. Called from SettingsRegistry (AVATAR > Mode).
        /// </summary>
        public void SetAvatarMode(int mode)
        {
            mode = Mathf.Clamp(mode, 0, MaxMode);
            avatarMode = mode;

            if (mode == 0)
            {
                DespawnAvatar();
                ShowDefaultRig(true);
                // Gallery: wylacz wszystkie
                if (AvatarGallery.Instance != null) AvatarGallery.Instance.SetActiveIndex(-1);
                Debug.Log($"{LOG} Mode=None -- default rig visible, no avatar spawned");
            }
            else
            {
                ShowDefaultRig(false);
                SpawnAvatar(mode);
            }
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
                if (g == null || g.Count == 0) return "Survivor (legacy)";
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

            // 3. Legacy fallback -- Survivor_A_Lusth z Resources
            if (mode == 1)
            {
                var legacy = Resources.Load<GameObject>(LEGACY_RESOURCE);
                if (legacy != null)
                {
                    Debug.Log($"{LOG} Legacy fallback -- loaded '{LEGACY_RESOURCE}' from Resources/");
                    return legacy;
                }
            }

            return null;
        }

        private void SpawnAvatar(int mode)
        {
            if (_instance != null && _spawnedMode == mode) return;
            if (_instance != null) DespawnAvatar();

            var prefab = ResolvePrefabForMode(mode);
            if (prefab == null)
            {
                Debug.LogWarning($"{LOG} No prefab for mode={mode}. Falling back to None.");
                avatarMode = 0;
                ShowDefaultRig(true);
                if (AvatarGallery.Instance != null) AvatarGallery.Instance.SetActiveIndex(-1);
                return;
            }

            _instance = Instantiate(prefab, transform);
            _instance.name = "Avatar_" + prefab.name;
            _instance.transform.localPosition = new Vector3(0f, yOffset, 0f);
            _spawnedMode = mode;

            _animator = _instance.GetComponent<Animator>();
            _headBone = null;
            _neckBone = null;
            if (_animator != null && _animator.isHuman)
            {
                _headBone = _animator.GetBoneTransform(HumanBodyBones.Head);
                _neckBone = _animator.GetBoneTransform(HumanBodyBones.Neck);
            }

            // Poinformuj Gallery zeby wylaczyla pozostale preview
            if (AvatarGallery.Instance != null)
            {
                int galleryIdx = mode - 1;
                if (galleryIdx >= 0 && galleryIdx < AvatarGallery.Instance.Count)
                    AvatarGallery.Instance.SetActiveIndex(galleryIdx);
                else
                    AvatarGallery.Instance.SetActiveIndex(-1);
            }

            Debug.Log($"{LOG} Spawned '{_instance.name}' mode={mode} (humanoid={(_animator != null && _animator.isHuman)})");
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
