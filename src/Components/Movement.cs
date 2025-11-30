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
}
