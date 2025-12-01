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

        private void OnUnitDefeated(Entity unit)
        {
            // When a unit is defeated, refresh attack ranges
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
            foreach (var tile in Entities.Query<AttackRangeTile>())
            {
                tile.Remove<AttackRangeTile>();
            }

            // assign the unit id to a tile for reference of its attack range
            foreach (var u in Entities.Query<Unit>())
            {
                foreach (var coord in GetAttackRangeTiles(u, u.Get<Coordinate>()))
                {
                    var tile = Entities.GetAt(coord);
                    if (tile != null && tile.Has<Traversable>())
                        tile.Add(new AttackRangeTile(u.Id));
                }
            }
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

            if (unit.Has<RangeAxisQ>())
                return GetRangeAxisQ(fromPosition);

            if (unit.Has<RangeAxisR>())
                return GetRangeAxisR(fromPosition);

            if (unit.Has<RangeAxisS>())
                return GetRangeAxisS(fromPosition);

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

        /// <summary>
        /// Axis Q pattern: Shoots East-West along q axis (2 opposite directions)
        /// In hex cube coordinates, Q axis is: (+q, 0, -q) and (-q, 0, +q)
        /// Distance 2-5 tiles in both directions
        /// </summary>
        public static IEnumerable<Vector3I> GetRangeAxisQ(Vector3I center)
        {
            var tiles = new List<Vector3I>();

            // Q axis directions: East (+q, 0r, -s) and West (-q, 0r, +s)
            for (int distance = Config.AxisRangeMin; distance <= Config.AxisRangeMax; distance++)
            {
                // East: increase q, decrease s, r stays 0
                tiles.Add(center + new Vector3I(distance, 0, -distance));
                // West: decrease q, increase s, r stays 0
                tiles.Add(center + new Vector3I(-distance, 0, distance));
            }

            return tiles;
        }

        /// <summary>
        /// Axis R pattern: Shoots along r axis (2 opposite directions)
        /// In hex cube coordinates, R axis is: (0, +r, -r) and (0, -r, +r)
        /// Distance 2-5 tiles in both directions
        /// </summary>
        public static IEnumerable<Vector3I> GetRangeAxisR(Vector3I center)
        {
            var tiles = new List<Vector3I>();

            // R axis directions: (0q, +r, -s) and (0q, -r, +s)
            for (int distance = Config.AxisRangeMin; distance <= Config.AxisRangeMax; distance++)
            {
                // Increase r, decrease s, q stays 0
                tiles.Add(center + new Vector3I(0, distance, -distance));
                // Decrease r, increase s, q stays 0
                tiles.Add(center + new Vector3I(0, -distance, distance));
            }

            return tiles;
        }

        /// <summary>
        /// Axis S pattern: Shoots along s axis (2 opposite directions)
        /// In hex cube coordinates, S axis is: (0, +q, -r) and (0, -q, +r)
        /// Distance 2-5 tiles in both directions
        /// </summary>
        public static IEnumerable<Vector3I> GetRangeAxisS(Vector3I center)
        {
            var tiles = new List<Vector3I>();

            // S axis directions: (+q, -r, 0s) and (-q, +r, 0s)
            for (int distance = Config.AxisRangeMin; distance <= Config.AxisRangeMax; distance++)
            {
                // Increase q, decrease r, s stays 0
                tiles.Add(center + new Vector3I(distance, -distance, 0));
                // Decrease q, increase r, s stays 0
                tiles.Add(center + new Vector3I(-distance, distance, 0));
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
