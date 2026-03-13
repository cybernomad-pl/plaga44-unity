// FaceTrackingManager.cs
// CYBERNOMAD -- Initializes OVRFaceExpressions and provides runtime blendshape API.
// Requires: com.meta.xr.sdk.core (auto-detected via HAS_META_XR define)
//
// Usage:
//   var manager = FaceTrackingManager.Instance;
//   float jaw = manager.GetExpression(OVRFaceExpressions.FaceExpression.JawOpen);

using UnityEngine;

namespace Plaga44.FaceTracking
{
    /// <summary>
    /// MonoBehaviour that wraps OVRFaceExpressions and provides a clean API
    /// for reading facial blendshape weights at runtime.
    /// Add to the same GameObject as OVRCameraRig or a persistent manager.
    /// </summary>
    public class FaceTrackingManager : MonoBehaviour
    {
        private const string LOG = "[FaceTracking]";

        // ── Singleton ────────────────────────────────────────────────────

        private static FaceTrackingManager _instance;

        /// <summary>
        /// Lazily spawned singleton. Persists across scenes.
        /// </summary>
        public static FaceTrackingManager Instance
        {
            get
            {
                if (_instance == null)
                    Spawn();
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            Spawn();
        }

        public static void Spawn()
        {
            if (_instance != null) return;
            var go = new GameObject("FaceTrackingManager");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<FaceTrackingManager>();
        }

        // ── State ────────────────────────────────────────────────────────

        /// <summary>
        /// Whether face tracking is currently valid and providing data.
        /// </summary>
        public bool IsTracking { get; private set; }

#if HAS_META_XR
        private OVRFaceExpressions _faceExpressions;
#endif

        // ── Unity lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            InitializeFaceTracking();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // ── Initialization ───────────────────────────────────────────────

        private void InitializeFaceTracking()
        {
#if HAS_META_XR
            // Look for OVRFaceExpressions in scene (should be on OVRCameraRig)
            _faceExpressions = FindFirstObjectByType<OVRFaceExpressions>();

            if (_faceExpressions == null)
            {
                // Not found in scene -- create a new GO for it
                var go = new GameObject("OVRFaceExpressions");
                go.transform.SetParent(transform);
                _faceExpressions = go.AddComponent<OVRFaceExpressions>();
                Debug.Log($"{LOG} Created OVRFaceExpressions component.");
            }
            else
            {
                Debug.Log($"{LOG} Found existing OVRFaceExpressions in scene.");
            }

            Debug.Log($"{LOG} Face tracking initialized. OVRFaceExpressions ready.");
#else
            Debug.LogWarning($"{LOG} HAS_META_XR not defined. Face tracking unavailable.");
#endif
        }

        // ── Public API ───────────────────────────────────────────────────

        /// <summary>
        /// Returns the current weight [0..1] for the given facial expression blendshape.
        /// Returns 0 when face tracking is unavailable or the expression is not valid.
        /// </summary>
        /// <param name="expression">The OVRFaceExpressions.FaceExpression to query.</param>
        /// <returns>Blendshape weight in [0..1] range.</returns>
        public float GetExpression(
#if HAS_META_XR
            OVRFaceExpressions.FaceExpression expression
#else
            int expression
#endif
        )
        {
#if HAS_META_XR
            if (_faceExpressions == null) return 0f;
            if (!_faceExpressions.FaceTrackingEnabled) return 0f;
            if (!_faceExpressions.ValidExpressions) return 0f;

            IsTracking = true;

            // OVRFaceExpressions implements IReadOnlyList<float> indexed by enum
            int index = (int)expression;
            if (index < 0 || index >= (int)OVRFaceExpressions.FaceExpression.Max)
                return 0f;

            return _faceExpressions[expression];
#else
            IsTracking = false;
            return 0f;
#endif
        }

        // ── Update ───────────────────────────────────────────────────────

        private void Update()
        {
#if HAS_META_XR
            if (_faceExpressions == null) return;
            IsTracking = _faceExpressions.FaceTrackingEnabled
                         && _faceExpressions.ValidExpressions;
#endif
        }
    }
}
