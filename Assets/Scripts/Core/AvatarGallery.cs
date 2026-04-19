// =============================================================================
// AvatarGallery.cs
// CYBERNOMAD -- runtime spawner JEDNEGO preview avatara przed graczem.
// Analogicznie do ItemBrowser: single preview, position = head.forward *
// spawnDistance + up * spawnHeightOffset. Obracany tylko y=yaw (twarza do gracza).
// Auto-boot via [RuntimeInitializeOnLoadMethod].
//
// SetActiveIndex(i):
//   i == -1  -> despawn (None)
//   i >= 0   -> despawn + spawn registry.Get(i).prefab przed graczem
// =============================================================================
using UnityEngine;

namespace Plaga44
{
    [DisallowMultipleComponent]
    public class AvatarGallery : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Gallery]";
        private const string OvrRigName = "OVRCameraRig";
        private const string AutoBootGoName = "AvatarGallery";

        public static AvatarGallery Instance { get; private set; }

        [Header("Spawn Position (relative to head -- jak ItemBrowser)")]
        [Tooltip("Distance przed graczem gdzie pojawia sie preview.")]
        public float spawnDistance = 2.0f;

        [Tooltip("Height offset od eye level (ujemne = nizej). -0.9 = stopy ~przy ziemi.")]
        public float spawnHeightOffset = -0.9f;

        [Tooltip("Obrot wokol Y (stopnie). 180 = twarza do gracza.")]
        public float yaw = 180f;

        [Tooltip("Target avatar height in meters. Avatar auto-scaled do tej wysokosci.")]
        public float targetAvatarHeight = 1.8f;

        [Header("Initial")]
        [Tooltip("Index avatara na start (-1 = None).")]
        public int initialActiveIndex = -1;

        private AvatarRegistry _registry;
        private GameObject _spawnedPreview; // single instance (jak ItemBrowser)
        private int _activeIndex = -1;

        // =====================================================================
        // Public API (query)
        // =====================================================================

        public int Count => _registry != null ? _registry.Count : 0;
        public int ActiveIndex => _activeIndex;

        /// <summary>Aktualny preview GO (moze byc null).</summary>
        public GameObject CurrentSpawned => _spawnedPreview;

        public GameObject GetInstance(int i) => (i == _activeIndex) ? _spawnedPreview : null;

        public GameObject GetPrefab(int i)
            => (_registry != null && _registry.Get(i) != null) ? _registry.Get(i).prefab : null;

        public string GetName(int i)
            => (_registry != null && _registry.Get(i) != null) ? _registry.Get(i).name : "";

        public bool IsBroken(int i)
        {
            var e = _registry?.Get(i);
            return e != null && e.broken;
        }

        public string GetError(int i) => _registry?.Get(i)?.errorMessage;

        // =====================================================================
        // Auto-boot + lifecycle
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<AvatarGallery>() != null) return;
            new GameObject(AutoBootGoName).AddComponent<AvatarGallery>();
            Debug.LogWarning($"{LOG} AutoBoot fallback -- Bootstrap should create this GO");
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            TryLoadRegistry();
            if (initialActiveIndex >= 0 && initialActiveIndex < Count)
                SetActiveIndex(initialActiveIndex);
        }

        // =====================================================================
        // Public API (control) -- called by HamburgerMenu/PlayerAvatar
        // =====================================================================

        /// <summary>Switch preview do index (despawn current, spawn new przed graczem).
        /// -1 = None -> spawn SDK default rig (StylizedCharacterLocomotion) jako preview T-pose.</summary>
        public void SetActiveIndex(int index)
        {
            if (_registry == null) TryLoadRegistry();
            int clamped = Mathf.Clamp(index, -1, Count - 1);
            if (clamped == _activeIndex && _spawnedPreview != null) return;

            DespawnPreview();
            _activeIndex = clamped;

            if (clamped < 0)
            {
                // None -- spawn SDK default rig (Borys: "chce zobaczyc default robota w T-pose")
                SpawnSdkDefaultPreview();
                return;
            }

            var entry = _registry.Get(clamped);
            if (entry == null || entry.prefab == null)
            {
                Debug.LogWarning($"{LOG} SetActiveIndex({clamped}): prefab null");
                return;
            }
            if (entry.broken)
            {
                Debug.LogWarning($"{LOG} SetActiveIndex({clamped}): '{entry.name}' BROKEN: {entry.errorMessage}");
                return;
            }

            SpawnPreviewAt(entry.prefab, entry.name);
        }

        /// <summary>Force re-spawn current preview (HamburgerMenu on open).
        /// Zawsze -- nawet gdy activeIndex = -1 (None) -- zeby pokazac SDK default rig.</summary>
        public void ForceSpawnNow()
        {
            if (_spawnedPreview != null) return;
            SetActiveIndex(_activeIndex);
        }

        private void SpawnSdkDefaultPreview()
        {
            var pa = PlayerAvatar.FindCurrent();
            if (pa == null || pa.defaultRig == null)
            {
                Debug.LogWarning($"{LOG} SpawnSdkDefaultPreview: PlayerAvatar.defaultRig missing -- skip (no preview shown for None)");
                return;
            }
            SpawnPreviewAt(pa.defaultRig, "SDKDefault");
        }

        /// <summary>Destroy current preview (HamburgerMenu on close).
        /// Brute-force cleanup: niszczy tracked _spawnedPreview PLUS szuka w scenie
        /// orphaned AvatarPreview_* (jak zostaly przez reparent/referencje reset).</summary>
        public void HideAllPreviews()
        {
            DespawnPreview();

            // Brute-force -- usun kazdego orphaned preview w scenie
            int orphanKilled = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t == null) continue;
                if (t.name.StartsWith("AvatarPreview_"))
                {
                    Destroy(t.gameObject);
                    orphanKilled++;
                }
            }
            Debug.Log($"{LOG} HideAllPreviews: tracked preview=null, orphans killed={orphanKilled}, _activeIndex={_activeIndex} retained");
        }

        // =====================================================================
        // Spawn / despawn
        // =====================================================================

        private void SpawnPreviewAt(GameObject prefab, string entryName)
        {
            Vector3 pos = GetSpawnPosition();
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            _spawnedPreview = Instantiate(prefab, pos, rot, transform);
            _spawnedPreview.name = $"AvatarPreview_{entryName}";

            // Static preview -- disable AnimatorController (T-pose), zero physics
            var animator = _spawnedPreview.GetComponent<Animator>();
            if (animator != null) animator.runtimeAnimatorController = null;

            var rb = _spawnedPreview.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            NormalizeToHeight(_spawnedPreview, targetAvatarHeight);

            // Diagnose materials (pink check)
            int pinkMatCount = 0;
            foreach (var r in _spawnedPreview.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader")
                        pinkMatCount++;
            if (pinkMatCount > 0)
                Debug.LogError($"{LOG} '{entryName}' has {pinkMatCount} BROKEN materials -- preview will be PINK!");

            Debug.Log($"{LOG} Spawned preview '{entryName}' at {pos:F2} (yaw={yaw}, scale={_spawnedPreview.transform.localScale.x:F3})");
        }

        private void DespawnPreview()
        {
            if (_spawnedPreview == null) return;
            Destroy(_spawnedPreview);
            _spawnedPreview = null;
        }

        private static void NormalizeToHeight(GameObject inst, float targetHeight)
        {
            var renderers = inst.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float h = b.size.y;
            if (h < 0.001f) return;
            float scale = targetHeight / h;
            inst.transform.localScale *= scale;
        }

        private Vector3 GetSpawnPosition()
        {
            Transform head = FindHead();
            if (head == null) return Vector3.forward * spawnDistance;
            Vector3 fwd = head.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();
            return head.position + fwd * spawnDistance + Vector3.up * spawnHeightOffset;
        }

        private static Transform FindHead()
        {
            var rig = GameObject.Find(OvrRigName);
            if (rig == null) return null;
            return rig.transform.Find("TrackingSpace/CenterEyeAnchor");
        }

        // =====================================================================
        // Registry loading
        // =====================================================================

        private bool TryLoadRegistry()
        {
            _registry = Resources.Load<AvatarRegistry>(AvatarRegistry.ResourcesPath);
            if (_registry == null)
            {
                Debug.LogWarning($"{LOG} AvatarRegistry not found in Resources/{AvatarRegistry.ResourcesPath}");
                return false;
            }
            if (_registry.Count == 0)
            {
                Debug.Log($"{LOG} Registry empty -- no avatars to preview");
                return false;
            }
            for (int i = 0; i < _registry.Count; i++)
            {
                var e = _registry.Get(i);
                string state = e == null ? "<null>" : (e.broken ? $"BROKEN({e.errorMessage})" : (e.prefab != null ? "OK" : "no-prefab"));
                Debug.Log($"{LOG}   Registry[{i}] = '{e?.name ?? "?"}' -- {state}");
            }
            return true;
        }
    }
}
