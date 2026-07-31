// =============================================================================
// ItemBrowser.cs
// CYBERNOMAD -- KATALOG itemow. Laduje grabbable z Resources/Items/ (po whitelist),
// sortuje wg PreferredOrder i wystawia je jako liste dla per-reka menu (HandItemMenu).
//
// TYLKO KATALOG -- zero spawnu. Preview-na-stole wycofane (2026-07-31): itemy
// spawnuja sie PROSTO do dloni przez LEFT/RIGHT HAND -> HandItemMenu ->
// GripSpawnToHand. ItemBrowser jest jednym zrodlem prawdy o zawartosci katalogu.
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

        // PODZIAL ITEMOW: GRABBABLES (trzymane w dloni) vs WEARABLES (ciuchy/armor
        // zakladane na cialo). Explicit whitelisty, zero fallback -- item spoza obu
        // list wypada z katalogu calkowicie.
        //
        // GRABBABLES: TYLKO te itemy sa ladowane do katalogu dloni. Box, BigStone,
        // Gun USUNIETE (2026-07-29). Aktualnie wylacznie Shotgun.
        private static readonly string[] GrabbableWhitelist = { "Shotgun" };

        // WEARABLES: ciuchy/armor zakladane na cialo (osobno od dloni). PLACEHOLDER --
        // brak prefabow i brak mechaniki zakladania. Pusta lista = sekcja WEARABLES w
        // menu jest explicit-pusta (nie udajemy dzialajacej mechaniki). Gdy powstana
        // wearable prefaby -> dopisz tu nazwy i zaimplementuj mechanike.
        private static readonly string[] WearableWhitelist = { };

        // Jawna kolejnosc na POCZATKU katalogu (reszta alfabetycznie po nich).
        // Deklaratywne, nie fallback: item spoza listy idzie do czesci alfabetycznej.
        private static readonly string[] PreferredOrder = { "Shotgun", "Blaster", "Torch" };

        public static ItemBrowser Instance { get; private set; }

        // =====================================================================
        // State
        // =====================================================================

        private GameObject[] _itemPrefabs;
        private string[] _itemNames;

        // =====================================================================
        // Public API -- katalog GRABBABLES (dla HandItemMenu)
        // =====================================================================

        /// <summary>Liczba grabbable itemow w katalogu (po whitelist).</summary>
        public int GrabbableCount => _itemPrefabs != null ? _itemPrefabs.Length : 0;

        /// <summary>Grabbable prefab pod indeksem 0-based, albo NULL gdy poza zakresem.
        /// Zero fallback -- caller decyduje co z null.</summary>
        public GameObject GrabbablePrefab(int zeroBased)
        {
            if (_itemPrefabs == null || zeroBased < 0 || zeroBased >= _itemPrefabs.Length) return null;
            return _itemPrefabs[zeroBased];
        }

        /// <summary>Nazwa grabbable pod indeksem 0-based, albo NULL gdy poza zakresem.</summary>
        public string GrabbableName(int zeroBased)
        {
            if (_itemNames == null || zeroBased < 0 || zeroBased >= _itemNames.Length) return null;
            return _itemNames[zeroBased];
        }

        /// <summary>Liczba WEARABLES w katalogu. Aktualnie 0 (placeholder -- brak prefabow
        /// i mechaniki). Sekcja WEARABLES w menu czyta to i pokazuje stan wprost.</summary>
        public int WearableCount => WearableWhitelist.Length;

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
            Debug.Log($"{LOG} Start: katalog {GrabbableCount} grabbable, {WearableCount} wearable");
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

            // WHITELIST GRABBABLES: odfiltruj do dozwolonych zanim zbudujesz katalog.
            int before = loaded.Length;
            loaded = System.Array.FindAll(loaded, p => System.Array.IndexOf(GrabbableWhitelist, p.name) >= 0);
            Debug.Log($"{LOG} GrabbableWhitelist: {loaded.Length}/{before} itemow ({string.Join(",", GrabbableWhitelist)})");

            System.Array.Sort(loaded, CompareItems);

            _itemPrefabs = loaded;
            _itemNames = new string[loaded.Length];
            for (int i = 0; i < loaded.Length; i++)
                _itemNames[i] = loaded[i].name;

            Debug.Log($"{LOG} Loaded {loaded.Length} grabbable: {string.Join(", ", _itemNames)}");
        }

        // Kolejnosc: itemy z PreferredOrder pierwsze (wg indeksu listy), reszta alfabetycznie.
        private static int CompareItems(GameObject a, GameObject b)
        {
            int ia = System.Array.IndexOf(PreferredOrder, a.name);
            int ib = System.Array.IndexOf(PreferredOrder, b.name);
            if (ia >= 0 && ib >= 0) return ia.CompareTo(ib); // oba preferred -> wg listy
            if (ia >= 0) return -1;                          // tylko a preferred -> a pierwszy
            if (ib >= 0) return 1;                           // tylko b preferred -> b pierwszy
            return string.Compare(a.name, b.name, System.StringComparison.Ordinal); // reszta alfabetycznie
        }
    }
}
