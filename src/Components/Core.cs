using Godot;

namespace Game.Components
{
    /// <summary>
    /// Core components for tiles and basic entity properties
    /// </summary>

    // Tile markers
    public readonly record struct Tile();
    public readonly record struct Traversable();
    public readonly record struct Untraversable();

    // Data components
    public record struct Instance(Node3D Node)
    {
        public static implicit operator Node3D(Instance node) => node.Node;
    }

    public record struct Name(StringName Value)
    {
        public static implicit operator StringName(Name name) => name.Value;
    }

    public record struct TileIndex(int Value)
    {
        public static implicit operator int(TileIndex index) => index.Value;
    }

    public record struct Coordinate(Vector3I Value)
    {
        public static implicit operator Vector3I(Coordinate coord) => coord.Value;
    }
}
