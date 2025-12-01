using Godot;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Game.Components;

namespace Game
{
    public class RangeSystem : System
    {
        public override void Initialize()
        {
            Events.UnitDefeated += OnUnitDefeated;
            Events.MoveCompleted += OnMoveCompleted;

            UpdateRanges();
        }

        public override async Task Update()
        {
            UpdateRanges();
        }

        private void OnUnitDefeated(Entity unit)
        {
            // When a unit is defeated, refresh attack ranges
            GD.Print("RangeSystem: Unit defeated, refreshing attack ranges");
            UpdateRanges();
        }

        private void OnMoveCompleted(Entity unit, Vector3I from, Vector3I to)
        {
            // When a unit moves, refresh attack ranges
            UpdateRanges();
        }

        private void UpdateRanges()
        {
            // remove old
            Entities.Query<AttackRangeTile>()
                .ToList()
                .ForEach(tile =>
                    tile.Remove<AttackRangeTile>());

            // assign the unit id to a tile for reference of its attack range
            Entities.Query<Unit>()
                .ToList()
                .ForEach(u =>
                {
                    var coordsInRange = GetAttackRangeTiles(u, u.Get<Coordinate>()).ToList();
                    coordsInRange.ForEach(coord =>
                    {
                        var tile = Entities.GetAt(coord);
                        if (tile != null && tile.Has<Traversable>())
                            tile.Add(new AttackRangeTile(u.Id));
                    });
                });
        }

        // Range Functions

        /// <summary>
        /// Gets attack range tiles for a unit based on their range type component
        /// </summary>
        public static IEnumerable<Vector3I> GetAttackRangeTiles(Entity unit, Vector3I fromPosition)
        {
            if (unit.Has<RangeCircle>())
                return GetRangeCircle(fromPosition);

            if (unit.Has<RangeDiagonal>())
                return GetRangeDiagonal(fromPosition);

            if (unit.Has<RangeHex>())
                return GetRangeHex(fromPosition);

            if (unit.Has<RangeExplosion>())
                return GetRangeExplosion(fromPosition);

            if (unit.Has<RangeNGon>())
                return GetRangeNGon(fromPosition);

            // Default to empty if no range type
            return Enumerable.Empty<Vector3I>();
        }

        /// <summary>
        /// Gets attack range tiles for a coordinate (when we don't have the entity)
        /// Assumes RangeCircle for now - can be extended
        /// </summary>
        public static IEnumerable<Vector3I> GetRangeCircle(Vector3I center)
        {
            return HexGrid.Directions.Values.Select(dir => center + dir);
        }

        public static IEnumerable<Vector3I> GetRangeDiagonal(Vector3I center)
        {
            // Directional lines along 6 hex directions, range 2-5
            // (Hoplite Archer behavior: can shoot in 6 directions, not adjacent, max 5 tiles)
            var tiles = new List<Vector3I>();

            foreach (var direction in HexGrid.Directions.Values)
            {
                for (int distance = Config.DiagonalRangeMin; distance <= Config.DiagonalRangeMax; distance++)
                {
                    tiles.Add(center + direction * distance);
                }
            }

            return tiles;
        }

        public static IEnumerable<Vector3I> GetRangeHex(Vector3I center)
        {
            // Hex ring at specific distance (tiles exactly N steps away)
            var tiles = new List<Vector3I>();
            var allTilesInRange = HexGrid.GetHexesInRange(center, Config.HexRingDistance);

            foreach (var coord in allTilesInRange)
            {
                if (HexGrid.GetDistance(center, coord) == Config.HexRingDistance)
                {
                    tiles.Add(coord);
                }
            }

            return tiles;
        }

        public static IEnumerable<Vector3I> GetRangeExplosion(Vector3I center)
        {
            // All tiles within radius (area of effect)
            var tiles = new List<Vector3I>();
            var allTilesInRange = HexGrid.GetHexesInRange(center, Config.ExplosionRadius);

            foreach (var coord in allTilesInRange)
            {
                // Exclude the center tile itself
                if (coord != center)
                {
                    tiles.Add(coord);
                }
            }

            return tiles;
        }

        public static IEnumerable<Vector3I> GetRangeNGon(Vector3I center)
        {
            // N-gon pattern: alternating directions forming polygon shape
            // Uses every other hex direction to create triangular pattern
            var tiles = new List<Vector3I>();
            var directions = HexGrid.Directions.Values.ToList();

            // Take every other direction (creates triangular/hexagonal pattern)
            for (int i = 0; i < directions.Count; i += 2)
            {
                var direction = directions[i];
                for (int distance = 1; distance <= 3; distance++)
                {
                    tiles.Add(center + direction * distance);
                }
            }

            return tiles;
        }

        public override void Cleanup()
        {
            Events.UnitDefeated -= OnUnitDefeated;
            Events.MoveCompleted -= OnMoveCompleted;
        }
    }
}
