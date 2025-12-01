namespace Game.Components
{
    /// <summary>
    /// Range pattern marker components
    /// Each component defines a different attack range pattern
    /// See RangeSystem.cs for pattern implementations
    /// </summary>

    /// <summary>
    /// Circle pattern: Adjacent tiles (6 hex neighbors)
    /// </summary>
    public readonly record struct RangeCircle();

    /// <summary>
    /// Diagonal pattern: Directional lines along 6 hex directions, distance 2-5
    /// (Hoplite Archer behavior)
    /// </summary>
    public readonly record struct RangeDiagonal();

    /// <summary>
    /// Explosion pattern: All tiles within radius (area of effect)
    /// </summary>
    public readonly record struct RangeExplosion();

    /// <summary>
    /// Hex ring pattern: Tiles exactly N steps away forming a ring
    /// </summary>
    public readonly record struct RangeHex();

    /// <summary>
    /// N-gon pattern: Alternating directions forming polygon shape
    /// </summary>
    public readonly record struct RangeNGon();
}
