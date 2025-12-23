using Godot;

public record Config
{
    // Player settings
    public static Vector3I PlayerStart = new(0, 4, -4);
    public const int PlayerSpawnExclusionRadius = 3;

    // Map generation settings
    public const int DefaultMapSize = 5;
    public const int DefaultBlockedTilesCount = 24;
    public const int BlockedTileIndexMin = 20;
    public const int BlockedTileIndexMax = 90;

    // Range settings
    public const int DiagonalRangeMin = 2;
    public const int DiagonalRangeMax = 6;
    public const int HexRingDistance = 2;
    public const int ExplosionRadius = 2;
    public const int AxisRangeMin = 2;
    public const int AxisRangeMax = 5;

    // Dash ability settings
    public const int DashRange = 2;
    public const int DashCooldown = 4;
    public const float DashAnimationSpeed = 0.25f;
    public const float NormalMoveAnimationSpeed = 0.25f;

    // Block ability settings
    public const int BlockCooldown = 3;
    public const int BlockDuration = 1;  // Number of turns block stays active

    // Rewind feature settings
    public const int RewindCooldownTurns = 3;  // Turns to wait between rewinds
    public const int MaxHistoryDepth = 100;    // Max snapshots to retain (full history)
    public const float RewindAnimationSpeed = 0.3f;  // Speed of rewind animation
    public const float RespawnFadeInDuration = 0.5f; // Duration of fade-in for respawned units
}