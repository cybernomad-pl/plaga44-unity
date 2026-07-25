// =============================================================================
// AvatarGallery.cs
// CYBERNOMAD -- runtime spawner preview avatarow w scenie.
// Czyta AvatarRegistry z Resources/, instancjonuje prefaby w rzadzie przed graczem.
// Auto-boot via [RuntimeInitializeOnLoadMethod] -- nie wymaga edycji .unity.
// =============================================================================
using UnityEngine;

namespace Plaga44
{
    /// <summary>
    /// Preview avatarow z AvatarRegistry w rzadzie PRZED graczem (relative do OVRCameraRig).
    /// Tylko aktywny avatar jest enabled (lazy) -- reszta disabled, oszczedzamy VRAM/drawcalls na Queście.
    /// Auto-boot: [RuntimeInitializeOnLoadMethod] tworzy GameObject "AvatarGallery" w kazdej scenie.
    /// </summary>
    [DisallowMultipleComponent]
    public class AvatarGallery : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Gallery]";
        private const string OvrRigName = "OVRCameraRig";
        private const string AutoBootGoName = "AvatarGallery";
        private const float GroundRaycastStartY = 0.5f;
        private const float GroundRaycastMaxDist = 2000f;
        private const float FallbackOriginZ = 3f;

        public static AvatarGallery Instance { get; private set; }

        [Header("Layout (wzgledem OVRCameraRig jesli znaleziony)")]
        [Tooltip("Offset od gracza (x=w prawo, y=nad ziemia, z=przed)")]
        public Vector3 relativeOffset = new Vector3(0f, 0f, 2.0f);

        [Tooltip("Kierunek rzadu avatarow (znormalizowany)")]
        public Vector3 direction = Vector3.right;

        [Tooltip("Odstep miedzy avatarami (metry)")]
        public float spacing = 1.5f;

        [Tooltip("Obrot kazdej instancji wokol Y (stopnie). 180 = twarza do gracza.")]
        public float yaw = 180f;

        [Tooltip("Target avatar height in meters. Avatars are auto-scaled to this height.")]
        public float targetAvatarHeight = 1.8f;

        [Tooltip("Y = poziom terenu pod graczem (raycast w dol). Default: ziemia.")]
        public bool useGroundY = true;

        [Header("Tryb wyswietlania")]
        [Tooltip("Lazy: tylko aktywny avatar enabled. Reszta disabled. Oszczedza VRAM.")]
        public bool lazyDisplay = true;

        [Tooltip("Index avatara aktywnego na start (-1 = zaden, wszystkie disabled)")]
        public int initialActiveIndex = -1;

        private AvatarRegistry _registry;
        private GameObject[] _instances;
        private int _activeIndex = -1;

        // =====================================================================
        // Public API (query)
        // =====================================================================

        public int Count => _instances != null ? _instances.Length : 0;
        public int ActiveIndex => _activeIndex;

        public GameObject GetInstance(int i)
            => (_instances != null && i >= 0 && i < _instances.Length) ? _instances[i] : null;

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
            // Fallback -- Bootstrap should create _AvatarGallery in scene.
            // This only fires if Bootstrap didn't run (e.g. standalone build without editor setup).
            if (Instance != null) return;
            if (FindAnyObjectByType<AvatarGallery>() != null) return;
            new GameObject(AutoBootGoName).AddComponent<AvatarGallery>();
            Debug.LogWarning("[PLAGA44][Gallery] AutoBoot fallback -- Bootstrap should create this GO");
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

        private bool _spawned;

        private void Start()
        {
            if (!TryLoadRegistry()) return;
            // Spawn deferred to Update -- wait for player to land on terrain so
            // origin (relative to CenterEyeAnchor) is at ground level, not 42m up.
        }

        private void Update()
        {
            if (_spawned || _instances == null || _instances.Length == 0) return;
            if (!IsPlayerGrounded()) return;
            DoSpawn("auto (grounded)");
        }

        /// <summary>Force spawn now regardless of grounded state (issue #157).
        /// Called by HamburgerMenu on open -- preview should be visible whenever menu is open,
        /// even if player is mid-flight.</summary>
        public void ForceSpawnNow()
        {
            if (_spawned || _instances == null || _instances.Length == 0) return;
            DoSpawn("force (menu open)");
        }

        /// <summary>Destroy all spawned preview instances (issue #158).
        /// Called by HamburgerMenu.Close -- preview should not linger after menu closes.
        /// After Hide, next Open triggers ForceSpawnNow -> fresh spawn at new player pos.</summary>
        public void HideAllPreviews()
        {
            if (!_spawned || _instances == null) return;
            int destroyed = 0;
            for (int i = 0; i < _instances.Length; i++)
            {
                if (_instances[i] != null) { Destroy(_instances[i]); _instances[i] = null; destroyed++; }
            }
            _spawned = false;
            _activeIndex = -1;
            Debug.Log($"{LOG} HideAllPreviews: {destroyed} instances destroyed");
        }

        private void DoSpawn(string reason)
        {
            ResolveOrigin(out Vector3 origin, out Vector3 rowRight);
            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : rowRight;
            var rot = Quaternion.Euler(0f, yaw, 0f);

            (int spawned, int skipped) = SpawnAllPreviews(origin, dir, rot);
            if (skipped > 0) Debug.LogWarning($"{LOG} Total broken skipped: {skipped}/{_instances.Length}");

            ApplyInitialLazyState();
            _spawned = true;
            Debug.Log($"{LOG} Spawned {spawned}/{_instances.Length} avatars at {origin} (lazy={lazyDisplay}, active={_activeIndex}) [trigger={reason}]");
        }

        private static bool IsPlayerGrounded()
        {
            var rig = GameObject.Find(OvrRigName);
            if (rig == null) return true; // no rig -- spawn immediately at fallback origin
            var cc = rig.GetComponent<CharacterController>();
            if (cc == null) return true;
            return cc.isGrounded;
        }

        // =====================================================================
        // Registry loading
        // =====================================================================

        private bool TryLoadRegistry()
        {
            _registry = Resources.Load<AvatarRegistry>(AvatarRegistry.ResourcesPath);
            if (_registry == null)
            {
                Debug.LogWarning($"{LOG} AvatarRegistry not found in Resources/{AvatarRegistry.ResourcesPath} -- skipping gallery");
                _instances = new GameObject[0];
                return false;
            }
            if (_registry.Count == 0)
            {
                Debug.Log($"{LOG} Registry empty -- no avatars to preview");
                _instances = new GameObject[0];
                return false;
            }
            _instances = new GameObject[_registry.Count];
            // Debug: list all registry entries with their state
            for (int i = 0; i < _registry.Count; i++)
            {
                var e = _registry.Get(i);
                string state = e == null ? "<null>" : (e.broken ? $"BROKEN({e.errorMessage})" : (e.prefab != null ? "OK" : "no-prefab"));
                Debug.Log($"{LOG}   Registry[{i}] = '{e?.name ?? "?"}' -- {state}");
            }
            return true;
        }

        // =====================================================================
        // Origin resolution (rig-relative / fallback)
        // =====================================================================

        private void ResolveOrigin(out Vector3 origin, out Vector3 rowRight)
        {
            var rigGO = GameObject.Find(OvrRigName);
            if (rigGO != null)
            {
                ResolveOriginFromRig(rigGO.transform, out origin, out rowRight);
                return;
            }
            origin = relativeOffset;
            rowRight = Vector3.right;
        }

        private void ResolveOriginFromRig(Transform rig, out Vector3 origin, out Vector3 rowRight)
        {
            // Use head camera (CenterEyeAnchor) like HamburgerMenu so avatars
            // appear in front of player at eye level, not at rig base (on head).
            Transform head = rig.Find("TrackingSpace/CenterEyeAnchor");
            Transform source = head != null ? head : rig;

            Vector3 fwd = source.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();
            rowRight = Vector3.Cross(Vector3.up, fwd);

            Vector3 basePos = source.position;
            if (useGroundY) basePos.y = RaycastGroundY(basePos) ?? basePos.y;

            origin = basePos
                + fwd * relativeOffset.z
                + rowRight * relativeOffset.x
                + Vector3.up * relativeOffset.y;
        }

        private static float? RaycastGroundY(Vector3 from)
        {
            Vector3 start = from + Vector3.up * GroundRaycastStartY;
            if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, GroundRaycastMaxDist))
                return hit.point.y;
            return null;
        }

        // =====================================================================
        // Spawning
        // =====================================================================

        private (int spawned, int skipped) SpawnAllPreviews(Vector3 origin, Vector3 dir, Quaternion rot)
        {
            int spawned = 0;
            int skipped = 0;
            Debug.Log($"{LOG} SpawnAllPreviews START: origin={origin:F2} dir={dir:F2} count={_registry.Count}");
            for (int i = 0; i < _registry.Count; i++)
            {
                var entry = _registry.Get(i);
                if (!IsSpawnable(entry, i, ref skipped))
                {
                    Debug.LogWarning($"{LOG}   [{i}] '{entry?.name ?? "<null>"}' SKIPPED (not spawnable)");
                    continue;
                }

                Vector3 pos = origin + dir * spacing * i;
                Debug.Log($"{LOG}   [{i}] '{entry.name}' instantiating at {pos:F2} (rot.y={rot.eulerAngles.y:F0})");
                var inst = Instantiate(entry.prefab, pos, rot, transform);
                Plaga44.Rendering.TestShaderApplier.Apply(inst); // Custom/Test Shader avatarom
                inst.name = $"Preview_{entry.name}";

                // Diagnose prefab structure
                var renderers = inst.GetComponentsInChildren<Renderer>(true);
                var skinned = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var animator = inst.GetComponentInChildren<Animator>(true);
                Debug.Log($"{LOG}   [{i}] '{entry.name}' components: renderers={renderers.Length} "
                    + $"skinned={skinned.Length} animator={(animator != null ? "yes" : "NO")} "
                    + $"avatar={(animator?.avatar != null ? animator.avatar.name : "NULL")} "
                    + $"valid={(animator?.avatar?.isValid ?? false)}");

                // Diagnose materials (pink check)
                int pinkMatCount = 0;
                foreach (var r in renderers)
                {
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader")
                        {
                            pinkMatCount++;
                            Debug.LogWarning($"{LOG}   [{i}] '{entry.name}' renderer '{r.name}' has BROKEN material (shader={m?.shader?.name ?? "<null mat>"})");
                        }
                    }
                }
                if (pinkMatCount > 0)
                    Debug.LogError($"{LOG}   [{i}] '{entry.name}' has {pinkMatCount} BROKEN materials -- will appear PINK!");

                NormalizeToHeight(inst, targetAvatarHeight);

                // Preview in T-pose -- disable AnimatorController so skeleton stays in bind pose
                if (animator != null)
                {
                    animator.runtimeAnimatorController = null;
                    Debug.Log($"{LOG}   [{i}] '{entry.name}' AnimatorController nulled (T-pose preview)");
                }
                _instances[i] = inst;
                spawned++;
            }
            Debug.Log($"{LOG} SpawnAllPreviews DONE: spawned={spawned} skipped={skipped}");
            return (spawned, skipped);
        }

        private bool IsSpawnable(AvatarRegistry.Entry entry, int index, ref int brokenCount)
        {
            if (entry == null || entry.prefab == null)
            {
                Debug.LogWarning($"{LOG} Registry entry {index} null/missing prefab -- skipped");
                return false;
            }
            if (entry.broken)
            {
                brokenCount++;
                Debug.LogWarning($"{LOG} Skipping broken avatar [{index}] '{entry.name}': {entry.errorMessage}");
                return false;
            }
            return true;
        }

        private void ApplyInitialLazyState()
        {
            if (lazyDisplay) DisableAllInstances();
            if (initialActiveIndex >= 0 && initialActiveIndex < _instances.Length)
                SetActiveIndex(initialActiveIndex);
        }

        private void DisableAllInstances()
        {
            for (int i = 0; i < _instances.Length; i++)
                if (_instances[i] != null) _instances[i].SetActive(false);
        }

        // =====================================================================
        // Active avatar switching
        // =====================================================================

        /// <summary>Scale avatar instance so its bounds.height matches targetHeight.</summary>
        private static void NormalizeToHeight(GameObject inst, float targetHeight)
        {
            var renderers = inst.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float currentHeight = b.size.y;
            if (currentHeight < 0.001f) return;
            float scale = targetHeight / currentHeight;
            inst.transform.localScale *= scale;
            Debug.Log($"[PLAGA44][Gallery] NormalizeToHeight: {inst.name} h={currentHeight:F2} -> scale={inst.transform.localScale.x:F4}");
        }

        /// <summary>Aktywuje tylko jeden avatar. -1 = wszystkie disabled.</summary>
        public void SetActiveIndex(int index)
        {
            if (_instances == null) return;
            _activeIndex = Mathf.Clamp(index, -1, _instances.Length - 1);
            for (int i = 0; i < _instances.Length; i++)
            {
                if (_instances[i] == null) continue;
                bool on = !lazyDisplay || i == _activeIndex;
                if (_instances[i].activeSelf != on) _instances[i].SetActive(on);
            }
            Debug.Log($"{LOG} SetActiveIndex({_activeIndex}) lazy={lazyDisplay}");
        }
    }
}
