// AUTO-DISABLED: PLAGA44_FULL_SDK
#if PLAGA44_FULL_SDK
// MicrogestureDispatcher.cs
// CYBERNOMAD -- Glue MonoBehaviour: wires MicrogestureManager events to GestureCommandMap.
//
// Usage:
//   1. Place MicrogestureManager in the scene (or let it auto-create via singleton).
//   2. Create a GestureCommandMap asset (Assets > Create > Plaga44 > Input > Gesture Command Map).
//   3. Place this component on any GameObject, assign the CommandMap reference.
//   4. Optionally assign a QuickActionWheel and InputDebugOverlay for full integration.
//
// This component does NOT contain gesture logic -- it only bridges the other components.
// Namespace: Plaga44.Input

using UnityEngine;

namespace Plaga44.Input
{
    /// <summary>
    /// Wires <see cref="MicrogestureManager"/> to a <see cref="GestureCommandMap"/>
    /// and optionally to a <see cref="QuickActionWheel"/> and <see cref="InputDebugOverlay"/>.
    /// </summary>
    public class MicrogestureDispatcher : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Required")]
        [Tooltip("Gesture-to-command mapping. Create via Assets > Create > Plaga44 > Input > Gesture Command Map.")]
        public GestureCommandMap commandMap;

        [Header("Optional integrations")]
        [Tooltip("Radial wheel to open/close via gesture commands.")]
        public QuickActionWheel quickActionWheel;

        [Tooltip("Debug overlay to show live gesture events.")]
        public InputDebugOverlay debugOverlay;

        [Header("Auto-create Manager")]
        [Tooltip("If true and MicrogestureManager.Instance is null, spawn one automatically.")]
        public bool autoCreateManager = true;

        // ── Private ───────────────────────────────────────────────────────────

        private MicrogestureManager _manager;
        private bool _subscribed;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (commandMap == null)
            {
                Debug.LogWarning("[MicrogestureDispatcher] CommandMap is not assigned. Gesture commands will not fire.", this);
                return;
            }
        }

        private void Start()
        {
            EnsureManager();
            Subscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        // ── Manager bootstrap ─────────────────────────────────────────────────

        private void EnsureManager()
        {
            _manager = MicrogestureManager.Instance;

            if (_manager == null && autoCreateManager)
            {
                var go = new GameObject("MicrogestureManager");
                DontDestroyOnLoad(go);
                _manager = go.AddComponent<MicrogestureManager>();
                Debug.Log("[MicrogestureDispatcher] Auto-created MicrogestureManager.", this);
            }

            if (_manager == null)
                Debug.LogWarning("[MicrogestureDispatcher] No MicrogestureManager found in scene.", this);
        }

        // ── Subscription ──────────────────────────────────────────────────────

        private void Subscribe()
        {
            if (_subscribed || _manager == null || commandMap == null) return;

            _manager.OnSwipe += OnSwipe;
            _manager.OnTap   += OnTap;

            // Wire command map to debug overlay
            if (debugOverlay != null)
            {
                debugOverlay.gestureManager = _manager;
                debugOverlay.commandMap     = commandMap;
            }

            // Wire command map to wheel
            if (quickActionWheel != null)
                quickActionWheel.commandMap = commandMap;

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _manager == null) return;
            _manager.OnSwipe -= OnSwipe;
            _manager.OnTap   -= OnTap;
            _subscribed = false;
        }

        // ── Gesture handlers ──────────────────────────────────────────────────

        private void OnSwipe(SwipeDirection direction, Hand hand)
        {
            if (commandMap == null) return;
            commandMap.HandleSwipe(direction, hand);
        }

        private void OnTap(Hand hand)
        {
            if (commandMap == null) return;
            commandMap.HandleTap(hand);
        }
    }
}
#endif // PLAGA44_FULL_SDK
