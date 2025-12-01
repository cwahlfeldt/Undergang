using System;

namespace Game.Components
{
    /// <summary>
    /// Components for game state and turn management
    /// </summary>

    // State markers
    public readonly record struct Active();
    public readonly record struct CurrentTurn();
    public readonly record struct WaitingForAction();
    public readonly record struct SelectedTile();

    /// <summary>
    /// Turn order component with comparison support for sorting
    /// </summary>
    public record struct TurnOrder(int Value) : IComparable<TurnOrder>
    {
        public static implicit operator int(TurnOrder index) => index.Value;
        public static implicit operator TurnOrder(int value) => new(value);

        public readonly int CompareTo(TurnOrder other) => Value.CompareTo(other.Value);
    }
}
