// =============================================================================
// ObjectSpawner.cs
// CYBERNOMAD -- Runtime spawner obiektow w scenie.
// Spawnuje itemy z Resources/ z pelnym setupem fizyki i interakcji VR.
// Auto-wires: Rigidbody, Collider, PlagaGrabbable, HapticOnGrab.
//
// Auto-boot via [RuntimeInitializeOnLoadMethod]. Konfigurowalny z inspektora
// (gdy GO istnieje w scenie) lub przez BootstrapConfig (domyslne wartosci).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using Plaga44.Feedback;
using Plaga44.Inventory;

namespace Plaga44
{
    [DisallowMultipleComponent]
    public class ObjectSpawner : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][ObjectSpawner]";
        private const string AutoBootGoName = "_ObjectSpawner";
        private const string OvrRigName = "OVRCameraRig";

        public static ObjectSpawner Instance { get; private set; }

        // -----------------------------------------------------------------
        // Config (editable in Inspector)
        // -----------------------------------------------------------------

        [System.Serializable]
        public class SpawnEntry
        {
            [Tooltip("Resources path (e.g. 'Items/Revolver').")]
            public string resourcePath = "Items/Shotgun";

            [Tooltip("Spawn offset relative to head (eye level). x=right, y=up from eyes (negative=table level), z=forward.")]
            public Vector3 offset = new Vector3(0f, -0.5f, 1.2f);

            [Tooltip("Auto-add Rigidbody if missing on prefab.")]
            public bool autoRigidbody = true;

            [Tooltip("Auto-add Collider if missing on prefab.")]
            public bool autoCollider = true;

            [Tooltip("Auto-add PlagaGrabbable + HapticOnGrab if missing.")]
            public bool autoGrabbable = true;

            [Tooltip("Mass (kg) for auto-added Rigidbody.")]
            public float mass = 1.0f;

            [Tooltip("Float at eye level: no gravity + gentle bob until first grab, then falls after release. When false, legacy 'table' behaviour is unchanged.")]
            public bool floatAtEyeLevel = false;

            public bool enabled = true;
        }

        [Header("Spawn Config")]
        public List<SpawnEntry> spawnList = new List<SpawnEntry>();

        [Header("Spawn on Start")]
        [Tooltip("If true, spawns all enabled entries on Start().")]
        public bool spawnOnStart = true;

        // -----------------------------------------------------------------
        // Spawned tracking
        // -----------------------------------------------------------------
        private readonly List<GameObject> _spawned = new List<GameObject>();
        public IReadOnlyList<GameObject> SpawnedItems => _spawned;

        // -----------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            // Fallback -- Bootstrap should create _ObjectSpawner in scene.
            if (Instance != null) return;
            if (FindAnyObjectByType<ObjectSpawner>() != null) return;
            new GameObject(AutoBootGoName).AddComponent<ObjectSpawner>();
            Debug.LogWarning("[PLAGA44][ObjectSpawner] AutoBoot fallback -- Bootstrap should create this GO");
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

        private bool _spawnTriggered;

        private void Start()
        {
            // Defer spawn to Update -- wait for player to land on terrain so spawn
            // position (relative to rig) is at ground level, not 42m up in the air.
        }

        private void Update()
        {
            if (_spawnTriggered || !spawnOnStart) return;
            // World-save (#196) jest autorytatywny: gdy istnieje save, obiekty odtwarza
            // WorldSaveManager -- domyslnego spawnu NIE robimy (inaczej duplikaty).
            if (WorldSaveManager.HasSave)
            {
                _spawnTriggered = true;
                Debug.Log($"{LOG} Default spawn skipped -- world save present.");
                return;
            }
            if (!IsPlayerGrounded()) return;
            _spawnTriggered = true;
            SpawnAll();
        }

        private static bool IsPlayerGrounded()
        {
            var rig = GameObject.Find(OvrRigName);
            if (rig == null) return true;
            var cc = rig.GetComponent<CharacterController>();
            if (cc == null) return true;
            return cc.isGrounded;
        }

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>Spawn all enabled entries from spawnList.</summary>
        public void SpawnAll()
        {
            int ok = 0, failed = 0;
            foreach (var entry in spawnList)
            {
                if (!entry.enabled) continue;
                var item = SpawnItem(entry);
                if (item != null) ok++; else failed++;
            }
            Debug.Log($"{LOG} SpawnAll done: {ok} spawned, {failed} failed.");
        }

        /// <summary>Spawn single item by Resources path. Returns spawned GO or null.</summary>
        public GameObject SpawnSingle(string resourcePath, Vector3 worldPos)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"{LOG} Resource not found: {resourcePath}");
                return null;
            }

            var entry = new SpawnEntry
            {
                resourcePath = resourcePath,
                autoRigidbody = true,
                autoCollider = true,
                autoGrabbable = true,
                mass = 1.0f
            };

            var instance = Instantiate(prefab, worldPos, Quaternion.identity);
            Plaga44.Rendering.TestShaderApplier.Apply(instance); // Custom/Test Shader
            instance.name = prefab.name;
            WireComponents(instance, entry);
            _spawned.Add(instance);
            Debug.Log($"{LOG} Spawned '{instance.name}' at {worldPos}");
            return instance;
        }

        /// <summary>Despawn all tracked items.</summary>
        public void DespawnAll()
        {
            foreach (var go in _spawned)
                if (go != null) Destroy(go);
            _spawned.Clear();
            Debug.Log($"{LOG} DespawnAll");
        }

        // -----------------------------------------------------------------
        // Internal
        // -----------------------------------------------------------------

        private GameObject SpawnItem(SpawnEntry entry)
        {
            var prefab = Resources.Load<GameObject>(entry.resourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"{LOG} Resource not found: {entry.resourcePath}");
                return null;
            }

            // Float items ignore the tabletop y-offset -- spawn exactly at eye level (y=0).
            Vector3 offset = entry.offset;
            if (entry.floatAtEyeLevel) offset.y = 0f;
            Vector3 pos = ResolveSpawnPosition(offset);
            var instance = Instantiate(prefab, pos, Quaternion.identity);
            Plaga44.Rendering.TestShaderApplier.Apply(instance); // Custom/Test Shader
            instance.name = prefab.name;

            WireComponents(instance, entry);
            _spawned.Add(instance);
            Debug.Log($"{LOG} Spawned '{instance.name}' at {pos} (rb={entry.autoRigidbody}, grab={entry.autoGrabbable})");
            return instance;
        }

        // Spawn head-relative (eye level) -- creates "virtual table" in front of player.
        // offset.y is relative to eye height (negative = below eyes, like a tabletop).
        private static Vector3 ResolveSpawnPosition(Vector3 offset)
        {
            var rig = GameObject.Find(OvrRigName);
            if (rig == null) return offset;

            // Prefer head (CenterEyeAnchor) -- spawn appears on "table" at eye level.
            Transform head = rig.transform.Find("TrackingSpace/CenterEyeAnchor");
            Transform source = head != null ? head : rig.transform;

            Vector3 fwd = source.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

            return source.position + right * offset.x + Vector3.up * offset.y + fwd * offset.z;
        }

        private static void WireComponents(GameObject instance, SpawnEntry entry)
        {
            // World-save (#196): explicit resourcePath do respawnu.
            SaveableObject.Tag(instance, entry.resourcePath);

            // Rigidbody
            if (entry.autoRigidbody)
            {
                var rb = instance.GetComponent<Rigidbody>();
                if (rb == null) rb = instance.AddComponent<Rigidbody>();
                rb.mass = entry.mass;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            // Collider -- add BoxCollider fitted to mesh bounds if none exists
            if (entry.autoCollider && instance.GetComponentInChildren<Collider>() == null)
            {
                var bounds = ComputeBounds(instance);
                var col = instance.AddComponent<BoxCollider>();
                col.center = instance.transform.InverseTransformPoint(bounds.center);
                col.size = bounds.size;
            }

            // Grabbable + Haptic
            if (entry.autoGrabbable)
            {
                if (instance.GetComponent<HapticOnGrab>() == null)
                    instance.AddComponent<HapticOnGrab>();
                if (instance.GetComponent<PlagaGrabbable>() == null)
                    instance.AddComponent<PlagaGrabbable>();
            }

            // Float-at-eye-level: attach hover which kills gravity on Awake and re-enables
            // it on the first release after a grab (item drifts, then falls when dropped).
            if (entry.floatAtEyeLevel)
            {
                if (instance.GetComponent<Plaga44.Items.FloatHover>() == null)
                    instance.AddComponent<Plaga44.Items.FloatHover>();
            }
        }

        private static Bounds ComputeBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.one * 0.1f);

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}
