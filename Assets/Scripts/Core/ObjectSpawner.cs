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
            public string resourcePath = "Items/Revolver";

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
            instance.name = prefab.name;
            WireComponents(instance, entry);
            _spawned.Add(instance);
            Debug.Log($"{LOG} Spawned '{instance.name}' at {worldPos}");
            return instance;
        }

        /// <summary>Spawn prefab z Resources/ prosto do rece gracza, state=GRABBED.
        /// Gdy grabber juz cos trzyma -> release + destroy poprzedniego.
        /// Zwraca spawned GO albo null gdy blad (brak prefabu / brak grabbera).</summary>
        /// <param name="hand">RTouch albo LTouch -- ktora reka ma item zlapac.</param>
        public GameObject SpawnIntoHand(string resourcePath, OVRInput.Controller hand = OVRInput.Controller.RTouch)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"{LOG} SpawnIntoHand: resource not found: {resourcePath}");
                return null;
            }

            var grabber = FindGrabber(hand);
            if (grabber == null)
            {
                Debug.LogWarning($"{LOG} SpawnIntoHand: grabber not found for {hand}");
                return null;
            }

            // Zachowaj referencje do poprzedniego itemu -- NIE destroy jeszcze.
            // Destroy PRZED ForceGrab = OVRGrabber trzyma destroyed Collider ref,
            // PlagaGrabber.GrabEnd wywoluje OVRGrabbable.ForceRelease na
            // zniszczonym GO, leci Cannot set parent + MissingReferenceException
            // na ClosestPointOnBounds w nastepnym GrabBegin.
            GameObject prevToDestroy = null;
            if (grabber.CurrentGrabbed != null)
            {
                prevToDestroy = grabber.CurrentGrabbed.gameObject;
                Debug.Log($"{LOG} SpawnIntoHand: replacing {prevToDestroy.name} in {hand} (destroy after ForceGrab)");
                _spawned.Remove(prevToDestroy);
            }

            // Spawn w pozycji grabbera. OVRGrabbable.GrabBegin sparentuje do ground,
            // a OVRGrabber.Update przesuwa rigidbody MoveRotation do grip pozycji.
            var entry = new SpawnEntry
            {
                resourcePath = resourcePath,
                autoRigidbody = true,
                autoCollider = true,
                autoGrabbable = true,
                mass = 1.0f
            };
            var instance = Instantiate(prefab, grabber.transform.position, grabber.transform.rotation);
            instance.name = prefab.name;
            WireComponents(instance, entry);
            _spawned.Add(instance);

            var grabbable = instance.GetComponent<OVRGrabbable>();
            if (grabbable == null)
            {
                Debug.LogWarning($"{LOG} SpawnIntoHand: {instance.name} missing OVRGrabbable after WireComponents -- cannot force grab");
                if (prevToDestroy != null) Destroy(prevToDestroy);
                return instance;
            }

            // ForceGrab czysto zwalnia poprzedniego (GrabEnd) + czysci m_grabCandidates
            // + chwyta nowy target. Dopiero potem bezpieczny Destroy(prev).
            if (!grabber.ForceGrab(grabbable))
            {
                Debug.LogWarning($"{LOG} SpawnIntoHand: ForceGrab failed for {instance.name}");
            }
            else
            {
                Debug.Log($"{LOG} SpawnIntoHand: {instance.name} zlapany przez {hand}");
            }

            if (prevToDestroy != null) Destroy(prevToDestroy);
            return instance;
        }

        private static PlagaGrabber FindGrabber(OVRInput.Controller hand)
        {
            foreach (var g in Object.FindObjectsByType<PlagaGrabber>(FindObjectsSortMode.None))
            {
                if (g.OwnerController == hand) return g;
            }
            return null;
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

            Vector3 pos = ResolveSpawnPosition(entry.offset);
            var instance = Instantiate(prefab, pos, Quaternion.identity);
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
            // Layer "Item" USUNIETY -- body physics olane. Poprzednio PlayerBody
            // x Item = ON powodowal ze item w rece zderzal sie z body capsule
            // avatara -> gracz wariowal. Item zostaje na Default layer.

            // Rigidbody -- mass z prefabu wygrywa nad entry.mass (celowa wartosc
            // ustawiona przez designera). entry.mass tylko dla freshly-added RB.
            if (entry.autoRigidbody)
            {
                var rb = instance.GetComponent<Rigidbody>();
                bool rbAdded = rb == null;
                if (rbAdded)
                {
                    rb = instance.AddComponent<Rigidbody>();
                    rb.mass = entry.mass;
                }
                rb.interpolation          = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            // Collider na ROOT (nie in-children). OVRGrabbable.Awake wymaga
            // Colliderra na tym samym GO do fallback grabPoints (gdy m_grabPoints
            // pusty). Child collidery nie wystarcza -- Awake rzuci ArgumentException.
            // BoxCollider dopasowany do mesh bounds -- obejmuje cala bryle obiektu.
            if (entry.autoCollider && instance.GetComponent<Collider>() == null)
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

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
