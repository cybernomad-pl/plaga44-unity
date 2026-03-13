namespace Plaga44.AI
{
    /// <summary>
    /// States for the enemy AI state machine.
    /// Idle       -- standing still, no patrol assigned or waiting between patrols.
    /// Patrol     -- walking between PatrolPath waypoints.
    /// Alert      -- heard/spotted something, turning to investigate.
    /// Chase      -- actively pursuing the player.
    /// Attack     -- within melee range, dealing damage.
    /// Dead       -- health depleted, physics ragdoll / death animation.
    /// </summary>
    public enum EnemyState
    {
        Idle,
        Patrol,
        Alert,
        Chase,
        Attack,
        Dead
    }
}
