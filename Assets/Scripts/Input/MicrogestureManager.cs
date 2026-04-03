// MicrogestureManager.cs
// CYBERNOMAD -- Detects thumb microgestures (swipe/tap) via OVRInput.
// Singleton MonoBehaviour. Raises events consumed by GestureCommandMap.
//
// Gesture detection strategy:
//   Swipe  -- thumb joystick displacement crosses deadzone, holds for minSwipeFrames,
//             then releases. Direction locked at gesture start.
//   Tap    -- thumbstick touched (OVRInput.Touch.PrimaryThumbstick) for a very short
//             window (< tapMaxDuration seconds) with near-zero displacement.
//
// #if HAS_META_XR guards all OVRInput calls so the file compiles without the SDK.
// Namespace: Plaga44.Input

using System;
using UnityEngine;

namespace Plaga44.Input
{
    /// <summary>Four cardinal directions for a thumb swipe gesture.</summary>
    public enum SwipeDirection
    {
        Left,
        Right,
        Forward,
        Backward
    }

    /// <summary>
    /// Singleton MonoBehaviour that detects thumb microgestures on Quest Touch controllers
    /// and broadcasts <see cref="OnSwipe"/> / <see cref="OnTap"/> events.
    /// </summary>
    public class MicrogestureManager : MonoBehaviour
    {
        // ── Singleton ───────────────────────────────────────────────────────

        public static MicrogestureManager Instance { get; private set; }

        // ── Inspector ───────────────────────────────────────────────────────

        [Header("Swipe Detection")]
        [Tooltip("Minimum thumbstick displacement to start tracking a swipe (0..1).")]
        [Range(0.1f, 0.9f)]
        public float swipeDeadzone = 0.35f;

        [Tooltip("Minimum frames the thumb must stay past the deadzone before a swipe is confirmed.")]
        [Min(1)]
        public int minSwipeFrames = 3;

        [Header("Tap Detection")]
        [Tooltip("Maximum seconds a thumb touch can last to be classified as a tap (not a swipe).")]
        [Range(0.05f, 0.5f)]
        public float tapMaxDuration = 0.18f;

        [Tooltip("Maximum thumbstick displacement during the tap window (0..1).")]
        [Range(0.01f, 0.3f)]
        public float tapMaxDisplacement = 0.12f;

        [Header("Cooldown")]
        [Tooltip("Minimum seconds between any two gesture events (per hand).")]
        [Range(0.05f, 1f)]
        public float gestureCooldown = 0.25f;

        // ── Events ──────────────────────────────────────────────────────────

        /// <summary>Fired when a thumb swipe is detected on either hand.</summary>
        public event Action<SwipeDirection, Hand> OnSwipe;

        /// <summary>Fired when a thumb tap is detected.</summary>
        public event Action<Hand> OnTap;

        // ── Private state ────────────────────────────────────────────────────

        private HandGestureState _leftState;
        private HandGestureState _rightState;

        // ── Unity ────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _leftState  = new HandGestureState(Hand.Left);
            _rightState = new HandGestureState(Hand.Right);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
#if HAS_META_XR
            UpdateHand(_leftState,  OVRInput.Controller.LTouch);
            UpdateHand(_rightState, OVRInput.Controller.RTouch);
#endif
        }

#if HAS_META_XR
        // ── Per-hand update ──────────────────────────────────────────────────

        private void UpdateHand(HandGestureState state, OVRInput.Controller ctrl)
        {
            state.cooldownTimer -= Time.deltaTime;

            Vector2 stick      = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, ctrl);
            bool    thumbTouch = OVRInput.Get(OVRInput.Touch.PrimaryThumbstick, ctrl);

            // --- Touch started this frame ---
            if (thumbTouch && !state.wasThumbTouching)
            {
                state.touchStartTime      = Time.time;
                state.maxDisplacementSeen = 0f;
                state.swipeFrameCount     = 0;
                state.swipeDirectionLocked = false;
                state.lockedDirection      = default;
            }

            // --- While touching ---
            if (thumbTouch)
            {
                float disp = stick.magnitude;
                if (disp > state.maxDisplacementSeen)
                    state.maxDisplacementSeen = disp;

                if (disp >= swipeDeadzone)
                {
                    // Lock direction on first frame past deadzone
                    if (!state.swipeDirectionLocked)
                    {
                        state.lockedDirection      = ComputeDirection(stick);
                        state.swipeDirectionLocked = true;
                        state.swipeFrameCount      = 0;
                    }
                    state.swipeFrameCount++;
                }
                else if (state.swipeDirectionLocked)
                {
                    // Dropped back below deadzone -- reset candidate
                    state.swipeDirectionLocked = false;
                    state.swipeFrameCount      = 0;
                }
            }

            // --- Touch released this frame ---
            if (!thumbTouch && state.wasThumbTouching)
            {
                float touchDuration = Time.time - state.touchStartTime;

                if (state.cooldownTimer <= 0f)
                {
                    if (state.swipeDirectionLocked && state.swipeFrameCount >= minSwipeFrames)
                    {
                        // Swipe confirmed
                        state.cooldownTimer = gestureCooldown;
                        OnSwipe?.Invoke(state.lockedDirection, state.hand);
                    }
                    else if (touchDuration <= tapMaxDuration
                             && state.maxDisplacementSeen <= tapMaxDisplacement)
                    {
                        // Tap confirmed
                        state.cooldownTimer = gestureCooldown;
                        OnTap?.Invoke(state.hand);
                    }
                }

                // Always reset after release
                state.swipeDirectionLocked = false;
                state.swipeFrameCount      = 0;
            }

            state.wasThumbTouching = thumbTouch;
        }

        // ── Direction helper ─────────────────────────────────────────────────

        /// <summary>
        /// Maps a thumbstick vector to a <see cref="SwipeDirection"/>.
        /// Forward/Backward = Y axis. Left/Right = X axis.
        /// Dominant axis wins.
        /// </summary>
        private static SwipeDirection ComputeDirection(Vector2 stick)
        {
            if (Mathf.Abs(stick.x) >= Mathf.Abs(stick.y))
                return stick.x > 0f ? SwipeDirection.Right : SwipeDirection.Left;
            else
                return stick.y > 0f ? SwipeDirection.Forward : SwipeDirection.Backward;
        }
#endif

        // ── Internal state container ─────────────────────────────────────────

        private class HandGestureState
        {
            public readonly Hand hand;

            public bool  wasThumbTouching;
            public float touchStartTime;
            public float maxDisplacementSeen;

            // Swipe tracking
            public bool           swipeDirectionLocked;
            public SwipeDirection lockedDirection;
            public int            swipeFrameCount;

            // Cooldown
            public float cooldownTimer;

            public HandGestureState(Hand h) { hand = h; }
        }
    }
}
