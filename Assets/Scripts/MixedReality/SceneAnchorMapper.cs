// AUTO-DISABLED: PLAGA44_FULL_SDK
#if PLAGA44_FULL_SDK
#if HAS_META_XR
using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.MixedReality
{
    /// <summary>
    /// Uses OVRSceneManager to discover room anchors and maps real-world furniture
    /// to PLAGA '44 gameplay props.
    ///
    /// Semantic labels supported:
    ///   TABLE       --> shelter table (crafting surface)
    ///   COUCH       --> medical cot (rest/healing)
    ///   WALL_FACE   --> bunker wall (decoration / occlusion mesh)
    ///   FLOOR       --> bunker floor (navigation mesh base)
    ///   CEILING     --> bunker ceiling (decoration)
    ///   WINDOW_FRAME --> bunker observation window
    ///   DOOR_FRAME  --> bunker entrance
    ///
    /// Attach to the same GameObject as OVRSceneManager.
    /// Assign prefabs for each label in the inspector.
    /// </summary>
    [RequireComponent(typeof(OVRSceneManager))]
    public class SceneAnchorMapper : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Mapping entry                                                      //
        // ------------------------------------------------------------------ //

        [System.Serializable]
        public class SemanticMapping
        {
            [Tooltip("OVR semantic label, e.g. TABLE, COUCH, WALL_FACE, FLOOR, CEILING")]
            public string semanticLabel = "TABLE";

            [Tooltip("Gameplay prefab to spawn at this anchor.")]
            public GameObject prefab;

            [Tooltip("Vertical offset applied when placing the prefab (metres).")]
            public float yOffset = 0f;

            [Tooltip("Uniform scale multiplier applied to the spawned prefab.")]
            public float scaleFactor = 1f;
        }

        // ------------------------------------------------------------------ //
        //  Inspector fields                                                   //
        // ------------------------------------------------------------------ //

        [Header("OVRSceneManager")]
        [SerializeField] private OVRSceneManager _sceneManager;

        [Header("Semantic -> Gameplay mappings")]
        [SerializeField]
        private List<SemanticMapping> _mappings = new List<SemanticMapping>
        {
            new SemanticMapping { semanticLabel = "TABLE",       yOffset = 0f,    scaleFactor = 1f },
            new SemanticMapping { semanticLabel = "COUCH",       yOffset = 0f,    scaleFactor = 1f },
            new SemanticMapping { semanticLabel = "WALL_FACE",   yOffset = 0f,    scaleFactor = 1f },
            new SemanticMapping { semanticLabel = "FLOOR",       yOffset = 0f,    scaleFactor = 1f },
            new SemanticMapping { semanticLabel = "CEILING",     yOffset = 0f,    scaleFactor = 1f },
            new SemanticMapping { semanticLabel = "WINDOW_FRAME",yOffset = 0f,    scaleFactor = 1f },
            new SemanticMapping { semanticLabel = "DOOR_FRAME",  yOffset = 0f,    scaleFactor = 1f },
        };

        [Header("Fallback")]
        [Tooltip("Prefab used when a detected anchor has no matching semantic mapping.")]
        [SerializeField] private GameObject _unknownAnchorPrefab;

        // ------------------------------------------------------------------ //
        //  Runtime                                                            //
        // ------------------------------------------------------------------ //

        // Dictionary for O(1) lookup: label --> mapping
        private Dictionary<string, SemanticMapping> _mappingLookup;

        // All spawned gameplay props, keyed by anchor UUID string
        private Dictionary<string, GameObject> _spawnedProps = new Dictionary<string, GameObject>();

        // ------------------------------------------------------------------ //
        //  Unity lifecycle                                                    //
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            if (_sceneManager == null)
                _sceneManager = GetComponent<OVRSceneManager>();

            BuildLookup();
        }

        private void OnEnable()
        {
            _sceneManager.SceneModelLoadedSuccessfully += OnSceneModelLoaded;
            _sceneManager.NoSceneModelToLoad            += OnNoSceneModel;
        }

        private void OnDisable()
        {
            _sceneManager.SceneModelLoadedSuccessfully -= OnSceneModelLoaded;
            _sceneManager.NoSceneModelToLoad            -= OnNoSceneModel;
        }

        // ------------------------------------------------------------------ //
        //  OVRSceneManager callbacks                                         //
        // ------------------------------------------------------------------ //

        private void OnSceneModelLoaded()
        {
            Debug.Log("[PLAGA44] Scene model loaded. Mapping anchors to gameplay objects.");
            MapAllAnchors();
        }

        private void OnNoSceneModel()
        {
            Debug.LogWarning("[PLAGA44] No Scene Model available on this device. " +
                             "Ask user to run Space Setup in Quest settings.");
        }

        // ------------------------------------------------------------------ //
        //  Mapping logic                                                      //
        // ------------------------------------------------------------------ //

        private void MapAllAnchors()
        {
            // OVRSceneAnchor is the runtime component added to each discovered anchor
            var anchors = FindObjectsOfType<OVRSceneAnchor>();
            Debug.Log($"[PLAGA44] Found {anchors.Length} scene anchors.");

            foreach (var anchor in anchors)
                MapAnchor(anchor);
        }

        private void MapAnchor(OVRSceneAnchor anchor)
        {
            string uuid = anchor.Uuid.ToString();

            // Already mapped (e.g. after scene model refresh)
            if (_spawnedProps.ContainsKey(uuid)) return;

            // Determine semantic label from OVRSemanticClassification
            string label = GetSemanticLabel(anchor.gameObject);

            GameObject prefabToSpawn = null;
            float yOffset = 0f;
            float scale   = 1f;

            if (label != null && _mappingLookup.TryGetValue(label, out var mapping))
            {
                prefabToSpawn = mapping.prefab;
                yOffset       = mapping.yOffset;
                scale         = mapping.scaleFactor;
            }
            else
            {
                prefabToSpawn = _unknownAnchorPrefab;
                Debug.Log($"[PLAGA44] Anchor {uuid} -- label '{label ?? "none"}' has no mapping, using fallback.");
            }

            if (prefabToSpawn == null)
            {
                // No prefab configured for this label -- skip silently
                _spawnedProps[uuid] = null;
                return;
            }

            // Spawn parented to the anchor so it tracks with the physical object
            Vector3 spawnPos = anchor.transform.position + Vector3.up * yOffset;
            GameObject prop  = Instantiate(prefabToSpawn, spawnPos, anchor.transform.rotation, anchor.transform);
            prop.transform.localScale = Vector3.one * scale;
            prop.name = $"[MR] {label ?? "Unknown"}_{uuid.Substring(0, 8)}";

            _spawnedProps[uuid] = prop;

            Debug.Log($"[PLAGA44] Mapped anchor '{label}' ({uuid.Substring(0, 8)}) --> {prop.name}");
        }

        private string GetSemanticLabel(GameObject anchorGO)
        {
            var classification = anchorGO.GetComponent<OVRSemanticClassification>();
            if (classification == null) return null;

            // OVRSemanticClassification.Labels is a List<string>
            if (classification.Labels != null && classification.Labels.Count > 0)
                return classification.Labels[0].ToUpper();

            return null;
        }

        private void BuildLookup()
        {
            _mappingLookup = new Dictionary<string, SemanticMapping>(_mappings.Count);
            foreach (var m in _mappings)
            {
                string key = m.semanticLabel.ToUpper();
                if (!_mappingLookup.ContainsKey(key))
                    _mappingLookup[key] = m;
                else
                    Debug.LogWarning($"[PLAGA44] Duplicate semantic label in SceneAnchorMapper: {key}");
            }
        }

        // ------------------------------------------------------------------ //
        //  Public API                                                         //
        // ------------------------------------------------------------------ //

        /// <summary>Returns the spawned gameplay prop for the given anchor UUID, or null.</summary>
        public GameObject GetPropForAnchor(string uuid)
        {
            _spawnedProps.TryGetValue(uuid, out var go);
            return go;
        }

        /// <summary>Forces a remap of all current scene anchors (e.g. after scene refresh).</summary>
        public void RemapAll()
        {
            MapAllAnchors();
        }
    }
}
#endif // HAS_META_XR
#endif // PLAGA44_FULL_SDK
