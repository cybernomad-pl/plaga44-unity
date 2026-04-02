// AUTO-DISABLED: PLAGA44_FULL_SDK
#if PLAGA44_FULL_SDK
// GestureTypes.cs
// CYBERNOMAD -- Shared enums and types for the Plaga44.Input namespace.
// Namespace: Plaga44.Input

namespace Plaga44.Input
{
    /// <summary>Identifies which controller hand performed a gesture.</summary>
    public enum Hand
    {
        Left,
        Right
    }

    /// <summary>Gameplay commands that can be bound to microgestures.</summary>
    public enum GestureCommand
    {
        None,
        PreviousSlot,
        NextSlot,
        MarkTarget,
        QuickHeal,
        OpenWheel,
        CloseWheel,
        Reload,
        ToggleMap,
        SignalGo,
        SignalStop
    }
}
#endif // PLAGA44_FULL_SDK
