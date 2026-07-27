// =============================================================================
// NpcSpawner.cs
// CYBERNOMAD -- Singleton spawnera NPC Pinea. Instancjuje Resources/Npc/PINEA_NPC
// head-relative (przed graczem), stopy na poziomie ziemi (wysokosc oczu - 1.6).
// Dodaje CapsuleCollider + NpcController(library z Resources/Npc/NpcAnimationLibrary).
//
// Wzor: Assets/Scripts/Core/ObjectSpawner.cs (ResolveSpawnPosition -- identyczna
// matematyka head-relative przez OVRCameraRig/TrackingSpace/CenterEyeAnchor).
//
// ZERO FALLBACKOW: brak prefabu/library -> LogError + return null, NIE zgadujemy.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.Npc
{
    [DisallowMultipleComponent]
    public class NpcSpawner : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][NpcSpawner]";
        private const string AutoBootGoName = "_NpcSpawner";
        private const string OvrRigName = "OVRCameraRig";
        private const string PineaResourcePath = "Npc/PINEA_NPC";
        private const string LibraryResourcePath = "Npc/NpcAnimationLibrary";
        private const string RegistryResourcePath = "Npc/NpcRegistry";

        // Wysokosc oczu -> stopy: NPC stoi na ziemi (oczy - 1.6m).
        private const float EyeToFeet = 1.6f;
        // Spawn przed graczem na wysokosci oczu; offset.y = -EyeToFeet zrzuca stopy na ziemie.
        private static readonly Vector3 SpawnOffset = new Vector3(0f, -EyeToFeet, 1.5f);

        // -----------------------------------------------------------------
        // Singleton (lazy Instance -- jak ObjectSpawner, auto-boot)
        // -----------------------------------------------------------------
        private static NpcSpawner _instance;

        public static NpcSpawner Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var existing = FindAnyObjectByType<NpcSpawner>();
                if (existing != null) { _instance = existing; return _instance; }
                _instance = new GameObject(AutoBootGoName).AddComponent<NpcSpawner>();
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            if (_instance != null) return;
            if (FindAnyObjectByType<NpcSpawner>() != null) return;
            new GameObject(AutoBootGoName).AddComponent<NpcSpawner>();
        }

        // -----------------------------------------------------------------
        // Active registry
        // -----------------------------------------------------------------
        private readonly List<NpcController> _active = new List<NpcController>();
        public IReadOnlyList<NpcController> Active => _active;

        // -----------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // -----------------------------------------------------------------
        // Public API (kontrakt)
        // -----------------------------------------------------------------

        /// <summary>Liczba NPC w rejestrze (Resources/Npc/NpcRegistry). 0 gdy brak rejestru.</summary>
        public int NpcCount
        {
            get { var reg = Resources.Load<NpcRegistry>(RegistryResourcePath); return reg != null ? reg.Count : 0; }
        }

        /// <summary>Czytelna nazwa NPC pod indeksem rejestru, lub null (brak wpisu/rejestru).</summary>
        public string NpcName(int index)
        {
            var reg = Resources.Load<NpcRegistry>(RegistryResourcePath);
            var e = reg != null ? reg.Get(index) : null;
            return e != null ? e.name : null;
        }

        /// <summary>Spawnuje NPC z rejestru pod danym indeksem. Zwraca NpcController lub null.</summary>
        public NpcController SpawnNpc(int index)
        {
            var reg = Resources.Load<NpcRegistry>(RegistryResourcePath);
            if (reg == null)
            {
                Debug.LogError($"{LOG} Rejestr nie znaleziony: Resources/{RegistryResourcePath}");
                return null;
            }
            var entry = reg.Get(index);
            if (entry == null || entry.prefab == null)
            {
                Debug.LogError($"{LOG} SpawnNpc({index}) -- brak wpisu/prefabu w rejestrze (Count={reg.Count})");
                return null;
            }
            return SpawnFromPrefab(entry.prefab, entry.name);
        }

        /// <summary>Spawnuje Pinee (skrot -- prefab bezposrednio z Resources). Zwraca NpcController lub null.</summary>
        public NpcController SpawnPinea()
        {
            var prefab = Resources.Load<GameObject>(PineaResourcePath);
            if (prefab == null)
            {
                Debug.LogError($"{LOG} Prefab nie znaleziony: Resources/{PineaResourcePath}");
                return null;
            }
            return SpawnFromPrefab(prefab, "Pinea");
        }

        // Wspolna logika spawnu NPC: pozycja head-relative, shader, collider, controller+library.
        private NpcController SpawnFromPrefab(GameObject prefab, string logName)
        {
            var library = Resources.Load<NpcAnimationLibrary>(LibraryResourcePath);
            if (library == null)
            {
                Debug.LogError($"{LOG} Library nie znaleziona: Resources/{LibraryResourcePath}");
                return null;
            }

            Vector3 pos = ResolveSpawnPosition(SpawnOffset);
            Quaternion rot = ResolveSpawnRotation();

            var instance = Instantiate(prefab, pos, rot);
            Plaga44.Rendering.TestShaderApplier.Apply(instance); // Custom/Test Shader
            instance.name = prefab.name;

            // Collider fizyczny -- kapsula stojacego NPC (stopy w 0, glowa ~1.8).
            // Konfiguruj ZAWSZE: prefab moze miec CapsuleCollider z domyslnymi parametrami
            // (center 0, height 2) -- gate "if == null" zostawilby dolna polowe pod ziemia.
            var capsule = instance.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = instance.AddComponent<CapsuleCollider>();
            capsule.direction = 1; // os Y
            capsule.height = 1.8f;
            capsule.radius = 0.3f;
            capsule.center = new Vector3(0f, 0.9f, 0f);

            var controller = instance.GetComponent<NpcController>();
            if (controller == null) controller = instance.AddComponent<NpcController>();
            controller.library = library;

            _active.Add(controller);
            Debug.Log($"{LOG} Spawn '{instance.name}' ({logName}) at {pos} (clips={library.Count}, active={_active.Count})");
            return controller;
        }

        /// <summary>Niszczy wszystkie zespawnione NPC i czysci rejestr.</summary>
        public void DespawnAll()
        {
            foreach (var npc in _active)
                if (npc != null) Destroy(npc.gameObject);
            _active.Clear();
            Debug.Log($"{LOG} DespawnAll");
        }

        // -----------------------------------------------------------------
        // Internal -- head-relative (identyczna matematyka jak ObjectSpawner)
        // -----------------------------------------------------------------

        private static Vector3 ResolveSpawnPosition(Vector3 offset)
        {
            var rig = GameObject.Find(OvrRigName);
            if (rig == null) return offset;

            Transform head = rig.transform.Find("TrackingSpace/CenterEyeAnchor");
            Transform source = head != null ? head : rig.transform;

            Vector3 fwd = source.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

            // Punkt przed graczem w plaszczyznie XZ (na wysokosci oczu).
            Vector3 planar = source.position + right * offset.x + fwd * offset.z;

            // Raycast w dol -> stopy NPC na powierzchni (ziemia/teren), niezaleznie od
            // wysokosci oczu gracza. Sztywne "oczy - 1.6m" powodowaloby lewitacje/toniecie.
            Vector3 rayStart = planar + Vector3.up * 0.5f;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 25f, ~0, QueryTriggerInteraction.Ignore))
                return new Vector3(planar.x, hit.point.y, planar.z);

            // Brak podloza pod NPC -> planowana wysokosc (oczy - EyeToFeet). Nie zgaduj innej.
            return planar + Vector3.up * offset.y;
        }

        // NPC obraca sie twarza do gracza.
        private static Quaternion ResolveSpawnRotation()
        {
            var rig = GameObject.Find(OvrRigName);
            if (rig == null) return Quaternion.identity;

            Transform head = rig.transform.Find("TrackingSpace/CenterEyeAnchor");
            Transform source = head != null ? head : rig.transform;

            Vector3 fwd = source.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();

            return Quaternion.LookRotation(-fwd, Vector3.up);
        }
    }
}
