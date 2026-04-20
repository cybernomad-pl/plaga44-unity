// =============================================================================
// PlayerMotionState.cs
// CYBERNOMAD -- Runtime motion state gracza. Publikowany przez LocomotionController,
// konsumowany przez PlayerAnimationController + FreefallCameraController.
//
// Stany:
//   Idle       -- stoimy nieruchomo na ziemi
//   Locomotion -- chodzenie/bieg (Speed > 0, grounded)
//   Fly        -- latanie (prawy stick up, !grounded)
//   Freefall   -- swobodny spadek (StratoJump start, !grounded, brak fly inputu)
//   Landing    -- krótka transition po zetknieciu z ziemia z Freefall
// =============================================================================

namespace Plaga44.Locomotion
{
    public enum PlayerMotionState
    {
        Idle       = 0,
        Locomotion = 1,
        Fly        = 2,
        Freefall   = 3,
        Landing    = 4,
    }

    /// <summary>Centralny publisher stanu. Singleton na OVRCameraRig.</summary>
    public interface IPlayerMotionSource
    {
        PlayerMotionState CurrentState { get; }
        /// <summary>Linear speed magnitude (m/s). 0 w Idle/Fly/Freefall.</summary>
        float Speed { get; }
        /// <summary>Lateral (strafe) axis -1..1 for blend tree, 0 = forward.</summary>
        float StrafeX { get; }
        /// <summary>Forward axis -1..1 for blend tree, +1 = forward, -1 = backward.</summary>
        float ForwardZ { get; }
    }
}
