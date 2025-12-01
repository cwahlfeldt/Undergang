namespace Game.Components
{
    /// <summary>
    /// Components related to combat mechanics
    /// </summary>

    // Combat markers
    public readonly record struct Attacker();
    public readonly record struct Target();

    // Combat data
    public record struct Health(int Value)
    {
        public static implicit operator int(Health health) => health.Value;
    }

    public record struct Damage(int Value)
    {
        public static implicit operator int(Damage damage) => damage.Value;
    }

    public record struct AttackRange(int Value)
    {
        public static implicit operator int(AttackRange range) => range.Value;
    }

    /// <summary>
    /// Marks a tile as being in a unit's attack range
    /// Value stores the entity ID of the attacking unit
    /// </summary>
    public record struct AttackRangeTile(int Value)
    {
        public static implicit operator int(AttackRangeTile range) => range.Value;
    }
}
