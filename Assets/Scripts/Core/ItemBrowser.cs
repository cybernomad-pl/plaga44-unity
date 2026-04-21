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

        private void SpawnPreview(GameObject prefab)
        {
            // Pozycja DOKLADNIE przed graczem (head-relative, poziom menu).
            Vector3 pos = GetSpawnPosition();
            Quaternion rot = GetSpawnRotation();

            _spawnedPreview = Instantiate(prefab, pos, rot);
            _spawnedPreview.name = $"ItemPreview_{prefab.name}";

            // Item WISI stabilnie przed graczem -- kinematic=true, no gravity.
            // OVRGrabbable.Start cache'uje starting kinematic -> po release
            // wraca do kinematic=true -> item znowu wisi (nie spada na ziemie).
            // Proste, przewidywalne, latwe do grab (non-moving target).
            var rb = _spawnedPreview.GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity  = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
                rb.useGravity  = false;
            }

            Debug.Log($"{LOG} SpawnPreview: {_spawnedPreview.name} @ {pos:F2} " +
                $"(rb kinem={rb?.isKinematic}, grav={rb?.useGravity})");
        }

        /// <summary>Destroy current preview item.
        /// Public for HamburgerMenu.Close (issue #158).</summary>
        public void DespawnPreview()
        {
            if (_spawnedPreview == null) return;
            Destroy(_spawnedPreview);
            _spawnedPreview = null;
        }

        /// <summary>Confirm preview -- spawned item zostaje w swiecie.
        /// ItemBrowser przestaje go sledzic (DespawnPreview nie tknie go).
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
            Debug.Log($"{LOG} ConfirmSpawn: {_spawnedPreview.name} zostaje w swiecie (unreferenced)");
            _spawnedPreview = null;
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
