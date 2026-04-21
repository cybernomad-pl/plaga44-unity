// =============================================================================
// ItemBrowser.cs
// CYBERNOMAD -- Runtime item browser. Laduje itemy z Resources/Items/,
// pozwala wybrac item z HamburgerMenu.
//
// SPAWN BEHAVIOR: item pojawia sie OD RAZU W PRAWEJ RECE gracza z state=GRABBED.
// Podmiana na inny item = poprzedni zostaje zniszczony, nowy laduje w rece.
// Gracz moze go zwolnic (grab toggle) -> item spadnie na ziemie (gravity ON).
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

        public static ItemBrowser Instance { get; private set; }

        // =====================================================================
        // Config
        // =====================================================================

        [Header("Spawn Target")]
        [Tooltip("Ktora reka trzyma nowo spawnowany item.")]
        public OVRInput.Controller spawnHand = OVRInput.Controller.RTouch;

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

            _selectedIndex = index;
            PlayerPrefs.SetInt(PrefsKey, _selectedIndex);

            // None -- release + destroy aktualnego spawned itemu.
            if (index == 0)
            {
                DespawnCurrent();
                Debug.Log($"{LOG} Item: None");
                return;
            }

            int prefabIdx = index - 1;
            if (prefabIdx < 0 || prefabIdx >= _itemPrefabs.Length) return;

            // Spawn do reki z state=GRABBED. ObjectSpawner.SpawnIntoHand sam
            // zrobi podmiane (release + destroy poprzedniego) gdy cos juz trzyma.
            var spawner = ObjectSpawner.Instance;
            if (spawner == null)
            {
                Debug.LogWarning($"{LOG} SetItem: ObjectSpawner.Instance == null -- cannot spawn");
                _spawnedPreview = null;
                return;
            }

            // Key = resource path w Resources/Items/<name> (bez ".prefab").
            string resourcePath = $"{ItemsResourceFolder}/{_itemPrefabs[prefabIdx].name}";
            _spawnedPreview = spawner.SpawnIntoHand(resourcePath, spawnHand);
            Debug.Log($"{LOG} Item: {CurrentLabel} -- spawned into {spawnHand}");
        }

        // =====================================================================
        // Despawn
        // =====================================================================

        /// <summary>Release + destroy aktualnego spawned itemu. Public dla
        /// HamburgerMenu.Close (issue #158) -- zamknieciu menu towarzyszy
        /// zniknieciem aktualnego itemu.</summary>
        public void DespawnCurrent()
        {
            if (_spawnedPreview == null) return;
            Destroy(_spawnedPreview);
            _spawnedPreview = null;
        }

        /// <summary>[DEPRECATED] Alias zeby nie zepsuc callsites w HamburgerMenu.
        /// Patrz DespawnCurrent().</summary>
        public void DespawnPreview() => DespawnCurrent();

        /// <summary>Confirm spawn -- spawned item staje sie "realnym" itemem w swiecie.
        /// ItemBrowser przestaje go sledzic (SetItem/Close nie bedzie go niszczyl).
        /// Przycisk A w ITEMS sekcji HamburgerMenu.</summary>
        public bool ConfirmSpawn()
        {
            if (_spawnedPreview == null)
            {
                Debug.LogWarning($"{LOG} ConfirmSpawn: brak itemu do potwierdzenia");
                return false;
            }
            // Rename z przedrostkiem Item_ dla spojnosci (ItemBrowser nie spawnuje
            // z prefiksem ItemPreview_ -- trzyma prefab name po WireComponents).
            if (!_spawnedPreview.name.StartsWith("Item_", System.StringComparison.Ordinal))
                _spawnedPreview.name = "Item_" + _spawnedPreview.name;

            Debug.Log($"{LOG} ConfirmSpawn: {_spawnedPreview.name} zostaje w swiecie (unreferenced)");
            _spawnedPreview = null; // unreferencuj -- despawn nie tknie go
            return true;
        }
    }
}
