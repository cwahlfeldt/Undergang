using Godot;

namespace Game.Components
{
    /// <summary>
    /// Components related to unit movement
    /// </summary>

    public record struct MoveRange(int Value)
    {
        public static implicit operator int(MoveRange range) => range.Value;
    }

    /// <summary>
    /// Marks a unit as currently moving from one tile to another
    /// </summary>
    public record struct Movement(Vector3I From, Vector3I To)
    {
        public static implicit operator (Vector3I, Vector3I)(Movement movement) =>
            (movement.From, movement.To);
    }

    /// <summary>
    /// Dash ability cooldown tracker
    /// Tracks remaining turns until dash is available again
    /// </summary>
    public record struct DashCooldown(int RemainingTurns)
    {
        public static implicit operator int(DashCooldown cooldown) => cooldown.RemainingTurns;
    }

    /// <summary>
    /// Marker component indicating dash mode is active
    /// </summary>
    public readonly record struct DashModeActive();

    /// <summary>
    /// Marker component indicating a tile is within dash range
    /// </summary>
    public readonly record struct DashRangeTile();

    /// <summary>
    /// Block ability cooldown tracker
    /// Tracks remaining turns until block is available again
    /// </summary>
    public record struct BlockCooldown(int RemainingTurns)
    {
        public static implicit operator int(BlockCooldown cooldown) => cooldown.RemainingTurns;
    }

    /// <summary>
    /// Marker component indicating block is currently active
    /// When active, the next incoming attack will be negated
    /// </summary>
    public readonly record struct BlockActive();
}
