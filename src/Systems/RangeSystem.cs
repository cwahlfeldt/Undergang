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

            Entities.Query<Unit>()
                .ToList()
                .ForEach(e =>
                {
                    if (e.Has<Grunt>() || e.Has<Player>())
                        e.Add(new RangeCircle());
                });

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
                    if (u.Has<RangeCircle>())
                    {
                        var coordsInRange = GetRangeCircle(u.Get<Coordinate>()).ToList();
                        coordsInRange.ForEach(coord =>
                        {
                            var tile = Entities.GetAt(coord);
                            if (tile != null && tile.Has<Traversable>())
                                tile.Add(new AttackRangeTile(u.Id));
                        });
                    }
                });
        }

        // Range Functions
        public static IEnumerable<Vector3I> GetRangeCircle(Vector3I center)
        {
            return HexGrid.Directions.Values.Select(dir => center + dir);
        }

        public override void Cleanup()
        {
            Events.UnitDefeated -= OnUnitDefeated;
            Events.MoveCompleted -= OnMoveCompleted;
        }
    }
}
