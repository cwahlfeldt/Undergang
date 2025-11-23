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
                for (int distance = 2; distance <= 5; distance++)
                {
                    tiles.Add(center + direction * distance);
                }
            }

            return tiles;
        }

        public static IEnumerable<Vector3I> GetRangeHex(Vector3I center)
        {
            // TODO: Implement hex range pattern (ring at distance 2?)
            return GetRangeCircle(center);
        }

        public static IEnumerable<Vector3I> GetRangeExplosion(Vector3I center)
        {
            // TODO: Implement explosion range (all tiles within radius 2?)
            return GetRangeCircle(center);
        }

        public static IEnumerable<Vector3I> GetRangeNGon(Vector3I center)
        {
            // TODO: Implement N-gon range pattern
            return GetRangeCircle(center);
        }

        /// <summary>
        /// Shoots along Q axis only (East-West directions)
        /// In cube coords: varying q, r and s constant
        /// </summary>
        public static IEnumerable<Vector3I> GetRangeAxisQ(Vector3I center)
        {
            var tiles = new List<Vector3I>();

            // Shoot in +q direction (keeping r and s constant)
            for (int distance = 2; distance <= 5; distance++)
            {
                tiles.Add(new Vector3I(center.X + distance, center.Y, center.Z));
            }

            // Shoot in -q direction (keeping r and s constant)
            for (int distance = 2; distance <= 5; distance++)
            {
                tiles.Add(new Vector3I(center.X - distance, center.Y, center.Z));
            }

            return tiles;
        }

        /// <summary>
        /// Shoots along R axis only
        /// In cube coords: varying r, q and s constant
        /// </summary>
        public static IEnumerable<Vector3I> GetRangeAxisR(Vector3I center)
        {
            var tiles = new List<Vector3I>();

            // Shoot in +r direction (keeping q and s constant)
            for (int distance = 2; distance <= 5; distance++)
            {
                tiles.Add(new Vector3I(center.X, center.Y + distance, center.Z));
            }

            // Shoot in -r direction (keeping q and s constant)
            for (int distance = 2; distance <= 5; distance++)
            {
                tiles.Add(new Vector3I(center.X, center.Y - distance, center.Z));
            }

            return tiles;
        }

        /// <summary>
        /// Shoots along S axis only
        /// In cube coords: varying s, q and r constant
        /// </summary>
        public static IEnumerable<Vector3I> GetRangeAxisS(Vector3I center)
        {
            var tiles = new List<Vector3I>();

            // Shoot in +s direction (keeping q and r constant)
            for (int distance = 2; distance <= 5; distance++)
            {
                tiles.Add(new Vector3I(center.X, center.Y, center.Z + distance));
            }

            // Shoot in -s direction (keeping q and r constant)
            for (int distance = 2; distance <= 5; distance++)
            {
                tiles.Add(new Vector3I(center.X, center.Y, center.Z - distance));
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
