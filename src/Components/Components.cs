using System;
using System.Collections.Generic;
using Godot;

namespace Game.Components
{
    // markers
    public readonly record struct Tile;
    public readonly record struct Traversable;
    public readonly record struct Untraversable;
    public readonly record struct Player;
    public readonly record struct Enemy;
    public readonly record struct Grunt;
    public readonly record struct Sniper;
    public readonly record struct Attacker;
    public readonly record struct Target;
    public readonly record struct Active;
    public readonly record struct CurrentTurn;
    public readonly record struct WaitingForAction;
    public readonly record struct SelectedTile;
    public readonly record struct RangeCircle;
    public readonly record struct RangeDiagonal;
    public readonly record struct RangeExplosion;
    public readonly record struct RangeHex;
    public readonly record struct RangeNGon;


    public record struct Instance(Node3D Node) { public static implicit operator Node3D(Instance node) => node.Node; }
    public record struct Name(StringName Value) { public static implicit operator StringName(Name name) => name.Value; }
    public record struct TileIndex(int Value) { public static implicit operator int(TileIndex index) => index.Value; }
    public record struct Health(int Value) { public static implicit operator int(Health health) => health.Value; }
    public record struct Coordinate(Vector3I Value) { public static implicit operator Vector3I(Coordinate coord) => coord.Value; }
    public record struct Damage(int Value) { public static implicit operator int(Damage damage) => damage.Value; }
    public record struct MoveRange(int Value) { public static implicit operator int(MoveRange range) => range.Value; }
    public record struct AttackRange(int Value) { public static implicit operator int(AttackRange range) => range.Value; }
    public record struct AttackRangeTile(int Value) { public static implicit operator int(AttackRangeTile range) => range.Value; }

    public record struct TurnOrder(int Value) : IComparable<TurnOrder>
    {
        public static implicit operator int(TurnOrder index) => index.Value;
        public static implicit operator TurnOrder(int value) => new(value);

        public readonly int CompareTo(TurnOrder other) => Value.CompareTo(other.Value);
    }

    public record struct Unit(UnitType Type)
    {
        public static implicit operator UnitType(Unit type) => type.Type;
        public override readonly string ToString() => Type.ToString();
    }

    public record struct Movement(Vector3I From, Vector3I To)
    {
        public static implicit operator (Vector3I, Vector3I)(Movement movement) =>
            (movement.From, movement.To);
    }

    /// <summary>
    /// Component to track current animation state of a unit
    /// </summary>
    public record struct CurrentAnimation(AnimationState State)
    {
        public static implicit operator AnimationState(CurrentAnimation animation) => animation.State;
    }

    /// <summary>
    /// Component to store reference to the AnimationPlayer node for a unit
    /// </summary>
    public record struct AnimationPlayer(Godot.AnimationPlayer Player)
    {
        public static implicit operator Godot.AnimationPlayer(AnimationPlayer player) => player.Player;
    }
}