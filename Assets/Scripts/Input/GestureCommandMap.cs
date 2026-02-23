// GestureCommandMap.cs
// CYBERNOMAD -- ScriptableObject that maps microgestures to gameplay commands.
//
// Create via: Assets > Create > Plaga44 > Input > Gesture Command Map
// Assign to MicrogestureDispatcher in the scene.
//
// Each entry binds a (SwipeDirection | Tap) + optional Hand filter to a GestureCommand.
// At runtime GestureCommandMap translates raw gesture events from MicrogestureManager
// into GestureCommand values and re-raises them via OnCommand.
//
// #if HAS_META_XR guards OVRInput-dependent code.
// Namespace: Plaga44.Input

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plaga44.Input
{
    // ── Binding data ──────────────────────────────────────────────────────────

    /// <summary>What kind of gesture this binding responds to.</summary>
    public enum GestureType
    {
        SwipeLeft,
        SwipeRight,
        SwipeForward,
        SwipeBackward,
        Tap
    }

    [Serializable]
    public class GestureBinding
    {
        [Tooltip("Which gesture type triggers this command.")]
        public GestureType gesture = GestureType.SwipeRight;

        [Tooltip("Restrict to a specific hand, or leave as 'Either'.")]
        public HandFilter handFilter = HandFilter.Either;

        [Tooltip("The gameplay command to execute.")]
        public GestureCommand command = GestureCommand.None;

        [Tooltip("Human-readable label shown in debug overlay.")]
        public string label = "";
    }

    /// <summary>Optionally restrict a binding to one hand.</summary>
    public enum HandFilter
    {
        Either,
        LeftOnly,
        RightOnly
    }

    // ── ScriptableObject ──────────────────────────────────────────────────────

    [CreateAssetMenu(
        fileName  = "GestureCommandMap",
        menuName  = "Plaga44/Input/Gesture Command Map",
        order     = 10)]
    public class GestureCommandMap : ScriptableObject
    {
        [Header("Gesture Bindings")]
        [Tooltip("List of gesture-to-command mappings. Evaluated top-to-bottom; first match wins.")]
        public List<GestureBinding> bindings = new List<GestureBinding>
        {
            new GestureBinding { gesture = GestureType.SwipeLeft,     handFilter = HandFilter.Either,    command = GestureCommand.PreviousSlot, label = "Prev Slot"  },
            new GestureBinding { gesture = GestureType.SwipeRight,    handFilter = HandFilter.Either,    command = GestureCommand.NextSlot,     label = "Next Slot"  },
            new GestureBinding { gesture = GestureType.Tap,           handFilter = HandFilter.Either,    command = GestureCommand.MarkTarget,   label = "Mark Target"},
            new GestureBinding { gesture = GestureType.SwipeForward,  handFilter = HandFilter.Either,    command = GestureCommand.QuickHeal,    label = "Quick Heal" },
            new GestureBinding { gesture = GestureType.SwipeBackward, handFilter = HandFilter.Either,    command = GestureCommand.OpenWheel,    label = "Open Wheel" },
        };

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Raised when a gesture resolves to a non-None command.</summary>
        public event Action<GestureCommand, Hand> OnCommand;

        // ── API ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Call this when a swipe is detected. Looks up the first matching binding
        /// and raises <see cref="OnCommand"/> if found.
        /// </summary>
        public void HandleSwipe(SwipeDirection direction, Hand hand)
        {
            GestureType type = DirectionToGestureType(direction);
            Dispatch(type, hand);
        }

        /// <summary>
        /// Call this when a tap is detected. Looks up the first matching binding
        /// and raises <see cref="OnCommand"/> if found.
        /// </summary>
        public void HandleTap(Hand hand)
        {
            Dispatch(GestureType.Tap, hand);
        }

        /// <summary>
        /// Returns the command bound to a gesture type for a given hand,
        /// or <see cref="GestureCommand.None"/> if no binding matches.
        /// </summary>
        public GestureCommand Resolve(GestureType type, Hand hand)
        {
            foreach (var binding in bindings)
            {
                if (binding.gesture != type) continue;
                if (!HandMatches(binding.handFilter, hand)) continue;
                return binding.command;
            }
            return GestureCommand.None;
        }

        /// <summary>Returns the label for a given binding, or an empty string.</summary>
        public string GetLabel(GestureType type, Hand hand)
        {
            foreach (var binding in bindings)
            {
                if (binding.gesture != type) continue;
                if (!HandMatches(binding.handFilter, hand)) continue;
                return string.IsNullOrEmpty(binding.label)
                    ? binding.command.ToString()
                    : binding.label;
            }
            return string.Empty;
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        private void Dispatch(GestureType type, Hand hand)
        {
            GestureCommand cmd = Resolve(type, hand);
            if (cmd != GestureCommand.None)
                OnCommand?.Invoke(cmd, hand);
        }

        private static bool HandMatches(HandFilter filter, Hand hand)
        {
            return filter == HandFilter.Either
                || (filter == HandFilter.LeftOnly  && hand == Hand.Left)
                || (filter == HandFilter.RightOnly && hand == Hand.Right);
        }

        private static GestureType DirectionToGestureType(SwipeDirection dir)
        {
            switch (dir)
            {
                case SwipeDirection.Left:     return GestureType.SwipeLeft;
                case SwipeDirection.Right:    return GestureType.SwipeRight;
                case SwipeDirection.Forward:  return GestureType.SwipeForward;
                case SwipeDirection.Backward: return GestureType.SwipeBackward;
                default:                      return GestureType.SwipeRight;
            }
        }
    }
}
