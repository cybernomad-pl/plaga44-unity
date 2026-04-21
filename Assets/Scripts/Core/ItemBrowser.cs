// =============================================================================
// ItemBrowser.cs
// CYBERNOMAD -- Runtime item browser. Laduje itemy z Resources/Items/,
// pozwala wybrac item z HamburgerMenu.
//
// SPAWN BEHAVIOR: item pojawia sie PRZED graczem (na "niewidzialnym stole")
// kiedy HamburgerMenu jest otwarte. Gracz podchodzi, chwyta gripem/triggerem
// i ustawia punkt przyczepienia do kontrolera.
// =============================================================================

using UnityEngine;

namespace Plaga44
{
    [DisallowMultipleComponent]
    public class ItemBrowser : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][ItemBrowser]";
        private const string AutoBootGoName = "ItemBrowser";
        private const string ItemsResourceFolder = "Items";
        private const string PrefsKey = "Plaga44_ItemBrowser_SelectedItem";
        private const string OvrRigName = "OVRCameraRig";

        public static ItemBrowser Instance { get; private set; }

        // =====================================================================
        // Config
        // =====================================================================

        [Header("Spawn Position (relative to head)")]
        [Tooltip("Distance in front of player where items appear.")]
        public float spawnDistance = 1.2f;

        [Tooltip("Height offset from head (negative = below eye level, like a table).")]
        public float spawnHeightOffset = -0.5f;

        // =====================================================================
        // State
        // =====================================================================

        private GameObject[] _itemPrefabs;
        private string[] _itemNames;
        private int _selectedIndex; // 0 = None, 1..N = item
        private GameObject _spawnedPreview;

        // =====================================================================
        // Public API
        // =====================================================================

        public int MaxItem => (_itemPrefabs != null) ? _itemPrefabs.Length : 0;
        public int SelectedItem => _selectedIndex;

        /// <summary>Currently spawned preview item (or null). Target for ITEM GRIP live tuning.</summary>
        public GameObject CurrentSpawned => _spawnedPreview;

        public string CurrentLabel
        {
            get
            {
                if (_selectedIndex == 0) return "None";
                int idx = _selectedIndex - 1;
                if (_itemNames == null || idx < 0 || idx >= _itemNames.Length) return "?";
                return _itemNames[idx];
            }
        }

        // =====================================================================
        // Lifecycle
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            // Fallback -- Bootstrap should create _ItemBrowser in scene.
            if (Instance != null) return;
            if (FindAnyObjectByType<ItemBrowser>() != null) return;
            new GameObject(AutoBootGoName).AddComponent<ItemBrowser>();
            Debug.LogWarning("[PLAGA44][ItemBrowser] AutoBoot fallback -- Bootstrap should create this GO");
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
            LoadItems();
            int saved = PlayerPrefs.GetInt(PrefsKey, 0);
            SetItem(Mathf.Clamp(saved, 0, MaxItem));
            Debug.Log($"{LOG} Start: {MaxItem} items, restored={_selectedIndex} ({CurrentLabel})");
        }

        // =====================================================================
        // Item loading
        // =====================================================================

        private void LoadItems()
        {
            var loaded = Resources.LoadAll<GameObject>(ItemsResourceFolder);
            if (loaded == null || loaded.Length == 0)
            {
                _itemPrefabs = new GameObject[0];
                _itemNames = new string[0];
                Debug.Log($"{LOG} No items in Resources/{ItemsResourceFolder}/");
                return;
            }

            System.Array.Sort(loaded, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

            _itemPrefabs = loaded;
            _itemNames = new string[loaded.Length];
            for (int i = 0; i < loaded.Length; i++)
                _itemNames[i] = loaded[i].name;

            Debug.Log($"{LOG} Loaded {loaded.Length} items: {string.Join(", ", _itemNames)}");
        }

        // =====================================================================
        // Selection
        // =====================================================================

        public void SetItem(int index)
        {
            index = Mathf.Clamp(index, 0, MaxItem);
            DespawnPreview();

            _selectedIndex = index;
            PlayerPrefs.SetInt(PrefsKey, _selectedIndex);

            if (index == 0)
            {
                Debug.Log($"{LOG} Item: None");
                return;
            }

            int prefabIdx = index - 1;
            if (prefabIdx < 0 || prefabIdx >= _itemPrefabs.Length) return;

            SpawnPreview(_itemPrefabs[prefabIdx]);
            Debug.Log($"{LOG} Item: {CurrentLabel} -- spawned in front of player");
        }

        // =====================================================================
        // Spawn / despawn -- item appears in front of player
        // =====================================================================

        // Invisible physical stand spawnowany RAZEM z itemem -- trzyma go przed
        // graczem (na poziomie menu), 1.5x1.5m cienka plyta zaraz pod itemem.
        // Renderer.enabled=false -- niewidoczna. Collider static.
        // Pojawia/znika z itemem (DespawnPreview/ConfirmSpawn).
        private GameObject _spawnedStand;

        private const float StandWidth   = 1.5f;  // 1.5x1.5m -- mieści każdy item
        private const float StandHeight  = 0.02f; // cienka płyta
        private const float StandGap     = 0.01f; // 1cm pod itemem

        private void SpawnPreview(GameObject prefab)
        {
            // Pozycja DOKLADNIE przed graczem (jak przed moim zepsuciem --
            // head-relative, poziom menu).
            Vector3 pos = GetSpawnPosition();
            Quaternion rot = GetSpawnRotation();

            // Invisible stand 1cm pod item -- trzyma go na miejscu.
            _spawnedStand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _spawnedStand.name = $"ItemStand_{prefab.name}";
            _spawnedStand.transform.position = pos + Vector3.down * (StandGap + StandHeight * 0.5f);
            _spawnedStand.transform.localScale = new Vector3(StandWidth, StandHeight, StandWidth);
            var standRenderer = _spawnedStand.GetComponent<Renderer>();
            if (standRenderer != null) standRenderer.enabled = false; // invisible

            _spawnedPreview = Instantiate(prefab, pos, rot);
            _spawnedPreview.name = $"ItemPreview_{prefab.name}";

            // Fizyka NORMALNA -- item "wisi" (spada na invisible stand, leży na nim).
            // OVRGrabbable.Start cache'uje kinem=false -> po release non-kinematic ->
            // spada na stand z powrotem (jesli obok) albo na ziemie.
            var rb = _spawnedPreview.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity  = true;
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            Debug.Log($"{LOG} SpawnPreview: {_spawnedPreview.name} @ {pos:F2} " +
                $"(stand={_spawnedStand.name} invisible, rb kinem={rb?.isKinematic})");
        }

        /// <summary>Destroy current preview item + jego stand.
        /// Public for HamburgerMenu.Close (issue #158).</summary>
        public void DespawnPreview()
        {
            if (_spawnedPreview != null)
            {
                Destroy(_spawnedPreview);
                _spawnedPreview = null;
            }
            if (_spawnedStand != null)
            {
                Destroy(_spawnedStand);
                _spawnedStand = null;
            }
        }

        /// <summary>Confirm preview -- spawned item + jego stand zostaja w swiecie.
        /// ItemBrowser przestaje je sledzic (DespawnPreview nie tknie). Stand dalej
        /// trzyma item na miejscu dopoki user go nie przesunie.
        /// Przycisk A w ITEMS sekcji HamburgerMenu.</summary>
        public bool ConfirmSpawn()
        {
            if (_spawnedPreview == null)
            {
                Debug.LogWarning($"{LOG} ConfirmSpawn: brak preview do potwierdzenia");
                return false;
            }
            string prevName = _spawnedPreview.name;
            _spawnedPreview.name = prevName.StartsWith("ItemPreview_")
                ? "Item_" + prevName.Substring("ItemPreview_".Length)
                : "Item_" + prevName;

            // Rename stand tez -- zeby pokazac ze jest "zatwierdzony"
            if (_spawnedStand != null)
                _spawnedStand.name = _spawnedStand.name.Replace("ItemStand_", "ItemStand_Confirmed_");

            Debug.Log($"{LOG} ConfirmSpawn: {_spawnedPreview.name} + stand zostaja w swiecie");
            _spawnedPreview = null;
            _spawnedStand   = null;
            return true;
        }

        private Vector3 GetSpawnPosition()
        {
            Transform head = FindHead();
            if (head == null) return Vector3.forward * spawnDistance;

            Vector3 fwd = head.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();

            return head.position + fwd * spawnDistance + Vector3.up * spawnHeightOffset;
        }

        private Quaternion GetSpawnRotation()
        {
            Transform head = FindHead();
            if (head == null) return Quaternion.identity;

            Vector3 fwd = head.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();

            // Item faces player
            return Quaternion.LookRotation(-fwd, Vector3.up);
        }

        private Transform FindHead()
        {
            var rig = GameObject.Find(OvrRigName);
            if (rig == null) return Camera.main?.transform;
            var tracking = rig.transform.Find("TrackingSpace");
            if (tracking == null) return rig.transform;
            var eye = tracking.Find("CenterEyeAnchor");
            return eye != null ? eye : tracking;
        }
    }
}
