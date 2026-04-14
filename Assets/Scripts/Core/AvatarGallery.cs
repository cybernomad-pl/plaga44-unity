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
    /// Preview avatarow z AvatarRegistry wyswietlane w rzadzie PRZED graczem
    /// (relative do OVRCameraRig). Tylko aktywny avatar jest enabled -- reszta
    /// siedzi disabled obok (oszczedzamy VRAM/draw calls na Queście).
    ///
    /// Samouruchomienie -- [RuntimeInitializeOnLoadMethod] AfterSceneLoad tworzy
    /// GameObject "AvatarGallery" w kazdej scenie. Jesli nie znajdzie rig'a,
    /// uzywa (0,0,3) jako fallback origin.
    ///
    /// API:
    ///   AvatarGallery.Instance.Count
    ///   AvatarGallery.Instance.GetName(i) / GetPrefab(i) / GetInstance(i)
    ///   AvatarGallery.Instance.SetActiveIndex(i)  -- aktywuje jeden, wylacza reszte
    ///   AvatarGallery.Instance.ActiveIndex
    /// </summary>
    [DisallowMultipleComponent]
    public class AvatarGallery : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Gallery]";

        public static AvatarGallery Instance { get; private set; }

        [Header("Layout (wzgledem OVRCameraRig jesli znaleziony)")]
        [Tooltip("Offset od gracza (x=w prawo, y=nad ziemia, z=przed)")]
        public Vector3 relativeOffset = new Vector3(0f, 0f, 3f);

        [Tooltip("Kierunek rzadu avatarow (znormalizowany)")]
        public Vector3 direction = Vector3.right;

        [Tooltip("Odstep miedzy avatarami (metry)")]
        public float spacing = 1.5f;

        [Tooltip("Obrot kazdej instancji wokol Y (stopnie). 180 = twarza do gracza.")]
        public float yaw = 180f;

        [Tooltip("Y = pozycja gracza stop zamiast centerEye. Default: ziemia.")]
        public bool useGroundY = true;

        [Header("Tryb wyswietlania")]
        [Tooltip("Lazy: tylko aktywny avatar enabled. Reszta disabled. Oszczedza VRAM.")]
        public bool lazyDisplay = true;

        [Tooltip("Index avatara aktywnego na start (-1 = zaden, wszystkie disabled)")]
        public int initialActiveIndex = -1;

        private AvatarRegistry _registry;
        private GameObject[] _instances;
        private int _activeIndex = -1;

        public int Count => _instances != null ? _instances.Length : 0;
        public int ActiveIndex => _activeIndex;

        public GameObject GetInstance(int i) =>
            (_instances != null && i >= 0 && i < _instances.Length) ? _instances[i] : null;

        public GameObject GetPrefab(int i) =>
            (_registry != null && _registry.Get(i) != null) ? _registry.Get(i).prefab : null;

        public string GetName(int i) =>
            (_registry != null && _registry.Get(i) != null) ? _registry.Get(i).name : "";

        public bool IsBroken(int i)
        {
            if (_registry == null) return false;
            var e = _registry.Get(i);
            return e != null && e.broken;
        }

        public string GetError(int i)
        {
            if (_registry == null) return null;
            var e = _registry.Get(i);
            return (e != null) ? e.errorMessage : null;
        }

        // =====================================================================
        // Auto-boot -- dodaje gallery do kazdej sceny bez edycji .unity
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            if (Instance != null) return;
            var go = new GameObject("AvatarGallery");
            go.AddComponent<AvatarGallery>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            _registry = Resources.Load<AvatarRegistry>(AvatarRegistry.ResourcesPath);
            if (_registry == null)
            {
                Debug.LogWarning($"{LOG} AvatarRegistry not found in Resources/{AvatarRegistry.ResourcesPath} -- skipping gallery");
                _instances = new GameObject[0];
                return;
            }

            int n = _registry.Count;
            if (n == 0)
            {
                Debug.Log($"{LOG} Registry empty -- no avatars to preview");
                _instances = new GameObject[0];
                return;
            }

            // Wylicz worldspace origin: OVRCameraRig position + relativeOffset (w rig.forward)
            Vector3 origin;
            Vector3 rowRight;
            ResolveOrigin(out origin, out rowRight);

            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : rowRight;
            var rot = Quaternion.Euler(0f, yaw, 0f);

            _instances = new GameObject[n];
            int spawned = 0;
            int skippedBroken = 0;
            for (int i = 0; i < n; i++)
            {
                var entry = _registry.Get(i);
                if (entry == null || entry.prefab == null)
                {
                    Debug.LogWarning($"{LOG} Registry entry {i} null/missing prefab -- skipped");
                    continue;
                }
                if (entry.broken)
                {
                    skippedBroken++;
                    Debug.LogWarning($"{LOG} Skipping broken avatar [{i}] '{entry.name}': {entry.errorMessage}");
                    continue; // _instances[i] zostaje null
                }

                Vector3 pos = origin + dir * spacing * i;
                var inst = Instantiate(entry.prefab, pos, rot, transform);
                inst.name = $"Preview_{entry.name}";
                _instances[i] = inst;
                spawned++;
            }
            if (skippedBroken > 0) Debug.LogWarning($"{LOG} Total broken skipped: {skippedBroken}/{n}");

            // Lazy: disable wszystkie, potem opcjonalnie aktywuj initialActiveIndex
            if (lazyDisplay)
            {
                for (int i = 0; i < _instances.Length; i++)
                    if (_instances[i] != null) _instances[i].SetActive(false);
            }

            if (initialActiveIndex >= 0 && initialActiveIndex < _instances.Length)
                SetActiveIndex(initialActiveIndex);

            Debug.Log($"{LOG} Spawned {spawned}/{n} avatars at {origin} (lazy={lazyDisplay}, active={_activeIndex})");
        }

        private void ResolveOrigin(out Vector3 origin, out Vector3 rowRight)
        {
            // Znajdz OVRCameraRig -- nie referujemy typu bezposrednio zeby nie wymuszac Oculus w assembly
            var rigGO = GameObject.Find("OVRCameraRig");
            if (rigGO != null)
            {
                var t = rigGO.transform;
                Vector3 fwd = t.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
                fwd.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, fwd);

                Vector3 basePos = t.position;
                if (useGroundY)
                {
                    // Raycast w dol zeby znalezc ground
                    if (Physics.Raycast(basePos + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2000f))
                        basePos.y = hit.point.y;
                }
                origin = basePos + fwd * relativeOffset.z + right * relativeOffset.x + Vector3.up * relativeOffset.y;
                rowRight = right;
                return;
            }

            // Fallback -- worldspace (0, 0, 3)
            origin = new Vector3(relativeOffset.x, relativeOffset.y, relativeOffset.z);
            rowRight = Vector3.right;
        }

        /// <summary>
        /// Aktywuje tylko jeden avatar. -1 = wszystkie disabled.
        /// </summary>
        public void SetActiveIndex(int index)
        {
            if (_instances == null) return;
            _activeIndex = Mathf.Clamp(index, -1, _instances.Length - 1);
            for (int i = 0; i < _instances.Length; i++)
            {
                if (_instances[i] == null) continue;
                bool on = (!lazyDisplay) || (i == _activeIndex);
                if (_instances[i].activeSelf != on) _instances[i].SetActive(on);
            }
            Debug.Log($"{LOG} SetActiveIndex({_activeIndex}) lazy={lazyDisplay}");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
