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

    // Rewind feature settings
    public const int RewindCooldownTurns = 3;
    public const int MaxHistoryDepth = 100;
    public const float RewindAnimationSpeed = 0.3f;
    public const float RespawnFadeInDuration = 0.5f;

    // Animation settings
    public const float AnimationBlendTime = 0.2f;
    public const float RotationAnimationSpeed = 0.15f;
    public const int SpawnStaggerDelayMs = 300;
    public const int PlayerSpawnDelayMs = 600;
    public const int FallbackAttackDurationMs = 300;
    public const float AttackLungeDuration = 0.15f;
    public const float AttackLungeDistance = 0.8f;

    // UI settings
    public const int TileSelectDurationMs = 500;
}