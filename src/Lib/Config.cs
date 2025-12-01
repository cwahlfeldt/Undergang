using Godot;

public record Config
{
    // Player settings
    public static Vector3I PlayerStart = new(0, 4, -4);
    public static int PlayerSpawnExclusionRadius = 3;

    // Map generation settings
    public static int DefaultMapSize = 5;
    public static int DefaultBlockedTilesCount = 16;
    public static int BlockedTileIndexMin = 20;
    public static int BlockedTileIndexMax = 90;

    // Range settings
    public static int DiagonalRangeMin = 2;
    public static int DiagonalRangeMax = 5;
    public static int HexRingDistance = 2;
    public static int ExplosionRadius = 2;
}