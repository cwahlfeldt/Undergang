namespace Game.Components
{
    /// <summary>
    /// Components for unit types and classifications
    /// </summary>

    // Unit classification markers
    public readonly record struct Player();
    public readonly record struct Enemy();

    // Enemy type markers
    public readonly record struct Grunt();
    public readonly record struct Wizard();
    public readonly record struct SniperAxisQ();
    public readonly record struct SniperAxisR();
    public readonly record struct SniperAxisS();

    /// <summary>
    /// Unit type component storing the specific unit type
    /// </summary>
    public record struct Unit(UnitType Type)
    {
        public static implicit operator UnitType(Unit type) => type.Type;
        public override readonly string ToString() => Type.ToString();
    }
}
